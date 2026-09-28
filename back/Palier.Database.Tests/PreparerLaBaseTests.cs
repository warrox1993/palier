using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Palier.Api.Outils;

namespace Palier.Database.Tests;

/// <summary>
/// La commande <c>preparer-la-base</c> — D81 : migrations puis référentiel,
/// sous le rôle propriétaire, sur une base NEUVE.
/// </summary>
/// <remarks>
/// Une base neuve, et non celle de la fixture : celle-ci est déjà migrée, et
/// une commande qui ne ferait rien y passerait au vert. Chaque épreuve crée la
/// sienne dans le même moteur, avec les mêmes rôles, et la détruit en sortant.
/// </remarks>
[Collection(BaseFixture.Collection)]
public sealed class PreparerLaBaseTests(BaseFixture baseDeDonnees)
{
    [Fact]
    public async Task Sur_une_base_VIDE_la_commande_migre_puis_charge_le_referentiel()
    {
        await using var neuve = await BaseNeuve.CreerAsync(baseDeDonnees);
        using var sortie = new StringWriter(CultureInfo.InvariantCulture);

        var code = await PreparerLaBase.ExecuterAsync(
            neuve.ChaineMigrations,
            Referentiel(),
            sortie,
            CancellationToken.None
        );

        Assert.Equal(0, code);

        // Toutes les migrations du code, et pas une de moins.
        await using (var contexte = BaseFixture.Contexte(neuve.ChaineMigrations))
        {
            Assert.Empty(await contexte.Database.GetPendingMigrationsAsync());
        }

        // Les mêmes cibles que les épreuves du référentiel : elles sont des
        // promesses des documents, pas des constats du contenu.
        Assert.True(
            await neuve.CompterAsync("select count(*) from public.exercises where is_custom = false") >= 250
        );
        Assert.True(
            await neuve.CompterAsync("select count(*) from public.programs where is_template = true") >= 55
        );

        Assert.Contains("Migrations appliquées.", sortie.ToString(), StringComparison.Ordinal);
        Assert.Contains("04b-programs-methodes.sql", sortie.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task La_commande_est_REJOUABLE_sans_doublon()
    {
        // La démonstration la relance à chaque démarrage.
        await using var neuve = await BaseNeuve.CreerAsync(baseDeDonnees);

        await PreparerLaBase.ExecuterAsync(neuve.ChaineMigrations, Referentiel(), TextWriter.Null, CancellationToken.None);
        var exercices = await neuve.CompterAsync("select count(*) from public.exercises");
        var programmes = await neuve.CompterAsync("select count(*) from public.programs");

        await PreparerLaBase.ExecuterAsync(neuve.ChaineMigrations, Referentiel(), TextWriter.Null, CancellationToken.None);

        Assert.Equal(exercices, await neuve.CompterAsync("select count(*) from public.exercises"));
        Assert.Equal(programmes, await neuve.CompterAsync("select count(*) from public.programs"));
    }

    [Fact]
    public async Task Un_dossier_SANS_fichier_refuse_avant_d_avoir_touche_la_base()
    {
        await using var neuve = await BaseNeuve.CreerAsync(baseDeDonnees);
        var vide = Directory.CreateTempSubdirectory("palier-referentiel-vide-");

        try
        {
            var refus = await Assert.ThrowsAsync<InvalidOperationException>(
                () => PreparerLaBase.ExecuterAsync(neuve.ChaineMigrations, vide.FullName, TextWriter.Null, CancellationToken.None)
            );

            Assert.Contains(vide.FullName, refus.Message, StringComparison.Ordinal);

            // Aucune migration : une base migrée sans catalogue démarrerait et
            // répondrait des listes vides, sans que rien ne le signale.
            Assert.Equal(
                0,
                await neuve.CompterAsync(
                    "select count(*) from pg_tables where schemaname = 'public'"
                )
            );
        }
        finally
        {
            vide.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task Un_fichier_REFUSE_par_le_moteur_est_nomme_avec_son_code()
    {
        await using var neuve = await BaseNeuve.CreerAsync(baseDeDonnees);
        var dossier = Directory.CreateTempSubdirectory("palier-referentiel-casse-");

        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(dossier.FullName, "99-casse.sql"),
                "select * from public.table_qui_n_existe_pas;"
            );

            var refus = await Assert.ThrowsAsync<InvalidOperationException>(
                () => PreparerLaBase.ExecuterAsync(neuve.ChaineMigrations, dossier.FullName, TextWriter.Null, CancellationToken.None)
            );

            Assert.Contains("99-casse.sql", refus.Message, StringComparison.Ordinal);
            Assert.Contains("42P01", refus.Message, StringComparison.Ordinal);
        }
        finally
        {
            dossier.Delete(recursive: true);
        }
    }

    /// <summary>Le dossier du dépôt, lu et jamais recopié.</summary>
    private string Referentiel() => Path.Combine(baseDeDonnees.Racine, "db", "referentiel");

    /// <summary>
    /// Une base vide, possédée par <c>palier_migrations</c> comme la base du
    /// produit, détruite à la fin de l'épreuve.
    /// </summary>
    private sealed class BaseNeuve : IAsyncDisposable
    {
        private readonly BaseFixture _fixture;
        private readonly string _nom;

        private BaseNeuve(BaseFixture fixture, string nom)
        {
            _fixture = fixture;
            _nom = nom;
            ChaineMigrations = new NpgsqlConnectionStringBuilder(fixture.ChaineMigrations)
            {
                Database = nom,
                Pooling = false,
            }.ConnectionString;
        }

        public string ChaineMigrations { get; }

        public static async Task<BaseNeuve> CreerAsync(BaseFixture fixture)
        {
            var nom = "palier_preparation_" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)[..12];
            await AdministrerAsync(fixture, $"create database {nom} owner palier_migrations");
            return new BaseNeuve(fixture, nom);
        }

        public async Task<long> CompterAsync(string requete)
        {
            await using var connexion = new NpgsqlConnection(ChaineMigrations);
            await connexion.OpenAsync();
#pragma warning disable CA2100 // Le SQL vient de littéraux de ce fichier.
            await using var commande = new NpgsqlCommand(requete, connexion);
#pragma warning restore CA2100
            return Convert.ToInt64(await commande.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
        }

        public async ValueTask DisposeAsync() =>
            await AdministrerAsync(_fixture, $"drop database if exists {_nom} with (force)");

        private static async Task AdministrerAsync(BaseFixture fixture, string instruction)
        {
            await using var connexion = new NpgsqlConnection(fixture.ChaineAdministrateur);
            await connexion.OpenAsync();
#pragma warning disable CA2100 // Le nom de base est engendré ici, jamais reçu.
            await using var commande = new NpgsqlCommand(instruction, connexion);
#pragma warning restore CA2100
            await commande.ExecuteNonQueryAsync();
        }
    }
}
