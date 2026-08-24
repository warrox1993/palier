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

        // ET les trois lignes sont bien là, ce que seul le rôle des migrations
        // peut voir : deux pour A, une pour B.
        Assert.Equal(3, await CompterPourAsync(a, b));
    }

    // ================================================================
    // L'adaptation par contrainte — le MARQUAGE, jamais le filtrage
    // ================================================================

    [Fact]
    public async Task Le_catalogue_rend_TOUT_et_MARQUE_ce_qui_est_contre_indique()
    {
        // D63, et c'est la conformité qui l'impose, pas le goût.
        //
        // `05-entrainement.md` § 4 emploie le mot « filtrent » ;
        // `00-produit.md` tranche ce qu'il peut vouloir dire — « voici ta
        // valeur, voici la référence, voici l'écart » est une information,
        // tandis que décider à la place de l'utilisateur « place l'éditeur en
        // conseiller, ce qui est réglementé ». Et `01-conformite.md` § 2 pose
        // la formule canonique : « un chiffre, une référence, un écart. JAMAIS
        // une action. »
        //
        // Retirer un exercice du catalogue EST une action. Cette épreuve garde
        // les DEUX moitiés : l'exercice est là, ET il est marqué.
        var proprietaire = await CompteAsync();
        var contreIndique = await ExerciceContreIndiqueAsync("genou");
        await using var hote = Hote(proprietaire);
        using var portee = hote.Services.CreateScope();

        await RemplacerAsync(portee.ServiceProvider, ["genou"], severite: "strict");

        var catalogue = await CatalogueAsync(portee.ServiceProvider);
        var exercice = catalogue.SingleOrDefault(e =>
            e.GetProperty("id").GetGuid() == contreIndique
        );

        // PREMIÈRE MOITIÉ : il est là. Un catalogue qui l'aurait retiré
        // mentirait sur son contenu, n'apprendrait rien, et prendrait une
        // décision médicale en silence pour quelqu'un qui a peut-être un avis
        // contraire de son kinésithérapeute.
        Assert.True(
            exercice.ValueKind is not JsonValueKind.Undefined,
            "L'exercice contre-indiqué a disparu du catalogue : l'API FILTRE, "
                + "ce que D63 interdit. Le marquage informe ; il ne retire rien. "
                + "La présentation — replier, signaler — appartient à l'écran, "
                + "et se déduit de la sévérité."
        );

        // SECONDE MOITIÉ : il est marqué, avec la sévérité déclarée. Sans elle,
        // l'écran n'aurait aucun moyen de présenter autre chose qu'une liste
        // plate, et le § 4 serait lettre morte.
        var marquages = exercice.GetProperty("marquages").EnumerateArray().ToList();
        Assert.Single(marquages);
        Assert.Equal("genou", marquages[0].GetProperty("region").GetString());
        Assert.Equal("strict", marquages[0].GetProperty("severite").GetString());
    }

    [Fact]
    public async Task Un_exercice_SANS_recoupement_ne_porte_AUCUN_marquage()
    {
        // L'autre bord. Sans lui, un marquage qui décorerait TOUT le catalogue
        // passerait l'épreuve précédente, et l'écran replierait tout.
        var proprietaire = await CompteAsync();
        var neutre = await ExerciceContreIndiqueAsync("epaule");
        await using var hote = Hote(proprietaire);
        using var portee = hote.Services.CreateScope();

        await RemplacerAsync(portee.ServiceProvider, ["genou"]);

        var catalogue = await CatalogueAsync(portee.ServiceProvider);
        var exercice = catalogue.Single(e => e.GetProperty("id").GetGuid() == neutre);

        Assert.Empty(exercice.GetProperty("marquages").EnumerateArray());
    }

    [Fact]
    public async Task SANS_contrainte_declaree_RIEN_n_est_marque()
    {
        // L'état de la plupart des utilisateurs. Le marquage ne doit pas
        // apparaître par défaut : il dit un recoupement, et sans contrainte
        // déclarée il n'y a rien à recouper.
        var proprietaire = await CompteAsync();
        var contreIndique = await ExerciceContreIndiqueAsync("genou");
        await using var hote = Hote(proprietaire);
        using var portee = hote.Services.CreateScope();

        var catalogue = await CatalogueAsync(portee.ServiceProvider);
        var exercice = catalogue.Single(e => e.GetProperty("id").GetGuid() == contreIndique);

        Assert.Empty(exercice.GetProperty("marquages").EnumerateArray());
    }

    [Fact]
    public async Task Le_marquage_de_B_ne_porte_PAS_les_contraintes_de_A()
    {
        // Le marquage lit les contraintes de l'APPELANT. Sans RLS, il lirait
        // celles de tout le monde — et le catalogue de B se couvrirait de
        // marquages tirés des données de santé de A.
        var a = await CompteAsync();
        var b = await CompteAsync();
        var contreIndique = await ExerciceContreIndiqueAsync("genou");

        await using (var hoteA = Hote(a))
        {
            using var porteeA = hoteA.Services.CreateScope();
            await RemplacerAsync(porteeA.ServiceProvider, ["genou"], severite: "strict");
        }

        await using var hoteB = Hote(b);
        using var porteeB = hoteB.Services.CreateScope();

        var catalogue = await CatalogueAsync(porteeB.ServiceProvider);
        var exercice = catalogue.Single(e => e.GetProperty("id").GetGuid() == contreIndique);

        Assert.Empty(exercice.GetProperty("marquages").EnumerateArray());
    }

    // ================================================================
    // La sévérité, bout à bout
    // ================================================================

    [Fact]
    public async Task La_severite_ABSENTE_est_stockee_a_MODERE()
    {
        var proprietaire = await CompteAsync();
        await using var hote = Hote(proprietaire);
        using var portee = hote.Services.CreateScope();

        await RemplacerAsync(portee.ServiceProvider, ["genou"]);

        var rendue = await UneAsync(portee.ServiceProvider, "genou");
        Assert.Equal("modere", rendue.GetProperty("severite").GetString());
    }

    [Fact]
    public async Task Changer_la_SEVERITE_ne_change_pas_la_DATE()
    {
        // Une contrainte qui passe de `modere` à `strict` reste la MÊME
        // contrainte, déclarée le même jour. Remettre la date à zéro ferait
        // dire à `declared_at` « depuis le dernier réglage » au lieu de
        // « depuis quand ».
        var proprietaire = await CompteAsync();
        await using var hote = Hote(proprietaire);
        using var portee = hote.Services.CreateScope();

        await RemplacerAsync(
            portee.ServiceProvider,
            ["genou"],
            new HorlogeFixe(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)),
            severite: "leger"
        );

        await RemplacerAsync(
            portee.ServiceProvider,
            ["genou"],
            new HorlogeFixe(new DateTimeOffset(2026, 8, 23, 0, 0, 0, TimeSpan.Zero)),
            severite: "strict"
        );

        var rendue = await UneAsync(portee.ServiceProvider, "genou");

        Assert.Equal("strict", rendue.GetProperty("severite").GetString());
        Assert.Equal(1, rendue.GetProperty("declareeLe").GetDateTimeOffset().Month);
    }

    [Fact]
    public async Task Une_severite_INCONNUE_est_refusee_avant_toute_ecriture()
    {
        var proprietaire = await CompteAsync();
        await using var hote = Hote(proprietaire);
        using var portee = hote.Services.CreateScope();

        var (code, corps) = await RemplacerAsync(
            portee.ServiceProvider,
            ["genou"],
            severite: "urgent"
        );

        Assert.Equal(StatusCodes.Status400BadRequest, code);
        Assert.Equal("SeveriteInvalide", HarnaisHttp.Code(corps));
        Assert.Empty(await ListerAsync(portee.ServiceProvider));
    }

    [Fact]
    public async Task Une_NOTE_se_relit_telle_qu_elle_a_ete_saisie()
    {
        var proprietaire = await CompteAsync();
        await using var hote = Hote(proprietaire);
        using var portee = hote.Services.CreateScope();

        var (code, _) = await HarnaisHttp.ExecuterAsync(
            portee.ServiceProvider,
            ContraintesDuCompte.RemplacerAsync(
                new DeclarationDeContraintes(
                    [new DeclarationDUneContrainte("lombaire", "strict", "  hernie L5-S1  ")]
                ),
                portee.ServiceProvider.GetRequiredService<IExecuteurDeCasDUsage>(),
                portee.ServiceProvider.GetRequiredService<RemplacerLesContraintes>(),
                TimeProvider.System,
                CancellationToken.None
            )
        );

        Assert.Equal(StatusCodes.Status200OK, code);

        var rendue = await UneAsync(portee.ServiceProvider, "lombaire");
        Assert.Equal("hernie L5-S1", rendue.GetProperty("note").GetString());
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
        TimeProvider? horloge = null,
        string? severite = null
    ) =>
        HarnaisHttp.ExecuterAsync(
            services,
            ContraintesDuCompte.RemplacerAsync(
                new DeclarationDeContraintes(
                    [.. regions.Select(r => new DeclarationDUneContrainte(r, severite, null))]
                ),
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

    private static async Task<IReadOnlyList<JsonElement>> CatalogueAsync(IServiceProvider services)
    {
        var (code, corps) = await HarnaisHttp.ExecuterAsync(
            services,
            Catalogue.ListerAsync(
                services.GetRequiredService<IExecuteurDeCasDUsage>(),
                services.GetRequiredService<ListerLeCatalogue>(),
                CancellationToken.None
            )
        );

        Assert.Equal(StatusCodes.Status200OK, code);
        return [.. JsonDocument.Parse(corps).RootElement.EnumerateArray().Select(e => e.Clone())];
    }

    private static async Task<JsonElement> UneAsync(IServiceProvider services, string region)
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
        return JsonDocument
            .Parse(corps)
            .RootElement.EnumerateArray()
            .Single(c => c.GetProperty("region").GetString() == region)
            .Clone();
    }

    /// <summary>
    /// Compte les contraintes de DEUX comptes nommés, sous
    /// <c>palier_migrations</c> qui voit tout.
    /// </summary>
    /// <remarks>
    /// <b>Bornée aux deux comptes, et pas à la table entière.</b> La base est
    /// partagée par toutes les épreuves de la collection : un
    /// <c>count(*)</c> nu compterait aussi les lignes des voisines, et
    /// l'assertion passerait ou échouerait selon l'ordre d'exécution. Mesuré —
    /// l'épreuve attendait 3 et trouvait 5 dès que d'autres épreuves de ce
    /// fichier ont déclaré des contraintes.
    ///
    /// Sous <c>palier_migrations</c> parce que la question est « la ligne
    /// existe-t-elle en base », pas « l'appelant la voit-il » : sous
    /// <c>palier_app</c>, les lignes de A seraient invisibles depuis la session
    /// de B et l'assertion passerait pour la mauvaise raison.
    /// </remarks>
    private async Task<long> CompterPourAsync(Guid a, Guid b)
    {
        await using var connexion = new NpgsqlConnection(baseDeDonnees.ChaineMigrations);
        await connexion.OpenAsync();
        await using var commande = new NpgsqlCommand(
            "select count(*) from public.user_constraints where owner_id = $1 or owner_id = $2",
            connexion
        );
        commande.Parameters.AddWithValue(a);
        commande.Parameters.AddWithValue(b);
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
