using Npgsql;
using Palier.Api.Outils;
using Palier.Infrastructure.Coffre;

namespace Palier.Database.Tests;

/// <summary>
/// La table <c>cles_de_donnees</c> porte les enveloppes que le coffre rend —
/// des JWE compacts, illisibles sans le compte de service OKMS.
///
/// Elle prend la forme « référence publique » de <c>nutrient_refs</c> : lecture
/// pour <c>palier_app</c>, AUCUNE politique d'écriture. C'est ce qui donne au
/// design ses deux compromissions indépendantes — l'enveloppe en base ne vaut
/// rien sans le coffre, et le coffre ne vaut rien sans l'enveloppe.
///
/// Le SQL est écrit à la main, sans EF : aucune couche d'abstraction ne peut
/// alors être soupçonnée d'avoir filtré à la place des politiques.
/// </summary>
[Collection(BaseFixture.Collection)]
public sealed class CleDeDonneesTests(BaseFixture baseDeDonnees)
{
    private const string _enveloppeDeLEpreuve = "eyJhbGciOiJkaXIi.aa.bb.cc";

    // ================================================================
    // Épreuve 1 — l'API lit ce que l'exploitation a posé
    // ================================================================

    [Fact]
    public async Task palier_app_LIT_les_cles_de_donnees()
    {
        var id = await PoserUneEnveloppeAsync();

        await using var connexion = await OuvrirAsync(baseDeDonnees.ChaineApp);
        await using var commande = new NpgsqlCommand(
            "select id, enveloppe from public.cles_de_donnees where id = $1",
            connexion
        );
        commande.Parameters.AddWithValue(id);

        await using var lecteur = await commande.ExecuteReaderAsync();

        Assert.True(
            await lecteur.ReadAsync(),
            "La politique `lecture_publique` ne rend aucune ligne : sans elle, "
                + "l'API démarrerait en croyant qu'aucune clé n'est posée, et "
                + "referait le trousseau à chaque démarrage."
        );
        Assert.Equal(_enveloppeDeLEpreuve, lecteur.GetString(1));
    }

    // ================================================================
    // Épreuve 2 — et elle n'écrit jamais
    // ================================================================

    [Fact]
    public async Task palier_app_NE_PEUT_PAS_ecrire_une_cle_de_donnees()
    {
        var refus = await Assert.ThrowsAsync<PostgresException>(async () =>
        {
            await using var connexion = await OuvrirAsync(baseDeDonnees.ChaineApp);
            await using var commande = new NpgsqlCommand(
                "insert into public.cles_de_donnees (id, enveloppe, creee_le) "
                    + "values ($1, $2, now())",
                connexion
            );
            commande.Parameters.AddWithValue(Guid.NewGuid());
            commande.Parameters.AddWithValue("forgee");
            await commande.ExecuteNonQueryAsync();
        });

        // Deux assertions. Le code seul ne prouve rien : une colonne mal
        // nommée lèverait aussi, et l'épreuve passerait pour la mauvaise
        // raison — c'est le défaut que le lot 1 a payé cinq fois.
        Assert.Equal("42501", refus.SqlState);
        Assert.Contains("cles_de_donnees", refus.Message, StringComparison.Ordinal);
    }

    // ================================================================
    // Épreuve 3 — sans quoi l'API entière refuserait de démarrer
    // ================================================================

    [Fact]
    public async Task La_table_porte_RLS_activee_ET_forcee()
    {
        // `LecteurDeSocle` refuse le démarrage si UNE SEULE table de `public`
        // manque l'un des deux drapeaux. Cette épreuve dit laquelle, là où
        // l'assertion de démarrage dirait seulement « une table ».
        await using var connexion = await OuvrirAsync(baseDeDonnees.ChaineAdministrateur);
        await using var commande = new NpgsqlCommand(
            """
            select c.relrowsecurity, c.relforcerowsecurity
              from pg_class c join pg_namespace n on n.oid = c.relnamespace
             where n.nspname = 'public' and c.relkind = 'r'
               and c.relname = 'cles_de_donnees'
            """,
            connexion
        );

        await using var lecteur = await commande.ExecuteReaderAsync();

        Assert.True(
            await lecteur.ReadAsync(),
            "La table `cles_de_donnees` n'existe pas."
        );
        Assert.True(lecteur.GetBoolean(0), "RLS n'est pas ACTIVÉE sur `cles_de_donnees`.");
        Assert.True(
            lecteur.GetBoolean(1),
            "RLS n'est pas FORCÉE : le propriétaire la contournerait, et "
                + "`ddl-rowsecurity.html` est explicite — « Table owners "
                + "normally bypass row security as well »."
        );
    }

    // ================================================================
    // Épreuve 4 — la borne : sans enveloppe posée, l'épreuve 1 mentirait
    // ================================================================

    [Fact]
    public async Task Le_role_proprietaire_PEUT_poser_une_enveloppe()
    {
        // Quatrième question du franchissement. Si `palier_migrations` ne
        // pouvait pas écrire, l'épreuve 1 lirait une table toujours vide et
        // resterait verte sans rien démontrer.
        var id = await PoserUneEnveloppeAsync();

        await using var connexion = await OuvrirAsync(baseDeDonnees.ChaineMigrations);
        await using var commande = new NpgsqlCommand(
            "select count(*) from public.cles_de_donnees where id = $1",
            connexion
        );
        commande.Parameters.AddWithValue(id);

        Assert.Equal(
            1L,
            await commande.ExecuteScalarAsync()
        );
    }


    // ================================================================
    // Épreuve 5 — la commande d'exploitation pose la première clé
    // ================================================================

    [Fact]
    public async Task La_commande_pose_une_cle_SANS_demarrer_le_serveur()
    {
        await ViderAsync();
        using var messager = new MessagerFactice(Jeton(), DataKey("enveloppe-posee"));
        using var http = new HttpClient(messager);

        var code = await PoserUneCleDeDonnees.ExecuterAsync(
            baseDeDonnees.ChaineMigrations,
            new ClientOkms(http, ReglagesDeLEpreuve()),
            CancellationToken.None
        );

        Assert.Equal(0, code);
        Assert.Equal(1, await CompterAsync());
    }

    // ================================================================
    // Épreuve 6 — rejouée, elle AJOUTE : c'est la rotation
    // ================================================================

    [Fact]
    public async Task Rejouer_la_commande_AJOUTE_une_cle_sans_toucher_a_la_precedente()
    {
        // Sans cette propriété, la rotation effacerait les anciens secrets au
        // lieu de les laisser lisibles — et personne ne le verrait avant la
        // prochaine connexion à deux facteurs.
        await ViderAsync();
        await ExecuterAsync("premiere");
        await ExecuterAsync("seconde");

        await using var connexion = await OuvrirAsync(baseDeDonnees.ChaineApp);
        await using var commande = new NpgsqlCommand(
            "select enveloppe from public.cles_de_donnees order by creee_le",
            connexion
        );
        await using var lecteur = await commande.ExecuteReaderAsync();

        var enveloppes = new List<string>();
        while (await lecteur.ReadAsync())
        {
            enveloppes.Add(lecteur.GetString(0));
        }

        Assert.Equal(2, enveloppes.Count);
        Assert.Contains("premiere", enveloppes);
        Assert.Contains("seconde", enveloppes);
    }

    // ================================================================

    private async Task ExecuterAsync(string enveloppe)
    {
        using var messager = new MessagerFactice(Jeton(), DataKey(enveloppe));
        using var http = new HttpClient(messager);
        await PoserUneCleDeDonnees.ExecuterAsync(
            baseDeDonnees.ChaineMigrations,
            new ClientOkms(http, ReglagesDeLEpreuve()),
            CancellationToken.None
        );
    }

    private static ReglagesDuCoffre ReglagesDeLEpreuve() =>
        new("https://coffre.invalid", "domaine", "cle", "EU.client", "secret");

    private static HttpResponseMessage Jeton() =>
        Json("""{"access_token":"jeton","expires_in":3599}""");

    private static HttpResponseMessage DataKey(string enveloppe) =>
        Json(
            $$"""
            {"key":"{{enveloppe}}","plaintext":"HcLxC2yurpdz4UFS8mNHVmxwNx6RaiLI5ultaBqfTJc="}
            """
        );

    private static HttpResponseMessage Json(string corps) =>
        new(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent(corps, System.Text.Encoding.UTF8, "application/json"),
        };

    private async Task ViderAsync()
    {
        await using var connexion = await OuvrirAsync(baseDeDonnees.ChaineMigrations);
        await using var commande = new NpgsqlCommand(
            "delete from public.cles_de_donnees",
            connexion
        );
        await commande.ExecuteNonQueryAsync();
    }

    private async Task<long> CompterAsync()
    {
        await using var connexion = await OuvrirAsync(baseDeDonnees.ChaineApp);
        await using var commande = new NpgsqlCommand(
            "select count(*) from public.cles_de_donnees",
            connexion
        );
        return (long)(await commande.ExecuteScalarAsync())!;
    }

    // ================================================================

    private async Task<Guid> PoserUneEnveloppeAsync()
    {
        var id = Guid.NewGuid();
        await using var connexion = await OuvrirAsync(baseDeDonnees.ChaineMigrations);
        await using var commande = new NpgsqlCommand(
            "insert into public.cles_de_donnees (id, enveloppe, creee_le) values ($1, $2, now())",
            connexion
        );
        commande.Parameters.AddWithValue(id);
        commande.Parameters.AddWithValue(_enveloppeDeLEpreuve);
        await commande.ExecuteNonQueryAsync();
        return id;
    }

    private static async Task<NpgsqlConnection> OuvrirAsync(string chaine)
    {
        var connexion = new NpgsqlConnection(chaine);
        await connexion.OpenAsync();
        return connexion;
    }
}
