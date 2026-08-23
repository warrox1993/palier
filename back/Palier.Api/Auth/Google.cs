using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Identity;
using Palier.Infrastructure.Identite;

namespace Palier.Api.Auth;

/// <summary>
/// La connexion par Google — exigence 1 de <c>docs/09-comptes.md</c> § 1.
/// </summary>
/// <remarks>
/// <para>
/// <b>Google est OPTIONNEL.</b> Sans identifiants configurés, les deux routes
/// ne sont pas attachées du tout — plutôt qu'attachées et cassées. Un
/// développement local sans compte Google reste donc utilisable, et une
/// production mal configurée refuse visiblement au lieu de rendre une erreur
/// 500 au premier clic.
/// </para>
///
/// <para>
/// <b>Aucun jeton ne passe par l'URL de redirection finale.</b> Le rappel pose
/// le cookie de rafraîchissement — <c>HttpOnly</c>, chemin restreint, comme au
/// lot 4 — et redirige sans paramètre secret. Un jeton dans une URL finit dans
/// l'historique du navigateur, dans les journaux du serveur et dans l'en-tête
/// <c>Referer</c> du premier lien externe cliqué.
/// </para>
/// </remarks>
internal static class Google
{
    /// <summary>Le schéma d'authentification, tel qu'ASP.NET le nomme.</summary>
    public const string Schema = GoogleDefaults.AuthenticationScheme;

    /// <summary>La clé de configuration de l'identifiant client.</summary>
    public const string CleDeLIdentifiant = "GOOGLE_OAUTH_CLIENT_ID";

    /// <summary>La clé de configuration du secret client — au coffre.</summary>
    public const string CleDuSecret = "GOOGLE_OAUTH_CLIENT_SECRET";

    /// <summary>
    /// Les trois scopes, et rien d'autre. <c>docs/09-comptes.md</c> § 1 :
    /// « les scopes <c>profile</c>, <c>email</c> et <c>openid</c> suffisent à ce
    /// que le bouton récupère, et la règle ci-dessus interdit d'en ajouter ».
    /// </summary>
    public static readonly string[] Scopes = ["openid", "profile", "email"];

    /// <summary>La revendication que Google pose quand il a vérifié l'adresse.</summary>
    public const string RevendicationEmailVerifie = "email_verified";

    /// <summary>Google est-il configuré ?</summary>
    public static bool EstConfigure(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return !string.IsNullOrWhiteSpace(configuration[CleDeLIdentifiant])
            && !string.IsNullOrWhiteSpace(configuration[CleDuSecret]);
    }

    /// <summary>Lit le constat depuis ce que Google a rendu.</summary>
    /// <remarks>
    /// <c>email_verified</c> arrive en TEXTE — « true » ou « false » — et non en
    /// booléen : la revendication traverse un JWT, où tout est chaîne. La
    /// comparer sans le dire donnerait un booléen toujours faux, ou toujours
    /// vrai selon la façon de le lire.
    /// </remarks>
    public static ConstatDeGoogle Lire(ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        return new ConstatDeGoogle(
            principal.FindFirstValue(ClaimTypes.NameIdentifier),
            principal.FindFirstValue(ClaimTypes.Email),
            string.Equals(
                principal.FindFirstValue(RevendicationEmailVerifie),
                "true",
                StringComparison.OrdinalIgnoreCase
            )
        );
    }

    /// <summary>Attache les deux routes, si et seulement si Google est configuré.</summary>
    public static void Router(RouteGroupBuilder groupe, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(groupe);

        if (!EstConfigure(configuration))
        {
            return;
        }

        groupe.MapGet("/google", Defier).AllowAnonymous();
        groupe.MapGet("/google/rappel", RappelerAsync).AllowAnonymous();
    }

    /// <summary>Renvoie vers Google.</summary>
    public static IResult Defier() =>
        Results.Challenge(
            new AuthenticationProperties { RedirectUri = PointsDEntree.Prefixe + "/google/rappel" },
            [Schema]
        );

    /// <summary>Applique <see cref="DecisionDeGoogle" /> au retour.</summary>
    public static async Task<IResult> RappelerAsync(
        HttpContext contexte,
        UserManager<Utilisateur> utilisateurs,
        MagasinDeSessions sessions,
        TimeProvider horloge,
        CancellationToken jeton
    )
    {
        ArgumentNullException.ThrowIfNull(contexte);
        ArgumentNullException.ThrowIfNull(utilisateurs);

        var authentification = await contexte.AuthenticateAsync(Schema).ConfigureAwait(false);
        if (!authentification.Succeeded || authentification.Principal is null)
        {
            return Refus();
        }

        var constat = Lire(authentification.Principal);

        // `FindByLoginAsync` d'abord : c'est la question la moins chère, et
        // c'est celle qui décide du cas le plus fréquent.
        var parConnexion =
            constat.Sujet is { Length: > 0 } sujet
                ? await utilisateurs.FindByLoginAsync(Schema, sujet).ConfigureAwait(false)
                : null;

        var parAdresse =
            constat.Email is { Length: > 0 } adresse
                ? await utilisateurs.FindByEmailAsync(adresse).ConfigureAwait(false)
                : null;

        var suite = DecisionDeGoogle.Trancher(
            constat,
            connexionExiste: parConnexion is not null,
            compteEmailExiste: parAdresse is not null
        );

        return suite switch
        {
            SuiteDeGoogle.Connecter => await OuvrirAsync(
                    parConnexion!,
                    contexte,
                    sessions,
                    horloge,
                    jeton
                )
                .ConfigureAwait(false),

            SuiteDeGoogle.Creer => await CreerPuisOuvrirAsync(
                    constat,
                    contexte,
                    utilisateurs,
                    sessions,
                    horloge,
                    jeton
                )
                .ConfigureAwait(false),

            // La liaison se PROPOSE. Le front reçoit un marqueur sans secret :
            // c'est un second appel, avec preuve de possession, qui liera.
            SuiteDeGoogle.ProposerLaLiaison => Results.Redirect("/lier-google"),

            _ => Refus(),
        };
    }

    private static async Task<IResult> CreerPuisOuvrirAsync(
        ConstatDeGoogle constat,
        HttpContext contexte,
        UserManager<Utilisateur> utilisateurs,
        MagasinDeSessions sessions,
        TimeProvider horloge,
        CancellationToken jeton
    )
    {
        var compte = new Utilisateur
        {
            UserName = constat.Email,
            Email = constat.Email,

            // Google a vérifié l'adresse, et `DecisionDeGoogle` a exigé que la
            // revendication le dise. Le compte naît donc vérifié : demander une
            // seconde vérification par courriel n'apporterait rien.
            EmailConfirmed = true,
        };

        var creation = await utilisateurs.CreateAsync(compte).ConfigureAwait(false);
        if (!creation.Succeeded)
        {
            return Refus();
        }

        var liaison = await utilisateurs
            .AddLoginAsync(compte, new UserLoginInfo(Schema, constat.Sujet!, Schema))
            .ConfigureAwait(false);

        return liaison.Succeeded
            ? await OuvrirAsync(compte, contexte, sessions, horloge, jeton)
                .ConfigureAwait(false)
            : Refus();
    }

    private static async Task<IResult> OuvrirAsync(
        Utilisateur compte,
        HttpContext contexte,
        MagasinDeSessions sessions,
        TimeProvider horloge,
        CancellationToken jeton
    )
    {
        var maintenant = horloge.GetUtcNow();
        var ouverture = await sessions
            .OuvrirAsync(compte.Id, Appareil(contexte), maintenant, jeton)
            .ConfigureAwait(false);

        CookieDeRafraichissement.Poser(contexte.Response, ouverture.Jeton, maintenant);

        // AUCUN jeton dans l'URL. Le front demandera son jeton d'accès par
        // `/rafraichir`, avec le cookie que nous venons de poser.
        return Results.Redirect("/");
    }

    private static string Appareil(HttpContext contexte)
    {
        var declare = contexte.Request.Headers.UserAgent.ToString();

        return declare.Length > PointsDEntree.LongueurMaximaleDeLAppareil
            ? declare[..PointsDEntree.LongueurMaximaleDeLAppareil]
            : declare;
    }

    /// <summary>
    /// Un refus ne dit PAS pourquoi. « Adresse non vérifiée » et « compte déjà
    /// pris » sont deux informations sur l'annuaire, et ce produit n'en donne
    /// aucune — la redirection porte un code stable que le front traduit.
    /// </summary>
    private static IResult Refus() =>
        Results.Redirect(
            string.Create(CultureInfo.InvariantCulture, $"/connexion?echec=google")
        );
}
