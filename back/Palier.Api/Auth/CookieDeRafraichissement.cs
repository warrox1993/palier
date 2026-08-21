using Palier.Application.Sessions;

namespace Palier.Api.Auth;

/// <summary>
/// Le jeton de rafraîchissement voyage dans un cookie, et jamais dans le corps
/// d'une réponse.
/// </summary>
/// <remarks>
/// <para>
/// <b>Pourquoi un cookie plutôt qu'une valeur que le client range.</b> Un jeton
/// rendu dans le corps doit être stocké quelque part par la page :
/// <c>localStorage</c> et <c>sessionStorage</c> sont lisibles par tout script
/// qui s'exécute dans l'origine, et une seule dépendance compromise suffit à
/// les vider. <c>HttpOnly</c> met la valeur hors de portée de JavaScript, y
/// compris du nôtre.
/// </para>
///
/// <para>
/// <b>Les quatre attributs, et ce que chacun coupe :</b>
/// </para>
/// <list type="bullet">
///   <item><c>HttpOnly</c> — aucun script ne le lit, donc un XSS ne l'exfiltre pas.</item>
///   <item>
///     <c>Secure</c> — il ne part jamais en clair. <b>Sans condition
///     d'environnement</b> : un drapeau qui s'éteint en développement est
///     exactement ce que rien n'attrape, et le jour où la configuration de
///     développement atteint un serveur, la protection a déjà disparu.
///   </item>
///   <item>
///     <c>SameSite=Strict</c> — il ne part sur aucune requête venue d'un autre
///     site. D16 place le front et l'API sous le même domaine ; c'est
///     précisément ce que cette décision achète, et <c>Lax</c> serait un
///     relâchement sans contrepartie.
///   </item>
///   <item>
///     <c>Path</c> restreint — il ne part que sur la route de rafraîchissement.
///     Toutes les autres requêtes de l'application ne le voient pas passer :
///     un journal de serveur frontal trop bavard, un intermédiaire, une trace
///     de débogage ne peuvent pas le capter au passage.
///   </item>
/// </list>
/// </remarks>
internal static class CookieDeRafraichissement
{
    /// <summary>
    /// Le nom du cookie. Le préfixe <c>__Host-</c> est une garantie que le
    /// navigateur applique lui-même : il refuse le cookie s'il n'est pas
    /// <c>Secure</c>, s'il porte un <c>Domain</c>, ou si son chemin n'est
    /// pas <c>/</c>.
    /// </summary>
    /// <remarks>
    /// Ce préfixe est <b>volontairement écarté</b> : il exige <c>Path=/</c>,
    /// ce qui enverrait le jeton sur CHAQUE requête de l'application. Le chemin
    /// restreint protège davantage ici, puisque le front et l'API partagent le
    /// domaine (D16) et qu'aucun sous-domaine tiers ne peut donc poser de
    /// cookie sur cette origine.
    /// </remarks>
    public const string Nom = "palier_rafraichissement";

    /// <summary>Le seul chemin sur lequel le cookie est émis — et donc renvoyé.</summary>
    public const string Chemin = "/api/v1/auth/rafraichir";

    /// <summary>Pose le cookie, pour la durée de vie du rafraîchissement.</summary>
    public static void Poser(HttpResponse reponse, string valeur, DateTimeOffset maintenant)
    {
        ArgumentNullException.ThrowIfNull(reponse);

        reponse.Cookies.Append(Nom, valeur, Options(maintenant + ParametresDeSession.DureeDuRafraichissement));
    }

    /// <summary>
    /// Efface le cookie. Les attributs doivent être <b>identiques</b> à ceux de
    /// la pose : un navigateur n'efface pas un cookie dont le chemin diffère, et
    /// la déconnexion laisserait alors le jeton en place.
    /// </summary>
    public static void Effacer(HttpResponse reponse)
    {
        ArgumentNullException.ThrowIfNull(reponse);

        reponse.Cookies.Delete(
            Nom,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Path = Chemin,
            }
        );
    }

    /// <summary>Lit le jeton présenté, ou <c>null</c>.</summary>
    public static string? Lire(HttpRequest requete)
    {
        ArgumentNullException.ThrowIfNull(requete);

        return requete.Cookies.TryGetValue(Nom, out var valeur) && !string.IsNullOrWhiteSpace(valeur)
            ? valeur
            : null;
    }

    private static CookieOptions Options(DateTimeOffset expiration) =>
        new()
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = Chemin,
            Expires = expiration,
        };
}
