using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Palier.Api;

namespace Palier.Database.Tests;

/// <summary>
/// Le document OpenAPI et l'interface Scalar n'existent qu'en Development — D82.
/// </summary>
/// <remarks>
/// L'application est composée par <see cref="Composition" />, la vraie, puis on
/// lit la table de routage : c'est elle qui dit ce qui répond, pas la ligne qui
/// l'enregistre. Le contenu du document est lu sur l'application DÉMARRÉE,
/// dans ce processus : Kestrel sur un port libre, la base de la fixture, et
/// l'assertion d'isolation de D37 qui s'exécute comme au vrai démarrage.
/// </remarks>
[Collection(BaseFixture.Collection)]
public sealed class DocumentationOpenApiTests(BaseFixture baseDeDonnees)
{
    [Fact]
    public async Task En_PRODUCTION_ni_le_document_ni_l_interface_ne_sont_routes()
    {
        await using var application = Composer("Production");

        var routes = Routes(application);

        // Le témoin : la table n'est pas vide, les routes du produit y sont.
        Assert.Contains("/api/v1/auth/connexion", routes);
        Assert.DoesNotContain(routes, r => r.StartsWith("/openapi", StringComparison.Ordinal));
        Assert.DoesNotContain(routes, r => r.StartsWith("/scalar", StringComparison.Ordinal));
    }

    [Fact]
    public async Task En_Development_le_document_et_l_interface_sont_routes()
    {
        await using var application = Composer("Development");

        var routes = Routes(application);

        Assert.Contains(routes, r => r.StartsWith("/openapi", StringComparison.Ordinal));
        Assert.Contains(routes, r => r.StartsWith("/scalar", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Le_document_declare_Bearer_et_ne_l_exige_que_sur_les_routes_PROTEGEES()
    {
        var constructeur = Constructeur("Development", baseDeDonnees.ChaineApp, baseDeDonnees.ChaineAuth);
        constructeur.WebHost.UseUrls("http://127.0.0.1:0");
        Composition.Composer(constructeur);
        await using var application = constructeur.Build();
        Composition.Router(application);
        await application.StartAsync();

        try
        {
            using var client = new HttpClient { BaseAddress = new Uri(application.Urls.First()) };
            using var document = JsonDocument.Parse(
                await client.GetStringAsync(new Uri("/openapi/v1.json", UriKind.Relative))
            );
            var racine = document.RootElement;

            Assert.Equal("Palier API", racine.GetProperty("info").GetProperty("title").GetString());

            var schema = racine.GetProperty("components").GetProperty("securitySchemes").GetProperty("Bearer");
            Assert.Equal("http", schema.GetProperty("type").GetString());
            Assert.Equal("bearer", schema.GetProperty("scheme").GetString());

            var operations = racine
                .GetProperty("paths")
                .EnumerateObject()
                .SelectMany(chemin =>
                    chemin.Value.EnumerateObject().Select(o => (Chemin: chemin.Name, Operation: o.Value))
                )
                .ToList();

            bool Exige((string Chemin, JsonElement Operation) o) =>
                o.Operation.TryGetProperty("security", out _);

            // Les routes d'entraînement sont toutes protégées, par leur groupe.
            Assert.All(
                operations.Where(o => o.Chemin.StartsWith("/api/v1/seances", StringComparison.Ordinal)),
                o => Assert.True(Exige(o), $"{o.Chemin} devrait exiger le jeton.")
            );

            // Les routes anonymes n'affichent pas un cadenas qu'elles n'ont pas.
            foreach (var anonyme in new[] { "/api/v1/auth/inscription", "/api/v1/auth/connexion" })
            {
                Assert.False(
                    Exige(operations.Single(o => o.Chemin == anonyme)),
                    $"{anonyme} est anonyme : aucune exigence de jeton ne doit y figurer."
                );
            }

            using var interfaceScalar = await client.GetAsync(new Uri("/scalar", UriKind.Relative));
            Assert.True(interfaceScalar.IsSuccessStatusCode);
        }
        finally
        {
            await application.StopAsync();
        }
    }

    private static WebApplication Composer(string environnement)
    {
        var constructeur = Constructeur(environnement, "Host=127.0.0.1", "Host=127.0.0.1");
        Composition.Composer(constructeur);
        var application = constructeur.Build();
        Composition.Router(application);
        return application;
    }

    private static WebApplicationBuilder Constructeur(string environnement, string chaineApp, string chaineAuth)
    {
        var constructeur = WebApplication.CreateBuilder(
            new WebApplicationOptions { EnvironmentName = environnement }
        );
        constructeur.Configuration.Sources.Clear();

        var valeurs = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["ConnectionStrings:Palier"] = chaineApp,
            ["ConnectionStrings:PalierAuth"] = chaineAuth,
            ["JWT_SIGNING_KEY"] = HarnaisHttp.Cle,
        };
        HarnaisHttp.PoserLeCourrier(valeurs);
        constructeur.Configuration.AddInMemoryCollection(valeurs);
        return constructeur;
    }

    private static List<string> Routes(WebApplication application) =>
        ((IEndpointRouteBuilder)application)
            .DataSources.SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(route => "/" + (route.RoutePattern.RawText ?? string.Empty).TrimStart('/'))
            .ToList();
}
