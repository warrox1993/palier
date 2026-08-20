# Brief — Tâche 9

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

## Tâche 9 : Knip et jscpd — code mort et duplication

**Fichiers :**
- Créer : `knip.json`, `.jscpd.json`, `tests/harness/fixtures/export-orphelin.ts`, `tests/harness/code-mort.test.ts`
- Modifier : `package.json`

**Interfaces :**
- Consomme : `lancerOutil`
- Produit : `npm run knip` (bloquant) et `npm run jscpd` (rapport, non bloquant).

- [ ] **Étape 1 : écrire l'épreuve**

`tests/harness/code-mort.test.ts` :

```typescript
import { existsSync } from 'node:fs'
import { describe, expect, it } from 'vitest'
import { lancerOutil } from './run-outil'

const FIXTURE = 'tests/harness/fixtures/export-orphelin.ts'

describe('garde-fou : code mort', () => {
  it('la fixture de violation existe', () => {
    expect(existsSync(FIXTURE), `Cible manquante : ${FIXTURE}`).toBe(true)
  })

  it('signale un export que personne n\'importe', () => {
    const r = lancerOutil(['npx', 'knip', '--config', 'knip.fixtures.json'])
    expect(r.code, `Knip n'a pas vu l'export orphelin :\n${r.sortie}`).not.toBe(0)
    expect(r.sortie).toMatch(/orphelin|unused|export/i)
  })
})
```

- [ ] **Étape 2 : lancer l'épreuve pour la voir échouer**

Lancer : `npx vitest run tests/harness/code-mort.test.ts`
Attendu : ÉCHEC.

- [ ] **Étape 3 : installer et configurer**

```bash
npm install -D knip jscpd
```

`knip.json` — les points d'entrée sont déclarés explicitement, faute de quoi Knip signale des faux positifs en masse :

```json
{
  "entry": [
    "src/main.tsx",
    "vite.config.ts",
    "vitest.config.ts",
    "playwright.config.ts",
    "scripts/verify.mjs",
    "tests/**/*.test.ts",
    "tests/**/*.spec.ts"
  ],
  "project": ["src/**/*.{ts,tsx}", "scripts/**/*.mjs"],
  "ignore": ["tests/harness/fixtures/**"],
  "ignoreDependencies": []
}
```

`knip.fixtures.json` — configuration dédiée à l'épreuve :

```json
{
  "entry": ["tests/harness/fixtures/point-entree.ts"],
  "project": ["tests/harness/fixtures/*.ts"],
  "ignore": []
}
```

`.jscpd.json` :

```json
{
  "threshold": 3,
  "reporters": ["console"],
  "ignore": ["**/node_modules/**", "**/dist/**", "tests/harness/fixtures/**"],
  "absolute": true
}
```

- [ ] **Étape 4 : créer les fixtures**

`tests/harness/fixtures/export-orphelin.ts` :

```typescript
// Violation délibérée : cet export n'est importé nulle part.
export function personneNeMAppelle(): string {
  return 'orphelin'
}
```

`tests/harness/fixtures/point-entree.ts` :

```typescript
// Point d'entrée de la configuration d'épreuve : il n'importe pas l'orphelin.
export const entree = true
```

- [ ] **Étape 5 : ajouter les scripts et lancer l'épreuve**

```json
{
  "scripts": {
    "knip": "knip",
    "jscpd": "jscpd src"
  }
}
```

Le caractère non bloquant de jscpd n'est pas porté par le script : `|| exit 0` ne se comporte pas de la même façon selon le shell sous Windows. Il est porté par la CI, qui lance cette étape en `continue-on-error`. Ruling C6 du ledger.

Lancer : `npx vitest run tests/harness/code-mort.test.ts`
Attendu : 2 tests passent.

- [ ] **Étape 6 : consigner les faux positifs**

Lancer : `npm run knip` sur le projet principal. Noter le nombre de signalements écartés et la raison de chacun **dans le rapport de tâche** — `docs/decisions.md` n'existe qu'à la tâche 12, qui les y reprendra. Ruling C2 du ledger. Un détecteur qui se trompe est un détecteur qu'on cesse de lire.

- [ ] **Étape 7 : commit**

```bash
git add -A
git commit -m "éprouve la détection de code mort"
```

---

---
