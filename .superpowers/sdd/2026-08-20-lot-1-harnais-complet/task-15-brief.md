# Brief — Tâche 15

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

## Tâche 15 : Format et style

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

---
