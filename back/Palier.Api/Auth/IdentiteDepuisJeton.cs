using System.Globalization;
using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;
using Palier.Application.Pipeline;

namespace Palier.Api.Auth;

/// <summary>
/// L'identité du demandeur, lue dans le jeton <b>vérifié</b> et nulle part
/// ailleurs.
/// </summary>
/// <remarks>
/// <para>
/// C'est le fondement de D36 : « l'identité ne se lit jamais dans la requête —
/// ni dans le corps, ni dans un paramètre, ni dans un en-tête que le client
/// contrôle ». Elle vient de <c>HttpContext.User</c>, que l'intergiciel
/// d'authentification n'a peuplé qu'après avoir vérifié la signature, l'émetteur,
/// l'audience et la date d'expiration.
/// </para>
///
/// <para>
/// Ce type remplace <c>DemandeurSansIdentite</c>, le bouchon du lot 2, qui
/// rendait toujours <c>null</c> et faisait refuser tous les cas d'usage.
/// </para>
/// </remarks>
internal sealed class IdentiteDepuisJeton(IHttpContextAccessor accesseur) : IIdentiteDemandeur
{
    public Guid? Identifiant
    {
        get
        {
            var utilisateur = accesseur.HttpContext?.User;

            // Pas d'authentification réussie : pas d'identité. Un ClaimsPrincipal
            // existe toujours, même anonyme — c'est Identity qui décide, pas la
            // présence de l'objet.
            if (utilisateur?.Identity?.IsAuthenticated != true)
            {
                return null;
            }

            var sujet =
                utilisateur.FindFirstValue(JwtRegisteredClaimNames.Sub)
                ?? utilisateur.FindFirstValue(ClaimTypes.NameIdentifier);

            return Guid.TryParse(sujet, CultureInfo.InvariantCulture, out var identifiant)
                ? identifiant
                : null;
        }
    }
}
