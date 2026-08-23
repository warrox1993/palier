using Microsoft.AspNetCore.Identity;
using Palier.Infrastructure.Courrier;
using Palier.Infrastructure.Identite;

namespace Palier.Api.Auth;

/// <summary>
/// Le pont entre le contrat d'Identity et l'envoyeur — D60.
/// </summary>
/// <remarks>
/// <para>
/// <c>IEmailSender&lt;TUser&gt;</c> vit dans l'assembly ASP.NET Core, que
/// <c>Palier.Infrastructure</c> ne référence pas et n'a aucune raison de
/// référencer : composer un message ne demande pas de connaître le framework
/// web. Ce type fait la délégation, et il ne fait que cela.
/// </para>
///
/// <para>
/// Il remplace <c>NoOpEmailSender</c>, le défaut d'Identity, qui « ne fait
/// rien » et existe pour qu'on remarque qu'on ne l'a pas remplacé. Tant qu'il
/// était en place, l'exigence 2 de <c>docs/09-comptes.md</c> § 1 — « pas de
/// nutrition sans email vérifié » — avait sa règle et pas son moyen : aucun
/// compte ne pouvait vérifier son adresse.
/// </para>
/// </remarks>
internal sealed class EnvoiDeCourriel(EnvoyeurSmtp envoyeur) : IEmailSender<Utilisateur>
{
    public Task SendConfirmationLinkAsync(
        Utilisateur user,
        string email,
        string confirmationLink
    ) => envoyeur.EnvoyerAsync(Courriel.Verification, email, confirmationLink);

    public Task SendPasswordResetLinkAsync(Utilisateur user, string email, string resetLink) =>
        envoyeur.EnvoyerAsync(Courriel.Reinitialisation, email, resetLink);

    /// <summary>
    /// Identity distingue le lien et le code ; le produit n'envoie qu'un lien.
    /// Le code seul obligerait l'utilisateur à le recopier, et rien dans
    /// <c>docs/14-contenu.md</c> § 4 ne le demande.
    /// </summary>
    public Task SendPasswordResetCodeAsync(Utilisateur user, string email, string resetCode) =>
        envoyeur.EnvoyerAsync(Courriel.Reinitialisation, email, resetCode);
}
