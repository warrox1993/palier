// Point d'entrée de l'API. Il ne fait plus que trois choses : construire,
// composer, router.
//
// La composition vit dans `Composition.cs` pour une raison mesurable : l'épreuve
// qui vérifie que la configuration de PRODUCTION n'active pas
// `EnableSensitiveDataLogging` doit construire la configuration RÉELLE. Recopiée
// dans l'épreuve, elle éprouverait la copie.
//
// Le `app.MapGet("/", () => "Hello World!")` produit par `dotnet new webapi` a
// été retiré au lot 1. Un résidu de génération finit en production si personne
// ne l'enlève, et celui-ci répondait en clair sur la racine du domaine.
using Palier.Api;
using Palier.Api.Outils;
using Palier.Infrastructure.Coffre;

// La commande d'exploitation passe AVANT la construction de l'hôte : elle pose
// la première clé de données, et le démarrage normal en dépend. Elle lit sa
// chaîne dans l'environnement et non dans le coffre — au premier passage,
// aucune clé n'existe, donc le fournisseur de configuration refuserait.
if (args is [PoserUneCleDeDonnees.Nom, ..])
{
    return await PoserUneCleDeDonnees.DepuisLEnvironnementAsync().ConfigureAwait(false);
}

var builder = WebApplication.CreateBuilder(args);

// Le coffre, AVANT la composition : les chaînes de connexion et la clé de
// signature en viennent, et `Composer` les lit. L'API refuse de démarrer si le
// coffre est injoignable, si un secret manque ou si l'environnement n'a pas de
// chemin — c'est délibéré, un démarrage sans coffre serait un démarrage sans
// configuration.
builder.Configuration.AjouterLeCoffre(builder.Environment.EnvironmentName);

Composition.Composer(builder);

var app = builder.Build();

Composition.Router(app);

// Étapes 6 et 7 : le trousseau, chargé avant que le port s'ouvre. Une table de
// clés vide, ou une enveloppe que le coffre refuse, arrêtent ici.
await app.Services.GetRequiredService<AmorcageDuTrousseau>()
    .ChargerAsync(CancellationToken.None)
    .ConfigureAwait(false);

await app.RunAsync().ConfigureAwait(false);

return 0;
