// Point d'entrée de l'API. Aucune route n'est encore exposée : elles arrivent
// avec la tâche 9 du lot 2 (`GET /api/v1/sante`) puis avec le lot 4.
//
// Le `app.MapGet("/", () => "Hello World!")` produit par `dotnet new webapi` a
// été retiré. Un résidu de génération finit en production si personne ne
// l'enlève, et celui-ci répondait en clair sur la racine du domaine.
using Microsoft.EntityFrameworkCore;
using Palier.Application.Pipeline;
using Palier.Infrastructure;
using Palier.Infrastructure.Pipeline;

var builder = WebApplication.CreateBuilder(args);

// La chaîne de l'API est celle du rôle RESTREINT — `palier_app` — jamais celle
// des migrations et jamais celle de l'administrateur. C'est la condition sans
// laquelle RLS ne mord pas du tout : « Table owners normally bypass row
// security as well ».
//
// Elle vient de la configuration, donc d'une variable d'environnement, jamais
// d'un fichier du dépôt. Absente, on lève ICI, en nommant la clé : sans cette
// garde, `UseNpgsql(null)` échoue plus loin avec un message qui ne dit pas quoi
// remplir. Les trois assertions de démarrage contre la base réelle (D37)
// appartiennent à la tâche 9 ; celle-ci ne les remplace pas.
var chaine =
    builder.Configuration.GetConnectionString("Palier")
    ?? throw new InvalidOperationException(
        "ConnectionStrings__Palier est absente. L'API se connecte sous le rôle "
            + "restreint `palier_app` ; voir db/README.md § Appliquer."
    );

builder.Services.AddDbContext<PalierDbContext>(options => options.UseNpgsql(chaine));

// Le pipeline. `IExecuteurDeCasDUsage` est le SEUL chemin par lequel un cas
// d'usage touche la base : il ouvre la transaction et y pose l'identité.
builder.Services.AddScoped<IExecuteurDeCasDUsage, ExecuteurDeCasDUsage>();

// Tant que l'authentification n'existe pas (lot 4), le demandeur ne rend jamais
// d'identité — et tout cas d'usage échoue donc bruyamment, en se nommant.
builder.Services.AddScoped<IIdentiteDemandeur, DemandeurSansIdentite>();

var app = builder.Build();

app.Run();
