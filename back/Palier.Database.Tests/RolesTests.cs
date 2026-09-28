using Npgsql;

namespace Palier.Database.Tests;

/// <summary>
/// L'épreuve la plus importante de la tâche 5, et celle dont dépendent toutes
/// les suivantes.
///
/// Sans elle, les épreuves d'isolation seraient des faux verts d'une classe
/// particulièrement traître : elles porteraient sur un rôle que la production
/// n'utilisera jamais. « Superusers and roles with the BYPASSRLS attribute
/// always bypass the row security system when accessing a table. Table owners
/// normally bypass row security as well » (ddl-rowsecurity.html). Une suite
/// d'isolation exécutée sous le compte d'administration du conteneur passe
/// INTÉGRALEMENT au vert, et ne démontre rien.
///
/// `pg_roles` est « a publicly readable view of pg_authid that blanks out the
/// password field » : les assertions ci-dessous sont donc exécutables depuis le
/// rôle applicatif lui-même, sans le moindre privilège.
/// </summary>
[Collection(BaseFixture.Collection)]
public sealed class RolesTests(BaseFixture baseDeDonnees)
{
    [Fact]
    public async Task La_cible_existe_le_tag_vient_du_compose_et_rien_ne_le_redeclare()
    {
        // Quatrième question du franchissement : le premier test vérifie que sa
        // cible existe. Une fixture qui se rabattrait sur un tag par défaut
        // rendrait toute la suite verte sur une base qui n'est pas celle du
        // produit.
        var compose = Path.Combine(baseDeDonnees.Racine, "db", "compose.yaml");
        Assert.True(File.Exists(compose), $"Cible manquante : {compose}");

        var declare = await File.ReadAllTextAsync(compose);
        Assert.Contains(
            baseDeDonnees.Tag,
            declare,
            StringComparison.Ordinal
        );

        // Le conteneur tourne bien sur CE tag, pas sur celui que le module
        // Testcontainers embarque par défaut : un réglage accepté n'est pas un
        // réglage appliqué (P11). On interroge le moteur, on ne relit pas le
        // constructeur.
        await using var source = new NpgsqlDataSourceBuilder(baseDeDonnees.ChaineApp).Build();
        await using var connexion = await source.OpenConnectionAsync();
        await using var commande = new NpgsqlCommand("select version()", connexion);
        var version = await commande.ExecuteScalarAsync();
        var majeure = baseDeDonnees.Tag.Split(':')[^1].Split('.')[0];
        Assert.True(
            $"{version}".StartsWith($"PostgreSQL {majeure}", StringComparison.Ordinal),
            $"Le compose déclare « {baseDeDonnees.Tag} », le moteur qui a répondu dit "
                + $"« {version} ». Le tag est accepté par le constructeur mais pas appliqué "
                + "au conteneur — P11."
        );
    }

    [Fact]
    public async Task Le_role_applicatif_n_est_ni_superutilisateur_ni_porteur_de_bypassrls()
    {
        await using var connexion = new NpgsqlDataSourceBuilder(baseDeDonnees.ChaineApp)
            .Build()
            .CreateConnection();
        await connexion.OpenAsync();

        // Première assertion : c'est bien le rôle applicatif qui parle. Une
        // suite lancée sous le compte d'administration passerait tout au vert.
        await using (var commande = new NpgsqlCommand("select current_user", connexion))
        {
            var role = (string)(await commande.ExecuteScalarAsync())!;
            Assert.True(
                role == "palier_app",
                $"La fixture parle au moteur sous « {role} », pas sous « palier_app ». "
                    + "Toutes les épreuves d'isolation de ce projet porteraient alors sur un rôle "
                    + "que la production n'utilise pas."
            );
        }

        // Seconde et troisième : les deux attributs qui feraient de RLS une
        // décoration. Chacune nomme la valeur trouvée — `Assert.False(...)` sans
        // message oblige à rouvrir le test pour comprendre ce qui a lâché.
        await using var attributs = new NpgsqlCommand(
            "select rolsuper, rolbypassrls from pg_roles where rolname = current_user",
            connexion
        );
        await using var lecteur = await attributs.ExecuteReaderAsync();
        Assert.True(await lecteur.ReadAsync(), "pg_roles ne connaît pas current_user");

        var superutilisateur = lecteur.GetBoolean(0);
        var contourne = lecteur.GetBoolean(1);
        Assert.True(
            superutilisateur == false,
            $"palier_app porte rolsuper = {superutilisateur}. Un superutilisateur contourne "
                + "TOUTES les politiques : les huit épreuves d'isolation deviendraient vertes "
                + "sans qu'aucune politique existe."
        );
        Assert.True(
            contourne == false,
            $"palier_app porte rolbypassrls = {contourne}. « Roles with the BYPASSRLS attribute "
                + "always bypass the row security system » — les politiques ne mordraient plus, "
                + "et rien ne le signalerait."
        );
    }

    [Fact]
    public async Task Le_role_des_migrations_ne_contourne_pas_rls_malgre_sa_propriete_des_tables()
    {
        // C'est le rôle qui possède les tables. Sous `force row level security`
        // (D38), un propriétaire redevient soumis aux politiques — mais un
        // propriétaire porteur de BYPASSRLS, lui, ne l'est jamais. Les deux
        // attributs se lisent depuis palier_app : pg_roles est publique.
        var (superutilisateur, contourne) = await Attributs("palier_migrations");
        Assert.True(
            superutilisateur == false,
            $"palier_migrations porte rolsuper = {superutilisateur}"
        );
        Assert.True(
            contourne == false,
            $"palier_migrations porte rolbypassrls = {contourne}. Il possède les tables : avec "
                + "cet attribut, `force row level security` ne changerait plus rien pour lui."
        );
    }

    [Fact]
    public async Task Un_seul_role_du_produit_contourne_rls_et_c_est_celui_de_la_sauvegarde()
    {
        // Forme CATALOGUE plutôt que liste écrite à la main : elle couvre les
        // rôles FUTURS sans que personne ait à y penser. Une liste vieillit, et
        // une liste qui vieillit passe au vert sur ce qu'elle ne connaît pas.
        var porteurs = new List<string>();
        await using var source = new NpgsqlDataSourceBuilder(baseDeDonnees.ChaineApp).Build();
        await using var connexion = await source.OpenConnectionAsync();
        // Le compte d'administration du conteneur est écarté NOMMÉMENT, et non
        // par un motif : il est superutilisateur par construction, il n'exécute
        // que `db/amorcage/01-roles.sql`, et sur l'instance managée il n'est même
        // pas le nôtre. Son nom est LU dans le compose, comme le reste — le
        // laisser dans le parcours faisait rougir cette épreuve pour la mauvaise
        // raison, ce qu'elle a signalé en le nommant.
        await using var commande = new NpgsqlCommand(
            "select rolname from pg_roles where rolname like 'palier@_%' escape '@' "
                + "and rolname <> $1 and (rolsuper or rolbypassrls) order by rolname",
            connexion
        );
        commande.Parameters.AddWithValue(baseDeDonnees.Administrateur);
        await using var lecteur = await commande.ExecuteReaderAsync();
        while (await lecteur.ReadAsync())
        {
            porteurs.Add(lecteur.GetString(0));
        }

        Assert.True(
            porteurs.SequenceEqual(["palier_sauvegarde"]),
            "Les rôles « palier_* » qui contournent RLS devraient se réduire à "
                + $"palier_sauvegarde ; trouvés : {(porteurs.Count == 0 ? "aucun" : string.Join(", ", porteurs))}. "
                + "Aucun n'est superutilisateur, et seul celui de la sauvegarde porte BYPASSRLS "
                + "— D37, avec le prix mesuré à la tâche 10."
        );

        var (superutilisateur, _) = await Attributs("palier_sauvegarde");
        Assert.True(
            superutilisateur == false,
            $"palier_sauvegarde porte rolsuper = {superutilisateur}"
        );
    }

    [Fact]
    public async Task Le_role_applicatif_ne_peut_creer_ni_base_ni_role()
    {
        var (creerBase, creerRole) = await Creations("palier_app");
        Assert.True(creerBase == false, $"palier_app porte rolcreatedb = {creerBase}");
        Assert.True(
            creerRole == false,
            $"palier_app porte rolcreaterole = {creerRole}. Avec CREATEROLE il pourrait se "
                + "fabriquer un rôle porteur de BYPASSRLS et sortir de sa propre barrière."
        );
    }

    private async Task<(bool Superutilisateur, bool Contourne)> Attributs(string role)
    {
        await using var source = new NpgsqlDataSourceBuilder(baseDeDonnees.ChaineApp).Build();
        await using var connexion = await source.OpenConnectionAsync();
        await using var commande = new NpgsqlCommand(
            "select rolsuper, rolbypassrls from pg_roles where rolname = $1",
            connexion
        );
        commande.Parameters.AddWithValue(role);
        await using var lecteur = await commande.ExecuteReaderAsync();
        Assert.True(await lecteur.ReadAsync(), $"pg_roles ne connaît aucun rôle « {role} »");
        return (lecteur.GetBoolean(0), lecteur.GetBoolean(1));
    }

    private async Task<(bool CreerBase, bool CreerRole)> Creations(string role)
    {
        await using var source = new NpgsqlDataSourceBuilder(baseDeDonnees.ChaineApp).Build();
        await using var connexion = await source.OpenConnectionAsync();
        await using var commande = new NpgsqlCommand(
            "select rolcreatedb, rolcreaterole from pg_roles where rolname = $1",
            connexion
        );
        commande.Parameters.AddWithValue(role);
        await using var lecteur = await commande.ExecuteReaderAsync();
        Assert.True(await lecteur.ReadAsync(), $"pg_roles ne connaît aucun rôle « {role} »");
        return (lecteur.GetBoolean(0), lecteur.GetBoolean(1));
    }

    // ================================================================
    // Le quatrième rôle — lot 4, le chemin que D38 laissait à concevoir
    // ================================================================

    [Fact]
    public async Task Le_role_d_authentification_n_est_ni_superutilisateur_ni_porteur_de_bypassrls()
    {
        await using var connexion = new NpgsqlDataSourceBuilder(baseDeDonnees.ChaineAuth)
            .Build()
            .CreateConnection();
        await connexion.OpenAsync();

        await using (var commande = new NpgsqlCommand("select current_user", connexion))
        {
            var role = (string)(await commande.ExecuteScalarAsync())!;
            Assert.True(
                role == "palier_auth",
                $"La fixture parle au moteur sous « {role} », pas sous « palier_auth »."
            );
        }

        await using var attributs = new NpgsqlCommand(
            "select rolsuper, rolbypassrls from pg_roles where rolname = current_user",
            connexion
        );
        await using var lecteur = await attributs.ExecuteReaderAsync();
        Assert.True(await lecteur.ReadAsync(), "pg_roles ne connaît pas current_user");

        Assert.True(
            !lecteur.GetBoolean(0),
            "palier_auth est SUPERUTILISATEUR : il contournerait toutes les politiques."
        );
        Assert.True(
            !lecteur.GetBoolean(1),
            "palier_auth porte BYPASSRLS : « roles with the BYPASSRLS attribute always "
                + "bypass the row security system ». La barrière des tables d'identité "
                + "deviendrait décorative."
        );
    }

    [Fact]
    public async Task Le_role_d_authentification_ne_possede_aucune_table()
    {
        await using var connexion = new NpgsqlDataSourceBuilder(baseDeDonnees.ChaineApp)
            .Build()
            .CreateConnection();
        await connexion.OpenAsync();

        await using var commande = new NpgsqlCommand(
            """
            select c.relname from pg_class c
              join pg_namespace n on n.oid = c.relnamespace
              join pg_roles r on r.oid = c.relowner
             where n.nspname = 'public' and c.relkind = 'r' and r.rolname = 'palier_auth'
             order by c.relname
            """,
            connexion
        );

        var possedees = new List<string>();
        await using var lecteur = await commande.ExecuteReaderAsync();
        while (await lecteur.ReadAsync())
        {
            possedees.Add(lecteur.GetString(0));
        }

        Assert.True(
            possedees.Count == 0,
            $"palier_auth possède {possedees.Count} table(s) : {string.Join(", ", possedees)}. "
                + "« Table owners normally bypass row security as well » — un propriétaire "
                + "échappe à ses propres politiques."
        );
    }

    [Fact]
    public async Task Le_role_d_authentification_n_atteint_aucune_donnee_de_sante()
    {
        // Sa seule fonction est de reconnaître qui se présente. Un privilège sur
        // workouts, sets ou body_weight ferait de lui un second chemin vers les
        // données de l'article 9 — et un chemin qui, lui, n'a aucune identité à
        // poser.
        //
        // Les trois requêtes sont écrites au site d'appel : un nom de table ne
        // peut pas être un paramètre lié, et CA2100 refuse jusqu'à la variable
        // issue d'un littéral. C'est la doctrine « aucune requête construite par
        // concaténation », appliquée par le compilateur.
        await RefuseAsync("workouts", c => new NpgsqlCommand("select count(*) from public.workouts", c));
        await RefuseAsync("sets", c => new NpgsqlCommand("select count(*) from public.sets", c));
        await RefuseAsync("body_weight", c => new NpgsqlCommand("select count(*) from public.body_weight", c));
    }

    /// <summary>
    /// Ouvre une connexion sous <c>palier_auth</c>, exécute la commande, et exige
    /// un refus de privilège. Le code 42501 est nommé : un code non nul ne prouve
    /// pas que le moteur a refusé — une table absente en rendrait un aussi.
    /// </summary>
    private async Task RefuseAsync(string table, Func<NpgsqlConnection, NpgsqlCommand> fabrique)
    {
        var refus = await Assert.ThrowsAsync<PostgresException>(async () =>
        {
            await using var connexion = new NpgsqlDataSourceBuilder(baseDeDonnees.ChaineAuth)
                .Build()
                .CreateConnection();
            await connexion.OpenAsync();
            await using var commande = fabrique(connexion);
            await commande.ExecuteScalarAsync();
        });

        Assert.True(
            refus.SqlState == "42501",
            $"palier_auth atteint `{table}` : attendu 42501 (privilège refusé), "
                + $"reçu {refus.SqlState}."
        );
    }
}
