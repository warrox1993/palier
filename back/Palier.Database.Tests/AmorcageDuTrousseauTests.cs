using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Palier.Infrastructure;
using Palier.Infrastructure.Coffre;

namespace Palier.Database.Tests;

/// <summary>
/// Le chargement du trousseau au démarrage — étapes 6 et 7 de la séquence.
///
/// L'API NE CRÉE JAMAIS DE CLÉ. Une table vide est un refus de démarrer, pas
/// une invitation à s'auto-réparer : une API qui fabrique sa clé quand elle
/// n'en trouve pas en fabriquerait une CHAQUE FOIS qu'elle démarre contre une
/// base qu'elle ne lit pas — mauvaise chaîne, politique cassée, migration non
/// jouée. Elle démarrerait au vert, et tous les secrets chiffrés par la clé
/// précédente seraient devenus illisibles sans que rien ne le signale.
/// </summary>
[Collection(BaseFixture.Collection)]
public sealed class AmorcageDuTrousseauTests(BaseFixture baseDeDonnees)
{
    private const string _clairBase64 = "HcLxC2yurpdz4UFS8mNHVmxwNx6RaiLI5ultaBqfTJc=";

    // ================================================================
    // Épreuve 1 — une table vide refuse, en nommant la commande
    // ================================================================

    [Fact]
    public async Task Une_table_de_cles_VIDE_refuse_le_demarrage_en_nommant_la_commande()
    {
        await ViderLesClesAsync();
        using var messager = new MessagerFactice(Jeton());
        using var http = new HttpClient(messager);

        var faute = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Amorcage(http).ChargerAsync(CancellationToken.None)
        );

        // Deux assertions : le refus ET le geste qui le lève. Un message qui
        // dirait seulement « aucune clé » laisserait chercher.
        Assert.Contains("aucune clé de données", faute.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("poser-cle-de-donnees", faute.Message, StringComparison.Ordinal);
    }

    // ================================================================
    // Épreuve 2 — une enveloppe que le coffre refuse arrête tout
    // ================================================================

    [Fact]
    public async Task Une_enveloppe_que_le_coffre_REFUSE_arrete_le_demarrage()
    {
        await PoserAsync("eyJhbGciOiJkaXIi.secret.ne.doit.pas.fuir", DateTimeOffset.UtcNow);
        using var messager = new MessagerFactice(
            Jeton(),
            new HttpResponseMessage(HttpStatusCode.Forbidden)
        );
        using var http = new HttpClient(messager);

        var faute = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Amorcage(http).ChargerAsync(CancellationToken.None)
        );

        // L'enveloppe elle-même ne remonte JAMAIS dans le message : elle n'est
        // pas secrète, mais un journal qui la recopie apprend à la relire.
        Assert.DoesNotContain("eyJhbGciOiJkaXIi", faute.Message, StringComparison.Ordinal);
    }

    // ================================================================
    // Épreuve 3 — toutes les clés chargées, la plus récente courante
    // ================================================================

    [Fact]
    public async Task Toutes_les_cles_sont_chargees_et_la_PLUS_RECENTE_devient_courante()
    {
        await ViderLesClesAsync();
        var ancienne = await PoserAsync("enveloppe-ancienne", DateTimeOffset.UtcNow.AddDays(-3));
        var recente = await PoserAsync("enveloppe-recente", DateTimeOffset.UtcNow);

        using var messager = new MessagerFactice(Jeton(), Clair(), Clair());
        using var http = new HttpClient(messager);
        var porteur = new PorteurDeTrousseau();

        await Amorcage(http, porteur).ChargerAsync(CancellationToken.None);

        // La plus récente chiffre ; l'ancienne reste lisible. C'est ce qui rend
        // la rotation possible sans réécrire toute la table d'un coup.
        var chiffre = porteur.Trousseau.Chiffrer("x", Guid.Empty);
        Assert.True(porteur.Trousseau.EstAJour(chiffre));
        Assert.Contains(recente.ToString("N"), chiffre, StringComparison.Ordinal);
        Assert.DoesNotContain(ancienne.ToString("N"), chiffre, StringComparison.Ordinal);
    }

    // ================================================================
    // Épreuve 4 — le porteur refuse avant l'amorçage
    // ================================================================

    [Fact]
    public void Le_porteur_REFUSE_tant_que_l_amorcage_n_a_pas_eu_lieu()
    {
        // Sans ce refus, une erreur d'ordonnancement se manifesterait par un
        // déchiffrement silencieusement impossible, loin de sa cause.
        var faute = Assert.Throws<InvalidOperationException>(
            () => new PorteurDeTrousseau().Trousseau
        );

        Assert.Contains("AmorcageDuTrousseau", faute.Message, StringComparison.Ordinal);
    }


    // ================================================================
    // Épreuve 5 — un coffre LENT nomme quand même l'enveloppe
    // ================================================================

    [Fact]
    public async Task Un_coffre_qui_DEPASSE_le_delai_nomme_encore_l_enveloppe()
    {
        // Le délai de dix secondes existe pour ce cas précis, et depuis .NET 5
        // un dépassement de `HttpClient.Timeout` lève `TaskCanceledException` —
        // qui n'hérite pas de `HttpRequestException`. Sans la branche
        // correspondante, l'exception remontait brute : sur une base ayant subi
        // une rotation, l'exploitant ne saurait pas quelle enveloppe accuser.
        // La table est vidée d'abord : l'amorçage bute sur la PREMIÈRE
        // enveloppe, et sans cela l'épreuve accuserait celle qu'un autre cas
        // a laissée derrière lui.
        await ViderLesClesAsync();
        var id = await PoserAsync("enveloppe-lente", DateTimeOffset.UtcNow);
        using var messager = new MessagerQuiExpire();
        using var http = new HttpClient(messager);

        var faute = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Amorcage(http).ChargerAsync(CancellationToken.None)
        );

        Assert.Contains("enveloppe", faute.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(id.ToString("N"), faute.Message, StringComparison.Ordinal);
    }

    // ================================================================

    private AmorcageDuTrousseau Amorcage(HttpClient http, PorteurDeTrousseau? porteur = null) =>
        new(
            new FabriqueDeContexteDEpreuve(baseDeDonnees.ChaineApp),
            new ClientOkms(http, Reglages()),
            porteur ?? new PorteurDeTrousseau()
        );

    private static ReglagesDuCoffre Reglages() =>
        new("https://coffre.invalid", "domaine", "cle", "EU.client", "secret");

    private async Task<Guid> PoserAsync(string enveloppe, DateTimeOffset quand)
    {
        var id = Guid.NewGuid();
        await using var connexion = new NpgsqlConnection(baseDeDonnees.ChaineMigrations);
        await connexion.OpenAsync();
        await using var commande = new NpgsqlCommand(
            "insert into public.cles_de_donnees (id, enveloppe, creee_le) values ($1, $2, $3)",
            connexion
        );
        commande.Parameters.AddWithValue(id);
        commande.Parameters.AddWithValue(enveloppe);
        commande.Parameters.AddWithValue(quand);
        await commande.ExecuteNonQueryAsync();
        return id;
    }

    private async Task ViderLesClesAsync()
    {
        await using var connexion = new NpgsqlConnection(baseDeDonnees.ChaineMigrations);
        await connexion.OpenAsync();
        await using var commande = new NpgsqlCommand(
            "delete from public.cles_de_donnees",
            connexion
        );
        await commande.ExecuteNonQueryAsync();
    }

    private static HttpResponseMessage Jeton() =>
        Json("""{"access_token":"jeton","expires_in":3599}""");

    private static HttpResponseMessage Clair() => Json($$"""{"plaintext":"{{_clairBase64}}"}""");

    private static HttpResponseMessage Json(string corps) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(corps, Encoding.UTF8, "application/json"),
        };
}

/// <summary>
/// Une fabrique de contexte pour les épreuves. En exploitation, c'est le
/// conteneur qui la fournit — un service hébergé est singleton et ne peut donc
/// pas prendre un contexte, dont la durée de vie est celle d'une requête.
/// </summary>
internal sealed class FabriqueDeContexteDEpreuve(string chaine) : IDbContextFactory<PalierDbContext>
{
    public PalierDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<PalierDbContext>().UseNpgsql(chaine).Options);
}

/// <summary>
/// Il rend le jeton, puis se comporte comme un <c>HttpClient</c> dont le délai
/// expire — <c>TaskCanceledException</c>, et non <c>HttpRequestException</c>.
/// </summary>
internal sealed class MessagerQuiExpire : HttpMessageHandler
{
    private bool _premier = true;

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage requete,
        CancellationToken annulation
    )
    {
        if (_premier)
        {
            _premier = false;

            // Le jeton est fabriqué ICI et non passé au constructeur : le
            // gestionnaire dispose ce qu'il rend, et l'analyseur n'a plus
            // d'objet orphelin à signaler.
            return Task.FromResult(
                new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        """{"access_token":"jeton","expires_in":3599}""",
                        System.Text.Encoding.UTF8,
                        "application/json"
                    ),
                }
            );
        }

        return Task.FromException<HttpResponseMessage>(
            new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout")
        );
    }
}
