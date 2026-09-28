using System.Globalization;
using MailKit.Security;
using Microsoft.Extensions.Configuration;

namespace Palier.Infrastructure.Courrier;

/// <summary>
/// De quoi joindre le relais SMTP — D60.
///
/// Le code ne connaît AUCUN fournisseur : l'hôte, le port, les identifiants et
/// l'expéditeur sont de la configuration. Passer d'OVHcloud à un autre relais ne
/// touche pas une ligne. C'est ce qui distingue ce choix d'une API de
/// fournisseur, où le code aurait su à qui il parle.
/// </summary>
public sealed record ReglagesDuCourrier(
    string Hote,
    int Port,
    string Expediteur,
    string? Utilisateur,
    string? MotDePasse,
    string BaseDesLiens
)
{
    /// <summary>
    /// Les quatre réglages EXIGÉS. <c>SMTP_USER</c> et <c>SMTP_PASSWORD</c> n'en
    /// sont pas : un relais interne ne demande pas toujours d'authentification,
    /// et les exiger empêcherait un déploiement parfaitement légitime.
    /// </summary>
    private static readonly string[] _requis = ["SMTP_HOST", "SMTP_PORT", "SMTP_FROM", "APP_URL"];

    /// <summary>
    /// <b>Le chiffrement est décidé par l'hôte, jamais par un réglage.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// Hors de la machine, c'est <c>StartTls</c> — <b>exigé</b>, et non
    /// <c>StartTlsWhenAvailable</c> : ce dernier négocierait en clair si le
    /// serveur ne propose pas l'extension, et un attaquant en position
    /// d'intermédiaire n'aurait qu'à retirer l'annonce pour lire les liens de
    /// vérification de tous les comptes créés.
    /// </para>
    ///
    /// <para>
    /// Sur la boucle locale, <c>None</c>. Ce n'est pas une commodité : le
    /// chiffrement protège d'un intermédiaire sur le réseau, et il n'y a pas de
    /// réseau à traverser entre un processus et un conteneur qui n'écoute que
    /// sur <c>127.0.0.1</c>. Exiger TLS y coûterait des certificats à fabriquer
    /// et à renouveler pour zéro menace.
    /// </para>
    ///
    /// <para>
    /// <b>La décision est AUTOMATIQUE, et c'est ce qui la rend sûre.</b> Un
    /// réglage « désactiver TLS » finirait un jour posé en production par
    /// quelqu'un qui déboguait ; une adresse d'hôte, elle, ne se règle pas par
    /// mégarde — si elle vaut <c>localhost</c> en production, le courriel ne
    /// part de toute façon nulle part.
    /// </para>
    /// </remarks>
    public SecureSocketOptions Chiffrement =>
        Hote is "localhost" or "127.0.0.1" or "::1" or "[::1]"
            ? SecureSocketOptions.None
            : SecureSocketOptions.StartTls;

    public static ReglagesDuCourrier Depuis(IConfiguration source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var absents = _requis.Where(c => string.IsNullOrWhiteSpace(source[c])).ToArray();
        if (absents.Length > 0)
        {
            // Le refus tombe au DÉMARRAGE. Un produit qui démarre sans pouvoir
            // envoyer d'email laisse ses utilisateurs bloqués à l'inscription,
            // et rien ne le signale avant la première plainte.
            throw new InvalidOperationException(
                $"Le courrier ne peut pas partir : {string.Join(", ", absents)} "
                    + "absent(s) de la configuration. Voir back/.env.example."
            );
        }

        if (!int.TryParse(source["SMTP_PORT"], CultureInfo.InvariantCulture, out var port))
        {
            throw new InvalidOperationException(
                "SMTP_PORT n'est pas un nombre. Voir back/.env.example."
            );
        }

        return new(
            source["SMTP_HOST"]!,
            port,
            source["SMTP_FROM"]!,
            Vide(source["SMTP_USER"]),
            Vide(source["SMTP_PASSWORD"]),
            source["APP_URL"]!.TrimEnd('/')
        );
    }

    private static string? Vide(string? valeur) =>
        string.IsNullOrWhiteSpace(valeur) ? null : valeur;
}
