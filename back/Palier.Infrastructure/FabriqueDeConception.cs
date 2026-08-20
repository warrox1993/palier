using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Palier.Infrastructure;

/// <summary>
/// Ce que `dotnet ef` appelle pour construire le modèle hors de l'application.
///
/// La chaîne vient de la variable d'environnement <c>ConnectionStrings__PalierMigrations</c>
/// — le nom que `docs/16-projet.md` § 3 et D37 donnent à la chaîne du rôle
/// propriétaire. Elle n'est JAMAIS écrite dans un fichier du dépôt : la règle
/// `chaine-connexion-postgres` de `.gitleaks.regles.toml` refuse un
/// une chaîne complète, mot de passe compris. Le motif n'est pas recopié ici :
/// écrit en toutes lettres, il déclenchait la règle sur ce fichier même —
/// mesuré le 20/08/2026, et corrigé en ne l'écrivant pas plutôt qu'en
/// élargissant l'exception, qui aurait rendu la règle aveugle sur tout le
/// dossier.
///
/// Sans la variable, on retombe sur une chaîne de CONCEPTION, sans mot de passe :
/// `dotnet ef migrations add` n'ouvre aucune connexion, il ne fait que
/// construire le modèle. `dotnet ef database update`, lui, échoue alors — en
/// disant quelle variable manque, plutôt qu'en tentant une connexion anonyme
/// dont le message ne nommerait rien.
/// </summary>
public sealed class FabriqueDeConception : IDesignTimeDbContextFactory<PalierDbContext>
{
    private const string _variable = "ConnectionStrings__PalierMigrations";

    public PalierDbContext CreateDbContext(string[] args)
    {
        var chaine =
            Environment.GetEnvironmentVariable(_variable)
            ?? "Host=absent;Database=palier;Username=palier_migrations";

        var options = new DbContextOptionsBuilder<PalierDbContext>()
            .UseNpgsql(chaine)
            .Options;

        return new PalierDbContext(options);
    }
}
