using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Palier.Application.Pipeline;
using Palier.Infrastructure.Identite;

namespace Palier.Api.Auth;

/// <summary>Ce que la préparation rend : de quoi enrôler un authentificateur.</summary>
/// <param name="Uri">
/// L'URI <c>otpauth://</c>. Le navigateur en dessine le QR code ; le backend
/// n'embarque aucune bibliothèque graphique pour cela.
/// </param>
/// <param name="Cle">
/// La même clé, en clair et par groupes de quatre — pour qui saisit à la main,
/// faute de caméra.
/// </param>
internal sealed record PreparationDeDeuxFacteurs(string Uri, string Cle);

/// <summary>Un code d'authentificateur, ou un code de récupération.</summary>
internal sealed record DemandeDeCode(string? Code);

/// <summary>
/// Les codes de récupération, rendus <b>une seule fois</b>.
/// </summary>
internal sealed record CodesDeRecuperation(IReadOnlyList<string> Codes);

/// <summary>
/// La double authentification par TOTP.
/// </summary>
/// <remarks>
/// <para>
/// <b>Ce qu'Identity apporte, et ce qui manque.</b> Toute la cryptographie est
/// là : clé partagée, <c>AuthenticatorTokenProvider</c>, vérification du code,
/// codes de récupération. Ce qui manque est le <b>QR code</b> — la
/// documentation le dit en toutes lettres, « les modèles d'application web
/// ASP.NET Core prennent en charge les authentificateurs, mais ne fournissent
/// pas la génération de QR code ».
/// </para>
///
/// <para>
/// <b>Et il n'en sera pas ajouté ici.</b> Le backend rend l'URI ; le navigateur
/// la dessine. Une bibliothèque de QR code côté serveur serait une dépendance
/// de plus pour produire une image que le client peut faire lui-même — et
/// <c>CLAUDE.md</c> § 4 est net : « éviter d'ajouter une dépendance ».
/// </para>
///
/// <para>
/// <b>Ce qui est haché en base, et ce qui ne l'est pas.</b> Les codes de
/// récupération le sont — <see cref="GestionnaireDUtilisateurs" /> redéfinit
/// les trois méthodes qu'Identity range en clair. LA CLÉ PARTAGÉE, elle, reste
/// EN CLAIR dans <c>AspNetUserTokens</c> : elle doit être relue à chaque
/// vérification, un haché ne conviendrait donc pas, et la chiffrer demande de
/// décider où vit la clé de chiffrement — une décision d'exploitation, pas une
/// ligne de code. Qui lit la base d'identité peut donc encore engendrer les
/// codes à six chiffres d'un compte ; il ne peut plus lire ses codes de
/// récupération.
/// </para>
///
/// <para>
/// <b>Le code TOTP ne se journalise jamais.</b> La documentation Microsoft en
/// fait une règle, et pour une raison précise : un code reste valable
/// <i>plusieurs</i> authentifications avant d'expirer — la fenêtre de
/// vérification couvre les périodes voisines. Un code trouvé dans un journal
/// n'est donc pas forcément périmé.
/// </para>
/// </remarks>
internal static class DeuxFacteurs
{
    /// <summary>Le nom affiché par l'authentificateur.</summary>
    public const string Emetteur = "palier";

    /// <summary>
    /// Les paramètres de l'URI, écrits EXPLICITEMENT.
    /// </summary>
    /// <remarks>
    /// Ce sont les valeurs par défaut de la RFC 6238, et la plupart des
    /// authentificateurs les supposent quand elles manquent. « La plupart » est
    /// exactement le problème : ceux qui ne les supposent pas produisent des
    /// codes que le serveur refuse, et l'utilisateur n'a aucun moyen de
    /// comprendre pourquoi. Les écrire coûte trente caractères.
    ///
    /// <para>
    /// <c>SHA1</c> n'est pas un choix : <c>AuthenticatorTokenProvider</c>
    /// d'Identity l'impose, comme la quasi-totalité des authentificateurs.
    /// Annoncer <c>SHA256</c> ferait produire des codes que la vérification
    /// rejetterait.
    /// </para>
    /// </remarks>
    public const string Algorithme = "SHA1";

    /// <summary>Six chiffres — le défaut de la RFC 6238.</summary>
    public static int Chiffres => 6;

    /// <summary>Trente secondes — le défaut de la RFC 6238.</summary>
    public static int PeriodeEnSecondes => 30;

    /// <summary>Le nombre de codes de récupération engendrés.</summary>
    public static int NombreDeCodesDeRecuperation => 10;

    /// <summary>Attache les routes.</summary>
    public static void Router(RouteGroupBuilder groupe)
    {
        ArgumentNullException.ThrowIfNull(groupe);

        // Toutes authentifiées : on ne règle la double authentification que de
        // son propre compte, et l'identité vient du jeton vérifié — D36.
        //
        // Toutes limitées : `activer` et `desactiver` vérifient un code à six
        // chiffres. Sans limitation, un million d'essais suffirait à le
        // deviner — et la limitation par compte du verrouillage ne s'applique
        // qu'à la connexion.
        groupe
            .MapPost("/2fa/preparer", PreparerAsync)
            .RequireAuthorization()
            .RequireRateLimiting(Limitation.Politique);
        groupe
            .MapPost("/2fa/activer", ActiverAsync)
            .RequireAuthorization()
            .RequireRateLimiting(Limitation.Politique);
        groupe
            .MapPost("/2fa/desactiver", DesactiverAsync)
            .RequireAuthorization()
            .RequireRateLimiting(Limitation.Politique);
    }

    /// <summary>
    /// Engendre une clé neuve et rend de quoi enrôler un authentificateur. La
    /// double authentification n'est <b>pas</b> activée par cet appel.
    /// </summary>
    /// <remarks>
    /// <para>
    /// La clé est RÉINITIALISÉE à chaque préparation. Réutiliser une clé
    /// existante ferait qu'une préparation abandonnée — l'écran fermé, le QR
    /// jamais scanné — laisserait la clé connue de qui l'a vue passer.
    /// </para>
    ///
    /// <para>
    /// <b>Et c'est exactement pourquoi une protection DÉJÀ active exige ici une
    /// preuve.</b> Remplacer la clé, c'est détruire celle du téléphone de
    /// l'utilisateur : sans preuve de possession, un jeton d'accès volé
    /// suffirait à enrôler un authentificateur à soi, puis à faire regénérer
    /// les dix codes de récupération — qui invalide ceux de la victime. Elle ne
    /// passerait plus sa propre porte. Une protection ne se remplace pas avec
    /// la seule chose contre laquelle elle protège.
    /// </para>
    ///
    /// <para>
    /// <b>Le corps est NULLABLE, et il le restera.</b> Au premier enrôlement il
    /// n'y a rien à prouver — <c>TwoFactorEnabled</c> est faux — et la requête
    /// part sans corps du tout. Un paramètre non-nullable est REQUIS pour la
    /// liaison des API minimales : ce premier enrôlement recevrait 400, et ce
    /// 400-là viendrait de la couche de liaison, donc SANS code, contrairement
    /// à tous les refus de ce dépôt.
    /// </para>
    /// </remarks>
    public static async Task<IResult> PreparerAsync(
        DemandeDeCode? corps,
        IIdentiteDemandeur demandeur,
        UserManager<Utilisateur> utilisateurs,
        CancellationToken jeton
    )
    {
        ArgumentNullException.ThrowIfNull(demandeur);
        ArgumentNullException.ThrowIfNull(utilisateurs);
        jeton.ThrowIfCancellationRequested();

        if (await CompteAsync(demandeur, utilisateurs).ConfigureAwait(false) is not { } utilisateur)
        {
            return Results.Unauthorized();
        }

        // La preuve n'est réclamée que s'il y a quelque chose à détruire. Corps
        // absent vaut code absent : au premier enrôlement il n'existe ni
        // authentificateur ni code de récupération, et l'exiger fermerait la
        // porte d'entrée.
        if (
            utilisateur.TwoFactorEnabled
            && !await SecondFacteurValideAsync(utilisateurs, utilisateur, corps?.Code)
                .ConfigureAwait(false)
        )
        {
            return Refus();
        }

        var pose = await utilisateurs.ResetAuthenticatorKeyAsync(utilisateur).ConfigureAwait(false);
        if (!pose.Succeeded)
        {
            // `ResetAuthenticatorKeyAsync` rend un `IdentityResult` que rien
            // n'oblige à lire. L'ignorer ferait rendre un QR code parfaitement
            // valide pour une clé QUI N'A PAS ÉTÉ ENREGISTRÉE : l'utilisateur
            // scannerait, activerait, et se retrouverait dehors.
            throw new InvalidOperationException(
                "La clé d'authentificateur n'a pas pu être enregistrée : "
                    + string.Join(", ", pose.Errors.Select(e => e.Code))
            );
        }

        var cle = await utilisateurs.GetAuthenticatorKeyAsync(utilisateur).ConfigureAwait(false);

        if (string.IsNullOrEmpty(cle))
        {
            // Quatrième question du franchissement : un mécanisme qui n'a plus
            // sa cible doit crier. Sans fournisseur de jetons enregistré,
            // Identity rend une clé vide — et l'écran afficherait un QR code
            // qui n'enrôle rien.
            throw new InvalidOperationException(
                "Identity n'a pas engendré de clé d'authentificateur. Le fournisseur de "
                    + "jetons est-il enregistré ? Voir `AddDefaultTokenProviders` dans "
                    + "Composition.Composer."
            );
        }

        return Results.Ok(new PreparationDeDeuxFacteurs(Uri(utilisateur.Email, cle), Grouper(cle)));
    }

    /// <summary>
    /// Active la double authentification, contre un code valide, et rend les
    /// codes de récupération — <b>une seule fois</b>.
    /// </summary>
    public static async Task<IResult> ActiverAsync(
        DemandeDeCode corps,
        IIdentiteDemandeur demandeur,
        UserManager<Utilisateur> utilisateurs,
        CancellationToken jeton
    )
    {
        ArgumentNullException.ThrowIfNull(corps);
        ArgumentNullException.ThrowIfNull(demandeur);
        ArgumentNullException.ThrowIfNull(utilisateurs);
        jeton.ThrowIfCancellationRequested();

        if (await CompteAsync(demandeur, utilisateurs).ConfigureAwait(false) is not { } utilisateur)
        {
            return Results.Unauthorized();
        }

        // Le code prouve que l'authentificateur a bien reçu la clé. Sans cette
        // vérification, activer la 2FA sur un QR jamais scanné enfermerait
        // l'utilisateur hors de son propre compte à la connexion suivante.
        if (!await CodeValideAsync(utilisateurs, utilisateur, corps.Code).ConfigureAwait(false))
        {
            return Refus();
        }

        await utilisateurs.SetTwoFactorEnabledAsync(utilisateur, true).ConfigureAwait(false);

        var codes = await utilisateurs
            .GenerateNewTwoFactorRecoveryCodesAsync(utilisateur, NombreDeCodesDeRecuperation)
            .ConfigureAwait(false);

        // UNE SEULE FOIS. Ce n'est pas Identity qui l'assure — son magasin les
        // range EN CLAIR — mais `GestionnaireDUtilisateurs`, qui n'en conserve
        // que des hachés : ni ce serveur ni le support ne pourront les
        // redonner. C'est le point, et c'est à l'écran de le dire clairement
        // avant que l'utilisateur ne ferme.
        return Results.Ok(new CodesDeRecuperation(codes?.ToArray() ?? []));
    }

    /// <summary>Désactive la double authentification, contre un code valide.</summary>
    /// <remarks>
    /// <para>
    /// <b>Le code est exigé, et ce n'est pas une formalité.</b> Sans lui, un
    /// jeton d'accès volé suffirait à retirer la double authentification — puis
    /// à s'installer. La protection ne doit pas se démonter avec la seule chose
    /// contre laquelle elle protège.
    /// </para>
    ///
    /// <para>
    /// Un code de RÉCUPÉRATION vaut preuve, comme à la connexion. Ne prendre
    /// que le code d'authentificateur laisserait le téléphone perdu sans issue :
    /// l'utilisateur pourrait encore se connecter en brûlant un code de secours
    /// à chaque fois, sans jamais pouvoir retirer ni refaire sa double
    /// authentification — et au dixième, le compte deviendrait injoignable.
    /// </para>
    /// </remarks>
    public static async Task<IResult> DesactiverAsync(
        DemandeDeCode corps,
        IIdentiteDemandeur demandeur,
        UserManager<Utilisateur> utilisateurs,
        CancellationToken jeton
    )
    {
        ArgumentNullException.ThrowIfNull(corps);
        ArgumentNullException.ThrowIfNull(demandeur);
        ArgumentNullException.ThrowIfNull(utilisateurs);
        jeton.ThrowIfCancellationRequested();

        if (await CompteAsync(demandeur, utilisateurs).ConfigureAwait(false) is not { } utilisateur)
        {
            return Results.Unauthorized();
        }

        if (
            !await SecondFacteurValideAsync(utilisateurs, utilisateur, corps.Code)
                .ConfigureAwait(false)
        )
        {
            return Refus();
        }

        await utilisateurs.SetTwoFactorEnabledAsync(utilisateur, false).ConfigureAwait(false);

        // La clé est effacée avec. La laisser en place ferait qu'une
        // réactivation reprendrait une clé que l'ancien appareil connaît encore
        // — y compris un appareil qu'on vient justement de perdre.
        await utilisateurs.ResetAuthenticatorKeyAsync(utilisateur).ConfigureAwait(false);

        return Results.NoContent();
    }

    /// <summary>
    /// Un code d'authentificateur, ou un code de RÉCUPÉRATION.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Les deux, et dans cet ordre. Ne vérifier que le code d'authentificateur
    /// rendrait les codes de récupération décoratifs — ils ne serviraient
    /// jamais, c'est-à-dire jamais le jour où le téléphone est perdu, qui est
    /// le seul jour où ils comptent.
    /// </para>
    ///
    /// <para>
    /// Un code de récupération est CONSOMMÉ par cette vérification : Identity
    /// le retire de la liste. C'est voulu — un code de secours qui resterait
    /// valable ne serait qu'un second mot de passe, plus court.
    /// </para>
    ///
    /// <para>
    /// <b>Elle vit ici, et à un seul endroit.</b> Trois portes demandent la
    /// même chose — la connexion, la préparation d'une clé neuve et la
    /// désactivation — et « ce qui prouve le second facteur » est UNE
    /// connaissance. Deux copies divergeraient, et celle qu'on oublierait de
    /// corriger serait celle qui laisse passer.
    /// </para>
    /// </remarks>
    public static async Task<bool> SecondFacteurValideAsync(
        UserManager<Utilisateur> utilisateurs,
        Utilisateur utilisateur,
        string? code
    )
    {
        ArgumentNullException.ThrowIfNull(utilisateurs);

        if (await CodeValideAsync(utilisateurs, utilisateur, code).ConfigureAwait(false))
        {
            return true;
        }

        // Le code absent s'arrête ICI : `RedeemTwoFactorRecoveryCodeAsync` LÈVE
        // sur un code nul — le magasin d'Identity le refuse avant de regarder
        // quoi que ce soit. Un code absent doit rendre faux, comme un code faux,
        // jamais une erreur 500.
        if (string.IsNullOrWhiteSpace(code))
        {
            return false;
        }

        return await utilisateurs
            .RedeemTwoFactorRecoveryCodeAsync(utilisateur, code)
            .ConfigureAwait(false) is { Succeeded: true };
    }

    /// <summary>
    /// Vérifie un code d'authentificateur. Rend faux sur un code absent, mal
    /// formé, ou faux — jamais d'exception, jamais de journal.
    /// </summary>
    public static async Task<bool> CodeValideAsync(
        UserManager<Utilisateur> utilisateurs,
        Utilisateur utilisateur,
        string? code
    )
    {
        ArgumentNullException.ThrowIfNull(utilisateurs);

        if (string.IsNullOrWhiteSpace(code))
        {
            return false;
        }

        // Les espaces et tirets que les authentificateurs affichent pour la
        // lisibilité sont retirés : un utilisateur qui recopie « 123 456 »
        // verrait son code refusé sans comprendre.
        var nu = code.Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal);

        return await utilisateurs
            .VerifyTwoFactorTokenAsync(
                utilisateur,
                utilisateurs.Options.Tokens.AuthenticatorTokenProvider,
                nu
            )
            .ConfigureAwait(false);
    }

    /// <summary>L'URI <c>otpauth://</c>, tous paramètres explicites.</summary>
    public static string Uri(string? compte, string cle)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cle);

        var etiquette = System.Uri.EscapeDataString(
            string.IsNullOrWhiteSpace(compte) ? Emetteur : compte
        );
        var emetteur = System.Uri.EscapeDataString(Emetteur);

        return string.Create(
            CultureInfo.InvariantCulture,
            $"otpauth://totp/{emetteur}:{etiquette}"
                + $"?secret={System.Uri.EscapeDataString(cle)}"
                + $"&issuer={emetteur}"
                + $"&algorithm={Algorithme}"
                + $"&digits={Chiffres}"
                + $"&period={PeriodeEnSecondes}"
        );
    }

    /// <summary>La clé par groupes de quatre, pour la saisie à la main.</summary>
    public static string Grouper(string cle)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cle);

        var groupe = new StringBuilder(cle.Length + (cle.Length / 4));
        for (var i = 0; i < cle.Length; i += 4)
        {
            if (i > 0)
            {
                groupe.Append(' ');
            }

            groupe.Append(cle.AsSpan(i, Math.Min(4, cle.Length - i)));
        }

        return groupe.ToString();
    }

    private static async Task<Utilisateur?> CompteAsync(
        IIdentiteDemandeur demandeur,
        UserManager<Utilisateur> utilisateurs
    ) =>
        demandeur.Identifiant is { } identifiant
            ? await utilisateurs
                .FindByIdAsync(identifiant.ToString("D", CultureInfo.InvariantCulture))
                .ConfigureAwait(false)
            : null;

    private static IResult Refus() =>
        Results.Json(new Reponse("CodeInvalide"), statusCode: StatusCodes.Status400BadRequest);
}
