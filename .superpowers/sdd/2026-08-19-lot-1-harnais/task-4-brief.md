# Brief — Tâche 4

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
{ "scripts": { "lint": "eslint src tests --ignore-pattern 'tests/harness/fixtures/**'" } }
```

Lancer : `npx vitest run tests/harness/eslint-base.test.ts`
Attendu : 4 tests passent.

- [ ] **Étape 7 : vérifier que le projet principal est propre**

Lancer : `npm run lint`
Attendu : aucune erreur. Les fixtures sont exclues par `--ignore-pattern` dans le script, et atteintes uniquement par les épreuves qui les nomment.

**Ne pas les placer dans `ignores` de la flat config** : le comportement de `--no-ignore` face à ce champ n'est pas garanti en ESLint 9, et les épreuves cesseraient silencieusement de voir leurs cibles. Ruling C5 du ledger.

- [ ] **Étape 8 : commit**

```bash
git add -A
git commit -m "éprouve les règles ESLint de typage et de nommage"
```

---
