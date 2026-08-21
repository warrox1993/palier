using System.Net;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;

namespace Palier.Api.Auth;

/// <summary>
/// La limitation des tentatives, par adresse réelle.
/// </summary>
/// <remarks>
/// <para>
/// <b>Le piège a deux mâchoires, et refermer l'une ouvre l'autre.</b>
/// </para>
///
/// <list type="number">
///   <item>
///     <b>Sans traitement des en-têtes transférés</b>, l'adresse observée
///     derrière le proxy de l'hébergement (D15) est celle du proxy. Tout le
///     monde tombe dans le même seau : cinq tentatives ratées par n'importe qui
///     bloquent l'ensemble des utilisateurs.
///   </item>
///   <item>
///     <b>Avec un <c>X-Forwarded-For</c> cru sur parole</b>, la limite ne vaut
///     plus rien : l'attaquant change l'en-tête à chaque essai et obtient un
///     seau neuf à chaque fois. La protection existe, elle est simplement
///     inopérante — et rien ne le dit.
///   </item>
/// </list>
///
/// <para>
/// La sortie est de n'accorder confiance qu'aux proxys <b>déclarés</b>, et à
/// eux seuls. Aucun proxy déclaré signifie <b>aucun en-tête cru</b> : le défaut
/// est le refus, pas la confiance.
/// </para>
///
/// <para>
/// <b>Une réserve à ne pas perdre : le compteur vit en mémoire du processus.</b>
/// Sur plusieurs répliques, chacune tient le sien — la limite effective est
/// multipliée par leur nombre, et un redémarrage la remet à zéro. Ce n'est pas
/// un défaut de cette implémentation, c'est le contrat de
/// <c>AddRateLimiter</c>. Le jour où l'API tournera à plus d'une instance, ce
/// compteur devra passer par un magasin partagé, et cette limitation-ci ne sera
/// plus qu'un premier filtre.
/// </para>
/// </remarks>
internal static class Limitation
{
    /// <summary>Le nom de la politique, attaché aux routes sensibles.</summary>
    public const string Politique = "auth";

    /// <summary>
    /// La clé de configuration listant les proxys de confiance, séparés par des
    /// virgules. Absente, <b>aucun</b> en-tête transféré n'est cru.
    /// </summary>
    public const string CleDesProxys = "TRUSTED_PROXIES";

    /// <summary>Cinq tentatives — <c>docs/09-comptes.md</c> § 1.</summary>
    public static int TentativesParFenetre => 5;

    /// <summary>Quinze minutes — <c>docs/09-comptes.md</c> § 1.</summary>
    public static TimeSpan Fenetre => TimeSpan.FromMinutes(15);

    /// <summary>
    /// Le nombre de tranches de la fenêtre glissante.
    /// </summary>
    /// <remarks>
    /// Une fenêtre <i>fixe</i> de quinze minutes laisse passer dix tentatives à
    /// cheval sur deux fenêtres — cinq à la fin de l'une, cinq au début de la
    /// suivante. Trois tranches de cinq minutes ramènent ce débordement à ce
    /// qu'une seule tranche autorise.
    /// </remarks>
    public static int TranchesParFenetre => 3;

    /// <summary>
    /// La partition : une adresse, un seau. <c>RemoteIpAddress</c> a déjà été
    /// corrigée — ou pas — par l'intergiciel des en-têtes transférés.
    /// </summary>
    /// <remarks>
    /// L'adresse absente n'est pas ignorée mais rangée dans un seau nommé, et
    /// partagé : le cas se produit sur une connexion par tube nommé ou par
    /// socket Unix. Lui donner un passe-droit ouvrirait un chemin sans limite.
    /// </remarks>
    public static RateLimitPartition<string> Partition(HttpContext contexte)
    {
        ArgumentNullException.ThrowIfNull(contexte);

        return RateLimitPartition.GetSlidingWindowLimiter(
            Cle(contexte),
            _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = TentativesParFenetre,
                Window = Fenetre,
                SegmentsPerWindow = TranchesParFenetre,

                // Aucune file d'attente : une tentative en trop est REFUSÉE, pas
                // mise en attente. Une file transformerait la limitation en
                // ralentisseur, et un attaquant y gagnerait des connexions
                // maintenues ouvertes à nos frais.
                QueueLimit = 0,
                AutoReplenishment = true,
            }
        );
    }

    /// <summary>
    /// Les options des en-têtes transférés, lues dans la configuration.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Les listes par défaut sont VIDÉES.</b> ASP.NET Core y met la boucle
    /// locale — <c>::1</c> dans <c>KnownProxies</c> et <c>::1/128</c> dans
    /// <c>KnownIPNetworks</c> (<c>KnownNetworks</c> est déprécié depuis .NET 10,
    /// ASPDEPR005). Les laisser ferait croire tout
    /// <c>X-Forwarded-For</c> venu de la machine elle-même : un service
    /// co-localisé, un conteneur voisin, un tunnel SSH suffiraient à forger
    /// n'importe quelle adresse.
    /// </para>
    ///
    /// <para>
    /// <c>ForwardLimit</c> reste à <b>un</b> — le défaut, vérifié à la source le
    /// 21/08/2026. Seule la valeur la plus à droite est lue, c'est-à-dire celle
    /// que notre propre proxy a écrite. Tout ce qu'un client aurait mis à gauche
    /// est ignoré.
    /// </para>
    /// </remarks>
    public static ForwardedHeadersOptions EnTetesTransferes(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var options = new ForwardedHeadersOptions { ForwardLimit = 1 };

        options.KnownProxies.Clear();
        options.KnownIPNetworks.Clear();

        foreach (var declare in Declares(configuration[CleDesProxys]))
        {
            options.KnownProxies.Add(declare);
        }

        // ⚠ VIDER LES LISTES N'INTERDIT PAS LA CONFIANCE — ELLE LA REND TOTALE.
        //
        // `ForwardedHeadersMiddleware` calcule `checkKnownIps = KnownIPNetworks.Count
        // > 0 || KnownProxies.Count > 0`, et ne contrôle l'origine QUE si ce
        // drapeau est vrai. Listes vides, il croit n'importe quel
        // `X-Forwarded-For`, venu de n'importe où.
        //
        // C'est l'inverse exact de l'intuition, et c'est ce que l'épreuve
        // `Sans_proxy_declare_un_X_Forwarded_For_est_IGNORE` a attrapé le
        // 21/08/2026 : « Expected 192.0.2.44, Actual 203.0.113.9 » — l'adresse
        // était choisie par le client.
        //
        // Le seul refus sûr est donc de ne RIEN traiter du tout.
        options.ForwardedHeaders =
            options.KnownProxies.Count == 0
                ? ForwardedHeaders.None
                : ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

        return options;
    }

    /// <summary>Enregistre le limiteur et les options d'en-têtes.</summary>
    public static void Composer(WebApplicationBuilder constructeur)
    {
        ArgumentNullException.ThrowIfNull(constructeur);

        var enTetes = EnTetesTransferes(constructeur.Configuration);
        constructeur.Services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = enTetes.ForwardedHeaders;
            options.ForwardLimit = enTetes.ForwardLimit;
            options.KnownProxies.Clear();
            options.KnownIPNetworks.Clear();
            foreach (var proxy in enTetes.KnownProxies)
            {
                options.KnownProxies.Add(proxy);
            }
        });

        constructeur.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(Politique, Partition);
        });
    }

    /// <summary>
    /// Les adresses déclarées, ignorant ce qui n'est pas une adresse IP.
    /// </summary>
    /// <remarks>
    /// Une valeur illisible est écartée en silence <b>et c'est délibéré</b> :
    /// lever au démarrage sur une virgule en trop rendrait l'API injoignable,
    /// alors que le défaut — ne croire personne — est déjà le comportement sûr.
    /// L'inverse serait vrai si l'erreur ouvrait la confiance.
    /// </remarks>
    private static IEnumerable<IPAddress> Declares(string? liste)
    {
        if (string.IsNullOrWhiteSpace(liste))
        {
            yield break;
        }

        foreach (var morceau in liste.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (IPAddress.TryParse(morceau, out var adresse))
            {
                yield return adresse;
            }
        }
    }

    /// <summary>
    /// La clé de partition d'un contexte : l'adresse réelle du client.
    /// </summary>
    /// <remarks>
    /// Elle est ici et nulle part ailleurs. <see cref="Partition" /> l'appelle
    /// plutôt que de recalculer la même chose : deux copies d'une clé de
    /// partition divergeraient, et le jour où l'une changerait, la limitation
    /// compterait dans un seau pendant que l'épreuve en lirait un autre.
    /// </remarks>
    public static string Cle(HttpContext contexte)
    {
        ArgumentNullException.ThrowIfNull(contexte);

        return contexte.Connection.RemoteIpAddress?.ToString() ?? _sansAdresse;
    }

    private const string _sansAdresse = "sans-adresse";
}
