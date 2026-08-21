using System.Security.Cryptography;

namespace Palier.Infrastructure.Identite;

/// <summary>
/// Engendre les jetons de rafraîchissement et calcule l'empreinte sous laquelle
/// ils sont rangés.
/// </summary>
/// <remarks>
/// <b>SHA-256, et non PBKDF2.</b> Un jeton de rafraîchissement est une valeur
/// aléatoire de 256 bits tirée d'un générateur cryptographique : il n'y a rien à
/// deviner, donc rien à ralentir. Y appliquer les 210 000 itérations du hachage
/// de mot de passe coûterait environ 220 ms à chaque rafraîchissement pour une
/// protection nulle — le facteur de travail protège contre la recherche
/// exhaustive d'un secret à faible entropie, ce qu'un jeton n'est pas.
///
/// <para>
/// L'empreinte existe pour une autre raison : une fuite de la table ne doit pas
/// livrer des jetons utilisables. C'est ce que SHA-256 garantit, et il suffit.
/// </para>
/// </remarks>
public static class HachageDeJeton
{
    /// <summary>Longueur du jeton, en octets. 256 bits d'entropie.</summary>
    public static int OctetsDeJeton => 32;

    /// <summary>
    /// Un jeton neuf, en base64url — sans <c>+</c>, <c>/</c> ni <c>=</c>, donc
    /// transportable en cookie sans encodage supplémentaire.
    /// </summary>
    public static string Engendrer() =>
        Base64UrlEncode(RandomNumberGenerator.GetBytes(OctetsDeJeton));

    /// <summary>L'empreinte SHA-256 du jeton, 32 octets. C'est elle qui est rangée.</summary>
    public static byte[] Calculer(string jeton)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jeton);
        return SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(jeton));
    }

    private static string Base64UrlEncode(byte[] octets) =>
        Convert.ToBase64String(octets).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
