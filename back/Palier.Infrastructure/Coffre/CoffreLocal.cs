using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;

namespace Palier.Infrastructure.Coffre;

/// <summary>
/// D'où vient la clé de données : du coffre OVHcloud KMS, ou du poste.
/// </summary>
public enum ModeDuCoffre
{
    /// <summary>Le chemin de D59, inchangé : configuration et enveloppes déballées par OKMS.</summary>
    Okms,

    /// <summary>D80 : configuration du poste, clé de données lue dans <c>PALIER_CLE_LOCALE</c>.</summary>
    Local,
}

/// <summary>
/// Le coffre LOCAL — D80. Une clé de données lue dans la configuration du
/// poste, à la place de l'enveloppe qu'OVHcloud KMS déballe.
/// </summary>
/// <remarks>
/// <para>
/// Il existe pour une seule raison : faire tourner l'API sur un poste, et dans
/// la démonstration conteneurisée, sans compte OVHcloud. Avant lui, l'API ne
/// démarrait nulle part sans un vrai coffre, pas même en développement.
/// </para>
///
/// <para>
/// <b>Il ne peut pas atteindre la production</b>, et c'est ce qui le rend
/// acceptable. Trois refus, tous au démarrage, avant que le port s'ouvre :
/// </para>
/// <list type="number">
/// <item>il faut le DEMANDER, par <c>PALIER_COFFRE=local</c>. L'absence de la
/// variable laisse le chemin d'OKMS exactement tel qu'il était, refus compris :
/// un coffre injoignable ne fait jamais basculer en local ;</item>
/// <item>l'environnement doit être <c>Development</c>, écrit exactement.
/// <c>Production</c>, <c>Staging</c>, <c>development</c> en minuscules ou une
/// faute de frappe refusent — la même règle que <c>CheminPour</c>, qui ne
/// devine pas non plus ;</item>
/// <item>aucune des cinq variables d'OKMS ne doit être posée. Un poste qui
/// porte les identifiants du vrai coffre est un poste d'exploitation : le
/// mélange se refuse plutôt que de choisir.</item>
/// </list>
///
/// <para>
/// Le format chiffré ne change pas : <see cref="TrousseauDeChiffrement" /> reste
/// le seul à chiffrer, avec la même liaison au propriétaire. Seule l'origine de
/// la clé diffère.
/// </para>
/// </remarks>
public static class CoffreLocal
{
    /// <summary>La variable qui demande le mode local.</summary>
    public const string CleDuMode = "PALIER_COFFRE";

    /// <summary>La seule valeur qu'elle accepte.</summary>
    public const string ValeurDuMode = "local";

    /// <summary>La variable qui porte la clé de données, en Base64.</summary>
    public const string CleDeLaCle = "PALIER_CLE_LOCALE";

    /// <summary>Le seul environnement où le mode local existe.</summary>
    public const string SeulEnvironnementPermis = "Development";

    /// <summary>
    /// Trente-deux octets : la taille des clés de données qu'OKMS rend
    /// (<c>size = 256</c>, mesuré le 22/08/2026). Le mode local imite la
    /// production jusque-là, pour qu'une valeur chiffrée ait la même forme.
    /// </summary>
    private const int _octetsDeCle = 32;

    /// <summary>
    /// Le mode que la configuration demande, ou un refus qui dit pourquoi.
    /// </summary>
    /// <param name="configuration">La configuration d'amorçage, lue AVANT toute source de coffre.</param>
    /// <param name="environnement">Le nom de l'environnement d'hébergement.</param>
    public static ModeDuCoffre Choisir(IConfiguration configuration, string environnement)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var valeur = configuration[CleDuMode];

        // `IsNullOrEmpty` et non `IsNullOrWhiteSpace` : une variable posée à
        // des espaces n'est pas une absence, c'est une valeur inconnue, et elle
        // doit refuser plus bas plutôt que de passer pour le défaut.
        if (string.IsNullOrEmpty(valeur))
        {
            return ModeDuCoffre.Okms;
        }

        if (!string.Equals(valeur, ValeurDuMode, StringComparison.Ordinal))
        {
            // La valeur reçue n'est pas recopiée : ce n'est pas un secret, mais
            // un message de démarrage qui recopie ce qu'on lui donne finit un
            // jour par recopier ce qu'il ne fallait pas.
            throw new InvalidOperationException(
                $"{CleDuMode} ne connaît qu'une valeur, « {ValeurDuMode} ». Pour le coffre "
                    + "OVHcloud KMS, retirer la variable de l'environnement."
            );
        }

        if (!string.Equals(environnement, SeulEnvironnementPermis, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Le coffre local est refusé en « {environnement} ». Il n'existe que pour "
                    + $"l'environnement « {SeulEnvironnementPermis} » : une instance d'exploitation "
                    + $"ne se passe pas d'OVHcloud KMS (D59, D80). Retirer {CleDuMode}."
            );
        }

        var posees = ReglagesDuCoffre
            .Variables.Where(v => !string.IsNullOrWhiteSpace(configuration[v]))
            .ToArray();

        if (posees.Length > 0)
        {
            // Les NOMS, jamais les valeurs : l'une d'elles est le secret du
            // compte de service.
            throw new InvalidOperationException(
                $"{CleDuMode}={ValeurDuMode} est refusé : {string.Join(", ", posees)} "
                    + "posée(s). Ces variables ouvrent le vrai coffre ; les porter en mode "
                    + "local est un mélange, et il se refuse plutôt que de choisir."
            );
        }

        return ModeDuCoffre.Local;
    }

    /// <summary>
    /// Le trousseau local : une seule clé, lue dans la configuration.
    /// </summary>
    /// <remarks>
    /// L'identifiant de la clé est DÉRIVÉ de la clé, et non tiré au hasard. Les
    /// secrets TOTP chiffrés nomment leur clé ; un identifiant neuf à chaque
    /// démarrage les rendrait illisibles après un simple redémarrage. Dérivé
    /// par HMAC, il est stable pour une même clé, différent pour une autre, et
    /// ne dit rien de la clé elle-même.
    /// </remarks>
    public static TrousseauDeChiffrement Trousseau(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var brute = configuration[CleDeLaCle];
        if (string.IsNullOrWhiteSpace(brute))
        {
            throw new InvalidOperationException(
                $"{CleDeLaCle} est absente. Le coffre local chiffre avec une clé de "
                    + $"{_octetsDeCle} octets lue dans la configuration. En développement : "
                    + $"`dotnet user-secrets set {CleDeLaCle} \"$(openssl rand -base64 32)\"` ; "
                    + "la démonstration l'engendre elle-même (`demarrer-demo.sh`)."
            );
        }

        byte[] cle;
        try
        {
            cle = Convert.FromBase64String(brute);
        }
        catch (FormatException faute)
        {
            throw new InvalidOperationException(
                $"{CleDeLaCle} n'est pas du Base64 valide.",
                faute
            );
        }

        if (cle.Length != _octetsDeCle)
        {
            throw new InvalidOperationException(
                $"{CleDeLaCle} porte {cle.Length} octet(s), {_octetsDeCle} attendus."
            );
        }

        var identifiant = new Guid(
            HMACSHA256.HashData(cle, "palier:cle-locale:identifiant"u8).AsSpan(0, 16)
        );

        return new TrousseauDeChiffrement(
            new Dictionary<Guid, byte[]> { [identifiant] = cle },
            identifiant
        );
    }
}
