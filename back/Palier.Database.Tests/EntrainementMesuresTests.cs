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
/// La force estimée, le plateau et le volume, sur le moteur réel.
/// </summary>
/// <remarks>
/// <b>L'épreuve la plus importante de ce fichier est celle de la VUE.</b>
/// <c>weekly_volume</c> porte <c>security_invoker = true</c> : sans lui, elle
/// s'exécuterait avec les droits de son propriétaire — <c>palier_migrations</c>,
/// qui possède toutes les tables — et un seul <c>SELECT</c> rendrait le volume
/// de tous les utilisateurs. C'est le seul endroit du schéma où l'isolation peut
/// changer de règle sans qu'aucune ligne de code applicatif ne bouge.
/// </remarks>
[Collection(BaseFixture.Collection)]
public sealed class EntrainementMesuresTests(BaseFixture baseDeDonnees)
{
    private const string _motDePasse = "brouette-hivernale-38-oscille";

    // ================================================================
    // La force estimée
    // ================================================================

    [Fact]
    public async Task La_force_estimee_IGNORE_les_series_d_echauffement()
    {
        // Une série d'échauffement plus LOURDE que les séries de travail, à
        // deux répétitions. Si elle comptait, elle donnerait le 1RM le plus
        // élevé et le produit proposerait une progression sur un maximum qui
        // n'a jamais été soulevé.
        var proprietaire = await CompteAsync();
        var exercice = await ExercicePublicAsync();
        await using var hote = Hote(proprietaire);
        using var portee = hote.Services.CreateScope();
        var seance = await OuvrirAsync(portee.ServiceProvider);

        await AjouterAsync(portee.ServiceProvider, seance, exercice, 1, 200m, 2, echauffement: true);
        await AjouterAsync(portee.ServiceProvider, seance, exercice, 2, 100m, 8, echauffement: false);

        var progression = await ProgressionAsync(portee.ServiceProvider, exercice);

        // Une seule série retenue : l'échauffement n'y est pas.
        Assert.Equal(1, progression.GetProperty("seriesRetenues").GetInt32());

        // Epley sur 100 kg × (8 + 2 de RIR) = 100 × (1 + 10/30) ≈ 133,33.
        // L'échauffement aurait donné 200 × (1 + 4/30) ≈ 226,67.
        var estime = progression.GetProperty("unRepetitionMaximumKg").GetDecimal();
        Assert.True(
            estime is > 130m and < 137m,
            $"1RM estimé de {estime} kg : l'échauffement a compté."
        );
    }

    [Fact]
    public async Task La_force_estimee_retient_la_MEILLEURE_serie()
    {
        var proprietaire = await CompteAsync();
        var exercice = await ExercicePublicAsync();
        await using var hote = Hote(proprietaire);
        using var portee = hote.Services.CreateScope();
        var seance = await OuvrirAsync(portee.ServiceProvider);

        // La deuxième est la meilleure malgré une charge plus faible : les
        // répétitions comptent. Prendre « la plus lourde » serait faux.
        await AjouterAsync(portee.ServiceProvider, seance, exercice, 1, 100m, 3, echauffement: false);
        await AjouterAsync(portee.ServiceProvider, seance, exercice, 2, 95m, 8, echauffement: false);

        var progression = await ProgressionAsync(portee.ServiceProvider, exercice);
        var estime = progression.GetProperty("unRepetitionMaximumKg").GetDecimal();

        // 100 × (1 + 5/30) ≈ 116,67 contre 95 × (1 + 10/30) ≈ 126,67.
        Assert.True(estime > 120m, $"1RM estimé de {estime} kg : la meilleure série a été manquée.");
    }

    [Fact]
    public async Task Un_exercice_SANS_serie_rend_un_etat_VIDE_et_non_une_erreur()
    {
        // « Je n'ai pas encore de donnée » est une réponse. Un 404 ici ferait
        // croire que l'exercice n'existe pas, et l'écran afficherait une erreur
        // là où il devrait inviter à saisir sa première série.
        var proprietaire = await CompteAsync();
        var exercice = await ExercicePublicAsync();
        await using var hote = Hote(proprietaire);
        using var portee = hote.Services.CreateScope();

        var progression = await ProgressionAsync(portee.ServiceProvider, exercice);

        Assert.Equal(0, progression.GetProperty("seriesRetenues").GetInt32());
        Assert.Equal(JsonValueKind.Null, progression.GetProperty("unRepetitionMaximumKg").ValueKind);
        Assert.Equal(JsonValueKind.Null, progression.GetProperty("fiabilite").ValueKind);
        Assert.False(progression.GetProperty("enPlateau").GetBoolean());
    }

    [Fact]
    public async Task Un_exercice_INCONNU_rend_404()
    {
        var proprietaire = await CompteAsync();
        await using var hote = Hote(proprietaire);
        using var portee = hote.Services.CreateScope();

        var (code, corps) = await HarnaisHttp.ExecuterAsync(
            portee.ServiceProvider,
            Mesures.ProgressionAsync(
                Guid.NewGuid(),
                portee.ServiceProvider.GetRequiredService<IExecuteurDeCasDUsage>(),
                portee.ServiceProvider.GetRequiredService<LireLaProgression>(),
                CancellationToken.None
            )
        );

        Assert.Equal(StatusCodes.Status404NotFound, code);
        Assert.Equal("ExerciceIntrouvable", HarnaisHttp.Code(corps));
    }

    // ================================================================
    // Le plateau
    // ================================================================

    [Fact]
    public async Task Trois_seances_a_la_MEME_charge_sans_progression_signalent_un_plateau()
    {
        var proprietaire = await CompteAsync();
        var exercice = await ExercicePublicAsync();
        await using var hote = Hote(proprietaire);
        using var portee = hote.Services.CreateScope();

        foreach (var jour in new[] { 1, 8, 15 })
        {
            var seance = await OuvrirAsync(
                portee.ServiceProvider,
                new DateTimeOffset(2026, 8, jour, 9, 0, 0, TimeSpan.Zero)
            );
            await AjouterAsync(portee.ServiceProvider, seance, exercice, 1, 100m, 5, echauffement: false);
        }

        var progression = await ProgressionAsync(portee.ServiceProvider, exercice);
        Assert.True(progression.GetProperty("enPlateau").GetBoolean());
    }

    [Fact]
    public async Task Des_repetitions_QUI_MONTENT_ne_sont_PAS_un_plateau()
    {
        // L'autre bord. Sans lui, une détection qui crierait toujours passerait
        // l'épreuve précédente — et le produit signalerait un plateau à
        // quelqu'un qui progresse, ce qui est le pire message possible.
        var proprietaire = await CompteAsync();
        var exercice = await ExercicePublicAsync();
        await using var hote = Hote(proprietaire);
        using var portee = hote.Services.CreateScope();

        var repetitions = 5;
        foreach (var jour in new[] { 1, 8, 15 })
        {
            var seance = await OuvrirAsync(
                portee.ServiceProvider,
                new DateTimeOffset(2026, 8, jour, 9, 0, 0, TimeSpan.Zero)
            );
            await AjouterAsync(
                portee.ServiceProvider,
                seance,
                exercice,
                1,
                100m,
                repetitions++,
                echauffement: false
            );
        }

        var progression = await ProgressionAsync(portee.ServiceProvider, exercice);
        Assert.False(progression.GetProperty("enPlateau").GetBoolean());
    }

    // ================================================================
    // Le volume, et l'isolation SUR LA VUE
    // ================================================================

    [Fact]
    public async Task Le_volume_compte_les_series_DURES_par_muscle()
    {
        var proprietaire = await CompteAsync();
        var exercice = await ExercicePublicAsync();
        await using var hote = Hote(proprietaire);
        using var portee = hote.Services.CreateScope();
        var seance = await OuvrirAsync(portee.ServiceProvider);

        await AjouterAsync(portee.ServiceProvider, seance, exercice, 1, 60m, 8, echauffement: false);
        await AjouterAsync(portee.ServiceProvider, seance, exercice, 2, 60m, 8, echauffement: false);
        await AjouterAsync(portee.ServiceProvider, seance, exercice, 3, 30m, 12, echauffement: true);

        var bilan = await VolumeAsync(portee.ServiceProvider);
        var semaines = bilan.GetProperty("semaines").EnumerateArray().ToList();

        Assert.Single(semaines);
        var pectoraux = semaines[0]
            .GetProperty("muscles")
            .EnumerateArray()
            .Single(m => m.GetProperty("muscle").GetString() == "pectoraux");

        // DEUX, et non trois : l'échauffement est exclu par la vue.
        Assert.Equal(2m, pectoraux.GetProperty("seriesDures").GetDecimal());
    }

    [Fact]
    public async Task La_VUE_de_volume_n_expose_que_les_series_de_l_APPELANT()
    {
        // L'ÉPREUVE QUI COMPTE. `weekly_volume` porte
        // `security_invoker = true` ; sans lui, elle s'exécuterait sous
        // `palier_migrations`, propriétaire de toutes les tables, et B verrait
        // le volume de A. Aucune ligne de code applicatif ne le montrerait :
        // c'est un attribut de la vue, posé dans une migration.
        var a = await CompteAsync();
        var b = await CompteAsync();
        var exercice = await ExercicePublicAsync();

        await using (var hoteA = Hote(a))
        {
            using var porteeA = hoteA.Services.CreateScope();
            var seanceA = await OuvrirAsync(porteeA.ServiceProvider);
            for (var rang = 1; rang <= 10; rang++)
            {
                await AjouterAsync(
                    porteeA.ServiceProvider,
                    seanceA,
                    exercice,
                    rang,
                    60m,
                    8,
                    echauffement: false
                );
            }
        }

        await using var hoteB = Hote(b);
        using var porteeB = hoteB.Services.CreateScope();
        var seanceB = await OuvrirAsync(porteeB.ServiceProvider);
        for (var rang = 1; rang <= 3; rang++)
        {
            await AjouterAsync(
                porteeB.ServiceProvider,
                seanceB,
                exercice,
                rang,
                50m,
                10,
                echauffement: false
            );
        }

        var bilan = await VolumeAsync(porteeB.ServiceProvider);
        var pectoraux = bilan
            .GetProperty("semaines")
            .EnumerateArray()
            .SelectMany(s => s.GetProperty("muscles").EnumerateArray())
            .Where(m => m.GetProperty("muscle").GetString() == "pectoraux")
            .Sum(m => m.GetProperty("seriesDures").GetDecimal());

        // TROIS, celles de B. Treize signifierait que la vue rend tout.
        Assert.Equal(3m, pectoraux);
    }

    [Fact]
    public async Task Un_volume_SANS_entrainement_est_un_etat_legitime()
    {
        var proprietaire = await CompteAsync();
        await using var hote = Hote(proprietaire);
        using var portee = hote.Services.CreateScope();

        var bilan = await VolumeAsync(portee.ServiceProvider);
        Assert.Empty(bilan.GetProperty("semaines").EnumerateArray());
    }

    [Theory]
    // Un LUNDI, puis un mardi, puis un dimanche. Le compte doit être le même.
    [InlineData("2026-08-24")]
    [InlineData("2026-08-25")]
    [InlineData("2026-08-30")]
    public async Task Le_nombre_de_semaines_rendues_NE_DEPEND_PAS_du_jour_de_la_requete(
        string jourDeLaRequete
    )
    {
        // L'OFF-BY-ONE QUE LA REVUE A TROUVÉ. La borne basse était calculée en
        // retirant `7 × nombre` jours à la date du jour, ce qui préserve le
        // jour de la semaine : un lundi, elle tombait SUR un lundi, donc sur
        // une valeur de la vue — que le `>=` incluait. La même requête rendait
        // N+1 semaines les lundis et N les autres jours.
        //
        // Trois séances, sur trois semaines distinctes. On demande DEUX
        // semaines : il doit en sortir deux, quel que soit le jour de l'appel.
        var proprietaire = await CompteAsync();
        var exercice = await ExercicePublicAsync();
        var aujourdHui = DateOnly.Parse(jourDeLaRequete, CultureInfo.InvariantCulture);
        var horloge = new HorlogeFixe(
            new DateTimeOffset(aujourdHui.ToDateTime(new TimeOnly(12, 0)), TimeSpan.Zero)
        );

        await using var hote = Hote(proprietaire);
        using var portee = hote.Services.CreateScope();

        // Une séance par semaine, sur les trois dernières semaines. C'est la
        // SÉRIE qu'on date : `weekly_volume` groupe sur `sets.logged_at`, pas
        // sur `workouts.started_at`.
        foreach (var recul in new[] { 0, 7, 14 })
        {
            var quand = new DateTimeOffset(
                aujourdHui.AddDays(-recul).ToDateTime(new TimeOnly(9, 0)),
                TimeSpan.Zero
            );
            var seance = await OuvrirAsync(portee.ServiceProvider, quand);
            await AjouterAsync(
                portee.ServiceProvider,
                seance,
                exercice,
                1,
                60m,
                8,
                false,
                new HorlogeFixe(quand)
            );
        }

        var (code, corps) = await HarnaisHttp.ExecuterAsync(
            portee.ServiceProvider,
            Mesures.VolumeAsync(
                2,
                portee.ServiceProvider.GetRequiredService<IExecuteurDeCasDUsage>(),
                portee.ServiceProvider.GetRequiredService<LireLeVolume>(),
                horloge,
                CancellationToken.None
            )
        );

        Assert.Equal(StatusCodes.Status200OK, code);
        Assert.Equal(
            2,
            JsonDocument.Parse(corps).RootElement.GetProperty("semaines").EnumerateArray().Count()
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

    private static async Task<JsonElement> ProgressionAsync(
        IServiceProvider services,
        Guid exercice
    )
    {
        var (code, corps) = await HarnaisHttp.ExecuterAsync(
            services,
            Mesures.ProgressionAsync(
                exercice,
                services.GetRequiredService<IExecuteurDeCasDUsage>(),
                services.GetRequiredService<LireLaProgression>(),
                CancellationToken.None
            )
        );

        Assert.Equal(StatusCodes.Status200OK, code);
        return JsonDocument.Parse(corps).RootElement.Clone();
    }

    private static async Task<JsonElement> VolumeAsync(IServiceProvider services)
    {
        var (code, corps) = await HarnaisHttp.ExecuterAsync(
            services,
            Mesures.VolumeAsync(
                null,
                services.GetRequiredService<IExecuteurDeCasDUsage>(),
                services.GetRequiredService<LireLeVolume>(),
                TimeProvider.System,
                CancellationToken.None
            )
        );

        Assert.Equal(StatusCodes.Status200OK, code);
        return JsonDocument.Parse(corps).RootElement.Clone();
    }

    private static async Task<Guid> OuvrirAsync(
        IServiceProvider services,
        DateTimeOffset? debut = null
    )
    {
        var (code, corps) = await HarnaisHttp.ExecuterAsync(
            services,
            Seances.OuvrirAsync(
                new OuvertureDeSeance(debut, null, null),
                services.GetRequiredService<IExecuteurDeCasDUsage>(),
                services.GetRequiredService<OuvrirUneSeance>(),
                TimeProvider.System,
                CancellationToken.None
            )
        );

        Assert.Equal(StatusCodes.Status201Created, code);
        return JsonDocument.Parse(corps).RootElement.GetProperty("id").GetGuid();
    }

    // L horloge date la SÉRIE, pas la séance — et c est ce qui compte pour le
    // volume : la vue groupe sur sets.logged_at, jamais sur
    // workouts.started_at. Une épreuve qui daterait les séances et lirait le
    // volume mesurerait deux choses différentes.
    private static async Task AjouterAsync(
        IServiceProvider services,
        Guid seance,
        Guid exercice,
        int rang,
        decimal charge,
        int repetitions,
        bool echauffement,
        TimeProvider? horloge = null
    )
    {
        var (code, _) = await HarnaisHttp.ExecuterAsync(
            services,
            Series.AjouterAsync(
                seance,
                new AjoutDeSerie(exercice, rang, charge, repetitions, 2, echauffement),
                services.GetRequiredService<IExecuteurDeCasDUsage>(),
                services.GetRequiredService<AjouterUneSerie>(),
                horloge ?? TimeProvider.System,
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
            insert into public.exercises (slug, name_fr, name_en, instructions_fr, instructions_en, common_errors_fr, common_errors_en, movement_role, primary_muscles, is_custom, owner_id)
            values ($1, $1, $1, 'Consignes.', 'Cues.', 'Erreurs.', 'Errors.',
                    'poussee', array['pectoraux'], false, null)
            returning id
            """,
            connexion
        );
        commande.Parameters.AddWithValue(
            $"exercice-{Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)}"
        );
        return (Guid)(await commande.ExecuteScalarAsync())!;
    }

    private async Task<Guid> CompteAsync()
    {
        await using var hote = HarnaisHttp.Hote(baseDeDonnees);
        using var portee = hote.Services.CreateScope();
        var utilisateurs = portee.ServiceProvider.GetRequiredService<UserManager<Utilisateur>>();

        var email =
            "mesure-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture) + "@exemple.test";
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
