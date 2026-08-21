using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Palier.Application.Autorisations;
using Palier.Application.Pipeline;
using Palier.Infrastructure.Identite;

namespace Palier.Api.Auth;

/// <summary>Les deux domaines du produit, qui ne se ferment pas ensemble.</summary>
internal enum Domaine
{
    /// <summary>Séances, séries, charges. Ouvert dès qu'on est authentifié.</summary>
    Entrainement,

    /// <summary>Apports, références, calculs. Fermé à deux verrous.</summary>
    Nutrition,
}

/// <summary>L'exigence d'accès à un domaine.</summary>
internal sealed class ExigenceDeDomaine(Domaine domaine) : IAuthorizationRequirement
{
    public Domaine Domaine => domaine;
}

/// <summary>
/// Applique <see cref="PorteDesDomaines" /> : il lit les drapeaux, il appelle,
/// il tranche.
/// </summary>
/// <remarks>
/// <para>
/// <b>Les drapeaux sont lus EN BASE, à chaque requête, et jamais dans le
/// jeton.</b> Un JWT est signé, pas chiffré, et surtout : il vaut quinze
/// minutes. Un consentement retiré à l'instant doit fermer la nutrition à
/// l'instant, pas au prochain rafraîchissement — c'est la différence entre un
/// droit de retrait effectif et un droit de retrait annoncé.
/// </para>
/// </remarks>
internal sealed class GardienDesDomaines(
    IIdentiteDemandeur demandeur,
    UserManager<Utilisateur> utilisateurs
) : AuthorizationHandler<ExigenceDeDomaine>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext contexte,
        ExigenceDeDomaine exigence
    )
    {
        ArgumentNullException.ThrowIfNull(contexte);
        ArgumentNullException.ThrowIfNull(exigence);

        if (demandeur.Identifiant is not { } identifiant)
        {
            // Pas d'identité, pas d'accès — et surtout pas de `Fail()` : un
            // autre gestionnaire pourrait légitimement accorder l'accès par une
            // autre voie, et `Fail` est définitif. Ne rien faire suffit à
            // refuser.
            return;
        }

        var utilisateur = await utilisateurs
            .FindByIdAsync(identifiant.ToString("D", CultureInfo.InvariantCulture))
            .ConfigureAwait(false);

        if (utilisateur is null)
        {
            return;
        }

        var ouvert = exigence.Domaine switch
        {
            Domaine.Nutrition => PorteDesDomaines.NutritionOuverte(
                utilisateur.EmailConfirmed,
                utilisateur.ConsentementSanteLe is not null
            ),
            Domaine.Entrainement => PorteDesDomaines.EntrainementOuvert(
                utilisateur.EmailConfirmed,
                utilisateur.ConsentementSanteLe is not null
            ),
            _ => false,
        };

        if (ouvert)
        {
            contexte.Succeed(exigence);
        }
    }
}

/// <summary>Les deux politiques nommées, et leur enregistrement.</summary>
internal static class PolitiquesDAutorisation
{
    /// <summary>Séances, séries, charges.</summary>
    public const string Entrainement = "entrainement";

    /// <summary>Apports, références, calculs.</summary>
    public const string Nutrition = "nutrition";

    /// <summary>Enregistre les politiques et leur gestionnaire.</summary>
    public static void Composer(WebApplicationBuilder constructeur)
    {
        ArgumentNullException.ThrowIfNull(constructeur);

        constructeur.Services.AddAuthorization(options =>
        {
            options.AddPolicy(
                Entrainement,
                politique =>
                {
                    politique.RequireAuthenticatedUser();
                    politique.Requirements.Add(new ExigenceDeDomaine(Domaine.Entrainement));
                }
            );

            options.AddPolicy(
                Nutrition,
                politique =>
                {
                    politique.RequireAuthenticatedUser();
                    politique.Requirements.Add(new ExigenceDeDomaine(Domaine.Nutrition));
                }
            );
        });

        // Scoped, et non singleton : il prend `UserManager`, qui l'est.
        constructeur.Services.AddScoped<IAuthorizationHandler, GardienDesDomaines>();
    }
}
