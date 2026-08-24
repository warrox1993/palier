using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Palier.Api.Entrainement;
using Palier.Application.Entrainement;
using Palier.Application.Pipeline;
using Palier.Infrastructure.Entrainement;
using Palier.Infrastructure.Identite;

namespace Palier.Database.Tests;

/// <summary>
/// La séance — le cœur du lot 5, sur le moteur réel et par la composition
/// RÉELLE de l'API.
/// </summary>
/// <remarks>
/// <para>
/// <b>Ces épreuves jugent une garantie que <c>IsolationTests</c> ne juge pas.</b>
/// Celui-ci prouve que le MOTEUR isole, en SQL nu, sans EF. Celles-ci prouvent
/// que le CHEMIN APPLICATIF isole — que le point d'entrée passe bien par le
/// pipeline, que le pipeline pose bien l'identité, et qu'aucune des couches
/// entre les deux ne contourne le dispositif. Ce sont deux propriétés
/// distinctes : la seconde peut tomber alors que la première tient.
/// </para>
///
/// <para>
/// <b>404 et non 403 sur la séance d'autrui.</b> Répondre « interdit »
/// confirmerait que l'identifiant existe — et sur un produit de santé, savoir
/// qu'une séance porte tel identifiant, c'est déjà savoir que quelqu'un
/// s'entraîne.
/// </para>
/// </remarks>
[Collection(BaseFixture.Collection)]
public sealed class EntrainementSeancesTests(BaseFixture baseDeDonnees)
{
    private const string _motDePasse = "brouette-hivernale-38-oscille";

    // ================================================================
    // Ouvrir
    // ================================================================

    [Fact]
    public async Task Une_seance_ouverte_se_relit_avec_son_identifiant()
    {
        var proprietaire = await CompteAsync();
        await using var hote = Hote(proprietaire);
        using var portee = hote.Services.CreateScope();

        var (code, corps) = await HarnaisHttp.ExecuterAsync(
            portee.ServiceProvider,
            Seances.OuvrirAsync(
                new OuvertureDeSeance(null, 7.5m, null),
                portee.ServiceProvider.GetRequiredService<IExecuteurDeCasDUsage>(),
                portee.ServiceProvider.GetRequiredService<OuvrirUneSeance>(),
                TimeProvider.System,
                CancellationToken.None
            )
        );

        Assert.Equal(StatusCodes.Status201Created, code);
        var id = JsonDocument.Parse(corps).RootElement.GetProperty("id").GetGuid();
        Assert.NotEqual(Guid.Empty, id);
    }

    // ================================================================
    // L'isolation, sur le chemin applicatif
    // ================================================================

    [Fact]
    public async Task La_seance_de_A_est_INTROUVABLE_pour_B()
    {
        var a = await CompteAsync();
        var b = await CompteAsync();

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
            Seances.LireAsync(
                seance,
                porteeB.ServiceProvider.GetRequiredService<IExecuteurDeCasDUsage>(),
                porteeB.ServiceProvider.GetRequiredService<LireUneSeance>(),
                CancellationToken.None
            )
        );

        // 404, et le code le dit. Un 403 confirmerait l'existence.
        Assert.Equal(StatusCodes.Status404NotFound, code);
        Assert.Equal("SeanceIntrouvable", HarnaisHttp.Code(corps));
    }

    // ================================================================
    // Clôturer
    // ================================================================

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(-1)]
    public async Task Une_energie_HORS_DE_UN_A_CINQ_est_refusee(int energie)
    {
        // La contrainte CHECK existe en base — mais un 500 sur violation de
        // contrainte n'est pas une réponse. Le refus se prononce AVANT, et
        // l'épreuve prouve les DEUX bords plutôt qu'un seul.
        var proprietaire = await CompteAsync();
        await using var hote = Hote(proprietaire);
        using var portee = hote.Services.CreateScope();
        var seance = await OuvrirAsync(portee.ServiceProvider);

        var (code, corps) = await HarnaisHttp.ExecuterAsync(
            portee.ServiceProvider,
            Seances.CloturerAsync(
                seance,
                new ClotureDeSeance(null, energie),
                portee.ServiceProvider.GetRequiredService<IExecuteurDeCasDUsage>(),
                portee.ServiceProvider.GetRequiredService<CloturerUneSeance>(),
                TimeProvider.System,
                CancellationToken.None
            )
        );

        Assert.Equal(StatusCodes.Status400BadRequest, code);
        Assert.Equal("EnergieHorsBornes", HarnaisHttp.Code(corps));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    public async Task Une_energie_AUX_BORNES_est_acceptee(int energie)
    {
        // L'autre bord. Sans elle, un refus trop large — « strictement entre 1
        // et 5 » — passerait inaperçu : les trois épreuves de refus resteraient
        // vertes, et le produit refuserait les deux valeurs extrêmes.
        var proprietaire = await CompteAsync();
        await using var hote = Hote(proprietaire);
        using var portee = hote.Services.CreateScope();
        var seance = await OuvrirAsync(portee.ServiceProvider);

        var (code, _) = await HarnaisHttp.ExecuterAsync(
            portee.ServiceProvider,
            Seances.CloturerAsync(
                seance,
                new ClotureDeSeance(null, energie),
                portee.ServiceProvider.GetRequiredService<IExecuteurDeCasDUsage>(),
                portee.ServiceProvider.GetRequiredService<CloturerUneSeance>(),
                TimeProvider.System,
                CancellationToken.None
            )
        );

        Assert.Equal(StatusCodes.Status200OK, code);
    }

    [Fact]
    public async Task Cloturer_la_seance_d_AUTRUI_est_INTROUVABLE()
    {
        var a = await CompteAsync();
        var b = await CompteAsync();

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
            Seances.CloturerAsync(
                seance,
                new ClotureDeSeance(null, 3),
                porteeB.ServiceProvider.GetRequiredService<IExecuteurDeCasDUsage>(),
                porteeB.ServiceProvider.GetRequiredService<CloturerUneSeance>(),
                TimeProvider.System,
                CancellationToken.None
            )
        );

        Assert.Equal(StatusCodes.Status404NotFound, code);
        Assert.Equal("SeanceIntrouvable", HarnaisHttp.Code(corps));
    }

    [Fact]
    public async Task Rejouer_la_CLOTURE_sans_Fin_PRESERVE_la_fin_deja_posee()
    {
        // LE CAS QUE LA REVUE A TROUVÉ. `PATCH` est une mise à jour PARTIELLE :
        // rejouer `{ energie: 4 }` sur une séance déjà close ne doit pas la
        // faire finir à l'heure du rejeu.
        //
        // Avant la correction, une séance d'une heure close à 10 h devenait
        // une séance de six heures dès qu'on notait son énergie à 15 h — sans
        // erreur, sans trace, et avec une durée fausse dans l'historique.
        var proprietaire = await CompteAsync();
        await using var hote = Hote(proprietaire);
        using var portee = hote.Services.CreateScope();

        var seance = await OuvrirAsync(
            portee.ServiceProvider,
            new DateTimeOffset(2026, 8, 24, 9, 0, 0, TimeSpan.Zero)
        );

        // Première clôture : la fin est donnée explicitement.
        var fin = new DateTimeOffset(2026, 8, 24, 10, 0, 0, TimeSpan.Zero);
        await HarnaisHttp.ExecuterAsync(
            portee.ServiceProvider,
            Seances.CloturerAsync(
                seance,
                new ClotureDeSeance(fin, null),
                portee.ServiceProvider.GetRequiredService<IExecuteurDeCasDUsage>(),
                portee.ServiceProvider.GetRequiredService<CloturerUneSeance>(),
                TimeProvider.System,
                CancellationToken.None
            )
        );

        // Cinq heures plus tard, l'utilisateur note son énergie. L'horloge du
        // rejeu est explicitement DIFFÉRENTE : c'est ce qui rendrait le défaut
        // visible.
        var (code, corps) = await HarnaisHttp.ExecuterAsync(
            portee.ServiceProvider,
            Seances.CloturerAsync(
                seance,
                new ClotureDeSeance(null, 4),
                portee.ServiceProvider.GetRequiredService<IExecuteurDeCasDUsage>(),
                portee.ServiceProvider.GetRequiredService<CloturerUneSeance>(),
                new HorlogeFixe(new DateTimeOffset(2026, 8, 24, 15, 0, 0, TimeSpan.Zero)),
                CancellationToken.None
            )
        );

        Assert.Equal(StatusCodes.Status200OK, code);

        var rendue = JsonDocument.Parse(corps).RootElement;
        Assert.Equal(4, rendue.GetProperty("energie").GetInt32());
        Assert.Equal(fin, rendue.GetProperty("fin").GetDateTimeOffset());
    }

    [Fact]
    public async Task Cloturer_SANS_Fin_une_seance_JAMAIS_close_prend_l_horloge()
    {
        // L'autre bord : la préservation ne doit pas empêcher la PREMIÈRE
        // clôture de dater la séance. Sans cette épreuve, un `?? seance.EndedAt`
        // mal placé laisserait `Fin` à null indéfiniment.
        var proprietaire = await CompteAsync();
        await using var hote = Hote(proprietaire);
        using var portee = hote.Services.CreateScope();
        var seance = await OuvrirAsync(portee.ServiceProvider);

        var maintenant = new DateTimeOffset(2026, 8, 24, 11, 0, 0, TimeSpan.Zero);
        var (code, corps) = await HarnaisHttp.ExecuterAsync(
            portee.ServiceProvider,
            Seances.CloturerAsync(
                seance,
                new ClotureDeSeance(null, 3),
                portee.ServiceProvider.GetRequiredService<IExecuteurDeCasDUsage>(),
                portee.ServiceProvider.GetRequiredService<CloturerUneSeance>(),
                new HorlogeFixe(maintenant),
                CancellationToken.None
            )
        );

        Assert.Equal(StatusCodes.Status200OK, code);
        Assert.Equal(
            maintenant,
            JsonDocument.Parse(corps).RootElement.GetProperty("fin").GetDateTimeOffset()
        );
    }

    // ================================================================
    // Supprimer
    // ================================================================

    [Fact]
    public async Task Une_seance_supprimee_ne_se_relit_plus()
    {
        var proprietaire = await CompteAsync();
        await using var hote = Hote(proprietaire);
        using var portee = hote.Services.CreateScope();
        var seance = await OuvrirAsync(portee.ServiceProvider);

        var (suppression, _) = await HarnaisHttp.ExecuterAsync(
            portee.ServiceProvider,
            Seances.SupprimerAsync(
                seance,
                portee.ServiceProvider.GetRequiredService<IExecuteurDeCasDUsage>(),
                portee.ServiceProvider.GetRequiredService<SupprimerUneSeance>(),
                CancellationToken.None
            )
        );
        Assert.Equal(StatusCodes.Status204NoContent, suppression);

        var (relecture, _) = await HarnaisHttp.ExecuterAsync(
            portee.ServiceProvider,
            Seances.LireAsync(
                seance,
                portee.ServiceProvider.GetRequiredService<IExecuteurDeCasDUsage>(),
                portee.ServiceProvider.GetRequiredService<LireUneSeance>(),
                CancellationToken.None
            )
        );
        Assert.Equal(StatusCodes.Status404NotFound, relecture);
    }

    [Fact]
    public async Task Supprimer_la_seance_d_AUTRUI_est_INTROUVABLE()
    {
        var a = await CompteAsync();
        var b = await CompteAsync();

        Guid seance;
        await using (var hoteA = Hote(a))
        {
            using var porteeA = hoteA.Services.CreateScope();
            seance = await OuvrirAsync(porteeA.ServiceProvider);
        }

        await using var hoteB = Hote(b);
        using var porteeB = hoteB.Services.CreateScope();

        var (code, _) = await HarnaisHttp.ExecuterAsync(
            porteeB.ServiceProvider,
            Seances.SupprimerAsync(
                seance,
                porteeB.ServiceProvider.GetRequiredService<IExecuteurDeCasDUsage>(),
                porteeB.ServiceProvider.GetRequiredService<SupprimerUneSeance>(),
                CancellationToken.None
            )
        );

        Assert.Equal(StatusCodes.Status404NotFound, code);

        // ET la séance de A est TOUJOURS LÀ. Sans cette seconde assertion, un
        // `DELETE` qui aurait effacé la ligne avant de rendre 404 passerait.
        await using var hoteA2 = Hote(a);
        using var porteeA2 = hoteA2.Services.CreateScope();
        var (relecture, _) = await HarnaisHttp.ExecuterAsync(
            porteeA2.ServiceProvider,
            Seances.LireAsync(
                seance,
                porteeA2.ServiceProvider.GetRequiredService<IExecuteurDeCasDUsage>(),
                porteeA2.ServiceProvider.GetRequiredService<LireUneSeance>(),
                CancellationToken.None
            )
        );
        Assert.Equal(StatusCodes.Status200OK, relecture);
    }

    // ================================================================
    // La pagination
    // ================================================================

    [Fact]
    public async Task La_pagination_par_curseur_ne_REPETE_ni_ne_SAUTE_aucune_seance()
    {
        // Cinq séances, deux par page. On parcourt jusqu'au bout et on
        // assertionne que les cinq identifiants sont sortis, CHACUN UNE FOIS.
        //
        // C'est l'épreuve qui distingue le curseur du décalage. Avec un
        // `OFFSET`, une insertion entre deux pages ferait rater une ligne — et
        // rien ne le signalerait, ni erreur, ni trou visible dans la réponse.
        var proprietaire = await CompteAsync();
        await using var hote = Hote(proprietaire);
        using var portee = hote.Services.CreateScope();

        var attendus = new HashSet<Guid>();
        for (var i = 0; i < 5; i++)
        {
            attendus.Add(await OuvrirAsync(portee.ServiceProvider));
        }

        var vus = new List<Guid>();
        DateTimeOffset? avant = null;
        Guid? avantId = null;

        // La borne de boucle est une SÉCURITÉ, pas une attente : sans elle, un
        // curseur qui ne progresse pas ferait tourner l'épreuve indéfiniment
        // au lieu de rougir.
        for (var tour = 0; tour < 10; tour++)
        {
            var (code, corps) = await HarnaisHttp.ExecuterAsync(
                portee.ServiceProvider,
                Seances.ListerAsync(
                    avant,
                    avantId,
                    2,
                    portee.ServiceProvider.GetRequiredService<IExecuteurDeCasDUsage>(),
                    portee.ServiceProvider.GetRequiredService<ListerLesSeances>(),
                    CancellationToken.None
                )
            );

            Assert.Equal(StatusCodes.Status200OK, code);
            var page = JsonDocument.Parse(corps).RootElement;

            foreach (var element in page.GetProperty("elements").EnumerateArray())
            {
                vus.Add(element.GetProperty("id").GetGuid());
            }

            var suite = page.GetProperty("suivantAvantId");
            if (suite.ValueKind == JsonValueKind.Null)
            {
                break;
            }

            avant = page.GetProperty("suivantAvant").GetDateTimeOffset();
            avantId = suite.GetGuid();
        }

        // AUCUN doublon : `vus` compte autant d'éléments que d'identifiants
        // distincts. Un curseur mal borné — `<=` au lieu de `<` — rendrait la
        // dernière séance de chaque page deux fois, et une assertion qui ne
        // regarderait que l'ensemble ne le verrait pas.
        Assert.Equal(vus.Count, vus.Distinct().Count());

        // ET aucun trou : les cinq sont là, ni plus ni moins.
        Assert.Equal(attendus, vus.ToHashSet());
    }

    [Fact]
    public async Task Deux_seances_au_MEME_INSTANT_sortent_toutes_les_deux()
    {
        // Le cas que le curseur composite existe pour couvrir. Avec un curseur
        // réduit à l'instant, la seconde séance de la paire serait sautée : le
        // `<` strict l'exclurait, et elle deviendrait invisible pour toujours.
        var proprietaire = await CompteAsync();
        await using var hote = Hote(proprietaire);
        using var portee = hote.Services.CreateScope();

        var instant = new DateTimeOffset(2026, 8, 23, 9, 0, 0, TimeSpan.Zero);
        var premiere = await OuvrirAsync(portee.ServiceProvider, instant);
        var seconde = await OuvrirAsync(portee.ServiceProvider, instant);

        var (_, page1) = await HarnaisHttp.ExecuterAsync(
            portee.ServiceProvider,
            Seances.ListerAsync(
                null,
                null,
                1,
                portee.ServiceProvider.GetRequiredService<IExecuteurDeCasDUsage>(),
                portee.ServiceProvider.GetRequiredService<ListerLesSeances>(),
                CancellationToken.None
            )
        );

        var racine = JsonDocument.Parse(page1).RootElement;
        var (_, page2) = await HarnaisHttp.ExecuterAsync(
            portee.ServiceProvider,
            Seances.ListerAsync(
                racine.GetProperty("suivantAvant").GetDateTimeOffset(),
                racine.GetProperty("suivantAvantId").GetGuid(),
                1,
                portee.ServiceProvider.GetRequiredService<IExecuteurDeCasDUsage>(),
                portee.ServiceProvider.GetRequiredService<ListerLesSeances>(),
                CancellationToken.None
            )
        );

        var sorties = new[] { page1, page2 }
            .SelectMany(p =>
                JsonDocument
                    .Parse(p)
                    .RootElement.GetProperty("elements")
                    .EnumerateArray()
                    .Select(e => e.GetProperty("id").GetGuid())
            )
            .ToHashSet();

        Assert.Contains(premiere, sorties);
        Assert.Contains(seconde, sorties);
    }

    [Fact]
    public async Task La_liste_de_B_ne_porte_AUCUNE_seance_de_A()
    {
        var a = await CompteAsync();
        var b = await CompteAsync();

        Guid deA;
        await using (var hoteA = Hote(a))
        {
            using var porteeA = hoteA.Services.CreateScope();
            deA = await OuvrirAsync(porteeA.ServiceProvider);
        }

        await using var hoteB = Hote(b);
        using var porteeB = hoteB.Services.CreateScope();
        var deB = await OuvrirAsync(porteeB.ServiceProvider);

        var (code, corps) = await HarnaisHttp.ExecuterAsync(
            porteeB.ServiceProvider,
            Seances.ListerAsync(
                null,
                null,
                null,
                porteeB.ServiceProvider.GetRequiredService<IExecuteurDeCasDUsage>(),
                porteeB.ServiceProvider.GetRequiredService<ListerLesSeances>(),
                CancellationToken.None
            )
        );

        Assert.Equal(StatusCodes.Status200OK, code);
        var identifiants = JsonDocument
            .Parse(corps)
            .RootElement.GetProperty("elements")
            .EnumerateArray()
            .Select(e => e.GetProperty("id").GetGuid())
            .ToHashSet();

        Assert.Contains(deB, identifiants);
        Assert.DoesNotContain(deA, identifiants);
    }

    // ================================================================
    // Le harnais
    // ================================================================

    /// <summary>
    /// L'API réelle, où l'identité est celle qu'on donne plutôt que celle d'un
    /// jeton. C'est le SEUL point remplacé : tout le reste — pipeline,
    /// contexte, politiques — vient de <c>Composition.Composer</c>.
    /// </summary>
    private WebApplication Hote(Guid proprietaire) =>
        HarnaisHttp.Hote(
            baseDeDonnees,
            ajuster: services =>
                services.AddScoped<IIdentiteDemandeur>(_ => new DemandeurFixe(proprietaire))
        );

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

    private async Task<Guid> CompteAsync()
    {
        await using var hote = HarnaisHttp.Hote(baseDeDonnees);
        using var portee = hote.Services.CreateScope();
        var utilisateurs = portee.ServiceProvider.GetRequiredService<UserManager<Utilisateur>>();

        var email =
            "seance-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture) + "@exemple.test";
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
