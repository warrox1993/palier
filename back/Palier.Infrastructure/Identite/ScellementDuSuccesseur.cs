using System.Security.Cryptography;
using System.Text;

namespace Palier.Infrastructure.Identite;

/// <summary>
/// Scelle le jeton successeur <b>sous le jeton qu'il remplace</b>, pour que la
/// fenêtre de grâce puisse le rendre au lieu d'en émettre un autre.
/// </summary>
/// <remarks>
/// <para>
/// <b>Le problème que ce type résout.</b> Deux requêtes qui se croisent
/// présentent la même chaîne : la seconde doit repartir avec <b>le même</b>
/// successeur que la première. En émettre un neuf laisserait deux jetons
/// vivants dans une même famille, chacun se faisant tourner de son côté ;
/// aucun jeton consommé ne serait jamais représenté, et la détection de
/// réemploi de la RFC 9700 s'éteindrait pour cette famille. Or la ligne du
/// successeur ne porte que son empreinte SHA-256, qui ne s'inverse pas.
/// </para>
///
/// <para>
/// <b>La clé est le jeton présenté, et rien d'autre.</b> Le sceau s'ouvre pour
/// qui détient déjà le prédécesseur — c'est-à-dire pour qui a déjà le droit
/// d'obtenir ce successeur — et pour personne d'autre. Aucun secret de serveur
/// n'entre ici : il n'y a donc ni clé à déployer, ni clé à faire tourner.
/// </para>
///
/// <para>
/// <b>Ce que la table livre toujours, et ce qu'elle livre en plus.</b> Une
/// fuite de la seule table ne rend aucun jeton utilisable : elle contient
/// l'empreinte, qui ne s'inverse pas, et un sceau dont la clé n'y figure pas.
/// Elle livre en revanche le successeur à qui détient <b>aussi</b> la valeur en
/// clair du jeton consommé qui le porte — celui-là obtenait déjà ce successeur
/// en le présentant dans les trente secondes de la grâce.
/// </para>
///
/// <para>
/// AES-GCM, et non un chiffrement seul : le sceau est authentifié. Un octet
/// modifié en base ne rend pas une valeur silencieusement fausse, il rend
/// <c>null</c> — et l'appelant refuse au lieu de forger une chaîne.
/// </para>
/// </remarks>
internal static class ScellementDuSuccesseur
{
    /// <summary>96 bits, la taille de nonce que le NIST SP 800-38D recommande pour GCM.</summary>
    private const int _octetsDeNonce = 12;

    /// <summary>128 bits, l'étiquette d'authentification pleine longueur.</summary>
    private const int _octetsDEtiquette = 16;

    /// <summary>256 bits de clé, dérivés du jeton parent.</summary>
    private const int _octetsDeCle = 32;

    /// <summary>
    /// La séparation de domaine de la dérivation : le jour où le même jeton
    /// servirait à dériver autre chose, les deux clés resteraient distinctes.
    /// </summary>
    private static readonly byte[] _domaine = "palier:scellement-du-successeur:v1"u8.ToArray();

    /// <summary>Le successeur, scellé sous le jeton parent. Nonce, étiquette, puis chiffré.</summary>
    public static byte[] Sceller(string jetonParent, string successeur)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jetonParent);
        ArgumentException.ThrowIfNullOrWhiteSpace(successeur);

        var clair = Encoding.UTF8.GetBytes(successeur);
        var scelle = new byte[_octetsDeNonce + _octetsDEtiquette + clair.Length];
        RandomNumberGenerator.Fill(scelle.AsSpan(0, _octetsDeNonce));

        using var coffre = new AesGcm(Cle(jetonParent), _octetsDEtiquette);
        coffre.Encrypt(
            scelle.AsSpan(0, _octetsDeNonce),
            clair,
            scelle.AsSpan(_octetsDeNonce + _octetsDEtiquette),
            scelle.AsSpan(_octetsDeNonce, _octetsDEtiquette)
        );

        return scelle;
    }

    /// <summary>
    /// Le successeur, ou <c>null</c> si le sceau est absent, tronqué, forgé, ou
    /// présenté avec un autre jeton que celui qui l'a scellé.
    /// </summary>
    public static string? Ouvrir(string jetonParent, byte[]? scelle)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jetonParent);

        if (scelle is not { Length: > _octetsDeNonce + _octetsDEtiquette })
        {
            return null;
        }

        var clair = new byte[scelle.Length - _octetsDeNonce - _octetsDEtiquette];

        try
        {
            using var coffre = new AesGcm(Cle(jetonParent), _octetsDEtiquette);
            coffre.Decrypt(
                scelle.AsSpan(0, _octetsDeNonce),
                scelle.AsSpan(_octetsDeNonce + _octetsDEtiquette),
                scelle.AsSpan(_octetsDeNonce, _octetsDEtiquette),
                clair
            );
        }
        catch (CryptographicException)
        {
            return null;
        }

        return Encoding.UTF8.GetString(clair);
    }

    private static byte[] Cle(string jetonParent) =>
        HKDF.DeriveKey(
            HashAlgorithmName.SHA256,
            Encoding.UTF8.GetBytes(jetonParent),
            _octetsDeCle,
            info: _domaine
        );
}
