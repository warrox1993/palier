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

var builder = WebApplication.CreateBuilder(args);

Composition.Composer(builder);

var app = builder.Build();

Composition.Router(app);

app.Run();
