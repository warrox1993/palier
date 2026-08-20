# Brief — Tâche 10

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

## Tâche 10 : Playwright et axe-core

**Fichiers :**
- Créer : `playwright.config.ts`, `tests/e2e/accessibilite.spec.ts`, `tests/harness/fixtures/bouton-sans-nom.html`, `tests/harness/accessibilite.test.ts`
- Modifier : `package.json`

**Interfaces :**
- Consomme : `lancerOutil`, le build de la tâche 1
- Produit : `npm run e2e`.

- [ ] **Étape 1 : écrire l'épreuve**

`tests/harness/accessibilite.test.ts` :

```typescript
import { existsSync } from 'node:fs'
import { describe, expect, it } from 'vitest'
import { lancerOutil } from './run-outil'

const FIXTURE = 'tests/harness/fixtures/bouton-sans-nom.html'

describe('garde-fou : accessibilité', () => {
  it('la fixture de violation existe', () => {
    expect(existsSync(FIXTURE), `Cible manquante : ${FIXTURE}`).toBe(true)
  })

  it('axe-core détecte un bouton sans nom accessible', () => {
    const r = lancerOutil(['npx', 'playwright', 'test', 'tests/e2e/fixture-a11y.spec.ts'])
    expect(r.code, `axe-core n'a pas vu la violation :\n${r.sortie}`).not.toBe(0)
    expect(r.sortie).toMatch(/button-name|violation/i)
  })
})
```

- [ ] **Étape 2 : lancer l'épreuve pour la voir échouer**

Lancer : `npx vitest run tests/harness/accessibilite.test.ts`
Attendu : ÉCHEC.

- [ ] **Étape 3 : installer Playwright et axe-core**

```bash
npm install -D @playwright/test @axe-core/playwright
npx playwright install --with-deps chromium webkit
```

- [ ] **Étape 4 : écrire `playwright.config.ts`**

```typescript
import { defineConfig, devices } from '@playwright/test'

export default defineConfig({
  testDir: './tests/e2e',
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 1 : 0,
  reporter: process.env.CI ? 'github' : 'list',
  use: { baseURL: 'http://localhost:4173', trace: 'on-first-retry' },
  projects: [
    { name: 'chromium', use: { ...devices['Desktop Chrome'] } },
    { name: 'webkit-mobile', use: { ...devices['iPhone 13'] } },
  ],
  webServer: {
    command: 'npm run build && npm run preview',
    url: 'http://localhost:4173',
    reuseExistingServer: !process.env.CI,
    timeout: 120000,
  },
})
```

Le second projet est `iPhone 13` et non un Chrome de bureau : `02-design.md` § 1 pose que le téléphone gouverne le design.

- [ ] **Étape 5 : écrire les deux tests de bout en bout**

`tests/e2e/accessibilite.spec.ts` — le vrai test du projet :

```typescript
import AxeBuilder from '@axe-core/playwright'
import { expect, test } from '@playwright/test'

test("la page d'accueil n'a aucune violation critique", async ({ page }) => {
  await page.goto('/')
  const resultats = await new AxeBuilder({ page })
    .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'wcag22aa'])
    .analyze()
  const critiques = resultats.violations.filter((v) => v.impact === 'critical' || v.impact === 'serious')
  expect(critiques, JSON.stringify(critiques, null, 2)).toHaveLength(0)
})
```

`tests/e2e/fixture-a11y.spec.ts` — l'épreuve, qui doit échouer :

```typescript
import AxeBuilder from '@axe-core/playwright'
import { expect, test } from '@playwright/test'
import { pathToFileURL } from 'node:url'
import { resolve } from 'node:path'

test('la fixture accessible doit être refusée par axe-core', async ({ page }) => {
  const chemin = pathToFileURL(resolve('tests/harness/fixtures/bouton-sans-nom.html')).href
  await page.goto(chemin)
  const resultats = await new AxeBuilder({ page }).analyze()
  expect(resultats.violations).toHaveLength(0)
})
```

Ce test est **conçu pour échouer** : il affirme qu'il n'y a aucune violation sur une page qui en contient une. L'épreuve de la tâche vérifie précisément cet échec.

- [ ] **Étape 6 : créer la fixture**

`tests/harness/fixtures/bouton-sans-nom.html` :

```html
<!doctype html>
<html lang="fr">
  <head><meta charset="UTF-8" /><title>fixture</title></head>
  <body>
    <!-- Violation délibérée : bouton sans nom accessible. -->
    <button type="button"></button>
  </body>
</html>
```

- [ ] **Étape 7 : ajouter le script et lancer l'épreuve**

```json
{ "scripts": { "e2e": "playwright test tests/e2e/accessibilite.spec.ts" } }
```

Le script `e2e` ne lance **que** le vrai test ; la fixture n'est appelée que par l'épreuve du harnais.

Lancer : `npx vitest run tests/harness/accessibilite.test.ts`
Attendu : 2 tests passent.

- [ ] **Étape 8 : commit**

```bash
git add -A
git commit -m "éprouve la détection des violations d'accessibilité"
```

---

---
