# Brief — Tâche 8

> Extrait de `docs/superpowers/plans/2026-08-19-lot-1-harnais.md`. C'est la source unique de tes exigences.
> Les valeurs exactes — code, chemins, chaînes de caractères — se reprennent **verbatim**.

## Contraintes globales

Ces règles s'appliquent à **toutes** les tâches sans être répétées.

- **Node 24** (`.nvmrc`), identique en local, en CI et sur Vercel. La documentation Vercel du 27/02/2026 donne 24.x comme version **par défaut** (24.x, 22.x et 20.x disponibles), et 24.16.0 est la version installée localement : aucun écart entre le poste, la CI et la cible de déploiement. Verrouillée aussi par `engines.node` dans `package.json`.
- **npm**, jamais pnpm ni yarn. En CI : `npm ci`, jamais `npm install`.
- **Aucune version de paquet figée dans ce plan.** Les installations se font sans numéro ; le lockfile verrouille.
- **Messages de commit en français, à l'impératif** — `16-projet.md` § 2. Exemple : `ajoute le calcul du TDEE adaptatif`.
- **Nommage** — fichiers `kebab-case.ts`, composants `PascalCase.tsx`, fonctions et variables `camelCase`, constantes `SCREAMING_SNAKE_CASE`, types `PascalCase` sans préfixe, booléens en `is`/`has`/`can`.
- **Tests à côté du source** — `<fichier>.test.ts`.
- **Aucun secret dans le dépôt.** `.env.example` versionné, `.env` ignoré.
- **Toute épreuve du harnais vit dans `tests/harness/`** ; les violations délibérées dans `tests/harness/fixtures/`, exclues de `tsconfig.json` et du build.
- **Une seule branche de travail** : `feat/lot-1-harnais`, fusionnée dans `main` en fin de lot.
- Le dépôt distant est `github.com/warrox1993/palier`, privé, branche par défaut `main`.

---

## Tâche 8 : Knip et jscpd — code mort et duplication

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
    "eslint.config.js",
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
