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
/// Les séries, sur le moteur réel.
/// </summary>
/// <remarks>
/// <para>
/// <b>La série est possédée PAR JOINTURE</b> — <c>sets</c> n'a pas de colonne
/// <c>owner_id</c>, et sa politique remonte jusqu'à <c>workouts</c>. C'est la
/// forme la plus fragile du schéma : une politique qui oublierait la remontée
/// laisserait lire les séries de tout le monde, et rien dans le code applicatif
/// ne le montrerait.
/// </para>
///
/// <para>
/// Les épreuves de bornes ne sont PAS ici : elles vivent dans
/// <c>Palier.Application.Tests</c>, où elles s'exécutent en microsecondes sans
/// conteneur. Ce fichier ne juge que ce qui exige un vrai moteur — l'isolation,
/// les clés étrangères, et le rattachement.
/// </para>
/// </remarks>
[Collection(BaseFixture.Collection)]
public sealed class EntrainementSeriesTests(BaseFixture baseDeDonnees)
{
    private const string _motDePasse = "brouette-hivernale-38-oscille";

    // ================================================================
    // Ajouter
    // ================================================================

    [Fact]
    public async Task Une_serie_ajoutee_ressort_AVEC_la_seance()
    {
        var proprietaire = await CompteAsync();
        var exercice = await ExercicePublicAsync();
        await using var hote = Hote(proprietaire);
        using var portee = hote.Services.CreateScope();

        var seance = await OuvrirAsync(portee.ServiceProvider);

        var (ajout, corps) = await HarnaisHttp.ExecuterAsync(
            portee.ServiceProvider,
            Series.AjouterAsync(
                seance,
                new AjoutDeSerie(exercice, 1, 62.5m, 8, 2, Echauffement: false),
                portee.ServiceProvider.GetRequiredService<IExecuteurDeCasDUsage>(),
                portee.ServiceProvider.GetRequiredService<AjouterUneSerie>(),
                TimeProvider.System,
                CancellationToken.None
            )
        );

        Assert.Equal(StatusCodes.Status201Created, ajout);
        var serie = JsonDocument.Parse(corps).RootElement.GetProperty("id").GetGuid();

        // Et elle ressort par la LECTURE de la séance, pas seulement par la
        // réponse de création. Sans cette seconde moitié, une écriture qui
        // n'aurait jamais atteint la base passerait.
        var (lecture, detail) = await HarnaisHttp.ExecuterAsync(
            portee.ServiceProvider,
            Seances.LireAsync(
                seance,
                portee.ServiceProvider.GetRequiredService<IExecuteurDeCasDUsage>(),
                portee.ServiceProvider.GetRequiredService<LireUneSeance>(),
                CancellationToken.None
            )
        );

        Assert.Equal(StatusCodes.Status200OK, lecture);
        var series = JsonDocument
            .Parse(detail)
            .RootElement.GetProperty("series")
            .EnumerateArray()
            .ToList();

        Assert.Single(series);
        Assert.Equal(serie, series[0].GetProperty("id").GetGuid());
        Assert.Equal(62.5m, series[0].GetProperty("chargeKg").GetDecimal());
        Assert.False(series[0].GetProperty("echauffement").GetBoolean());
    }

    [Fact]
    public async Task Les_series_ressortent_DANS_L_ORDRE_de_leur_rang()
    {
        // PostgreSQL ne garantit aucun ordre sans `ORDER BY`. Un écran de
        // séance qui affiche les séries dans le désordre est un écran faux, et
        // le désordre n'apparaît qu'au hasard des plans — donc jamais en
        // développement, toujours en production.
        var proprietaire = await CompteAsync();
        var exercice = await ExercicePublicAsync();
        await using var hote = Hote(proprietaire);
        using var portee = hote.Services.CreateScope();
        var seance = await OuvrirAsync(portee.ServiceProvider);

        foreach (var rang in new[] { 3, 1, 2 })
        {
            await AjouterAsync(portee.ServiceProvider, seance, exercice, rang);
        }

        var (_, detail) = await HarnaisHttp.ExecuterAsync(
            portee.ServiceProvider,
            Seances.LireAsync(
                seance,
                portee.ServiceProvider.GetRequiredService<IExecuteurDeCasDUsage>(),
                portee.ServiceProvider.GetRequiredService<LireUneSeance>(),
                CancellationToken.None
            )
        );

        var rangs = JsonDocument
            .Parse(detail)
            .RootElement.GetProperty("series")
            .EnumerateArray()
            .Select(s => s.GetProperty("index").GetInt32())
            .ToList();

        Assert.Equal([1, 2, 3], rangs);
    }

    // ================================================================
    // Les refus, chacun PROVOQUÉ
    // ================================================================

    [Fact]
    public async Task Ajouter_une_serie_a_la_seance_d_AUTRUI_est_INTROUVABLE()
    {
        var a = await CompteAsync();
        var b = await CompteAsync();
        var exercice = await ExercicePublicAsync();

        Guid seance;
        await using (var hoteA = Hote(a))
        {
            using var porteeA = hoteA.Services.CreateScope();
            seance = await OuvrirAsync(porteeA.ServiceProvider);
        }

        await using var hoteB = Hote(b);
        using var porteeB = hoteB.Services.CreateScope();

        var (code, corps) = await HarnaisHttp.ExecuterAsync(
            porteeB.ServiceProvider,
            Series.AjouterAsync(
                seance,
                new AjoutDeSerie(exercice, 1, 60m, 8, 2, Echauffement: false),
                porteeB.ServiceProvider.GetRequiredService<IExecuteurDeCasDUsage>(),
                porteeB.ServiceProvider.GetRequiredService<AjouterUneSerie>(),
                TimeProvider.System,
                CancellationToken.None
            )
        );

        // 404, et surtout PAS 500. Sans le contrôle applicatif préalable, le
        // `with check` de la politique refuserait par un 42501 — une exception,
        // donc un défaut de serveur pour un identifiant simplement inconnu.
        Assert.Equal(StatusCodes.Status404NotFound, code);
        Assert.Equal("SeanceIntrouvable", HarnaisHttp.Code(corps));

        // ET la séance de A n'a rien reçu.
        await using var hoteA2 = Hote(a);
        using var porteeA2 = hoteA2.Services.CreateScope();
        var (_, detail) = await HarnaisHttp.ExecuterAsync(
            porteeA2.ServiceProvider,
            Seances.LireAsync(
                seance,
                porteeA2.ServiceProvider.GetRequiredService<IExecuteurDeCasDUsage>(),
                porteeA2.ServiceProvider.GetRequiredService<LireUneSeance>(),
                CancellationToken.None
            )
        );
        Assert.Empty(JsonDocument.Parse(detail).RootElement.GetProperty("series").EnumerateArray());
    }

    [Fact]
    public async Task Un_exercice_INCONNU_est_refuse_par_un_400_et_non_un_500()
    {
        // `FK_sets_exercises_exercise_id` refuserait cet identifiant par une
        // violation de clé étrangère — une exception, donc un 500. L'épreuve
        // PROVOQUE le cas pour prouver que le contrôle tombe avant.
        var proprietaire = await CompteAsync();
        await using var hote = Hote(proprietaire);
        using var portee = hote.Services.CreateScope();
        var seance = await OuvrirAsync(portee.ServiceProvider);

        var (code, corps) = await HarnaisHttp.ExecuterAsync(
            portee.ServiceProvider,
            Series.AjouterAsync(
                seance,
                new AjoutDeSerie(Guid.NewGuid(), 1, 60m, 8, 2, Echauffement: false),
                portee.ServiceProvider.GetRequiredService<IExecuteurDeCasDUsage>(),
                portee.ServiceProvider.GetRequiredService<AjouterUneSerie>(),
                TimeProvider.System,
                CancellationToken.None
            )
        );

        Assert.Equal(StatusCodes.Status400BadRequest, code);
        Assert.Equal("ExerciceIntrouvable", HarnaisHttp.Code(corps));
    }

    [Fact]
    public async Task L_exercice_PERSONNALISE_d_autrui_est_refuse_comme_INCONNU()
    {
        // La politique de `exercises` rend le personnalisé d'autrui invisible.
        // « Pas visible » et « n'existe pas » doivent donc être indiscernables
        // — sinon la route devient un oracle : essayer des identifiants
        // apprendrait lesquels existent chez les autres.
        var a = await CompteAsync();
        var b = await CompteAsync();
        var exerciceDeA = await ExercicePersonnaliseAsync(a);

        await using var hoteB = Hote(b);
        using var porteeB = hoteB.Services.CreateScope();
        var seanceDeB = await OuvrirAsync(porteeB.ServiceProvider);

        var (code, corps) = await HarnaisHttp.ExecuterAsync(
            porteeB.ServiceProvider,
            Series.AjouterAsync(
                seanceDeB,
                new AjoutDeSerie(exerciceDeA, 1, 60m, 8, 2, Echauffement: false),
                porteeB.ServiceProvider.GetRequiredService<IExecuteurDeCasDUsage>(),
                porteeB.ServiceProvider.GetRequiredService<AjouterUneSerie>(),
                TimeProvider.System,
                CancellationToken.None
            )
        );

        Assert.Equal(StatusCodes.Status400BadRequest, code);
        Assert.Equal("ExerciceIntrouvable", HarnaisHttp.Code(corps));
    }

    // ================================================================
    // Retirer
    // ================================================================

    [Fact]
    public async Task Une_serie_retiree_ne_ressort_plus()
    {
        var proprietaire = await CompteAsync();
        var exercice = await ExercicePublicAsync();
        await using var hote = Hote(proprietaire);
        using var portee = hote.Services.CreateScope();
        var seance = await OuvrirAsync(portee.ServiceProvider);
        var serie = await AjouterAsync(portee.ServiceProvider, seance, exercice, 1);

        var (retrait, _) = await HarnaisHttp.ExecuterAsync(
            portee.ServiceProvider,
            Series.RetirerAsync(
                seance,
                serie,
                portee.ServiceProvider.GetRequiredService<IExecuteurDeCasDUsage>(),
                portee.ServiceProvider.GetRequiredService<RetirerUneSerie>(),
                CancellationToken.None
            )
        );

        Assert.Equal(StatusCodes.Status204NoContent, retrait);

        var (_, detail) = await HarnaisHttp.ExecuterAsync(
            portee.ServiceProvider,
            Seances.LireAsync(
                seance,
                portee.ServiceProvider.GetRequiredService<IExecuteurDeCasDUsage>(),
                portee.ServiceProvider.GetRequiredService<LireUneSeance>(),
                CancellationToken.None
            )
        );
        Assert.Empty(JsonDocument.Parse(detail).RootElement.GetProperty("series").EnumerateArray());
    }

    [Fact]
    public async Task Retirer_une_serie_par_la_MAUVAISE_seance_est_refuse()
    {
        // Le cas que RLS seul ne couvre PAS. Les deux séances appartiennent au
        // même utilisateur : RLS laisse passer. Sans la clause `WorkoutId`
        // dans le gestionnaire, `DELETE /seances/{A}/series/{s}` effacerait une
        // série de la séance B — ses propres données, mais pas celles qu'il
        // visait, et sans le moindre signal.
        var proprietaire = await CompteAsync();
        var exercice = await ExercicePublicAsync();
        await using var hote = Hote(proprietaire);
        using var portee = hote.Services.CreateScope();

        var seanceA = await OuvrirAsync(portee.ServiceProvider);
        var seanceB = await OuvrirAsync(portee.ServiceProvider);
        var serieDeB = await AjouterAsync(portee.ServiceProvider, seanceB, exercice, 1);

        var (code, corps) = await HarnaisHttp.ExecuterAsync(
            portee.ServiceProvider,
            Series.RetirerAsync(
                seanceA,
                serieDeB,
                portee.ServiceProvider.GetRequiredService<IExecuteurDeCasDUsage>(),
                portee.ServiceProvider.GetRequiredService<RetirerUneSerie>(),
                CancellationToken.None
            )
        );

        Assert.Equal(StatusCodes.Status404NotFound, code);
        Assert.Equal("SerieIntrouvable", HarnaisHttp.Code(corps));

        // ET la série de B est toujours là.
        var (_, detail) = await HarnaisHttp.ExecuterAsync(
            portee.ServiceProvider,
            Seances.LireAsync(
                seanceB,
                portee.ServiceProvider.GetRequiredService<IExecuteurDeCasDUsage>(),
                portee.ServiceProvider.GetRequiredService<LireUneSeance>(),
                CancellationToken.None
            )
        );
        Assert.Single(JsonDocument.Parse(detail).RootElement.GetProperty("series").EnumerateArray());
    }

    [Fact]
    public async Task Supprimer_la_SEANCE_emporte_ses_series()
    {
        // `FK_sets_workouts_workout_id` porte `on delete cascade`. Sans cette
        // épreuve, un passage en `Restrict` ferait échouer toute suppression de
        // séance non vide — et le message serait une violation de contrainte.
        var proprietaire = await CompteAsync();
        var exercice = await ExercicePublicAsync();
        await using var hote = Hote(proprietaire);
        using var portee = hote.Services.CreateScope();
        var seance = await OuvrirAsync(portee.ServiceProvider);
        var serie = await AjouterAsync(portee.ServiceProvider, seance, exercice, 1);

        var (code, _) = await HarnaisHttp.ExecuterAsync(
            portee.ServiceProvider,
            Seances.SupprimerAsync(
                seance,
                portee.ServiceProvider.GetRequiredService<IExecuteurDeCasDUsage>(),
                portee.ServiceProvider.GetRequiredService<SupprimerUneSeance>(),
                CancellationToken.None
            )
        );

        Assert.Equal(StatusCodes.Status204NoContent, code);
        Assert.Equal(0, await CompterLaSerieAsync(serie));
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

    private static async Task<Guid> AjouterAsync(
        IServiceProvider services,
        Guid seance,
        Guid exercice,
        int rang
    )
    {
        var (code, corps) = await HarnaisHttp.ExecuterAsync(
            services,
            Series.AjouterAsync(
                seance,
                new AjoutDeSerie(exercice, rang, 60m, 8, 2, Echauffement: false),
                services.GetRequiredService<IExecuteurDeCasDUsage>(),
                services.GetRequiredService<AjouterUneSerie>(),
                TimeProvider.System,
                CancellationToken.None
            )
        );

        Assert.Equal(StatusCodes.Status201Created, code);
        return JsonDocument.Parse(corps).RootElement.GetProperty("id").GetGuid();
    }

    /// <summary>
    /// Le comptage se fait sous <c>palier_migrations</c>, qui voit tout : la
    /// question est « la ligne existe-t-elle encore en base », pas « l'appelant
    /// la voit-il ». Compter sous <c>palier_app</c> rendrait zéro même si la
    /// cascade n'avait rien fait.
    /// </summary>
    private async Task<long> CompterLaSerieAsync(Guid serie)
    {
        await using var connexion = new NpgsqlConnection(baseDeDonnees.ChaineMigrations);
        await connexion.OpenAsync();
        await using var commande = new NpgsqlCommand(
            "select count(*) from public.sets where id = $1",
            connexion
        );
        commande.Parameters.AddWithValue(serie);
        return (long)(await commande.ExecuteScalarAsync())!;
    }

    private Task<Guid> ExercicePublicAsync() => ExerciceAsync(estPersonnalise: false, null);

    private Task<Guid> ExercicePersonnaliseAsync(Guid proprietaire) =>
        ExerciceAsync(estPersonnalise: true, proprietaire);

    private async Task<Guid> ExerciceAsync(bool estPersonnalise, Guid? proprietaire)
    {
        await using var connexion = new NpgsqlConnection(baseDeDonnees.ChaineMigrations);
        await connexion.OpenAsync();
        await using var commande = new NpgsqlCommand(
            """
            insert into public.exercises (slug, name_fr, name_en, instructions_fr, instructions_en, common_errors_fr, common_errors_en, movement_role, primary_muscles, is_custom, owner_id)
            values (case when $2 then null else $1 end,
                    $1,
                    case when $2 then null else $1 end,
                    case when $2 then null else 'Consignes.' end,
                    case when $2 then null else 'Cues.' end,
                    case when $2 then null else 'Erreurs.' end,
                    case when $2 then null else 'Errors.' end,
                    'poussee', array['pectoraux'], $2, $3)
            returning id
            """,
            connexion
        );
        commande.Parameters.AddWithValue(
            $"exercice-{Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)}"
        );
        commande.Parameters.AddWithValue(estPersonnalise);
        commande.Parameters.AddWithValue(
            proprietaire.HasValue ? proprietaire.Value : (object)DBNull.Value
        );
        return (Guid)(await commande.ExecuteScalarAsync())!;
    }

    private async Task<Guid> CompteAsync()
    {
        await using var hote = HarnaisHttp.Hote(baseDeDonnees);
        using var portee = hote.Services.CreateScope();
        var utilisateurs = portee.ServiceProvider.GetRequiredService<UserManager<Utilisateur>>();

        var email =
            "serie-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture) + "@exemple.test";
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
