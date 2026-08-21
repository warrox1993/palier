using Npgsql;
using Palier.Api.Socle;

namespace Palier.Database.Tests;

/// <summary>
/// L'ASSERTION DE DÉMARRAGE — D37, et elle vient de D30.
///
/// Ces épreuves ne portent pas sur le contenu d'un fichier mais sur l'ÉTAT DU
/// SERVICE : elles provoquent, sur un moteur réel, les trois configurations dans
/// lesquelles RLS ne mord pas, et vérifient que l'API refuse de servir en
/// NOMMANT la cause. Ce sont les seules preuves qui vaudront quelque chose sur
/// l'instance managée, où deux inconnues décident si l'isolation existe : le
/// compte d'administration porte-t-il BYPASSRLS, et les tables créées par les
/// migrations appartiennent-elles au rôle de l'API.
///
/// Chaque violation est DÉFAITE dans un <c>finally</c>. Les classes d'épreuve de
/// ce projet partagent une collection xUnit, donc un conteneur et une exécution
/// séquentielle : une violation laissée en place ferait rougir `RolesTests` ou
/// `SchemaTests` à la place, avec un message qui ne dirait pas d'où elle vient.
/// </summary>
[Collection(BaseFixture.Collection)]
public sealed class AssertionDemarrageTests(BaseFixture baseDeDonnees)
{
    [Fact]
    public async Task Les_quatre_requetes_de_catalogue_lisent_reellement_quelque_chose()
    {
        // Quatrième question du franchissement. Sans cette épreuve, les trois
        // contrôles suivants pourraient interroger une base vide, ne rien
        // trouver, et passer au vert sans avoir rien regardé.
        await using var contexte = BaseFixture.Contexte(baseDeDonnees.ChaineApp);
        var diagnostic = await new LecteurDeSocle(contexte).DiagnostiquerAsync();

        Assert.Equal("palier_app", diagnostic.Role);
        Assert.True(
            diagnostic.NombreDeTables >= 13,
            $"Le catalogue ne rend que {diagnostic.NombreDeTables} table(s) dans `public`. "
                + "La tranche D39 en pose treize, historique des migrations compris — "
                + "l'assertion regarde donc une base qui n'est pas celle du produit."
        );
    }

    [Fact]
    public async Task Sous_le_role_applicatif_l_API_accepte_de_demarrer()
    {
        await using var contexte = BaseFixture.Contexte(baseDeDonnees.ChaineApp);
        var diagnostic = await new AssertionDIsolation(new LecteurDeSocle(contexte)).VerifierAsync(

        );

        Assert.False(diagnostic.IsSuperutilisateur);
        Assert.False(diagnostic.IsContournementRls);
        Assert.Empty(diagnostic.TablesPossedees);
        Assert.Empty(diagnostic.TablesSansForce);
    }

    [Fact]
    public async Task VIOLATION_1_avec_la_chaine_des_migrations_le_service_refuse_en_NOMMANT_une_table_possedee()
    {
        // La faute la plus facile à commettre et la plus coûteuse : une seule
        // variable d'environnement mal remplie, et TOUTES les politiques du
        // schéma cessent de mordre — « Table owners normally bypass row
        // security as well ».
        await using var contexte = BaseFixture.Contexte(baseDeDonnees.ChaineMigrations);
        var refus = await Assert.ThrowsAsync<IsolationNonGarantieException>(() =>
            new AssertionDIsolation(new LecteurDeSocle(contexte)).VerifierAsync(

            )
        );

        // Deux assertions, D19 : le refus a bien eu lieu, ET il nomme la cause.
        // Un refus pour une autre raison — base injoignable, rôle inexistant —
        // passerait la première sans rien démontrer.
        Assert.Contains("PROPRIÉTAIRE", refus.Cause, StringComparison.Ordinal);
        Assert.Contains("body_weight", refus.Cause, StringComparison.Ordinal);
        Assert.Contains("palier_migrations", refus.Cause, StringComparison.Ordinal);
    }

    [Fact]
    public async Task VIOLATION_2_avec_BYPASSRLS_sur_le_role_applicatif_le_service_refuse_en_NOMMANT_le_role()
    {
        await Administrer("alter role palier_app bypassrls");
        try
        {
            // `Pooling=false` : l'attribut du rôle est relu à chaque connexion,
            // mais une connexion déjà ouverte dans le pool aurait été établie
            // AVANT l'alter. La question porte sur l'état du rôle, pas sur celui
            // du pool.
            await using var contexte = BaseFixture.Contexte(
                baseDeDonnees.Chaine("palier_app", pooling: false)
            );
            var refus = await Assert.ThrowsAsync<IsolationNonGarantieException>(() =>
                new AssertionDIsolation(new LecteurDeSocle(contexte)).VerifierAsync(

                )
            );

            Assert.Contains("palier_app", refus.Cause, StringComparison.Ordinal);
            Assert.Contains("BYPASSRLS", refus.Cause, StringComparison.Ordinal);
        }
        finally
        {
            await Administrer("alter role palier_app nobypassrls");
        }
    }

    [Fact]
    public async Task VIOLATION_3_sans_force_row_level_security_le_service_refuse_en_NOMMANT_la_table()
    {
        await Administrer("alter table public.body_weight no force row level security");
        try
        {
            await using var contexte = BaseFixture.Contexte(baseDeDonnees.ChaineApp);
            var refus = await Assert.ThrowsAsync<IsolationNonGarantieException>(() =>
                new AssertionDIsolation(new LecteurDeSocle(contexte)).VerifierAsync(

                )
            );

            Assert.Contains("body_weight", refus.Cause, StringComparison.Ordinal);
            Assert.Contains("forcée", refus.Cause, StringComparison.Ordinal);
        }
        finally
        {
            await Administrer("alter table public.body_weight force row level security");
        }
    }

    [Fact]
    public async Task VIOLATION_4_sur_une_base_SANS_AUCUNE_TABLE_le_service_refuse_au_lieu_de_passer_au_vert()
    {
        // LA BRANCHE QU'ON NE FRANCHIT JAMAIS, et donc celle qui ment. Les trois
        // contrôles ci-dessus cherchent des rôles fautifs et des tables fautives.
        // Sur une base non migrée, il n'y a NI l'un NI l'autre : les trois listes
        // sortent vides et l'API démarrerait, sur une base sans schéma, en ayant
        // « vérifié » l'isolation.
        //
        // L'état est PROVOQUÉ — une base neuve, créée pour cette épreuve — et non
        // simulé par une liste vide construite à la main.
        var nom = $"vide_{Guid.NewGuid():N}";
        await Administrer($"""create database "{nom}" owner "{baseDeDonnees.Administrateur}";""");
        try
        {
            await using var contexte = BaseFixture.Contexte(
                Remplacer(baseDeDonnees.ChaineApp, nom)
            );
            var refus = await Assert.ThrowsAsync<IsolationNonGarantieException>(() =>
                new AssertionDIsolation(new LecteurDeSocle(contexte)).VerifierAsync(

                )
            );

            Assert.Contains("AUCUNE table", refus.Cause, StringComparison.Ordinal);
            Assert.Contains("n'est pas migrée", refus.Cause, StringComparison.Ordinal);
        }
        finally
        {
            await Administrer($"""drop database if exists "{nom}" with (force);""");
        }
    }

    /// <summary>
    /// Le compte d'administration du conteneur, et LUI SEUL, sert à provoquer les
    /// violations : accorder BYPASSRLS ou retirer FORCE demande des droits que
    /// les trois rôles du produit n'ont pas — c'est précisément ce que D37
    /// garantit. Les épreuves, elles, tournent sous les rôles du produit.
    /// </summary>
    private async Task Administrer(string sql)
    {
        await using var connexion = new NpgsqlConnection(baseDeDonnees.ChaineAdministrateur);
        await connexion.OpenAsync();
#pragma warning disable CA2100 // Aucune de ces instructions ne reçoit quoi que ce soit de l'extérieur : un nom de base tiré d'un Guid, sinon des littéraux.
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

    // ================================================================
    // VIOLATION 5 — le quatrième rôle, lot 4
    // ================================================================

    [Fact]
    public async Task VIOLATION_5_avec_BYPASSRLS_sur_le_role_d_authentification_le_service_refuse()
    {
        // Ajouter un accès sans étendre le contrôle qui le surveille est
        // l'erreur que le lot 1 a payée quatre fois. `palier_auth` détient le
        // seul chemin vers les empreintes de mots de passe, les secrets TOTP et
        // les sessions : s'il contournait RLS, la barrière que D38 avait posée
        // sur ces tables deviendrait décorative, et rien ne le dirait.
        await AvecBypassRlsSurAuthAsync(async () =>
        {
            await using var contexte = BaseFixture.Contexte(baseDeDonnees.ChaineApp);
            var refus = await Assert.ThrowsAsync<IsolationNonGarantieException>(() =>
                new AssertionDIsolation(new LecteurDeSocle(contexte)).VerifierAsync()
            );

            // Deux assertions, D19 : le refus a eu lieu, ET il nomme le rôle.
            // Un refus pour une autre cause passerait la première sans rien
            // démontrer.
            Assert.Contains("palier_auth", refus.Cause, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task VIOLATION_6_si_le_role_d_authentification_possede_une_table_le_service_refuse()
    {
        // « Table owners normally bypass row security as well ». Un rôle
        // propriétaire échappe à ses propres politiques : le contrôle vaut pour
        // palier_auth comme pour palier_app.
        await ExecuterAdministrateurAsync(c => new NpgsqlCommand(
            """
            create table public.temoin_possedee_auth (id int);
            alter table public.temoin_possedee_auth owner to palier_auth;
            alter table public.temoin_possedee_auth enable row level security;
            alter table public.temoin_possedee_auth force row level security;
            """, c));

        try
        {
            await using var contexte = BaseFixture.Contexte(baseDeDonnees.ChaineApp);
            var refus = await Assert.ThrowsAsync<IsolationNonGarantieException>(() =>
                new AssertionDIsolation(new LecteurDeSocle(contexte)).VerifierAsync()
            );

            Assert.Contains("palier_auth", refus.Cause, StringComparison.Ordinal);
            Assert.Contains("temoin_possedee_auth", refus.Cause, StringComparison.Ordinal);
        }
        finally
        {
            await ExecuterAdministrateurAsync(c =>
                new NpgsqlCommand("drop table if exists public.temoin_possedee_auth", c));
        }
    }

    /// <summary>
    /// Pose un attribut sur un rôle, exécute le corps, puis le retire — quoi
    /// qu'il arrive. Une épreuve qui laisserait BYPASSRLS derrière elle rendrait
    /// toutes les suivantes vertes sans rien démontrer.
    /// </summary>
    private async Task AvecBypassRlsSurAuthAsync(Func<Task> corps)
    {
        await PoserBypassRlsSurAuthAsync();
        try
        {
            await corps();
        }
        finally
        {
            await RetirerBypassRlsSurAuthAsync();
        }
    }

    // `alter role` n'accepte de paramètre lié ni pour le nom ni pour l'attribut,
    // et CA2100 refuse jusqu'à la variable issue d'un littéral. Les deux formes
    // sont donc écrites au site d'appel — c'est la même leçon que RolesTests.
    private Task PoserBypassRlsSurAuthAsync() =>
        ExecuterAdministrateurAsync(c => new NpgsqlCommand("alter role palier_auth bypassrls", c));

    private Task RetirerBypassRlsSurAuthAsync() =>
        ExecuterAdministrateurAsync(c =>
            new NpgsqlCommand("alter role palier_auth nobypassrls", c)
        );

    private async Task ExecuterAdministrateurAsync(Func<NpgsqlConnection, NpgsqlCommand> fabrique)
    {
        await using var connexion = await OuvrirAdministrateurAsync();
        await using var commande = fabrique(connexion);
        await commande.ExecuteNonQueryAsync();
    }

    private async Task<NpgsqlConnection> OuvrirAdministrateurAsync()
    {
        var connexion = new NpgsqlDataSourceBuilder(baseDeDonnees.ChaineAdministrateur)
            .Build()
            .CreateConnection();
        await connexion.OpenAsync();
        return connexion;
    }
}
