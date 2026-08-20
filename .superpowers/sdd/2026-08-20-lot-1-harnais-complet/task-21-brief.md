# Brief — Tâche 21

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

## Tâche 21 : Intégration continue, front et backend

**Fichiers :**
- Créer : `.github/workflows/ci.yml`, `.github/dependabot.yml`

**Interfaces :**
- Consomme : `npm run verify`, `npm run test:harness`
- Produit : la CI qui bloque la fusion.

> Cette tâche remplace les deux définitions de CI des plans séparés. **Aucun `vercel.json` n'est produit** : l'hébergement passe chez OVHcloud, et la configuration de déploiement appartient au lot 9.

- [ ] **Étape 1 : relever les empreintes SHA des actions**

Un tag est mutable, une empreinte ne l'est pas. Une action compromise puis repointée exécuterait du code arbitraire avec vos secrets.

```bash
gh api repos/actions/checkout/git/ref/tags/v4 --jq '.object.sha'
gh api repos/actions/setup-node/git/ref/tags/v4 --jq '.object.sha'
gh api repos/actions/setup-dotnet/git/ref/tags/v4 --jq '.object.sha'
```

Reporter les valeurs obtenues à la place des `<SHA>`.

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
  front:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@<SHA> # v4
      - uses: actions/setup-node@<SHA> # v4
        with:
          node-version-file: .nvmrc
          cache: npm
          cache-dependency-path: front/package-lock.json
      - run: npm --prefix front ci
      - run: npm --prefix front run format:check
      - run: npm --prefix front run lint
      - run: npm --prefix front run typecheck
      - run: npm --prefix front run test
      - run: npm --prefix front run knip
      - run: npm --prefix front run build
      - run: npm --prefix front run jscpd
        continue-on-error: true

  backend:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@<SHA> # v4
      - uses: actions/setup-dotnet@<SHA> # v4
        with:
          global-json-file: global.json
      - run: dotnet restore back/Palier.sln
      - run: dotnet build back/Palier.sln --no-restore
      - run: dotnet test back/Palier.sln --no-build --settings back/coverage.runsettings
      - run: dotnet format back/Palier.sln --verify-no-changes
      - run: dotnet list back/Palier.sln package --vulnerable --include-transitive

  securite:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@<SHA> # v4
        with:
          fetch-depth: 0
      - uses: actions/setup-node@<SHA> # v4
        with:
          node-version-file: .nvmrc
      - run: npm --prefix front ci
      - run: npm --prefix front audit --audit-level=high
      - run: npx gitleaks detect --redact --config .gitleaks.toml
      - run: npx semgrep --config=p/owasp-top-ten --error --quiet .
      - run: node scripts/verifier-licences.mjs

  e2e:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@<SHA> # v4
      - uses: actions/setup-node@<SHA> # v4
        with:
          node-version-file: .nvmrc
          cache: npm
          cache-dependency-path: front/package-lock.json
      - run: npm --prefix front ci
      - run: npx --prefix front playwright install --with-deps chromium webkit
      - run: npm --prefix front run e2e

  performance:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@<SHA> # v4
      - uses: actions/setup-node@<SHA> # v4
        with:
          node-version-file: .nvmrc
          cache: npm
          cache-dependency-path: front/package-lock.json
      - run: npm --prefix front ci
      - run: npm --prefix front run build
      - run: npx --prefix front @lhci/cli autorun

  franchissement:
    needs: [front, backend, securite, e2e, performance]
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@<SHA> # v4
      - uses: actions/setup-node@<SHA> # v4
        with:
          node-version-file: .nvmrc
      - uses: actions/setup-dotnet@<SHA> # v4
        with:
          global-json-file: global.json
      - run: npm --prefix front ci
      - run: npx --prefix front playwright install --with-deps chromium
      - name: La CI n'appelle-t-elle que verify en local ?
        run: grep -q 'npm run verify' .husky/pre-push
      - name: Les garde-fous refusent-ils encore ?
        run: npm run test:harness
```

- [ ] **Étape 2 bis : écrire `front/.lighthouserc.json`**

`08-workflow.md` § 5 liste Lighthouse CI comme dixième élément du harnais, et
§ 9 fait de « Lighthouse supérieur à 90 » un critère de sortie du domaine
frontend. Il manquait au plan entier — trouvé par la revue de fin de tâche 5.

```json
{
  "ci": {
    "collect": {
      "staticDistDir": "./dist",
      "numberOfRuns": 3
    },
    "assert": {
      "assertions": {
        "categories:performance": ["error", { "minScore": 0.9 }],
        "categories:accessibility": ["error", { "minScore": 0.9 }],
        "categories:best-practices": ["error", { "minScore": 0.9 }],
        "categories:seo": ["warn", { "minScore": 0.9 }]
      }
    },
    "upload": { "target": "temporary-public-storage" }
  }
}
```

Ajouter `@lhci/cli` aux dépendances de développement du front.

> **Le seuil se recalibre au lot 2.** Aujourd'hui la seule page est le squelette
> Vite : un score élevé ne prouve rien sur le produit. Le garde-fou est installé
> maintenant pour qu'une régression soit visible dès la première vraie page,
> pas pour valider quoi que ce soit sur celle-ci. Le job échoue si le score
> baisse — c'est tout ce qu'on lui demande à ce stade.

- [ ] **Étape 3 : écrire `.github/dependabot.yml`**

```yaml
version: 2
updates:
  - package-ecosystem: npm
    directory: /front
    schedule:
      interval: weekly
    groups:
      mineures:
        update-types: [minor, patch]

  - package-ecosystem: nuget
    directory: /back
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

Le troisième bloc est celui qu'on oublie ; c'est pourtant lui qui porte le risque d'exécution.

- [ ] **Étape 4 : pousser et vérifier que la CI passe**

```bash
git add -A
git commit -m "ajoute l'intégration continue des deux écosystèmes"
git push -u origin feat/lot-1-harnais
gh pr create --title "Lot 1 — harnais front et backend" \
  --body "Voir docs/superpowers/specs/2026-08-19-architecture-backend-csharp-design.md"
```

Attendu : les cinq jobs passent. Une CI rouge n'est jamais « temporaire ».

- [ ] **Étape 5 : protéger `main`**

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

- [ ] **Étape 6 : fusionner**

Invoquer `superpowers:finishing-a-development-branch`.

---
