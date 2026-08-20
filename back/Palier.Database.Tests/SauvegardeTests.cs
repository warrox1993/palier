using Npgsql;

namespace Palier.Database.Tests;

/// <summary>
/// LE PRIX DE <c>FORCE ROW LEVEL SECURITY</c>, MESURÉ ET NON SUPPOSÉ — D37.
///
/// Sous <c>FORCE</c>, le propriétaire cesse de contourner RLS. <c>pg_dump</c>
/// pose <c>row_security = off</c>, et « If the user does not have sufficient
/// privileges to bypass row security, then an error is thrown »
/// (<c>app-pgdump.html</c>). La sauvegarde devient donc un LIVRABLE ÉPROUVÉ du
/// lot 2, et non une promesse trimestrielle qui rencontrerait ce mur au pire
/// moment — `docs/14-contenu.md` § 7 : « Une sauvegarde jamais restaurée n'est
/// pas une sauvegarde ».
///
/// Les commandes tournent SOUS LES RÔLES DU PRODUIT. Le compte d'administration
/// ne sert qu'à créer la base de destination et à provoquer les violations —
/// une épreuve lancée sous le superutilisateur serait verte chez nous et rouge
/// en production.
/// </summary>
[Collection(BaseFixture.Collection)]
public sealed class SauvegardeTests(BaseFixture baseDeDonnees)
{
    [Fact]
    public async Task Les_outils_de_sauvegarde_existent_dans_l_image()
    {
        // Quatrième question du franchissement. Sans elle, une image sans
        // `pg_dump` ferait échouer les épreuves suivantes avec « command not
        // found » — un échec qui ressemble à s'y méprendre au refus qu'elles
        // cherchent à démontrer.
        var dump = await baseDeDonnees.ExecuterDansLeConteneurAsync("pg_dump", "--version");
        Assert.Equal(0, dump.ExitCode);
        Assert.Contains("pg_dump", dump.Stdout, StringComparison.Ordinal);

        var restore = await baseDeDonnees.ExecuterDansLeConteneurAsync("pg_restore", "--version");
        Assert.Equal(0, restore.ExitCode);
        Assert.Contains("pg_restore", restore.Stdout, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Le_role_de_sauvegarde_a_SELECT_sur_TOUTE_table_de_public()
    {
        // BYPASSRLS contourne les POLITIQUES, jamais les PRIVILÈGES. Mesuré le
        // 20/08/2026 : avec BYPASSRLS et sans SELECT, `pg_dump` sort en
        // « permission denied for table __EFMigrationsHistory ».
        //
        // Cette épreuve refuse le dépôt le jour où une migration ajoutera une
        // table sans l'accorder — un défaut qui, sinon, ne se découvrirait qu'à
        // la sauvegarde suivante.
        var sansSelect = await ListerAsync(
            baseDeDonnees.ChaineMigrations,
            """
            select c.relname from pg_class c join pg_namespace n on n.oid = c.relnamespace
             where n.nspname = 'public' and c.relkind = 'r'
               and not has_table_privilege('palier_sauvegarde', c.oid, 'select')
             order by c.relname
            """
        );

        var total = await ListerAsync(
            baseDeDonnees.ChaineMigrations,
            """
            select c.relname from pg_class c join pg_namespace n on n.oid = c.relnamespace
             where n.nspname = 'public' and c.relkind = 'r'
            """
        );

        Assert.True(total.Count >= 13, $"Le catalogue ne rend que {total.Count} table(s).");
        Assert.True(
            sansSelect.Count == 0,
            "`palier_sauvegarde` n'a pas SELECT sur ces tables : "
                + string.Join(", ", sansSelect)
                + ". `pg_dump` échouera sur « permission denied for table », et le fichier "
                + "produit sera tronqué sans que rien d'autre ne le signale."
        );
    }

    [Fact]
    public async Task ÉPREUVE_INVERSÉE_sous_le_role_des_migrations_pg_dump_ECHOUE_a_cause_de_FORCE()
    {
        // ÉPREUVE INVERSÉE, ET C'EST VOULU — annotée pour que personne ne la
        // « corrige » en la rendant verte. Elle prouve que la contrainte
        // DOCUMENTÉE est RÉELLE sur cette version du moteur, au lieu de la
        // croire : une contrainte citée sans avoir été provoquée est une
        // citation, pas une mesure.
        var chemin = $"/tmp/inverse_{Guid.NewGuid():N}.dump";
        var resultat = await ShAsync(
            $"pg_dump '{baseDeDonnees.ConnexionInterne("palier_migrations")}' -F c -f {chemin}"
        );

        // Deux assertions, D19 : l'échec, ET un motif propre à la cause. Un
        // `pg_dump` qui échouerait sur un mauvais port ou un rôle inexistant
        // passerait la première sans rien prouver.
        Assert.NotEqual(0, resultat.ExitCode);
        Assert.Contains("row-level security", resultat.Stderr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Sous_le_role_de_sauvegarde_le_dump_et_la_restauration_rendent_le_MEME_nombre_de_lignes()
    {
        var marqueur = $"sonde_{Guid.NewGuid():N}"[..24];
        var cible = $"restauree_{Guid.NewGuid():N}";
        var chemin = $"/tmp/sauvegarde_{Guid.NewGuid():N}.dump";

        await PoserDesLignesAsync(marqueur);
        try
        {
            var source = await CompterAsync(baseDeDonnees.ChaineMigrations);

            // (1) LE DUMP, sous `palier_sauvegarde`.
            var dump = await ShAsync(
                $"pg_dump '{baseDeDonnees.ConnexionInterne("palier_sauvegarde")}' -F c -f {chemin}"
            );
            Assert.True(
                dump.ExitCode == 0,
                $"`pg_dump` sous `palier_sauvegarde` a échoué :\n{dump.Stderr}"
            );

            // (2) LA BASE DE DESTINATION, neuve. Le compte d'administration ne
            // sert qu'à cela : créer la base et y donner à `palier_migrations`
            // les privilèges que `db/amorcage/01-roles.sql` lui donne sur la
            // base du produit.
            await AdministrerAsync(
                $"""create database "{cible}" owner "{baseDeDonnees.Administrateur}";"""
            );
            await AdministrerAsync($"""grant create on database "{cible}" to palier_migrations;""");
            await AdministrerAsync(
                "grant create, usage on schema public to palier_migrations;",
                cible
            );

            try
            {
                // (3) LA RESTAURATION, sous `palier_migrations` — le rôle
                // PROPRIÉTAIRE. `palier_sauvegarde` sait lire, il ne sait pas
                // créer : la lecture et l'écriture d'une restauration sont deux
                // rôles distincts, et c'est écrit dans `db/README.md`.
                var restore = await ShAsync(
                    $"pg_restore -d '{baseDeDonnees.ConnexionInterne("palier_migrations", cible)}' {chemin}"
                );
                Assert.True(
                    restore.ExitCode == 0,
                    $"`pg_restore` a échoué :\n{restore.Stderr}"
                );

                // (4) ON COMPTE. Le code de sortie ne suffit pas : un dump VIDE
                // se restaure parfaitement, en code 0, et ne contient rien.
                var restauree = await CompterAsync(
                    Remplacer(baseDeDonnees.ChaineMigrations, cible)
                );

                Assert.True(
                    source.Lignes > 0,
                    "La base source ne porte aucune ligne de `nutrient_refs` : la comparaison "
                        + "serait 0 = 0, et l'épreuve verte sans rien démontrer."
                );
                Assert.Equal(source.Lignes, restauree.Lignes);
                Assert.Equal(source.Tables, restauree.Tables);
                Assert.Equal(source.Politiques, restauree.Politiques);

                // (5) ET L'ISOLATION SURVIT À LA RESTAURATION. Une base restaurée
                // sans `FORCE` serait une base ouverte, restaurée en code 0.
                Assert.Equal(0, restauree.SansForce);
            }
            finally
            {
                await AdministrerAsync($"""drop database if exists "{cible}" with (force);""");
            }
        }
        finally
        {
            await RetirerLesLignesAsync(marqueur);
        }
    }

    [Fact]
    public async Task VERDICT_sans_BYPASSRLS_le_meme_dump_ECHOUE_en_laissant_un_fichier_PRESQUE_COMPLET()
    {
        // LA MESURE QUI TRANCHE D37, DANS LES DEUX SENS. L'épreuve ci-dessus
        // montre que le dump réussit AVEC `BYPASSRLS` ; celle-ci retire
        // l'attribut et montre que le même dump échoue. Sans elle, on ne saurait
        // pas si l'attribut sert à quelque chose — et un privilège dont on ne
        // sait pas s'il sert est un privilège de trop.
        //
        // Le second point est le plus important : le fichier produit N'EST PAS
        // VIDE. `pg_dump` écrit le schéma, puis butte sur la première table dont
        // le COPY est refusé. Sur le disque, il reste un fichier de taille
        // presque normale. SEUL LE CODE DE SORTIE dit qu'il ne vaut rien.
        var chemin = $"/tmp/verdict_{Guid.NewGuid():N}.dump";
        await AdministrerAsync("alter role palier_sauvegarde nobypassrls;");
        try
        {
            var dump = await ShAsync(
                $"pg_dump '{baseDeDonnees.ConnexionInterne("palier_sauvegarde")}' -F c -f {chemin}"
            );

            Assert.NotEqual(0, dump.ExitCode);
            Assert.Contains("row-level security", dump.Stderr, StringComparison.Ordinal);

            var taille = await ShAsync($"stat -c %s {chemin}");
            Assert.Equal(0, taille.ExitCode);
            Assert.True(
                int.Parse(taille.Stdout.Trim(), System.Globalization.CultureInfo.InvariantCulture)
                    > 1000,
                "Le fichier tronqué fait moins de 1 000 octets : le piège que cette épreuve "
                    + "documente — « un dump raté ressemble à un dump » — n'existerait pas, et "
                    + "il faudrait réécrire ce qui est dit dans `db/README.md`."
            );
        }
        finally
        {
            await AdministrerAsync("alter role palier_sauvegarde bypassrls;");
        }
    }

    private Task<DotNet.Testcontainers.Containers.ExecResult> ShAsync(string commande) =>
        baseDeDonnees.ExecuterDansLeConteneurAsync("/bin/sh", "-c", commande);

    private static async Task<(long Lignes, long Tables, long Politiques, long SansForce)> CompterAsync(
        string chaine
    )
    {
        await using var connexion = new NpgsqlConnection(chaine);
        await connexion.OpenAsync();
        await using var commande = new NpgsqlCommand(
            """
            select (select count(*) from public.nutrient_refs),
                   (select count(*) from pg_class c join pg_namespace n on n.oid = c.relnamespace
                     where n.nspname = 'public' and c.relkind = 'r'),
                   (select count(*) from pg_policies where schemaname = 'public'),
                   (select count(*) from pg_class c join pg_namespace n on n.oid = c.relnamespace
                     where n.nspname = 'public' and c.relkind = 'r'
                       and (not c.relrowsecurity or not c.relforcerowsecurity))
            """,
            connexion
        );
        await using var lecteur = await commande.ExecuteReaderAsync();
        await lecteur.ReadAsync();
        return (lecteur.GetInt64(0), lecteur.GetInt64(1), lecteur.GetInt64(2), lecteur.GetInt64(3));
    }

    private static async Task<IReadOnlyList<string>> ListerAsync(string chaine, string sql)
    {
        await using var connexion = new NpgsqlConnection(chaine);
        await connexion.OpenAsync();
#pragma warning disable CA2100 // Constantes littérales de ce fichier ; aucune entrée extérieure.
        await using var commande = new NpgsqlCommand(sql, connexion);
#pragma warning restore CA2100
        await using var lecteur = await commande.ExecuteReaderAsync();
        var noms = new List<string>();
        while (await lecteur.ReadAsync())
        {
            noms.Add(lecteur.GetString(0));
        }

        return noms;
    }

    /// <summary>
    /// Les lignes de sonde sont écrites SOUS `palier_migrations`, par la
    /// politique `migrations_referentiel` — jamais par un contournement.
    /// </summary>
    private async Task PoserDesLignesAsync(string marqueur)
    {
        await using var connexion = new NpgsqlConnection(baseDeDonnees.ChaineMigrations);
        await connexion.OpenAsync();
        await using var commande = new NpgsqlCommand(
            """
            insert into public.nutrient_refs (nutrient, label_fr, label_en, unit, source, source_year)
            values (@a, 'Sonde A', 'Probe A', 'mg', 'SONDE', 2026),
                   (@b, 'Sonde B', 'Probe B', 'mg', 'SONDE', 2026)
            """,
            connexion
        );
        commande.Parameters.AddWithValue("a", marqueur + "_a");
        commande.Parameters.AddWithValue("b", marqueur + "_b");
        await commande.ExecuteNonQueryAsync();
    }

    private async Task RetirerLesLignesAsync(string marqueur)
    {
        await using var connexion = new NpgsqlConnection(baseDeDonnees.ChaineMigrations);
        await connexion.OpenAsync();
        await using var commande = new NpgsqlCommand(
            "delete from public.nutrient_refs where nutrient like @m",
            connexion
        );
        commande.Parameters.AddWithValue("m", marqueur + "%");
        await commande.ExecuteNonQueryAsync();
    }

    private async Task AdministrerAsync(string sql, string? baseCible = null)
    {
        var chaine =
            baseCible is null
                ? baseDeDonnees.ChaineAdministrateur
                : Remplacer(baseDeDonnees.ChaineAdministrateur, baseCible);
        await using var connexion = new NpgsqlConnection(chaine);
        await connexion.OpenAsync();
#pragma warning disable CA2100 // Littéraux et noms de base tirés d'un Guid ; aucune entrée extérieure.
        await using var commande = new NpgsqlCommand(sql, connexion);
#pragma warning restore CA2100
        await commande.ExecuteNonQueryAsync();
    }

    private static string Remplacer(string chaine, string nomDeBase) =>
        string.Join(
            ';',
            chaine
                .Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Select(p =>
                    p.Trim().StartsWith("Database=", StringComparison.OrdinalIgnoreCase)
                        ? $"Database={nomDeBase}"
                        : p
                )
        );
}
