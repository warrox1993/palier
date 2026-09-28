using System.Globalization;
using Microsoft.AspNetCore.Identity;
using Palier.Application.Pipeline;
using Palier.Infrastructure.Courrier;
using Palier.Infrastructure.Identite;

namespace Palier.Api.Auth;

/// <summary>Le retour du lien de confirmation.</summary>
internal sealed record DemandeDeSuppression(string? Code);

/// <summary>
/// La suppression de compte — article 17, et ce que le lot 4 n'avait livré
/// qu'à moitié.
/// </summary>
/// <remarks>
/// <para>
/// <b>Aucun délai de grâce.</b> L'idée d'un compte « supprimé dans 30 jours »
/// est séduisante et se paie cher : un état de plus dans chaque requête, une
/// purge à écrire, et une donnée de santé qui survit à la demande de son
/// propriétaire. L'article 17 demande l'effacement, pas l'archivage.
/// </para>
///
/// <para>
/// <b>La confirmation passe par courriel</b>, ce qui protège du geste
/// accidentel sans rien conserver. Le code vient d'Identity — même fabrique que
/// la vérification d'adresse — et il est donc lié au compte et à son sceau de
/// sécurité : changer de mot de passe l'invalide.
/// </para>
///
/// <para>
/// <b>L'effacement est une CASCADE du moteur</b>, posée au lot 2 : séances,
/// séries, poids et sessions disparaissent avec la ligne d'`AspNetUsers`.
/// L'éprouver au travers de l'API ne prouverait rien — l'épreuve interroge donc
/// la base directement.
/// </para>
/// </remarks>
internal static class Suppression
{
    /// <summary>Le chemin du front qui reçoit le lien.</summary>
    public const string CheminDuFront = "/supprimer-mon-compte";

    public static void Router(RouteGroupBuilder groupe)
    {
        ArgumentNullException.ThrowIfNull(groupe);

        // Les DEUX exigent une session : on ne supprime pas un compte dont on
        // ne prouve pas qu'on y est connecté. L'adresse seule ouvrirait un
        // chemin de nuisance contre n'importe qui.
        groupe.MapPost("/demander-la-suppression", DemanderAsync).RequireAuthorization();
        groupe.MapPost("/confirmer-la-suppression", ConfirmerAsync).RequireAuthorization();
    }

    /// <summary>Envoie le courriel de confirmation au titulaire.</summary>
    public static async Task<IResult> DemanderAsync(
        IIdentiteDemandeur demandeur,
        UserManager<Utilisateur> utilisateurs,
        EnvoyeurSmtp envoyeur,
        ReglagesDuCourrier reglages,
        CancellationToken jeton
    )
    {
        ArgumentNullException.ThrowIfNull(demandeur);
        ArgumentNullException.ThrowIfNull(utilisateurs);
        ArgumentNullException.ThrowIfNull(envoyeur);
        ArgumentNullException.ThrowIfNull(reglages);

        if (await CompteAsync(demandeur, utilisateurs).ConfigureAwait(false) is not { } compte)
        {
            return Results.Unauthorized();
        }

        if (compte.Email is not { Length: > 0 } destinataire)
        {
            return Refus();
        }

        // Le jeton de changement d'adresse vers SA PROPRE adresse : Identity le
        // lie au compte, à son sceau de sécurité et à la valeur visée. Il n'en
        // existe pas de dédié à la suppression, et en fabriquer un demanderait
        // un fournisseur de jetons de plus pour la même garantie.
        var code = await utilisateurs
            .GenerateChangeEmailTokenAsync(compte, destinataire)
            .ConfigureAwait(false);

        var lien = string.Create(
            CultureInfo.InvariantCulture,
            $"{reglages.BaseDesLiens}{CheminDuFront}?code={Verification.Encoder(code)}"
        );

        await envoyeur
            .EnvoyerAsync(Courriel.Suppression, destinataire, lien, jeton: jeton)
            .ConfigureAwait(false);

        return Results.Accepted();
    }

    /// <summary>Efface le compte, et tout ce qui en dépend.</summary>
    public static async Task<IResult> ConfirmerAsync(
        DemandeDeSuppression corps,
        IIdentiteDemandeur demandeur,
        UserManager<Utilisateur> utilisateurs,
        CancellationToken jeton
    )
    {
        ArgumentNullException.ThrowIfNull(corps);
        ArgumentNullException.ThrowIfNull(demandeur);
        ArgumentNullException.ThrowIfNull(utilisateurs);
        jeton.ThrowIfCancellationRequested();

        if (await CompteAsync(demandeur, utilisateurs).ConfigureAwait(false) is not { } compte)
        {
            return Results.Unauthorized();
        }

        if (
            corps.Code is not { Length: > 0 } code
            || compte.Email is not { Length: > 0 } adresse
        )
        {
            return Refus();
        }

        // `VerifyUserTokenAsync` plutôt que `ChangeEmailAsync` : on VÉRIFIE le
        // jeton sans appliquer le changement d'adresse qu'il autoriserait.
        var valide = await utilisateurs
            .VerifyUserTokenAsync(
                compte,
                utilisateurs.Options.Tokens.ChangeEmailTokenProvider,
                UserManager<Utilisateur>.GetChangeEmailTokenPurpose(adresse),
                Verification.Decoder(code)
            )
            .ConfigureAwait(false);

        if (!valide)
        {
            return Refus();
        }

        var resultat = await utilisateurs.DeleteAsync(compte).ConfigureAwait(false);

        return resultat.Succeeded ? Results.NoContent() : Refus();
    }

    private static async Task<Utilisateur?> CompteAsync(
        IIdentiteDemandeur demandeur,
        UserManager<Utilisateur> utilisateurs
    ) =>
        demandeur.Identifiant is { } identifiant
            ? await utilisateurs
                .FindByIdAsync(identifiant.ToString("D", CultureInfo.InvariantCulture))
                .ConfigureAwait(false)
            : null;

    private static IResult Refus() =>
        Results.Json(new Reponse("SuppressionInvalide"), statusCode: StatusCodes.Status400BadRequest);
}
