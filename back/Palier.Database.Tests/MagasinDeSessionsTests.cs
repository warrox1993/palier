using Npgsql;
using NpgsqlTypes;
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

        // C'est LE MÊME jeton que la première requête, et cette égalité est le
        // cœur de la correction. Cette épreuve affirmait l'inverse — « seule
        // l'empreinte est rangée, la valeur n'est pas reconstructible » — et
        // c'était exact de l'empreinte, mais le successeur est désormais scellé
        // sous le jeton présenté, donc restituable à qui le présente. Rendre un
        // jeton NEUF laissait deux chaînes vivantes dans une même famille : plus
        // aucun jeton consommé n'était jamais représenté, donc plus aucun
        // réemploi détecté.
        Assert.False(string.IsNullOrWhiteSpace(seconde.Jeton));
        Assert.Equal(premiere.Jeton, seconde.Jeton);
        Assert.Equal(premiere.Famille, seconde.Famille);

        // Et surtout : la famille est INTACTE. L'utilisateur n'a pas été
        // déconnecté pour avoir ouvert deux onglets.
        var vivantes = await CompterSessionsVivantesAsync(utilisateur);
        Assert.True(vivantes > 0, "la famille a été révoquée sur un simple rejeu concurrent");
    }

    // LA branche qui attrape les voleurs — celle que le rejeu dans la grâce
    // éteignait.
    [Fact]
    public async Task Un_rejeu_dans_la_grace_ne_laisse_PAS_deux_chaines_renouvelables()
    {
        // Le magasin émettait ici un jeton NEUF au lieu de rendre le successeur
        // déjà émis. La famille repartait alors sur DEUX chaînes vivantes —
        // celle de la victime, celle du voleur — qui ne se recroisaient plus
        // jamais : aucun jeton consommé n'était représenté, donc aucun réemploi
        // n'était détecté, et le voleur renouvelait son accès jusqu'à ce que le
        // compte soit fermé.
        var utilisateur = await UtilisateurAsync();
        await using var contexte = BaseFixture.ContexteAuth(baseDeDonnees.ChaineAuth);
        var magasin = new MagasinDeSessions(contexte);

        var ouverture = await magasin.OuvrirAsync(utilisateur, "victime", _maintenant);
        var victime = await magasin.FaireTournerAsync(ouverture.Jeton, "victime", _maintenant);

        // Le voleur rejoue le jeton volé DANS la grâce. Il repart avec le
        // successeur de la victime, pas avec une chaîne à lui.
        var voleur = await magasin.FaireTournerAsync(
            ouverture.Jeton,
            "voleur",
            _maintenant.AddSeconds(10)
        );

        Assert.Equal(IssueDeRotation.RejeuDansLaGrace, voleur.Issue);
        Assert.Equal(victime.Jeton, voleur.Jeton);

        // Il fait tourner ce qu'il détient, une heure plus tard.
        var suite = await magasin.FaireTournerAsync(
            voleur.Jeton!,
            "voleur",
            _maintenant.AddHours(1)
        );
        Assert.Equal(IssueDeRotation.Acceptee, suite.Issue);

        // Et c'est là que tout se joue : la victime présente SON jeton, que le
        // voleur vient de consommer. Le croisement se voit, la famille tombe.
        // Avec deux chaînes parallèles, cette issue était `Acceptee` et le vol
        // ne se voyait jamais.
        var croisement = await magasin.FaireTournerAsync(
            victime.Jeton!,
            "victime",
            _maintenant.AddHours(2)
        );
        Assert.Equal(IssueDeRotation.ReemploiDetecte, croisement.Issue);

        // Y compris la chaîne du voleur : c'est le prix de l'indécidabilité.
        var apres = await magasin.FaireTournerAsync(
            suite.Jeton!,
            "voleur",
            _maintenant.AddHours(3)
        );
        Assert.Equal(IssueDeRotation.Revoquee, apres.Issue);

        var vivantes = await CompterSessionsVivantesAsync(utilisateur);
        Assert.True(vivantes == 0, $"{vivantes} session(s) ont survécu au réemploi détecté");
    }

    [Fact]
    public async Task Un_SCEAU_ILLISIBLE_refuse_au_lieu_d_ouvrir_une_seconde_chaine()
    {
        // Trois états qu'une ligne consommée peut porter et qu'aucun rejeu ne
        // doit convertir en jeton neuf : pas de sceau — une ligne consommée
        // avant que la colonne existe —, un sceau tronqué, un sceau forgé.
        // Sans ce refus, le repli rouvrirait exactement le trou que le sceau
        // ferme.
        byte[]?[] sceaux = [null, new byte[5], new byte[40]];
        await using var contexte = BaseFixture.ContexteAuth(baseDeDonnees.ChaineAuth);
        var magasin = new MagasinDeSessions(contexte);

        foreach (var sceau in sceaux)
        {
            var utilisateur = await UtilisateurAsync();
            var ouverture = await magasin.OuvrirAsync(utilisateur, null, _maintenant);
            await magasin.FaireTournerAsync(ouverture.Jeton, null, _maintenant);
            await AbimerLeSceauAsync(HachageDeJeton.Calculer(ouverture.Jeton), sceau);

            var rejeu = await magasin.FaireTournerAsync(
                ouverture.Jeton,
                null,
                _maintenant.AddSeconds(10)
            );

            Assert.Null(rejeu.Jeton);
            Assert.Null(rejeu.Utilisateur);

            // Et rien n'a été inscrit : la famille en reste à ses deux lignes.
            var lignes = await CompterLignesAsync(utilisateur);
            Assert.True(lignes == 2, $"{lignes} ligne(s) dans la famille au lieu de deux");
        }
    }

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

    [Fact]
    public async Task Une_rotation_ACCEPTEE_nomme_l_utilisateur()
    {
        // Le point d'entrée doit signer un jeton d'accès APRÈS la rotation : il
        // lui faut donc l'identifiant. Le lui faire relire en base l'obligerait
        // à toucher le contexte d'identité, que `ArchitectureTests` lui refuse.
        var utilisateur = await UtilisateurAsync();
        await using var contexte = BaseFixture.ContexteAuth(baseDeDonnees.ChaineAuth);
        var magasin = new MagasinDeSessions(contexte);

        var ouverture = await magasin.OuvrirAsync(utilisateur, null, _maintenant);
        var rotation = await magasin.FaireTournerAsync(ouverture.Jeton, null, _maintenant);

        Assert.Equal(IssueDeRotation.Acceptee, rotation.Issue);
        Assert.Equal(utilisateur, rotation.Utilisateur);
    }

    [Fact]
    public async Task Un_rejeu_DANS_LA_GRACE_nomme_aussi_l_utilisateur()
    {
        var utilisateur = await UtilisateurAsync();
        await using var contexte = BaseFixture.ContexteAuth(baseDeDonnees.ChaineAuth);
        var magasin = new MagasinDeSessions(contexte);

        var ouverture = await magasin.OuvrirAsync(utilisateur, null, _maintenant);
        await magasin.FaireTournerAsync(ouverture.Jeton, null, _maintenant);

        // Le même jeton, dix secondes plus tard : dans la grâce.
        var rejeu = await magasin.FaireTournerAsync(
            ouverture.Jeton,
            null,
            _maintenant.AddSeconds(10)
        );

        Assert.Equal(IssueDeRotation.RejeuDansLaGrace, rejeu.Issue);
        Assert.Equal(utilisateur, rejeu.Utilisateur);
    }

    [Fact]
    public async Task Une_issue_SANS_JETON_ne_nomme_aucun_utilisateur()
    {
        // La contrepartie : un refus ne doit pas laisser filtrer à qui
        // appartenait la session. Un appelant qui lirait `Utilisateur` sur un
        // refus signerait un jeton d'accès pour quelqu'un qui vient d'être
        // déconnecté.
        await using var contexte = BaseFixture.ContexteAuth(baseDeDonnees.ChaineAuth);
        var magasin = new MagasinDeSessions(contexte);

        var inconnue = await magasin.FaireTournerAsync("un-jeton-qui-n-existe-pas", null, _maintenant);

        Assert.Equal(IssueDeRotation.Inconnue, inconnue.Issue);
        Assert.Null(inconnue.Utilisateur);
        Assert.Null(inconnue.Jeton);
    }

    [Fact]
    public async Task Revoquer_la_FAMILLE_coupe_toute_la_chaine()
    {
        // Une déconnexion qui ne révoquerait que la session présentée laisserait
        // vivant le successeur déjà émis : l'utilisateur croirait s'être
        // déconnecté, et l'onglet resté ouvert continuerait de se rafraîchir.
        var utilisateur = await UtilisateurAsync();
        await using var contexte = BaseFixture.ContexteAuth(baseDeDonnees.ChaineAuth);
        var magasin = new MagasinDeSessions(contexte);

        var premiere = await magasin.OuvrirAsync(utilisateur, null, _maintenant);
        var seconde = await magasin.FaireTournerAsync(premiere.Jeton, null, _maintenant);
        Assert.NotNull(seconde.Jeton);

        var coupee = await magasin.RevoquerFamilleAsync(
            seconde.Jeton,
            _maintenant,
            CancellationToken.None
        );

        Assert.True(coupee, "la révocation n'a trouvé aucune session à couper");
        Assert.Equal(
            IssueDeRotation.Revoquee,
            (await magasin.FaireTournerAsync(seconde.Jeton, null, _maintenant)).Issue
        );
    }

    [Fact]
    public async Task Revoquer_une_famille_INCONNUE_rend_faux()
    {
        // Quatrième question du franchissement : un contrôle qui n'a plus de
        // cible doit le DIRE. Sans ce retour, une déconnexion sur un cookie
        // périmé rendrait « c'est fait » sans que rien n'ait été fait.
        await using var contexte = BaseFixture.ContexteAuth(baseDeDonnees.ChaineAuth);
        var magasin = new MagasinDeSessions(contexte);

        Assert.False(
            await magasin.RevoquerFamilleAsync(
                "un-jeton-qui-n-existe-pas",
                _maintenant,
                CancellationToken.None
            )
        );
    }

    [Fact]
    public async Task Un_sceau_PERIME_est_EFFACE_de_la_base()
    {
        // Le sceau ne sert que pendant la fenêtre de grâce de sa ligne. Passé
        // ce délai, plus personne ne l'ouvrira par l'API — mais il resterait
        // déchiffrable QUATORZE JOURS pour qui lit la table.
        //
        // Ce n'est pas théorique : à partir du texte clair d'un jeton consommé
        // six heures plus tôt et d'une lecture de `sessions_refresh`, on remonte
        // de sceau en sceau jusqu'au jeton VIVANT. Sans cet effacement, le
        // scellement DONNERAIT une capacité que le code d'avant n'avait pas —
        // il fermerait un défaut en en ouvrant un pire.
        var utilisateur = await UtilisateurAsync();
        await using var contexte = BaseFixture.ContexteAuth(baseDeDonnees.ChaineAuth);
        var magasin = new MagasinDeSessions(contexte);

        var premier = await magasin.OuvrirAsync(utilisateur, null, _maintenant);
        var second = await magasin.FaireTournerAsync(premier.Jeton, null, _maintenant);
        Assert.NotNull(second.Jeton);

        // Le sceau du premier existe bien : sans cette assertion, l'épreuve
        // passerait sur une colonne restée vide et ne prouverait rien.
        Assert.True(
            await CompterSceauxAsync(utilisateur) >= 1,
            "aucun sceau n'a été écrit : l'épreuve ne regarde rien."
        );

        // Une rotation plus tard, bien au-delà de la grâce.
        await magasin.FaireTournerAsync(
            second.Jeton,
            null,
            _maintenant + ParametresDeSession.FenetreDeGrace + TimeSpan.FromMinutes(1)
        );

        Assert.True(
            await CompterSceauxAsync(utilisateur) <= 1,
            "un sceau périmé survit en base : il reste déchiffrable pendant les quatorze "
                + "jours de sa ligne, et les sceaux s'enchaînent jusqu'au jeton vivant."
        );
    }

    [Fact]
    public async Task La_purge_efface_les_sceaux_des_familles_DORMANTES()
    {
        // La rotation efface les sceaux de la famille qu'elle traverse. Une
        // famille qui ne tourne plus — l'utilisateur ne revient pas — garderait
        // les siens jusqu'à l'expiration de ses lignes.
        var utilisateur = await UtilisateurAsync();
        await using var contexte = BaseFixture.ContexteAuth(baseDeDonnees.ChaineAuth);
        var magasin = new MagasinDeSessions(contexte);

        var premier = await magasin.OuvrirAsync(utilisateur, null, _maintenant);
        await magasin.FaireTournerAsync(premier.Jeton, null, _maintenant);

        Assert.True(
            await CompterSceauxAsync(utilisateur) >= 1,
            "aucun sceau n'a été écrit : l'épreuve ne regarde rien."
        );

        await magasin.PurgerAsync(
            _maintenant + ParametresDeSession.FenetreDeGrace + TimeSpan.FromMinutes(1),
            CancellationToken.None
        );

        Assert.True(
            await CompterSceauxAsync(utilisateur) == 0,
            "la purge a laissé un sceau périmé derrière elle."
        );
    }

    private async Task<long> CompterSceauxAsync(Guid utilisateur)
    {
        await using var connexion = new NpgsqlDataSourceBuilder(baseDeDonnees.ChaineAdministrateur)
            .Build()
            .CreateConnection();
        await connexion.OpenAsync();
        await using var commande = new NpgsqlCommand(
            "select count(*) from public.sessions_refresh "
                + "where owner_id = $1 and successor_sealed is not null",
            connexion
        );
        commande.Parameters.AddWithValue(utilisateur);
        return (long)(await commande.ExecuteScalarAsync())!;
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

    /// <summary>Remplace le sceau du successeur, ou l'efface si <c>null</c>.</summary>
    private async Task AbimerLeSceauAsync(byte[] empreinte, byte[]? sceau)
    {
        await using var connexion = new NpgsqlDataSourceBuilder(baseDeDonnees.ChaineAdministrateur)
            .Build()
            .CreateConnection();
        await connexion.OpenAsync();
        await using var commande = new NpgsqlCommand(
            "update public.sessions_refresh set successor_sealed = $2 where token_hash = $1",
            connexion
        );
        commande.Parameters.AddWithValue(empreinte);
        commande.Parameters.AddWithValue(NpgsqlDbType.Bytea, (object?)sceau ?? DBNull.Value);
        await commande.ExecuteNonQueryAsync();
    }

    private async Task<long> CompterLignesAsync(Guid utilisateur)
    {
        await using var connexion = new NpgsqlDataSourceBuilder(baseDeDonnees.ChaineAdministrateur)
            .Build()
            .CreateConnection();
        await connexion.OpenAsync();
        await using var commande = new NpgsqlCommand(
            "select count(*) from public.sessions_refresh where owner_id = $1",
            connexion
        );
        commande.Parameters.AddWithValue(utilisateur);
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
