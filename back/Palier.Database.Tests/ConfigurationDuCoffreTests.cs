using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using Palier.Infrastructure.Coffre;

namespace Palier.Database.Tests;

/// <summary>
/// Le coffre comme SOURCE DE CONFIGURATION, et c'est tout le point du design :
/// <c>GetConnectionString("Palier")</c> continue de fonctionner exactement
/// comme avant, et aucune autre ligne du backend ne change. Le coffre devient
/// une source parmi d'autres, pas une dépendance qui se propage — ce qui rend
/// l'ensemble réversible : le retirer, c'est retirer une source.
/// </summary>
public sealed class ConfigurationDuCoffreTests
{
    private static readonly string[] _sixClefs =
    [
        "ConnectionStrings__Palier",
        "ConnectionStrings__PalierAuth",
        "ConnectionStrings__PalierMigrations",
        "ConnectionStrings__PalierSauvegarde",
        "JWT_SIGNING_KEY",
        "GOOGLE_OAUTH_CLIENT_SECRET",
    ];

    // ================================================================
    // Épreuve 1 — rien d'autre dans le backend ne change
    // ================================================================

    [Fact]
    public void Les_six_paires_du_coffre_alimentent_la_configuration()
    {
        using var messager = MessagerAvec(Toutes());
        using var http = new HttpClient(messager);

        var configuration = Construire(http);

        // L'assertion qui porte le design entier.
        Assert.Equal("Host=palier", configuration.GetConnectionString("Palier"));
        Assert.Equal("Host=auth", configuration.GetConnectionString("PalierAuth"));
        Assert.Equal(new string('k', 32), configuration["JWT_SIGNING_KEY"]);
    }

    // ================================================================
    // Épreuve 2 — une clé manquante refuse en la NOMMANT
    // ================================================================

    [Fact]
    public void Une_cle_manquante_refuse_en_la_NOMMANT_sans_divulguer_les_autres()
    {
        var incompletes = Toutes();
        incompletes.Remove("JWT_SIGNING_KEY");
        using var messager = MessagerAvec(incompletes);
        using var http = new HttpClient(messager);

        var faute = Assert.Throws<InvalidOperationException>(() => Construire(http));

        // Deux assertions. La seconde est celle qui protège : un message qui
        // recopierait le contenu du secret pour aider au diagnostic mettrait
        // les cinq autres valeurs dans le journal de démarrage.
        Assert.Contains("JWT_SIGNING_KEY", faute.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("Host=palier", faute.Message, StringComparison.Ordinal);
    }

    // ================================================================
    // Épreuve 3 — un chemin introuvable nomme le chemin
    // ================================================================

    [Fact]
    public void Un_chemin_introuvable_refuse_en_nommant_le_chemin()
    {
        using var messager = new MessagerFactice(
            Jeton(),
            new HttpResponseMessage(HttpStatusCode.NotFound)
        );
        using var http = new HttpClient(messager);

        var faute = Assert.Throws<InvalidOperationException>(() => Construire(http, "Production"));

        // OKMS rend 404 aussi bien pour « ce chemin n'existe pas » que pour
        // « vous n'y avez pas droit » — la sonde l'a vu le 22/08. Le message
        // doit donc nommer ce qu'on a demandé, sinon le diagnostic est aveugle.
        Assert.Contains("palier/prod", faute.Message, StringComparison.Ordinal);
    }

    // ================================================================
    // Épreuve 4 — l'environnement décide du chemin
    // ================================================================

    [Theory]
    [InlineData("Development", "palier/dev")]
    [InlineData("Production", "palier/prod")]
    public void L_environnement_decide_du_chemin_lu(string environnement, string chemin)
    {
        using var messager = MessagerAvec(Toutes());
        using var http = new HttpClient(messager);

        Construire(http, environnement);

        Assert.Contains(messager.Urls, u => u.Contains(chemin, StringComparison.Ordinal));
    }

    // ================================================================
    // Épreuve 5 — la borne : un environnement inconnu ne se devine pas
    // ================================================================

    [Fact]
    public void Un_environnement_INCONNU_refuse_plutot_que_de_choisir_un_chemin()
    {
        // Sans ce refus, une faute de frappe dans ASPNETCORE_ENVIRONMENT
        // ferait lire les secrets de développement à une instance de
        // production, ou l'inverse. Aucun des deux ne se signale.
        using var messager = MessagerAvec(Toutes());
        using var http = new HttpClient(messager);

        var faute = Assert.Throws<InvalidOperationException>(
            () => Construire(http, "Staging")
        );

        Assert.Contains("Staging", faute.Message, StringComparison.Ordinal);
    }

    // ================================================================

    private static IConfigurationRoot Construire(
        HttpClient http,
        string environnement = "Development"
    ) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["OKMS_ENDPOINT"] = "https://eu-west-rbx.okms.ovh.net",
                    ["OKMS_ID"] = "domaine",
                    ["OKMS_KEY_ID"] = "cle",
                    ["OKMS_CLIENT_ID"] = "EU.client",
                    ["OKMS_CLIENT_SECRET"] = "secret",
                }
            )
            .AjouterLeCoffre(environnement, reglages => new ClientOkms(http, reglages))
            .Build();

    private static Dictionary<string, string> Toutes() =>
        _sixClefs.ToDictionary(
            c => c,
            c =>
                c switch
                {
                    "ConnectionStrings__Palier" => "Host=palier",
                    "ConnectionStrings__PalierAuth" => "Host=auth",
                    "JWT_SIGNING_KEY" => new string('k', 32),
                    _ => "valeur",
                },
            StringComparer.Ordinal
        );

    private static MessagerFactice MessagerAvec(Dictionary<string, string> paires)
    {
        var corps = string.Join(
            ",",
            paires.Select(p => $"\"{p.Key}\":\"{p.Value}\"")
        );
        return new MessagerFactice(
            Jeton(),
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "{\"data\":{" + corps + "}}",
                    Encoding.UTF8,
                    "application/json"
                ),
            }
        );
    }

    private static HttpResponseMessage Jeton() =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """{"access_token":"jeton","expires_in":3599}""",
                Encoding.UTF8,
                "application/json"
            ),
        };
}
