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
/// Le poids corporel, sur le moteur réel.
/// </summary>
/// <remarks>
/// <b>Ce que ces épreuves gardent avant tout : l'idempotence par jour.</b>
/// <c>body_weight</c> porte <c>unique (owner_id, measured_on)</c>. Sans
/// traitement, la seconde saisie du même jour remonterait une violation de
/// contrainte — donc un 500 — à quelqu'un qui corrige une faute de frappe.
/// C'est le genre de défaut qui ne se voit jamais en développement, où l'on
/// saisit une valeur par jour et une seule.
/// </remarks>
[Collection(BaseFixture.Collection)]
public sealed class EntrainementPoidsTests(BaseFixture baseDeDonnees)
{
    private const string _motDePasse = "brouette-hivernale-38-oscille";

    // ================================================================
    // L'idempotence
    // ================================================================

    [Fact]
    public async Task Deux_pesees_LE_MEME_JOUR_remplacent_au_lieu_d_echouer()
    {
        var proprietaire = await CompteAsync();
        var horloge = new HorlogeFixe(new DateTimeOffset(2026, 8, 23, 10, 0, 0, TimeSpan.Zero));
        await using var hote = Hote(proprietaire);
        using var portee = hote.Services.CreateScope();

        var (premiere, _) = await EnregistrerAsync(portee.ServiceProvider, horloge, null, 82.4m);
        Assert.Equal(StatusCodes.Status200OK, premiere);

        // La correction. Sans le traitement d'idempotence, c'est ici que
        // `23505` remonterait.
        var (seconde, corps) = await EnregistrerAsync(portee.ServiceProvider, horloge, null, 81.9m);
        Assert.Equal(StatusCodes.Status200OK, seconde);
        Assert.Equal(81.9m, JsonDocument.Parse(corps).RootElement.GetProperty("poidsKg").GetDecimal());

        // UNE seule ligne, portant la valeur corrigée.
        var mesures = await LireAsync(portee.ServiceProvider, horloge);
        Assert.Single(mesures.GetProperty("mesures").EnumerateArray());
        Assert.Equal(
            81.9m,
            mesures.GetProperty("mesures")[0].GetProperty("poidsKg").GetDecimal()
        );
    }

    [Fact]
    public async Task Deux_pesees_a_des_JOURS_DIFFERENTS_coexistent()
    {
        var proprietaire = await CompteAsync();
        var horloge = new HorlogeFixe(new DateTimeOffset(2026, 8, 23, 10, 0, 0, TimeSpan.Zero));
        await using var hote = Hote(proprietaire);
        using var portee = hote.Services.CreateScope();

        await EnregistrerAsync(portee.ServiceProvider, horloge, new DateOnly(2026, 8, 22), 82.4m);
        await EnregistrerAsync(portee.ServiceProvider, horloge, new DateOnly(2026, 8, 23), 82.1m);

        var serie = await LireAsync(portee.ServiceProvider, horloge);
        var mesures = serie.GetProperty("mesures").EnumerateArray().ToList();

        Assert.Equal(2, mesures.Count);

        // DE LA PLUS ANCIENNE À LA PLUS RÉCENTE. Une courbe tracée dans le
        // désordre est une courbe fausse, et PostgreSQL ne garantit aucun
        // ordre sans `ORDER BY`.
        Assert.Equal("2026-08-22", mesures[0].GetProperty("jour").GetString());
        Assert.Equal("2026-08-23", mesures[1].GetProperty("jour").GetString());
    }

    // ================================================================
    // Le constat de sécurité
    // ================================================================

    [Fact]
    public async Task Une_perte_RAPIDE_produit_un_constat_SANS_bloquer_la_saisie()
    {
        // Quatre semaines à −1,5 % par semaine, pesées tous les jours — le cas
        // réel, pas une série hebdomadaire de laboratoire.
        var proprietaire = await CompteAsync();
        var horloge = new HorlogeFixe(new DateTimeOffset(2026, 8, 30, 10, 0, 0, TimeSpan.Zero));
        await using var hote = Hote(proprietaire);
        using var portee = hote.Services.CreateScope();

        var poids = 80m;
        for (var semaine = 0; semaine < 4; semaine++)
        {
            for (var jour = 0; jour < 7; jour++)
            {
                var (code, _) = await EnregistrerAsync(
                    portee.ServiceProvider,
                    horloge,
                    new DateOnly(2026, 8, 3).AddDays((semaine * 7) + jour),
                    poids
                );

                // LA SAISIE N'EST JAMAIS BLOQUÉE. `01-conformite.md` § 5
                // impose « un message d'orientation, jamais de renforcement de
                // la restriction » — et un produit qui refuserait la saisie se
                // ferait contourner en cessant de saisir, ce qui supprime
                // justement le signal.
                Assert.Equal(StatusCodes.Status200OK, code);
            }

            poids *= 0.985m;
        }

        var serie = await LireAsync(portee.ServiceProvider, horloge);
        Assert.Equal("PerteRapide", serie.GetProperty("constat").GetString());
    }

    [Fact]
    public async Task Un_poids_STABLE_ne_produit_AUCUN_constat()
    {
        // L'autre bord, sans lequel une détection qui crierait toujours
        // passerait l'épreuve précédente. Un garde-fou qui ne se tait jamais
        // est un garde-fou qu'on désactive.
        var proprietaire = await CompteAsync();
        var horloge = new HorlogeFixe(new DateTimeOffset(2026, 8, 30, 10, 0, 0, TimeSpan.Zero));
        await using var hote = Hote(proprietaire);
        using var portee = hote.Services.CreateScope();

        for (var jour = 0; jour < 28; jour++)
        {
            await EnregistrerAsync(
                portee.ServiceProvider,
                horloge,
                new DateOnly(2026, 8, 3).AddDays(jour),
                80m
            );
        }

        var serie = await LireAsync(portee.ServiceProvider, horloge);
        Assert.Equal(JsonValueKind.Null, serie.GetProperty("constat").ValueKind);
    }

    [Fact]
    public async Task Une_FLUCTUATION_QUOTIDIENNE_ne_declenche_pas_le_constat()
    {
        // LE CAS QUI JUSTIFIE LA MOYENNE HEBDOMADAIRE. Le poids varie de 1 à
        // 2 % d'un jour à l'autre — eau, glycogène, contenu digestif — soit
        // PLUS que le seuil de 1 %. Sur un poids stable qui oscille, un
        // échantillon hebdomadaire unique tomberait tôt ou tard sur quatre
        // creux successifs et crierait pour rien.
        var proprietaire = await CompteAsync();
        var horloge = new HorlogeFixe(new DateTimeOffset(2026, 8, 30, 10, 0, 0, TimeSpan.Zero));
        await using var hote = Hote(proprietaire);
        using var portee = hote.Services.CreateScope();

        // Chaque dimanche — dernier jour de la semaine ISO — est 1,5 % plus
        // bas que le précédent, alors que la moyenne de chaque semaine, elle,
        // ne bouge pas.
        var creux = new[] { 80m, 78.8m, 77.6m, 76.4m };
        for (var semaine = 0; semaine < 4; semaine++)
        {
            for (var jour = 0; jour < 7; jour++)
            {
                var date = new DateOnly(2026, 8, 3).AddDays((semaine * 7) + jour);
                var valeur = jour == 6 ? creux[semaine] : 80m + (semaine * 0.4m);
                await EnregistrerAsync(portee.ServiceProvider, horloge, date, valeur);
            }
        }

        var serie = await LireAsync(portee.ServiceProvider, horloge);
        Assert.Equal(JsonValueKind.Null, serie.GetProperty("constat").ValueKind);
    }

    // ================================================================
    // Les refus
    // ================================================================

    [Fact]
    public async Task Un_poids_HORS_BORNES_est_refuse_avant_toute_ecriture()
    {
        var proprietaire = await CompteAsync();
        var horloge = new HorlogeFixe(new DateTimeOffset(2026, 8, 23, 10, 0, 0, TimeSpan.Zero));
        await using var hote = Hote(proprietaire);
        using var portee = hote.Services.CreateScope();

        var (code, corps) = await EnregistrerAsync(portee.ServiceProvider, horloge, null, 0m);

        Assert.Equal(StatusCodes.Status400BadRequest, code);
        Assert.Equal("PoidsInvalide", HarnaisHttp.Code(corps));

        // Et RIEN n'a été écrit.
        var serie = await LireAsync(portee.ServiceProvider, horloge);
        Assert.Empty(serie.GetProperty("mesures").EnumerateArray());
    }

    [Fact]
    public async Task Une_pesee_DANS_LE_FUTUR_est_refusee()
    {
        var proprietaire = await CompteAsync();
        var horloge = new HorlogeFixe(new DateTimeOffset(2026, 8, 23, 10, 0, 0, TimeSpan.Zero));
        await using var hote = Hote(proprietaire);
        using var portee = hote.Services.CreateScope();

        var (code, corps) = await EnregistrerAsync(
            portee.ServiceProvider,
            horloge,
            new DateOnly(2026, 8, 24),
            82m
        );

        Assert.Equal(StatusCodes.Status400BadRequest, code);
        Assert.Equal("JourDansLeFutur", HarnaisHttp.Code(corps));
    }

    // ================================================================
    // L'isolation
    // ================================================================

    [Fact]
    public async Task La_serie_de_B_ne_porte_AUCUNE_pesee_de_A()
    {
        var a = await CompteAsync();
        var b = await CompteAsync();
        var horloge = new HorlogeFixe(new DateTimeOffset(2026, 8, 23, 10, 0, 0, TimeSpan.Zero));

        await using (var hoteA = Hote(a))
        {
            using var porteeA = hoteA.Services.CreateScope();
            await EnregistrerAsync(porteeA.ServiceProvider, horloge, null, 95m);
        }

        await using var hoteB = Hote(b);
        using var porteeB = hoteB.Services.CreateScope();
        await EnregistrerAsync(porteeB.ServiceProvider, horloge, null, 62m);

        var serie = await LireAsync(porteeB.ServiceProvider, horloge);
        var poids = serie
            .GetProperty("mesures")
            .EnumerateArray()
            .Select(m => m.GetProperty("poidsKg").GetDecimal())
            .ToList();

        Assert.Equal([62m], poids);
    }

    [Fact]
    public async Task Le_MEME_JOUR_chez_DEUX_utilisateurs_ne_se_marchent_pas_dessus()
    {
        // La contrainte d'unicité porte sur `(owner_id, measured_on)`, pas sur
        // `measured_on` seul. Si l'idempotence cherchait la pesée du jour SANS
        // que RLS morde, elle trouverait celle de l'autre et l'écraserait —
        // une perte de donnée silencieuse chez un tiers.
        var a = await CompteAsync();
        var b = await CompteAsync();
        var horloge = new HorlogeFixe(new DateTimeOffset(2026, 8, 23, 10, 0, 0, TimeSpan.Zero));
        var jour = new DateOnly(2026, 8, 23);

        await using (var hoteA = Hote(a))
        {
            using var porteeA = hoteA.Services.CreateScope();
            await EnregistrerAsync(porteeA.ServiceProvider, horloge, jour, 95m);
        }

        await using (var hoteB = Hote(b))
        {
            using var porteeB = hoteB.Services.CreateScope();
            var (code, _) = await EnregistrerAsync(porteeB.ServiceProvider, horloge, jour, 62m);
            Assert.Equal(StatusCodes.Status200OK, code);
        }

        // A a TOUJOURS sa pesée, inchangée.
        await using var hoteA2 = Hote(a);
        using var porteeA2 = hoteA2.Services.CreateScope();
        var serie = await LireAsync(porteeA2.ServiceProvider, horloge);

        Assert.Single(serie.GetProperty("mesures").EnumerateArray());
        Assert.Equal(95m, serie.GetProperty("mesures")[0].GetProperty("poidsKg").GetDecimal());
    }

    // ================================================================
    // La fenêtre
    // ================================================================

    [Fact]
    public async Task Une_pesee_HORS_de_la_fenetre_ne_sort_pas()
    {
        var proprietaire = await CompteAsync();
        var horloge = new HorlogeFixe(new DateTimeOffset(2026, 8, 23, 10, 0, 0, TimeSpan.Zero));
        await using var hote = Hote(proprietaire);
        using var portee = hote.Services.CreateScope();

        await EnregistrerAsync(portee.ServiceProvider, horloge, new DateOnly(2026, 1, 1), 90m);
        await EnregistrerAsync(portee.ServiceProvider, horloge, new DateOnly(2026, 8, 20), 82m);

        // Fenêtre par défaut : 90 jours. Le 1er janvier est dehors.
        var serie = await LireAsync(portee.ServiceProvider, horloge);
        Assert.Single(serie.GetProperty("mesures").EnumerateArray());

        // Élargie, elle sort. Sans cette seconde moitié, une fenêtre qui
        // n'aurait AUCUN effet passerait l'épreuve précédente.
        var large = await LireAsync(portee.ServiceProvider, horloge, jours: 365);
        Assert.Equal(2, large.GetProperty("mesures").EnumerateArray().Count());
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

    private static Task<(int Code, string Corps)> EnregistrerAsync(
        IServiceProvider services,
        TimeProvider horloge,
        DateOnly? jour,
        decimal poids
    ) =>
        HarnaisHttp.ExecuterAsync(
            services,
            PoidsCorporel.EnregistrerAsync(
                new MesureDePoids(jour, poids),
                services.GetRequiredService<IExecuteurDeCasDUsage>(),
                services.GetRequiredService<EnregistrerLePoids>(),
                horloge,
                CancellationToken.None
            )
        );

    private static async Task<JsonElement> LireAsync(
        IServiceProvider services,
        TimeProvider horloge,
        int? jours = null
    )
    {
        var (code, corps) = await HarnaisHttp.ExecuterAsync(
            services,
            PoidsCorporel.ListerAsync(
                jours,
                services.GetRequiredService<IExecuteurDeCasDUsage>(),
                services.GetRequiredService<ListerLePoids>(),
                horloge,
                CancellationToken.None
            )
        );

        Assert.Equal(StatusCodes.Status200OK, code);
        return JsonDocument.Parse(corps).RootElement.Clone();
    }

    private async Task<Guid> CompteAsync()
    {
        await using var hote = HarnaisHttp.Hote(baseDeDonnees);
        using var portee = hote.Services.CreateScope();
        var utilisateurs = portee.ServiceProvider.GetRequiredService<UserManager<Utilisateur>>();

        var email =
            "poids-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture) + "@exemple.test";
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
