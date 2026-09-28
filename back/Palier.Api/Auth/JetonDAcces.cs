using System.Globalization;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Palier.Api.Auth;

/// <summary>
/// Le jeton d'accès : quinze minutes, signé, porteur de la seule identité.
/// </summary>
/// <remarks>
/// <para>
/// <b>Quinze minutes.</b> OWASP place les jetons d'accès portant des données de
/// santé entre cinq et quinze minutes. C'est aussi ce qui borne l'effet d'une
/// déconnexion : révoquer les sessions agit immédiatement sur le
/// rafraîchissement, et au plus tard au bout d'un quart d'heure sur les jetons
/// déjà émis.
/// </para>
///
/// <para>
/// <b>Il ne porte rien d'autre que l'identité.</b> Ni email, ni rôle, ni
/// consentement : un JWT n'est pas chiffré, seulement signé, et tout ce qu'on y
/// met est lisible par qui le détient. Les drapeaux d'autorisation se lisent en
/// base, à chaque requête, là où ils sont à jour.
/// </para>
/// </remarks>
internal static class JetonDAcces
{
    /// <summary>L'émetteur et l'audience : le produit ne parle qu'à lui-même.</summary>
    public static string Emetteur => "palier";

    /// <summary>
    /// La longueur minimale de la clé de signature, en octets. HMAC-SHA256 exige
    /// au moins 256 bits ; une clé plus courte fait lever la bibliothèque, mais
    /// le refus doit venir d'ici, au démarrage, et non à la première connexion.
    /// </summary>
    public static int OctetsDeCleMinimum => 32;

    /// <summary>Émet un jeton pour un utilisateur.</summary>
    public static string Emettre(Guid utilisateur, string cle, DateTimeOffset maintenant)
    {
        var identifiants = Signature(cle);

        var descripteur = new SecurityTokenDescriptor
        {
            Issuer = Emetteur,
            Audience = Emetteur,
            IssuedAt = maintenant.UtcDateTime,
            NotBefore = maintenant.UtcDateTime,
            Expires = (maintenant + Palier.Application.Sessions.ParametresDeSession.DureeDuJetonDAcces).UtcDateTime,
            Subject = new ClaimsIdentity(
                [
                    new Claim(
                        JwtRegisteredClaimNames.Sub,
                        utilisateur.ToString("D", CultureInfo.InvariantCulture)
                    ),
                    // Un identifiant propre au jeton : il rend deux jetons émis
                    // dans la même seconde distinguables, ce dont la
                    // journalisation a besoin sans jamais voir l'utilisateur.
                    new Claim(
                        JwtRegisteredClaimNames.Jti,
                        Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)
                    ),
                ]
            ),
            SigningCredentials = identifiants,
        };

        return new JsonWebTokenHandler().CreateToken(descripteur);
    }

    /// <summary>
    /// Les paramètres de validation. <c>ClockSkew</c> est mis à zéro : le défaut
    /// de cinq minutes prolongerait de 33 % la vie d'un jeton de quinze minutes,
    /// et rendrait la durée annoncée fausse.
    /// </summary>
    public static TokenValidationParameters Validation(string cle) =>
        new()
        {
            ValidateIssuer = true,
            ValidIssuer = Emetteur,
            ValidateAudience = true,
            ValidAudience = Emetteur,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = Cle(cle),
            ClockSkew = TimeSpan.Zero,
        };

    private static SigningCredentials Signature(string cle) =>
        new(Cle(cle), SecurityAlgorithms.HmacSha256);

    private static SymmetricSecurityKey Cle(string cle)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cle);

        var octets = Encoding.UTF8.GetBytes(cle);
        if (octets.Length < OctetsDeCleMinimum)
        {
            // Le refus vient d'ici, en toutes lettres. Sans lui, la
            // bibliothèque lèverait à la première connexion, avec un message
            // qui ne nomme ni la variable ni la longueur attendue.
            throw new InvalidOperationException(
                $"JWT_SIGNING_KEY fait {octets.Length} octet(s) ; il en faut au moins "
                    + $"{OctetsDeCleMinimum} pour HMAC-SHA256. Une clé plus courte réduit la "
                    + "signature à la longueur de la clé, quelle que soit la fonction employée."
            );
        }

        return new SymmetricSecurityKey(octets);
    }
}
