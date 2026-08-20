# Brief — Tâche 6

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
