using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using Palier.Infrastructure.Coffre;

namespace Palier.Database.Tests;

/// <summary>
/// Le client OKMS, éprouvé sans réseau par un gestionnaire de messages factice.
///
/// Ces épreuves figent ce qu'une SONDE a mesuré le 22/08/2026 contre le domaine
/// réel, et non ce qu'une documentation annonce — la documentation d'OVH ne dit
/// nulle part l'adresse du jeton, et les trois candidats évidents rendent une
/// page marketing en 404.
/// </summary>
public sealed class CoffreTests
{
    private const string _clientId = "EU.client-de-lepreuve";
    private const string _clientSecret = "secret-de-lepreuve";
    private const string _jeton = "jeton-de-lepreuve";

    /// <summary>Ce que la sonde a vu revenir de <c>datakey</c>, forme exacte.</summary>
    private const string _corpsDataKey = """
        {"key":"eyJhbGciOiJkaXIi.aa.bb.cc","plaintext":"HcLxC2yurpdz4UFS8mNHVmxwNx6RaiLI5ultaBqfTJc="}
        """;

    // ================================================================
    // Épreuve 1 — l'adresse du jeton, mesurée et non devinée
    // ================================================================

    [Fact]
    public async Task Le_jeton_est_demande_a_www_ovh_com_et_non_a_ovhcloud_com()
    {
        using var messager = new MessagerFactice(Jeton(), Json(_corpsDataKey));
        using var http = new HttpClient(messager);

        await Client(http).CreerUneCleDeDonneesAsync(CancellationToken.None);

        Assert.Equal("https://www.ovh.com/auth/oauth2/token", messager.Urls[0]);
    }

    // ================================================================
    // Épreuve 2 — chaque appel au coffre porte le jeton
    // ================================================================

    [Fact]
    public async Task Chaque_appel_au_coffre_porte_le_jeton()
    {
        using var messager = new MessagerFactice(Jeton(), Json(_corpsDataKey));
        using var http = new HttpClient(messager);

        await Client(http).CreerUneCleDeDonneesAsync(CancellationToken.None);

        Assert.Null(messager.Autorisations[0]); // la demande de jeton n'en porte pas
        Assert.Equal($"Bearer {_jeton}", messager.Autorisations[1]);
    }

    // ================================================================
    // Épreuve 3 — le jeton n'est demandé qu'UNE fois
    // ================================================================

    [Fact]
    public async Task Le_jeton_n_est_demande_qu_une_fois_pour_plusieurs_appels()
    {
        // La spec écarte tout renouvellement : le client meurt avec le
        // démarrage. Sans cette épreuve, un jeton redemandé à chaque appel
        // passerait inaperçu — trois allers-retours au lieu d'un, sur le
        // chemin de mise en service.
        using var messager = new MessagerFactice(
            Jeton(),
            Json(_corpsDataKey),
            Json("""{"plaintext":"HcLxC2yurpdz4UFS8mNHVmxwNx6RaiLI5ultaBqfTJc="}""")
        );
        using var http = new HttpClient(messager);
        var client = Client(http);

        await client.CreerUneCleDeDonneesAsync(CancellationToken.None);
        await client.DeballerAsync("eyJhbGciOiJkaXIi.aa.bb.cc", CancellationToken.None);

        Assert.Single(messager.Urls, u => u.Contains("oauth2", StringComparison.Ordinal));
    }

    // ================================================================
    // Épreuve 4 — un refus ne divulgue rien
    // ================================================================

    [Fact]
    public async Task Un_refus_du_coffre_ne_divulgue_ni_identifiant_ni_secret()
    {
        using var messager = new MessagerFactice(new HttpResponseMessage(HttpStatusCode.Unauthorized));
        using var http = new HttpClient(messager);

        var faute = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Client(http).CreerUneCleDeDonneesAsync(CancellationToken.None)
        );

        // Deux assertions, et la seconde est celle qui compte : un message
        // qui recopierait le corps de la réponse d'OVH y remettrait
        // l'identifiant du compte de service.
        Assert.Contains("coffre", faute.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(_clientId, faute.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(_clientSecret, faute.Message, StringComparison.Ordinal);
    }

    // ================================================================
    // Épreuve 5 — la forme de ce que `datakey` rend
    // ================================================================

    [Fact]
    public async Task Une_cle_de_donnees_rend_32_octets_et_son_enveloppe()
    {
        using var messager = new MessagerFactice(Jeton(), Json(_corpsDataKey));
        using var http = new HttpClient(messager);

        var (enveloppe, clair) = await Client(http)
            .CreerUneCleDeDonneesAsync(CancellationToken.None);

        Assert.Equal(32, clair.Length); // AES-256, mesuré
        Assert.StartsWith("eyJhbGciOiJkaXIi", enveloppe, StringComparison.Ordinal);
    }

    // ================================================================
    // Épreuve 6 — un secret rend ses paires
    // ================================================================

    [Fact]
    public async Task Un_secret_rend_toutes_ses_paires_d_un_seul_appel()
    {
        // OKMS est un moteur clé-valeur : un chemin porte plusieurs paires.
        // C'est ce qui permet de lire les six secrets en un aller-retour.
        using var messager = new MessagerFactice(
            Jeton(),
            Json("""{"data":{"JWT_SIGNING_KEY":"aaa","GOOGLE_OAUTH_CLIENT_SECRET":"bbb"}}""")
        );
        using var http = new HttpClient(messager);

        var paires = await Client(http).LireLeSecretAsync("palier/dev", CancellationToken.None);

        Assert.Equal(2, paires.Count);
        Assert.Equal("aaa", paires["JWT_SIGNING_KEY"]);
    }

    // ================================================================
    // Épreuves 7 à 11 — les cinq variables d'amorçage
    // ================================================================

    [Theory]
    [InlineData("OKMS_ENDPOINT")]
    [InlineData("OKMS_ID")]
    [InlineData("OKMS_KEY_ID")]
    [InlineData("OKMS_CLIENT_ID")]
    [InlineData("OKMS_CLIENT_SECRET")]
    public void Chaque_variable_d_amorcage_ABSENTE_refuse_en_la_NOMMANT(string absente)
    {
        var faute = Assert.Throws<InvalidOperationException>(
            () => ReglagesDuCoffre.Depuis(ConfigurationSans(absente))
        );

        Assert.Contains(absente, faute.Message, StringComparison.Ordinal);
        Assert.Contains(".env.example", faute.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Une_variable_VIDE_est_traitee_comme_absente()
    {
        // Une variable posée à la chaîne vide est le cas le plus courant d'un
        // fichier d'environnement mal rempli. `IsNullOrWhiteSpace`, et non
        // `is null` : sans cela, le refus tomberait plus tard, ailleurs, en
        // disant autre chose.
        var faute = Assert.Throws<InvalidOperationException>(
            () => ReglagesDuCoffre.Depuis(ConfigurationSans("OKMS_ID", "   "))
        );

        Assert.Contains("OKMS_ID", faute.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Les_cinq_variables_PRESENTES_construisent_les_reglages()
    {
        // La borne. Sans elle, un refus systématique rendrait les six épreuves
        // ci-dessus vertes sans rien démontrer.
        var reglages = ReglagesDuCoffre.Depuis(ConfigurationComplete());

        Assert.Equal("https://eu-west-rbx.okms.ovh.net/api/domaine/v1", reglages.Racine);
    }

    [Fact]
    public void La_barre_finale_de_l_adresse_est_retiree()
    {
        // Sans quoi la racine porterait un double `/`, et OKMS rendrait un 404
        // dont le message ne dirait pas pourquoi.
        var avecBarre = ConfigurationComplete();
        avecBarre["OKMS_ENDPOINT"] = "https://eu-west-rbx.okms.ovh.net/";

        Assert.Equal(
            "https://eu-west-rbx.okms.ovh.net/api/domaine/v1",
            ReglagesDuCoffre.Depuis(avecBarre).Racine
        );
    }

    // ================================================================

    private static ClientOkms Client(HttpClient http) =>
        new(http, ReglagesDuCoffre.Depuis(ConfigurationComplete()));

    private static HttpResponseMessage Jeton() =>
        Json($$"""{"access_token":"{{_jeton}}","expires_in":3599}""");

    private static HttpResponseMessage Json(string corps) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(corps, Encoding.UTF8, "application/json"),
        };

    private static IConfigurationRoot ConfigurationComplete() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["OKMS_ENDPOINT"] = "https://eu-west-rbx.okms.ovh.net",
                    ["OKMS_ID"] = "domaine",
                    ["OKMS_KEY_ID"] = "cle",
                    ["OKMS_CLIENT_ID"] = _clientId,
                    ["OKMS_CLIENT_SECRET"] = _clientSecret,
                }
            )
            .Build();

    private static IConfigurationRoot ConfigurationSans(string absente, string? valeur = null)
    {
        var configuration = ConfigurationComplete();
        configuration[absente] = valeur;
        return configuration;
    }
}

/// <summary>
/// Il rend les réponses qu'on lui donne, dans l'ordre, et retient ce qu'on lui
/// a demandé. Aucun réseau : le client se juge sur les requêtes qu'il forme.
/// </summary>
internal sealed class MessagerFactice(params HttpResponseMessage[] reponses) : HttpMessageHandler
{
    private int _rang;

    public List<string> Urls { get; } = [];

    public List<string?> Autorisations { get; } = [];

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage requete,
        CancellationToken jeton
    )
    {
        ArgumentNullException.ThrowIfNull(requete);
        Urls.Add(requete.RequestUri!.ToString());
        Autorisations.Add(requete.Headers.Authorization?.ToString());

        return Task.FromResult(reponses[Math.Min(_rang++, reponses.Length - 1)]);
    }
}
