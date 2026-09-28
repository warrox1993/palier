using Microsoft.AspNetCore.Builder;
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
/// l'enregistre. Le contenu du document lui-même est éprouvé sur l'API lancée,
/// dans <see cref="DemarrageDeLApiTests" />.
/// </remarks>
public sealed class DocumentationOpenApiTests
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

    private static WebApplication Composer(string environnement)
    {
        var constructeur = WebApplication.CreateBuilder(
            new WebApplicationOptions { EnvironmentName = environnement }
        );
        constructeur.Configuration.Sources.Clear();

        var valeurs = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["ConnectionStrings:Palier"] = "Host=127.0.0.1",
            ["ConnectionStrings:PalierAuth"] = "Host=127.0.0.1",
            ["JWT_SIGNING_KEY"] = HarnaisHttp.Cle,
        };
        HarnaisHttp.PoserLeCourrier(valeurs);
        constructeur.Configuration.AddInMemoryCollection(valeurs);

        Composition.Composer(constructeur);
        var application = constructeur.Build();
        Composition.Router(application);
        return application;
    }

    private static List<string> Routes(WebApplication application) =>
        ((IEndpointRouteBuilder)application)
            .DataSources.SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(route => "/" + (route.RoutePattern.RawText ?? string.Empty).TrimStart('/'))
            .ToList();
}
