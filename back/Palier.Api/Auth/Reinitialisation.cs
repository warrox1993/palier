using System.Globalization;
using Microsoft.AspNetCore.Identity;
using Palier.Infrastructure.Courrier;
using Palier.Infrastructure.Identite;

namespace Palier.Api.Auth;

/// <summary>Une demande de réinitialisation, par adresse.</summary>
internal sealed record DemandeDOubli(string? Email);

/// <summary>Le retour du lien : le compte, le code, et le nouveau secret.</summary>
internal sealed record DemandeDeNouveauMotDePasse(
    string? Compte,
    string? Code,
    string? NouveauMotDePasse
);

/// <summary>
/// La réinitialisation de mot de passe — le pendant de
/// <see cref="Verification" />, et les mêmes règles.
/// </summary>
/// <remarks>
/// <para>
/// <b>Elle révoque toutes les sessions.</b> Une réinitialisation est ce qu'on
/// fait quand on croit son compte compromis ; laisser vivre les jetons de
/// rafraîchissement existants viderait le geste de son sens — l'attaquant
/// garderait l'accès qu'on croit venir de lui retirer.
/// </para>
///
/// <para>
/// <b>Le nouveau mot de passe passe par le validateur à deux étages</b> du lot
/// 4 : liste embarquée obligatoire, HaveIBeenPwned opportuniste. Un mot de
/// passe choisi après réinitialisation n'est pas moins exposé qu'un autre, et
/// c'est même l'inverse — on le choisit vite, sous le coup de l'urgence.
/// </para>
/// </remarks>
internal static class Reinitialisation
{
    /// <summary>Le chemin du front qui reçoit le lien.</summary>
    public const string CheminDuFront = "/reinitialiser";

    public static void Router(RouteGroupBuilder groupe)
    {
        ArgumentNullException.ThrowIfNull(groupe);

        groupe
            .MapPost("/mot-de-passe-oublie", OublierAsync)
            .AllowAnonymous()
            .RequireRateLimiting(Limitation.Politique);

        groupe
            .MapPost("/reinitialiser-le-mot-de-passe", ReinitialiserAsync)
            .AllowAnonymous()
            .RequireRateLimiting(Limitation.Politique);
    }

    /// <summary>Envoie le lien — ou fait semblant.</summary>
    public static async Task<IResult> OublierAsync(
        DemandeDOubli corps,
        UserManager<Utilisateur> utilisateurs,
        EnvoyeurSmtp envoyeur,
        ReglagesDuCourrier reglages,
        CancellationToken jeton
    )
    {
        ArgumentNullException.ThrowIfNull(corps);
        ArgumentNullException.ThrowIfNull(utilisateurs);
        ArgumentNullException.ThrowIfNull(envoyeur);
        ArgumentNullException.ThrowIfNull(reglages);

        var compte =
            corps.Email is { Length: > 0 } adresse
                ? await utilisateurs.FindByEmailAsync(adresse).ConfigureAwait(false)
                : null;

        // On n'envoie qu'à une adresse VÉRIFIÉE. Sans cette condition, le
        // produit enverrait un lien de reprise de compte à une adresse dont
        // personne n'a prouvé la possession — ce qui est exactement le chemin
        // qu'une prise de contrôle emprunterait.
        if (compte is { EmailConfirmed: true, Email: { } destinataire })
        {
            var code = await utilisateurs.GeneratePasswordResetTokenAsync(compte)
                .ConfigureAwait(false);

            var lien = string.Create(
                CultureInfo.InvariantCulture,
                $"{reglages.BaseDesLiens}{CheminDuFront}"
                    + $"?compte={compte.Id:D}&code={Verification.Encoder(code)}"
            );

            await envoyeur
                .EnvoyerAsync(Courriel.Reinitialisation, destinataire, lien, jeton: jeton)
                .ConfigureAwait(false);
        }

        return Results.Accepted();
    }

    /// <summary>Consomme le code, pose le nouveau secret, coupe tout le reste.</summary>
    public static async Task<IResult> ReinitialiserAsync(
        DemandeDeNouveauMotDePasse corps,
        UserManager<Utilisateur> utilisateurs,
        MagasinDeSessions sessions,
        TimeProvider horloge,
        CancellationToken jeton
    )
    {
        ArgumentNullException.ThrowIfNull(corps);
        ArgumentNullException.ThrowIfNull(utilisateurs);
        ArgumentNullException.ThrowIfNull(sessions);
        ArgumentNullException.ThrowIfNull(horloge);

        var compte =
            corps.Compte is { Length: > 0 } identifiant
                ? await utilisateurs.FindByIdAsync(identifiant).ConfigureAwait(false)
                : null;

        if (compte is null || string.IsNullOrEmpty(corps.Code))
        {
            return Refus("ReinitialisationInvalide");
        }

        var resultat = await utilisateurs
            .ResetPasswordAsync(
                compte,
                Verification.Decoder(corps.Code),
                corps.NouveauMotDePasse ?? string.Empty
            )
            .ConfigureAwait(false);

        if (!resultat.Succeeded)
        {
            // Le code d'Identity est rendu tel quel — `PasswordTooShort`,
            // `PasswordRequiresDigit`, ou le code du validateur à deux étages.
            // Aucun ne trahit l'existence d'un compte : à ce stade, l'appelant
            // détient déjà un code valide, donc il sait.
            return Refus(resultat.Errors.FirstOrDefault()?.Code ?? "ReinitialisationInvalide");
        }

        await sessions
            .RevoquerToutesAsync(compte.Id, horloge.GetUtcNow(), jeton)
            .ConfigureAwait(false);

        return Results.NoContent();
    }

    private static IResult Refus(string code) =>
        Results.Json(new Reponse(code), statusCode: StatusCodes.Status400BadRequest);
}
