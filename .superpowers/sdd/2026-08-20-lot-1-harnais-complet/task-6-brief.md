# Brief — Tâche 6

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

## Tâche 6 : Oxlint type-aware, et la frontière front / domaine

**Fichiers :**
- Modifier : `front/.oxlintrc.json`, `front/package.json`
- Créer : `front/tests/harness/fixtures/core-impur.ts`, `front/tests/harness/oxlint-architecture.test.ts`

**Interfaces :**
- Consomme : `.oxlintrc.json` de la tâche 5
- Produit : la barrière d'import qui protège `front/src/core/`, et les 59 règles type-aware.

> **La moitié de cette tâche a changé de nature avec l'architecture backend.** `src/llm/` a disparu du front — le modèle vit dans `Palier.Infrastructure`. `front/src/core/` reste légitime pour les conversions d'unités, le formatage et les agrégations d'affichage, mais **plus pour un calcul de conformité** : ceux-là vivent exclusivement dans `Palier.Domain`. L'interdiction des calculs de conformité est portée par la tâche 7, `no-restricted-syntax` n'existant pas dans Oxlint.

- [ ] **Étape 1 : écrire l'épreuve**

`front/tests/harness/oxlint-architecture.test.ts` :

```typescript
import { existsSync } from 'node:fs'
import { describe, expect, it } from 'vitest'
import { lancerOutil } from './run-outil'

const IMPUR = 'tests/harness/fixtures/core-impur.ts'

describe('garde-fou : pureté de front/src/core/', () => {
  it('la fixture de violation existe', () => {
    expect(existsSync(IMPUR), `Cible manquante : ${IMPUR}`).toBe(true)
  })

  it('refuse un import de React depuis core/', () => {
    const r = lancerOutil(['npx', 'oxlint', IMPUR])
    expect(r.code, `Oxlint a accepté l'import impur :\n${r.sortie}`).not.toBe(0)
    expect(r.sortie).toMatch(/no-restricted-imports/)
  })

  it('le linting type-aware est actif', () => {
    const r = lancerOutil(['npx', 'oxlint', '--type-aware', 'src'])
    expect(r.code, `Le mode type-aware a échoué :\n${r.sortie}`).toBe(0)
  })
})
```

- [ ] **Étape 2 : lancer l'épreuve pour la voir échouer**

Attendu : ÉCHEC — ni la fixture ni la règle n'existent, et `oxlint-tsgolint` n'est pas installé.

- [ ] **Étape 3 : installer le composant type-aware**

```bash
npm --prefix front add -D oxlint-tsgolint@latest
```

`tsgolint` s'appuie sur `typescript-go` et cible TypeScript 7 : il suit la version du compilateur au lieu de la contraindre.

- [ ] **Étape 4 : ajouter la barrière d'architecture à `.oxlintrc.json`**

Dans le tableau `overrides` :

```json
    {
      "files": ["src/core/**/*.ts", "tests/harness/fixtures/core-impur.ts"],
      "rules": {
        "eslint/no-restricted-imports": [
          "error",
          {
            "patterns": [
              { "group": ["react", "react-dom", "react/*"], "message": "core/ est pur : aucune dépendance UI." },
              { "group": ["dexie"], "message": "core/ est pur : aucun accès stockage." },
              { "group": ["@/lib/*", "@/ui/*", "@/features/*", "../lib/*", "../ui/*", "../features/*"], "message": "core/ est pur : aucune dépendance applicative." }
            ]
          }
        ]
      }
    }
```

Et activer les règles type-aware dans la section `rules` :

```json
    "typescript/no-floating-promises": "error",
    "typescript/no-misused-promises": "error",
    "typescript/await-thenable": "error",
    "typescript/no-unsafe-assignment": "warn",
    "typescript/strict-boolean-expressions": "warn"
```

Les deux premières sont bloquantes : une promesse non attendue dans une application hors ligne à file de retry produit des pertes de données silencieuses.

- [ ] **Étape 5 : créer la fixture**

`front/tests/harness/fixtures/core-impur.ts` :

```typescript
// Violation délibérée : un module pur qui importe React.
import { useState } from 'react'

export function agregeQuelqueChose(): number {
  const [valeur] = useState(0)
  return valeur
}
```

- [ ] **Étape 6 : ajouter le script type-aware**

```json
{
  "scripts": {
    "lint": "oxlint --ignore-pattern \"tests/harness/fixtures/**\" .",
    "lint:types": "oxlint --type-aware --ignore-pattern \"tests/harness/fixtures/**\" src"
  }
}
```

- [ ] **Étape 7 : lancer l'épreuve pour la voir passer**

Attendu : 3 tests passent.

- [ ] **Étape 7 bis : vérifier que le front réel reste propre**

Lancer : `npm run lint` et `npm run lint:types`.
Attendu : code 0, aucune sortie.

Cette étape existe parce que le script `lint` porte l'exclusion des fixtures
(ruling P6) : toute réécriture du script qui l'oublie fait rougir le lint sur
les violations délibérées. La tâche 5 avait cette étape ; son absence ici avait
laissé passer une régression jusqu'à la revue.

- [ ] **Étape 8 : commit**

```bash
git add -A
git commit -m "active le linting type-aware et la barrière de pureté du front"
```

> **Note pour la tâche 18.** Oxlint propose `oxlint --type-aware --type-check`, qui remplace l'étape `tsc --noEmit`. Ce plan conserve `tsc --noEmit` séparément : c'est le contrôle connu et éprouvé, et fusionner les deux au moment où l'on installe le harnais mêlerait deux changements. À évaluer une fois le lot stabilisé, et à consigner dans `docs/decisions.md`.

---
