using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Palier.Api.Auth;
using Palier.Infrastructure.Coffre;
using Palier.Infrastructure.Identite;

namespace Palier.Database.Tests;

/// <summary>
/// Le rappel de Google, de bout en bout — sur base réelle, sans réseau.
///
/// Le service d'authentification est remplacé : c'est lui qui, en exploitation,
/// aurait échangé le code contre un jeton chez Google. Ce qui se juge ici est
/// tout ce qui vient APRÈS — la décision appliquée, le compte créé ou non, le
/// cookie posé, et surtout ce qui ne part PAS dans l'URL.
/// </summary>
[Collection(BaseFixture.Collection)]
public sealed class RappelDeGoogleTests(BaseFixture baseDeDonnees)
{
    private const string _motDePasse = "brouette-hivernale-38-oscille";

    // ================================================================
    // Épreuve 1 — une adresse inconnue crée le compte, DÉJÀ vérifié
    // ================================================================

    [Fact]
    public async Task Une_adresse_INCONNUE_cree_le_compte_avec_EmailConfirmed()
    {
        var adresse = AdresseUnique();
        await using var hote = Hote(Principal("sujet-" + Guid.NewGuid(), adresse, verifie: true));

        var contexte = await RappelerAsync(hote);

        Assert.Equal(StatusCodes.Status302Found, contexte.Response.StatusCode);
        var compte = await TrouverAsync(hote.Services, adresse);
        Assert.NotNull(compte);

        // Google a vérifié l'adresse : demander une seconde vérification par
        // courriel n'apporterait rien qu'un obstacle.
        Assert.True(compte.EmailConfirmed);
    }

    // ================================================================
    // Épreuve 2 — celle qui protège : aucun jeton dans l'URL
    // ================================================================

    [Fact]
    public async Task La_redirection_finale_ne_porte_AUCUN_jeton()
    {
        // Un jeton dans une URL finit dans l'historique du navigateur, dans les
        // journaux du serveur, et dans l'en-tête `Referer` du premier lien
        // externe cliqué. Le cookie, lui, ne voyage pas.
        await using var hote = Hote(
            Principal("sujet-" + Guid.NewGuid(), AdresseUnique(), verifie: true)
        );

        var contexte = await RappelerAsync(hote);

        var destination = contexte.Response.Headers.Location.ToString();
        Assert.DoesNotContain("token", destination, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("jeton", destination, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("=ey", destination, StringComparison.Ordinal);

        // Et le cookie de rafraîchissement, lui, EST posé — c'est par là que le
        // front obtiendra son jeton d'accès.
        Assert.Contains(
            CookieDeRafraichissement.Nom,
            contexte.Response.Headers.SetCookie.ToString(),
            StringComparison.Ordinal
        );
    }

    // ================================================================
    // Épreuve 3 — une adresse NON vérifiée ne crée rien
    // ================================================================

    [Fact]
    public async Task Une_adresse_NON_VERIFIEE_par_Google_ne_cree_AUCUN_compte()
    {
        var adresse = AdresseUnique();
        await using var hote = Hote(Principal("sujet-" + Guid.NewGuid(), adresse, verifie: false));

        var contexte = await RappelerAsync(hote);

        Assert.Contains("echec=google", contexte.Response.Headers.Location.ToString(), StringComparison.Ordinal);
        Assert.Null(await TrouverAsync(hote.Services, adresse));
    }

    // ================================================================
    // Épreuve 4 — un compte email existant déclenche la LIAISON
    // ================================================================

    [Fact]
    public async Task Un_compte_email_EXISTANT_redirige_vers_la_liaison()
    {
        var adresse = AdresseUnique();
        await using var hote = Hote(Principal("sujet-" + Guid.NewGuid(), adresse, verifie: true));
        await CreerLeCompteAsync(hote.Services, adresse);

        var contexte = await RappelerAsync(hote);

        // Il n'est PAS connecté : la liaison se propose, elle ne se fait pas.
        Assert.Contains("lier-google", contexte.Response.Headers.Location.ToString(), StringComparison.Ordinal);

        // Le cookie posé est celui de la LIAISON, pas celui du
        // rafraîchissement : le sceau atteste qui se présente, il n'ouvre rien.
        var cookies = contexte.Response.Headers.SetCookie.ToString();
        Assert.Contains(LiaisonGoogle.NomDuCookie, cookies, StringComparison.Ordinal);
        Assert.DoesNotContain(CookieDeRafraichissement.Nom, cookies, StringComparison.Ordinal);
    }

    // ================================================================
    // Épreuve 5 — une authentification qui échoue ne crée rien
    // ================================================================

    [Fact]
    public async Task Une_authentification_REFUSEE_par_Google_ne_cree_rien()
    {
        await using var hote = Hote(principal: null);

        var contexte = await RappelerAsync(hote);

        Assert.Contains("echec=google", contexte.Response.Headers.Location.ToString(), StringComparison.Ordinal);
        Assert.Empty(contexte.Response.Headers.SetCookie.ToString());
    }

    // ================================================================
    // Épreuve 6 — une connexion déjà liée ouvre la session
    // ================================================================

    [Fact]
    public async Task Une_connexion_Google_EXISTANTE_ouvre_la_session()
    {
        var adresse = AdresseUnique();
        var sujet = "sujet-" + Guid.NewGuid();
        await using var hote = Hote(Principal(sujet, adresse, verifie: true));
        var compte = await CreerLeCompteAsync(hote.Services, adresse);
        await LierAsync(hote.Services, compte, sujet);

        var contexte = await RappelerAsync(hote);

        Assert.Equal("/", contexte.Response.Headers.Location.ToString());
        Assert.NotEmpty(contexte.Response.Headers.SetCookie.ToString());
    }

    // ================================================================

    private WebApplication Hote(ClaimsPrincipal? principal) =>
        HarnaisHttp.Hote(
            baseDeDonnees,
            services =>
                services.AddSingleton<IAuthenticationService>(
                    new AuthentificationFactice(principal)
                )
        );

    private static async Task<DefaultHttpContext> RappelerAsync(WebApplication hote)
    {
        using var portee = hote.Services.GetRequiredService<IServiceScopeFactory>().CreateScope();
        var contexte = HarnaisHttp.Contexte(portee.ServiceProvider);

        var resultat = await Google.RappelerAsync(
            contexte,
            portee.ServiceProvider.GetRequiredService<UserManager<Utilisateur>>(),
            portee.ServiceProvider.GetRequiredService<MagasinDeSessions>(),
            hote.Services.GetRequiredService<PorteurDeTrousseau>(),
            hote.Services.GetRequiredService<TimeProvider>(),
            CancellationToken.None
        );

        await resultat.ExecuteAsync(contexte);
        return contexte;
    }

    private static ClaimsPrincipal Principal(string sujet, string adresse, bool verifie) =>
        new(
            new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, sujet),
                    new Claim(ClaimTypes.Email, adresse),
                    new Claim(Google.RevendicationEmailVerifie, verifie ? "true" : "false"),
                ],
                Google.Schema
            )
        );

    private static async Task<Utilisateur> CreerLeCompteAsync(
        IServiceProvider services,
        string adresse
    )
    {
        using var portee = services.GetRequiredService<IServiceScopeFactory>().CreateScope();
        var utilisateurs = portee.ServiceProvider.GetRequiredService<UserManager<Utilisateur>>();

        var compte = new Utilisateur
        {
            UserName = adresse,
            Email = adresse,
            EmailConfirmed = true,
        };

        var resultat = await utilisateurs.CreateAsync(compte, _motDePasse);
        Assert.True(resultat.Succeeded, string.Join(", ", resultat.Errors.Select(e => e.Code)));

        return compte;
    }

    private static async Task LierAsync(
        IServiceProvider services,
        Utilisateur compte,
        string sujet
    )
    {
        using var portee = services.GetRequiredService<IServiceScopeFactory>().CreateScope();
        var utilisateurs = portee.ServiceProvider.GetRequiredService<UserManager<Utilisateur>>();
        var suivi = await utilisateurs.FindByIdAsync(
            compte.Id.ToString("D", CultureInfo.InvariantCulture)
        );

        var resultat = await utilisateurs.AddLoginAsync(
            suivi!,
            new UserLoginInfo(Google.Schema, sujet, Google.Schema)
        );

        Assert.True(resultat.Succeeded, string.Join(", ", resultat.Errors.Select(e => e.Code)));
    }

    private static async Task<Utilisateur?> TrouverAsync(
        IServiceProvider services,
        string adresse
    )
    {
        using var portee = services.GetRequiredService<IServiceScopeFactory>().CreateScope();
        return await portee
            .ServiceProvider.GetRequiredService<UserManager<Utilisateur>>()
            .FindByEmailAsync(adresse);
    }

    private static string AdresseUnique() =>
        string.Create(CultureInfo.InvariantCulture, $"g-{Guid.NewGuid():N}@exemple.test");
}

/// <summary>
/// Il rend le principal qu'on lui donne, ou un échec. En exploitation, c'est ce
/// service qui aurait échangé le code d'autorisation contre un jeton chez
/// Google — un aller-retour réseau que rien ici ne cherche à éprouver.
/// </summary>
internal sealed class AuthentificationFactice(ClaimsPrincipal? principal) : IAuthenticationService
{
    public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme) =>
        Task.FromResult(
            principal is null
                ? AuthenticateResult.Fail("refusé par l'épreuve")
                : AuthenticateResult.Success(
                    new AuthenticationTicket(principal, scheme ?? Google.Schema)
                )
        );

    public Task ChallengeAsync(
        HttpContext context,
        string? scheme,
        AuthenticationProperties? properties
    ) => Task.CompletedTask;

    public Task ForbidAsync(
        HttpContext context,
        string? scheme,
        AuthenticationProperties? properties
    ) => Task.CompletedTask;

    public Task SignInAsync(
        HttpContext context,
        string? scheme,
        ClaimsPrincipal principal,
        AuthenticationProperties? properties
    ) => Task.CompletedTask;

    public Task SignOutAsync(
        HttpContext context,
        string? scheme,
        AuthenticationProperties? properties
    ) => Task.CompletedTask;
}
