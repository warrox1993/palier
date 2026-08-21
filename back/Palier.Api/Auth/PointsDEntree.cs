using Microsoft.AspNetCore.Identity;
using Palier.Infrastructure.Identite;

namespace Palier.Api.Auth;

/// <summary>Ce que le client envoie pour s'inscrire ou se connecter.</summary>
/// <remarks>
/// Les deux champs sont <b>nullables</b> et le resteront : un corps JSON est une
/// entrée hostile, et un champ absent y est parfaitement légal. Les déclarer
/// non-nullables donnerait une garantie que le désérialiseur ne tient pas.
/// </remarks>
internal sealed record DemandeDIdentifiants(string? Email, string? MotDePasse);

/// <summary>Ce que la connexion rend. Le rafraîchissement n'y est PAS.</summary>
internal sealed record ReponseDeConnexion(string JetonDAcces, int ExpireDansSecondes);

/// <summary>
/// Une réponse qui ne porte qu'un CODE, jamais une phrase.
/// </summary>
/// <remarks>
/// Les libellés vivent en base et passent par i18next — <c>CLAUDE.md</c> § 4,
/// « aucune chaîne de caractères en dur ». Et un code se compare octet pour
/// octet dans une épreuve, là où une phrase invite à des nuances qui finissent
/// par distinguer deux causes d'échec.
/// </remarks>
internal sealed record Reponse(string Code);

/// <summary>
/// L'inscription et la connexion.
/// </summary>
/// <remarks>
/// <para>
/// <b>La règle qui gouverne tout ce fichier : ne jamais laisser deviner si une
/// adresse a un compte.</b> C'est l'énumération de comptes, et sur un produit de
/// santé la seule existence du compte est déjà la donnée sensible — savoir que
/// quelqu'un s'est inscrit ici, c'est savoir qu'il suit un entraînement.
/// </para>
///
/// <para>
/// Elle a deux moitiés, et la seconde est celle qu'on oublie :
/// </para>
/// <list type="number">
///   <item>
///     <b>Le même message</b> pour une adresse inconnue et un mot de passe faux.
///   </item>
///   <item>
///     <b>Le même temps.</b> Une réponse qui revient cent millisecondes plus tôt
///     dit tout ce que le message tait. Les deux chemins paient donc le même
///     PBKDF2 à 210 000 itérations.
///   </item>
/// </list>
///
/// <para>
/// <b>Ce qui échappe à la règle, et pourquoi.</b> Un mot de passe refusé parce
/// qu'il est trop court ou compromis <i>doit</i> le dire : l'information porte
/// sur ce que l'utilisateur vient de taper, pas sur l'existence d'un compte.
/// La taire ne protégerait personne et laisserait un formulaire muet.
/// </para>
/// </remarks>
internal static class PointsDEntree
{
    /// <summary>Le préfixe des routes d'authentification.</summary>
    public const string Prefixe = "/api/v1/auth";

    /// <summary>
    /// Les codes d'erreur d'Identity qui trahissent l'existence d'un compte.
    /// Ils sont <b>avalés</b> ; tous les autres sont rendus.
    /// </summary>
    private static readonly string[] _codesQuiTrahissent =
    [
        "DuplicateUserName",
        "DuplicateEmail",
    ];

    /// <summary>La longueur au-delà de laquelle l'appareil déclaré est coupé.</summary>
    /// <remarks>
    /// Un <c>User-Agent</c> est entièrement sous le contrôle du client : rien
    /// n'empêche d'en envoyer un de dix mégaoctets. La colonne le stockerait.
    /// </remarks>
    public static int LongueurMaximaleDeLAppareil => 200;

    /// <summary>Attache les routes.</summary>
    public static void Router(WebApplication application)
    {
        ArgumentNullException.ThrowIfNull(application);

        var groupe = application.MapGroup(Prefixe);

        // `AllowAnonymous` est EXPLICITE. Il n'est pas redondant : le jour où
        // une politique de repli exigera un jeton par défaut, ces deux routes
        // deviendraient inaccessibles — et plus personne ne pourrait obtenir le
        // jeton qu'elles réclament.
        groupe.MapPost("/inscription", InscrireAsync).AllowAnonymous();
        groupe.MapPost("/connexion", ConnecterAsync).AllowAnonymous();
    }

    /// <summary>
    /// Crée un compte. Rend <b>toujours</b> la même réponse, que l'adresse soit
    /// libre ou déjà prise.
    /// </summary>
    public static async Task<IResult> InscrireAsync(
        DemandeDIdentifiants corps,
        UserManager<Utilisateur> utilisateurs,
        CancellationToken jeton
    )
    {
        ArgumentNullException.ThrowIfNull(corps);
        ArgumentNullException.ThrowIfNull(utilisateurs);
        jeton.ThrowIfCancellationRequested();

        var email = corps.Email?.Trim() ?? string.Empty;

        // `CreateAsync` valide et hache le mot de passe AVANT de contrôler
        // l'unicité de l'adresse. C'est ce qui égalise le temps de réponse sans
        // qu'on ait rien à ajouter — une inscription sur une adresse déjà prise
        // paie le même PBKDF2 qu'une inscription neuve. L'épreuve
        // `Le_MOT_DE_PASSE_est_juge_AVANT_l_unicite_de_l_adresse` garde cet
        // ordre, qui n'est écrit nulle part dans le contrat d'Identity.
        var resultat = await utilisateurs
            .CreateAsync(
                new Utilisateur { UserName = email, Email = email },
                corps.MotDePasse ?? string.Empty
            )
            .ConfigureAwait(false);

        if (resultat.Succeeded)
        {
            return Enregistree();
        }

        var revelateur = Array.Find(
            resultat.Errors.Select(e => e.Code).ToArray(),
            code => !Array.Exists(_codesQuiTrahissent, c => c == code)
        );

        // Rien d'autre que l'unicité n'a échoué : on ne dit pas que l'adresse
        // est prise, et la réponse est celle du succès, à l'octet près.
        return revelateur is null ? Enregistree() : Results.Json(
            new Reponse(revelateur),
            statusCode: StatusCodes.Status400BadRequest
        );
    }

    /// <summary>
    /// Ouvre une session. Le jeton d'accès part dans le corps, le
    /// rafraîchissement dans un cookie <c>HttpOnly</c> — jamais l'inverse.
    /// </summary>
    public static async Task<IResult> ConnecterAsync(
        DemandeDIdentifiants corps,
        UserManager<Utilisateur> utilisateurs,
        MagasinDeSessions sessions,
        SignataireDeJetons signataire,
        IPasswordHasher<Utilisateur> hacheur,
        TimeProvider horloge,
        HttpContext contexte,
        CancellationToken jeton
    )
    {
        ArgumentNullException.ThrowIfNull(corps);
        ArgumentNullException.ThrowIfNull(utilisateurs);
        ArgumentNullException.ThrowIfNull(sessions);
        ArgumentNullException.ThrowIfNull(signataire);
        ArgumentNullException.ThrowIfNull(hacheur);
        ArgumentNullException.ThrowIfNull(horloge);
        ArgumentNullException.ThrowIfNull(contexte);

        var email = corps.Email?.Trim() ?? string.Empty;
        var utilisateur = string.IsNullOrEmpty(email)
            ? null
            : await utilisateurs.FindByEmailAsync(email).ConfigureAwait(false);

        if (utilisateur is null)
        {
            // LE HACHAGE A LIEU QUAND MÊME. Sans lui, la réponse pour une
            // adresse inconnue reviendrait avant celle d'un mot de passe faux,
            // et le chronomètre rendrait l'annuaire interrogeable — l'égalité
            // des messages n'y changerait rien.
            //
            // `HashPassword` fait exactement le même PBKDF2 que la vérification
            // du chemin nominal : 210 000 itérations, même fonction, même coût.
            _ = hacheur.HashPassword(new Utilisateur(), corps.MotDePasse ?? string.Empty);
            return Refus();
        }

        var motDePasse = corps.MotDePasse ?? string.Empty;
        if (!await utilisateurs.CheckPasswordAsync(utilisateur, motDePasse).ConfigureAwait(false))
        {
            return Refus();
        }

        var maintenant = horloge.GetUtcNow();
        var ouverture = await sessions
            .OuvrirAsync(utilisateur.Id, Appareil(contexte.Request), maintenant, jeton)
            .ConfigureAwait(false);

        CookieDeRafraichissement.Poser(contexte.Response, ouverture.Jeton, maintenant);

        return Results.Ok(
            new ReponseDeConnexion(
                signataire.Emettre(utilisateur.Id),
                SignataireDeJetons.DureeEnSecondes
            )
        );
    }

    /// <summary>
    /// L'appareil déclaré, tel quel et coupé. Il n'est jamais interprété : il
    /// sert seulement à ce que l'utilisateur reconnaisse ses propres sessions.
    /// </summary>
    private static string? Appareil(HttpRequest requete)
    {
        var declare = requete.Headers.UserAgent.ToString();
        if (string.IsNullOrWhiteSpace(declare))
        {
            return null;
        }

        return declare.Length <= LongueurMaximaleDeLAppareil
            ? declare
            : declare[..LongueurMaximaleDeLAppareil];
    }

    private static IResult Enregistree() =>
        Results.Json(new Reponse("InscriptionEnregistree"), statusCode: StatusCodes.Status202Accepted);

    private static IResult Refus() =>
        Results.Json(new Reponse("IdentifiantsInvalides"), statusCode: StatusCodes.Status401Unauthorized);
}
