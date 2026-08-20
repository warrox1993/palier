// Point d'entrée de l'API. Aucune route n'est encore exposée : elles arrivent
// avec le lot 2, une fois le schéma et l'authentification posés.
//
// Le `app.MapGet("/", () => "Hello World!")` produit par `dotnet new webapi` a
// été retiré. Un résidu de génération finit en production si personne ne
// l'enlève, et celui-ci répondait en clair sur la racine du domaine.
var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.Run();
