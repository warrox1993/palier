# Brief — Tâche 4

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

## Tâche 4 : Épreuve du typecheck — le `any` interdit

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

---
