using Microsoft.AspNetCore.Authorization;
using System.Net;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Palier.Api.Auth;

namespace Palier.Database.Tests;

/// <summary>
/// La limitation, et le piège du proxy.
/// </summary>
/// <remarks>
/// <para>
/// Ces épreuves n'ont besoin d'aucune base : elles portent sur l'adresse qu'on
/// compte, et sur le fait qu'un client ne puisse pas la choisir.
/// </para>
///
/// <para>
/// L'intergiciel des en-têtes transférés est invoqué <b>directement</b>, avec
/// les options réelles. Une épreuve qui relirait <c>ForwardedHeadersOptions</c>
/// vérifierait qu'un réglage a été <i>accepté</i>, pas qu'il est
/// <i>appliqué</i> — et ce dépôt a déjà payé cette différence.
/// </para>
/// </remarks>
public sealed class LimitationTests
{
    private const string _proxy = "198.51.100.1";
    private const string _client = "203.0.113.9";

    // ================================================================
    // Le piège du proxy
    // ================================================================

    [Fact]
    public async Task Sans_proxy_declare_un_X_Forwarded_For_est_IGNORE()
    {
        // Le défaut est le REFUS. Aucun proxy déclaré, aucun en-tête cru.
        var contexte = Contexte(reelle: "192.0.2.44", declare: _client);

        await TransfererAsync(contexte, proxysDeConfiance: null);

        Assert.Equal("192.0.2.44", Limitation.Cle(contexte));
    }

    [Fact]
    public async Task Un_X_Forwarded_For_CHANGE_a_chaque_essai_ne_donne_pas_de_seau_neuf()
    {
        // L'attaque exacte : six tentatives depuis la MÊME machine, en changeant
        // l'en-tête à chaque fois. Si l'en-tête était cru, chaque essai
        // tomberait dans un seau vierge et la limitation ne vaudrait rien.
        var cles = new HashSet<string>(StringComparer.Ordinal);

        for (var i = 0; i < 6; i++)
        {
            var contexte = Contexte(
                reelle: "192.0.2.44",
                declare: string.Create(System.Globalization.CultureInfo.InvariantCulture, $"203.0.113.{i}")
            );
            await TransfererAsync(contexte, proxysDeConfiance: null);
            cles.Add(Limitation.Cle(contexte));
        }

        Assert.True(
            cles.Count == 1,
            $"{cles.Count} seaux distincts pour six requêtes de la même machine : "
                + "l'en-tête déclaré a été cru, et la limitation se contourne en une ligne."
        );
    }

    [Fact]
    public async Task Un_proxy_DECLARE_fait_croire_l_en_tete()
    {
        // Le mordant inverse, sans lequel l'épreuve précédente serait satisfaite
        // par un intergiciel qui n'écoute jamais rien — et derrière le proxy de
        // l'hébergement, TOUT LE MONDE tomberait dans le même seau.
        var contexte = Contexte(reelle: _proxy, declare: _client);

        await TransfererAsync(contexte, proxysDeConfiance: _proxy);

        Assert.Equal(_client, Limitation.Cle(contexte));
    }

    [Fact]
    public async Task La_valeur_de_GAUCHE_est_ignoree()
    {
        // Le proxy ajoute l'adresse de son pair à DROITE ; tout ce qui est à
        // gauche vient du client et ne vaut rien. On ne retient que ce que
        // notre propre proxy a écrit.
        var contexte = Contexte(reelle: _proxy, declare: $"192.0.2.99, {_client}");

        await TransfererAsync(contexte, proxysDeConfiance: _proxy);

        Assert.Equal(_client, Limitation.Cle(contexte));
    }

    [Fact]
    public async Task ForwardLimit_a_UN_arrete_la_remontee_des_le_premier_cran()
    {
        // Cette épreuve porte sur `ForwardLimit`, et sur lui seul.
        //
        // Avec UN proxy déclaré, `ForwardLimit` ne décide de rien : la remontée
        // s'arrête d'elle-même dès que l'adresse atteinte n'est plus un proxy
        // connu. Mesuré le 21/08/2026 — le porter à `null` laissait
        // l'épreuve précédente VERTE, et elle ne prouvait donc pas ce que son
        // nom annonçait.
        //
        // Il ne décide que lorsque PLUSIEURS proxys sont déclarés. Ici, un
        // client qui glisse l'adresse d'un second proxy connu dans son propre
        // en-tête ferait remonter d'un cran de plus — et choisirait l'adresse
        // qu'on compte.
        var second = "198.51.100.2";
        var contexte = Contexte(reelle: _proxy, declare: $"192.0.2.99, {second}");

        await TransfererAsync(contexte, proxysDeConfiance: $"{_proxy},{second}");

        Assert.Equal(second, Limitation.Cle(contexte));
        Assert.NotEqual(
            "192.0.2.99",
            Limitation.Cle(contexte)
        );
    }

    [Fact]
    public void La_BOUCLE_LOCALE_n_est_pas_de_confiance()
    {
        // ASP.NET Core met `::1` dans KnownProxies et `::1/128` dans
        // KnownIPNetworks par défaut. Les laisser ferait croire tout en-tête
        // venu de la machine elle-même : un conteneur voisin, un service
        // co-localisé, un tunnel suffiraient à forger n'importe quelle adresse.
        var options = Limitation.EnTetesTransferes(Configuration(null));

        Assert.Empty(options.KnownProxies);
        Assert.Empty(options.KnownIPNetworks);
    }

    [Fact]
    public void Une_valeur_ILLISIBLE_n_ouvre_aucune_confiance()
    {
        var options = Limitation.EnTetesTransferes(Configuration("pas-une-adresse, , 198.51.100.1"));

        Assert.Single(options.KnownProxies);
        Assert.Equal(IPAddress.Parse(_proxy), options.KnownProxies[0]);
    }

    // ================================================================
    // Le compteur
    // ================================================================

    [Fact]
    public async Task La_SIXIEME_tentative_depuis_la_meme_adresse_est_refusee()
    {
        using var limiteur = PartitionedRateLimiter.Create<HttpContext, string>(
            Limitation.Partition
        );

        for (var i = 0; i < Limitation.TentativesParFenetre; i++)
        {
            using var permis = await limiteur.AcquireAsync(Contexte(reelle: "192.0.2.44"), 1);
            Assert.True(permis.IsAcquired, $"la tentative {i + 1} a été refusée avant la limite");
        }

        using var refuse = await limiteur.AcquireAsync(Contexte(reelle: "192.0.2.44"), 1);

        Assert.False(
            refuse.IsAcquired,
            $"la tentative {Limitation.TentativesParFenetre + 1} est passée : "
                + "le seau ne se referme jamais."
        );
    }

    [Fact]
    public async Task Deux_adresses_DIFFERENTES_ont_des_seaux_distincts()
    {
        // Sans cette épreuve, un limiteur qui compterait TOUT dans un seul seau
        // satisferait la précédente — et cinq échecs de n'importe qui
        // bloqueraient l'ensemble des utilisateurs.
        using var limiteur = PartitionedRateLimiter.Create<HttpContext, string>(
            Limitation.Partition
        );

        for (var i = 0; i < Limitation.TentativesParFenetre; i++)
        {
            using var permis = await limiteur.AcquireAsync(Contexte(reelle: "192.0.2.44"), 1);
            Assert.True(permis.IsAcquired);
        }

        using var voisin = await limiteur.AcquireAsync(Contexte(reelle: "192.0.2.45"), 1);

        Assert.True(
            voisin.IsAcquired,
            "une adresse voisine a été refusée : tout le monde partage le même seau."
        );
    }

    [Fact]
    public async Task Une_connexion_SANS_ADRESSE_est_comptee_elle_aussi()
    {
        // Un tube nommé ou une socket Unix n'ont pas d'adresse. Leur donner un
        // passe-droit ouvrirait un chemin sans aucune limite.
        using var limiteur = PartitionedRateLimiter.Create<HttpContext, string>(
            Limitation.Partition
        );

        for (var i = 0; i < Limitation.TentativesParFenetre; i++)
        {
            using var permis = await limiteur.AcquireAsync(new DefaultHttpContext(), 1);
            Assert.True(permis.IsAcquired);
        }

        using var refuse = await limiteur.AcquireAsync(new DefaultHttpContext(), 1);

        Assert.False(refuse.IsAcquired, "une connexion sans adresse échappe à la limitation");
    }

    // ================================================================
    // Le réglage est-il APPLIQUÉ ?
    // ================================================================

    [Theory]
    [InlineData("/api/v1/auth/inscription")]
    [InlineData("/api/v1/auth/connexion")]
    [InlineData("/api/v1/auth/rafraichir")]
    public void Les_routes_SENSIBLES_portent_la_politique(string chemin)
    {
        // On interroge le système, on ne relit pas la ligne d'enregistrement.
        // Une route ajoutée demain sans `RequireRateLimiting` serait un trou
        // parfaitement invisible.
        Assert.Equal(Limitation.Politique, PolitiqueDe(chemin));
    }

    [Fact]
    public void La_route_de_SANTE_n_est_plus_ANONYME()
    {
        // D41 : « au lot 4 il passe derrière l'authentification et un rôle
        // d'administration ». Le lot 4 avait livré la route ouverte, et elle
        // rendait l'identifiant EXACT de la dernière migration appliquée —
        // rapproché de l'historique public de ce dépôt, il dit quelles
        // politiques RLS l'instance possède ou non.
        //
        // On interroge le système, pas la ligne d'enregistrement : une route
        // qui perdrait sa métadonnée d'autorisation serait un trou parfaitement
        // invisible.
        var route = RouteDe("/api/v1/sante");

        Assert.True(
            route.Metadata.GetMetadata<IAuthorizeData>() is not null,
            "la route de santé est de nouveau anonyme : elle publie l'identifiant de "
                + "migration à qui le demande."
        );
    }

    [Fact]
    public void La_DECONNEXION_n_est_pas_limitee()
    {
        // Délibéré, et donc éprouvé : un utilisateur qui a épuisé son seau doit
        // pouvoir couper ses sessions. C'est justement le geste qu'on fait
        // quand quelque chose ne va pas.
        Assert.Null(PolitiqueDe("/api/v1/auth/deconnexion"));
    }

    // ================================================================
    // Le harnais
    // ================================================================

    /// <summary>La route enregistrée sous ce chemin, ou l'épreuve échoue.</summary>
    private static RouteEndpoint RouteDe(string chemin)
    {
        var routes = Endpoints();
        var route = routes.Find(e => Chemin(e) == chemin);

        Assert.True(
            route is not null,
            $"la route {chemin} est introuvable : l'épreuve ne regarde rien. Vues : "
                + string.Join(" | ", routes.Select(Chemin))
        );

        return route;
    }

    private static string Chemin(RouteEndpoint e) => "/" + e.RoutePattern.RawText?.TrimStart('/');

    /// <summary>
    /// Les endpoints se lisent sur le <c>WebApplication</c> LUI-MÊME. Le
    /// <c>EndpointDataSource</c> du conteneur est vide tant que le pipeline n'a
    /// pas démarré — mesuré : « Vues : » ne listait rien, et l'épreuve n'aurait
    /// rien regardé si l'assertion ne l'avait pas dit.
    /// </summary>
    private static List<RouteEndpoint> Endpoints()
    {
        var constructeur = WebApplication.CreateBuilder();
        constructeur.Configuration.Sources.Clear();
        constructeur.Configuration.AddInMemoryCollection(
            new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["ConnectionStrings:Palier"] = "Host=127.0.0.1",
                ["ConnectionStrings:PalierAuth"] = "Host=127.0.0.1",
                ["JWT_SIGNING_KEY"] = HarnaisHttp.Cle,
                // D60 : la composition refuse sans le courrier.
                ["SMTP_HOST"] = "relais.invalid",
                ["SMTP_PORT"] = "587",
                ["SMTP_FROM"] = "palier@exemple.test",
                ["APP_URL"] = "https://palier.test",
            }
        );

        Palier.Api.Composition.Composer(constructeur);
        var application = constructeur.Build();
        Palier.Api.Composition.Router(application);

        return [
            .. ((IEndpointRouteBuilder)application)
                .DataSources.SelectMany(d => d.Endpoints)
                .OfType<RouteEndpoint>(),
        ];
    }

    private static string? PolitiqueDe(string chemin) =>
        RouteDe(chemin).Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName;

    /// <summary>Applique l'intergiciel RÉEL des en-têtes transférés.</summary>
    private static async Task TransfererAsync(HttpContext contexte, string? proxysDeConfiance)
    {
        var options = Limitation.EnTetesTransferes(Configuration(proxysDeConfiance));
        var intergiciel = new ForwardedHeadersMiddleware(
            _ => Task.CompletedTask,
            NullLoggerFactory.Instance,
            Options.Create(options)
        );

        await intergiciel.Invoke(contexte);
    }

    private static IConfiguration Configuration(string? proxys) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>(StringComparer.Ordinal)
                {
                    [Limitation.CleDesProxys] = proxys,
                }
            )
            .Build();

    private static DefaultHttpContext Contexte(string reelle, string? declare = null)
    {
        var contexte = new DefaultHttpContext();
        contexte.Connection.RemoteIpAddress = IPAddress.Parse(reelle);

        if (declare is not null)
        {
            contexte.Request.Headers["X-Forwarded-For"] = declare;
        }

        return contexte;
    }
}
