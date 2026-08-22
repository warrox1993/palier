using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Palier.Infrastructure.Identite;

/// <summary>
/// Le <c>UserManager</c> du produit. Il n'existe que pour UNE raison : les
/// codes de récupération de la double authentification sont rangés HACHÉS.
/// </summary>
/// <remarks>
/// <para>
/// <b>Ce qu'Identity range, et pourquoi cela ne suffit pas.</b> Son magasin
/// colle les codes bout à bout, séparés par des points-virgules, et écrit la
/// chaîne TELLE QUELLE dans <c>AspNetUserTokens.Value</c> — sous le
/// fournisseur <c>[AspNetUserStore]</c> et le nom <c>RecoveryCodes</c>. Aucun
/// <c>IPersonalDataProtector</c> n'est enregistré ici, donc rien ne chiffre
/// cette colonne : une lecture de la base rendait dix seconds facteurs
/// UTILISABLES pour chaque compte protégé.
/// </para>
///
/// <para>
/// <b>Pourquoi un gestionnaire dérivé, et non un magasin dérivé.</b>
/// <c>IUserTwoFactorRecoveryCodeStore</c> vit sur le magasin, et le dériver
/// ferait naître un type qui prend <c>PalierAuthDbContext</c> en dépendance —
/// donc une exemption de plus dans <c>ArchitectureTests</c>, c'est-à-dire une
/// porte de plus. Les trois méthodes qui portent les codes sont <c>virtual</c>
/// sur le gestionnaire ; les redéfinir suffit, et aucun appelant ne change
/// puisque tout le produit injecte <c>UserManager&lt;Utilisateur&gt;</c>.
/// </para>
///
/// <para>
/// <b>PBKDF2, là où <see cref="HachageDeJeton" /> se contente de SHA-256.</b>
/// Un jeton de rafraîchissement porte 256 bits tirés d'un générateur
/// cryptographique : il n'y a rien à deviner, donc rien à ralentir. Un code de
/// récupération se lit et se recopie à la main : il est court et tiré d'un
/// alphabet volontairement réduit, donc à portée d'une recherche exhaustive
/// hors ligne si son haché fuit. D'où un facteur de travail ici, et pas
/// là-bas.
/// </para>
///
/// <para>
/// <b>Un seul sel par lot, et c'est délibéré.</b> Un sel par code obligerait à
/// recalculer PBKDF2 autant de fois qu'il reste de codes, à chaque
/// vérification — sur le chemin de connexion, où TOUT code d'authentificateur
/// refusé passe ensuite par ici. Le sel ne sert pas à distinguer deux codes du
/// même compte, il sert à rendre inutile une table précalculée ; un sel neuf à
/// chaque écriture du lot le fait.
/// </para>
///
/// <para>
/// <b>La REPRISE des lots restés en clair.</b> Une base déjà en service porte
/// des codes écrits par Identity, en clair. Les refuser enfermerait dehors qui
/// a perdu son téléphone AVANT le correctif — les codes de récupération SONT
/// le chemin de secours, et <c>/2fa/activer</c> comme <c>/2fa/desactiver</c>
/// exigent l'un et l'autre un code d'authentificateur. Le lot en clair est
/// donc haché tel quel, à la première lecture, et la ligne d'Identity
/// disparaît dans le même geste : les codes que l'utilisateur détient
/// continuent d'ouvrir sa session, et plus rien n'est lisible dans la base.
/// </para>
///
/// <para>
/// <b>CE QU'IL NE COUVRE PAS : la clé partagée du TOTP.</b> Elle reste en
/// clair dans <c>AspNetUserTokens</c>, et ce correctif n'y touche pas. Elle
/// doit être RELUE à chaque vérification — un haché ne conviendrait pas — donc
/// la protéger veut dire la chiffrer, donc décider où vit la clé de
/// chiffrement et comment elle tourne. C'est une décision d'exploitation, pas
/// une ligne de code : elle appartient au porteur du projet.
/// </para>
/// </remarks>
public sealed class GestionnaireDUtilisateurs(
    IUserStore<Utilisateur> magasin,
    IOptions<IdentityOptions> options,
    IPasswordHasher<Utilisateur> hacheur,
    IEnumerable<IUserValidator<Utilisateur>> validateursDUtilisateur,
    IEnumerable<IPasswordValidator<Utilisateur>> validateursDeMotDePasse,
    ILookupNormalizer normalisateur,
    IdentityErrorDescriber erreurs,
    IServiceProvider services,
    ILogger<UserManager<Utilisateur>> journal
)
    : UserManager<Utilisateur>(
        magasin,
        options,
        hacheur,
        validateursDUtilisateur,
        validateursDeMotDePasse,
        normalisateur,
        erreurs,
        services,
        journal
    )
{
    /// <summary>Le fournisseur sous lequel le lot HACHÉ est rangé.</summary>
    public const string Fournisseur = "[Palier]";

    /// <summary>Le nom du jeton qui porte le lot HACHÉ.</summary>
    public const string NomDuJeton = "CodesDeRecuperation";

    /// <summary>
    /// Le fournisseur et le nom sous lesquels Identity range son lot EN CLAIR.
    /// Deux constantes PRIVÉES de son magasin : elles ne peuvent qu'être
    /// recopiées. Ce qui les tient, ce sont les épreuves de reprise — elles
    /// font écrire le lot par le magasin d'Identity, donc sous les VRAIS noms,
    /// et rougissent le jour où ceux-ci changent.
    /// </summary>
    private const string _fournisseurDIdentity = "[AspNetUserStore]";
    private const string _nomDuJetonDIdentity = "RecoveryCodes";

    /// <summary>
    /// Le facteur de travail : <b>210 000 itérations</b>, la même valeur que
    /// <c>Composition</c> configure pour les mots de passe.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Ce n'était pas le premier choix, et le motif du changement mérite d'être
    /// écrit. Cent mille itérations avaient été posées ici parce que c'est le
    /// <i>plancher</i> qu'exige CA5387 — un plancher d'analyseur n'est pas une
    /// recommandation, c'est le seuil en dessous duquel l'outil refuse de se
    /// taire.
    /// </para>
    ///
    /// <para>
    /// Deux raisons de remonter. D'abord la cohérence : le projet a déjà tranché
    /// cette question pour les mots de passe et retenu le chiffre d'OWASP plutôt
    /// que le plancher — une même connaissance ne se règle pas à deux valeurs
    /// selon l'endroit. Ensuite l'ordre de grandeur : un code de récupération
    /// tient sur une quarantaine de bits, là où une phrase de passe en vaut
    /// davantage. Le secret le plus court mérite le facteur de travail le plus
    /// élevé, pas l'inverse.
    /// </para>
    ///
    /// <para>
    /// Le coût reste borné : un code refusé ne paie qu'<b>UN</b> calcul — le sel
    /// est celui du lot — sur un chemin déjà limité en débit et compté par le
    /// verrouillage progressif.
    /// </para>
    /// </remarks>
    private const int _iterations = 210_000;

    private const int _octetsDeSel = 16;
    private const int _octetsDEmpreinte = 32;
    private const char _separateur = ';';

    /// <summary>Engendre un lot neuf et le range HACHÉ.</summary>
    /// <remarks>
    /// <para>
    /// Les paramètres de ces redéfinitions portent les noms de la classe de
    /// base parce que CA1725 l'exige, et le refus est une erreur de compilation
    /// — la même contrainte que sur <see cref="ValidateurDeMotDePasse" />.
    /// </para>
    ///
    /// <para>
    /// <b>Mais pas tous, et la nuance a été mesurée.</b> Un commentaire posé ici
    /// affirmait que les trois — <c>utilisateur</c>, <c>number</c>, <c>code</c> —
    /// étaient contraints. C'est faux pour <c>utilisateur</c> : CA1725 admet un
    /// paramètre nommé d'après son type, si bien que <c>utilisateur</c>
    /// compile. Vérifié en renommant les trois, puis en construisant : seuls
    /// <c>number</c> et <c>code</c> lèvent l'erreur.
    /// </para>
    ///
    /// <para>
    /// Une contrainte affirmée sans mesure est exactement ce que ce dépôt
    /// traque ailleurs. Le paramètre porte donc le nom français que la règle
    /// autorise, et les deux qui restent en anglais le restent pour une raison
    /// qu'on peut reproduire.
    /// </para>
    /// </remarks>
    public override async Task<IEnumerable<string>?> GenerateNewTwoFactorRecoveryCodesAsync(
        Utilisateur utilisateur,
        int number
    )
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(utilisateur);

        var codes = new List<string>(number);
        for (var i = 0; i < number; i++)
        {
            codes.Add(CreateTwoFactorRecoveryCode());
        }

        // `Distinct` comme dans l'implémentation d'origine : deux codes
        // identiques rangés une seule fois feraient qu'un code rendu à
        // l'utilisateur n'ouvrirait rien. Ce qui est rendu est EXACTEMENT ce
        // qui est rangé.
        var lot = codes.Distinct(StringComparer.Ordinal).ToArray();

        return await RangerAsync(utilisateur, lot).ConfigureAwait(false) ? lot : null;
    }

    /// <summary>Consomme un code de récupération, s'il est valide.</summary>
    public override async Task<IdentityResult> RedeemTwoFactorRecoveryCodeAsync(
        Utilisateur utilisateur,
        string code
    )
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(utilisateur);

        var (sel, empreintes) = Lire(await LotAsync(utilisateur).ConfigureAwait(false));
        var candidat = Empreinte(code, sel);

        // Comparaison à temps constant : une comparaison qui s'arrête au
        // premier octet différent dit, par sa durée, combien d'octets étaient
        // justes.
        var restants = empreintes
            .Where(empreinte => !CryptographicOperations.FixedTimeEquals(empreinte, candidat))
            .ToArray();

        if (restants.Length == empreintes.Count)
        {
            return IdentityResult.Failed(ErrorDescriber.RecoveryCodeRedemptionFailed());
        }

        return await EcrireAsync(utilisateur, sel, restants).ConfigureAwait(false);
    }

    /// <summary>Combien de codes restent utilisables.</summary>
    public override async Task<int> CountRecoveryCodesAsync(Utilisateur utilisateur)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(utilisateur);

        var (_, empreintes) = Lire(await LotAsync(utilisateur).ConfigureAwait(false));
        return empreintes.Count;
    }

    /// <summary>
    /// Le lot haché du compte — en reprenant d'abord celui qu'Identity aurait
    /// laissé en clair.
    /// </summary>
    private async Task<string?> LotAsync(Utilisateur utilisateur)
    {
        var hache = await GetAuthenticationTokenAsync(utilisateur, Fournisseur, NomDuJeton)
            .ConfigureAwait(false);
        var clair = await GetAuthenticationTokenAsync(
                utilisateur,
                _fournisseurDIdentity,
                _nomDuJetonDIdentity
            )
            .ConfigureAwait(false);

        if (string.IsNullOrEmpty(clair))
        {
            return hache;
        }

        if (string.IsNullOrEmpty(hache))
        {
            // La reprise : le lot en clair est haché TEL QUEL. Les codes que
            // l'utilisateur détient restent valables, et la ligne en clair s'en
            // va — c'est `RangerAsync` qui l'efface.
            await RangerAsync(utilisateur, clair.Split(_separateur)).ConfigureAwait(false);

            return await GetAuthenticationTokenAsync(utilisateur, Fournisseur, NomDuJeton)
                .ConfigureAwait(false);
        }

        // Les deux lots coexistent — un effacement qui n'a pas abouti. Le lot
        // haché fait foi, il est le plus récent ; le clair s'en va sans rien
        // remplacer.
        await RemoveAuthenticationTokenAsync(
                utilisateur,
                _fournisseurDIdentity,
                _nomDuJetonDIdentity
            )
            .ConfigureAwait(false);

        return hache;
    }

    /// <summary>Range un lot de codes, haché, et efface celui d'Identity.</summary>
    private async Task<bool> RangerAsync(Utilisateur utilisateur, IEnumerable<string> codes)
    {
        var sel = RandomNumberGenerator.GetBytes(_octetsDeSel);
        var pose = await EcrireAsync(utilisateur, sel, codes.Select(code => Empreinte(code, sel)))
            .ConfigureAwait(false);

        // L'EFFACEMENT DU CLAIR, dans le même geste que l'écriture du haché.
        // Sans lui, la ligne `[AspNetUserStore]/RecoveryCodes` survivrait à la
        // réécriture : des codes devenus inutilisables, mais toujours lisibles
        // dans la base, indéfiniment.
        var efface = await RemoveAuthenticationTokenAsync(
                utilisateur,
                _fournisseurDIdentity,
                _nomDuJetonDIdentity
            )
            .ConfigureAwait(false);

        // `&` et non `&&` : les deux écritures sont TENTÉES, l'échec de l'une
        // ne dispense pas de l'autre.
        return pose.Succeeded & efface.Succeeded;
    }

    /// <summary>Écrit le lot : le sel, puis une empreinte par code.</summary>
    private async Task<IdentityResult> EcrireAsync(
        Utilisateur utilisateur,
        byte[] sel,
        IEnumerable<byte[]> empreintes
    )
    {
        var valeur = new StringBuilder(Convert.ToBase64String(sel));
        foreach (var empreinte in empreintes)
        {
            valeur.Append(_separateur).Append(Convert.ToBase64String(empreinte));
        }

        return await SetAuthenticationTokenAsync(
                utilisateur,
                Fournisseur,
                NomDuJeton,
                valeur.ToString()
            )
            .ConfigureAwait(false);
    }

    /// <summary>Décompose ce qui a été rangé : le sel, puis les empreintes.</summary>
    /// <remarks>
    /// <c>FormatException</c> est attrapée, et RIEN d'autre : une valeur qui
    /// n'est pas celle qu'on a écrite — colonne modifiée à la main, format
    /// d'une autre version — ne rend AUCUN code valide, et la vérification
    /// échoue alors comme sur un code faux. Attraper plus large avalerait la
    /// panne du magasin, qui, elle, doit remonter.
    /// </remarks>
    private static (byte[] Sel, IReadOnlyList<byte[]> Empreintes) Lire(string? valeur)
    {
        var champs = (valeur ?? string.Empty).Split(_separateur);
        var empreintes = new List<byte[]>(champs.Length);

        try
        {
            var sel = Convert.FromBase64String(champs[0]);
            for (var i = 1; i < champs.Length; i++)
            {
                empreintes.Add(Convert.FromBase64String(champs[i]));
            }

            return (sel, empreintes);
        }
        catch (FormatException)
        {
            return ([], []);
        }
    }

    private static byte[] Empreinte(string code, byte[] sel) =>
        Rfc2898DeriveBytes.Pbkdf2(
            code,
            sel,
            _iterations,
            HashAlgorithmName.SHA256,
            _octetsDEmpreinte
        );
}
