using Npgsql;
using Palier.Infrastructure.Coffre;

namespace Palier.Api.Outils;

/// <summary>
/// La commande qui pose une clé de données — D59, § 7 de la conception.
///
/// Poser une clé est un ACTE D'EXPLOITATION, pas un effet du démarrage. Elle se
/// range à côté de <c>db/amorcage/01-roles.sql</c> : la base ne se crée pas
/// toute seule, les rôles non plus, la première clé non plus.
///
/// Rejouée, elle AJOUTE une clé sans toucher aux précédentes — c'est la
/// rotation. Les nouveaux secrets utiliseront la nouvelle ; les anciens
/// resteront lisibles, puisque chaque valeur chiffrée nomme la clé qui l'a
/// produite.
///
/// Elle écrit sous <c>palier_migrations</c>, seul rôle qui en ait le droit :
/// <c>palier_app</c> n'a que <c>select</c> sur cette table, et aucune politique
/// d'écriture ne le concerne.
/// </summary>
internal static class PoserUneCleDeDonnees
{
    /// <summary>Le nom que <c>Program</c> reconnaît sur la ligne de commande.</summary>
    internal const string Nom = "poser-cle-de-donnees";

    /// <summary>
    /// Le chemin de la ligne de commande : il assemble ce que
    /// <see cref="ExecuterAsync" /> reçoit déjà construit dans les épreuves.
    /// Il tient en six lignes pour cette raison — tout ce qui se juge vit dans
    /// la méthode éprouvée, et non ici.
    /// </summary>
    internal static async Task<int> DepuisLEnvironnementAsync()
    {
        var amorcage = new ConfigurationBuilder().AddEnvironmentVariables().Build();
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };

        return await ExecuterAsync(
                amorcage.GetConnectionString("PalierMigrations")
                    ?? throw new InvalidOperationException(
                        "ConnectionStrings__PalierMigrations est absente : la commande "
                            + "écrit sous le rôle propriétaire, elle ne peut pas s'en passer."
                    ),
                new ClientOkms(http, ReglagesDuCoffre.Depuis(amorcage)),
                CancellationToken.None
            )
            .ConfigureAwait(false);
    }

    internal static async Task<int> ExecuterAsync(
        string chaineDeMigrations,
        ClientOkms client,
        CancellationToken jeton
    )
    {
        ArgumentNullException.ThrowIfNull(client);

        var (enveloppe, clair) = await client.CreerUneCleDeDonneesAsync(jeton).ConfigureAwait(false);

        // La forme claire n'a AUCUN usage ici : la commande ne chiffre rien,
        // elle range une enveloppe. On l'efface plutôt que de la laisser
        // traîner dans un tas que le ramasse-miettes videra quand il voudra.
        Array.Clear(clair);

        var connexion = new NpgsqlConnection(chaineDeMigrations);
        await using var _ = connexion.ConfigureAwait(false);
        await connexion.OpenAsync(jeton).ConfigureAwait(false);

        var commande = new NpgsqlCommand(
            "insert into public.cles_de_donnees (id, enveloppe, creee_le) "
                + "values ($1, $2, now())",
            connexion
        );
        await using var __ = commande.ConfigureAwait(false);

        var id = Guid.NewGuid();
        commande.Parameters.AddWithValue(id);
        commande.Parameters.AddWithValue(enveloppe);
        await commande.ExecuteNonQueryAsync(jeton).ConfigureAwait(false);

        // L'identifiant, jamais l'enveloppe : elle n'est pas secrète, mais un
        // journal qui la recopie apprend à la relire.
        Console.WriteLine($"Clé de données posée : {id:D}");
        return 0;
    }
}
