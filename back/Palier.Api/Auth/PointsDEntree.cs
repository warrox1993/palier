using Microsoft.AspNetCore.Identity;
using Palier.Application.Pipeline;
using Palier.Infrastructure.Courrier;
using Palier.Infrastructure.Identite;

namespace Palier.Api.Auth;

/// <summary>Ce que le client envoie pour s'inscrire ou se connecter.</summary>
/// <remarks>
/// Les deux champs sont <b>nullables</b> et le resteront : un corps JSON est une
/// entrée hostile, et un champ absent y est parfaitement légal. Les déclarer
/// non-nullables donnerait une garantie que le désérialiseur ne tient pas.
/// </remarks>
internal sealed record DemandeDIdentifiants(
    string? Email,
    string? MotDePasse,
    string? CodeDeDeuxFacteurs = null
);

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

    /// <summary>
    /// Le temps minimum qu'un refus de connexion met à revenir.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Le message identique ne suffit pas : le temps parle aussi.</b> Les
    /// branches de refus ne coûtent pas la même chose. Une adresse inconnue ne
    /// paie qu'un PBKDF2 ; une adresse CONNUE dont le mot de passe est faux paie
    /// en plus l'enregistrement de l'échec — une transaction avec verrou de
    /// ligne depuis que le compteur est sérialisé. Chronométrer la réponse
    /// séparait donc les deux populations, et rendait l'annuaire interrogeable
    /// malgré des corps de réponse identiques à l'octet près.
    /// </para>
    ///
    /// <para>
    /// Sur un produit de santé, l'annuaire EST la donnée sensible : savoir que
    /// quelqu'un a un compte ici, c'est savoir qu'il suit un entraînement.
    /// </para>
    ///
    /// <para>
    /// Toutes les branches de refus attendent donc le même budget : adresse
    /// inconnue, compte verrouillé, mot de passe faux, second facteur absent ou
    /// faux. La valeur couvre le pire chemin — PBKDF2 à 210 000 itérations plus
    /// la transaction d'échec — avec de la marge pour une base lente. Trop
    /// courte, elle ne masquerait rien ; démesurée, elle offrirait un levier
    /// d'épuisement à qui ouvre mille connexions.
    /// </para>
    ///
    /// <para>
    /// Le chemin de RÉUSSITE n'est pas égalisé, délibérément : il rend un jeton,
    /// donc il se distingue déjà par son corps. L'égaliser ne coûterait que de
    /// la latence à l'utilisateur légitime.
    /// </para>
    /// </remarks>
    public static TimeSpan BudgetDeRefus => TimeSpan.FromMilliseconds(400);

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
        groupe.MapPost("/inscription", InscrireAsync).AllowAnonymous().RequireRateLimiting(Limitation.Politique);
        groupe.MapPost("/connexion", ConnecterAsync).AllowAnonymous().RequireRateLimiting(Limitation.Politique);

        // Le rafraîchissement et la déconnexion sont ANONYMES, et ce n'est pas
        // un oubli : le cookie est leur seul justificatif, et le jeton d'accès
        // est justement celui qui vient d'expirer quand on les appelle. Les
        // exiger authentifiés rendrait la déconnexion impossible passé un quart
        // d'heure d'inactivité — et la première réaction serait de fermer
        // l'onglet en laissant la session vivante.
        groupe.MapPost("/rafraichir", RafraichirAsync).AllowAnonymous().RequireRateLimiting(Limitation.Politique);

        // La déconnexion n'est PAS limitée, délibérément. Un utilisateur qui a
        // épuisé son seau doit pouvoir couper ses sessions — c'est justement le
        // geste qu'on fait quand quelque chose ne va pas.
        groupe.MapPost("/deconnexion", DeconnecterAsync).AllowAnonymous();

        // Ces deux-là, en revanche, agissent sur TOUTES les sessions : il faut
        // savoir de qui, et un cookie ne le dit pas — il dit seulement qu'on
        // détient une chaîne.
        groupe.MapPost("/deconnexion-totale", DeconnecterPartoutAsync).RequireAuthorization();
        groupe.MapGet("/sessions", ListerAsync).RequireAuthorization();

        Verification.Router(groupe);
        Reinitialisation.Router(groupe);
        Google.Router(groupe, application.Configuration);
        LiaisonGoogle.Router(groupe);
        DeuxFacteurs.Router(groupe);
    }

    /// <summary>
    /// Crée un compte. Rend <b>toujours</b> la même réponse, que l'adresse soit
    /// libre ou déjà prise.
    /// </summary>
    public static async Task<IResult> InscrireAsync(
        DemandeDIdentifiants corps,
        UserManager<Utilisateur> utilisateurs,
        EnvoyeurSmtp envoyeur,
        ReglagesDuCourrier reglages,
        CancellationToken jeton
    )
    {
        ArgumentNullException.ThrowIfNull(corps);
        ArgumentNullException.ThrowIfNull(utilisateurs);
        ArgumentNullException.ThrowIfNull(envoyeur);
        ArgumentNullException.ThrowIfNull(reglages);
        jeton.ThrowIfCancellationRequested();

        var email = corps.Email?.Trim() ?? string.Empty;

        // `CreateAsync` valide et hache le mot de passe AVANT de contrôler
        // l'unicité de l'adresse. C'est ce qui égalise le temps de réponse sans
        // qu'on ait rien à ajouter — une inscription sur une adresse déjà prise
        // paie le même PBKDF2 qu'une inscription neuve. L'épreuve
        // `Le_MOT_DE_PASSE_est_juge_AVANT_l_unicite_de_l_adresse` garde cet
        // ordre, qui n'est écrit nulle part dans le contrat d'Identity.
        var nouveau = new Utilisateur { UserName = email, Email = email };
        var resultat = await utilisateurs
            .CreateAsync(nouveau, corps.MotDePasse ?? string.Empty)
            .ConfigureAwait(false);

        if (resultat.Succeeded)
        {
            await Verification
                .EnvoyerLaVerificationAsync(utilisateurs, envoyeur, reglages, nouveau, email, jeton)
                .ConfigureAwait(false);

            return Enregistree();
        }

        var revelateur = Array.Find(
            resultat.Errors.Select(e => e.Code).ToArray(),
            code => !Array.Exists(_codesQuiTrahissent, c => c == code)
        );

        if (revelateur is not null)
        {
            return Results.Json(
                new Reponse(revelateur),
                statusCode: StatusCodes.Status400BadRequest
            );
        }

        // Rien d'autre que l'unicité n'a échoué : l'adresse est prise. On ne le
        // dit pas — la réponse est celle du succès, à l'octet près — ET ON
        // ENVOIE QUAND MÊME un courriel, à son propriétaire légitime.
        //
        // Sans ce second envoi, le seul fait qu'un message parte ou non
        // trahirait l'existence du compte : le temps de réponse suffirait à
        // interroger l'annuaire. Le lot 4 a fermé ce canal sur la connexion ;
        // l'inscription le rouvrait par la porte de derrière.
        await AvertirLeProprietaireAsync(envoyeur, reglages, email, jeton).ConfigureAwait(false);

        return Enregistree();
    }

    /// <summary>
    /// Avertit le propriétaire d'une adresse qu'on a tenté de s'inscrire avec.
    /// Il ne porte AUCUN code : le message ne fait qu'informer, et pointe vers
    /// la réinitialisation si la personne a simplement oublié son mot de passe.
    /// </summary>
    private static Task AvertirLeProprietaireAsync(
        EnvoyeurSmtp envoyeur,
        ReglagesDuCourrier reglages,
        string adresse,
        CancellationToken jeton
    ) =>
        envoyeur.EnvoyerAsync(
            Courriel.TentativeDInscription,
            adresse,
            reglages.BaseDesLiens + "/mot-de-passe-oublie",
            jeton: jeton
        );

    /// <summary>
    /// Ouvre une session. Le jeton d'accès part dans le corps, le
    /// rafraîchissement dans un cookie <c>HttpOnly</c> — jamais l'inverse.
    /// </summary>
    public static async Task<IResult> ConnecterAsync(
        DemandeDIdentifiants corps,
        UserManager<Utilisateur> utilisateurs,
        MagasinDeSessions sessions,
        SignataireDeJetons signataire,
        GardienDeVerrouillage gardien,
        IPasswordHasher<Utilisateur> hacheur,
        TimeProvider horloge,
        HttpContext contexte,
        CancellationToken jeton
    )
    {
        ArgumentNullException.ThrowIfNull(corps);
        ArgumentNullException.ThrowIfNull(utilisateurs);
        ArgumentNullException.ThrowIfNull(sessions);
        ArgumentNullException.ThrowIfNull(gardien);
        ArgumentNullException.ThrowIfNull(signataire);
        ArgumentNullException.ThrowIfNull(hacheur);
        ArgumentNullException.ThrowIfNull(horloge);
        ArgumentNullException.ThrowIfNull(contexte);

        var email = corps.Email?.Trim() ?? string.Empty;
        var maintenant = horloge.GetUtcNow();

        // Le chronomètre part ICI, avant toute lecture : c'est ce qui permet à
        // chaque branche de refus de revenir au même instant.
        var depart = horloge.GetTimestamp();

        var utilisateur = string.IsNullOrEmpty(email)
            ? null
            : await utilisateurs.FindByEmailAsync(email).ConfigureAwait(false);

        // LE HACHAGE A LIEU QUAND MÊME, sur les deux chemins qui refusent avant
        // toute vérification. Sans lui, ces réponses reviendraient avant celle
        // d'un mot de passe faux, et le chronomètre rendrait l'annuaire
        // interrogeable — l'égalité des messages n'y changerait rien.
        //
        // `HashPassword` fait exactement le même PBKDF2 que la vérification du
        // chemin nominal : 210 000 itérations, même fonction, même coût.
        if (utilisateur is null)
        {
            _ = hacheur.HashPassword(new Utilisateur(), corps.MotDePasse ?? string.Empty);
            return await RefusEgaliseAsync(horloge, depart, jeton).ConfigureAwait(false);
        }

        // ⚠ LE VERROU SE CONTRÔLE AVANT LA VÉRIFICATION DU MOT DE PASSE, ET
        // C'EST L'ORDRE QUI COMPTE.
        //
        // Vérifier d'abord, pour pouvoir dire « compte verrouillé » à qui
        // connaît le mot de passe, serait plus agréable — et transformerait le
        // verrouillage en ORACLE : l'attaquant continuerait de tester des mots
        // de passe pendant le verrouillage et saurait, au changement de
        // réponse, lequel est le bon. Le verrou n'empêcherait plus la
        // découverte, seulement l'ouverture de session, c'est-à-dire rien.
        //
        // Le coût est réel et assumé : l'utilisateur légitime qui tape le BON
        // mot de passe pendant son verrouillage lit « identifiants invalides ».
        // C'est au message générique du front d'inviter à réessayer plus tard.
        if (GardienDeVerrouillage.EstVerrouille(utilisateur, maintenant))
        {
            _ = hacheur.HashPassword(new Utilisateur(), corps.MotDePasse ?? string.Empty);
            return await RefusEgaliseAsync(horloge, depart, jeton).ConfigureAwait(false);
        }

        var motDePasse = corps.MotDePasse ?? string.Empty;
        if (!await utilisateurs.CheckPasswordAsync(utilisateur, motDePasse).ConfigureAwait(false))
        {
            await gardien.EnregistrerUnEchecAsync(utilisateur, maintenant).ConfigureAwait(false);
            return await RefusEgaliseAsync(horloge, depart, jeton).ConfigureAwait(false);
        }

        // La double authentification, si elle est active. Le mot de passe vient
        // d'être vérifié : dire ici « il manque le code » n'apprend donc rien à
        // qui ne le savait pas déjà, et c'est ce que le front doit lire pour
        // afficher le champ.
        if (utilisateur.TwoFactorEnabled)
        {
            var code = corps.CodeDeDeuxFacteurs;

            if (string.IsNullOrWhiteSpace(code))
            {
                return await EgaliserAsync(
                        horloge,
                        depart,
                        Results.Json(
                            new Reponse("DeuxFacteursRequis"),
                            statusCode: StatusCodes.Status401Unauthorized
                        ),
                        jeton
                    )
                    .ConfigureAwait(false);
            }

            if (
                !await DeuxFacteurs
                    .SecondFacteurValideAsync(utilisateurs, utilisateur, code)
                    .ConfigureAwait(false)
            )
            {
                // Un code faux COMPTE comme un échec. Sans cela, la limitation
                // par compte s'arrêterait au mot de passe, et six chiffres
                // seraient devinables en un million d'essais — sans jamais
                // verrouiller.
                await gardien.EnregistrerUnEchecAsync(utilisateur, maintenant).ConfigureAwait(false);

                return await EgaliserAsync(
                        horloge,
                        depart,
                        Results.Json(
                            new Reponse("DeuxFacteursRequis"),
                            statusCode: StatusCodes.Status401Unauthorized
                        ),
                        jeton
                    )
                    .ConfigureAwait(false);
            }
        }

        await gardien.EnregistrerUneReussiteAsync(utilisateur).ConfigureAwait(false);
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
    /// Fait tourner le jeton de rafraîchissement. Le cookie est le seul
    /// justificatif ; le jeton d'accès qui accompagne la réponse est neuf.
    /// </summary>
    /// <remarks>
    /// <b>Un seul code d'échec pour six causes.</b> Cookie absent, session
    /// inconnue, expirée, révoquée, réemploi détecté : la réponse est la même.
    /// Distinguer « réemploi détecté » apprendrait à un voleur qu'il a été
    /// repéré — et lui dirait, du même coup, que le jeton qu'il détient était
    /// bien authentique.
    /// </remarks>
    public static async Task<IResult> RafraichirAsync(
        MagasinDeSessions sessions,
        SignataireDeJetons signataire,
        TimeProvider horloge,
        HttpContext contexte,
        CancellationToken jeton
    )
    {
        ArgumentNullException.ThrowIfNull(sessions);
        ArgumentNullException.ThrowIfNull(signataire);
        ArgumentNullException.ThrowIfNull(horloge);
        ArgumentNullException.ThrowIfNull(contexte);

        var presente = CookieDeRafraichissement.Lire(contexte.Request);
        if (presente is null)
        {
            return SessionRefusee();
        }

        var maintenant = horloge.GetUtcNow();
        var rotation = await sessions
            .FaireTournerAsync(presente, Appareil(contexte.Request), maintenant, jeton)
            .ConfigureAwait(false);

        if (rotation.Jeton is not { } neuf || rotation.Utilisateur is not { } utilisateur)
        {
            // Le cookie est EFFACÉ. Le garder ferait re-présenter la même
            // chaîne morte à chaque tentative, et sur un réemploi détecté cela
            // rejouerait la révocation indéfiniment.
            CookieDeRafraichissement.Effacer(contexte.Response);
            return SessionRefusee();
        }

        CookieDeRafraichissement.Poser(contexte.Response, neuf, maintenant);

        return Results.Ok(
            new ReponseDeConnexion(
                signataire.Emettre(utilisateur),
                SignataireDeJetons.DureeEnSecondes
            )
        );
    }

    /// <summary>Coupe la chaîne du cookie présenté. Rend <b>toujours</b> 204.</summary>
    /// <remarks>
    /// Une déconnexion n'échoue jamais du point de vue de l'utilisateur : quoi
    /// qu'il arrive, le cookie est effacé. Rendre une erreur sur un cookie déjà
    /// périmé laisserait un bouton « se déconnecter » qui refuse de marcher.
    /// </remarks>
    public static async Task<IResult> DeconnecterAsync(
        MagasinDeSessions sessions,
        TimeProvider horloge,
        HttpContext contexte,
        CancellationToken jeton
    )
    {
        ArgumentNullException.ThrowIfNull(sessions);
        ArgumentNullException.ThrowIfNull(horloge);
        ArgumentNullException.ThrowIfNull(contexte);

        var presente = CookieDeRafraichissement.Lire(contexte.Request);
        if (presente is not null)
        {
            await sessions
                .RevoquerFamilleAsync(presente, horloge.GetUtcNow(), jeton)
                .ConfigureAwait(false);
        }

        CookieDeRafraichissement.Effacer(contexte.Response);
        return Results.NoContent();
    }

    /// <summary>Révoque TOUTES les sessions de l'utilisateur, immédiatement.</summary>
    /// <remarks>
    /// C'est le geste qu'on fait après avoir perdu un téléphone. Il agit sur le
    /// champ, là où le <c>SecurityStamp</c> d'Identity ne produit qu'un effet
    /// différé, borné par l'intervalle de revalidation.
    /// </remarks>
    public static async Task<IResult> DeconnecterPartoutAsync(
        IIdentiteDemandeur demandeur,
        MagasinDeSessions sessions,
        TimeProvider horloge,
        HttpContext contexte,
        CancellationToken jeton
    )
    {
        ArgumentNullException.ThrowIfNull(demandeur);
        ArgumentNullException.ThrowIfNull(sessions);
        ArgumentNullException.ThrowIfNull(horloge);
        ArgumentNullException.ThrowIfNull(contexte);

        if (demandeur.Identifiant is not { } utilisateur)
        {
            return Results.Unauthorized();
        }

        await sessions
            .RevoquerToutesAsync(utilisateur, horloge.GetUtcNow(), jeton)
            .ConfigureAwait(false);

        CookieDeRafraichissement.Effacer(contexte.Response);
        return Results.NoContent();
    }

    /// <summary>Les sessions vivantes de l'utilisateur, pour l'écran des réglages.</summary>
    /// <remarks>
    /// L'identité vient du jeton VÉRIFIÉ, jamais d'un paramètre de requête —
    /// D36. Sans cela, <c>?utilisateur=&lt;autre&gt;</c> listerait les appareils
    /// de n'importe qui, avec leurs dates de dernière activité.
    /// </remarks>
    public static async Task<IResult> ListerAsync(
        IIdentiteDemandeur demandeur,
        MagasinDeSessions sessions,
        TimeProvider horloge,
        CancellationToken jeton
    )
    {
        ArgumentNullException.ThrowIfNull(demandeur);
        ArgumentNullException.ThrowIfNull(sessions);
        ArgumentNullException.ThrowIfNull(horloge);

        if (demandeur.Identifiant is not { } utilisateur)
        {
            return Results.Unauthorized();
        }

        return Results.Ok(
            await sessions.ListerAsync(utilisateur, horloge.GetUtcNow(), jeton).ConfigureAwait(false)
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

    /// <summary>Rend le refus générique, pas avant le budget.</summary>
    private static Task<IResult> RefusEgaliseAsync(
        TimeProvider horloge,
        long depart,
        CancellationToken jeton
    ) => EgaliserAsync(horloge, depart, Refus(), jeton);

    /// <summary>Attend que le budget soit écoulé, puis rend la réponse.</summary>
    /// <remarks>
    /// L'attente est calculée sur le temps DÉJÀ passé, jamais ajoutée en bloc :
    /// une branche lente n'attend rien, une branche rapide comble l'écart, et
    /// les deux reviennent au même instant. Ajouter un délai fixe à toutes
    /// laisserait l'écart intact, simplement décalé.
    /// </remarks>
    private static async Task<IResult> EgaliserAsync(
        TimeProvider horloge,
        long depart,
        IResult reponse,
        CancellationToken jeton
    )
    {
        var reste = BudgetDeRefus - horloge.GetElapsedTime(depart);
        if (reste > TimeSpan.Zero)
        {
            await Task.Delay(reste, horloge, jeton).ConfigureAwait(false);
        }

        return reponse;
    }

    private static IResult Refus() =>
        Results.Json(
            new Reponse("IdentifiantsInvalides"),
            statusCode: StatusCodes.Status401Unauthorized
        );

    private static IResult SessionRefusee() =>
        Results.Json(
            new Reponse("SessionInvalide"),
            statusCode: StatusCodes.Status401Unauthorized
        );
}
