# Brief — Tâche 5

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

## Tâche 5 : Oxlint — socle et règles syntaxiques

**Fichiers :**
- Créer : `front/.oxlintrc.json`, `front/tests/harness/fixtures/any-explicite.ts`, `front/tests/harness/fixtures/nommage-Invalide.ts`, `front/tests/harness/oxlint-base.test.ts`
- Modifier : `front/package.json` (script `lint`)

**Interfaces :**
- Consomme : `lancerOutil` de la tâche 2
- Produit : `npm --prefix front run lint`, et le fichier `.oxlintrc.json` que les tâches 6 et 7 enrichissent sans le recréer.

> **Pourquoi Oxlint et non ESLint.** `typescript-eslint` 8.67.0, publié le 10/08/2026, exige `typescript >=4.8.4 <6.1.0` — vérifié sur le registre npm. TypeScript 7.0.2 est hors de cette plage. Oxlint ne dépend pas de l'API du compilateur pour ses règles syntaxiques, et son composant type-aware `tsgolint` est **construit sur TypeScript 7** : ses versions suivent celles du compilateur (`v7.0.2001` = TypeScript 7.0.2, patch 001). C'est l'option alignée sur la version installée, pas un contournement.

- [ ] **Étape 1 : écrire l'épreuve**

`front/tests/harness/oxlint-base.test.ts` :

```typescript
import { existsSync } from 'node:fs'
import { describe, expect, it } from 'vitest'
import { lancerOutil } from './run-outil'

const ANY = 'tests/harness/fixtures/any-explicite.ts'
const NOMMAGE = 'tests/harness/fixtures/nommage-Invalide.ts'

describe('garde-fou : Oxlint, règles syntaxiques', () => {
  it.each([ANY, NOMMAGE])('la fixture %s existe', (f) => {
    expect(existsSync(f), `Cible manquante : ${f}`).toBe(true)
  })

  it('refuse un any explicite', () => {
    const r = lancerOutil(['npx', 'oxlint', ANY])
    expect(r.code, `Oxlint a accepté le any :\n${r.sortie}`).not.toBe(0)
    expect(r.sortie).toContain('no-explicit-any')
  })

  it('refuse un nom de fichier hors kebab-case et PascalCase', () => {
    const r = lancerOutil(['npx', 'oxlint', NOMMAGE])
    expect(r.code, `Oxlint a accepté le nommage :\n${r.sortie}`).not.toBe(0)
    expect(r.sortie).toMatch(/filename-case/)
  })
})
```

- [ ] **Étape 2 : lancer l'épreuve pour la voir échouer**

Lancer : `npm --prefix front run test:harness -- oxlint-base`
Attendu : ÉCHEC — Oxlint n'est pas installé.

- [ ] **Étape 3 : installer Oxlint**

```bash
npm --prefix front add -D oxlint
```

- [ ] **Étape 4 : écrire `front/.oxlintrc.json`**

```json
{
  "$schema": "./node_modules/oxlint/configuration_schema.json",
  "plugins": ["typescript", "unicorn", "import", "jsx-a11y", "react"],
  "env": { "browser": true, "es2024": true },
  "categories": { "correctness": "error" },
  "ignorePatterns": ["dist/**", "coverage/**", "tests/harness/fixtures/**"],
  "rules": {
    "typescript/no-explicit-any": "error",
    "eslint/no-unused-vars": "error",
    "unicorn/filename-case": ["error", { "cases": { "kebabCase": true, "pascalCase": true } }],
    "import/no-cycle": "error",
    "eslint/no-console": "error"
  },
  "overrides": [
    {
      "files": ["**/*.test.{ts,tsx}", "**/*.spec.ts"],
      "rules": { "typescript/no-explicit-any": "off" }
    }
  ]
}
```

Les fixtures figurent dans `ignorePatterns` : le lint courant ne doit pas les voir. Les épreuves les atteignent en nommant leur chemin explicitement, ce qui contourne l'ignore.

- [ ] **Étape 5 : créer les fixtures**

`front/tests/harness/fixtures/any-explicite.ts` :

```typescript
// Violation délibérée : any explicite.
export function traite(valeur: any): string {
  return String(valeur)
}
```

`front/tests/harness/fixtures/nommage-Invalide.ts` :

```typescript
// Violation délibérée : nom de fichier ni kebab-case ni PascalCase pur.
export const valeur = 1
```

- [ ] **Étape 6 : ajouter le script et lancer l'épreuve**

Dans `front/package.json` :

```json
{ "scripts": { "lint": "oxlint src tests" } }
```

Lancer l'épreuve : attendu, 4 tests passent.

- [ ] **Étape 7 : vérifier que le front réel est propre**

Lancer : `npm --prefix front run lint`
Attendu : aucune erreur.

- [ ] **Étape 8 : commit**

```bash
git add -A
git commit -m "installe Oxlint et ses règles syntaxiques"
```

---
