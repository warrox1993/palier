using System.Data.Common;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Palier.Api.Auth;
using Palier.Api.Entrainement;
using Palier.Api.Socle;
using Palier.Application.Pipeline;
using Palier.Infrastructure;
using Palier.Infrastructure.Coffre;
using Palier.Infrastructure.Courrier;
using Palier.Infrastructure.Entrainement;
using Palier.Infrastructure.Identite;
using Palier.Infrastructure.Pipeline;

namespace Palier.Api;

/// <summary>
/// La composition de l'application, sortie de <c>Program.cs</c> pour UNE raison
/// mesurable : l'épreuve
/// <c>La_configuration_de_PRODUCTION_n_active_pas_la_journalisation_sensible</c>
/// doit construire la configuration RÉELLE de production. Recopier ces lignes
/// dans l'épreuve reviendrait à éprouver la copie — le défaut que ce dépôt
/// ferme partout ailleurs par une lecture plutôt que par une discipline.
///
/// <c>Program.cs</c> n'appelle donc plus que ces deux méthodes.
/// </summary>
internal static class Composition
{
    /// <summary>Le nom de la chaîne du rôle RESTREINT — jamais celle des migrations.</summary>
    internal const string CleDeChaine = "Palier";

    /// <summary>La chaîne du rôle d'authentification — D38, lot 4.</summary>
    internal const string CleDeChaineAuth = "PalierAuth";

    /// <summary>Le chemin de la route de santé. <c>/api/v1</c> dès la PREMIÈRE route.</summary>
    internal const string CheminDeSante = "/api/v1/sante";

    /// <summary>
    /// La clé de signature des jetons d'accès. Nom PLAT, sans double tiret bas :
    /// c'est un secret unique, pas une branche de configuration.
    /// </summary>
    internal const string CleDeSignature = "JWT_SIGNING_KEY";

    // `LoggerMessage.Define` et non un appel direct : CA1848 refuse
    // `logger.LogError(...)` sur un chemin chaud, et un journal qui coûte finit
    // par être retiré.
    //
    // L'EXCEPTION N'EST PAS JOURNALISÉE, et c'est délibéré. Le message d'une
    // `PostgresException` peut porter l'instruction fautive, sur des tables
    // nommées `body_weight` ou `intake_entries` — `01-conformite.md` § 4 :
    // « Aucune donnée de santé dans les logs applicatifs ». Le TYPE suffit à
    // distinguer « le moteur est éteint » de « la requête a été refusée ».
    private static readonly Action<ILogger, string, Exception?> _baseInjoignable =
        LoggerMessage.Define<string>(
            LogLevel.Error,
            new EventId(2038, "SanteBaseInjoignable"),
            "La route de santé n'a pas pu joindre la base. Type d'échec : {Type}. "
                + "Ni l'instruction ni ses paramètres ne sont journalisés."
        );

    /// <summary>Enregistre les services. Appelée par <c>Program.cs</c> et par l'épreuve de production.</summary>
    public static void Composer(WebApplicationBuilder constructeur)
    {
        ArgumentNullException.ThrowIfNull(constructeur);

        // La chaîne de l'API est celle du rôle RESTREINT — `palier_app` — jamais
        // celle des migrations et jamais celle de l'administrateur. C'est la
        // condition sans laquelle RLS ne mord pas du tout : « Table owners
        // normally bypass row security as well ».
        //
        // Elle vient de la configuration, donc d'une variable d'environnement,
        // jamais d'un fichier du dépôt. Absente, on lève ICI, en nommant la clé.
        var chaine =
            constructeur.Configuration.GetConnectionString(CleDeChaine)
            ?? throw new InvalidOperationException(
                "ConnectionStrings__Palier est absente. L'API se connecte sous le rôle "
                    + "restreint `palier_app` ; voir back/.env.example et db/README.md § Appliquer."
            );

        // AUCUN `EnableSensitiveDataLogging`, dans AUCUN environnement. L'option
        // fait journaliser les VALEURS des paramètres — donc un poids, un apport,
        // un identifiant d'utilisateur — à chaque erreur SQL. Et le mécanisme
        // d'identité de D36 fabrique justement un chemin d'erreur SQL : tout
        // défaut d'identité devient une `PostgresException` portée par
        // l'événement `CommandError` d'EF Core.
        //
        // Un appel conditionné à `IsDevelopment()` est ce que rien n'attrape :
        // l'épreuve construit la configuration de PRODUCTION et assertionne
        // l'option à faux.
        constructeur.Services.AddDbContext<PalierDbContext>(options => options.UseNpgsql(chaine));

        // Le pipeline. `IExecuteurDeCasDUsage` est le SEUL chemin par lequel un
        // cas d'usage touche la base : il ouvre la transaction et y pose
        // l'identité.
        constructeur.Services.AddScoped<IExecuteurDeCasDUsage, ExecuteurDeCasDUsage>();

        // L'identité vient du jeton VÉRIFIÉ, et de nulle part ailleurs — D36.
        // `IdentiteDepuisJeton` lit `HttpContext.User`, que l'intergiciel n'a
        // peuplé qu'après avoir contrôlé signature, émetteur, audience et date.
        // Il remplace `DemandeurSansIdentite`, le bouchon du lot 2.
        constructeur.Services.AddHttpContextAccessor();
        constructeur.Services.AddScoped<IIdentiteDemandeur, IdentiteDepuisJeton>();

        // Le lecteur du socle et l'assertion de D37.
        // ---- L'authentification — lot 4 ---------------------------------
        //
        // Le contexte d'identité se connecte sous `palier_auth`, JAMAIS sous
        // `palier_app` : ce dernier n'a aucun privilège sur les tables d'identité
        // et le moteur le refuserait par un 42501. C'est le chemin que D38
        // laissait à concevoir.
        var chaineAuth =
            constructeur.Configuration.GetConnectionString(CleDeChaineAuth)
            ?? throw new InvalidOperationException(
                "ConnectionStrings__PalierAuth est absente. Le chemin d'authentification se "
                    + "connecte sous le rôle `palier_auth`, seul autorisé sur les tables "
                    + "d'identité — voir back/.env.example et db/amorcage/01-roles.sql."
            );
        constructeur.Services.AddDbContext<PalierAuthDbContext>(options =>
            options.UseNpgsql(chaineAuth)
        );
        constructeur.Services.AddScoped<MagasinDeSessions>();

        // Le verrouillage progressif — la fenêtre et l'escalade qu'Identity n'a
        // pas. La décision qu'il applique est pure, dans `Palier.Application`.
        constructeur.Services.AddScoped<GardienDeVerrouillage>();

        // Le validateur à deux étages est enregistré AVEC son client : l'étage
        // réseau est opportuniste, mais il a besoin d'un client géré par la
        // fabrique — un HttpClient construit à la main épuise les sockets.
        constructeur.Services.AddHttpClient<ValidateurDeMotDePasse>();

        constructeur
            .Services.AddIdentityCore<Utilisateur>()
            .AddEntityFrameworkStores<PalierAuthDbContext>()
            .AddPasswordValidator<ValidateurDeMotDePasse>()
            // Le gestionnaire du produit, et non celui d'Identity. Sans cette
            // ligne, les codes de récupération repartent EN CLAIR dans
            // `AspNetUserTokens.Value` : le magasin par défaut les colle bout à
            // bout et les écrit tels quels, sans qu'aucun `IPersonalDataProtector`
            // ne chiffre la colonne. Les épreuves de `DeuxFacteursTests` qui
            // lisent la table rougissent le jour où elle disparaît.
            .AddUserManager<GestionnaireDUtilisateurs>()
            // Sans cette ligne, `GetAuthenticatorKeyAsync` rend une clé VIDE et
            // `VerifyTwoFactorTokenAsync` refuse tout : la double
            // authentification afficherait un QR code qui n'enrôle rien.
            // `AddIdentityCore` n'enregistre aucun fournisseur de jetons.
            .AddDefaultTokenProviders();

        // Une heure, là où le fournisseur en applique VINGT-QUATRE par défaut —
        // docs/09-comptes.md § 1. Un jeton de réinitialisation de mot de passe
        // valable un jour reste utilisable longtemps après que son courriel a
        // été lu, transféré, ou retrouvé dans une boîte compromise.
        //
        // La réserve du document tient toujours : cette option est PARTAGÉE par
        // tous les jetons de ce fournisseur — confirmation d'adresse comprise.
        // Le jour où l'une des durées devra différer, il faudra un fournisseur
        // dédié.
        constructeur.Services.Configure<DataProtectionTokenProviderOptions>(options =>
            options.TokenLifespan = TimeSpan.FromHours(1)
        );

        // 210 000 itérations, là où Identity en applique 100 000 par défaut.
        //
        // C'est ce qu'OWASP recommande pour PBKDF2-HMAC-SHA512, la fonction que
        // cette version emploie réellement — vérifié en décodant le format du
        // haché, la documentation annonçant tantôt SHA-256, tantôt 10 000
        // itérations. Mesuré le 21/08/2026 sur seize cœurs : 56,7 ms contre
        // 111,2 ms, soit 54 ms de plus par connexion. Sur un vCPU mutualisé,
        // compter le double — d'où l'ordre des contrôles, la limitation venant
        // AVANT le hachage.
        //
        // Le marqueur de version en tête du haché rend l'opération sûre : les
        // hachés existants restent vérifiables et sont recalculés à la connexion
        // suivante.
        constructeur.Services.Configure<PasswordHasherOptions>(options =>
            options.IterationCount = 210_000
        );

        // docs/09-comptes.md § 1 : dix caractères, là où Identity en exige six.
        //
        // Et AUCUNE règle de composition, là où Identity en impose quatre par
        // défaut — chiffre, minuscule, majuscule, signe non alphanumérique.
        // NIST SP 800-63B révision 4 les interdit en toutes lettres :
        // « Verifiers and CSPs SHALL NOT impose other composition rules (e.g.,
        // requiring mixtures of different character types) for passwords. »
        //
        // Le motif n'est pas le confort. Ces règles produisent des mots de
        // passe PRÉVISIBLES : la majuscule tombe en tête, le chiffre à la fin,
        // le signe est un point d'exclamation — « P@ssw0rd » les satisfait
        // toutes les quatre et figure dans la liste embarquée du validateur.
        // Ce qui protège ici est la longueur et le refus des mots de passe
        // compromis, contrôlés l'un et l'autre.
        //
        // Mesuré le 21/08/2026 : avec les défauts, l'inscription avec
        // « brouette-hivernale-38-oscille » — vingt-neuf signes, absent de
        // toute fuite — rendait 400 au lieu de 202, faute de MAJUSCULE. Deux
        // épreuves gardent les deux bords : celui-là passe, neuf signes ne
        // passent pas.
        constructeur.Services.Configure<IdentityOptions>(options =>
        {
            options.Password.RequiredLength = 10;
            options.Password.RequireDigit = false;
            options.Password.RequireLowercase = false;
            options.Password.RequireUppercase = false;
            options.Password.RequireNonAlphanumeric = false;

            // L'unicité de l'ADRESSE, que le défaut d'Identity laisse à FAUX.
            //
            // Rien ne l'imposait tant que le nom d'utilisateur valait l'adresse
            // — mais c'était un accident, pas une garantie : `UserValidator` ne
            // contrôle l'unicité de l'email QUE si cette option est vraie.
            // Découverte en franchissant : retirer `DuplicateEmail` de la liste
            // des codes avalés laissait l'épreuve de l'énumération VERTE, parce
            // qu'Identity ne produisait jamais ce code.
            //
            // Sans elle, `FindByEmailAsync` — sur lequel repose la connexion —
            // choisirait arbitrairement l'un de deux comptes.
            options.User.RequireUniqueEmail = true;
        });

        // ---- Le jeton d'accès ------------------------------------------
        //
        // La clé vient de la configuration, donc d'une variable
        // d'environnement, jamais d'un fichier du dépôt. Absente ou trop
        // courte, on lève ICI — `JetonDAcces.Validation` contrôle la longueur —
        // et non à la première connexion, où le message de la bibliothèque ne
        // nommerait ni la variable ni la longueur attendue.
        var cleDeSignature =
            constructeur.Configuration[CleDeSignature]
            ?? throw new InvalidOperationException(
                "JWT_SIGNING_KEY est absente. Aucun jeton d'accès ne peut être signé ni "
                    + "vérifié ; voir back/.env.example. Engendrer une valeur d'au moins "
                    + "32 octets, propre à chaque environnement."
            );

        // Construits ICI, et non dans le rappel : `AddJwtBearer` DIFFÈRE son
        // délégué jusqu'à la résolution des options, c'est-à-dire jusqu'à la
        // première requête portant un jeton. Le contrôle de longueur y serait
        // tombé des heures après le démarrage, dans une réponse 500.
        //
        // Mesuré le 21/08/2026 : l'épreuve
        // `Une_JWT_SIGNING_KEY_TROP_COURTE_est_refusee_au_DEMARRAGE` a rougi
        // sur « No exception was thrown » tant que cet appel vivait dans le
        // rappel — alors que le commentaire au-dessus affirmait le contraire.
        var parametresDuJeton = JetonDAcces.Validation(cleDeSignature);

        constructeur
            .Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = parametresDuJeton;

                // `sub` reste `sub`. La transposition par défaut le renomme en
                // `ClaimTypes.NameIdentifier`, une URI de schéma SOAP — et le
                // code qui cherche la revendication qu'il a émise ne la trouve
                // plus. La correspondance devient alors une convention tacite
                // entre l'émission et la lecture.
                options.MapInboundClaims = false;

                // Le jeton ne voyage QUE dans l'en-tête `Authorization`. Le lire
                // dans la chaîne de requête le ferait écrire dans les journaux
                // du serveur frontal, l'historique du navigateur et le
                // `Referer` sortant.
                options.SaveToken = false;
            });

        // Les deux politiques nommées : la nutrition à deux verrous,
        // l'entraînement ouvert. `AddAuthorization` est appelé LÀ et une seule
        // fois — deux appels laisseraient croire que les politiques sont
        // enregistrées alors que le second écraserait les options du premier.
        PolitiquesDAutorisation.Composer(constructeur);

        // La limitation par adresse RÉELLE, et le traitement des en-têtes
        // transférés dont elle dépend. Les deux vont ensemble : l'une sans
        // l'autre est soit inopérante, soit un seau unique pour tout le monde.
        Limitation.Composer(constructeur);

        // L'horloge vient du conteneur. `DateTimeOffset.UtcNow` écrit en dur
        // rendrait toute épreuve d'expiration dépendante de l'heure de la
        // machine — trois épreuves du jeton l'ont déjà payé.
        // Le porteur du trousseau — D59. Il est SINGLETON et vide au démarrage :
        // `AmorcageDuTrousseau` le remplit avant que le port s'ouvre. Y mettre
        // le trousseau lui-même obligerait le conteneur à parler au coffre
        // pendant une requête d'utilisateur, ce que la conception écarte.
        // Google — exigence 1, lot 4b. AJOUTÉ SEULEMENT S'IL EST CONFIGURÉ :
        // sans identifiants, `AddGoogle` lève au premier défi avec un message
        // qui ne dit rien à qui n'a pas ouvert de compte Google. Un
        // développement local sans Google reste ainsi parfaitement utilisable.
        if (Auth.Google.EstConfigure(constructeur.Configuration))
        {
            constructeur
                .Services.AddAuthentication()
                .AddGoogle(options =>
                {
                    options.ClientId = constructeur.Configuration[Auth.Google.CleDeLIdentifiant]!;
                    options.ClientSecret = constructeur.Configuration[Auth.Google.CleDuSecret]!;
                    options.CallbackPath = "/api/v1/auth/google/retour";

                    // Les trois scopes, et RIEN d'autre : `09-comptes.md` § 1
                    // l'interdit explicitement. `AddGoogle` en pose déjà
                    // certains ; on repart d'une liste vide pour que celle-ci
                    // fasse foi.
                    options.Scope.Clear();
                    foreach (var scope in Auth.Google.Scopes)
                    {
                        options.Scope.Add(scope);
                    }

                    // Google rend `email_verified` ; sans cette ligne, la
                    // revendication n'atteint pas le principal et le produit
                    // croirait chaque adresse non vérifiée.
                    options.ClaimActions.MapJsonKey(
                        Auth.Google.RevendicationEmailVerifie,
                        Auth.Google.RevendicationEmailVerifie
                    );
                });
        }

        constructeur.Services.AddSingleton<PorteurDeTrousseau>();

        // Le courrier — D60. Les réglages sont construits ICI, et non
        // paresseusement : un produit qui démarre sans pouvoir envoyer d'email
        // laisse ses utilisateurs bloqués à l'inscription, et rien ne le
        // signale avant la première plainte. Le refus tombe au démarrage.
        constructeur.Services.AddSingleton(
            ReglagesDuCourrier.Depuis(constructeur.Configuration)
        );
        constructeur.Services.AddSingleton<EnvoyeurSmtp>();
        constructeur.Services.AddSingleton<IEmailSender<Utilisateur>, EnvoiDeCourriel>();

        // Les trois services du coffre. Tous PARESSEUX : le conteneur ne les
        // construit qu'à la première résolution, et seul `Program` la demande.
        // Les épreuves composent donc l'application réelle sans jamais parler
        // au coffre — et sans que ces lignes soient une branche morte, puisque
        // le démarrage réel les emprunte toutes.
        constructeur.Services.AddSingleton(fournisseur =>
            ReglagesDuCoffre.Depuis(
                fournisseur.GetRequiredService<IConfiguration>()
            )
        );

        constructeur.Services.AddSingleton(fournisseur => new ClientOkms(
            new HttpClient { Timeout = TimeSpan.FromSeconds(10) },
            fournisseur.GetRequiredService<ReglagesDuCoffre>()
        ));

        // Une fabrique explicite plutôt que `AddDbContextFactory` : celui-ci
        // entre en conflit avec le `AddDbContext` déjà posé plus haut sur la
        // durée de vie de `DbContextOptions<PalierDbContext>`. Trois lignes
        // valent mieux qu'un réglage dont l'effet se découvre au démarrage.
        constructeur.Services.AddSingleton<IDbContextFactory<PalierDbContext>>(
            _ => new FabriqueDeContextePalier(
                new DbContextOptionsBuilder<PalierDbContext>().UseNpgsql(chaine).Options
            )
        );

        constructeur.Services.AddSingleton<AmorcageDuTrousseau>();

        constructeur.Services.AddSingleton(TimeProvider.System);

        // Le SEUL objet qui détient la clé. Elle n'est relue nulle part
        // ailleurs : un secret qui circule dans chaque point d'entrée finit par
        // être journalisé par l'un d'eux.
        constructeur.Services.AddSingleton(fournisseur => new SignataireDeJetons(
            cleDeSignature,
            fournisseur.GetRequiredService<TimeProvider>()
        ));

        // ---- L'entraînement — lot 5 -------------------------------------
        //
        // SCOPED, comme le contexte qu'ils prennent. Un gestionnaire singleton
        // capturerait un `PalierDbContext` de la première requête et le
        // partagerait entre tous les utilisateurs : le contexte porte l'état de
        // suivi des entités, et l'identité posée par le pipeline vit dans la
        // transaction de SA connexion.
        constructeur.Services.AddScoped<OuvrirUneSeance>();
        constructeur.Services.AddScoped<LireUneSeance>();
        constructeur.Services.AddScoped<ListerLesSeances>();
        constructeur.Services.AddScoped<CloturerUneSeance>();
        constructeur.Services.AddScoped<SupprimerUneSeance>();
        constructeur.Services.AddScoped<AjouterUneSerie>();
        constructeur.Services.AddScoped<RetirerUneSerie>();
        constructeur.Services.AddScoped<EnregistrerLePoids>();
        constructeur.Services.AddScoped<ListerLePoids>();

        constructeur.Services.AddScoped<LecteurDeSocle>();
        constructeur.Services.AddScoped<AssertionDIsolation>();
        constructeur.Services.AddHostedService<AssertionAuDemarrage>();
    }

    /// <summary>Attache les routes. Une seule à ce lot.</summary>
    public static void Router(WebApplication application)
    {
        ArgumentNullException.ThrowIfNull(application);

        // `/api/v1` DÈS LA PREMIÈRE ROUTE : `14-contenu.md` § 8 décrit le
        // versionnement rétroactif comme « un problème insoluble », et le poser
        // maintenant coûte zéro.
        //
        // Handler ÉCRIT À LA MAIN : pas de CQRS, pas de `Mediator.SourceGenerator`
        // — c'est le repli que D12 prévoit explicitement, « une cinquantaine de
        // lignes, zéro dépendance ». Conséquence directe : l'exception de licence
        // que D13 renvoyait au lot 2 n'est PAS due, puisque le paquet n'est pas
        // installé.
        // L'ORDRE COMPTE, et il est celui-ci.
        //
        // `UseForwardedHeaders` vient EN PREMIER : il corrige `RemoteIpAddress`,
        // dont la limitation se sert pour partitionner. Placé après, la
        // limitation compterait l'adresse du proxy — donc tout le monde dans le
        // même seau, et cinq échecs de n'importe qui bloqueraient l'ensemble
        // des utilisateurs.
        application.UseForwardedHeaders();
        application.UseRateLimiter();

        // Puis authentifier, puis autoriser, puis servir. Inversé,
        // l'autorisation s'exécuterait sur un principal encore anonyme et
        // refuserait tout.
        application.UseAuthentication();
        application.UseAuthorization();

        // D41 : « au lot 4 il passe derrière l'authentification et un rôle
        // d'administration ». Le lot 4 est livré et la route était restée
        // anonyme — un audit de sécurité l'a relevé.
        //
        // CE QU'ELLE LIVRAIT À UN APPELANT ANONYME : l'identifiant EXACT de la
        // dernière migration appliquée. Rapproché de l'historique public de ce
        // dépôt, il dit précisément quelles migrations — donc quelles
        // politiques RLS et quelles fonctions d'authentification — l'instance
        // qui tourne possède ou non, avant qu'un attaquant ne choisisse son
        // angle. Plus une sonde gratuite et illimitée de l'accessibilité de la
        // base.
        //
        // D41 EST FERMÉE — lot 4b. Le rôle manquait : `PolitiquesDAutorisation`
        // n'en portait que deux, qui sont des DOMAINES. La liste des
        // administrateurs vit au coffre, par adresse, et une adresse non
        // vérifiée n'ouvre rien — sans quoi s'inscrire avec l'adresse d'un
        // administrateur suffirait à en devenir un.
        application
            .MapGet(CheminDeSante, RepondreAsync)
            .RequireAuthorization(PolitiquesDAutorisation.Administration);

        PointsDEntree.Router(application);
        RoutesDEntrainement.Router(application);
    }

    private static async Task<IResult> RepondreAsync(
        LecteurDeSocle lecteur,
        ILoggerFactory fabrique,
        CancellationToken jeton
    )
    {
        try
        {
            var etat = await lecteur.LireAsync(jeton).ConfigureAwait(false);
            return Results.Ok(etat);
        }
        catch (DbException echec)
        {
            // 503 et non 500 : « la base ne répond pas » est un état de service,
            // pas un défaut du code. C'est aussi ce que l'écran d'état du lot 2
            // provoque pour de vrai, par `npm run db:down`.
            _baseInjoignable(fabrique.CreateLogger(typeof(Composition)), echec.GetType().Name, null);
            return Results.Json(
                new EtatDuSocle(string.Empty, false, 0),
                statusCode: StatusCodes.Status503ServiceUnavailable
            );
        }
    }
}
