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
/// Le catalogue mixte, sur le moteur réel.
/// </summary>
/// <remarks>
/// <b>C'est la seule table du schéma dont une partie est lisible par tous.</b>
/// Deux politiques séparées la gouvernent — <c>catalogue_public</c> en
/// <c>select</c> seul, <c>proprietaire</c> en <c>for all</c> — et la
/// combinaison produit trois refus distincts que le moteur, lui, réduit tous à
/// « zéro ligne ». Ces épreuves les provoquent un par un.
/// </remarks>
[Collection(BaseFixture.Collection)]
public sealed class EntrainementCatalogueTests(BaseFixture baseDeDonnees)
{
    private const string _motDePasse = "brouette-hivernale-38-oscille";

    // ================================================================
    // Lister
    // ================================================================

    [Fact]
    public async Task Le_catalogue_rend_le_PUBLIC_et_LES_SIENS()
    {
        var proprietaire = await CompteAsync();
        var public1 = await ExercicePublicAsync();
        await using var hote = Hote(proprietaire);
        using var portee = hote.Services.CreateScope();

        var sien = await CreerAsync(portee.ServiceProvider, "Mon rowing");
        var identifiants = await ListerAsync(portee.ServiceProvider);

        Assert.Contains(public1, identifiants);
        Assert.Contains(sien, identifiants);
    }

    [Fact]
    public async Task Le_catalogue_de_B_ne_porte_AUCUN_exercice_personnalise_de_A()
    {
        var a = await CompteAsync();
        var b = await CompteAsync();

        Guid deA;
        await using (var hoteA = Hote(a))
        {
            using var porteeA = hoteA.Services.CreateScope();
            deA = await CreerAsync(porteeA.ServiceProvider, "Secret de A");
        }

        await using var hoteB = Hote(b);
        using var porteeB = hoteB.Services.CreateScope();
        var identifiants = await ListerAsync(porteeB.ServiceProvider);

        Assert.DoesNotContain(deA, identifiants);
    }

    // ================================================================
    // Créer
    // ================================================================

    [Fact]
    public async Task Un_exercice_cree_est_PERSONNALISE()
    {
        // Sans `is_custom = true`, la politique `catalogue_public` le rendrait
        // visible de TOUS — et plus personne ne pourrait le supprimer, puisque
        // la suppression exige d'en être propriétaire.
        var proprietaire = await CompteAsync();
        await using var hote = Hote(proprietaire);
        using var portee = hote.Services.CreateScope();

        var (code, corps) = await HarnaisHttp.ExecuterAsync(
            portee.ServiceProvider,
            Catalogue.CreerAsync(
                new CreationDExercice("Mon curl", "haltère", ["biceps"], [], true, 1.25m, ["epaule"]),
                portee.ServiceProvider.GetRequiredService<IExecuteurDeCasDUsage>(),
                portee.ServiceProvider.GetRequiredService<CreerUnExercice>(),
                CancellationToken.None
            )
        );

        Assert.Equal(StatusCodes.Status201Created, code);
        var rendu = JsonDocument.Parse(corps).RootElement;
        Assert.True(rendu.GetProperty("estPersonnalise").GetBoolean());
        Assert.Equal("Mon curl", rendu.GetProperty("nom").GetString());
        Assert.Equal(1.25m, rendu.GetProperty("incrementParDefaut").GetDecimal());
    }

    [Fact]
    public async Task Une_contrainte_est_NORMALISEE_a_sa_forme_stockee()
    {
        // « Épaule » saisi, « epaule » stocké. Sans normalisation, les deux
        // formes cohabiteraient en base et le filtrage par intersection —
        // celui du lot 6 — manquerait la moitié des cas, sans erreur visible.
        var proprietaire = await CompteAsync();
        await using var hote = Hote(proprietaire);
        using var portee = hote.Services.CreateScope();

        var (code, corps) = await HarnaisHttp.ExecuterAsync(
            portee.ServiceProvider,
            Catalogue.CreerAsync(
                new CreationDExercice("Élévations", null, ["deltoïdes"], [], false, 1m, ["EPAULE"]),
                portee.ServiceProvider.GetRequiredService<IExecuteurDeCasDUsage>(),
                portee.ServiceProvider.GetRequiredService<CreerUnExercice>(),
                CancellationToken.None
            )
        );

        Assert.Equal(StatusCodes.Status201Created, code);
        var contraintes = JsonDocument
            .Parse(corps)
            .RootElement.GetProperty("contreIndicationsPour")
            .EnumerateArray()
            .Select(c => c.GetString())
            .ToList();

        Assert.Equal(["epaule"], contraintes);
    }

    [Fact]
    public async Task Une_creation_INVALIDE_est_refusee_avant_toute_ecriture()
    {
        var proprietaire = await CompteAsync();
        await using var hote = Hote(proprietaire);
        using var portee = hote.Services.CreateScope();

        var avant = (await ListerAsync(portee.ServiceProvider)).Count;

        var (code, corps) = await HarnaisHttp.ExecuterAsync(
            portee.ServiceProvider,
            Catalogue.CreerAsync(
                new CreationDExercice("", null, [], [], false, 2.5m, []),
                portee.ServiceProvider.GetRequiredService<IExecuteurDeCasDUsage>(),
                portee.ServiceProvider.GetRequiredService<CreerUnExercice>(),
                CancellationToken.None
            )
        );

        Assert.Equal(StatusCodes.Status400BadRequest, code);
        Assert.Equal("NomRequis", HarnaisHttp.Code(corps));
        Assert.Equal(avant, (await ListerAsync(portee.ServiceProvider)).Count);
    }

    // ================================================================
    // Supprimer — les trois refus, chacun PROVOQUÉ
    // ================================================================

    [Fact]
    public async Task Supprimer_UN_SIEN_fonctionne()
    {
        var proprietaire = await CompteAsync();
        await using var hote = Hote(proprietaire);
        using var portee = hote.Services.CreateScope();
        var sien = await CreerAsync(portee.ServiceProvider, "À supprimer");

        var (code, _) = await SupprimerAsync(portee.ServiceProvider, sien);

        Assert.Equal(StatusCodes.Status204NoContent, code);
        Assert.DoesNotContain(sien, await ListerAsync(portee.ServiceProvider));
    }

    [Fact]
    public async Task Supprimer_un_exercice_PUBLIC_est_refuse_par_un_403()
    {
        // 403 et NON 404, contrairement aux séances : l'appelant vient de lire
        // cet exercice dans le catalogue. Répondre « il n'existe pas » sur une
        // ressource qu'on vient de lui montrer serait un mensonge, et le
        // laisserait chercher un défaut de son côté.
        var proprietaire = await CompteAsync();
        var publique = await ExercicePublicAsync();
        await using var hote = Hote(proprietaire);
        using var portee = hote.Services.CreateScope();

        var (code, corps) = await SupprimerAsync(portee.ServiceProvider, publique);

        Assert.Equal(StatusCodes.Status403Forbidden, code);
        Assert.Equal("ExercicePublic", HarnaisHttp.Code(corps));

        // ET il est toujours là, pour tout le monde.
        Assert.Contains(publique, await ListerAsync(portee.ServiceProvider));
    }

    [Fact]
    public async Task Supprimer_l_exercice_D_UN_AUTRE_est_INTROUVABLE()
    {
        // 404 ici, parce que l'exercice personnalisé d'autrui n'est pas
        // visible : son existence EST un secret. Les deux codes de refus
        // coexistent sans se contredire — ils répondent à deux situations
        // différentes.
        var a = await CompteAsync();
        var b = await CompteAsync();

        Guid deA;
        await using (var hoteA = Hote(a))
        {
            using var porteeA = hoteA.Services.CreateScope();
            deA = await CreerAsync(porteeA.ServiceProvider, "Secret de A");
        }

        await using var hoteB = Hote(b);
        using var porteeB = hoteB.Services.CreateScope();
        var (code, corps) = await SupprimerAsync(porteeB.ServiceProvider, deA);

        Assert.Equal(StatusCodes.Status404NotFound, code);
        Assert.Equal("ExerciceIntrouvable", HarnaisHttp.Code(corps));

        // ET il est toujours là chez A.
        await using var hoteA2 = Hote(a);
        using var porteeA2 = hoteA2.Services.CreateScope();
        Assert.Contains(deA, await ListerAsync(porteeA2.ServiceProvider));
    }

    [Fact]
    public async Task Supprimer_un_exercice_UTILISE_est_refuse_par_un_409()
    {
        // `FK_sets_exercises_exercise_id` porte `Restrict`, donc le moteur
        // refuserait par une violation de clé étrangère — un 500 pour un geste
        // parfaitement compréhensible. Et le refus est le BON comportement :
        // supprimer l'exercice effacerait des séries de l'historique, donc du
        // volume et de la progression déjà mesurés.
        var proprietaire = await CompteAsync();
        await using var hote = Hote(proprietaire);
        using var portee = hote.Services.CreateScope();

        var sien = await CreerAsync(portee.ServiceProvider, "Utilisé");
        var seance = await OuvrirAsync(portee.ServiceProvider);
        await AjouterUneSerieAsync(portee.ServiceProvider, seance, sien);

        var (code, corps) = await SupprimerAsync(portee.ServiceProvider, sien);

        Assert.Equal(StatusCodes.Status409Conflict, code);
        Assert.Equal("ExerciceUtilise", HarnaisHttp.Code(corps));
        Assert.Contains(sien, await ListerAsync(portee.ServiceProvider));
    }

    [Fact]
    public async Task Supprimer_un_exercice_reference_par_un_RESSENTI_SANS_serie_rend_409()
    {
        // LE CAS QUE LA REVUE A TROUVÉ, et qu'aucune épreuve n'exerçait.
        //
        // DEUX tables référencent `exercises` en `Restrict` :
        // `FK_sets_exercises_exercise_id` (lot 2) et
        // `FK_exercise_feedback_exercises_exercise_id` (lot 5). Le gestionnaire
        // n'interrogeait que la première, et le commentaire qui l'accompagnait
        // — « seules ses séries peuvent le référencer » — est devenu faux le
        // jour même où la seconde a été posée.
        //
        // Un ressenti se note SANS série : `NoterUnRessenti` ne vérifie que la
        // visibilité de la séance et de l'exercice. L'état « exercice référencé
        // uniquement par un ressenti » est donc atteignable par l'API, et il
        // produisait une violation de clé étrangère — un 500 — là où le contrat
        // annonce un 409 nommé.
        var proprietaire = await CompteAsync();
        await using var hote = Hote(proprietaire);
        using var portee = hote.Services.CreateScope();

        var sien = await CreerAsync(portee.ServiceProvider, "Noté sans série");
        var seance = await OuvrirAsync(portee.ServiceProvider);

        // AUCUNE série n'est ajoutée : c'est tout le sujet.
        var (note, _) = await HarnaisHttp.ExecuterAsync(
            portee.ServiceProvider,
            RessentiParExercice.NoterAsync(
                seance,
                new NoteDeRessenti(sien, "pain"),
                portee.ServiceProvider.GetRequiredService<IExecuteurDeCasDUsage>(),
                portee.ServiceProvider.GetRequiredService<NoterUnRessenti>(),
                CancellationToken.None
            )
        );
        Assert.Equal(StatusCodes.Status204NoContent, note);

        var (code, corps) = await SupprimerAsync(portee.ServiceProvider, sien);

        Assert.Equal(StatusCodes.Status409Conflict, code);
        Assert.Equal("ExerciceUtilise", HarnaisHttp.Code(corps));

        // Et il est toujours là : le refus a bien empêché l'écriture.
        Assert.Contains(sien, await ListerAsync(portee.ServiceProvider));
    }

    [Fact]
    public async Task Supprimer_un_exercice_INCONNU_est_INTROUVABLE()
    {
        var proprietaire = await CompteAsync();
        await using var hote = Hote(proprietaire);
        using var portee = hote.Services.CreateScope();

        var (code, corps) = await SupprimerAsync(portee.ServiceProvider, Guid.NewGuid());

        Assert.Equal(StatusCodes.Status404NotFound, code);
        Assert.Equal("ExerciceIntrouvable", HarnaisHttp.Code(corps));
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

    private static async Task<IReadOnlyList<Guid>> ListerAsync(IServiceProvider services)
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
        return
        [
            .. JsonDocument
                .Parse(corps)
                .RootElement.EnumerateArray()
                .Select(e => e.GetProperty("id").GetGuid()),
        ];
    }

    private static async Task<Guid> CreerAsync(IServiceProvider services, string nom)
    {
        var (code, corps) = await HarnaisHttp.ExecuterAsync(
            services,
            Catalogue.CreerAsync(
                new CreationDExercice(nom, null, ["pectoraux"], [], false, 2.5m, []),
                services.GetRequiredService<IExecuteurDeCasDUsage>(),
                services.GetRequiredService<CreerUnExercice>(),
                CancellationToken.None
            )
        );

        Assert.Equal(StatusCodes.Status201Created, code);
        return JsonDocument.Parse(corps).RootElement.GetProperty("id").GetGuid();
    }

    private static Task<(int Code, string Corps)> SupprimerAsync(
        IServiceProvider services,
        Guid identifiant
    ) =>
        HarnaisHttp.ExecuterAsync(
            services,
            Catalogue.SupprimerAsync(
                identifiant,
                services.GetRequiredService<IExecuteurDeCasDUsage>(),
                services.GetRequiredService<SupprimerUnExercice>(),
                CancellationToken.None
            )
        );

    private static async Task<Guid> OuvrirAsync(IServiceProvider services)
    {
        var (code, corps) = await HarnaisHttp.ExecuterAsync(
            services,
            Seances.OuvrirAsync(
                new OuvertureDeSeance(null, null, null),
                services.GetRequiredService<IExecuteurDeCasDUsage>(),
                services.GetRequiredService<OuvrirUneSeance>(),
                TimeProvider.System,
                CancellationToken.None
            )
        );

        Assert.Equal(StatusCodes.Status201Created, code);
        return JsonDocument.Parse(corps).RootElement.GetProperty("id").GetGuid();
    }

    private static async Task AjouterUneSerieAsync(
        IServiceProvider services,
        Guid seance,
        Guid exercice
    )
    {
        var (code, _) = await HarnaisHttp.ExecuterAsync(
            services,
            Series.AjouterAsync(
                seance,
                new AjoutDeSerie(exercice, 1, 60m, 8, 2, Echauffement: false),
                services.GetRequiredService<IExecuteurDeCasDUsage>(),
                services.GetRequiredService<AjouterUneSerie>(),
                TimeProvider.System,
                CancellationToken.None
            )
        );

        Assert.Equal(StatusCodes.Status201Created, code);
    }

    private async Task<Guid> ExercicePublicAsync()
    {
        await using var connexion = new NpgsqlConnection(baseDeDonnees.ChaineMigrations);
        await connexion.OpenAsync();
        await using var commande = new NpgsqlCommand(
            """
            insert into public.exercises (name, primary_muscles, is_custom, owner_id)
            values ($1, array['pectoraux'], false, null) returning id
            """,
            connexion
        );
        commande.Parameters.AddWithValue(
            $"public-{Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)}"
        );
        return (Guid)(await commande.ExecuteScalarAsync())!;
    }

    private async Task<Guid> CompteAsync()
    {
        await using var hote = HarnaisHttp.Hote(baseDeDonnees);
        using var portee = hote.Services.CreateScope();
        var utilisateurs = portee.ServiceProvider.GetRequiredService<UserManager<Utilisateur>>();

        var email =
            "catalogue-"
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
