# Brief — Tâche 13

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

## Tâche 13 : intégration continue

**Fichiers :**
- Créer : `.github/workflows/ci.yml`, `.github/dependabot.yml`, `vercel.json`

**Interfaces :**
- Consomme : `npm run verify`
- Produit : la CI qui bloque la fusion.

- [ ] **Étape 1 : relever les empreintes SHA des actions**

Un tag est mutable ; une empreinte ne l'est pas. Relever la SHA du commit correspondant à la version courante de chaque action :

```bash
gh api repos/actions/checkout/git/ref/tags/v4 --jq '.object.sha'
gh api repos/actions/setup-node/git/ref/tags/v4 --jq '.object.sha'
```

Reporter les valeurs obtenues dans le fichier ci-dessous, à la place des `<SHA>`.

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
  qualite:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@<SHA> # v4
      - uses: actions/setup-node@<SHA> # v4
        with:
          node-version-file: .nvmrc
          cache: npm
      - run: npm ci
      - run: npm run verify
      - run: npm run jscpd
        continue-on-error: true

  securite:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@<SHA> # v4
        with:
          fetch-depth: 0
      - uses: actions/setup-node@<SHA> # v4
        with:
          node-version-file: .nvmrc
          cache: npm
      - run: npm ci
      - run: npm audit --audit-level=high
      - run: npx gitleaks detect --redact --config .gitleaks.toml
      - run: npx semgrep --config=p/owasp-top-ten --error --quiet .

  e2e:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@<SHA> # v4
      - uses: actions/setup-node@<SHA> # v4
        with:
          node-version-file: .nvmrc
          cache: npm
      - run: npm ci
      - run: npx playwright install --with-deps chromium webkit
      - run: npm run e2e

  franchissement:
    needs: [qualite, securite, e2e]
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@<SHA> # v4
      - uses: actions/setup-node@<SHA> # v4
        with:
          node-version-file: .nvmrc
          cache: npm
      - run: npm ci
      - run: npx playwright install --with-deps chromium
      - name: La CI n'appelle que verify
        run: grep -q 'npm run verify' .github/workflows/ci.yml
      - name: Les garde-fous refusent-ils encore ?
        run: npm run test:harness
```

- [ ] **Étape 3 : écrire `.github/dependabot.yml`**

```yaml
version: 2
updates:
  - package-ecosystem: npm
    directory: /
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

Le second bloc est celui qu'on oublie ; c'est pourtant lui qui porte le risque d'exécution.

- [ ] **Étape 4 : écrire `vercel.json`**

```json
{
  "headers": [
    {
      "source": "/(.*)",
      "headers": [
        { "key": "Content-Security-Policy", "value": "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data: blob:; font-src 'self'; connect-src 'self'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'" },
        { "key": "Strict-Transport-Security", "value": "max-age=63072000; includeSubDomains; preload" },
        { "key": "X-Content-Type-Options", "value": "nosniff" },
        { "key": "Referrer-Policy", "value": "strict-origin-when-cross-origin" },
        { "key": "Permissions-Policy", "value": "camera=(self), microphone=(), geolocation=()" }
      ]
    }
  ]
}
```

`camera=(self)` est nécessaire : la saisie par photo d'étiquette et le scan de code-barres en dépendent aux lots ultérieurs.

- [ ] **Étape 5 : pousser et vérifier que la CI passe**

```bash
git add -A
git commit -m "ajoute l'intégration continue et les en-têtes de sécurité"
git push -u origin feat/lot-1-harnais
gh pr create --title "Lot 1 — harnais et outillage" --body "Voir docs/superpowers/specs/2026-08-19-lot-1-harnais-design.md"
```

Attendu : les quatre jobs passent au vert. En cas d'échec, corriger avant d'aller plus loin — une CI rouge n'est jamais « temporaire ».

- [ ] **Étape 6 : protéger `main`**

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

- [ ] **Étape 7 : commit final et fusion**

Invoquer `superpowers:finishing-a-development-branch`.

---
