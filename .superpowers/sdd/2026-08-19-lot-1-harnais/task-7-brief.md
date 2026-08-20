# Brief — Tâche 7

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
