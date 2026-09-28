using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Palier.Infrastructure.Coffre;

/// <summary>
/// Les clés de données en mémoire, et le chiffrement du secret TOTP — D59.
///
/// Chaque valeur chiffrée NOMME la clé qui l'a produite et est LIÉE à son
/// propriétaire. La première propriété rend la rotation possible sans réécrire
/// toute la table d'un coup ; la seconde interdit de déplacer un secret d'un
/// compte vers un autre — sans elle, un accès en écriture à la base suffirait
/// pour se connecter comme n'importe qui.
///
/// Rien ici ne parle au coffre : le trousseau est déjà déballé quand il arrive.
/// </summary>
public sealed class TrousseauDeChiffrement(
    IReadOnlyDictionary<Guid, byte[]> cles,
    Guid courante
)
{
    private const string _version = "v1";
    private const int _octetsDeNonce = 12;
    private const int _octetsDEtiquette = 16;

    /// <summary>
    /// La détection du clair est EXPLICITE, jamais une heuristique sur
    /// l'alphabet Base32 : « ça ressemble à du Base32 donc c'est du clair » est
    /// le genre de test qui se trompe un jour sur un cas limite, et ce jour-là
    /// il écrase un secret valide.
    /// </summary>
    public static bool EstEnClair(string valeur) =>
        !(valeur ?? string.Empty).StartsWith(_version + ":", StringComparison.Ordinal);

    /// <summary>La valeur porte-t-elle la clé courante ?</summary>
    public bool EstAJour(string valeur) =>
        !EstEnClair(valeur) && Decouper(valeur).Cle == courante;

    public string Chiffrer(string clair, Guid proprietaire)
    {
        var nonce = RandomNumberGenerator.GetBytes(_octetsDeNonce);
        var octets = Encoding.UTF8.GetBytes(clair);
        var chiffre = new byte[octets.Length];
        var etiquette = new byte[_octetsDEtiquette];

        using var gcm = new AesGcm(cles[courante], _octetsDEtiquette);
        gcm.Encrypt(nonce, octets, chiffre, etiquette, proprietaire.ToByteArray());

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{_version}:{courante:N}:{Convert.ToBase64String(nonce)}:{Convert.ToBase64String([.. chiffre, .. etiquette])}"
        );
    }

    public string Dechiffrer(string valeur, Guid proprietaire)
    {
        var (cle, nonce, corps) = Decouper(valeur);

        if (!cles.TryGetValue(cle, out var octetsDeCle))
        {
            // L'identifiant, pour que le message dise QUELLE enveloppe manque
            // en base. Sans lui, le diagnostic serait aveugle sur une base qui
            // en porte plusieurs.
            throw new InvalidOperationException(
                $"Aucune clé de données « {cle:N} » dans le trousseau. "
                    + "L'enveloppe correspondante manque en base, ou le coffre a "
                    + "refusé de la déballer."
            );
        }

        var chiffre = corps.AsSpan(0, corps.Length - _octetsDEtiquette);
        var etiquette = corps.AsSpan(corps.Length - _octetsDEtiquette);
        var clair = new byte[chiffre.Length];

        using var gcm = new AesGcm(octetsDeCle, _octetsDEtiquette);

        // Une CryptographicException remonte telle quelle si l'étiquette ne
        // colle pas — valeur altérée, ou secret déplacé vers un autre compte.
        // C'est le refus, et il ne se déguise pas en valeur abîmée.
        gcm.Decrypt(nonce, chiffre, etiquette, clair, proprietaire.ToByteArray());

        return Encoding.UTF8.GetString(clair);
    }

    private static (Guid Cle, byte[] Nonce, byte[] Corps) Decouper(string valeur)
    {
        var morceaux = (valeur ?? string.Empty).Split(':');

        if (
            morceaux.Length != 4
            || !string.Equals(morceaux[0], _version, StringComparison.Ordinal)
            || !Guid.TryParseExact(morceaux[1], "N", out var cle)
        )
        {
            // Aucune tentative de rattrapage : une forme inattendue refuse
            // plutôt que de deviner. Deviner ici, c'est écrire n'importe quoi
            // dans une colonne de sécurité.
            throw new InvalidOperationException(
                "La valeur chiffrée n'a pas la forme attendue "
                    + "`v1:<clé>:<nonce>:<corps>`."
            );
        }

        try
        {
            var nonce = Convert.FromBase64String(morceaux[2]);
            var corps = Convert.FromBase64String(morceaux[3]);

            if (nonce.Length != _octetsDeNonce || corps.Length < _octetsDEtiquette)
            {
                throw new InvalidOperationException(
                    "La valeur chiffrée porte un nonce ou un corps de taille "
                        + "impossible."
                );
            }

            return (cle, nonce, corps);
        }
        catch (FormatException faute)
        {
            throw new InvalidOperationException(
                "La valeur chiffrée n'est pas du Base64 valide.",
                faute
            );
        }
    }
}
