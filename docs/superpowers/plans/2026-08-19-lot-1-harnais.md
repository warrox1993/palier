# Lot 1 — Harnais et outillage — Plan d'implémentation

> **Pour les exécutants agentiques :** SOUS-SKILL REQUISE — utiliser `superpowers:subagent-driven-development` (recommandé) ou `superpowers:executing-plans` pour exécuter ce plan tâche par tâche. Les étapes utilisent la syntaxe à cases (`- [ ]`) pour le suivi.

**But :** mettre en place l'environnement déterministe du projet `palier` et prouver que chacun de ses garde-fous refuse effectivement ce qu'il prétend refuser.

**Architecture :** Vite + React + TypeScript strict, sans aucun code produit. Chaque garde-fou est installé selon un cycle rouge-vert : on écrit d'abord l'épreuve — une violation délibérée placée dans `tests/harness/fixtures/`, exclue du typecheck et du build — puis on vérifie qu'elle *passe* (le garde-fou n'existe pas encore, donc rien ne la refuse), puis on installe le garde-fou, puis l'épreuve échoue à produire son résultat interdit. Une commande unique, `npm run verify`, enchaîne l'ensemble ; le hook pre-push et la CI n'appellent qu'elle.

**Pile technique :** Vite, React, TypeScript, ESLint 9 (flat config), Prettier, Vitest, Playwright, axe-core, Knip, jscpd, Husky, lint-staged, gitleaks, Semgrep, GitHub Actions.

**Spec :** `docs/superpowers/specs/2026-08-19-lot-1-harnais-design.md`

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

## Structure des fichiers

Créés au cours de ce lot, avec la responsabilité de chacun :

| Fichier | Responsabilité |
|---|---|
| `package.json` | dépendances et scripts, dont `verify` |
| `.nvmrc` | version de Node, unique source de vérité |
| `tsconfig.json` | TypeScript strict ; exclut `tests/harness/fixtures/` |
| `vite.config.ts` | build et serveur de développement |
| `vitest.config.ts` | tests unitaires, environnement jsdom, seuils de couverture |
| `playwright.config.ts` | tests de bout en bout, Chromium et WebKit |
| `eslint.config.js` | **toutes** les règles de lint, en un seul fichier |
| `.prettierrc` | format |
| `knip.json` | code mort, points d'entrée déclarés explicitement |
| `.jscpd.json` | duplication, non bloquant |
| `.gitleaks.toml` | détection de secrets |
| `.husky/pre-commit` | lint et format sur les fichiers modifiés, secrets |
| `.husky/pre-push` | `npm run verify` |
| `scripts/verify.mjs` | enchaîne les contrôles, mesure et affiche la durée |
| `.github/workflows/ci.yml` | quatre jobs parallèles puis le franchissement |
| `.github/dependabot.yml` | npm et github-actions, hebdomadaire |
| `vercel.json` | en-têtes de sécurité |
| `.env.example` | variables documentées, aucune valeur |
| `src/main.tsx`, `src/app/app.tsx` | point d'entrée minimal |
| `src/core/index.ts` | module pur, vide, existe pour que la règle d'architecture ait une cible |
| `tests/harness/*.test.ts` | les douze épreuves |
| `tests/harness/fixtures/*` | les violations délibérées |
| `tests/harness/run-outil.ts` | utilitaire : lance un outil en sous-processus, retourne code et sortie |
| `tests/e2e/accessibilite.spec.ts` | axe-core sur la page d'accueil |
| `docs/decisions.md` | journal des décisions, lu au démarrage |
| `docs/gabarit-rapport-lot.md` | les quatre sections du rapport de fin de lot |
| `docs/securite/asvs-l2.md` | état de chaque exigence ASVS niveau 2 |

---

## Tâche 1 : Socle Vite + React + TypeScript strict

**Fichiers :**
- Créer : `package.json`, `.nvmrc`, `tsconfig.json`, `tsconfig.node.json`, `vite.config.ts`, `index.html`, `src/main.tsx`, `src/app/app.tsx`, `src/core/index.ts`, `.gitignore`, `.env.example`

**Interfaces :**
- Consomme : rien
- Produit : les scripts `npm run build`, `npm run dev`, `npm run typecheck`. Le dossier `src/core/` existe et n'exporte rien encore.

- [ ] **Étape 1 : créer la branche de travail**

```bash
git checkout -b feat/lot-1-harnais
```

- [ ] **Étape 2 : initialiser le paquet et installer le socle**

```bash
npm init -y
npm install react react-dom
npm install -D vite @vitejs/plugin-react typescript @types/react @types/react-dom
echo "24" > .nvmrc
```

- [ ] **Étape 3 : écrire `tsconfig.json` en mode strict**

```json
{
  "compilerOptions": {
    "target": "ES2022",
    "lib": ["ES2022", "DOM", "DOM.Iterable"],
    "module": "ESNext",
    "moduleResolution": "bundler",
    "jsx": "react-jsx",
    "strict": true,
    "noUncheckedIndexedAccess": true,
    "exactOptionalPropertyTypes": true,
    "noImplicitOverride": true,
    "noUnusedLocals": true,
    "noUnusedParameters": true,
    "noFallthroughCasesInSwitch": true,
    "isolatedModules": true,
    "verbatimModuleSyntax": true,
    "resolveJsonModule": true,
    "skipLibCheck": true,
    "noEmit": true,
    "baseUrl": ".",
    "paths": { "@/*": ["src/*"] }
  },
  "include": ["src", "tests"],
  "exclude": ["tests/harness/fixtures"]
}
```

`exclude` sur `tests/harness/fixtures` est essentiel : c'est ce qui permet d'héberger un `any` interdit dans le dépôt sans casser le typecheck du projet.

- [ ] **Étape 4 : écrire `vite.config.ts`**

```typescript
import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import { fileURLToPath, URL } from 'node:url'

export default defineConfig({
  plugins: [react()],
  resolve: {
    alias: { '@': fileURLToPath(new URL('./src', import.meta.url)) },
  },
  build: { outDir: 'dist', sourcemap: true },
})
```

- [ ] **Étape 5 : écrire le point d'entrée minimal**

`index.html` :

```html
<!doctype html>
<html lang="fr">
  <head>
    <meta charset="UTF-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>palier</title>
  </head>
  <body>
    <div id="root"></div>
    <script type="module" src="/src/main.tsx"></script>
  </body>
</html>
```

`src/main.tsx` :

```typescript
import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { App } from './app/app'

const conteneur = document.getElementById('root')
if (!conteneur) throw new Error('Élément racine introuvable')

createRoot(conteneur).render(
  <StrictMode>
    <App />
  </StrictMode>,
)
```

`src/app/app.tsx` :

```typescript
export function App() {
  return <main id="contenu">palier</main>
}
```

`src/core/index.ts` :

```typescript
// Modules purs : aucun accès réseau, base ou React.
// Ce fichier existe pour que la règle d'architecture ait une cible dès le lot 1.
export {}
```

- [ ] **Étape 6 : écrire `.gitignore` et `.env.example`**

`.gitignore` :

```
node_modules/
dist/
coverage/
playwright-report/
test-results/
.env
.env.local
*.local
.DS_Store
```

`.env.example` — reprendre **intégralement** la liste de `docs/16-projet.md` § 3, sans aucune valeur.

- [ ] **Étape 7 : ajouter les scripts au `package.json`**

```json
{
  "type": "module",
  "engines": { "node": "24.x" },
  "scripts": {
    "dev": "vite",
    "build": "vite build",
    "preview": "vite preview",
    "typecheck": "tsc --noEmit"
  }
}
```

- [ ] **Étape 8 : vérifier que tout tourne**

Lancer : `npm run typecheck && npm run build`
Attendu : aucune erreur, un dossier `dist/` produit.

- [ ] **Étape 9 : commit**

```bash
git add -A
git commit -m "installe le socle Vite React TypeScript strict"
```

---

## Tâche 2 : Vitest et l'utilitaire de franchissement

**Fichiers :**
- Créer : `vitest.config.ts`, `tests/harness/run-outil.ts`, `src/core/exemple.test.ts`
- Modifier : `package.json` (script `test`)

**Interfaces :**
- Consomme : le socle de la tâche 1
- Produit : `npm run test`, et la fonction `lancerOutil(commande: string[], options?: { cwd?: string }): { code: number; sortie: string }` utilisée par **toutes** les épreuves suivantes.

- [ ] **Étape 1 : installer Vitest**

```bash
npm install -D vitest @vitest/coverage-v8 jsdom @testing-library/react @testing-library/jest-dom
```

- [ ] **Étape 2 : écrire `vitest.config.ts`**

```typescript
import { defineConfig } from 'vitest/config'
import react from '@vitejs/plugin-react'
import { fileURLToPath, URL } from 'node:url'

export default defineConfig({
  plugins: [react()],
  resolve: {
    alias: { '@': fileURLToPath(new URL('./src', import.meta.url)) },
  },
  test: {
    environment: 'jsdom',
    globals: true,
    include: ['src/**/*.test.{ts,tsx}', 'tests/harness/**/*.test.ts'],
    testTimeout: 60000,
    coverage: {
      provider: 'v8',
      include: ['src/core/**'],
      thresholds: { lines: 100, functions: 100, branches: 100, statements: 100 },
    },
  },
})
```

Le délai de 60 s est nécessaire : les épreuves lancent des outils en sous-processus.

- [ ] **Étape 3 : écrire l'utilitaire de franchissement**

`tests/harness/run-outil.ts` :

```typescript
import { spawnSync } from 'node:child_process'

export interface ResultatOutil {
  code: number
  sortie: string
}

/**
 * Lance un outil en sous-processus et retourne son code de sortie et sa sortie
 * complète. Utilisé par les épreuves du harnais pour vérifier qu'un garde-fou
 * refuse effectivement une violation.
 */
export function lancerOutil(commande: string[], options: { cwd?: string } = {}): ResultatOutil {
  const [binaire, ...args] = commande
  if (!binaire) throw new Error('Commande vide')

  const r = spawnSync(binaire, args, {
    cwd: options.cwd ?? process.cwd(),
    encoding: 'utf8',
    shell: process.platform === 'win32',
  })

  return {
    code: r.status ?? -1,
    sortie: `${r.stdout ?? ''}${r.stderr ?? ''}`,
  }
}
```

- [ ] **Étape 4 : écrire un test unitaire de l'utilitaire lui-même**

`tests/harness/run-outil.test.ts` :

```typescript
import { describe, expect, it } from 'vitest'
import { lancerOutil } from './run-outil'

describe('lancerOutil', () => {
  it('retourne le code 0 et la sortie quand la commande réussit', () => {
    const r = lancerOutil(['node', '-e', 'console.log("bonjour")'])
    expect(r.code).toBe(0)
    expect(r.sortie).toContain('bonjour')
  })

  it('retourne un code non nul quand la commande échoue', () => {
    const r = lancerOutil(['node', '-e', 'process.exit(3)'])
    expect(r.code).toBe(3)
  })

  it('lève une erreur si la commande est vide', () => {
    expect(() => lancerOutil([])).toThrow('Commande vide')
  })
})
```

- [ ] **Étape 5 : lancer les tests pour vérifier qu'ils échouent**

Lancer : `npx vitest run tests/harness/run-outil.test.ts`
Attendu : ÉCHEC — le fichier `run-outil.ts` n'est pas encore écrit si l'on suit l'ordre rouge-vert. Si l'étape 3 a déjà été faite, inverser : écrire le test avant l'implémentation.

- [ ] **Étape 6 : ajouter le script et vérifier que les tests passent**

```json
{ "scripts": { "test": "vitest run", "test:watch": "vitest" } }
```

Lancer : `npm run test`
Attendu : 3 tests passent.

- [ ] **Étape 7 : commit**

```bash
git add -A
git commit -m "installe Vitest et l'utilitaire de franchissement"
```

---

## Tâche 3 : Épreuve du typecheck — le `any` interdit

**Fichiers :**
- Créer : `tests/harness/fixtures/any-interdit.ts`, `tests/harness/typecheck.test.ts`, `tsconfig.fixtures.json`

**Interfaces :**
- Consomme : `lancerOutil` de la tâche 2
- Produit : le motif d'épreuve réutilisé par les tâches 4 à 10.

- [ ] **Étape 1 : écrire l'épreuve, avant toute fixture**

`tests/harness/typecheck.test.ts` :

```typescript
import { existsSync } from 'node:fs'
import { describe, expect, it } from 'vitest'
import { lancerOutil } from './run-outil'

const FIXTURE = 'tests/harness/fixtures/any-interdit.ts'

describe('garde-fou : TypeScript strict', () => {
  it('la fixture de violation existe', () => {
    expect(existsSync(FIXTURE), `Cible manquante : ${FIXTURE}`).toBe(true)
  })

  it('refuse un any implicite', () => {
    const r = lancerOutil(['npx', 'tsc', '--noEmit', '--project', 'tsconfig.fixtures.json'])
    expect(r.code, `tsc a accepté la violation :\n${r.sortie}`).not.toBe(0)
    expect(r.sortie).toMatch(/implicitly has an 'any' type|noImplicitAny/i)
  })
})
```

Le premier test est l'épreuve de **cible manquante** : si quelqu'un supprime la fixture, l'épreuve échoue au lieu de passer au vert par absence de sujet.

- [ ] **Étape 2 : lancer l'épreuve pour la voir échouer**

Lancer : `npx vitest run tests/harness/typecheck.test.ts`
Attendu : ÉCHEC — la fixture et `tsconfig.fixtures.json` n'existent pas.

- [ ] **Étape 3 : créer la fixture et sa configuration**

`tests/harness/fixtures/any-interdit.ts` :

```typescript
// Violation délibérée. Ce fichier est exclu de tsconfig.json et du build.
// Il sert uniquement à prouver que le typecheck refuse un any implicite.
export function additionne(a, b) {
  return a + b
}
```

`tsconfig.fixtures.json` :

```json
{
  "extends": "./tsconfig.json",
  "include": ["tests/harness/fixtures/any-interdit.ts"],
  "exclude": []
}
```

- [ ] **Étape 4 : lancer l'épreuve pour la voir passer**

Lancer : `npx vitest run tests/harness/typecheck.test.ts`
Attendu : 2 tests passent — `tsc` a bien refusé la violation.

- [ ] **Étape 5 : vérifier que le projet principal reste sain**

Lancer : `npm run typecheck && npm run build`
Attendu : aucune erreur. La fixture est invisible pour le projet.

- [ ] **Étape 6 : commit**

```bash
git add -A
git commit -m "éprouve le refus du any par le typecheck"
```

---

## Tâche 4 : ESLint — règles de base et de nommage

**Fichiers :**
- Créer : `eslint.config.js`, `tests/harness/fixtures/nommage-Invalide.ts`, `tests/harness/eslint-base.test.ts`
- Modifier : `package.json` (script `lint`)

**Interfaces :**
- Consomme : `lancerOutil`
- Produit : `npm run lint`, et le fichier `eslint.config.js` que les tâches 5 et 6 enrichissent sans le recréer.

- [ ] **Étape 1 : écrire l'épreuve**

`tests/harness/eslint-base.test.ts` :

```typescript
import { existsSync } from 'node:fs'
import { describe, expect, it } from 'vitest'
import { lancerOutil } from './run-outil'

const FIXTURES = [
  'tests/harness/fixtures/any-explicite.ts',
  'tests/harness/fixtures/nommage-Invalide.ts',
]

describe('garde-fou : ESLint, règles de base', () => {
  it.each(FIXTURES)('la fixture %s existe', (f) => {
    expect(existsSync(f), `Cible manquante : ${f}`).toBe(true)
  })

  it('refuse un any explicite', () => {
    const r = lancerOutil(['npx', 'eslint', '--no-ignore', 'tests/harness/fixtures/any-explicite.ts'])
    expect(r.code, `ESLint a accepté le any :\n${r.sortie}`).not.toBe(0)
    expect(r.sortie).toContain('no-explicit-any')
  })

  it('refuse un nom de fichier hors kebab-case', () => {
    const r = lancerOutil(['npx', 'eslint', '--no-ignore', 'tests/harness/fixtures/nommage-Invalide.ts'])
    expect(r.code, `ESLint a accepté le nommage :\n${r.sortie}`).not.toBe(0)
    expect(r.sortie).toMatch(/filename|kebab/i)
  })
})
```

- [ ] **Étape 2 : lancer l'épreuve pour la voir échouer**

Lancer : `npx vitest run tests/harness/eslint-base.test.ts`
Attendu : ÉCHEC — ESLint n'est pas installé.

- [ ] **Étape 3 : installer ESLint et ses greffons**

```bash
npm install -D eslint @eslint/js typescript-eslint eslint-plugin-react-hooks \
  eslint-plugin-jsx-a11y eslint-plugin-import eslint-plugin-unicorn \
  eslint-plugin-sonarjs eslint-config-prettier globals
```

- [ ] **Étape 4 : écrire `eslint.config.js`**

```javascript
import js from '@eslint/js'
import tseslint from 'typescript-eslint'
import reactHooks from 'eslint-plugin-react-hooks'
import jsxA11y from 'eslint-plugin-jsx-a11y'
import importPlugin from 'eslint-plugin-import'
import unicorn from 'eslint-plugin-unicorn'
import sonarjs from 'eslint-plugin-sonarjs'
import prettier from 'eslint-config-prettier'
import globals from 'globals'

export default tseslint.config(
  { ignores: ['dist/**', 'coverage/**', 'playwright-report/**', 'node_modules/**'] },
  js.configs.recommended,
  ...tseslint.configs.strictTypeChecked,
  {
    languageOptions: {
      parserOptions: { projectService: true, tsconfigRootDir: import.meta.dirname },
      globals: { ...globals.browser },
    },
    plugins: {
      'react-hooks': reactHooks,
      'jsx-a11y': jsxA11y,
      import: importPlugin,
      unicorn,
      sonarjs,
    },
    rules: {
      // --- bloquant : sécurité du typage ---
      '@typescript-eslint/no-explicit-any': 'error',
      '@typescript-eslint/no-unused-vars': ['error', { argsIgnorePattern: '^_' }],

      // --- bloquant : nommage, docs/16-projet.md § 2 ---
      'unicorn/filename-case': ['error', { cases: { kebabCase: true, pascalCase: true } }],
      '@typescript-eslint/naming-convention': [
        'error',
        { selector: 'default', format: ['camelCase'] },
        { selector: 'variable', format: ['camelCase', 'UPPER_CASE', 'PascalCase'] },
        { selector: 'parameter', format: ['camelCase'], leadingUnderscore: 'allow' },
        { selector: 'typeLike', format: ['PascalCase'], custom: { regex: '^I[A-Z]', match: false } },
        { selector: 'variable', types: ['boolean'], format: ['PascalCase'], prefix: ['is', 'has', 'can', 'should'] },
        { selector: 'objectLiteralProperty', format: null },
      ],

      // --- bloquant : structure ---
      'import/no-cycle': 'error',

      // --- avertissement : jugement, traité en revue ---
      'sonarjs/cognitive-complexity': ['warn', 15],
      'sonarjs/no-duplicate-string': ['warn', { threshold: 3 }],
      'max-lines': ['warn', { max: 300, skipBlankLines: true, skipComments: true }],
      '@typescript-eslint/no-magic-numbers': [
        'warn',
        { ignore: [-1, 0, 1, 2], ignoreArrayIndexes: true, ignoreEnums: true },
      ],
    },
  },
  { files: ['**/*.tsx'], plugins: { 'react-hooks': reactHooks }, rules: reactHooks.configs.recommended.rules },
  { files: ['**/*.tsx'], rules: jsxA11y.flatConfigs.recommended.rules },
  prettier,
)
```

- [ ] **Étape 5 : créer les fixtures**

`tests/harness/fixtures/any-explicite.ts` :

```typescript
// Violation délibérée : any explicite.
export function traite(valeur: any): string {
  return String(valeur)
}
```

`tests/harness/fixtures/nommage-Invalide.ts` :

```typescript
// Violation délibérée : nom de fichier hors kebab-case et hors PascalCase pur.
export const valeur = 1
```

- [ ] **Étape 6 : ajouter le script et lancer l'épreuve**

```json
{ "scripts": { "lint": "eslint ." } }
```

Lancer : `npx vitest run tests/harness/eslint-base.test.ts`
Attendu : 4 tests passent.

- [ ] **Étape 7 : vérifier que le projet principal est propre**

Lancer : `npm run lint`
Attendu : aucune erreur sur `src/`. Les fixtures sont atteintes uniquement via `--no-ignore` et un chemin explicite ; ajouter `tests/harness/fixtures/**` à `ignores` dans `eslint.config.js` si le lint global les remonte.

- [ ] **Étape 8 : commit**

```bash
git add -A
git commit -m "éprouve les règles ESLint de typage et de nommage"
```

---

## Tâche 5 : ESLint — architecture, la pureté de `core/`

**Fichiers :**
- Modifier : `eslint.config.js`
- Créer : `tests/harness/fixtures/core-impur.ts`, `tests/harness/eslint-architecture.test.ts`

**Interfaces :**
- Consomme : `eslint.config.js` de la tâche 4
- Produit : la barrière d'import qui protège `src/core/` et `src/llm/` pour tous les lots suivants.

- [ ] **Étape 1 : écrire l'épreuve**

`tests/harness/eslint-architecture.test.ts` :

```typescript
import { existsSync } from 'node:fs'
import { describe, expect, it } from 'vitest'
import { lancerOutil } from './run-outil'

const FIXTURE = 'tests/harness/fixtures/core-impur.ts'

describe("garde-fou : pureté de src/core/", () => {
  it('la fixture de violation existe', () => {
    expect(existsSync(FIXTURE), `Cible manquante : ${FIXTURE}`).toBe(true)
  })

  it('refuse un import de React depuis core/', () => {
    const r = lancerOutil(['npx', 'eslint', '--no-ignore', FIXTURE])
    expect(r.code, `ESLint a accepté l'import impur :\n${r.sortie}`).not.toBe(0)
    expect(r.sortie).toMatch(/no-restricted-imports|core/i)
  })
})
```

- [ ] **Étape 2 : lancer l'épreuve pour la voir échouer**

Lancer : `npx vitest run tests/harness/eslint-architecture.test.ts`
Attendu : ÉCHEC — la fixture n'existe pas et la règle n'est pas écrite.

- [ ] **Étape 3 : ajouter le bloc d'architecture à `eslint.config.js`**

À insérer avant l'appel final à `prettier` :

```javascript
  // --- Architecture : src/core/ ne contient que des fonctions pures.
  // docs/16-projet.md § 1 ; docs/01-conformite.md § 3 en dépend.
  {
    files: ['src/core/**/*.ts', 'tests/harness/fixtures/core-impur.ts'],
    rules: {
      'no-restricted-imports': [
        'error',
        {
          patterns: [
            { group: ['react', 'react-dom', 'react/*'], message: 'core/ est pur : aucune dépendance UI.' },
            { group: ['@supabase/*'], message: 'core/ est pur : aucun accès base.' },
            { group: ['dexie'], message: 'core/ est pur : aucun accès stockage.' },
            { group: ['@/lib/*', '@/ui/*', '@/features/*', '../lib/*', '../ui/*', '../features/*'],
              message: 'core/ est pur : aucune dépendance applicative.' },
          ],
        },
      ],
    },
  },
  // --- Architecture : le LLM n'écrit jamais sur les objectifs ni les compléments.
  // docs/06-ia.md § 1.
  {
    files: ['src/llm/**/*.ts'],
    rules: {
      'no-restricted-imports': [
        'error',
        {
          patterns: [
            { group: ['@/features/nutrition/ecriture*', '@/features/supplements/ecriture*'],
              message: 'Le modèle ne peut pas écrire sur user_targets ni supplements.' },
          ],
        },
      ],
    },
  },
```

- [ ] **Étape 4 : créer la fixture**

`tests/harness/fixtures/core-impur.ts` :

```typescript
// Violation délibérée : un module pur qui importe React.
import { useState } from 'react'

export function calculeQuelqueChose(): number {
  const [valeur] = useState(0)
  return valeur
}
```

- [ ] **Étape 5 : lancer l'épreuve pour la voir passer**

Lancer : `npx vitest run tests/harness/eslint-architecture.test.ts`
Attendu : 2 tests passent.

- [ ] **Étape 6 : commit**

```bash
git add -A
git commit -m "éprouve la barrière d'architecture de core et llm"
```

---

## Tâche 6 : ESLint — chaînes en dur, couleurs, mouvement

**Fichiers :**
- Modifier : `eslint.config.js`
- Créer : `tests/harness/fixtures/couleur-en-dur.ts`, `tests/harness/fixtures/chaine-en-dur.tsx`, `tests/harness/eslint-design.test.ts`

**Interfaces :**
- Consomme : `eslint.config.js`
- Produit : les règles qui mécanisent `02-design.md` et la règle i18n de `CLAUDE.md` § 4.

- [ ] **Étape 1 : écrire l'épreuve**

`tests/harness/eslint-design.test.ts` :

```typescript
import { existsSync } from 'node:fs'
import { describe, expect, it } from 'vitest'
import { lancerOutil } from './run-outil'

const COULEUR = 'tests/harness/fixtures/couleur-en-dur.ts'
const CHAINE = 'tests/harness/fixtures/chaine-en-dur.tsx'

describe('garde-fou : design et i18n', () => {
  it.each([COULEUR, CHAINE])('la fixture %s existe', (f) => {
    expect(existsSync(f), `Cible manquante : ${f}`).toBe(true)
  })

  it('refuse une couleur hexadécimale hors jetons', () => {
    const r = lancerOutil(['npx', 'eslint', '--no-ignore', COULEUR])
    expect(r.code, `ESLint a accepté la couleur :\n${r.sortie}`).not.toBe(0)
    expect(r.sortie).toMatch(/jeton|no-restricted-syntax/i)
  })

  it('refuse une chaîne de texte en dur dans le JSX', () => {
    const r = lancerOutil(['npx', 'eslint', '--no-ignore', CHAINE])
    expect(r.code, `ESLint a accepté la chaîne :\n${r.sortie}`).not.toBe(0)
    expect(r.sortie).toMatch(/i18next|literal/i)
  })
})
```

- [ ] **Étape 2 : lancer l'épreuve pour la voir échouer**

Lancer : `npx vitest run tests/harness/eslint-design.test.ts`
Attendu : ÉCHEC.

- [ ] **Étape 3 : installer le greffon i18n**

```bash
npm install -D eslint-plugin-i18next
```

- [ ] **Étape 4 : ajouter le bloc design à `eslint.config.js`**

```javascript
  // --- Design : docs/02-design.md § 2 et § 4.
  // Couleurs hors jetons, ombres, flou d'arrière-plan, durées hors échelle.
  {
    files: ['src/**/*.{ts,tsx,css}', 'tests/harness/fixtures/couleur-en-dur.ts'],
    ignores: ['src/ui/jetons.ts'],
    rules: {
      'no-restricted-syntax': [
        'error',
        {
          selector: "Literal[value=/^#(?:[0-9a-fA-F]{3}|[0-9a-fA-F]{6})$/]",
          message: 'Aucune couleur hors des jetons de src/ui/jetons.ts — docs/02-design.md § 9.',
        },
        {
          selector: "Literal[value=/box-shadow|drop-shadow|backdrop-filter|backdrop-blur/]",
          message: 'Aucune ombre portée ni flou d\'arrière-plan — docs/02-design.md § 4.',
        },
        {
          selector: "Literal[value=/linear-gradient|radial-gradient/]",
          message: 'Aucun dégradé — docs/02-design.md § 2.',
        },
        {
          selector: "Literal[value=/[→←↑↓]/]",
          message: 'Aucune flèche Unicode dans un libellé.',
        },
      ],
    },
  },
  // --- i18n : aucune chaîne en dur. CLAUDE.md § 4.
  {
    files: ['src/**/*.tsx', 'tests/harness/fixtures/chaine-en-dur.tsx'],
    plugins: { i18next: i18next },
    rules: { 'i18next/no-literal-string': ['error', { markupOnly: true }] },
  },
```

Ajouter en tête du fichier : `import i18next from 'eslint-plugin-i18next'`.

- [ ] **Étape 5 : créer les fixtures**

`tests/harness/fixtures/couleur-en-dur.ts` :

```typescript
// Violation délibérée : couleur hexadécimale hors jetons.
export const fondInterdit = '#D97757'
```

`tests/harness/fixtures/chaine-en-dur.tsx` :

```typescript
// Violation délibérée : texte affiché sans passer par i18next.
export function Bouton() {
  return <button type="button">Enregistrer la séance</button>
}
```

- [ ] **Étape 6 : lancer l'épreuve pour la voir passer**

Lancer : `npx vitest run tests/harness/eslint-design.test.ts`
Attendu : 4 tests passent.

- [ ] **Étape 7 : commit**

```bash
git add -A
git commit -m "éprouve les règles de design et d'internationalisation"
```

---

## Tâche 7 : Prettier

**Fichiers :**
- Créer : `.prettierrc`, `.prettierignore`, `tests/harness/fixtures/format-casse.ts`, `tests/harness/prettier.test.ts`
- Modifier : `package.json`

**Interfaces :**
- Consomme : `lancerOutil`
- Produit : `npm run format` et `npm run format:check`.

- [ ] **Étape 1 : écrire l'épreuve**

`tests/harness/prettier.test.ts` :

```typescript
import { existsSync } from 'node:fs'
import { describe, expect, it } from 'vitest'
import { lancerOutil } from './run-outil'

const FIXTURE = 'tests/harness/fixtures/format-casse.ts'

describe('garde-fou : Prettier', () => {
  it('la fixture de violation existe', () => {
    expect(existsSync(FIXTURE), `Cible manquante : ${FIXTURE}`).toBe(true)
  })

  it('refuse un fichier mal formaté', () => {
    const r = lancerOutil(['npx', 'prettier', '--check', FIXTURE])
    expect(r.code, `Prettier a accepté le fichier :\n${r.sortie}`).not.toBe(0)
  })
})
```

- [ ] **Étape 2 : lancer l'épreuve pour la voir échouer**

Lancer : `npx vitest run tests/harness/prettier.test.ts`
Attendu : ÉCHEC.

- [ ] **Étape 3 : installer et configurer Prettier**

```bash
npm install -D prettier
```

`.prettierrc` :

```json
{
  "semi": false,
  "singleQuote": true,
  "printWidth": 100,
  "trailingComma": "all",
  "arrowParens": "always",
  "endOfLine": "lf"
}
```

`.prettierignore` :

```
dist/
coverage/
playwright-report/
package-lock.json
```

- [ ] **Étape 4 : créer la fixture**

`tests/harness/fixtures/format-casse.ts` :

```typescript
export   const    mal = {a:1,   b:2,
      c : 3}
```

- [ ] **Étape 5 : ajouter les scripts et vérifier**

```json
{
  "scripts": {
    "format": "prettier --write .",
    "format:check": "prettier --check ."
  }
}
```

Lancer : `npx prettier --write src tests/harness/*.ts` puis `npx vitest run tests/harness/prettier.test.ts`
Attendu : 2 tests passent. Ajouter `tests/harness/fixtures/` à `.prettierignore` **après** l'épreuve, sinon `npm run format:check` échouerait sur la fixture.

- [ ] **Étape 6 : commit**

```bash
git add -A
git commit -m "éprouve le contrôle de format"
```

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
    "jscpd": "jscpd src || exit 0"
  }
}
```

Le `|| exit 0` matérialise le classement de la spec : jscpd publie un rapport, il ne bloque jamais.

Lancer : `npx vitest run tests/harness/code-mort.test.ts`
Attendu : 2 tests passent.

- [ ] **Étape 6 : consigner les faux positifs**

Lancer : `npm run knip` sur le projet principal. Noter le nombre de signalements écartés et la raison de chacun dans `docs/decisions.md` (créé en tâche 12). Un détecteur qui se trompe est un détecteur qu'on cesse de lire.

- [ ] **Étape 7 : commit**

```bash
git add -A
git commit -m "éprouve la détection de code mort"
```

---

## Tâche 9 : Playwright et axe-core

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

## Tâche 10 : la commande unique `verify`

**Fichiers :**
- Créer : `scripts/verify.mjs`, `tests/harness/verify.test.ts`
- Modifier : `package.json`

**Interfaces :**
- Consomme : tous les scripts des tâches 1 à 9
- Produit : `npm run verify` — **la seule commande** que le hook pre-push et la CI appellent.

- [ ] **Étape 1 : écrire l'épreuve**

`tests/harness/verify.test.ts` :

```typescript
import { existsSync, readFileSync } from 'node:fs'
import { describe, expect, it } from 'vitest'

describe('garde-fou : point d\'entrée unique', () => {
  it('le script verify existe', () => {
    expect(existsSync('scripts/verify.mjs'), 'Cible manquante : scripts/verify.mjs').toBe(true)
  })

  it('le hook pre-push n\'appelle que verify', () => {
    const hook = readFileSync('.husky/pre-push', 'utf8')
    expect(hook).toContain('npm run verify')
  })

  it('la CI n\'appelle que verify pour le job de synthèse', () => {
    const ci = readFileSync('.github/workflows/ci.yml', 'utf8')
    expect(ci).toContain('npm run verify')
  })

  it('verify enchaîne les six contrôles attendus', () => {
    const s = readFileSync('scripts/verify.mjs', 'utf8')
    for (const etape of ['format:check', 'lint', 'typecheck', 'test', 'knip', 'build']) {
      expect(s, `Contrôle manquant dans verify : ${etape}`).toContain(etape)
    }
  })
})
```

Les deux tests sur le hook et la CI sont ce qui empêche la dérive : le jour où quelqu'un ajoute un contrôle à la CI sans le mettre dans `verify`, le local et la CI cessent de vérifier la même chose.

- [ ] **Étape 2 : lancer l'épreuve pour la voir échouer**

Lancer : `npx vitest run tests/harness/verify.test.ts`
Attendu : ÉCHEC — ni le script, ni le hook, ni la CI n'existent.

- [ ] **Étape 3 : écrire `scripts/verify.mjs`**

```javascript
#!/usr/bin/env node
import { spawnSync } from 'node:child_process'

const SEUIL_MS = 45000

const etapes = [
  ['format:check', ['npm', 'run', 'format:check']],
  ['lint', ['npm', 'run', 'lint']],
  ['typecheck', ['npm', 'run', 'typecheck']],
  ['test', ['npm', 'run', 'test']],
  ['knip', ['npm', 'run', 'knip']],
  ['build', ['npm', 'run', 'build']],
]

const debut = Date.now()
const durees = []
let echec = null

for (const [nom, commande] of etapes) {
  const t0 = Date.now()
  const [bin, ...args] = commande
  const r = spawnSync(bin, args, { stdio: 'inherit', shell: process.platform === 'win32' })
  const ms = Date.now() - t0
  durees.push([nom, ms])
  if (r.status !== 0) {
    echec = nom
    break
  }
}

const total = Date.now() - debut
console.log('\n─── verify ───')
for (const [nom, ms] of durees) console.log(`  ${nom.padEnd(14)} ${(ms / 1000).toFixed(1)} s`)
console.log(`  ${'TOTAL'.padEnd(14)} ${(total / 1000).toFixed(1)} s`)

if (echec) {
  console.error(`\nÉCHEC sur : ${echec}`)
  process.exit(1)
}

if (total > SEUIL_MS) {
  console.warn(
    `\nAVERTISSEMENT : verify a pris ${(total / 1000).toFixed(1)} s, au-delà du seuil de ${SEUIL_MS / 1000} s.\n` +
      'Une boucle de rétroaction lente est un défaut à traiter — docs/08-workflow.md § 5.',
  )
}
```

Le dépassement de seuil avertit sans bloquer : un harnais lent est un défaut, pas une erreur.

- [ ] **Étape 4 : ajouter le script**

```json
{ "scripts": { "verify": "node scripts/verify.mjs" } }
```

- [ ] **Étape 5 : lancer verify et mesurer**

Lancer : `npm run verify`
Attendu : les six contrôles passent, la durée totale s'affiche. Noter cette durée dans le rapport de lot.

- [ ] **Étape 6 : commit**

```bash
git add -A
git commit -m "ajoute la commande de vérification unique"
```

---

## Tâche 11 : hooks Git et détection de secrets

**Fichiers :**
- Créer : `.husky/pre-commit`, `.husky/pre-push`, `.gitleaks.toml`, `tests/harness/fixtures/faux-secret.txt`, `tests/harness/secrets.test.ts`
- Modifier : `package.json`

**Interfaces :**
- Consomme : `npm run verify` de la tâche 10
- Produit : le refus au commit et au push.

- [ ] **Étape 1 : écrire l'épreuve**

`tests/harness/secrets.test.ts` :

```typescript
import { existsSync } from 'node:fs'
import { describe, expect, it } from 'vitest'
import { lancerOutil } from './run-outil'

const FIXTURE = 'tests/harness/fixtures/faux-secret.txt'

describe('garde-fou : secrets', () => {
  it('la fixture de violation existe', () => {
    expect(existsSync(FIXTURE), `Cible manquante : ${FIXTURE}`).toBe(true)
  })

  it('gitleaks détecte une clé au format reconnu', () => {
    const r = lancerOutil(['npx', 'gitleaks', 'detect', '--no-git', '--source', 'tests/harness/fixtures', '--redact'])
    expect(r.code, `gitleaks n'a rien vu :\n${r.sortie}`).not.toBe(0)
  })

  it('les deux hooks existent', () => {
    expect(existsSync('.husky/pre-commit')).toBe(true)
    expect(existsSync('.husky/pre-push')).toBe(true)
  })
})
```

- [ ] **Étape 2 : lancer l'épreuve pour la voir échouer**

Lancer : `npx vitest run tests/harness/secrets.test.ts`
Attendu : ÉCHEC.

- [ ] **Étape 3 : installer Husky, lint-staged et gitleaks**

```bash
npm install -D husky lint-staged gitleaks
npx husky init
```

- [ ] **Étape 4 : écrire les hooks**

`.husky/pre-commit` :

```bash
npx lint-staged
npx gitleaks protect --staged --redact --config .gitleaks.toml
```

`.husky/pre-push` :

```bash
npm run verify
```

- [ ] **Étape 5 : configurer lint-staged et gitleaks**

Dans `package.json` :

```json
{
  "lint-staged": {
    "*.{ts,tsx}": ["eslint --fix", "prettier --write"],
    "*.{json,md,css,html}": ["prettier --write"]
  }
}
```

`.gitleaks.toml` :

```toml
title = "palier"

[extend]
useDefault = true

[[rules]]
id = "cle-anthropic"
description = "Clé API Anthropic"
regex = '''sk-ant-[A-Za-z0-9_\-]{20,}'''

[[rules]]
id = "cle-supabase-service"
description = "Clé de service Supabase"
regex = '''eyJ[A-Za-z0-9_\-]{20,}\.[A-Za-z0-9_\-]{20,}\.[A-Za-z0-9_\-]{20,}'''
```

- [ ] **Étape 6 : créer la fixture**

`tests/harness/fixtures/faux-secret.txt` :

```
# Faux secret, format valide, valeur inventée. Sert à prouver que gitleaks mord.
ANTHROPIC_API_KEY=sk-ant-api03-CECI-EST-UN-FAUX-SECRET-DE-TEST-0000000000
```

- [ ] **Étape 7 : lancer l'épreuve et vérifier le refus réel au commit**

Lancer : `npx vitest run tests/harness/secrets.test.ts`
Attendu : 3 tests passent.

Puis provoquer un refus réel :

```bash
echo "export const x: any = 1" > src/violation-temporaire.ts
git add src/violation-temporaire.ts
git commit -m "essai de refus"
```

Attendu : le commit est **refusé** par le hook. Nettoyer ensuite :

```bash
git reset HEAD src/violation-temporaire.ts && rm src/violation-temporaire.ts
```

- [ ] **Étape 8 : commit**

```bash
git add -A
git commit -m "installe les hooks de commit et de push"
```

---

## Tâche 12 : documents de travail

**Fichiers :**
- Créer : `docs/decisions.md`, `docs/gabarit-rapport-lot.md`, `docs/securite/asvs-l2.md`

**Interfaces :**
- Consomme : les décisions de la spec, § 2
- Produit : les trois documents lus au démarrage de chaque session.

- [ ] **Étape 1 : écrire `docs/decisions.md`**

Une entrée par décision, avec ce format exact :

```markdown
# Journal des décisions

**Lu au démarrage de chaque session, pas rempli à la fin.**
Une décision qu'on ne relit pas au démarrage se reprend.

## D1 — Nom du projet : palier
**Tranché le :** 19/08/2026
**Motif :** recommandation de `15-marque.md` § 3, cohérent avec le système de progression.
**Ce qui la rouvrirait :** une antériorité trouvée au registre BOIP ou EUIPO, ou une priorité donnée à l'international.

## D2 — Gestionnaire de paquets : npm
**Tranché le :** 19/08/2026
**Motif :** cohérent avec `DEMARRAGE.md`, déjà autorisé dans les permissions, détecté nativement par Vercel.
**Ce qui la rouvrirait :** une durée de `verify` durablement au-delà du seuil imputable à l'installation des dépendances.
```

Compléter avec D3 à D7 : fonctions serveur dans `api/`, référentiel ASVS L2 + Top 10 CI/CD, franchissement permanent, sévérité à deux niveaux, découpage de l'étape 1 en sept lots. Chacune avec sa date, son motif et sa condition de réouverture.

- [ ] **Étape 2 : écrire `docs/gabarit-rapport-lot.md`**

```markdown
# Gabarit — rapport de fin de lot

## Ce qui a changé
Fichiers, compteurs, avant/après.

## Ce qui a cassé
Y compris ce qui a été cassé puis rattrapé.

## Ce que je signale sans y avoir touché
Trouvé en chemin, hors périmètre.

## Le franchissement
La preuve que le garde-fou refuse, pas sa relecture. Pour chaque épreuve :
ce qui a été provoqué, ce qui était attendu, ce qui s'est produit.

## Ce qui n'a pas pu être vérifié
Nommément. Un rapport sans incertitude est un rapport incomplet.

## Skills et agents invoqués
Et pour ceux qui ne l'ont pas été, pourquoi.
```

- [ ] **Étape 3 : écrire `docs/securite/asvs-l2.md`**

En-tête portant la version exacte de l'ASVS retenue, relevée au moment de la rédaction, puis un tableau : identifiant de l'exigence · intitulé · état (`couverte` / `non applicable` / `prévue lot N`) · mécanisme ou test qui la prouve.

Renseigner à ce stade les exigences qui relèvent de la chaîne de build et de la gestion des secrets — elles sont couvertes par ce lot. Marquer les exigences de contrôle d'accès `prévue lot 3`, celles d'authentification `prévue lot 4`, celles de journalisation `prévue lot 7`.

- [ ] **Étape 4 : commit**

```bash
git add -A
git commit -m "ajoute le journal des décisions, le gabarit de rapport et le suivi ASVS"
```

---

## Tâche 13 : intégration continue

**Fichiers :**
- Créer : `.github/workflows/ci.yml`, `.github/dependabot.yml`, `vercel.json`

**Interfaces :**
- Consomme : `npm run verify`
- Produit : la CI qui bloque la fusion.

- [ ] **Étape 1 : relever les empreintes SHA des actions**

Un tag est mutable ; une empreinte ne l'est pas. Relever la SHA du commit correspondant à la version courante de chaque action :

```bash
gh api repos/actions/checkout/git/ref/tags/v4 --jq '.object.sha'
gh api repos/actions/setup-node/git/ref/tags/v4 --jq '.object.sha'
```

Reporter les valeurs obtenues dans le fichier ci-dessous, à la place des `<SHA>`.

- [ ] **Étape 2 : écrire `.github/workflows/ci.yml`**

```yaml
name: CI

on:
  push:
    branches: [main]
  pull_request:

permissions:
  contents: read

concurrency:
  group: ${{ github.workflow }}-${{ github.ref }}
  cancel-in-progress: true

jobs:
  qualite:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@<SHA> # v4
      - uses: actions/setup-node@<SHA> # v4
        with:
          node-version-file: .nvmrc
          cache: npm
      - run: npm ci
      - run: npm run verify
      - run: npm run jscpd

  securite:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@<SHA> # v4
        with:
          fetch-depth: 0
      - uses: actions/setup-node@<SHA> # v4
        with:
          node-version-file: .nvmrc
          cache: npm
      - run: npm ci
      - run: npm audit --audit-level=high
      - run: npx gitleaks detect --redact --config .gitleaks.toml
      - run: npx semgrep --config=p/owasp-top-ten --error --quiet .

  e2e:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@<SHA> # v4
      - uses: actions/setup-node@<SHA> # v4
        with:
          node-version-file: .nvmrc
          cache: npm
      - run: npm ci
      - run: npx playwright install --with-deps chromium webkit
      - run: npm run e2e

  franchissement:
    needs: [qualite, securite, e2e]
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@<SHA> # v4
      - uses: actions/setup-node@<SHA> # v4
        with:
          node-version-file: .nvmrc
          cache: npm
      - run: npm ci
      - run: npx playwright install --with-deps chromium
      - name: Les garde-fous refusent-ils encore ?
        run: npx vitest run tests/harness
```

- [ ] **Étape 3 : écrire `.github/dependabot.yml`**

```yaml
version: 2
updates:
  - package-ecosystem: npm
    directory: /
    schedule:
      interval: weekly
    groups:
      mineures:
        update-types: [minor, patch]
  - package-ecosystem: github-actions
    directory: /
    schedule:
      interval: weekly
```

Le second bloc est celui qu'on oublie ; c'est pourtant lui qui porte le risque d'exécution.

- [ ] **Étape 4 : écrire `vercel.json`**

```json
{
  "headers": [
    {
      "source": "/(.*)",
      "headers": [
        { "key": "Content-Security-Policy", "value": "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data: blob:; font-src 'self'; connect-src 'self'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'" },
        { "key": "Strict-Transport-Security", "value": "max-age=63072000; includeSubDomains; preload" },
        { "key": "X-Content-Type-Options", "value": "nosniff" },
        { "key": "Referrer-Policy", "value": "strict-origin-when-cross-origin" },
        { "key": "Permissions-Policy", "value": "camera=(self), microphone=(), geolocation=()" }
      ]
    }
  ]
}
```

`camera=(self)` est nécessaire : la saisie par photo d'étiquette et le scan de code-barres en dépendent aux lots ultérieurs.

- [ ] **Étape 5 : pousser et vérifier que la CI passe**

```bash
git add -A
git commit -m "ajoute l'intégration continue et les en-têtes de sécurité"
git push -u origin feat/lot-1-harnais
gh pr create --title "Lot 1 — harnais et outillage" --body "Voir docs/superpowers/specs/2026-08-19-lot-1-harnais-design.md"
```

Attendu : les quatre jobs passent au vert. En cas d'échec, corriger avant d'aller plus loin — une CI rouge n'est jamais « temporaire ».

- [ ] **Étape 6 : protéger `main`**

```bash
gh api -X PUT repos/warrox1993/palier/branches/main/protection \
  -F required_status_checks[strict]=true \
  -F 'required_status_checks[contexts][]=franchissement' \
  -F enforce_admins=false \
  -F required_pull_request_reviews[required_approving_review_count]=0 \
  -F restrictions=null \
  -F required_linear_history=true \
  -F allow_force_pushes=false
```

- [ ] **Étape 7 : commit final et fusion**

Invoquer `superpowers:finishing-a-development-branch`.

---

## Auto-revue du plan

**Couverture de la spec.** Chaque section de la spec pointe vers une tâche : ossature → T1 · chaîne d'outils → T1, T2, T4, T7, T9 · règles mécanisées → T4, T5, T6 · code mort → T8 · hooks → T11 · pipeline → T13 · sécurité de la chaîne → T13 · livraison → T13 · maintenance → T13 · ASVS → T12 · suite de franchissement → T3 à T11 · critère de sortie → T13 · workflow agentique → T10, T12.

**Douze épreuves, une par garde-fou.** TypeScript strict (T3) · ESLint `any` (T4) · nommage (T4) · pureté de `core/` (T5) · i18n (T6) · couleurs hors jetons (T6) · Prettier (T7) · Knip (T8) · axe-core (T9) · point d'entrée unique (T10) · gitleaks (T11) · hooks (T11). L'épreuve de test rouge est portée par le motif lui-même : chaque tâche exige de voir l'épreuve échouer avant d'installer le garde-fou.

**Épreuve de cible manquante :** présente dans chaque fichier d'épreuve, en premier test.

**Cohérence des noms :** `lancerOutil` défini en T2 est utilisé sous ce nom exact en T3, T4, T5, T6, T7, T8, T9 et T11. `npm run verify` défini en T10 est appelé sous ce nom en T11 et T13.

**Point à vérifier au moment de l'exécution :** `gitleaks` distribué en paquet npm peut ne pas exposer de binaire sur toutes les plateformes. Si `npx gitleaks` échoue sous Windows, replier sur l'action GitHub officielle pour la CI et sur une installation locale pour le hook — et le consigner dans `docs/decisions.md`.
