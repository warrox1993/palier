using System.Net.Http.Json;
using System.Text.Json;

namespace Palier.Infrastructure.Coffre;

/// <summary>
/// Les trois appels dont le produit a besoin, écrits à la main — OVH ne publie
/// aucun SDK .NET, seulement une interface en ligne de commande et un SDK Go.
/// Pour trois appels, une dépendance de plus coûterait davantage que ce
/// fichier (doctrine des dépendances, `CLAUDE.md` § 4).
///
/// LE JETON N'EST JAMAIS RENOUVELÉ. Il vit 59 minutes — mesuré — et le client
/// meurt avec le démarrage : après le chargement du trousseau, l'API n'a plus
/// rien à demander au coffre. Rien à rafraîchir, aucune expiration à suivre.
///
/// Aucun corps de réponse ne remonte dans un message d'erreur : celui de la
/// demande de jeton porterait l'identifiant du compte de service.
/// </summary>
public sealed class ClientOkms(HttpClient http, ReglagesDuCoffre reglages)
{
    /// <summary>
    /// Mesurée le 22/08/2026, et introuvable dans la documentation d'OVH : les
    /// trois candidats en <c>ovhcloud.com</c> rendent une page marketing en 404.
    /// </summary>
    private const string _urlDuJeton = "https://www.ovh.com/auth/oauth2/token";

    private string? _jeton;

    /// <summary>Les paires d'un chemin du Secret Manager, en un seul appel.</summary>
    /// <remarks>
    /// <para>
    /// <b>L'URL et la forme du corps ont été MESURÉES le 22/08/2026</b>, contre
    /// le domaine réel, et toutes deux démentaient ce que la spécification
    /// laissait supposer. <c>/secret/{chemin}/data</c> rend un 404 ;
    /// <c>/secret/data/{chemin}</c> rend 200 — c'est la forme de HashiCorp
    /// Vault KV v2, dont OKMS reprend le moteur.
    /// </para>
    ///
    /// <para>
    /// Le corps est doublement imbriqué : <c>{ data: { data, metadata } }</c>.
    /// Lire le premier <c>data</c> seul rapporterait deux clés nommées
    /// « data » et « metadata », et l'API refuserait de démarrer en annonçant
    /// que les secrets manquent — alors qu'ils sont là.
    /// </para>
    /// </remarks>
    public async Task<IReadOnlyDictionary<string, string>> LireLeSecretAsync(
        string chemin,
        CancellationToken jeton
    )
    {
        using var requete = new HttpRequestMessage(
            HttpMethod.Get,
            $"{reglages.Racine}/secret/data/{chemin}"
        );
        using var reponse = await EnvoyerAsync(requete, $"la lecture de « {chemin} »", jeton)
            .ConfigureAwait(false);

        using var doc = await LireAsync(reponse, jeton).ConfigureAwait(false);
        return doc
            .RootElement.GetProperty("data")
            .GetProperty("data")
            .EnumerateObject()
            .ToDictionary(
                p => p.Name,
                p => p.Value.GetString() ?? string.Empty,
                StringComparer.Ordinal
            );
    }

    /// <summary>
    /// Une clé de données neuve : sa forme claire, qui ne quitte jamais la
    /// mémoire, et son enveloppe, qui seule va en base.
    /// </summary>
    public async Task<(string Enveloppe, byte[] Clair)> CreerUneCleDeDonneesAsync(
        CancellationToken jeton
    )
    {
        using var requete = new HttpRequestMessage(
            HttpMethod.Post,
            $"{reglages.Racine}/servicekey/{reglages.CleId}/datakey"
        )
        {
            Content = JsonContent.Create(new { size = 256 }),
        };
        using var reponse = await EnvoyerAsync(requete, "la création d'une clé de données", jeton)
            .ConfigureAwait(false);

        using var doc = await LireAsync(reponse, jeton).ConfigureAwait(false);
        return (
            doc.RootElement.GetProperty("key").GetString()!,
            Convert.FromBase64String(doc.RootElement.GetProperty("plaintext").GetString()!)
        );
    }

    /// <summary>Déballe une enveloppe. Mesuré : 30 ms de médiane.</summary>
    public async Task<byte[]> DeballerAsync(string enveloppe, CancellationToken jeton)
    {
        using var requete = new HttpRequestMessage(
            HttpMethod.Post,
            $"{reglages.Racine}/servicekey/{reglages.CleId}/datakey/decrypt"
        )
        {
            Content = JsonContent.Create(new { key = enveloppe }),
        };
        using var reponse = await EnvoyerAsync(requete, "le déballage d'une enveloppe", jeton)
            .ConfigureAwait(false);

        using var doc = await LireAsync(reponse, jeton).ConfigureAwait(false);
        return Convert.FromBase64String(doc.RootElement.GetProperty("plaintext").GetString()!);
    }

    private async Task<HttpResponseMessage> EnvoyerAsync(
        HttpRequestMessage requete,
        string quoi,
        CancellationToken jeton
    )
    {
        requete.Headers.Authorization = new("Bearer", await JetonAsync(jeton).ConfigureAwait(false));
        var reponse = await http.SendAsync(requete, jeton).ConfigureAwait(false);

        if (!reponse.IsSuccessStatusCode)
        {
            var code = (int)reponse.StatusCode;
            reponse.Dispose();

            // Le corps n'est PAS repris : OKMS y nomme les ressources, et un
            // 404 y distingue « le chemin n'existe pas » de « vous n'y avez pas
            // droit » — une distinction qui aide surtout celui qui cherche.
            throw new InvalidOperationException(
                $"Le coffre a refusé {quoi} (code {code})."
            );
        }

        return reponse;
    }

    private async Task<string> JetonAsync(CancellationToken jeton)
    {
        if (_jeton is not null)
        {
            return _jeton;
        }

        using var contenu = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = reglages.ClientId,
                ["client_secret"] = reglages.ClientSecret,
                ["scope"] = "all",
            }
        );
        using var reponse = await http.PostAsync(new Uri(_urlDuJeton), contenu, jeton)
            .ConfigureAwait(false);

        if (!reponse.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Le coffre a refusé l'authentification (code {(int)reponse.StatusCode}). "
                    + "Vérifier le compte de service et sa politique."
            );
        }

        using var doc = await LireAsync(reponse, jeton).ConfigureAwait(false);
        return _jeton = doc.RootElement.GetProperty("access_token").GetString()!;
    }

    private static async Task<JsonDocument> LireAsync(
        HttpResponseMessage reponse,
        CancellationToken jeton
    ) =>
        await JsonDocument
            .ParseAsync(
                await reponse.Content.ReadAsStreamAsync(jeton).ConfigureAwait(false),
                cancellationToken: jeton
            )
            .ConfigureAwait(false);
}
