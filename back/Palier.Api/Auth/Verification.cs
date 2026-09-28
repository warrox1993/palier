using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.AspNetCore.Identity;
using Palier.Infrastructure.Courrier;
using Palier.Infrastructure.Identite;

namespace Palier.Api.Auth;

/// <summary>Ce que le front renvoie après avoir suivi le lien.</summary>
internal sealed record DemandeDeVerification(string? Compte, string? Code);

/// <summary>Une demande de renvoi, par adresse.</summary>
internal sealed record DemandeDeRenvoi(string? Email);

/// <summary>
/// La vérification d'adresse — exigence 2 de <c>docs/09-comptes.md</c> § 1.
/// </summary>
/// <remarks>
/// <para>
/// Le lot 4 avait livré la RÈGLE — <c>PorteDesDomaines.NutritionOuverte</c> lit
/// <c>EmailConfirmed</c> — et pas le MOYEN de la satisfaire. Aucun compte ne
/// pouvait donc atteindre la nutrition.
/// </para>
///
/// <para>
/// <b>Le renvoi rend 202 que le compte existe ou non</b>, et n'envoie que s'il
/// existe et n'est pas déjà vérifié. Sans cela, il devient un oracle
/// d'énumération — le défaut exact que le lot 4 a corrigé sur l'inscription en
/// découvrant que <c>RequireUniqueEmail</c> valait <c>false</c> par défaut.
/// </para>
/// </remarks>
internal static class Verification
{
    /// <summary>Le chemin du front qui reçoit le lien.</summary>
    public const string CheminDuFront = "/verifier";

    /// <summary>Attache les deux routes au groupe d'authentification.</summary>
    public static void Router(RouteGroupBuilder groupe)
    {
        ArgumentNullException.ThrowIfNull(groupe);

        groupe
            .MapPost("/verifier-l-adresse", VerifierAsync)
            .AllowAnonymous()
            .RequireRateLimiting(Limitation.Politique);

        groupe
            .MapPost("/renvoyer-la-verification", RenvoyerAsync)
            .AllowAnonymous()
            .RequireRateLimiting(Limitation.Politique);
    }

    /// <summary>Consomme le code et pose <c>EmailConfirmed</c>.</summary>
    public static async Task<IResult> VerifierAsync(
        DemandeDeVerification corps,
        UserManager<Utilisateur> utilisateurs,
        CancellationToken jeton
    )
    {
        ArgumentNullException.ThrowIfNull(corps);
        ArgumentNullException.ThrowIfNull(utilisateurs);
        jeton.ThrowIfCancellationRequested();

        var compte =
            corps.Compte is { Length: > 0 } identifiant
                ? await utilisateurs.FindByIdAsync(identifiant).ConfigureAwait(false)
                : null;

        // Un compte introuvable et un code faux rendent la MÊME chose. Les
        // distinguer dirait à l'appelant quels identifiants existent.
        if (compte is null || string.IsNullOrEmpty(corps.Code))
        {
            return Refus();
        }

        var resultat = await utilisateurs
            .ConfirmEmailAsync(compte, Decoder(corps.Code))
            .ConfigureAwait(false);

        return resultat.Succeeded ? Results.NoContent() : Refus();
    }

    /// <summary>Renvoie le courriel — ou fait semblant.</summary>
    public static async Task<IResult> RenvoyerAsync(
        DemandeDeRenvoi corps,
        UserManager<Utilisateur> utilisateurs,
        EnvoyeurSmtp envoyeur,
        ReglagesDuCourrier reglages,
        CancellationToken jeton
    )
    {
        ArgumentNullException.ThrowIfNull(corps);
        ArgumentNullException.ThrowIfNull(utilisateurs);
        ArgumentNullException.ThrowIfNull(envoyeur);

        var compte =
            corps.Email is { Length: > 0 } adresse
                ? await utilisateurs.FindByEmailAsync(adresse).ConfigureAwait(false)
                : null;

        if (compte is { EmailConfirmed: false, Email: { } destinataire })
        {
            await EnvoyerLaVerificationAsync(
                    utilisateurs,
                    envoyeur,
                    reglages,
                    compte,
                    destinataire,
                    jeton
                )
                .ConfigureAwait(false);
        }

        // 202 dans TOUS les cas, et un corps vide. Le code, le corps et le
        // temps doivent être indiscernables : c'est le trio qui ferme l'oracle.
        return Results.Accepted();
    }

    /// <summary>
    /// Compose et envoie le lien de vérification. Partagé par l'inscription et
    /// par le renvoi : deux copies divergeraient, et la divergence porterait sur
    /// la façon dont un code est encodé dans une URL.
    /// </summary>
    public static async Task EnvoyerLaVerificationAsync(
        UserManager<Utilisateur> utilisateurs,
        EnvoyeurSmtp envoyeur,
        ReglagesDuCourrier reglages,
        Utilisateur compte,
        string destinataire,
        CancellationToken jeton
    )
    {
        ArgumentNullException.ThrowIfNull(utilisateurs);
        ArgumentNullException.ThrowIfNull(envoyeur);
        ArgumentNullException.ThrowIfNull(reglages);
        ArgumentNullException.ThrowIfNull(compte);

        var code = await utilisateurs.GenerateEmailConfirmationTokenAsync(compte)
            .ConfigureAwait(false);

        var lien = string.Create(
            CultureInfo.InvariantCulture,
            $"{reglages.BaseDesLiens}{CheminDuFront}"
                + $"?compte={compte.Id:D}&code={Encoder(code)}"
        );

        await envoyeur
            .EnvoyerAsync(Courriel.Verification, destinataire, lien, jeton: jeton)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Le jeton d'Identity est du Base64 : il porte <c>+</c>, <c>/</c> et
    /// <c>=</c>, que l'URL mange. Il voyage donc en <b>Base64Url</b>, la forme
    /// que le reste d'ASP.NET emploie pour la même raison.
    /// </summary>
    /// <remarks>
    /// <b>Une première version encodait en Base64 SIMPLE, et c'était faux d'une
    /// façon qui ne se voyait pas.</b> Le jeton étant déjà du Base64,
    /// <c>Convert.FromBase64String</c> réussissait sur un jeton NON encodé et
    /// rendait d'autres octets — le décodage « marchait » en produisant un
    /// jeton invalide. L'épreuve
    /// <c>Un_code_VALIDE_pose_EmailConfirmed</c> l'a attrapé.
    /// </remarks>
    public static string Encoder(string code) =>
        WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));

    /// <summary>
    /// Public parce que la réinitialisation lit le même format. Deux copies du
    /// décodage divergeraient, et la divergence porterait sur la façon dont un
    /// code de sécurité traverse une URL.
    /// </summary>
    public static string Decoder(string code)
    {
        try
        {
            return Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code));
        }
        catch (FormatException)
        {
            // Un code illisible n'est pas un incident : c'est une saisie
            // fautive ou un lien tronqué par un client de messagerie. Il repart
            // dans la branche du refus ordinaire.
            return code;
        }
    }

    private static IResult Refus() =>
        Results.Json(new Reponse("VerificationInvalide"), statusCode: StatusCodes.Status400BadRequest);
}
