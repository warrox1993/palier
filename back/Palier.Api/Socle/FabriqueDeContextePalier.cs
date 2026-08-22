using Microsoft.EntityFrameworkCore;
using Palier.Infrastructure;

namespace Palier.Api;

/// <summary>
/// De quoi construire un <see cref="PalierDbContext" /> hors d'une requête.
///
/// Le chargement du trousseau s'exécute AVANT que le port s'ouvre : il n'a donc
/// aucune portée de requête où prendre un contexte. Cette fabrique lui en donne
/// un, et `ArchitectureTests` la voit — D59 a ajouté
/// <c>IDbContextFactory&lt;&gt;</c> aux voies surveillées, précisément pour que
/// ce chemin ne passe pas inaperçu.
/// </summary>
internal sealed class FabriqueDeContextePalier(DbContextOptions<PalierDbContext> options)
    : IDbContextFactory<PalierDbContext>
{
    public PalierDbContext CreateDbContext() => new(options);
}
