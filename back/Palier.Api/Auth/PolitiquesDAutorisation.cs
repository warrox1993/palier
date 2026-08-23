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

/// <summary>L'exigence d'être administrateur — D41.</summary>
internal sealed class ExigenceDAdministration : IAuthorizationRequirement;

/// <summary>
/// Les adresses des administrateurs, lues UNE FOIS au démarrage depuis
/// <c>ADMIN_EMAILS</c> — donc depuis le coffre (D59).
/// </summary>
/// <remarks>
/// <para>
/// <b>Une liste vide n'ouvre à personne.</b> C'est le piège classique de la
/// liste blanche, et ce dépôt l'a déjà payé une fois : vider
/// <c>TRUSTED_PROXIES</c> rendait la confiance TOTALE au lieu de la fermer,
/// parce que le cadre n'inspectait plus rien. Ici, l'ensemble vide fait
/// simplement échouer <c>Contains</c>, et c'est ce qu'on veut.
/// </para>
///
/// <para>
/// La comparaison est <b>insensible à la casse</b> : Identity normalise les
/// adresses, et comparer brut ferait perdre son rôle à un administrateur qui
/// s'inscrirait « Gardien@… » quand la liste dit « gardien@… ».
/// </para>
/// </remarks>
internal sealed class ListeDesAdministrateurs
{
    private readonly HashSet<string> _adresses;

    public ListeDesAdministrateurs(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        _adresses = (configuration["ADMIN_EMAILS"] ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public bool Porte(string? adresse) => adresse is not null && _adresses.Contains(adresse);
}

/// <summary>
/// Applique la liste. Il charge le compte en base plutôt que de lire une
/// revendication : le jeton vaut quinze minutes, et un administrateur retiré de
/// la liste ne doit pas garder son pouvoir jusqu'au prochain rafraîchissement —
/// c'est le raisonnement de <see cref="GardienDesDomaines" />, appliqué au rôle.
/// </summary>
internal sealed class GardienDAdministration(
    IIdentiteDemandeur demandeur,
    UserManager<Utilisateur> utilisateurs,
    ListeDesAdministrateurs liste
) : AuthorizationHandler<ExigenceDAdministration>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext contexte,
        ExigenceDAdministration exigence
    )
    {
        ArgumentNullException.ThrowIfNull(contexte);

        if (demandeur.Identifiant is not { } identifiant)
        {
            return;
        }

        var compte = await utilisateurs
            .FindByIdAsync(identifiant.ToString("D", CultureInfo.InvariantCulture))
            .ConfigureAwait(false);

        // `EmailConfirmed` n'est PAS une précaution de plus : l'inscription est
        // ouverte, et sans elle quiconque s'inscrit avec l'adresse d'un
        // administrateur en devient un, sans jamais prouver qu'il la possède.
        if (compte is { EmailConfirmed: true } && liste.Porte(compte.Email))
        {
            contexte.Succeed(exigence);
        }
    }
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

    /// <summary>Le rôle que D41 réclamait depuis le lot 4.</summary>
    public const string Administration = "administration";

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

            options.AddPolicy(
                Administration,
                politique =>
                {
                    politique.RequireAuthenticatedUser();
                    politique.Requirements.Add(new ExigenceDAdministration());
                }
            );
        });

        // Singleton : la liste est lue une fois, au démarrage, depuis le
        // coffre. La relire à chaque requête ferait un aller-retour pour une
        // valeur qui ne change qu'au redéploiement.
        constructeur.Services.AddSingleton<ListeDesAdministrateurs>();

        // Scoped, et non singleton : il prend `UserManager`, qui l'est.
        constructeur.Services.AddScoped<IAuthorizationHandler, GardienDesDomaines>();
        constructeur.Services.AddScoped<IAuthorizationHandler, GardienDAdministration>();
    }
}
