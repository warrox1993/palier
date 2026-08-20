using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Palier.Api.Socle;
using Palier.Application.Pipeline;
using Palier.Infrastructure;
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

    /// <summary>Le chemin de la route de santé. <c>/api/v1</c> dès la PREMIÈRE route.</summary>
    internal const string CheminDeSante = "/api/v1/sante";

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

        // Tant que l'authentification n'existe pas (lot 4), le demandeur ne rend
        // jamais d'identité — et tout cas d'usage échoue donc bruyamment, en se
        // nommant.
        constructeur.Services.AddScoped<IIdentiteDemandeur, DemandeurSansIdentite>();

        // Le lecteur du socle et l'assertion de D37.
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
        application.MapGet(CheminDeSante, RepondreAsync);
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
