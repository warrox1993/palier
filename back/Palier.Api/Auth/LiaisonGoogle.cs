using System.Globalization;
using Microsoft.AspNetCore.Identity;
using Palier.Infrastructure.Coffre;
using Palier.Infrastructure.Identite;

namespace Palier.Api.Auth;

/// <summary>Ce que le front renvoie pour lier un compte Google à un compte email.</summary>
internal sealed record DemandeDeLiaison(
    string? Email,
    string? MotDePasse,
    string? CodeDeDeuxFacteurs
);

/// <summary>
/// La fusion des comptes — exigence 7 de <c>docs/09-comptes.md</c> § 1.
/// </summary>
/// <remarks>
/// <para>
/// <b>La preuve de possession est le mot de passe du compte existant</b>, et le
/// second facteur s'il est actif. Lier sur la seule égalité des adresses serait
/// une prise de contrôle : <c>09-comptes.md</c> le dit en toutes lettres.
/// </para>
///
/// <para>
/// <b>Le sceau de liaison n'est pas stocké.</b> Il est chiffré sous la clé de
/// données, avec l'identifiant du compte visé en données associées — donc il ne
/// vaut que pour CE compte, et un sceau volé ne lie rien ailleurs. Une table
/// pour un objet qui vit dix minutes coûterait un schéma, une purge, et une
/// épreuve de purge.
/// </para>
///
/// <para>
/// <b>Il voyage en cookie, jamais dans l'URL</b> — même raison qu'au § 6 de la
/// conception : une URL finit dans l'historique, les journaux et le
/// <c>Referer</c>.
/// </para>
/// </remarks>
internal static class LiaisonGoogle
{
    /// <summary>Le nom du cookie qui porte le sceau.</summary>
    public const string NomDuCookie = "palier_liaison";

    /// <summary>Le chemin auquel le cookie est restreint.</summary>
    public const string CheminDuCookie = PointsDEntree.Prefixe + "/lier-google";

    /// <summary>Dix minutes : le temps d'un parcours, pas d'une session.</summary>
    public static TimeSpan Duree => TimeSpan.FromMinutes(10);

    public static void Router(RouteGroupBuilder groupe)
    {
        ArgumentNullException.ThrowIfNull(groupe);

        groupe
            .MapPost("/lier-google", LierAsync)
            .AllowAnonymous()
            .RequireRateLimiting(Limitation.Politique);
    }

    /// <summary>Compose le sceau que le rappel de Google posera en cookie.</summary>
    public static string Sceller(
        TrousseauDeChiffrement trousseau,
        Guid compte,
        string sujet,
        DateTimeOffset expiration
    )
    {
        ArgumentNullException.ThrowIfNull(trousseau);

        return trousseau.Chiffrer(
            string.Create(
                CultureInfo.InvariantCulture,
                $"{sujet}|{expiration.ToUnixTimeSeconds()}"
            ),
            compte
        );
    }

    /// <summary>Lie le compte Google au compte email, contre preuve de possession.</summary>
    public static async Task<IResult> LierAsync(
        DemandeDeLiaison corps,
        HttpContext contexte,
        UserManager<Utilisateur> utilisateurs,
        PorteurDeTrousseau porteur,
        TimeProvider horloge,
        CancellationToken jeton
    )
    {
        ArgumentNullException.ThrowIfNull(corps);
        ArgumentNullException.ThrowIfNull(contexte);
        ArgumentNullException.ThrowIfNull(utilisateurs);
        ArgumentNullException.ThrowIfNull(porteur);
        ArgumentNullException.ThrowIfNull(horloge);
        jeton.ThrowIfCancellationRequested();

        var compte =
            corps.Email is { Length: > 0 } adresse
                ? await utilisateurs.FindByEmailAsync(adresse).ConfigureAwait(false)
                : null;

        var sceau = contexte.Request.Cookies[NomDuCookie];

        if (compte is null || string.IsNullOrEmpty(sceau))
        {
            return Refus();
        }

        // Le sceau est déchiffré AVEC l'identifiant du compte trouvé. S'il a été
        // produit pour un autre compte, les données associées ne collent pas et
        // le déchiffrement échoue — c'est ce qui empêche un sceau volé de lier
        // n'importe quel compte Google à n'importe quel compte email.
        string ouvert;
        try
        {
            ouvert = porteur.Trousseau.Dechiffrer(sceau, compte.Id);
        }
        catch (Exception faute)
            when (faute is InvalidOperationException or System.Security.Cryptography.CryptographicException)
        {
            return Refus();
        }

        var morceaux = ouvert.Split('|');
        if (
            morceaux.Length != 2
            || !long.TryParse(morceaux[1], CultureInfo.InvariantCulture, out var echeance)
            || DateTimeOffset.FromUnixTimeSeconds(echeance) < horloge.GetUtcNow()
        )
        {
            return Refus();
        }

        // LA PREUVE DE POSSESSION. Sans elle, ce point d'entrée lierait un
        // compte Google à n'importe quel compte dont on connaît l'adresse.
        if (!await utilisateurs.CheckPasswordAsync(compte, corps.MotDePasse ?? string.Empty)
            .ConfigureAwait(false))
        {
            return Refus();
        }

        // Le second facteur garde le compte, pas seulement la connexion — c'est
        // ce que le lot 4 a établi. Lier une seconde voie d'accès est
        // exactement le geste qu'il doit couvrir.
        if (compte.TwoFactorEnabled && !await SecondFacteurValideAsync(compte, corps, utilisateurs)
            .ConfigureAwait(false))
        {
            return Refus();
        }

        var liaison = await utilisateurs
            .AddLoginAsync(compte, new UserLoginInfo(Google.Schema, morceaux[0], Google.Schema))
            .ConfigureAwait(false);

        if (!liaison.Succeeded)
        {
            return Refus();
        }

        // Le sceau est à USAGE UNIQUE : le cookie est effacé, et il n'y a rien
        // d'autre à révoquer puisque rien n'est stocké.
        contexte.Response.Cookies.Delete(NomDuCookie, new CookieOptions { Path = CheminDuCookie });

        return Results.NoContent();
    }

    private static async Task<bool> SecondFacteurValideAsync(
        Utilisateur compte,
        DemandeDeLiaison corps,
        UserManager<Utilisateur> utilisateurs
    ) =>
        corps.CodeDeDeuxFacteurs is { Length: > 0 } code
        && await utilisateurs
            .VerifyTwoFactorTokenAsync(
                compte,
                utilisateurs.Options.Tokens.AuthenticatorTokenProvider,
                code
            )
            .ConfigureAwait(false);

    /// <summary>
    /// Un seul refus pour toutes les causes : adresse inconnue, sceau absent,
    /// sceau d'un autre compte, sceau périmé, mot de passe faux, second facteur
    /// manquant. Les distinguer dirait à l'appelant où il en est.
    /// </summary>
    private static IResult Refus() =>
        Results.Json(new Reponse("LiaisonInvalide"), statusCode: StatusCodes.Status400BadRequest);
}
