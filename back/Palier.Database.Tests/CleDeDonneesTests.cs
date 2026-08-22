using Npgsql;

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
