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
/// Le ressenti par exercice, sur le moteur réel — table NOUVELLE du lot 5.
/// </summary>
/// <remarks>
/// <b>L'épreuve la plus importante de ce fichier provoque le <c>with check</c>.</b>
/// La lecture serait refusée de toute façon par le <c>using</c> ; c'est
/// l'INSERTION rattachée à la séance d'autrui qu'il faut éprouver. Sans
/// <c>with check</c>, un utilisateur poserait une ligne dans l'historique de
/// quelqu'un d'autre — invisible pour lui, mais comptée dans les seuils du § 5
/// de sa victime, où deux <c>pain</c> consécutifs déclenchent une orientation
/// vers un professionnel.
/// </remarks>
[Collection(BaseFixture.Collection)]
public sealed class EntrainementRessentiTests(BaseFixture baseDeDonnees)
{
    private const string _motDePasse = "brouette-hivernale-38-oscille";

    // ================================================================
    // Noter
    // ================================================================

    [Theory]
    [InlineData("good")]
    [InlineData("meh")]
    [InlineData("pain")]
    public async Task Les_TROIS_valeurs_du_document_sont_acceptees_par_le_MOTEUR(string valeur)
    {
        // Ce qui est éprouvé ici n'est pas la validation applicative — elle vit
        // dans `Palier.Application.Tests` — mais l'ACCORD entre l'énumération
        // et la contrainte `ck_exercise_feedback_feeling`. Un désaccord ferait
        // un 500 sur une valeur que le produit annonce comme valide.
        var proprietaire = await CompteAsync();
        var exercice = await ExercicePublicAsync();
        await using var hote = Hote(proprietaire);
        using var portee = hote.Services.CreateScope();
        var seance = await OuvrirAsync(portee.ServiceProvider);

        var (code, _) = await NoterAsync(portee.ServiceProvider, seance, exercice, valeur);

        Assert.Equal(StatusCodes.Status204NoContent, code);
    }

    [Fact]
    public async Task Se_RAVISER_remplace_au_lieu_d_echouer()
    {
        // `unique (workout_id, exercise_id)`. Sans traitement, la seconde note
        // remonterait une violation de contrainte — donc un 500 — à quelqu'un
        // qui se ravise, ce qui est exactement le geste qu'on veut encourager :
        // le ressenti pilote l'adaptation, et un utilisateur qui n'ose plus
        // corriger sa note supprime le signal.
        var proprietaire = await CompteAsync();
        var exercice = await ExercicePublicAsync();
        await using var hote = Hote(proprietaire);
        using var portee = hote.Services.CreateScope();
        var seance = await OuvrirAsync(portee.ServiceProvider);

        var (premiere, _) = await NoterAsync(portee.ServiceProvider, seance, exercice, "pain");
        Assert.Equal(StatusCodes.Status204NoContent, premiere);

        var (seconde, _) = await NoterAsync(portee.ServiceProvider, seance, exercice, "good");
        Assert.Equal(StatusCodes.Status204NoContent, seconde);

        var historique = await ListerAsync(portee.ServiceProvider, exercice);
        Assert.Single(historique);
        Assert.Equal("good", historique[0].GetProperty("ressenti").GetString());
    }

    [Fact]
    public async Task Une_valeur_saisie_en_MAJUSCULES_est_normalisee()
    {
        // La contrainte `CHECK` n'accepte que la forme minuscule. Sans
        // normalisation, « PAIN » serait accepté par la validation applicative
        // — qui tolère la casse — et refusé par le moteur : un 500 sur une
        // saisie que le produit venait de déclarer valide.
        var proprietaire = await CompteAsync();
        var exercice = await ExercicePublicAsync();
        await using var hote = Hote(proprietaire);
        using var portee = hote.Services.CreateScope();
        var seance = await OuvrirAsync(portee.ServiceProvider);

        var (code, _) = await NoterAsync(portee.ServiceProvider, seance, exercice, "PAIN");
        Assert.Equal(StatusCodes.Status204NoContent, code);

        var historique = await ListerAsync(portee.ServiceProvider, exercice);
        Assert.Equal("pain", historique[0].GetProperty("ressenti").GetString());
    }

    // ================================================================
    // L'isolation — et LE `with check`
    // ================================================================

    [Fact]
    public async Task INSERER_un_ressenti_sur_la_seance_d_AUTRUI_est_refuse()
    {
        // LE `with check`, provoqué. Une épreuve qui ne ferait que LIRE
        // laisserait cette moitié de la politique se retirer sans rougir : le
        // `using` refuserait la relecture, et tout semblerait normal — pendant
        // qu'une ligne étrangère compterait dans les seuils de la victime.
        var a = await CompteAsync();
        var b = await CompteAsync();
        var exercice = await ExercicePublicAsync();

        Guid seanceDeA;
        await using (var hoteA = Hote(a))
        {
            using var porteeA = hoteA.Services.CreateScope();
            seanceDeA = await OuvrirAsync(porteeA.ServiceProvider);
        }

        await using var hoteB = Hote(b);
        using var porteeB = hoteB.Services.CreateScope();
        var (code, corps) = await NoterAsync(porteeB.ServiceProvider, seanceDeA, exercice, "pain");

        Assert.Equal(StatusCodes.Status404NotFound, code);
        Assert.Equal("SeanceIntrouvable", HarnaisHttp.Code(corps));

        // ET rien n'a été écrit, ce que seul le rôle des migrations peut
        // vérifier : sous `palier_app`, la ligne serait invisible de toute
        // façon et l'assertion passerait pour la mauvaise raison.
        Assert.Equal(0, await CompterEnBaseAsync(seanceDeA));
    }

    [Fact]
    public async Task L_historique_de_B_ne_porte_AUCUN_ressenti_de_A()
    {
        var a = await CompteAsync();
        var b = await CompteAsync();
        var exercice = await ExercicePublicAsync();

        await using (var hoteA = Hote(a))
        {
            using var porteeA = hoteA.Services.CreateScope();
            var seanceA = await OuvrirAsync(porteeA.ServiceProvider);
            await NoterAsync(porteeA.ServiceProvider, seanceA, exercice, "pain");
        }

        await using var hoteB = Hote(b);
        using var porteeB = hoteB.Services.CreateScope();
        var seanceB = await OuvrirAsync(porteeB.ServiceProvider);
        await NoterAsync(porteeB.ServiceProvider, seanceB, exercice, "good");

        var historique = await ListerAsync(porteeB.ServiceProvider, exercice);

        Assert.Single(historique);
        Assert.Equal("good", historique[0].GetProperty("ressenti").GetString());
    }

    // ================================================================
    // L'historique
    // ================================================================

    [Fact]
    public async Task L_historique_sort_du_PLUS_RECENT_au_plus_ancien()
    {
        // L'ordre n'est pas cosmétique : les seuils du § 5 parlent de valeurs
        // CONSÉCUTIVES — « 2 pain consécutifs », « 3 meh consécutifs ». Un
        // historique rendu dans le désordre ferait conclure sur des suites qui
        // n'ont pas eu lieu, et l'une de ces conclusions oriente vers un
        // professionnel.
        var proprietaire = await CompteAsync();
        var exercice = await ExercicePublicAsync();
        await using var hote = Hote(proprietaire);
        using var portee = hote.Services.CreateScope();

        foreach (var (jour, valeur) in new[] { (1, "good"), (8, "meh"), (15, "pain") })
        {
            var seance = await OuvrirAsync(
                portee.ServiceProvider,
                new DateTimeOffset(2026, 8, jour, 9, 0, 0, TimeSpan.Zero)
            );
            await NoterAsync(
                portee.ServiceProvider,
                seance,
                exercice,
                valeur,
                new HorlogeFixe(new DateTimeOffset(2026, 8, jour, 10, 0, 0, TimeSpan.Zero))
            );
        }

        var historique = await ListerAsync(portee.ServiceProvider, exercice);
        var valeurs = historique.Select(h => h.GetProperty("ressenti").GetString()).ToList();

        Assert.Equal(["pain", "meh", "good"], valeurs);
    }

    [Fact]
    public async Task Un_exercice_SANS_ressenti_rend_une_liste_VIDE()
    {
        var proprietaire = await CompteAsync();
        var exercice = await ExercicePublicAsync();
        await using var hote = Hote(proprietaire);
        using var portee = hote.Services.CreateScope();

        Assert.Empty(await ListerAsync(portee.ServiceProvider, exercice));
    }

    [Fact]
    public async Task L_historique_d_un_exercice_INCONNU_rend_404()
    {
        var proprietaire = await CompteAsync();
        await using var hote = Hote(proprietaire);
        using var portee = hote.Services.CreateScope();

        var (code, corps) = await HarnaisHttp.ExecuterAsync(
            portee.ServiceProvider,
            RessentiParExercice.ListerAsync(
                Guid.NewGuid(),
                null,
                portee.ServiceProvider.GetRequiredService<IExecuteurDeCasDUsage>(),
                portee.ServiceProvider.GetRequiredService<ListerLesRessentis>(),
                CancellationToken.None
            )
        );

        Assert.Equal(StatusCodes.Status404NotFound, code);
        Assert.Equal("ExerciceIntrouvable", HarnaisHttp.Code(corps));
    }

    // ================================================================
    // Les refus
    // ================================================================

    [Fact]
    public async Task Un_ressenti_INVALIDE_est_refuse_avant_toute_ecriture()
    {
        var proprietaire = await CompteAsync();
        var exercice = await ExercicePublicAsync();
        await using var hote = Hote(proprietaire);
        using var portee = hote.Services.CreateScope();
        var seance = await OuvrirAsync(portee.ServiceProvider);

        var (code, corps) = await NoterAsync(portee.ServiceProvider, seance, exercice, "excellent");

        Assert.Equal(StatusCodes.Status400BadRequest, code);
        Assert.Equal("RessentiInvalide", HarnaisHttp.Code(corps));
        Assert.Empty(await ListerAsync(portee.ServiceProvider, exercice));
    }

    [Fact]
    public async Task Un_exercice_INCONNU_est_refuse_par_un_400()
    {
        // `FK_exercise_feedback_exercises_exercise_id` refuserait par une
        // violation de clé étrangère — un 500 pour une saisie.
        var proprietaire = await CompteAsync();
        await using var hote = Hote(proprietaire);
        using var portee = hote.Services.CreateScope();
        var seance = await OuvrirAsync(portee.ServiceProvider);

        var (code, corps) = await NoterAsync(
            portee.ServiceProvider,
            seance,
            Guid.NewGuid(),
            "good"
        );

        Assert.Equal(StatusCodes.Status400BadRequest, code);
        Assert.Equal("ExerciceIntrouvable", HarnaisHttp.Code(corps));
    }

    [Fact]
    public async Task Supprimer_la_SEANCE_emporte_ses_ressentis()
    {
        var proprietaire = await CompteAsync();
        var exercice = await ExercicePublicAsync();
        await using var hote = Hote(proprietaire);
        using var portee = hote.Services.CreateScope();
        var seance = await OuvrirAsync(portee.ServiceProvider);
        await NoterAsync(portee.ServiceProvider, seance, exercice, "meh");

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
        Assert.Equal(0, await CompterEnBaseAsync(seance));
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

    private static Task<(int Code, string Corps)> NoterAsync(
        IServiceProvider services,
        Guid seance,
        Guid exercice,
        string valeur,
        TimeProvider? horloge = null
    ) =>
        HarnaisHttp.ExecuterAsync(
            services,
            RessentiParExercice.NoterAsync(
                seance,
                new NoteDeRessenti(exercice, valeur),
                services.GetRequiredService<IExecuteurDeCasDUsage>(),
                services.GetRequiredService<NoterUnRessenti>(),
                horloge ?? TimeProvider.System,
                CancellationToken.None
            )
        );

    private static async Task<IReadOnlyList<JsonElement>> ListerAsync(
        IServiceProvider services,
        Guid exercice
    )
    {
        var (code, corps) = await HarnaisHttp.ExecuterAsync(
            services,
            RessentiParExercice.ListerAsync(
                exercice,
                null,
                services.GetRequiredService<IExecuteurDeCasDUsage>(),
                services.GetRequiredService<ListerLesRessentis>(),
                CancellationToken.None
            )
        );

        Assert.Equal(StatusCodes.Status200OK, code);
        return [.. JsonDocument.Parse(corps).RootElement.EnumerateArray().Select(e => e.Clone())];
    }

    /// <summary>
    /// Compte sous <c>palier_migrations</c>, qui voit tout. La question est
    /// « la ligne existe-t-elle », pas « l'appelant la voit-il » : sous
    /// <c>palier_app</c>, une ligne étrangère rendrait zéro même si elle avait
    /// été écrite.
    /// </summary>
    private async Task<long> CompterEnBaseAsync(Guid seance)
    {
        await using var connexion = new NpgsqlConnection(baseDeDonnees.ChaineMigrations);
        await connexion.OpenAsync();
        await using var commande = new NpgsqlCommand(
            "select count(*) from public.exercise_feedback where workout_id = $1",
            connexion
        );
        commande.Parameters.AddWithValue(seance);
        return (long)(await commande.ExecuteScalarAsync())!;
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
            $"ressenti-{Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)}"
        );
        return (Guid)(await commande.ExecuteScalarAsync())!;
    }

    private async Task<Guid> CompteAsync()
    {
        await using var hote = HarnaisHttp.Hote(baseDeDonnees);
        using var portee = hote.Services.CreateScope();
        var utilisateurs = portee.ServiceProvider.GetRequiredService<UserManager<Utilisateur>>();

        var email =
            "ressenti-"
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
