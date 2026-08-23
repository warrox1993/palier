using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Palier.Api.Entrainement;
using Palier.Application.Entrainement;
using Palier.Application.Pipeline;
using Palier.Infrastructure.Entrainement;
using Palier.Infrastructure.Identite;

namespace Palier.Database.Tests;

/// <summary>
/// Les contraintes déclarées, sur le moteur réel — table NOUVELLE du lot 5.
/// </summary>
/// <remarks>
/// <b>Ces données relèvent de l'article 9.</b> Une contrainte cervicale ou
/// lombaire est une information de santé : la politique de cette table appelle
/// l'accesseur qui LÈVE, sans branche publique, contrairement à
/// <c>exercises</c>. Une épreuve d'isolation le vérifie dans les deux sens —
/// lecture et écriture.
/// </remarks>
[Collection(BaseFixture.Collection)]
public sealed class EntrainementContraintesTests(BaseFixture baseDeDonnees)
{
    private const string _motDePasse = "brouette-hivernale-38-oscille";

    // ================================================================
    // Déclarer
    // ================================================================

    [Fact]
    public async Task Une_declaration_se_relit()
    {
        var proprietaire = await CompteAsync();
        await using var hote = Hote(proprietaire);
        using var portee = hote.Services.CreateScope();

        var (code, _) = await RemplacerAsync(portee.ServiceProvider, ["cervicale", "genou"]);
        Assert.Equal(StatusCodes.Status200OK, code);

        var regions = await ListerAsync(portee.ServiceProvider);
        Assert.Equal(["cervicale", "genou"], regions);
    }

    [Fact]
    public async Task Le_remplacement_RETIRE_ce_qui_n_est_plus_dans_la_liste()
    {
        // C'est ce qui fait de `PUT` le bon verbe : déclarer ses contraintes
        // est un ÉTAT. Un `POST` qui n'aurait fait qu'ajouter aurait laissé
        // l'utilisateur sans geste évident pour retirer une contrainte guérie.
        var proprietaire = await CompteAsync();
        await using var hote = Hote(proprietaire);
        using var portee = hote.Services.CreateScope();

        await RemplacerAsync(portee.ServiceProvider, ["cervicale", "genou", "epaule"]);
        await RemplacerAsync(portee.ServiceProvider, ["genou"]);

        Assert.Equal(["genou"], await ListerAsync(portee.ServiceProvider));
    }

    [Fact]
    public async Task Une_liste_VIDE_retire_TOUT()
    {
        var proprietaire = await CompteAsync();
        await using var hote = Hote(proprietaire);
        using var portee = hote.Services.CreateScope();

        await RemplacerAsync(portee.ServiceProvider, ["cervicale", "lombaire"]);
        var (code, _) = await RemplacerAsync(portee.ServiceProvider, []);

        Assert.Equal(StatusCodes.Status200OK, code);
        Assert.Empty(await ListerAsync(portee.ServiceProvider));
    }

    [Fact]
    public async Task Redeclarer_la_MEME_liste_ne_change_pas_la_date()
    {
        // `declared_at` dira un jour depuis QUAND une contrainte dure. Un
        // `DELETE` puis `INSERT` de la liste entière serait plus court à
        // écrire, mais il remettrait la date à zéro sur des contraintes que
        // l'utilisateur n'a pas touchées — et la date dirait alors « depuis le
        // dernier enregistrement » au lieu de « depuis quand ».
        var proprietaire = await CompteAsync();
        await using var hote = Hote(proprietaire);
        using var portee = hote.Services.CreateScope();

        await RemplacerAsync(
            portee.ServiceProvider,
            ["genou"],
            new HorlogeFixe(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero))
        );

        await RemplacerAsync(
            portee.ServiceProvider,
            ["genou", "epaule"],
            new HorlogeFixe(new DateTimeOffset(2026, 8, 23, 0, 0, 0, TimeSpan.Zero))
        );

        var (_, corps) = await HarnaisHttp.ExecuterAsync(
            portee.ServiceProvider,
            ContraintesDuCompte.ListerAsync(
                portee.ServiceProvider.GetRequiredService<IExecuteurDeCasDUsage>(),
                portee.ServiceProvider.GetRequiredService<ListerLesContraintes>(),
                CancellationToken.None
            )
        );

        var dates = JsonDocument
            .Parse(corps)
            .RootElement.EnumerateArray()
            .ToDictionary(
                c => c.GetProperty("region").GetString()!,
                c => c.GetProperty("declareeLe").GetDateTimeOffset(),
                StringComparer.Ordinal
            );

        Assert.Equal(2026, dates["genou"].Year);
        Assert.Equal(1, dates["genou"].Month);
        Assert.Equal(8, dates["epaule"].Month);
    }

    [Fact]
    public async Task Les_DOUBLONS_sont_dedupliques_a_l_ecriture()
    {
        // La contrainte `unique (owner_id, region)` refuserait la seconde
        // insertion — un 500. La déduplication tombe avant.
        var proprietaire = await CompteAsync();
        await using var hote = Hote(proprietaire);
        using var portee = hote.Services.CreateScope();

        var (code, _) = await RemplacerAsync(
            portee.ServiceProvider,
            ["genou", "GENOU", "genou"]
        );

        Assert.Equal(StatusCodes.Status200OK, code);
        Assert.Equal(["genou"], await ListerAsync(portee.ServiceProvider));
    }

    // ================================================================
    // Retirer une seule
    // ================================================================

    [Fact]
    public async Task Retirer_UNE_contrainte_laisse_les_autres()
    {
        var proprietaire = await CompteAsync();
        await using var hote = Hote(proprietaire);
        using var portee = hote.Services.CreateScope();

        await RemplacerAsync(portee.ServiceProvider, ["cervicale", "genou"]);

        var (code, _) = await RetirerAsync(portee.ServiceProvider, "genou");

        Assert.Equal(StatusCodes.Status204NoContent, code);
        Assert.Equal(["cervicale"], await ListerAsync(portee.ServiceProvider));
    }

    [Fact]
    public async Task Retirer_une_contrainte_NON_DECLAREE_rend_404()
    {
        var proprietaire = await CompteAsync();
        await using var hote = Hote(proprietaire);
        using var portee = hote.Services.CreateScope();

        var (code, corps) = await RetirerAsync(portee.ServiceProvider, "genou");

        Assert.Equal(StatusCodes.Status404NotFound, code);
        Assert.Equal("ContrainteNonDeclaree", HarnaisHttp.Code(corps));
    }

    [Fact]
    public async Task Retirer_une_region_INCONNUE_rend_400_et_non_500()
    {
        // La région vient de l'URL, donc de l'extérieur. Sans validation,
        // `Contraintes.EnBase` lèverait plus bas — un 500 pour une URL mal
        // tapée.
        var proprietaire = await CompteAsync();
        await using var hote = Hote(proprietaire);
        using var portee = hote.Services.CreateScope();

        var (code, corps) = await RetirerAsync(portee.ServiceProvider, "poignet");

        Assert.Equal(StatusCodes.Status400BadRequest, code);
        Assert.Equal("ContrainteInvalide", HarnaisHttp.Code(corps));
    }

    // ================================================================
    // L'isolation, dans LES DEUX SENS
    // ================================================================

    [Fact]
    public async Task Les_contraintes_de_B_ne_portent_AUCUNE_declaration_de_A()
    {
        var a = await CompteAsync();
        var b = await CompteAsync();

        await using (var hoteA = Hote(a))
        {
            using var porteeA = hoteA.Services.CreateScope();
            await RemplacerAsync(porteeA.ServiceProvider, ["cervicale"]);
        }

        await using var hoteB = Hote(b);
        using var porteeB = hoteB.Services.CreateScope();
        await RemplacerAsync(porteeB.ServiceProvider, ["genou"]);

        Assert.Equal(["genou"], await ListerAsync(porteeB.ServiceProvider));
    }

    [Fact]
    public async Task Le_remplacement_de_B_n_EFFACE_PAS_les_contraintes_de_A()
    {
        // LE CAS QUI COMPTE. Le remplacement supprime « tout ce qui n'est plus
        // dans la liste » — sans RLS, ce « tout » serait celui de la table
        // entière, et déclarer une contrainte effacerait celles de tous les
        // autres utilisateurs. Une perte de données silencieuse chez des tiers,
        // sur une donnée de santé.
        var a = await CompteAsync();
        var b = await CompteAsync();

        await using (var hoteA = Hote(a))
        {
            using var porteeA = hoteA.Services.CreateScope();
            await RemplacerAsync(porteeA.ServiceProvider, ["cervicale", "lombaire"]);
        }

        await using (var hoteB = Hote(b))
        {
            using var porteeB = hoteB.Services.CreateScope();
            await RemplacerAsync(porteeB.ServiceProvider, ["genou"]);
        }

        await using var hoteA2 = Hote(a);
        using var porteeA2 = hoteA2.Services.CreateScope();
        Assert.Equal(["cervicale", "lombaire"], await ListerAsync(porteeA2.ServiceProvider));

        // ET la table porte bien les trois lignes : ce que seul le rôle des
        // migrations peut voir.
        Assert.Equal(3, await CompterToutAsync());
    }

    // ================================================================
    // Ce que ce lot ne fait PAS — l'épreuve INVERSÉE
    // ================================================================

    [Fact]
    public async Task Declarer_une_contrainte_ne_FILTRE_PAS_encore_le_catalogue()
    {
        // ÉPREUVE INVERSÉE, et son rôle est de ROUGIR le jour où le filtrage
        // arrivera.
        //
        // `docs/05-entrainement.md` § 4 décrit un filtrage automatique par
        // intersection avec `exercises.contraindicated_for`. Mais il laisse
        // ouvert ce qu'on fait d'un exercice contre-indiqué : l'EXCLURE du
        // catalogue, ou l'AFFICHER MARQUÉ. C'est une décision de produit
        // visible par l'utilisateur — `CLAUDE.md` § 6 — donc elle appartient au
        // porteur du projet.
        //
        // Sans cette épreuve, le filtrage s'installerait un jour au détour
        // d'une implémentation, dans la forme que quelqu'un aura trouvée
        // évidente, et personne n'aurait jamais posé la question. Elle force à
        // venir ici et à décider.
        var proprietaire = await CompteAsync();
        var contreIndique = await ExerciceContreIndiqueAsync("genou");
        await using var hote = Hote(proprietaire);
        using var portee = hote.Services.CreateScope();

        await RemplacerAsync(portee.ServiceProvider, ["genou"]);

        var (code, corps) = await HarnaisHttp.ExecuterAsync(
            portee.ServiceProvider,
            Catalogue.ListerAsync(
                portee.ServiceProvider.GetRequiredService<IExecuteurDeCasDUsage>(),
                portee.ServiceProvider.GetRequiredService<ListerLeCatalogue>(),
                CancellationToken.None
            )
        );

        Assert.Equal(StatusCodes.Status200OK, code);
        var identifiants = JsonDocument
            .Parse(corps)
            .RootElement.EnumerateArray()
            .Select(e => e.GetProperty("id").GetGuid())
            .ToHashSet();

        Assert.True(
            identifiants.Contains(contreIndique),
            "L'exercice contre-indiqué a disparu du catalogue : le filtrage du § 4 a été "
                + "implémenté. C'est peut-être la bonne décision — mais elle appartient au "
                + "porteur du projet (exclure, ou afficher marqué), et cette épreuve existe "
                + "pour qu'elle soit prise plutôt que subie. Voir le § 8 de la conception."
        );
    }

    // ================================================================
    // Le harnais
    // ================================================================

    private WebApplication Hote(Guid proprietaire) =>
        HarnaisHttp.Hote(
            baseDeDonnees,
            ajuster: services =>
                services.AddScoped<IIdentiteDemandeur>(_ => new DemandeurFixe(proprietaire))
        );

    private static Task<(int Code, string Corps)> RemplacerAsync(
        IServiceProvider services,
        IReadOnlyList<string> regions,
        TimeProvider? horloge = null
    ) =>
        HarnaisHttp.ExecuterAsync(
            services,
            ContraintesDuCompte.RemplacerAsync(
                new DeclarationDeContraintes(regions),
                services.GetRequiredService<IExecuteurDeCasDUsage>(),
                services.GetRequiredService<RemplacerLesContraintes>(),
                horloge ?? TimeProvider.System,
                CancellationToken.None
            )
        );

    private static Task<(int Code, string Corps)> RetirerAsync(
        IServiceProvider services,
        string region
    ) =>
        HarnaisHttp.ExecuterAsync(
            services,
            ContraintesDuCompte.RetirerAsync(
                region,
                services.GetRequiredService<IExecuteurDeCasDUsage>(),
                services.GetRequiredService<RetirerUneContrainte>(),
                CancellationToken.None
            )
        );

    private static async Task<IReadOnlyList<string>> ListerAsync(IServiceProvider services)
    {
        var (code, corps) = await HarnaisHttp.ExecuterAsync(
            services,
            ContraintesDuCompte.ListerAsync(
                services.GetRequiredService<IExecuteurDeCasDUsage>(),
                services.GetRequiredService<ListerLesContraintes>(),
                CancellationToken.None
            )
        );

        Assert.Equal(StatusCodes.Status200OK, code);
        return
        [
            .. JsonDocument
                .Parse(corps)
                .RootElement.EnumerateArray()
                .Select(c => c.GetProperty("region").GetString()!),
        ];
    }

    private async Task<long> CompterToutAsync()
    {
        await using var connexion = new NpgsqlConnection(baseDeDonnees.ChaineMigrations);
        await connexion.OpenAsync();
        await using var commande = new NpgsqlCommand(
            "select count(*) from public.user_constraints",
            connexion
        );
        return (long)(await commande.ExecuteScalarAsync())!;
    }

    private async Task<Guid> ExerciceContreIndiqueAsync(string region)
    {
        await using var connexion = new NpgsqlConnection(baseDeDonnees.ChaineMigrations);
        await connexion.OpenAsync();
        await using var commande = new NpgsqlCommand(
            """
            insert into public.exercises
                   (name, primary_muscles, contraindicated_for, is_custom, owner_id)
            values ($1, array['quadriceps'], array[$2], false, null) returning id
            """,
            connexion
        );
        commande.Parameters.AddWithValue(
            $"fentes-{Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)}"
        );
        commande.Parameters.AddWithValue(region);
        return (Guid)(await commande.ExecuteScalarAsync())!;
    }

    private async Task<Guid> CompteAsync()
    {
        await using var hote = HarnaisHttp.Hote(baseDeDonnees);
        using var portee = hote.Services.CreateScope();
        var utilisateurs = portee.ServiceProvider.GetRequiredService<UserManager<Utilisateur>>();

        var email =
            "contrainte-"
            + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)
            + "@exemple.test";
        var compte = new Utilisateur
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
        };

        var resultat = await utilisateurs.CreateAsync(compte, _motDePasse);
        Assert.True(
            resultat.Succeeded,
            "le harnais n'a pas pu créer l'utilisateur : "
                + string.Join(", ", resultat.Errors.Select(e => e.Code))
        );

        return compte.Id;
    }
}
