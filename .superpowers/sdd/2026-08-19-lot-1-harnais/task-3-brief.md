# Brief — Tâche 3

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
