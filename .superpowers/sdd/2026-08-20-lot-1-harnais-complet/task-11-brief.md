# Brief — Tâche 11

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

## Tâche 11 : Solution .NET et les quatre projets

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

---
