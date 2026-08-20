# Brief — Tâche 8

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

## Tâche 8 : Prettier

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

---
