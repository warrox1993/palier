# Brief — Tâche 5

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
