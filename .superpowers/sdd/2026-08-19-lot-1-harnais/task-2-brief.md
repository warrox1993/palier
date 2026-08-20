# Brief — Tâche 2

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
    // L'epreuve d'accessibilite lance Playwright : elle n'appartient qu'a
    // test:harness, execute par le job de CI qui installe les navigateurs.
    exclude: ['**/node_modules/**', 'tests/harness/accessibilite.test.ts'],
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
Attendu : ÉCHEC — `run-outil.ts` n'existe pas encore.

**Ordre impératif :** écrire le test de l'étape 4 avant l'implémentation de l'étape 3. Si l'implémentation a déjà été écrite, la supprimer et recommencer — `08-workflow.md` § 11 : « Écrire du code avant son test — ce code est supprimé, pas corrigé. » Ruling C4 du ledger.

- [ ] **Étape 6 : ajouter le script et vérifier que les tests passent**

```json
{
  "scripts": {
    "test": "vitest run",
    "test:harness": "vitest run tests/harness --exclude ''",
    "test:watch": "vitest"
  }
}
```

Lancer : `npm run test`
Attendu : 3 tests passent.

`test` exclut l'épreuve d'accessibilité, qui exige les navigateurs Playwright ; `test:harness` lance les douze épreuves et n'est appelé que par le job de CI qui les installe. Sans cette séparation, le job qualité échouerait pour une raison sans rapport avec ce qu'il mesure. Ruling C3 du ledger.

- [ ] **Étape 7 : commit**

```bash
git add -A
git commit -m "installe Vitest et l'utilitaire de franchissement"
```

---
