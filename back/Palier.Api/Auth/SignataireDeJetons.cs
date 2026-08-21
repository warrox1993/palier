using Palier.Application.Sessions;

namespace Palier.Api.Auth;

/// <summary>
/// Le seul objet qui détient la clé de signature.
/// </summary>
/// <remarks>
/// <para>
/// Sans lui, chaque point d'entrée relirait <c>JWT_SIGNING_KEY</c> dans la
/// configuration : le secret circulerait dans autant d'endroits qu'il y a de
/// routes, et rien n'empêcherait l'un d'eux de le journaliser ou de le renvoyer
/// dans une réponse d'erreur. Ici il entre une fois, au démarrage, et ne
/// ressort jamais — aucune propriété ne l'expose.
/// </para>
///
/// <para>
/// L'horloge est injectée plutôt que lue : <c>DateTimeOffset.UtcNow</c> écrit en
/// dur rendrait toute épreuve d'expiration dépendante de l'heure de la machine.
/// </para>
/// </remarks>
internal sealed class SignataireDeJetons(string cle, TimeProvider horloge)
{
    /// <summary>La durée de vie annoncée, en secondes, pour le client.</summary>
    public static int DureeEnSecondes =>
        (int)ParametresDeSession.DureeDuJetonDAcces.TotalSeconds;

    /// <summary>Émet un jeton d'accès pour un utilisateur.</summary>
    public string Emettre(Guid utilisateur) =>
        JetonDAcces.Emettre(utilisateur, cle, horloge.GetUtcNow());
}
