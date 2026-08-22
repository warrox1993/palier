using Microsoft.Extensions.Configuration;

namespace Palier.Infrastructure.Coffre;

/// <summary>
/// Le coffre branché comme SOURCE de configuration — et c'est tout le design.
///
/// <c>GetConnectionString("Palier")</c> continue de fonctionner exactement
/// comme avant : aucune autre ligne du backend ne change. Le coffre devient une
/// source parmi d'autres, pas une dépendance qui se propage, et le retirer
/// consiste à retirer une source.
///
/// La source, le fournisseur et l'extension vivent dans le même fichier : ils
/// changent ensemble, toujours, et les séparer obligerait à lire trois fichiers
/// pour comprendre un chargement qui tient en vingt lignes.
/// </summary>
public static class ConfigurationDuCoffre
{
    /// <summary>
    /// Les secrets EXIGÉS au démarrage — exactement ceux que l'API lit, ni plus
    /// ni moins. Une liste blanche : ce qui n'y est pas ne vient pas du coffre,
    /// et ce qui y est manque bruyamment.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Elle en portait six, et c'était un faux garde-fou.</b> Y figuraient
    /// <c>GOOGLE_OAUTH_CLIENT_SECRET</c> — qu'aucune ligne du produit ne lit,
    /// l'exigence 1 de l'AIPD étant reportée — ainsi que les chaînes de
    /// migration et de sauvegarde, qui servent à <c>dotnet ef</c> et à
    /// <c>pg_dump</c>, jamais à l'API. Exiger au démarrage ce qu'on ne lit pas
    /// ne protège rien : cela empêche seulement de démarrer.
    /// </para>
    ///
    /// <para>
    /// Le chemin du coffre peut en porter davantage, et il le fera : les
    /// chaînes de migration et de sauvegarde y ont leur place, et les outils
    /// qui les emploient les y liront. Ce qui est exigé ici est ce qui manque à
    /// l'API pour servir une requête.
    /// </para>
    /// </remarks>
    public static readonly string[] ClefsAttendues =
    [
        "ConnectionStrings__Palier",
        "ConnectionStrings__PalierAuth",
        "JWT_SIGNING_KEY",
    ];

    /// <summary>
    /// Le chemin ne se devine pas. Un environnement inconnu refuse plutôt que
    /// de choisir : sans ce refus, une faute de frappe dans
    /// <c>ASPNETCORE_ENVIRONMENT</c> ferait lire les secrets de développement à
    /// une instance de production — ou l'inverse — et aucun des deux ne se
    /// signale.
    /// </summary>
    public static string CheminPour(string environnement) =>
        environnement switch
        {
            "Development" => "palier/dev",
            "Production" => "palier/prod",
            _ => throw new InvalidOperationException(
                $"Aucun chemin de coffre n'est défini pour l'environnement « {environnement} ». "
                    + "Les deux chemins connus sont `palier/dev` et `palier/prod`."
            ),
        };

    /// <summary>
    /// La fabrique est un point d'injection pour les épreuves : elles fournissent
    /// un client bâti sur un gestionnaire de messages factice. En exploitation,
    /// le défaut construit un vrai client, avec le délai maximal de dix secondes
    /// qui empêche un coffre LENT de suspendre le démarrage sans rien signaler.
    /// </summary>
    public static IConfigurationBuilder AjouterLeCoffre(
        this IConfigurationBuilder constructeur,
        string environnement,
        Func<ReglagesDuCoffre, ClientOkms>? fabrique = null
    )
    {
        ArgumentNullException.ThrowIfNull(constructeur);

        // L'amorçage se lit ICI, AVANT que notre source rejoigne la liste.
        //
        // Le faire dans `IConfigurationSource.Build` serait le réflexe, et
        // c'est une récursion infinie : `constructeur.Build()` construit
        // TOUTES les sources, la nôtre comprise, qui rappellerait
        // `constructeur.Build()`. Mesuré — la pile déborde et le processus de
        // test meurt sans qu'aucune épreuve ne soit rapportée.
        var amorcage = constructeur.Build();
        var client = (fabrique ?? ClientReel)(ReglagesDuCoffre.Depuis(amorcage));

        return constructeur.Add(
            new SourceDeConfigurationOkms(CheminPour(environnement), client)
        );
    }

    /// <summary>
    /// Le délai maximal de dix secondes empêche un coffre LENT de suspendre le
    /// démarrage sans rien signaler — un état pire qu'un refus, parce qu'il ne
    /// déclenche aucune alerte. Un coffre injoignable, lui, échoue en 91 ms.
    ///
    /// Le client est statique, et ce n'est pas une commodité : un
    /// <c>HttpClient</c> créé puis abandonné garde sa connexion ouverte le temps
    /// du <c>TIME_WAIT</c>, et la forme recommandée est un client unique pour la
    /// vie du processus. Ici il n'en sert qu'un, et seulement au démarrage.
    /// </summary>
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(10) };

    private static ClientOkms ClientReel(ReglagesDuCoffre reglages) => new(_http, reglages);
}

internal sealed class SourceDeConfigurationOkms(string chemin, ClientOkms client)
    : IConfigurationSource
{
    public IConfigurationProvider Build(IConfigurationBuilder constructeur) =>
        new FournisseurDeConfigurationOkms(chemin, client);
}

internal sealed class FournisseurDeConfigurationOkms(string chemin, ClientOkms client)
    : ConfigurationProvider
{
    public override void Load()
    {
        // `Load` n'est pas asynchrone, et c'est le démarrage : bloquer ici est
        // correct. Le port n'est pas encore ouvert, aucune requête n'attend.
        var paires = client.LireLeSecretAsync(chemin, CancellationToken.None).GetAwaiter().GetResult();

        var absentes = ConfigurationDuCoffre
            .ClefsAttendues.Where(c => !paires.ContainsKey(c) || string.IsNullOrWhiteSpace(paires[c]))
            .ToArray();

        if (absentes.Length > 0)
        {
            // Le NOM de ce qui manque, jamais la valeur de ce qui est là : un
            // message qui recopierait le secret pour aider au diagnostic
            // mettrait les cinq autres valeurs dans le journal de démarrage.
            throw new InvalidOperationException(
                $"Le chemin « {chemin} » du coffre ne porte pas : "
                    + $"{string.Join(", ", absentes)}."
            );
        }

        // Le double tiret bas est la convention .NET d'un chemin de
        // configuration : `ConnectionStrings__Palier` alimente
        // `GetConnectionString("Palier")`.
        Data = paires.ToDictionary(
            p => p.Key.Replace("__", ":", StringComparison.Ordinal),
            p => (string?)p.Value,
            StringComparer.OrdinalIgnoreCase
        );
    }
}
