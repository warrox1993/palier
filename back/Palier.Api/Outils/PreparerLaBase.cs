using Microsoft.EntityFrameworkCore;
using Npgsql;
using Palier.Infrastructure;

namespace Palier.Api.Outils;

/// <summary>
/// La commande qui met une base au niveau du code : les migrations, puis le
/// référentiel — D81.
/// </summary>
/// <remarks>
/// <para>
/// C'est un ACTE D'EXPLOITATION, rangé à côté de <c>poser-cle-de-donnees</c>, et
/// non un effet du démarrage de l'API. La raison se lit dans les rôles : les
/// migrations et le référentiel s'écrivent sous <c>palier_migrations</c>,
/// propriétaire des tables, alors que l'API se connecte sous <c>palier_app</c>,
/// qui ne possède rien. Migrer au démarrage obligerait le processus qui sert les
/// requêtes à détenir la chaîne du propriétaire — exactement ce que D37 sépare.
/// </para>
///
/// <para>
/// La démonstration conteneurisée la lance dans un conteneur à part, qui se
/// termine avant que l'API démarre. Sur un poste, <c>dotnet ef database update</c>
/// et <c>npm run db:referentiel</c> restent les deux gestes documentés ; cette
/// commande fait les deux, dans le même ordre, sans exiger ni <c>dotnet-ef</c>
/// ni <c>psql</c> dans l'image.
/// </para>
///
/// <para>
/// <b>Rejouable.</b> Une migration déjà appliquée ne se rejoue pas, et chaque
/// fichier du référentiel est idempotent par construction (<c>on conflict</c> sur
/// le slug, D68). Relancer la démonstration ne duplique rien.
/// </para>
/// </remarks>
internal static class PreparerLaBase
{
    /// <summary>Le nom que <c>Program</c> reconnaît sur la ligne de commande.</summary>
    internal const string Nom = "preparer-la-base";

    /// <summary>Le dossier des fichiers de référentiel, lu dans l'environnement.</summary>
    internal const string CleDuReferentiel = "PALIER_REFERENTIEL";

    /// <summary>
    /// Le chemin de la ligne de commande : il lit les deux réglages dans
    /// l'environnement, puis délègue. Tout ce qui se juge vit dans
    /// <see cref="DepuisLaConfigurationAsync" /> et <see cref="ExecuterAsync" />.
    /// </summary>
    internal static Task<int> DepuisLEnvironnementAsync() =>
        DepuisLaConfigurationAsync(
            new ConfigurationBuilder().AddEnvironmentVariables().Build(),
            Console.Out,
            CancellationToken.None
        );

    /// <summary>Lit les deux réglages, et refuse en nommant celui qui manque.</summary>
    internal static async Task<int> DepuisLaConfigurationAsync(
        IConfiguration configuration,
        TextWriter sortie,
        CancellationToken jeton
    )
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return await ExecuterAsync(
                configuration.GetConnectionString("PalierMigrations")
                    ?? throw new InvalidOperationException(
                        "ConnectionStrings__PalierMigrations est absente : les migrations et le "
                            + "référentiel s'écrivent sous le rôle propriétaire."
                    ),
                configuration[CleDuReferentiel]
                    ?? throw new InvalidOperationException(
                        $"{CleDuReferentiel} est absente : elle nomme le dossier des fichiers "
                            + "`db/referentiel/*.sql`."
                    ),
                sortie,
                jeton
            )
            .ConfigureAwait(false);
    }

    internal static async Task<int> ExecuterAsync(
        string chaineDeMigrations,
        string dossierDuReferentiel,
        TextWriter sortie,
        CancellationToken jeton
    )
    {
        ArgumentNullException.ThrowIfNull(sortie);

        // Les fichiers sont cherchés AVANT de migrer : un dossier vide ou mal
        // nommé refuse sans avoir rien touché, au lieu de laisser une base
        // migrée sans catalogue, qui démarre et répond des listes vides.
        var fichiers = Directory.Exists(dossierDuReferentiel)
            ? Directory
                .GetFiles(dossierDuReferentiel, "*.sql")
                .OrderBy(chemin => Path.GetFileName(chemin), StringComparer.Ordinal)
                .ToArray()
            : [];

        if (fichiers.Length == 0)
        {
            throw new InvalidOperationException(
                $"Aucun fichier de référentiel dans « {dossierDuReferentiel} ». C'est la cible de "
                    + "cette commande ; une cible absente se signale au lieu de se remplacer."
            );
        }

        // 1. Les migrations, sous le propriétaire — comme `BaseFixture`, et pour
        // la même raison : c'est ce qui fait de `palier_migrations` le
        // propriétaire des tables, donc ce qui rend `force row level security`
        // observable.
        var options = new DbContextOptionsBuilder<PalierDbContext>()
            .UseNpgsql(chaineDeMigrations)
            .Options;
        var contexte = new PalierDbContext(options);
        await using (contexte.ConfigureAwait(false))
        {
            await contexte.Database.MigrateAsync(jeton).ConfigureAwait(false);
        }

        await sortie.WriteLineAsync("Migrations appliquées.").ConfigureAwait(false);

        // 2. Le référentiel, fichier par fichier, dans l'ordre de leur préfixe
        // — celui de `scripts/referentiel.mjs`. Chaque fichier part en une
        // commande : un refus arrête tout et nomme le fichier.
        var connexion = new NpgsqlConnection(chaineDeMigrations);
        await using (connexion.ConfigureAwait(false))
        {
            await connexion.OpenAsync(jeton).ConfigureAwait(false);

            foreach (var chemin in fichiers)
            {
                var sql = await File.ReadAllTextAsync(chemin, jeton).ConfigureAwait(false);

#pragma warning disable CA2100 // Le SQL vient des fichiers de référentiel du DÉPÔT, versionnés, jamais d'une entrée.
                var commande = new NpgsqlCommand(sql, connexion);
#pragma warning restore CA2100
                await using (commande.ConfigureAwait(false))
                {
                    try
                    {
                        await commande.ExecuteNonQueryAsync(jeton).ConfigureAwait(false);
                    }
                    catch (PostgresException refus)
                    {
                        // Le code SQLSTATE et le fichier, pas le message : il
                        // recopie l'instruction fautive, et un fichier de
                        // référentiel en compte des centaines de lignes.
                        throw new InvalidOperationException(
                            $"Le fichier « {Path.GetFileName(chemin)} » a été refusé par le "
                                + $"moteur (SQLSTATE {refus.SqlState}).",
                            refus
                        );
                    }
                }

                await sortie
                    .WriteLineAsync($"Référentiel appliqué : {Path.GetFileName(chemin)}")
                    .ConfigureAwait(false);
            }
        }

        return 0;
    }
}
