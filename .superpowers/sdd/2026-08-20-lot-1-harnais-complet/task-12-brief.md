# Brief — Tâche 12

> Extrait de `docs/superpowers/plans/2026-08-20-lot-1-harnais-complet.md`. C'est la source unique de tes exigences.
> Les valeurs exactes — code, chemins, chaînes de caractères — se reprennent **verbatim**.

## Contraintes globales

- **Node 24** (`.nvmrc`) et **.NET 10** (`global.json`, SDK 10.0.303, `rollForward: latestFeature`).
- **npm** côté front, jamais pnpm ni yarn. En CI : `npm ci`, jamais `npm install`.
- **Aucune version de paquet figée dans ce plan.** Les installations se font sans numéro ; les fichiers de verrouillage figent.
- **Toute dépendance ajoutée doit passer la liste blanche de licences** — MIT, Apache-2.0, BSD, ISC, PostgreSQL. Décision D13 du journal. MediatR est exclu : sa licence RPL-1.5 obligerait à publier le code source d'un service commercial.
- **Messages de commit en français, à l'impératif** — `16-projet.md` § 2.
- **Nommage front** : fichiers `kebab-case.ts`, composants `PascalCase.tsx`, fonctions et variables `camelCase`, constantes `SCREAMING_SNAKE_CASE`, booléens en `is`/`has`/`can`.
- **Nommage C#** : fichiers et types en `PascalCase`, un type public par fichier, champs privés en `_camelCase`.
- **Tests à côté du source** côté front — `<fichier>.test.ts`.
- **Aucun secret dans le dépôt.** `.env.example` versionné, `.env` ignoré.
- **`front/tests/harness/fixtures/` et `back/tests-harness/fixtures/`** hébergent les violations délibérées, exclues du typecheck et du build.
- **`Palier.Domain` ne référence aucun projet** et aucun paquet d'accès aux données, réseau ou UI. Traduction de `01-conformite.md` § 3.
- **Couverture 100 % sur `Palier.Domain`** — `08-workflow.md` § 6. Aucun seuil global ailleurs.
- **Une seule branche de travail** : `feat/lot-1-harnais`, déjà active.
- Dépôt distant : `github.com/warrox1993/palier`, privé, branche par défaut `main`.

---

## Tâche 12 : Rigueur globale — `Directory.Build.props`

**Fichiers :**
- Créer : `Directory.Build.props` (racine), `.editorconfig` (racine), `back/tests-harness/fixtures/Nullable.cs`, `back/tests-harness/rigueur.test.mjs`

**Interfaces :**
- Consomme : la solution de la tâche 2
- Produit : la rigueur appliquée à tous les projets, présents et futurs.

- [ ] **Étape 1 : écrire l'épreuve**

`back/tests-harness/rigueur.test.mjs` :

```javascript
import { existsSync } from 'node:fs'
import { describe, expect, it } from 'vitest'
import { lancerOutil } from '../../front/tests/harness/run-outil.js'

const FIXTURE = 'back/tests-harness/fixtures/Nullable.cs'

describe('garde-fou : rigueur du compilateur', () => {
  it('la fixture de violation existe', () => {
    expect(existsSync(FIXTURE), `Cible manquante : ${FIXTURE}`).toBe(true)
  })

  it('refuse un déréférencement possiblement nul', () => {
    const r = lancerOutil(['dotnet', 'build', 'back/tests-harness/fixtures/Fixtures.csproj'])
    expect(r.code, `Le compilateur a accepté la violation :\n${r.sortie}`).not.toBe(0)
    expect(r.sortie).toMatch(/CS8600|CS8602|CS8604/)
  })
})
```

- [ ] **Étape 2 : lancer l'épreuve pour la voir échouer**

Attendu : ÉCHEC — ni le fichier de rigueur ni la fixture n'existent.

- [ ] **Étape 3 : écrire `Directory.Build.props`**

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <ImplicitUsings>enable</ImplicitUsings>

    <!-- L'équivalent de "strict" en TypeScript -->
    <Nullable>enable</Nullable>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <WarningsNotAsErrors></WarningsNotAsErrors>

    <!-- Analyseurs de code : l'équivalent côté .NET de ce qu'Oxlint fait au front -->
    <EnableNETAnalyzers>true</EnableNETAnalyzers>
    <AnalysisLevel>latest-all</AnalysisLevel>
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>

    <!-- Reproductibilité et traçabilité -->
    <Deterministic>true</Deterministic>
    <ContinuousIntegrationBuild Condition="'$(CI)' == 'true'">true</ContinuousIntegrationBuild>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <NoWarn>$(NoWarn);CS1591</NoWarn>
  </PropertyGroup>
</Project>
```

`CS1591` (commentaire XML manquant) est la seule exception : `GenerateDocumentationFile` est activé pour que les analyseurs voient les signatures publiques, pas pour imposer un commentaire sur chaque membre.

- [ ] **Étape 4 : écrire `.editorconfig`**

```ini
root = true

[*]
charset = utf-8
end_of_line = lf
insert_final_newline = true
indent_style = space
trim_trailing_whitespace = true

[*.cs]
indent_size = 4
csharp_new_line_before_open_brace = all
csharp_style_namespace_declarations = file_scoped:error
dotnet_style_require_accessibility_modifiers = always:error
dotnet_diagnostic.IDE0005.severity = error

# Nommage — conventions .NET
dotnet_naming_rule.types_pascal.symbols = types
dotnet_naming_rule.types_pascal.style = pascal
dotnet_naming_rule.types_pascal.severity = error
dotnet_naming_symbols.types.applicable_kinds = class,struct,enum,property,method
dotnet_naming_style.pascal.capitalization = pascal_case

dotnet_naming_rule.champs_prives.symbols = champs_prives
dotnet_naming_rule.champs_prives.style = underscore_camel
dotnet_naming_rule.champs_prives.severity = error
dotnet_naming_symbols.champs_prives.applicable_kinds = field
dotnet_naming_symbols.champs_prives.applicable_accessibilities = private
dotnet_naming_style.underscore_camel.required_prefix = _
dotnet_naming_style.underscore_camel.capitalization = camel_case

[*.{ts,tsx,js,jsx,json,md,yml,yaml}]
indent_size = 2
```

- [ ] **Étape 5 : créer la fixture et son projet isolé**

`back/tests-harness/fixtures/Fixtures.csproj` :

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>
</Project>
```

Ce projet n'est **pas** ajouté à `Palier.sln` : il ne doit jamais être compilé par le build normal.

`back/tests-harness/fixtures/Nullable.cs` :

```csharp
namespace Fixtures;

// Violation délibérée : déréférencement d'une référence possiblement nulle.
// Ce projet est hors de Palier.sln et n'est compilé que par son épreuve.
public static class Nullable
{
    public static int Longueur(string? valeur)
    {
        return valeur.Length;
    }
}
```

- [ ] **Étape 6 : lancer l'épreuve pour la voir passer**

Attendu : 2 tests passent — le compilateur refuse `valeur.Length` sur un `string?`.

- [ ] **Étape 7 : vérifier que la solution reste compilable**

```bash
dotnet build back/Palier.sln
```

Attendu : compilation réussie, **zéro avertissement**. Si les analyseurs en remontent sur le code généré par les templates, les corriger — ne jamais les faire taire globalement.

- [ ] **Étape 8 : commit**

```bash
git add -A
git commit -m "impose la rigueur du compilateur et les conventions de style"
```

---

---
