# Brief — Tâche 3

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

## Tâche 3 : Réorganiser en `front/` et `back/`

**Fichiers :**
- Déplacer : `package.json`, `package-lock.json`, `tsconfig*.json`, `vite.config.ts`, `vitest.config.ts`, `index.html`, `src/`, `tests/`, `.prettierrc`, `.prettierignore` → `front/`
- Modifier : `.gitignore`, `.husky/pre-commit`, `.husky/pre-push`
- Créer : `package.json` à la racine (orchestration seulement)

**Interfaces :**
- Consomme : le harnais front du lot 1a
- Produit : `npm run verify` à la racine, qui délègue au front. Les scripts front restent inchangés **dans** `front/`.

> **Pourquoi cette tâche est la plus risquée du lot.** Elle ne crée rien et casse potentiellement tout : chaque chemin relatif du harnais existant doit continuer à résoudre. Elle est isolée en première position pour que sa revue porte sur elle seule.

- [ ] **Étape 1 : créer la branche et constater l'état vert AVANT de bouger quoi que ce soit**

```bash
git checkout -b feat/lot-1b-harnais-backend
npm run typecheck && npm run build && npm run test
```

Attendu : les trois passent. Noter leur sortie — c'est la référence à retrouver à l'étape 6. Si l'un échoue **avant** le déplacement, arrêter et signaler : on ne réorganise pas sur un socle cassé.

- [ ] **Étape 2 : déplacer avec git, pour que l'historique suive**

```bash
mkdir -p front
git mv package.json package-lock.json tsconfig.json tsconfig.node.json front/
git mv vite.config.ts vitest.config.ts index.html front/
git mv src tests front/
git mv .prettierrc .prettierignore front/ 2>/dev/null || true
[ -f .env.example ] && git mv .env.example front/
git status
```

`git mv` plutôt que `mv` : l'historique de chaque fichier reste attaché, et `git log --follow` continue de fonctionner.

- [ ] **Étape 3 : créer le `package.json` racine, qui n'orchestre que**

```json
{
  "name": "palier",
  "version": "0.1.0",
  "private": true,
  "license": "UNLICENSED",
  "description": "Application de suivi de musculation et de nutrition, pour éviter les excès et les blessures.",
  "type": "module",
  "engines": { "node": "24.x" },
  "scripts": {
    "verify": "node scripts/verify.mjs",
    "front": "npm --prefix front run",
    "front:dev": "npm --prefix front run dev"
  }
}
```

Ce fichier ne porte **aucune dépendance** : les dépendances du front restent dans `front/package.json`.

- [ ] **Étape 4 : corriger les hooks — seulement s'ils existent déjà**

**Vérifier d'abord :** `ls .husky/` . Si le répertoire n'existe pas, **passer cette étape** : la tâche 19 installe Husky et écrit ces deux fichiers avec un contenu qui tient déjà compte de la structure `front`/`back`. Cette étape n'a de sens que si le harnais front a été installé avant la réorganisation, ce qui n'est pas le cas dans l'ordre actuel du plan.

Si les hooks existent, les corriger ainsi.

`.husky/pre-commit` :

```bash
npx --prefix front lint-staged
npx gitleaks protect --staged --redact --config .gitleaks.toml
```

`.husky/pre-push` :

```bash
npm run verify
```

- [ ] **Étape 5 : corriger `.gitignore`**

Remplacer les chemins racine par leurs équivalents :

```
front/node_modules/
front/dist/
front/coverage/
front/playwright-report/
front/test-results/
back/**/bin/
back/**/obj/
node_modules/
.env
.env.local
*.local
.DS_Store
```

- [ ] **Étape 6 : retrouver l'état vert, exactement**

```bash
npm --prefix front run typecheck
npm --prefix front run build
npm --prefix front run test
```

Attendu : les trois passent, avec le **même nombre de tests** qu'à l'étape 1. Un test qui disparaît sans échouer est le défaut le plus dangereux de cette tâche : le compte doit être identique, pas seulement vert.

- [ ] **Étape 7 : commit**

```bash
git add -A
git commit -m "réorganise le dépôt en front et back"
```

---

---
