using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.JsonWebTokens;
using Palier.Api.Auth;

namespace Palier.Database.Tests;

/// <summary>
/// D36, en une phrase : « l'identité ne se lit jamais dans la requête ». Les
/// épreuves qui comptent ici sont celles où la requête ESSAIE de dicter
/// l'identité et n'y arrive pas.
/// </summary>
public sealed class IdentiteDepuisJetonTests
{
    [Fact]
    public void Sans_requete_il_n_y_a_pas_d_identite()
    {
        // Hors requête — une tâche de fond, un service hébergé — l'accesseur
        // rend null. Le cas d'usage doit alors échouer bruyamment, pas hériter
        // de l'identité du dernier appelant.
        Assert.Null(Identite(null).Identifiant);
    }

    [Fact]
    public void Un_visiteur_anonyme_n_a_pas_d_identite()
    {
        var contexte = new DefaultHttpContext();

        Assert.Null(Identite(contexte).Identifiant);
    }

    [Fact]
    public void Un_EN_TETE_FORGE_ne_fabrique_aucune_identite()
    {
        // Le cœur de D36. Un attaquant contrôle entièrement ses en-têtes, sa
        // chaîne de requête et son corps. Si l'un des trois pouvait nommer
        // l'utilisateur, la ligne d'isolation du produit entier tomberait — et
        // ce serait invisible, puisque la requête réussirait.
        var victime = Guid.NewGuid();
        var contexte = new DefaultHttpContext();
        contexte.Request.Headers["X-User-Id"] = victime.ToString("D", CultureInfo.InvariantCulture);
        contexte.Request.Headers["Authorization"] = "Bearer " + victime.ToString("D", CultureInfo.InvariantCulture);
        contexte.Request.QueryString = new QueryString(
            "?utilisateur=" + victime.ToString("D", CultureInfo.InvariantCulture)
        );

        Assert.True(
            Identite(contexte).Identifiant is null,
            "une identité a été lue dans un en-tête ou la chaîne de requête. "
                + "N'importe qui peut alors se faire passer pour n'importe qui."
        );
    }

    [Fact]
    public void Un_principal_NON_AUTHENTIFIE_ne_compte_pas_MEME_avec_un_sub_valide()
    {
        // Un `ClaimsPrincipal` existe TOUJOURS, même anonyme, et rien n'empêche
        // un intergiciel d'y avoir déposé des revendications sans les avoir
        // vérifiées. C'est `IsAuthenticated` qui tranche — pas la présence de la
        // revendication.
        var contexte = new DefaultHttpContext
        {
            // Aucun `authenticationType` : l'identité est bâtie, pas prouvée.
            User = new ClaimsPrincipal(
                new ClaimsIdentity(
                    [
                        new Claim(
                            JwtRegisteredClaimNames.Sub,
                            Guid.NewGuid().ToString("D", CultureInfo.InvariantCulture)
                        ),
                    ]
                )
            ),
        };

        Assert.True(
            Identite(contexte).Identifiant is null,
            "un `sub` non authentifié a été accepté : la vérification de signature "
                + "ne conditionne plus l'identité."
        );
    }

    [Fact]
    public void Un_jeton_VERIFIE_donne_l_identifiant()
    {
        var utilisateur = Guid.NewGuid();

        Assert.Equal(utilisateur, Identite(Authentifie(utilisateur.ToString("D", CultureInfo.InvariantCulture))).Identifiant);
    }

    [Theory]
    [InlineData("pas-un-guid")]
    [InlineData("")]
    [InlineData("00000000-0000-0000-0000-00000000000")] // un signe de trop en moins
    public void Un_sujet_ILLISIBLE_rend_null_et_ne_leve_pas(string sujet)
    {
        // Rendre null fait refuser le cas d'usage. Lever ici produirait un 500
        // et, pire, une trace qui renseigne l'appelant sur le format attendu.
        Assert.Null(Identite(Authentifie(sujet)).Identifiant);
    }

    private static IdentiteDepuisJeton Identite(HttpContext? contexte) =>
        new(new HttpContextAccessor { HttpContext = contexte });

    /// <summary>Un contexte dont le principal a RÉELLEMENT été authentifié.</summary>
    private static DefaultHttpContext Authentifie(string sujet) =>
        new()
        {
            User = new ClaimsPrincipal(
                new ClaimsIdentity([new Claim(JwtRegisteredClaimNames.Sub, sujet)], "Epreuve")
            ),
        };
}
