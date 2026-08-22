using Microsoft.Extensions.Configuration;

namespace Palier.Infrastructure.Coffre;

/// <summary>
/// Les cinq variables qui ouvrent le coffre — D59.
///
/// Elles ne peuvent pas y aller elles-mêmes : ce sont elles qui l'ouvrent. Un
/// secret restera donc toujours dans l'environnement du serveur, et le gain du
/// coffre n'est pas « zéro secret local » mais « un seul secret local au lieu
/// de huit, et celui-là ne donne accès qu'à sept actions ».
/// </summary>
public sealed record ReglagesDuCoffre(
    string Endpoint,
    string OkmsId,
    string CleId,
    string ClientId,
    string ClientSecret
)
{
    private static readonly string[] _requises =
    [
        "OKMS_ENDPOINT",
        "OKMS_ID",
        "OKMS_KEY_ID",
        "OKMS_CLIENT_ID",
        "OKMS_CLIENT_SECRET",
    ];

    /// <summary>
    /// La racine de l'API du domaine. La barre finale de l'adresse est retirée :
    /// sans cela la racine porterait un double <c>/</c>, et OKMS rendrait un 404
    /// dont le message ne dirait pas pourquoi.
    /// </summary>
    public string Racine => $"{Endpoint}/api/{OkmsId}/v1";

    /// <summary>
    /// Refuse en NOMMANT ce qui manque, sans jamais citer la valeur d'une autre
    /// variable. Le refus tombe au démarrage — pas à la première connexion.
    /// </summary>
    public static ReglagesDuCoffre Depuis(IConfiguration source)
    {
        ArgumentNullException.ThrowIfNull(source);

        // `IsNullOrWhiteSpace`, et non `is null` : une variable posée à la
        // chaîne vide est le cas le plus courant d'un fichier d'environnement
        // mal rempli, et le refus doit tomber ici plutôt que plus loin, ailleurs,
        // en disant autre chose.
        var absentes = _requises.Where(c => string.IsNullOrWhiteSpace(source[c])).ToArray();
        if (absentes.Length > 0)
        {
            throw new InvalidOperationException(
                $"Le coffre ne peut pas s'ouvrir : {string.Join(", ", absentes)} "
                    + "absente(s) de l'environnement. Voir back/.env.example."
            );
        }

        return new(
            source["OKMS_ENDPOINT"]!.TrimEnd('/'),
            source["OKMS_ID"]!,
            source["OKMS_KEY_ID"]!,
            source["OKMS_CLIENT_ID"]!,
            source["OKMS_CLIENT_SECRET"]!
        );
    }
}
