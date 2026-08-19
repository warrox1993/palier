# Lot 1b — Harnais backend .NET — Plan d'implémentation

> **Pour les exécutants agentiques :** SOUS-SKILL REQUISE — utiliser `superpowers:subagent-driven-development` (recommandé) ou `superpowers:executing-plans` pour exécuter ce plan tâche par tâche. Les étapes utilisent la syntaxe à cases (`- [ ]`) pour le suivi.

**But :** installer le harnais de qualité du backend C# et prouver que chacun de ses garde-fous refuse effectivement ce qu'il prétend refuser — en particulier celui qui rend un calcul de conformité incapable d'atteindre la base ou le réseau.

**Architecture :** solution .NET à quatre projets suivant les dépendances de la Clean Architecture. `Palier.Domain` ne référence rien : c'est une impossibilité de compilation, non une convention. La rigueur est portée par un `Directory.Build.props` unique appliqué à tous les projets. Chaque garde-fou suit le cycle rouge-vert : on écrit d'abord l'épreuve qui provoque la violation, on la voit passer faute de garde-fou, on installe le garde-fou, l'épreuve constate le refus.

**Pile technique :** .NET 10.0.303, xUnit, coverlet, analyseurs Roslyn, `dotnet format`, Docker 29.6.2, GitHub Actions.

**Spec :** `docs/superpowers/specs/2026-08-19-architecture-backend-csharp-design.md`

## Contraintes globales

- **.NET 10** — SDK 10.0.303 installé. Verrouillé par `global.json` avec `rollForward: latestFeature`.
- **Aucune version de paquet figée dans ce plan.** Les templates `dotnet new` choisissent ce qui est cohérent avec le SDK ; les fichiers de verrouillage figent.
- **Toute dépendance ajoutée doit passer la liste blanche de licences** : MIT, Apache-2.0, BSD-2-Clause, BSD-3-Clause, ISC, PostgreSQL. Décision D12/D13 du journal — MediatR est exclu, sa licence RPL-1.5 obligerait à publier le code source d'un service commercial.
- **Messages de commit en français, à l'impératif.**
- **Nommage C#** — fichiers et types en `PascalCase`, un type public par fichier, pas de préfixe `I` sur les classes (mais `I` sur les interfaces, convention .NET), champs privés en `_camelCase`.
- **Nommage front inchangé** — `kebab-case.ts`, `PascalCase.tsx`, conformément à `16-projet.md` § 2.
- **`Palier.Domain` ne référence aucun projet et aucun paquet d'accès aux données, réseau ou UI.** C'est la traduction de `01-conformite.md` § 3.
- **Couverture 100 % sur `Palier.Domain`** — `08-workflow.md` § 6.
- **Un seul point d'entrée de vérification :** `npm run verify` à la racine, qui enchaîne front puis backend. C'est la seule commande que le hook de pré-envoi et la CI appellent.
- **Branche de travail :** `feat/lot-1b-harnais-backend`, créée depuis `main` après fusion du lot 1a, ou depuis l'état courant si le lot 1a n'est pas fusionné.

---

## Structure des fichiers

| Fichier | Responsabilité |
|---|---|
| `global.json` | version du SDK .NET, unique source de vérité |
| `Directory.Build.props` | rigueur appliquée à **tous** les projets : nullabilité, avertissements en erreurs, analyseurs |
| `.editorconfig` | règles de style et de nommage, appliquées par `dotnet format` |
| `back/Palier.sln` | la solution |
| `back/Palier.Domain/` | entités, invariants, calculs purs — **aucune référence** |
| `back/Palier.Application/` | cas d'usage, ports |
| `back/Palier.Infrastructure/` | accès données, adaptateurs |
| `back/Palier.Api/` | exposition HTTP |
| `back/Palier.Domain.Tests/` | couverture 100 % |
| `back/Palier.Api.Tests/` | tests d'intégration |
| `back/tests-harness/` | les épreuves de franchissement du backend |
| `scripts/verifier-licences.mjs` | contrôle des licences, npm **et** NuGet |
| `scripts/verify.mjs` | point d'entrée unique, front puis backend |
| `front/` | l'application React, déplacée depuis la racine |

---

## Tâche 1 : Réorganiser en `front/` et `back/`

**Fichiers :**
- Déplacer : `package.json`, `package-lock.json`, `tsconfig*.json`, `vite.config.ts`, `vitest.config.ts`, `index.html`, `src/`, `tests/`, `.prettierrc`, `.prettierignore`, `eslint.config.js` (s'il existe) → `front/`
- Modifier : `.gitignore`, `.husky/pre-commit`, `.husky/pre-push`
- Créer : `package.json` à la racine (orchestration seulement)

**Interfaces :**
- Consomme : le harnais front du lot 1a
- Produit : `npm run verify` à la racine, qui délègue au front. Les scripts front restent inchangés **dans** `front/`.

> **Pourquoi cette tâche est la plus risquée du lot.** Elle ne crée rien et casse potentiellement tout : chaque chemin relatif du harnais existant doit continuer à résoudre. Elle est isolée en première position pour que sa revue porte sur elle seule.

- [ ] **Étape 1 : créer la branche et constater l'état vert AVANT de bouger quoi que ce soit**

```bash
git checkout -b feat/lot-1b-harnais-backend
npm run typecheck && npm run build && npm run test
```

Attendu : les trois passent. Noter leur sortie — c'est la référence à retrouver à l'étape 6. Si l'un échoue **avant** le déplacement, arrêter et signaler : on ne réorganise pas sur un socle cassé.

- [ ] **Étape 2 : déplacer avec git, pour que l'historique suive**

```bash
mkdir -p front
git mv package.json package-lock.json tsconfig.json tsconfig.node.json front/
git mv vite.config.ts vitest.config.ts index.html front/
git mv src tests front/
git mv .prettierrc .prettierignore front/ 2>/dev/null || true
[ -f eslint.config.js ] && git mv eslint.config.js front/
[ -f .env.example ] && git mv .env.example front/
git status
```

`git mv` plutôt que `mv` : l'historique de chaque fichier reste attaché, et `git log --follow` continue de fonctionner.

- [ ] **Étape 3 : créer le `package.json` racine, qui n'orchestre que**

```json
{
  "name": "palier",
  "version": "0.1.0",
  "private": true,
  "license": "UNLICENSED",
  "description": "Application de suivi de musculation et de nutrition, pour éviter les excès et les blessures.",
  "type": "module",
  "engines": { "node": "24.x" },
  "scripts": {
    "verify": "node scripts/verify.mjs",
    "front": "npm --prefix front run",
    "front:dev": "npm --prefix front run dev"
  }
}
```

Ce fichier ne porte **aucune dépendance** : les dépendances du front restent dans `front/package.json`.

- [ ] **Étape 4 : corriger les hooks, qui pointent encore vers la racine**

`.husky/pre-commit` :

```bash
npx --prefix front lint-staged
npx gitleaks protect --staged --redact --config .gitleaks.toml
```

`.husky/pre-push` :

```bash
npm run verify
```

- [ ] **Étape 5 : corriger `.gitignore`**

Remplacer les chemins racine par leurs équivalents :

```
front/node_modules/
front/dist/
front/coverage/
front/playwright-report/
front/test-results/
back/**/bin/
back/**/obj/
node_modules/
.env
.env.local
*.local
.DS_Store
```

- [ ] **Étape 6 : retrouver l'état vert, exactement**

```bash
npm --prefix front run typecheck
npm --prefix front run build
npm --prefix front run test
```

Attendu : les trois passent, avec le **même nombre de tests** qu'à l'étape 1. Un test qui disparaît sans échouer est le défaut le plus dangereux de cette tâche : le compte doit être identique, pas seulement vert.

- [ ] **Étape 7 : commit**

```bash
git add -A
git commit -m "réorganise le dépôt en front et back"
```

---

## Tâche 2 : Solution .NET et les quatre projets

**Fichiers :**
- Créer : `global.json`, `back/Palier.sln`, les quatre projets, `back/tests-harness/references.test.mjs`

**Interfaces :**
- Consomme : rien
- Produit : la solution `back/Palier.sln`, compilable par `dotnet build back/Palier.sln`. Le graphe de références que toutes les tâches suivantes respectent.

- [ ] **Étape 1 : écrire l'épreuve, avant les projets**

`back/tests-harness/references.test.mjs` :

```javascript
import { readFileSync, existsSync } from 'node:fs'
import { describe, expect, it } from 'vitest'

const CSPROJ = 'back/Palier.Domain/Palier.Domain.csproj'

describe('garde-fou : pureté du domaine', () => {
  it('le projet Domain existe', () => {
    expect(existsSync(CSPROJ), `Cible manquante : ${CSPROJ}`).toBe(true)
  })

  it('Palier.Domain ne référence aucun projet', () => {
    const x = readFileSync(CSPROJ, 'utf8')
    expect(x, 'Domain doit rester sans référence de projet').not.toMatch(/<ProjectReference/)
  })

  it("Palier.Domain ne référence aucun paquet d'accès aux données, réseau ou UI", () => {
    const x = readFileSync(CSPROJ, 'utf8')
    for (const interdit of [
      'EntityFrameworkCore',
      'Npgsql',
      'Microsoft.AspNetCore',
      'System.Net.Http',
      'Dapper',
    ]) {
      expect(x, `Domain ne doit pas référencer ${interdit}`).not.toContain(interdit)
    }
  })
})
```

- [ ] **Étape 2 : lancer l'épreuve pour la voir échouer**

```bash
npx --prefix front vitest run --root .. back/tests-harness/references.test.mjs
```

Attendu : ÉCHEC — le projet n'existe pas.

*Note d'exécution : si l'invocation depuis `front/` est malcommode, ajouter à la tâche 9 un script racine `test:harness` qui lance vitest sur `back/tests-harness/` avec la configuration front. Le chemin exact est un détail d'outillage, pas une exigence.*

- [ ] **Étape 3 : figer la version du SDK**

`global.json` à la racine :

```json
{
  "sdk": {
    "version": "10.0.303",
    "rollForward": "latestFeature"
  }
}
```

`latestFeature` : les correctifs de sécurité passent, un changement de version majeure non.

- [ ] **Étape 4 : créer la solution et les quatre projets**

```bash
cd back
dotnet new sln --name Palier
dotnet new classlib --name Palier.Domain --output Palier.Domain
dotnet new classlib --name Palier.Application --output Palier.Application
dotnet new classlib --name Palier.Infrastructure --output Palier.Infrastructure
dotnet new web --name Palier.Api --output Palier.Api
dotnet sln add Palier.Domain Palier.Application Palier.Infrastructure Palier.Api
```

Supprimer les fichiers `Class1.cs` générés par les templates `classlib` — ce sont des résidus, et le lot 1a a montré que les résidus de génération se retrouvent en production si personne ne les enlève.

- [ ] **Étape 5 : établir le graphe de références**

```bash
cd back
dotnet add Palier.Application reference Palier.Domain
dotnet add Palier.Infrastructure reference Palier.Domain Palier.Application
dotnet add Palier.Api reference Palier.Domain Palier.Application Palier.Infrastructure
```

Aucune commande ne donne de référence à `Palier.Domain`. C'est l'invariant du lot.

- [ ] **Étape 6 : lancer l'épreuve pour la voir passer**

```bash
dotnet build back/Palier.sln
```

Attendu : compilation réussie. Puis relancer l'épreuve : 3 tests passent.

- [ ] **Étape 7 : provoquer la violation, pour vérifier que la barrière tient**

```bash
cd back
dotnet add Palier.Domain reference Palier.Infrastructure
dotnet build Palier.sln
```

Attendu : **échec de compilation** pour référence circulaire. C'est la preuve que la barrière n'est pas décorative. Annuler immédiatement :

```bash
dotnet remove Palier.Domain reference Palier.Infrastructure
dotnet build Palier.sln   # doit repasser au vert
```

Consigner la sortie de l'échec dans le rapport de tâche.

- [ ] **Étape 8 : commit**

```bash
git add -A
git commit -m "crée la solution et les quatre projets du backend"
```

---

## Tâche 3 : Rigueur globale — `Directory.Build.props`

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

    <!-- L'équivalent d'ESLint -->
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

## Tâche 4 : xUnit et la première fonction du domaine

**Fichiers :**
- Créer : `back/Palier.Domain.Tests/`, `back/Palier.Domain/Energie/MetabolismeDeBase.cs`, son test
- Modifier : `back/Palier.sln`

**Interfaces :**
- Consomme : la solution, la rigueur
- Produit : `MetabolismeDeBase.MifflinStJeor(Sexe sexe, decimal poidsKg, decimal tailleCm, int age) → decimal`, consommée par le lot 3.

> Cette tâche installe l'outillage de test **et** la première fonction réelle, parce qu'un harnais de test sans rien à tester ne prouve rien.

- [ ] **Étape 1 : créer le projet de tests**

```bash
cd back
dotnet new xunit --name Palier.Domain.Tests --output Palier.Domain.Tests
dotnet sln add Palier.Domain.Tests
dotnet add Palier.Domain.Tests reference Palier.Domain
```

Supprimer le fichier de test généré par le template.

- [ ] **Étape 2 : écrire le test AVANT l'implémentation**

`back/Palier.Domain.Tests/Energie/MetabolismeDeBaseTests.cs` :

```csharp
using Palier.Domain.Energie;

namespace Palier.Domain.Tests.Energie;

public sealed class MetabolismeDeBaseTests
{
    // docs/04-nutrition.md § 1 :
    // Homme : 10 × poids + 6,25 × taille − 5 × âge + 5
    // Femme : 10 × poids + 6,25 × taille − 5 × âge − 161
    [Fact]
    public void MifflinStJeor_calcule_la_valeur_pour_un_homme()
    {
        var r = MetabolismeDeBase.MifflinStJeor(Sexe.Homme, poidsKg: 73m, tailleCm: 178m, age: 35m);
        // 730 + 1112,5 − 175 + 5 = 1672,5
        Assert.Equal(1672.5m, r);
    }

    [Fact]
    public void MifflinStJeor_calcule_la_valeur_pour_une_femme()
    {
        var r = MetabolismeDeBase.MifflinStJeor(Sexe.Femme, poidsKg: 60m, tailleCm: 165m, age: 30m);
        // 600 + 1031,25 − 150 − 161 = 1320,25
        Assert.Equal(1320.25m, r);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void MifflinStJeor_refuse_un_poids_non_positif(int poids)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => MetabolismeDeBase.MifflinStJeor(Sexe.Homme, poids, 178m, 35m));
    }
}
```

`decimal` et non `double` : `16-projet.md` § 2 interdit le flottant pour les quantités nutritionnelles, et la raison vaut aussi pour les calculs intermédiaires.

- [ ] **Étape 3 : lancer le test pour le voir échouer**

```bash
dotnet test back/Palier.Domain.Tests
```

Attendu : ÉCHEC de compilation — `MetabolismeDeBase` et `Sexe` n'existent pas.

- [ ] **Étape 4 : écrire l'implémentation minimale**

`back/Palier.Domain/Energie/Sexe.cs` :

```csharp
namespace Palier.Domain.Energie;

public enum Sexe
{
    Homme,
    Femme,
}
```

`back/Palier.Domain/Energie/MetabolismeDeBase.cs` :

```csharp
namespace Palier.Domain.Energie;

/// <summary>
/// Métabolisme de base. Voir docs/04-nutrition.md § 1.
/// Module pur : aucun accès réseau, base ou interface.
/// </summary>
public static class MetabolismeDeBase
{
    public static decimal MifflinStJeor(Sexe sexe, decimal poidsKg, decimal tailleCm, decimal age)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(poidsKg);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(tailleCm);
        ArgumentOutOfRangeException.ThrowIfNegative(age);

        var socle = (10m * poidsKg) + (6.25m * tailleCm) - (5m * age);
        return sexe switch
        {
            Sexe.Homme => socle + 5m,
            Sexe.Femme => socle - 161m,
            _ => throw new ArgumentOutOfRangeException(nameof(sexe)),
        };
    }
}
```

- [ ] **Étape 5 : lancer le test pour le voir passer**

```bash
dotnet test back/Palier.Domain.Tests
```

Attendu : 4 tests passent (2 faits + 2 cas de théorie).

- [ ] **Étape 6 : éprouver que le harnais de test mord**

Ajouter temporairement un test qui échoue, vérifier que `dotnet test` sort en code non nul, puis le retirer. Consigner la sortie dans le rapport.

- [ ] **Étape 7 : commit**

```bash
git add -A
git commit -m "installe xUnit et le calcul du métabolisme de base"
```

---

## Tâche 5 : Couverture à 100 % sur le domaine

**Fichiers :**
- Modifier : `back/Palier.Domain.Tests/Palier.Domain.Tests.csproj`
- Créer : `back/coverage.runsettings`, `back/tests-harness/couverture.test.mjs`

**Interfaces :**
- Consomme : le projet de tests de la tâche 4
- Produit : `dotnet test --settings back/coverage.runsettings`, en échec sous 100 % sur `Palier.Domain`.

- [ ] **Étape 1 : écrire l'épreuve**

`back/tests-harness/couverture.test.mjs` :

```javascript
import { existsSync, writeFileSync, rmSync } from 'node:fs'
import { describe, expect, it, afterAll } from 'vitest'
import { lancerOutil } from '../../front/tests/harness/run-outil.js'

const NON_TESTE = 'back/Palier.Domain/Energie/NonTeste.cs'

afterAll(() => rmSync(NON_TESTE, { force: true }))

describe('garde-fou : couverture du domaine', () => {
  it('le fichier de configuration existe', () => {
    expect(existsSync('back/coverage.runsettings'), 'Cible manquante').toBe(true)
  })

  it('refuse une fonction du domaine non couverte', () => {
    writeFileSync(
      NON_TESTE,
      `namespace Palier.Domain.Energie;\n\n` +
        `public static class NonTeste\n{\n` +
        `    public static decimal Double(decimal x) => x * 2m;\n}\n`,
    )
    const r = lancerOutil([
      'dotnet', 'test', 'back/Palier.Domain.Tests',
      '--settings', 'back/coverage.runsettings',
    ])
    expect(r.code, `Le seuil de couverture n'a pas mordu :\n${r.sortie}`).not.toBe(0)
  })
})
```

- [ ] **Étape 2 : lancer l'épreuve pour la voir échouer**

Attendu : ÉCHEC — aucun seuil n'est configuré, `dotnet test` passe malgré la fonction non couverte.

- [ ] **Étape 3 : ajouter coverlet au projet de tests**

```bash
cd back
dotnet add Palier.Domain.Tests package coverlet.collector
dotnet add Palier.Domain.Tests package coverlet.msbuild
```

- [ ] **Étape 4 : écrire `back/coverage.runsettings`**

```xml
<?xml version="1.0" encoding="utf-8"?>
<RunSettings>
  <DataCollectionRunSettings>
    <DataCollectors>
      <DataCollector friendlyName="XPlat code coverage">
        <Configuration>
          <Format>cobertura</Format>
          <Include>[Palier.Domain]*</Include>
          <Threshold>100</Threshold>
          <ThresholdType>line,branch,method</ThresholdType>
          <ThresholdStat>total</ThresholdStat>
        </Configuration>
      </DataCollector>
    </DataCollectors>
  </DataCollectionRunSettings>
</RunSettings>
```

Le seuil ne porte que sur `Palier.Domain` : `08-workflow.md` § 6 exige 100 % sur les modules de calcul, et **aucun seuil global** — un chiffre global pousse à tester ce qui est facile.

- [ ] **Étape 5 : lancer l'épreuve pour la voir passer**

Attendu : 2 tests passent — le seuil refuse la fonction non couverte, et la fixture est nettoyée après.

- [ ] **Étape 6 : vérifier que le domaine réel passe le seuil**

```bash
dotnet test back/Palier.Domain.Tests --settings back/coverage.runsettings
```

Attendu : succès, 100 % sur `Palier.Domain`.

- [ ] **Étape 7 : commit**

```bash
git add -A
git commit -m "impose une couverture totale sur le domaine"
```

---

## Tâche 6 : Format et style

**Fichiers :**
- Créer : `back/tests-harness/fixtures/MalFormate.cs.txt`, `back/tests-harness/format.test.mjs`

**Interfaces :**
- Consomme : `.editorconfig` de la tâche 3
- Produit : `dotnet format --verify-no-changes`, appelé par `verify`.

- [ ] **Étape 1 : écrire l'épreuve**

`back/tests-harness/format.test.mjs` :

```javascript
import { copyFileSync, rmSync, existsSync } from 'node:fs'
import { describe, expect, it, afterAll } from 'vitest'
import { lancerOutil } from '../../front/tests/harness/run-outil.js'

const SOURCE = 'back/tests-harness/fixtures/MalFormate.cs.txt'
const CIBLE = 'back/Palier.Domain/MalFormate.cs'

afterAll(() => rmSync(CIBLE, { force: true }))

describe('garde-fou : format du code', () => {
  it('la fixture existe', () => {
    expect(existsSync(SOURCE), `Cible manquante : ${SOURCE}`).toBe(true)
  })

  it('refuse un fichier mal formaté', () => {
    copyFileSync(SOURCE, CIBLE)
    const r = lancerOutil(['dotnet', 'format', 'back/Palier.sln', '--verify-no-changes'])
    expect(r.code, `dotnet format a accepté le fichier :\n${r.sortie}`).not.toBe(0)
  })
})
```

La fixture porte l'extension `.cs.txt` : elle ne doit pas être compilée tant qu'elle n'est pas copiée par l'épreuve.

- [ ] **Étape 2 : lancer l'épreuve pour la voir échouer**

Attendu : ÉCHEC — la fixture n'existe pas.

- [ ] **Étape 3 : créer la fixture**

`back/tests-harness/fixtures/MalFormate.cs.txt` :

```
namespace Palier.Domain;
    public   static class MalFormate {
public static int Valeur( ) {return    1;}
        }
```

- [ ] **Étape 4 : lancer l'épreuve pour la voir passer**

Attendu : 2 tests passent.

- [ ] **Étape 5 : vérifier que le code réel est bien formaté**

```bash
dotnet format back/Palier.sln --verify-no-changes
```

Attendu : succès. Si des écarts apparaissent, lancer `dotnet format back/Palier.sln` puis recommencer.

- [ ] **Étape 6 : commit**

```bash
git add -A
git commit -m "éprouve le contrôle de format du backend"
```

---

## Tâche 7 : Contrôle des licences, npm et NuGet

**Fichiers :**
- Créer : `scripts/verifier-licences.mjs`, `back/tests-harness/licences.test.mjs`

**Interfaces :**
- Consomme : `front/package.json`, les `.csproj` du backend
- Produit : `node scripts/verifier-licences.mjs`, en échec si une dépendance sort de la liste blanche. Décision D13.

> **Pourquoi un script maison.** Dépendre d'un outil tiers dont il faudrait d'abord vérifier la licence, pour vérifier des licences, est circulaire. Ce script n'a aucune dépendance et couvre les deux écosystèmes.

- [ ] **Étape 1 : écrire l'épreuve**

`back/tests-harness/licences.test.mjs` :

```javascript
import { describe, expect, it } from 'vitest'
import { lancerOutil } from '../../front/tests/harness/run-outil.js'

describe('garde-fou : licences des dépendances', () => {
  it('accepte les dépendances actuelles', () => {
    const r = lancerOutil(['node', 'scripts/verifier-licences.mjs'])
    expect(r.code, `Une dépendance actuelle sort de la liste blanche :\n${r.sortie}`).toBe(0)
  })

  it('refuse un paquet sous licence réciproque', () => {
    const r = lancerOutil(['node', 'scripts/verifier-licences.mjs', '--tester', 'MediatR'])
    expect(r.code, `MediatR (RPL-1.5) a été accepté :\n${r.sortie}`).not.toBe(0)
    expect(r.sortie).toMatch(/MediatR/)
  })
})
```

Le second test utilise MediatR comme cas d'école : c'est le paquet qui a motivé cette décision, et sa licence est un fait vérifiable plutôt qu'une fixture inventée.

- [ ] **Étape 2 : lancer l'épreuve pour la voir échouer**

Attendu : ÉCHEC — le script n'existe pas.

- [ ] **Étape 3 : écrire `scripts/verifier-licences.mjs`**

```javascript
#!/usr/bin/env node
// Vérifie que chaque dépendance porte une licence permissive.
// Aucune dépendance : interroge directement les registres npm et NuGet.
// Décision D13 du journal — motivée par le passage de MediatR sous RPL-1.5.
import { readFileSync, readdirSync, statSync } from 'node:fs'
import { join } from 'node:path'

const PERMISES = [
  'MIT', 'Apache-2.0', 'BSD-2-Clause', 'BSD-3-Clause', 'ISC',
  'PostgreSQL', '0BSD', 'Unlicense', 'CC0-1.0', 'MIT-0',
]

const estPermise = (l) =>
  !!l && PERMISES.some((p) => l.toUpperCase().includes(p.toUpperCase()))

async function licenceNpm(nom) {
  const r = await fetch(`https://registry.npmjs.org/${encodeURIComponent(nom)}/latest`)
  if (!r.ok) return null
  const j = await r.json()
  return typeof j.license === 'string' ? j.license : j.license?.type ?? null
}

async function licenceNuget(nom) {
  const r = await fetch(
    `https://azuresearch-usnc.nuget.org/query?q=packageid:${encodeURIComponent(nom)}&prerelease=false`,
  )
  if (!r.ok) return null
  const j = await r.json()
  const d = (j.data ?? [])[0]
  if (!d) return null
  // Une expression SPDX absente signale une licence non standard : à examiner.
  return d.licenseExpression ?? null
}

function paquetsNpm() {
  const p = JSON.parse(readFileSync('front/package.json', 'utf8'))
  return Object.keys({ ...p.dependencies, ...p.devDependencies })
}

function paquetsNuget() {
  const noms = new Set()
  const parcourir = (d) => {
    for (const e of readdirSync(d)) {
      const chemin = join(d, e)
      if (statSync(chemin).isDirectory()) {
        if (e === 'bin' || e === 'obj' || e === 'node_modules') continue
        parcourir(chemin)
      } else if (e.endsWith('.csproj')) {
        const x = readFileSync(chemin, 'utf8')
        for (const m of x.matchAll(/PackageReference\s+Include="([^"]+)"/g)) noms.add(m[1])
      }
    }
  }
  parcourir('back')
  return [...noms]
}

const aTester = process.argv.indexOf('--tester')
const cibles =
  aTester !== -1
    ? [{ nom: process.argv[aTester + 1], source: 'nuget' }]
    : [
        ...paquetsNpm().map((nom) => ({ nom, source: 'npm' })),
        ...paquetsNuget().map((nom) => ({ nom, source: 'nuget' })),
      ]

const refuses = []
for (const { nom, source } of cibles) {
  const licence = source === 'npm' ? await licenceNpm(nom) : await licenceNuget(nom)
  if (!estPermise(licence)) {
    refuses.push(`${source.padEnd(6)} ${nom.padEnd(50)} ${licence ?? '(licence non standard)'}`)
  }
}

if (refuses.length > 0) {
  console.error('Dépendances hors liste blanche :\n')
  for (const l of refuses) console.error('  ' + l)
  console.error(
    `\nListe blanche : ${PERMISES.join(', ')}.\n` +
      "Une licence non standard n'est pas forcément interdite — elle doit être lue avant d'être admise.",
  )
  process.exit(1)
}

console.log(`${cibles.length} dépendances vérifiées, toutes sous licence permissive.`)
```

- [ ] **Étape 4 : lancer l'épreuve pour la voir passer**

Attendu : 2 tests passent — les dépendances actuelles sont acceptées, MediatR est refusé.

> Note : une licence renvoyée comme non standard n'est pas nécessairement interdite. Certains paquets Microsoft déclarent une URL au lieu d'une expression SPDX. Ceux-là doivent être lus une fois, puis inscrits dans une liste d'exceptions **avec le motif** — jamais ajoutés silencieusement à la liste blanche.

- [ ] **Étape 5 : commit**

```bash
git add -A
git commit -m "vérifie les licences des dépendances des deux écosystèmes"
```

---

## Tâche 8 : Vulnérabilités des dépendances

**Fichiers :**
- Créer : `back/tests-harness/vulnerabilites.test.mjs`
- Modifier : `package.json` racine

**Interfaces :**
- Consomme : la solution
- Produit : `npm run audit:back`, appelé par `verify`.

- [ ] **Étape 1 : écrire l'épreuve**

`back/tests-harness/vulnerabilites.test.mjs` :

```javascript
import { describe, expect, it } from 'vitest'
import { lancerOutil } from '../../front/tests/harness/run-outil.js'

describe('garde-fou : vulnérabilités des dépendances', () => {
  it('aucune vulnérabilité connue dans les paquets du backend', () => {
    const r = lancerOutil([
      'dotnet', 'list', 'back/Palier.sln', 'package', '--vulnerable', '--include-transitive',
    ])
    expect(r.code, `La commande a échoué :\n${r.sortie}`).toBe(0)
    expect(r.sortie, `Vulnérabilité détectée :\n${r.sortie}`).not.toMatch(/High|Critical/i)
  })
})
```

`dotnet list package --vulnerable` sort en code 0 même quand il trouve quelque chose : c'est la sortie qu'il faut examiner, pas le code de retour. C'est exactement le genre de contrôle qui approuve en silence si on ne le vérifie pas.

- [ ] **Étape 2 : lancer l'épreuve**

Attendu : succès sur les dépendances actuelles.

- [ ] **Étape 3 : ajouter le script**

```json
{ "scripts": { "audit:back": "dotnet list back/Palier.sln package --vulnerable --include-transitive" } }
```

- [ ] **Étape 4 : commit**

```bash
git add -A
git commit -m "surveille les vulnérabilités des paquets du backend"
```

---

## Tâche 9 : Le point d'entrée unique, étendu au backend

**Fichiers :**
- Créer : `scripts/verify.mjs`
- Modifier : `package.json` racine, `.husky/pre-push`
- Créer : `back/tests-harness/verify.test.mjs`

**Interfaces :**
- Consomme : tous les scripts des tâches 1 à 8
- Produit : `npm run verify` — **la seule commande** appelée par le hook de pré-envoi et la CI.

- [ ] **Étape 1 : écrire l'épreuve**

`back/tests-harness/verify.test.mjs` :

```javascript
import { existsSync, readFileSync } from 'node:fs'
import { describe, expect, it } from 'vitest'

describe("garde-fou : point d'entrée unique", () => {
  it('le script verify existe', () => {
    expect(existsSync('scripts/verify.mjs'), 'Cible manquante').toBe(true)
  })

  it('verify enchaîne le front et le backend', () => {
    const s = readFileSync('scripts/verify.mjs', 'utf8')
    for (const etape of [
      'front:format', 'front:lint', 'front:typecheck', 'front:test',
      'back:format', 'back:build', 'back:test', 'licences',
    ]) {
      expect(s, `Contrôle manquant : ${etape}`).toContain(etape)
    }
  })

  it("le hook de pré-envoi n'appelle que verify", () => {
    const h = readFileSync('.husky/pre-push', 'utf8')
    expect(h).toContain('npm run verify')
    expect(h, 'Le hook délègue, il n\'énumère pas').not.toMatch(/dotnet (build|test)/)
  })
})
```

- [ ] **Étape 2 : lancer l'épreuve pour la voir échouer**

Attendu : ÉCHEC — `scripts/verify.mjs` n'existe pas.

- [ ] **Étape 3 : écrire `scripts/verify.mjs`**

```javascript
#!/usr/bin/env node
import { spawnSync } from 'node:child_process'

const SEUIL_MS = 90000 // deux harnais, seuil doublé par rapport au front seul

const etapes = [
  ['front:format', ['npm', '--prefix', 'front', 'run', 'format:check']],
  ['front:lint', ['npm', '--prefix', 'front', 'run', 'lint']],
  ['front:typecheck', ['npm', '--prefix', 'front', 'run', 'typecheck']],
  ['front:test', ['npm', '--prefix', 'front', 'run', 'test']],
  ['front:knip', ['npm', '--prefix', 'front', 'run', 'knip']],
  ['back:format', ['dotnet', 'format', 'back/Palier.sln', '--verify-no-changes']],
  ['back:build', ['dotnet', 'build', 'back/Palier.sln', '--no-incremental']],
  ['back:test', ['dotnet', 'test', 'back/Palier.sln', '--settings', 'back/coverage.runsettings']],
  ['licences', ['node', 'scripts/verifier-licences.mjs']],
  ['front:build', ['npm', '--prefix', 'front', 'run', 'build']],
]

const debut = Date.now()
const durees = []
let echec = null

for (const [nom, commande] of etapes) {
  const t0 = Date.now()
  const [bin, ...args] = commande
  const r = spawnSync(bin, args, { stdio: 'inherit', shell: process.platform === 'win32' })
  durees.push([nom, Date.now() - t0])
  if (r.status !== 0) {
    echec = nom
    break
  }
}

const total = Date.now() - debut
console.log('\n─── verify ───')
for (const [nom, ms] of durees) console.log(`  ${nom.padEnd(18)} ${(ms / 1000).toFixed(1)} s`)
console.log(`  ${'TOTAL'.padEnd(18)} ${(total / 1000).toFixed(1)} s`)

if (echec) {
  console.error(`\nÉCHEC sur : ${echec}`)
  process.exit(1)
}

if (total > SEUIL_MS) {
  console.warn(
    `\nAVERTISSEMENT : verify a pris ${(total / 1000).toFixed(1)} s, au-delà de ${SEUIL_MS / 1000} s.\n` +
      'Une boucle de rétroaction lente est un défaut à traiter — docs/08-workflow.md § 5.',
  )
}
```

Les épreuves du harnais ne sont **pas** dans `verify` : elles vérifient que les contrôles refusent, ce qui suppose de les faire échouer délibérément. Elles ont leur propre commande, appelée par la CI.

- [ ] **Étape 4 : lancer l'épreuve pour la voir passer**

Attendu : 3 tests passent.

- [ ] **Étape 5 : lancer `verify` et mesurer**

```bash
npm run verify
```

Attendu : toutes les étapes passent, la durée s'affiche. **Noter cette durée dans le rapport de lot** — c'est la mesure de référence de la boucle de rétroaction.

- [ ] **Étape 6 : commit**

```bash
git add -A
git commit -m "étend la commande de vérification unique au backend"
```

---

## Tâche 10 : Intégration continue

**Fichiers :**
- Modifier : `.github/workflows/ci.yml`
- Modifier : `.github/dependabot.yml`

**Interfaces :**
- Consomme : `npm run verify`
- Produit : la CI qui bloque la fusion.

- [ ] **Étape 1 : ajouter le job backend au workflow**

Dans `.github/workflows/ci.yml`, après le job `qualite` :

```yaml
  backend:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@<SHA> # v4
      - uses: actions/setup-dotnet@<SHA> # v4
        with:
          global-json-file: global.json
      - run: dotnet restore back/Palier.sln
      - run: dotnet build back/Palier.sln --no-restore
      - run: dotnet test back/Palier.sln --no-build --settings back/coverage.runsettings
      - run: dotnet format back/Palier.sln --verify-no-changes
      - run: dotnet list back/Palier.sln package --vulnerable --include-transitive

  licences:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@<SHA> # v4
      - uses: actions/setup-node@<SHA> # v4
        with:
          node-version-file: .nvmrc
      - run: node scripts/verifier-licences.mjs
```

Relever les empreintes SHA comme au lot 1a :

```bash
gh api repos/actions/setup-dotnet/git/ref/tags/v4 --jq '.object.sha'
```

- [ ] **Étape 2 : étendre le job de franchissement**

```yaml
  franchissement:
    needs: [qualite, securite, e2e, backend, licences]
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@<SHA> # v4
      - uses: actions/setup-node@<SHA> # v4
        with:
          node-version-file: .nvmrc
      - uses: actions/setup-dotnet@<SHA> # v4
        with:
          global-json-file: global.json
      - run: npm --prefix front ci
      - name: Les garde-fous refusent-ils encore ?
        run: npm run test:harness
```

- [ ] **Étape 3 : ajouter NuGet à Dependabot**

```yaml
  - package-ecosystem: nuget
    directory: /back
    schedule:
      interval: weekly
    groups:
      mineures:
        update-types: [minor, patch]
```

- [ ] **Étape 4 : pousser et vérifier que la CI passe**

```bash
git add -A
git commit -m "ajoute l'intégration continue du backend"
git push -u origin feat/lot-1b-harnais-backend
gh pr create --title "Lot 1b — harnais backend" --body "Voir docs/superpowers/specs/2026-08-19-architecture-backend-csharp-design.md"
```

Attendu : tous les jobs passent. Une CI rouge n'est jamais « temporaire ».

- [ ] **Étape 5 : fusionner**

Invoquer `superpowers:finishing-a-development-branch`.

---

## Auto-revue du plan

**Couverture de la spec.** Section 3 (structure des projets) → T1, T2 · section 4 (le domaine) → T4 · section 11 (harnais .NET) → T3 à T9 · les neuf épreuves listées en section 11 → pureté du domaine T2, nullabilité et avertissements T3, format T6, couverture T5, licences T7, vulnérabilités T8, point d'entrée unique T9.

**Trois épreuves de la spec ne sont pas dans ce plan, et c'est délibéré :** « une donnée de santé dans un journal », « A lit une ligne de B » et « un nom franchit la frontière du modèle » exigent respectivement un journal, une base et un adaptateur LLM — aucun n'existe au lot 1b. Elles appartiennent aux lots 4, 5 et 8, et sont inscrites à leurs spécifications.

**Testcontainers n'est pas installé ici**, pour la même raison : sans schéma ni EF Core, il n'y a rien à tester contre une base réelle. Il arrive au lot 4.

**Cohérence des noms :** `lancerOutil` de la tâche 2 du lot 1a est réutilisé par toutes les épreuves backend, importé depuis `front/tests/harness/run-outil.js`. `MetabolismeDeBase.MifflinStJeor` défini en T4 garde cette signature au lot 3.

**Point à vérifier à l'exécution :** les chemins d'import entre `back/tests-harness/` et `front/tests/harness/run-outil.js` traversent la nouvelle frontière `front`/`back`. Si l'import échoue, la parade est un script racine `test:harness` avec sa propre configuration vitest — inscrit en note dans la tâche 2, à trancher par l'implémenteur et à consigner.
