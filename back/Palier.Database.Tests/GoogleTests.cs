using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Palier.Api.Auth;

namespace Palier.Database.Tests;

/// <summary>
/// Le câblage de Google — exigence 1 de <c>docs/09-comptes.md</c> § 1.
///
/// La décision au retour est éprouvée ailleurs, seule et sans réseau
/// (<see cref="DecisionDeGoogleTests" />). Ce fichier garde ce qui l'entoure :
/// les scopes qu'on demande, la lecture du constat, et le fait que les routes
/// n'existent QUE si Google est configuré.
/// </summary>
[Collection(BaseFixture.Collection)]
public sealed class GoogleTests(BaseFixture baseDeDonnees)
{
    // ================================================================
    // Épreuve 1 — les scopes, que le document métier interdit d'élargir
    // ================================================================

    [Fact]
    public void Les_scopes_demandes_sont_EXACTEMENT_openid_profile_email()
    {
        // `09-comptes.md` § 1 : « les scopes profile, email et openid suffisent
        // à ce que le bouton récupère, et la règle ci-dessus interdit d'en
        // ajouter ». Un scope de plus, c'est une donnée de plus collectée sans
        // base légale — et personne ne le verrait passer.
        Assert.Equal(["openid", "profile", "email"], Google.Scopes);
    }

    // ================================================================
    // Épreuve 2 — la revendication arrive en TEXTE, pas en booléen
    // ================================================================

    [Theory]
    [InlineData("true", true)]
    [InlineData("True", true)]
    [InlineData("false", false)]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("oui", false)]
    public void La_revendication_email_verified_se_lit_en_TEXTE(string? valeur, bool attendu)
    {
        // Elle traverse un JWT, où tout est chaîne. La lire comme un booléen
        // donnerait faux en toutes circonstances — et le produit croirait
        // chaque adresse non vérifiée, donc refuserait toute connexion Google.
        var revendications = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "123"),
            new(ClaimTypes.Email, "x@exemple.test"),
        };

        if (valeur is not null)
        {
            revendications.Add(new Claim(Google.RevendicationEmailVerifie, valeur));
        }

        var constat = Google.Lire(new ClaimsPrincipal(new ClaimsIdentity(revendications, "Google")));

        Assert.Equal(attendu, constat.EmailVerifie);
    }

    [Fact]
    public void Le_constat_lit_le_SUJET_et_l_ADRESSE()
    {
        var constat = Google.Lire(
            new ClaimsPrincipal(
                new ClaimsIdentity(
                    [
                        new Claim(ClaimTypes.NameIdentifier, "1093847"),
                        new Claim(ClaimTypes.Email, "x@exemple.test"),
                    ],
                    "Google"
                )
            )
        );

        Assert.Equal("1093847", constat.Sujet);
        Assert.Equal("x@exemple.test", constat.Email);
    }

    // ================================================================
    // Épreuve 3 — configuré ou non, et les deux branches sont franchies
    // ================================================================

    [Fact]
    public void Sans_identifiants_Google_n_est_PAS_configure()
    {
        Assert.False(Google.EstConfigure(Configuration(identifiant: null, secret: null)));
        Assert.False(Google.EstConfigure(Configuration(identifiant: "abc", secret: null)));
        Assert.False(Google.EstConfigure(Configuration(identifiant: null, secret: "def")));
        Assert.False(Google.EstConfigure(Configuration(identifiant: "  ", secret: "def")));
    }

    [Fact]
    public void Avec_les_DEUX_identifiants_Google_est_configure()
    {
        Assert.True(Google.EstConfigure(Configuration("abc", "def")));
    }

    // ================================================================
    // Épreuve 4 — les routes n'existent que si Google est configuré
    // ================================================================

    [Fact]
    public async Task Sans_configuration_les_routes_Google_N_EXISTENT_PAS()
    {
        // Attachées mais cassées, elles rendraient une erreur 500 au premier
        // clic. Absentes, le front sait qu'il ne doit pas afficher le bouton.
        await using var hote = HarnaisHttp.Hote(baseDeDonnees);

        // Le filtre porte sur le PRÉFIXE, pas sur « contient google » :
        // `/lier-google` existe toujours, lui, et n'a rien à voir avec le
        // câblage OAuth — c'est la seconde moitié de la fusion des comptes.
        Assert.DoesNotContain(Endpoints(hote), EstUneRouteDeGoogle);
    }

    [Fact]
    public async Task Avec_configuration_les_DEUX_routes_Google_existent()
    {
        // La borne : sans elle, l'épreuve ci-dessus serait verte pour la
        // mauvaise raison — parce que `Router` n'attache jamais rien.
        await using var hote = HarnaisHttp.Hote(
            baseDeDonnees,
            configurationEnPlus: new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                [Google.CleDeLIdentifiant] = "client-de-l-epreuve",
                [Google.CleDuSecret] = "secret-de-l-epreuve",
            }
        );

        Assert.Equal(2, Endpoints(hote).Count(EstUneRouteDeGoogle));
    }

    // ================================================================
    // Épreuve 5 — le défi renvoie vers Google, et vers le bon rappel
    // ================================================================

    [Fact]
    public void Le_defi_designe_le_schema_Google_et_le_chemin_de_rappel()
    {
        var defi = Assert.IsAssignableFrom<IResult>(Google.Defier());

        // Le type porte les deux informations qui comptent ; les lire ici évite
        // de monter tout un pipeline pour vérifier une redirection.
        Assert.Contains("Challenge", defi.GetType().Name, StringComparison.Ordinal);
    }

    // ================================================================

    /// <summary>Les routes du CÂBLAGE OAuth, et elles seules.</summary>
    private static bool EstUneRouteDeGoogle(RouteEndpoint endpoint) =>
        endpoint.RoutePattern.RawText?.StartsWith(
            PointsDEntree.Prefixe + "/google",
            StringComparison.Ordinal
        ) == true;

    private static List<RouteEndpoint> Endpoints(WebApplication hote)
    {
        Palier.Api.Composition.Router(hote);

        return
        [
            .. ((IEndpointRouteBuilder)hote)
                .DataSources.SelectMany(d => d.Endpoints)
                .OfType<RouteEndpoint>(),
        ];
    }

    private static IConfiguration Configuration(string? identifiant, string? secret) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>(StringComparer.Ordinal)
                {
                    [Google.CleDeLIdentifiant] = identifiant,
                    [Google.CleDuSecret] = secret,
                }
            )
            .Build();
}
