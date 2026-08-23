using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace Palier.Database.Tests;

/// <summary>
/// Les épreuves du schéma, écrites en forme de CATALOGUE et non de liste.
///
/// Le choix est délibéré. Une liste de tables écrite à la main couvre ce que
/// son auteur connaissait le jour où il l'a écrite, et passe au vert sur les
/// tables qu'elle ne connaît pas. Une interrogation de `pg_class` couvre les
/// tables FUTURES sans que personne ait à y penser.
///
/// Elles ferment le mode de défaillance que `docs/03-donnees.md` nomme :
/// « un schéma qui compile, qui démarre, et qui n'applique ni les vues, ni RLS,
/// ni les bornes. Rien ne le signale au démarrage. »
/// </summary>
[Collection(BaseFixture.Collection)]
public sealed class SchemaTests(BaseFixture baseDeDonnees)
{
    [Fact]
    public async Task Toute_table_de_public_porte_rls_activee_ET_forcee()
    {
        var nues = new List<string>();
        await using var connexion = await Connexion();

        // Les DEUX colonnes, pas une. `relrowsecurity` seule laisse le
        // PROPRIÉTAIRE contourner les politiques — « Table owners normally
        // bypass row security as well » — et c'est la première des trois
        // familles de cas que RLS doit couvrir.
        await using var commande = new NpgsqlCommand(
            """
            select c.relname,
                   c.relrowsecurity      as activee,
                   c.relforcerowsecurity as forcee
              from pg_class c
              join pg_namespace n on n.oid = c.relnamespace
             where n.nspname = 'public'
               and c.relkind = 'r'
               and (not c.relrowsecurity or not c.relforcerowsecurity)
             order by c.relname
            """,
            connexion
        );
        await using var lecteur = await commande.ExecuteReaderAsync();
        while (await lecteur.ReadAsync())
        {
            nues.Add(
                $"{lecteur.GetString(0)} (activée={lecteur.GetBoolean(1)}, "
                    + $"forcée={lecteur.GetBoolean(2)})"
            );
        }

        Assert.True(
            nues.Count == 0,
            "Tables de `public` sans RLS activée ET forcée : "
                + $"{string.Join(" · ", nues)}. Le catalogue interrogé est `pg_class`, "
                + "relkind = 'r', schéma `public`."
        );

        // Sans cette seconde assertion, un parcours cassé — mauvais schéma,
        // mauvais `relkind`, base vide — rendrait zéro ligne et l'épreuve
        // ci-dessus serait verte en n'ayant rien lu. C'est le ruling P12.
        var comptees = await Compter("select count(*) from pg_class c "
            + "join pg_namespace n on n.oid = c.relnamespace "
            + "where n.nspname = 'public' and c.relkind = 'r'");
        Assert.True(
            comptees >= 13,
            $"Le catalogue n'a vu que {comptees} tables dans `public`. La migration porte "
                + "sept tables d'identité, cinq tables du produit et l'historique des "
                + "migrations : moins de treize signifie que le parcours regarde ailleurs."
        );
    }

    [Fact]
    public async Task Les_tables_de_la_tranche_D39_existent_toutes()
    {
        // Quatrième question du franchissement : la cible existe-t-elle ?
        // Les épreuves d'isolation qui suivent portent chacune sur une table
        // nommée ; si l'une manquait, elles rougiraient toutes pour un motif
        // qui ne dirait pas ce qui s'est passé.
        foreach (
            var table in new[]
            {
                "AspNetUsers",
                "nutrient_refs",
                "exercises",
                "workouts",
                "sets",
                "body_weight",
            }
        )
        {
            var existe = await Compter(
                "select count(*) from pg_class c join pg_namespace n on n.oid = c.relnamespace "
                    + $"where n.nspname = 'public' and c.relkind = 'r' and c.relname = '{table}'"
            );
            Assert.True(existe == 1, $"Table manquante dans `public` : {table}");
        }
    }

    [Fact]
    public async Task Les_DEUX_tables_du_lot_5_existent()
    {
        // D39 : « les treize autres tables arrivent au lot qui les utilise ».
        // Le lot 5 en a exigé DEUX, et pas davantage — le ressenti par
        // exercice et les contraintes déclarées. Les onze restantes
        // appartiennent à la nutrition, à l'abonnement et à l'assistant : les
        // poser ici aurait produit onze tables qu'aucun cas d'usage n'exerce,
        // le « garde-fou sans cible » que D39 refuse nommément.
        //
        // Cette épreuve est SÉPARÉE de celle de la tranche D39, parce qu'elles
        // ne disent pas la même chose : l'une garde une décision d'architecture
        // prise au lot 2, l'autre constate ce que le lot 5 a ajouté.
        foreach (var table in new[] { "exercise_feedback", "user_constraints" })
        {
            var existe = await Compter(
                "select count(*) from pg_class c join pg_namespace n on n.oid = c.relnamespace "
                    + $"where n.nspname = 'public' and c.relkind = 'r' and c.relname = '{table}'"
            );
            Assert.True(existe == 1, $"Table manquante dans `public` : {table}");
        }
    }

    [Fact]
    public async Task Les_bornes_des_DEUX_listes_fermees_sont_appliquees_par_le_MOTEUR()
    {
        // Les contraintes `CHECK` des deux tables nouvelles. La validation
        // applicative refuse déjà, mais elle ne protège pas d'une écriture
        // faite HORS de l'API — une commande d'exploitation, une reprise de
        // données, un script. Et une valeur inconnue en base ne produirait
        // aucune erreur : les seuils du § 5 compteraient faux, et le filtrage
        // du § 4 ne trouverait simplement rien.
        //
        // On interroge le catalogue plutôt que de relire la migration : c'est
        // l'état du moteur qui compte, pas le texte qui l'a produit.
        foreach (
            var (table, contrainte) in new[]
            {
                ("exercise_feedback", "ck_exercise_feedback_feeling"),
                ("user_constraints", "ck_user_constraints_region"),
            }
        )
        {
            var posee = await Compter(
                "select count(*) from pg_constraint c "
                    + "join pg_class t on t.oid = c.conrelid "
                    + "join pg_namespace n on n.oid = t.relnamespace "
                    + $"where n.nspname = 'public' and t.relname = '{table}' "
                    + $"and c.conname = '{contrainte}' and c.contype = 'c'"
            );
            Assert.True(posee == 1, $"Contrainte CHECK manquante : {contrainte} sur {table}");
        }
    }

    [Fact]
    public async Task La_vue_weekly_volume_est_appliquee_et_s_execute_avec_les_droits_de_l_appelant()
    {
        // Interroger `pg_views` plutôt que comparer un fichier : c'est l'état
        // du moteur qui compte, pas le texte de la migration.
        var vues = await Compter(
            "select count(*) from pg_views where schemaname = 'public' and viewname = 'weekly_volume'"
        );
        Assert.True(
            vues == 1,
            "La vue `weekly_volume` est absente de `pg_views` (schéma public). Une migration "
                + "peut s'appliquer entièrement sans elle : l'application démarrerait, et rien "
                + "ne le signalerait."
        );

        // `security_invoker` n'est pas un détail de performance. Sans lui la
        // vue s'exécute avec les droits de son propriétaire, `palier_migrations`,
        // et ce sont SES politiques qui filtrent — pas celles de l'appelant.
        var invoker = await Compter(
            """
            select count(*) from pg_class c
              join pg_namespace n on n.oid = c.relnamespace
             where n.nspname = 'public' and c.relname = 'weekly_volume'
               and c.reloptions @> array['security_invoker=true']
            """
        );
        Assert.True(
            invoker == 1,
            "La vue `weekly_volume` ne porte pas `security_invoker=true`. Elle serait le seul "
                + "chemin du schéma où l'isolation change de règle sans que personne le voie."
        );
    }

    [Fact]
    public async Task Le_Down_de_la_migration_defait_ce_que_le_Up_a_pose()
    {
        // ÉPREUVE DISTINCTE de la précédente, et c'est voulu. Retirer la vue du
        // `Up` et retirer le `Down` de la vue sont deux fautes différentes :
        // l'application démarrerait dans les deux cas. La première se voit à la
        // création, la seconde seulement le jour d'un retour arrière — c'est-à-
        // dire le jour où l'on a le moins de temps.
        //
        // L'épreuve tourne sur une base À PART, créée pour elle : dérouler le
        // `Down` sur la base partagée détruirait le schéma des autres épreuves.
        var nom = $"repli_{Guid.NewGuid():N}";
        await using (var administrateur = new NpgsqlConnection(baseDeDonnees.ChaineAdministrateur))
        {
            await administrateur.OpenAsync();
            // `create database` n'accepte aucun paramètre lié : PostgreSQL
            // refuse les paramètres dans les commandes utilitaires. Les deux
            // valeurs interpolées sont un `Guid` au format N et le compte lu
            // dans le compose — aucune n'a d'origine extérieure.
#pragma warning disable CA2100
            await using var creer = new NpgsqlCommand(
                $"""
                create database "{nom}" owner "{baseDeDonnees.Administrateur}";
                """,
                administrateur
            );
#pragma warning restore CA2100
            await creer.ExecuteNonQueryAsync();
        }

        var chaineAdministrateur = Remplacer(baseDeDonnees.ChaineAdministrateur, nom);
        var chaineMigrations = Remplacer(baseDeDonnees.ChaineMigrations, nom);
        await using (var administrateur = new NpgsqlConnection(chaineAdministrateur))
        {
            await administrateur.OpenAsync();
            // Les mêmes privilèges que `db/amorcage/01-roles.sql` accorde sur la
            // base du produit : CREATE sur la BASE pour `create schema app`, et
            // CREATE sur `public` pour les tables. Les rôles, eux, existent déjà
            // — ils sont à l'échelle du groupe de bases, pas de la base.
#pragma warning disable CA2100 // Même motif : `grant … on database` n'accepte pas de paramètre lié.
            await using var droits = new NpgsqlCommand(
                $"""
                grant create on database "{nom}" to palier_migrations;
                grant create, usage on schema public to palier_migrations;
                """,
                administrateur
            );
#pragma warning restore CA2100
            await droits.ExecuteNonQueryAsync();
        }

        try
        {
            await using (var contexte = BaseFixture.Contexte(chaineMigrations))
            {
                await contexte.Database.MigrateAsync();
            }

            var apresUp = await CompterSur(chaineMigrations, _vuesEtTables);

            // `Down` : la migration se déroule. Si elle oublie la vue, PostgreSQL
            // refuse le `drop table` et l'épreuve rougit ici même.
            await using (var contexte = BaseFixture.Contexte(chaineMigrations))
            {
                var migrateur = contexte.GetService<IMigrator>();
                await migrateur.MigrateAsync("0");
            }

            var apresDown = await CompterSur(chaineMigrations, _vuesEtTables);
            Assert.True(
                apresDown == 0,
                $"Après le `Down`, il reste {apresDown} objets du socle dans `public`. "
                    + "Un `Down` incomplet laisse une base dans un état qu'aucune migration "
                    + "ne décrit."
            );

            // Puis `Up` de nouveau : l'état initial doit revenir à l'identique.
            await using (var contexte = BaseFixture.Contexte(chaineMigrations))
            {
                await contexte.Database.MigrateAsync();
            }

            var apresRejeu = await CompterSur(chaineMigrations, _vuesEtTables);
            Assert.True(
                apresRejeu == apresUp,
                $"`Down` puis `Up` rend {apresRejeu} objets là où le premier `Up` en posait "
                    + $"{apresUp}. La migration n'est pas rejouable."
            );
        }
        finally
        {
            await using var administrateur = new NpgsqlConnection(
                baseDeDonnees.ChaineAdministrateur
            );
            await administrateur.OpenAsync();
#pragma warning disable CA2100 // Même motif : `drop database` n'accepte pas de paramètre lié.
            await using var supprimer = new NpgsqlCommand(
                $"""drop database if exists "{nom}" with (force);""",
                administrateur
            );
#pragma warning restore CA2100
            await supprimer.ExecuteNonQueryAsync();
        }
    }

    [Fact]
    public async Task Le_moteur_refuse_une_energie_hors_des_bornes_du_schema()
    {
        // La borne vit dans le MOTEUR, pas dans une validation applicative :
        // une écriture directe, une restauration ou un `FromSqlRaw` la
        // rencontrent aussi. L'insertion se fait sous `palier_migrations`, qui
        // porte la politique d'écriture — sans quoi le refus viendrait de RLS
        // et non du `CHECK`, et l'épreuve prouverait autre chose.
        var proprietaire = await CreerUtilisateur();

        var refus = await Assert.ThrowsAsync<PostgresException>(async () =>
        {
            await using var connexion = new NpgsqlConnection(baseDeDonnees.ChaineMigrations);
            await connexion.OpenAsync();
            await using var commande = new NpgsqlCommand(
                "insert into public.workouts (owner_id, energy_1_5) values ($1, 6)",
                connexion
            );
            commande.Parameters.AddWithValue(proprietaire);
            await commande.ExecuteNonQueryAsync();
        });

        Assert.True(
            refus.SqlState == PostgresErrorCodes.CheckViolation,
            $"Attendu 23514 (violation de CHECK), reçu {refus.SqlState} : {refus.MessageText}"
        );
        Assert.True(
            refus.ConstraintName == "ck_workouts_energy_1_5",
            $"Le refus vient de la contrainte « {refus.ConstraintName} », pas de "
                + "`ck_workouts_energy_1_5` : l'épreuve mesure autre chose que la borne du schéma."
        );
    }

    [Fact]
    public async Task Le_modele_Csharp_ne_derive_pas_du_schema_migre()
    {
        // LE SEUL GARDE-FOU AUTOMATIQUE de dérive entre le modèle C# et le
        // schéma. Modifier une entité sans générer la migration produit une
        // application qui compile, démarre, et interroge des colonnes qui
        // n'existent pas.
        //
        // L'appel passe par l'API d'EF plutôt que par
        // `dotnet ef migrations has-pending-model-changes` : la commande
        // exigerait une seconde liste de contrôles (D25), là où ce test entre
        // dans `back:test`, donc dans `npm run verify`, sans étape nouvelle.
        await using var contexte = BaseFixture.Contexte(baseDeDonnees.ChaineMigrations);
        Assert.True(
            contexte.Database.HasPendingModelChanges() == false,
            "Le modèle C# a changé sans qu'une migration soit générée. "
                + "`dotnet ef migrations add <Nom> --project back/Palier.Infrastructure "
                + "--startup-project back/Palier.Infrastructure` — et la migration se relit "
                + "avant d'être appliquée."
        );
    }

    private const string _vuesEtTables = """
        select count(*) from (
          select c.relname from pg_class c
            join pg_namespace n on n.oid = c.relnamespace
           where n.nspname = 'public' and c.relkind in ('r', 'v')
             and c.relname <> '__EFMigrationsHistory'
        ) x
        """;

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

    private async Task<Guid> CreerUtilisateur()
    {
        var identifiant = Guid.NewGuid();
        await using var connexion = new NpgsqlConnection(baseDeDonnees.ChaineAdministrateur);
        await connexion.OpenAsync();
        await using var commande = new NpgsqlCommand(
            """
            insert into public."AspNetUsers" ("Id", "EmailConfirmed", "PhoneNumberConfirmed",
                   "TwoFactorEnabled", "LockoutEnabled", "AccessFailedCount")
            values ($1, false, false, false, false, 0)
            """,
            connexion
        );
        commande.Parameters.AddWithValue(identifiant);
        await commande.ExecuteNonQueryAsync();
        return identifiant;
    }

    private async Task<NpgsqlConnection> Connexion()
    {
        var connexion = new NpgsqlConnection(baseDeDonnees.ChaineApp);
        await connexion.OpenAsync();
        return connexion;
    }

    private async Task<long> Compter(string sql)
    {
        await using var connexion = await Connexion();
#pragma warning disable CA2100 // Le SQL vient exclusivement de constantes de ce fichier.
        await using var commande = new NpgsqlCommand(sql, connexion);
#pragma warning restore CA2100
        return (long)(await commande.ExecuteScalarAsync())!;
    }

    private static async Task<long> CompterSur(string chaine, string sql)
    {
        await using var connexion = new NpgsqlConnection(chaine);
        await connexion.OpenAsync();
#pragma warning disable CA2100 // Le SQL vient exclusivement de constantes de ce fichier.
        await using var commande = new NpgsqlCommand(sql, connexion);
#pragma warning restore CA2100
        return (long)(await commande.ExecuteScalarAsync())!;
    }
}
