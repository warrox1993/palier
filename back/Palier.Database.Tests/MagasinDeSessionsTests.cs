using Npgsql;
using Palier.Application.Sessions;
using Palier.Infrastructure.Identite;

namespace Palier.Database.Tests;

/// <summary>
/// Le magasin de sessions, sur le moteur réel et sous le rôle réel.
///
/// <para>
/// Ces épreuves tournent sous <c>palier_auth</c>, jamais sous le compte
/// d'administration : une suite lancée sous l'administrateur passerait
/// intégralement au vert et ne démontrerait rien du chemin que D38 laissait à
/// concevoir.
/// </para>
/// </summary>
[Collection(BaseFixture.Collection)]
public sealed class MagasinDeSessionsTests(BaseFixture baseDeDonnees)
{
    private static readonly DateTimeOffset _maintenant = new(2026, 8, 21, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Ouvrir_rend_un_jeton_utilisable_et_range_son_empreinte()
    {
        var utilisateur = await UtilisateurAsync();
        await using var contexte = BaseFixture.ContexteAuth(baseDeDonnees.ChaineAuth);
        var magasin = new MagasinDeSessions(contexte);

        var ouverture = await magasin.OuvrirAsync(utilisateur, "Firefox/141", _maintenant);

        Assert.False(string.IsNullOrWhiteSpace(ouverture.Jeton));

        // Le jeton lui-même n'est nulle part : seule son empreinte est rangée.
        var enClair = await CompterAsync(
            "select count(*) from public.sessions_refresh where token_hash = $1",
            System.Text.Encoding.UTF8.GetBytes(ouverture.Jeton)
        );
        Assert.True(enClair == 0, "le jeton est rangé en clair dans la colonne d'empreinte");

        var parEmpreinte = await CompterAsync(
            "select count(*) from public.sessions_refresh where token_hash = $1",
            HachageDeJeton.Calculer(ouverture.Jeton)
        );
        Assert.True(parEmpreinte == 1, $"l'empreinte n'est pas rangée : {parEmpreinte} ligne(s)");
    }

    [Fact]
    public async Task Une_rotation_valide_consomme_l_ancien_et_en_rend_un_neuf()
    {
        var utilisateur = await UtilisateurAsync();
        await using var contexte = BaseFixture.ContexteAuth(baseDeDonnees.ChaineAuth);
        var magasin = new MagasinDeSessions(contexte);

        var ouverture = await magasin.OuvrirAsync(utilisateur, "Firefox/141", _maintenant);
        var rotation = await magasin.FaireTournerAsync(
            ouverture.Jeton,
            "Firefox/141",
            _maintenant.AddMinutes(10)
        );

        Assert.Equal(IssueDeRotation.Acceptee, rotation.Issue);
        Assert.False(string.IsNullOrWhiteSpace(rotation.Jeton));
        Assert.NotEqual(ouverture.Jeton, rotation.Jeton);

        // La chaîne reste dans la même famille : c'est elle qui tombera en bloc
        // si un réemploi est détecté plus tard.
        Assert.Equal(ouverture.Famille, rotation.Famille);
    }

    // LA branche qui protège les innocents.
    [Fact]
    public async Task Un_rejeu_dans_la_grace_rend_un_jeton_utilisable_sans_revoquer()
    {
        var utilisateur = await UtilisateurAsync();
        await using var contexte = BaseFixture.ContexteAuth(baseDeDonnees.ChaineAuth);
        var magasin = new MagasinDeSessions(contexte);

        var ouverture = await magasin.OuvrirAsync(utilisateur, "Firefox/141", _maintenant);
        var premiere = await magasin.FaireTournerAsync(ouverture.Jeton, null, _maintenant);

        // Le second onglet rejoue le même jeton, vingt secondes plus tard.
        var seconde = await magasin.FaireTournerAsync(
            ouverture.Jeton,
            null,
            _maintenant.AddSeconds(20)
        );

        Assert.Equal(IssueDeRotation.RejeuDansLaGrace, seconde.Issue);

        // Il ne peut PAS s'agir du même jeton que la première requête : seule
        // l'empreinte est rangée, la valeur n'est pas reconstructible. Ce qui
        // compte est que le second appelant reparte avec un jeton utilisable,
        // dans la même famille.
        Assert.False(string.IsNullOrWhiteSpace(seconde.Jeton));
        Assert.NotEqual(premiere.Jeton, seconde.Jeton);
        Assert.Equal(premiere.Famille, seconde.Famille);

        // Et surtout : la famille est INTACTE. L'utilisateur n'a pas été
        // déconnecté pour avoir ouvert deux onglets.
        var vivantes = await CompterSessionsVivantesAsync(utilisateur);
        Assert.True(vivantes > 0, "la famille a été révoquée sur un simple rejeu concurrent");
    }

    // LA branche qui attrape les voleurs.
    [Fact]
    public async Task Un_rejeu_hors_grace_revoque_TOUTE_la_famille()
    {
        var utilisateur = await UtilisateurAsync();
        await using var contexte = BaseFixture.ContexteAuth(baseDeDonnees.ChaineAuth);
        var magasin = new MagasinDeSessions(contexte);

        var ouverture = await magasin.OuvrirAsync(utilisateur, "Firefox/141", _maintenant);
        var apres = await magasin.FaireTournerAsync(ouverture.Jeton, null, _maintenant);

        // Le voleur rejoue l'ancien jeton, cinq minutes après sa consommation.
        var vol = await magasin.FaireTournerAsync(
            ouverture.Jeton,
            null,
            _maintenant.AddMinutes(5)
        );

        Assert.Equal(IssueDeRotation.ReemploiDetecte, vol.Issue);
        Assert.Null(vol.Jeton);

        // Le jeton que le porteur légitime détenait ne vaut plus rien non plus :
        // c'est le prix, et c'est voulu — lequel des deux se présente est
        // indécidable.
        var apresRevocation = await magasin.FaireTournerAsync(
            apres.Jeton!,
            null,
            _maintenant.AddMinutes(6)
        );
        Assert.Equal(IssueDeRotation.Revoquee, apresRevocation.Issue);
    }

    [Fact]
    public async Task Un_jeton_inconnu_ne_revoque_rien()
    {
        await using var contexte = BaseFixture.ContexteAuth(baseDeDonnees.ChaineAuth);
        var magasin = new MagasinDeSessions(contexte);

        var resultat = await magasin.FaireTournerAsync("jeton-qui-n-existe-pas", null, _maintenant);

        Assert.Equal(IssueDeRotation.Inconnue, resultat.Issue);
        Assert.Null(resultat.Jeton);
    }

    [Fact]
    public async Task La_deconnexion_totale_revoque_toutes_les_familles()
    {
        var utilisateur = await UtilisateurAsync();
        await using var contexte = BaseFixture.ContexteAuth(baseDeDonnees.ChaineAuth);
        var magasin = new MagasinDeSessions(contexte);

        var premiere = await magasin.OuvrirAsync(utilisateur, "Firefox/141", _maintenant);
        var seconde = await magasin.OuvrirAsync(utilisateur, "Safari/26", _maintenant);
        Assert.NotEqual(premiere.Famille, seconde.Famille);

        await magasin.RevoquerToutesAsync(utilisateur, _maintenant);

        // Immédiat, contrairement au SecurityStamp d'Identity dont l'effet est
        // borné par l'intervalle de revalidation.
        Assert.Equal(
            IssueDeRotation.Revoquee,
            (await magasin.FaireTournerAsync(premiere.Jeton, null, _maintenant)).Issue
        );
        Assert.Equal(
            IssueDeRotation.Revoquee,
            (await magasin.FaireTournerAsync(seconde.Jeton, null, _maintenant)).Issue
        );
    }

    [Fact]
    public async Task Lister_ne_rend_que_les_sessions_vivantes_de_l_utilisateur()
    {
        var utilisateur = await UtilisateurAsync();
        var autre = await UtilisateurAsync();
        await using var contexte = BaseFixture.ContexteAuth(baseDeDonnees.ChaineAuth);
        var magasin = new MagasinDeSessions(contexte);

        await magasin.OuvrirAsync(utilisateur, "Firefox/141", _maintenant);
        await magasin.OuvrirAsync(autre, "Safari/26", _maintenant);

        var siennes = await magasin.ListerAsync(utilisateur, _maintenant);

        Assert.True(siennes.Count == 1, $"{siennes.Count} session(s) rendues au lieu d'une");
        Assert.Equal("Firefox/141", siennes[0].Appareil);
    }

    [Fact]
    public async Task La_purge_supprime_les_sessions_eteintes_et_epargne_les_vivantes()
    {
        var utilisateur = await UtilisateurAsync();
        await using var contexte = BaseFixture.ContexteAuth(baseDeDonnees.ChaineAuth);
        var magasin = new MagasinDeSessions(contexte);

        var vieille = await magasin.OuvrirAsync(utilisateur, "vieille", _maintenant.AddDays(-30));
        var vivante = await magasin.OuvrirAsync(utilisateur, "vivante", _maintenant);

        var supprimees = await magasin.PurgerAsync(_maintenant, CancellationToken.None);

        Assert.True(supprimees >= 1, "la purge n'a rien supprimé alors qu'une session a expiré");
        Assert.Equal(
            IssueDeRotation.Inconnue,
            (await magasin.FaireTournerAsync(vieille.Jeton, null, _maintenant)).Issue
        );
        Assert.Equal(
            IssueDeRotation.Acceptee,
            (await magasin.FaireTournerAsync(vivante.Jeton, null, _maintenant)).Issue
        );
    }

    [Fact]
    public async Task La_purge_est_idempotente()
    {
        var utilisateur = await UtilisateurAsync();
        await using var contexte = BaseFixture.ContexteAuth(baseDeDonnees.ChaineAuth);
        var magasin = new MagasinDeSessions(contexte);

        await magasin.OuvrirAsync(utilisateur, "vieille", _maintenant.AddDays(-30));
        await magasin.PurgerAsync(_maintenant, CancellationToken.None);

        var second = await magasin.PurgerAsync(_maintenant, CancellationToken.None);

        Assert.True(second == 0, $"le second passage a encore supprimé {second} ligne(s)");
    }

    private async Task<Guid> UtilisateurAsync()
    {
        var identifiant = Guid.NewGuid();
        await using var connexion = new NpgsqlDataSourceBuilder(baseDeDonnees.ChaineAdministrateur)
            .Build()
            .CreateConnection();
        await connexion.OpenAsync();
        await using var commande = new NpgsqlCommand(
            """
            insert into public."AspNetUsers" ("Id", "EmailConfirmed", "PhoneNumberConfirmed",
                   "TwoFactorEnabled", "LockoutEnabled", "AccessFailedCount")
            values ($1, false, false, false, false, 0)
            """,
            connexion
        );
        commande.Parameters.AddWithValue(identifiant);
        await commande.ExecuteNonQueryAsync();
        return identifiant;
    }

    private async Task<long> CompterAsync(string requete, byte[] empreinte)
    {
        await using var connexion = new NpgsqlDataSourceBuilder(baseDeDonnees.ChaineAdministrateur)
            .Build()
            .CreateConnection();
        await connexion.OpenAsync();
#pragma warning disable CA2100 // La requête vient des appelants de ce fichier, tous littéraux.
        await using var commande = new NpgsqlCommand(requete, connexion);
#pragma warning restore CA2100
        commande.Parameters.AddWithValue(empreinte);
        return (long)(await commande.ExecuteScalarAsync())!;
    }

    private async Task<long> CompterSessionsVivantesAsync(Guid utilisateur)
    {
        await using var connexion = new NpgsqlDataSourceBuilder(baseDeDonnees.ChaineAdministrateur)
            .Build()
            .CreateConnection();
        await connexion.OpenAsync();
        await using var commande = new NpgsqlCommand(
            "select count(*) from public.sessions_refresh "
                + "where owner_id = $1 and revoked_at is null",
            connexion
        );
        commande.Parameters.AddWithValue(utilisateur);
        return (long)(await commande.ExecuteScalarAsync())!;
    }
}
