using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;

namespace Palier.Infrastructure.Identite;

/// <summary>
/// Le refus d'un mot de passe compromis, à deux étages.
/// </summary>
/// <remarks>
/// <para>
/// <b>Le dilemme « échec ouvert ou fermé » repose sur une prémisse fausse.</b>
/// Il suppose que le seul contrôle possible soit distant. ASVS 5.0 autorise
/// explicitement la vérification « localement <b>ou</b> via une API » : rien
/// n'oblige à ne dépendre que du réseau.
/// </para>
///
/// <list type="number">
///   <item>
///     <b>Étage local, obligatoire.</b> Une liste embarquée, chargée une fois.
///     Aucun réseau, aucune base, aucune requête — <b>il ne peut pas échouer</b>.
///   </item>
///   <item>
///     <b>Étage réseau, opportuniste.</b> L'API k-anonymat de HIBP, avec un
///     délai d'attente court. Si elle ne répond pas, l'étage 1 a déjà tranché.
///   </item>
/// </list>
///
/// <para>
/// Le contrôle n'échoue donc <b>jamais ouvert</b> — sa partie essentielle ne
/// dépend pas du réseau — et il ne bloque <b>jamais</b> les inscriptions.
/// </para>
///
/// <para>
/// <b>Pourquoi l'étage local suffit à porter le risque.</b> Le bourrage
/// d'identifiants représente 22 % des fuites en 2024-2025, premier vecteur, et
/// il s'appuie sur les mots de passe fréquents. NIST SP 800-63B révision 4 vise
/// « <i>commonly-used, expected, or compromised</i> » : le <i>commonly-used</i>
/// est le cœur du risque. Un mot de passe vu une seule fois dans une fuite n'est
/// pas devinable ; c'est le bonus que l'étage 2 apporte quand il le peut.
/// </para>
/// </remarks>
public sealed class ValidateurDeMotDePasse(HttpClient client) : IPasswordValidator<Utilisateur>
{
    /// <summary>
    /// L'API n'a droit qu'à ce délai. Au-delà, l'inscription continue — l'étage
    /// local a déjà tranché.
    /// </summary>
    public static TimeSpan DelaiDAttente => TimeSpan.FromSeconds(2);

    private static readonly Lazy<HashSet<string>> _frequents = new(ChargerLaListe);

    /// <summary>La liste embarquée, pour que les épreuves la comptent.</summary>
    public static int NombreDeMotsDePasseFrequents => _frequents.Value.Count;

    public async Task<IdentityResult> ValidateAsync(
        UserManager<Utilisateur> manager,
        Utilisateur user,
        string? password
    )
    {
        if (string.IsNullOrEmpty(password))
        {
            // La longueur et la nullité relèvent de PasswordOptions ; ce
            // validateur ne double pas ce contrôle, il ne le contredit pas.
            return IdentityResult.Success;
        }

        // ---- Étage 1 : local, et il ne peut pas échouer --------------------
        if (_frequents.Value.Contains(password))
        {
            return Refus();
        }

        // ---- Étage 2 : opportuniste ----------------------------------------
        return await EstConnuDeHibpAsync(password).ConfigureAwait(false)
            ? Refus()
            : IdentityResult.Success;
    }

    /// <summary>
    /// Interroge l'API k-anonymat : seuls les CINQ premiers caractères de
    /// l'empreinte SHA-1 sortent d'ici, jamais le mot de passe.
    /// </summary>
    /// <remarks>
    /// L'en-tête <c>Add-Padding</c> uniformise la réponse à 800-1000 entrées :
    /// sans lui, la taille de la réponse laisserait deviner combien de fuites
    /// portent ce préfixe. Les entrées de remplissage ont un compte de zéro et
    /// sont écartées.
    ///
    /// <para>
    /// SHA-1 n'est pas ici une fonction de hachage de mot de passe — c'est le
    /// format que l'API impose, et le préfixe partagé est précisément ce qui
    /// rend la requête anonyme.
    /// </para>
    ///
    /// <para>
    /// <b>Toute défaillance rend faux</b>, et c'est délibéré : réseau coupé,
    /// délai dépassé, service en panne, réponse illisible. L'étage local a déjà
    /// fait son travail.
    /// </para>
    /// </remarks>
    private async Task<bool> EstConnuDeHibpAsync(string motDePasse)
    {
        try
        {
            // CA5350 signale SHA-1 comme algorithme faible, et il a raison DANS
            // SON DOMAINE : SHA-1 ne doit plus servir de fonction de hachage
            // résistante aux collisions. Ce n'est pas son rôle ici. L'API de HIBP
            // impose ce format, et le préfixe partagé de cinq caractères est
            // précisément ce qui rend la requête anonyme — c'est le mécanisme du
            // k-anonymat, pas une protection de secret. Le mot de passe, lui, est
            // haché par PBKDF2-HMAC-SHA512 à 210 000 itérations, ailleurs.
#pragma warning disable CA5350
            var empreinte = Convert
                .ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(motDePasse)))
                .ToUpperInvariant();
#pragma warning restore CA5350
            var prefixe = empreinte[..5];
            var suffixe = empreinte[5..];

            using var requete = new HttpRequestMessage(
                HttpMethod.Get,
                new Uri($"https://api.pwnedpasswords.com/range/{prefixe}")
            );
            requete.Headers.Add("Add-Padding", "true");

            using var source = new CancellationTokenSource(DelaiDAttente);
            using var reponse = await client
                .SendAsync(requete, source.Token)
                .ConfigureAwait(false);

            if (!reponse.IsSuccessStatusCode)
            {
                return false;
            }

            var corps = await reponse.Content.ReadAsStringAsync(source.Token).ConfigureAwait(false);

            foreach (var ligne in corps.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var separateur = ligne.IndexOf(':', StringComparison.Ordinal);
                if (separateur <= 0)
                {
                    continue;
                }

                var candidat = ligne[..separateur].Trim();
                if (!candidat.Equals(suffixe, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // Le remplissage porte un compte de zéro : le trouver ne prouve
                // rien, et le prendre pour une correspondance refuserait des
                // mots de passe parfaitement sains.
                var compte = ligne[(separateur + 1)..].Trim();
                return long.TryParse(compte, NumberStyles.Integer, CultureInfo.InvariantCulture, out var vu)
                    && vu > 0;
            }

            return false;
        }
#pragma warning disable CA1031 // Toute défaillance de l'API doit rendre faux — c'est le point.
        catch (Exception)
#pragma warning restore CA1031
        {
            return false;
        }
    }

    private static IdentityResult Refus() =>
        IdentityResult.Failed(
            new IdentityError
            {
                // Un CODE, jamais une phrase : les libellés vivent en base et
                // passent par i18next.
                Code = "MotDePasseCompromis",
                Description = "MotDePasseCompromis",
            }
        );

    private static HashSet<string> ChargerLaListe()
    {
        var assemblage = Assembly.GetExecutingAssembly();
        var nom = Array.Find(
            assemblage.GetManifestResourceNames(),
            n => n.EndsWith("mots-de-passe-frequents.txt", StringComparison.Ordinal)
        );

        if (nom is null)
        {
            // Quatrième question du franchissement : un contrôle qui n'a plus
            // de cible doit crier. Une liste absente rendrait ce validateur
            // silencieusement inoffensif.
            throw new InvalidOperationException(
                "La liste des mots de passe fréquents est absente de l'assemblage. "
                    + "L'étage local du validateur serait sans effet, et rien ne le dirait. "
                    + "Vérifier l'EmbeddedResource de Palier.Infrastructure.csproj."
            );
        }

        using var flux =
            assemblage.GetManifestResourceStream(nom)
            ?? throw new InvalidOperationException($"La ressource « {nom} » ne s'ouvre pas.");
        using var lecteur = new StreamReader(flux, Encoding.UTF8);

        var liste = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (lecteur.ReadLine() is { } ligne)
        {
            var nue = ligne.Trim();
            if (nue.Length > 0 && !nue.StartsWith('#'))
            {
                liste.Add(nue);
            }
        }

        return liste;
    }
}
