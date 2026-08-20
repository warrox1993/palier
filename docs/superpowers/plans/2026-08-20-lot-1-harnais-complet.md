# Lot 1 — Harnais complet, front et backend — Plan d'implémentation

> **Pour les exécutants agentiques :** SOUS-SKILL REQUISE — utiliser `superpowers:subagent-driven-development` (recommandé) ou `superpowers:executing-plans` pour exécuter ce plan tâche par tâche. Les étapes utilisent la syntaxe à cases (`- [ ]`) pour le suivi.

**Remplace :** `2026-08-19-lot-1-harnais.md` et `2026-08-20-lot-1b-harnais-backend.md`, désormais caducs. Ces deux plans définissaient chacun `verify` et la CI — le point d'entrée unique ne peut pas être défini deux fois, c'est précisément le défaut qu'il existe pour empêcher.

**But :** installer l'environnement déterministe du projet `palier`, front et backend, et prouver que chacun de ses garde-fous refuse effectivement ce qu'il prétend refuser.

**Architecture :** un dépôt à deux moitiés, `front/` en React et TypeScript, `back/` en C# suivant la Clean Architecture. Chaque garde-fou est installé selon un cycle rouge-vert : on écrit d'abord l'épreuve — une violation délibérée placée hors du build normal — on la voit passer faute de garde-fou, on installe le garde-fou, l'épreuve constate le refus. Une commande unique, `npm run verify` à la racine, enchaîne les deux moitiés ; le hook de pré-envoi et la CI n'appellent qu'elle.

**Pile technique :** Vite, React, TypeScript, Oxlint et tsgolint, Prettier, Vitest, Playwright, axe-core, Knip, jscpd, Husky, gitleaks, Semgrep · .NET 10, xUnit, coverlet, analyseurs Roslyn, `dotnet format` · GitHub Actions.

**Spec :** `docs/superpowers/specs/2026-08-19-architecture-backend-csharp-design.md`

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

## Structure des fichiers

| Fichier | Responsabilité |
|---|---|
| `package.json` (racine) | orchestration seule, aucune dépendance |
| `scripts/verify.mjs` | point d'entrée unique, front puis backend, mesure la durée |
| `scripts/verifier-licences.mjs` | contrôle des licences, npm **et** NuGet, sans dépendance |
| `global.json` | version du SDK .NET |
| `Directory.Build.props` | rigueur appliquée à tous les projets C# |
| `.editorconfig` | style et nommage, appliqués par `dotnet format` |
| `.husky/pre-commit` · `pre-push` | lint et secrets · `verify` |
| `.github/workflows/ci.yml` | cinq jobs, dont le franchissement qui bloque la fusion |
| `.github/dependabot.yml` | npm, NuGet **et** github-actions |
| `front/` | l'application React et son harnais |
| `back/Palier.{Domain,Application,Infrastructure,Api}/` | les quatre projets |
| `back/tests-harness/` | les épreuves de franchissement du backend |
| `docs/decisions.md` | journal des décisions, lu au démarrage |
| `docs/gabarit-rapport-lot.md` | les sections du rapport de fin de lot |
| `docs/securite/asvs-l2.md` | état de chaque exigence ASVS niveau 2 |

---

## Tâche 1 : Socle Vite + React + TypeScript strict — **LIVRÉE**

**Commits :** `fb6ef6c` · `ac2aedd` · `745ce96` · `2a769c2` · `851fb6f` · `f505cbd`

Livrée le 19/08/2026, revue passée, un round de correction. Ce qu'elle a produit : Vite, React, TypeScript strict avec quinze options, `npm run typecheck` couvrant les deux configurations TypeScript, `npm run build`, et surtout `"exclude": ["tests/harness/fixtures"]` dans `tsconfig.json` — la condition sans laquelle aucune épreuve de ce plan ne pourrait exister.

**Incident notable, consigné :** le `tsconfig.json` du plan initial utilisait `baseUrl`, que TypeScript 6 comme 7 refusent désormais. Corrigé en `paths` seul, testé sous les deux versions.

**Aucune action.** Les fichiers déplacent en `front/` à la tâche 3.

---

## Tâche 2 : Vitest et l'utilitaire de franchissement — **LIVRÉE**

**Commit :** `fe33880`

Livrée le 19/08/2026. Ce qu'elle a produit : Vitest, et la fonction `lancerOutil(commande: string[], options?: { cwd?: string }): { code: number; sortie: string }` que **toutes les épreuves de ce plan consomment**, front comme backend.

**Déviation acceptée :** le code du plan initial échouait sous Windows et Node 24 (`shell: true` avec un tableau d'arguments corrompt les guillemets). Corrigé en interne, signature inchangée.

**Point à traiter à la tâche 3 :** cet utilitaire vit dans `front/tests/harness/run-outil.ts` après la réorganisation. Les épreuves backend l'importent en traversant la frontière `front`/`back` ; si l'import échoue, la parade est une configuration vitest à la racine, à trancher par l'implémenteur et à consigner.

**Aucune action.**

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

## Tâche 5 : Oxlint — socle et règles syntaxiques

**Fichiers :**
- Créer : `front/.oxlintrc.json`, `front/tests/harness/fixtures/any-explicite.ts`, `front/tests/harness/fixtures/nommage-Invalide.ts`, `front/tests/harness/oxlint-base.test.ts`
- Modifier : `front/package.json` (script `lint`)

**Interfaces :**
- Consomme : `lancerOutil` de la tâche 2
- Produit : `npm --prefix front run lint`, et le fichier `.oxlintrc.json` que les tâches 6 et 7 enrichissent sans le recréer.

> **Pourquoi Oxlint et non ESLint.** `typescript-eslint` 8.67.0, publié le 10/08/2026, exige `typescript >=4.8.4 <6.1.0` — vérifié sur le registre npm. TypeScript 7.0.2 est hors de cette plage. Oxlint ne dépend pas de l'API du compilateur pour ses règles syntaxiques, et son composant type-aware `tsgolint` est **construit sur TypeScript 7** : ses versions suivent celles du compilateur (`v7.0.2001` = TypeScript 7.0.2, patch 001). C'est l'option alignée sur la version installée, pas un contournement.

- [ ] **Étape 1 : écrire l'épreuve**

`front/tests/harness/oxlint-base.test.ts` :

```typescript
import { existsSync } from 'node:fs'
import { describe, expect, it } from 'vitest'
import { lancerOutil } from './run-outil'

const ANY = 'tests/harness/fixtures/any-explicite.ts'
const NOMMAGE = 'tests/harness/fixtures/nommage-Invalide.ts'

describe('garde-fou : Oxlint, règles syntaxiques', () => {
  it.each([ANY, NOMMAGE])('la fixture %s existe', (f) => {
    expect(existsSync(f), `Cible manquante : ${f}`).toBe(true)
  })

  it('refuse un any explicite', () => {
    const r = lancerOutil(['npx', 'oxlint', ANY])
    expect(r.code, `Oxlint a accepté le any :\n${r.sortie}`).not.toBe(0)
    expect(r.sortie).toContain('no-explicit-any')
  })

  it('refuse un nom de fichier hors kebab-case et PascalCase', () => {
    const r = lancerOutil(['npx', 'oxlint', NOMMAGE])
    expect(r.code, `Oxlint a accepté le nommage :\n${r.sortie}`).not.toBe(0)
    expect(r.sortie).toMatch(/filename-case/)
  })
})
```

- [ ] **Étape 2 : lancer l'épreuve pour la voir échouer**

Lancer : `npm --prefix front run test:harness -- oxlint-base`
Attendu : ÉCHEC — Oxlint n'est pas installé.

- [ ] **Étape 3 : installer Oxlint**

```bash
npm --prefix front add -D oxlint
```

- [ ] **Étape 4 : écrire `front/.oxlintrc.json`**

```json
{
  "$schema": "./node_modules/oxlint/configuration_schema.json",
  "plugins": ["typescript", "unicorn", "import", "jsx-a11y", "react"],
  "env": { "browser": true, "es2024": true },
  "categories": { "correctness": "error" },
  "ignorePatterns": ["dist/**", "coverage/**"],
  "rules": {
    "typescript/no-explicit-any": "error",
    "eslint/no-unused-vars": "error",
    "unicorn/filename-case": ["error", { "cases": { "kebabCase": true, "pascalCase": true } }],
    "import/no-cycle": "error",
    "eslint/no-console": "error"
  },
  "overrides": [
    {
      "files": ["**/*.test.{ts,tsx}", "**/*.spec.ts"],
      "rules": { "typescript/no-explicit-any": "off" }
    }
  ]
}
```

**Les fixtures ne figurent pas dans `ignorePatterns`, et c'est délibéré.** Un fichier ignoré par la configuration reste ignoré même lorsqu'on le nomme explicitement en ligne de commande — vérifié empiriquement, et `--no-ignore` ne le contourne pas davantage. Placées là, les fixtures deviendraient invisibles **pour leurs propres épreuves**, qui passeraient au vert sans rien contrôler.

L'exclusion est donc portée par le script `lint`, au moyen de `--ignore-pattern`. Le lint courant ne voit pas les fixtures ; les épreuves, qui appellent `oxlint` sans ce drapeau, les voient.

**Guillemets doubles obligatoires** autour du motif : sous `cmd.exe`, les guillemets simples ne sont pas interprétés comme des délimiteurs et le motif échoue silencieusement — l'exclusion ne s'applique alors pas, et `npm run lint` devient rouge en permanence sur les violations délibérées.

- [ ] **Étape 5 : créer les fixtures**

`front/tests/harness/fixtures/any-explicite.ts` :

```typescript
// Violation délibérée : any explicite.
export function traite(valeur: any): string {
  return String(valeur)
}
```

`front/tests/harness/fixtures/nommage-Invalide.ts` :

```typescript
// Violation délibérée : nom de fichier ni kebab-case ni PascalCase pur.
export const valeur = 1
```

- [ ] **Étape 6 : ajouter le script et lancer l'épreuve**

Dans `front/package.json` :

```json
{ "scripts": { "lint": "oxlint --ignore-pattern \"tests/harness/fixtures/**\" src tests" } }
```

Lancer l'épreuve : attendu, 4 tests passent.

- [ ] **Étape 7 : vérifier que le front réel est propre**

Lancer : `npm --prefix front run lint`
Attendu : aucune erreur.

- [ ] **Étape 8 : commit**

```bash
git add -A
git commit -m "installe Oxlint et ses règles syntaxiques"
```

---

## Tâche 6 : Oxlint type-aware, et la frontière front / domaine

**Fichiers :**
- Modifier : `front/.oxlintrc.json`, `front/package.json`
- Créer : `front/tests/harness/fixtures/core-impur.ts`, `front/tests/harness/oxlint-architecture.test.ts`

**Interfaces :**
- Consomme : `.oxlintrc.json` de la tâche 5
- Produit : la barrière d'import qui protège `front/src/core/`, et les 59 règles type-aware.

> **La moitié de cette tâche a changé de nature avec l'architecture backend.** `src/llm/` a disparu du front — le modèle vit dans `Palier.Infrastructure`. `front/src/core/` reste légitime pour les conversions d'unités, le formatage et les agrégations d'affichage, mais **plus pour un calcul de conformité** : ceux-là vivent exclusivement dans `Palier.Domain`. L'interdiction des calculs de conformité est portée par la tâche 7, `no-restricted-syntax` n'existant pas dans Oxlint.

- [ ] **Étape 1 : écrire l'épreuve**

`front/tests/harness/oxlint-architecture.test.ts` :

```typescript
import { existsSync } from 'node:fs'
import { describe, expect, it } from 'vitest'
import { lancerOutil } from './run-outil'

const IMPUR = 'tests/harness/fixtures/core-impur.ts'

describe('garde-fou : pureté de front/src/core/', () => {
  it('la fixture de violation existe', () => {
    expect(existsSync(IMPUR), `Cible manquante : ${IMPUR}`).toBe(true)
  })

  it('refuse un import de React depuis core/', () => {
    const r = lancerOutil(['npx', 'oxlint', IMPUR])
    expect(r.code, `Oxlint a accepté l'import impur :\n${r.sortie}`).not.toBe(0)
    expect(r.sortie).toMatch(/no-restricted-imports/)
  })

  it('le linting type-aware est actif', () => {
    const r = lancerOutil(['npx', 'oxlint', '--type-aware', 'src'])
    expect(r.code, `Le mode type-aware a échoué :\n${r.sortie}`).toBe(0)
  })
})
```

- [ ] **Étape 2 : lancer l'épreuve pour la voir échouer**

Attendu : ÉCHEC — ni la fixture ni la règle n'existent, et `oxlint-tsgolint` n'est pas installé.

- [ ] **Étape 3 : installer le composant type-aware**

```bash
npm --prefix front add -D oxlint-tsgolint@latest
```

`tsgolint` s'appuie sur `typescript-go` et cible TypeScript 7 : il suit la version du compilateur au lieu de la contraindre.

- [ ] **Étape 4 : ajouter la barrière d'architecture à `.oxlintrc.json`**

Dans le tableau `overrides` :

```json
    {
      "files": ["src/core/**/*.ts", "tests/harness/fixtures/core-impur.ts"],
      "rules": {
        "eslint/no-restricted-imports": [
          "error",
          {
            "patterns": [
              { "group": ["react", "react-dom", "react/*"], "message": "core/ est pur : aucune dépendance UI." },
              { "group": ["dexie"], "message": "core/ est pur : aucun accès stockage." },
              { "group": ["@/lib/*", "@/ui/*", "@/features/*", "../lib/*", "../ui/*", "../features/*"], "message": "core/ est pur : aucune dépendance applicative." }
            ]
          }
        ]
      }
    }
```

Et activer les règles type-aware dans la section `rules` :

```json
    "typescript/no-floating-promises": "error",
    "typescript/no-misused-promises": "error",
    "typescript/await-thenable": "error",
    "typescript/no-unsafe-assignment": "warn",
    "typescript/strict-boolean-expressions": "warn"
```

Les deux premières sont bloquantes : une promesse non attendue dans une application hors ligne à file de retry produit des pertes de données silencieuses.

- [ ] **Étape 5 : créer la fixture**

`front/tests/harness/fixtures/core-impur.ts` :

```typescript
// Violation délibérée : un module pur qui importe React.
import { useState } from 'react'

export function agregeQuelqueChose(): number {
  const [valeur] = useState(0)
  return valeur
}
```

- [ ] **Étape 6 : ajouter le script type-aware**

```json
{
  "scripts": {
    "lint": "oxlint --ignore-pattern \"tests/harness/fixtures/**\" .",
    "lint:types": "oxlint --type-aware --ignore-pattern \"tests/harness/fixtures/**\" src"
  }
}
```

- [ ] **Étape 7 : lancer l'épreuve pour la voir passer**

Attendu : 3 tests passent.

- [ ] **Étape 7 bis : vérifier que le front réel reste propre**

Lancer : `npm run lint` et `npm run lint:types`.
Attendu : code 0, aucune sortie.

Cette étape existe parce que le script `lint` porte l'exclusion des fixtures
(ruling P6) : toute réécriture du script qui l'oublie fait rougir le lint sur
les violations délibérées. La tâche 5 avait cette étape ; son absence ici avait
laissé passer une régression jusqu'à la revue.

- [ ] **Étape 8 : commit**

```bash
git add -A
git commit -m "active le linting type-aware et la barrière de pureté du front"
```

> **Note pour la tâche 18.** Oxlint propose `oxlint --type-aware --type-check`, qui remplace l'étape `tsc --noEmit`. Ce plan conserve `tsc --noEmit` séparément : c'est le contrôle connu et éprouvé, et fusionner les deux au moment où l'on installe le harnais mêlerait deux changements. À évaluer une fois le lot stabilisé, et à consigner dans `docs/decisions.md`.

---

## Tâche 7 : Règles de projet — ce qu'aucun linter ne connaît

**Fichiers :**
- Créer : `scripts/regles-projet.mjs`, `front/tests/harness/fixtures/calcul-conformite.ts`, `front/tests/harness/fixtures/couleur-en-dur.ts`, `front/tests/harness/fixtures/chaine-en-dur.tsx`, `front/tests/harness/regles-projet.test.ts`
- Modifier : `package.json` racine

**Interfaces :**
- Consomme : rien
- Produit : `node scripts/regles-projet.mjs`, appelé par `verify`.

> **Pourquoi un script plutôt qu'une règle de linter.** Oxlint ne fournit ni `no-restricted-syntax`, ni équivalent d'`eslint-plugin-i18next`, ni `naming-convention`. Ces trois besoins sont de la recherche de motifs dans le source — un script les couvre, et il est plus lisible qu'un sélecteur AST. La règle qui interdit les calculs de conformité passe de ceci :
>
> `Identifier[name=/^(mifflin|katch|tdee|limiteHaute|epley)/i]`
>
> à une phrase en clair dans un tableau de motifs, avec son message et son motif. C'est le même choix que pour le contrôle de licences : un outil dont on lit le code.

- [ ] **Étape 1 : écrire l'épreuve**

`front/tests/harness/regles-projet.test.ts` :

```typescript
import { existsSync } from 'node:fs'
import { describe, expect, it } from 'vitest'
import { lancerOutil } from './run-outil'

const FIXTURES = [
  'tests/harness/fixtures/calcul-conformite.ts',
  'tests/harness/fixtures/couleur-en-dur.ts',
  'tests/harness/fixtures/chaine-en-dur.tsx',
]

describe('garde-fou : règles de projet', () => {
  it.each(FIXTURES)('la fixture %s existe', (f) => {
    expect(existsSync(f), `Cible manquante : ${f}`).toBe(true)
  })

  it('accepte le code réel du front', () => {
    const r = lancerOutil(['node', '../scripts/regles-projet.mjs'])
    expect(r.code, `Le front réel viole une règle de projet :\n${r.sortie}`).toBe(0)
  })

  it('refuse un calcul de conformité dans le front', () => {
    const r = lancerOutil([
      'node', '../scripts/regles-projet.mjs',
      '--fichier', 'front/tests/harness/fixtures/calcul-conformite.ts',
    ])
    expect(r.code, `Le calcul de conformité a été accepté :\n${r.sortie}`).not.toBe(0)
    expect(r.sortie).toMatch(/Palier\.Domain/)
  })

  it('refuse une couleur hexadécimale hors jetons', () => {
    const r = lancerOutil([
      'node', '../scripts/regles-projet.mjs',
      '--fichier', 'front/tests/harness/fixtures/couleur-en-dur.ts',
    ])
    expect(r.code, `La couleur a été acceptée :\n${r.sortie}`).not.toBe(0)
    expect(r.sortie).toMatch(/jeton/i)
  })

  it('refuse une chaîne de texte en dur dans le JSX', () => {
    const r = lancerOutil([
      'node', '../scripts/regles-projet.mjs',
      '--fichier', 'front/tests/harness/fixtures/chaine-en-dur.tsx',
    ])
    expect(r.code, `La chaîne en dur a été acceptée :\n${r.sortie}`).not.toBe(0)
    expect(r.sortie).toMatch(/i18n/i)
  })
})
```

- [ ] **Étape 2 : lancer l'épreuve pour la voir échouer**

Attendu : ÉCHEC — le script n'existe pas.

- [ ] **Étape 3 : écrire `scripts/regles-projet.mjs`**

```javascript
#!/usr/bin/env node
// Règles propres à ce projet, qu'aucun linter généraliste ne connaît.
// Remplace no-restricted-syntax, eslint-plugin-i18next et naming-convention,
// absents d'Oxlint. Aucune dépendance : on lit ce qu'on exécute.
import { readFileSync, readdirSync, statSync } from 'node:fs'
import { join, extname, basename } from 'node:path'

const REGLES = [
  {
    id: 'calcul-de-conformite',
    motif:
      /\b(mifflin|katchMcArdle|tdeeAdaptatif|metabolismeDeBase|limiteHauteEfsa|plancherCalorique|plancherProteique|epleyCorrige)\w*/i,
    extensions: ['.ts', '.tsx'],
    message:
      'Les calculs de conformité vivent dans Palier.Domain, jamais dans le front. ' +
      "Le front affiche ce que l'API a calculé ; hors ligne, la dernière valeur connue avec son horodatage.",
  },
  {
    id: 'couleur-hors-jetons',
    motif: /#[0-9a-fA-F]{3}(?:[0-9a-fA-F]{3})?\b/,
    extensions: ['.ts', '.tsx', '.css'],
    exclure: [/src[\\/]ui[\\/]jetons\./],
    message: 'Aucune couleur hors des jetons de src/ui/jetons.ts — docs/02-design.md § 9.',
  },
  {
    id: 'ombre-ou-degrade',
    motif: /\b(box-shadow|drop-shadow|backdrop-blur|backdrop-filter|linear-gradient|radial-gradient)\b/,
    extensions: ['.ts', '.tsx', '.css'],
    message: 'Aucune ombre portée, aucun flou, aucun dégradé — docs/02-design.md § 2 et § 4.',
  },
  {
    id: 'fleche-unicode',
    motif: /["'`][^"'`]*[→←↑↓][^"'`]*["'`]/,
    extensions: ['.ts', '.tsx'],
    message: "Aucune flèche Unicode dans un libellé : utiliser une icône, ou rien.",
  },
  {
    id: 'chaine-en-dur',
    // Texte visible entre balises JSX : > Bonjour <
    motif: />\s*[A-Za-zÀ-ÿ][A-Za-zÀ-ÿ ,.'’!?-]{2,}\s*</,
    extensions: ['.tsx'],
    message:
      'Aucune chaîne de texte en dur : tout passe par i18next, y compris les erreurs et les états vides — CLAUDE.md § 4.',
  },
  {
    id: 'booleen-mal-nomme',
    motif: /\b(?:const|let)\s+(?!is|has|can|should)[a-z]\w*\s*:\s*boolean\b/,
    extensions: ['.ts', '.tsx'],
    message: "Un booléen se préfixe par is, has, can ou should — docs/16-projet.md § 2.",
  },
  {
    id: 'type-prefixe-i',
    motif: /\b(?:interface|type)\s+I[A-Z]\w*/,
    extensions: ['.ts', '.tsx'],
    message: 'Préfixe I interdit sur les types — docs/16-projet.md § 2.',
  },
]

const IGNORES = ['node_modules', 'dist', 'coverage', '.git', 'bin', 'obj', 'playwright-report']

function fichiers(racine) {
  const out = []
  const parcourir = (d) => {
    for (const e of readdirSync(d)) {
      if (IGNORES.includes(e)) continue
      const p = join(d, e)
      if (statSync(p).isDirectory()) parcourir(p)
      else out.push(p)
    }
  }
  parcourir(racine)
  return out
}

const iFichier = process.argv.indexOf('--fichier')
const cibles =
  iFichier !== -1
    ? [process.argv[iFichier + 1]]
    : fichiers('front/src').concat(fichiers('front/tests').filter((f) => !f.includes('fixtures')))

const violations = []
for (const f of cibles) {
  const ext = extname(f)
  let contenu
  try {
    contenu = readFileSync(f, 'utf8')
  } catch {
    console.error(`Fichier illisible : ${f}`)
    process.exit(2)
  }
  const lignes = contenu.split('\n')
  for (const regle of REGLES) {
    if (!regle.extensions.includes(ext)) continue
    if (regle.exclure?.some((r) => r.test(f))) continue
    lignes.forEach((ligne, i) => {
      if (ligne.trimStart().startsWith('//')) return
      if (regle.motif.test(ligne)) {
        violations.push({ fichier: f, ligne: i + 1, regle: regle.id, message: regle.message })
      }
    })
  }
}

if (violations.length > 0) {
  console.error(`${violations.length} violation(s) des règles de projet :\n`)
  for (const v of violations) {
    console.error(`  ${v.fichier}:${v.ligne}  [${v.regle}]`)
    console.error(`    ${v.message}\n`)
  }
  process.exit(1)
}

console.log(`${cibles.length} fichiers vérifiés, aucune violation des règles de projet.`)
```

- [ ] **Étape 4 : créer les trois fixtures**

`front/tests/harness/fixtures/calcul-conformite.ts` :

```typescript
export function mifflinStJeor(poidsKg: number, tailleCm: number, age: number): number {
  return 10 * poidsKg + 6.25 * tailleCm - 5 * age + 5
}
```

`front/tests/harness/fixtures/couleur-en-dur.ts` :

```typescript
export const fondInterdit = '#D97757'
```

`front/tests/harness/fixtures/chaine-en-dur.tsx` :

```typescript
export function Bouton() {
  return <button type="button">Enregistrer la séance</button>
}
```

Aucune de ces fixtures ne porte de commentaire explicatif en tête : le script ignore les lignes commentées, et un commentaire décrivant la violation la masquerait.

- [ ] **Étape 5 : ajouter le script racine**

```json
{ "scripts": { "regles": "node scripts/regles-projet.mjs" } }
```

- [ ] **Étape 6 : lancer l'épreuve pour la voir passer**

Attendu : 7 tests passent — les trois fixtures existent, le front réel est accepté, les trois violations sont refusées.

- [ ] **Étape 7 : mesurer les faux positifs**

Lancer `npm run regles` sur le front réel. Si des violations légitimes apparaissent — une couleur dans un commentaire de documentation, un texte de test — affiner le motif plutôt que d'ajouter une exception au fichier. **Consigner le nombre de motifs affinés dans le rapport de tâche** : un détecteur qui crie à tort est un détecteur qu'on cesse de lire.

- [ ] **Étape 8 : commit**

```bash
git add -A
git commit -m "ajoute les règles de projet que le linter ne couvre pas"
```

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
    // Seconde assertion obligatoire : un code non nul prouve seulement que
    // quelque chose a échoué, pas que Prettier a refusé. Sans elle, l'épreuve
    // passe au vert quand l'outil est simplement absent — mesuré.
    expect(r.sortie).toMatch(/Code style issues/)
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

## Tâche 9 : Knip et jscpd — code mort et duplication

**Fichiers :**
- Créer : `knip.json`, `.jscpd.json`, `tests/harness/fixtures/export-orphelin.ts`, `tests/harness/code-mort.test.ts`
- Modifier : `package.json`

**Interfaces :**
- Consomme : `lancerOutil`
- Produit : `npm run knip` (bloquant) et `npm run jscpd` (rapport, non bloquant).

- [ ] **Étape 1 : écrire l'épreuve**

`tests/harness/code-mort.test.ts` :

```typescript
import { existsSync } from 'node:fs'
import { describe, expect, it } from 'vitest'
import { lancerOutil } from './run-outil'

const FIXTURE = 'tests/harness/fixtures/export-orphelin.ts'

describe('garde-fou : code mort', () => {
  it('la fixture de violation existe', () => {
    expect(existsSync(FIXTURE), `Cible manquante : ${FIXTURE}`).toBe(true)
  })

  it('signale un export que personne n\'importe', () => {
    const r = lancerOutil(['npx', 'knip', '--config', 'knip.fixtures.json'])
    expect(r.code, `Knip n'a pas vu l'export orphelin :\n${r.sortie}`).not.toBe(0)
    expect(r.sortie).toMatch(/orphelin|unused|export/i)
  })
})
```

- [ ] **Étape 2 : lancer l'épreuve pour la voir échouer**

Lancer : `npx vitest run tests/harness/code-mort.test.ts`
Attendu : ÉCHEC.

- [ ] **Étape 3 : installer et configurer**

```bash
npm install -D knip jscpd
```

`knip.json` — les points d'entrée sont déclarés explicitement, faute de quoi Knip signale des faux positifs en masse :

```json
{
  "entry": [
    "src/main.tsx",
    "vite.config.ts",
    "vitest.config.ts",
    "playwright.config.ts",
    "scripts/verify.mjs",
    "tests/**/*.test.ts",
    "tests/**/*.spec.ts"
  ],
  "project": ["src/**/*.{ts,tsx}", "scripts/**/*.mjs"],
  "ignore": ["tests/harness/fixtures/**"],
  "ignoreDependencies": [
    "@testing-library/jest-dom",
    "@testing-library/react"
  ]
}
```

Les deux `@testing-library` sont installées et pas encore consommées : elles
attendent les premiers composants du lot 2. Elles sont déclarées ici plutôt que
retirées puis réinstallées, mais la mention est **datée** — si le lot 2 se
termine sans qu'elles soient câblées (aucun `setupFiles`, aucun import), elles
sortent de cette liste et du `package.json`.

`knip.fixtures.json` — configuration dédiée à l'épreuve :

```json
{
  "entry": ["tests/harness/fixtures/point-entree.ts"],
  "project": ["tests/harness/fixtures/*.ts"],
  "ignore": []
}
```

`.jscpd.json` :

```json
{
  "threshold": 3,
  "reporters": ["console"],
  "ignore": ["**/node_modules/**", "**/dist/**", "tests/harness/fixtures/**"],
  "absolute": true
}
```

- [ ] **Étape 4 : créer les fixtures**

`tests/harness/fixtures/export-orphelin.ts` :

```typescript
// Violation délibérée : cet export n'est importé nulle part.
export function personneNeMAppelle(): string {
  return 'orphelin'
}
```

`tests/harness/fixtures/point-entree.ts` :

```typescript
// Point d'entrée de la configuration d'épreuve : il n'importe pas l'orphelin.
export const entree = true
```

- [ ] **Étape 5 : ajouter les scripts et lancer l'épreuve**

```json
{
  "scripts": {
    "knip": "knip",
    "jscpd": "jscpd src"
  }
}
```

Le caractère non bloquant de jscpd n'est pas porté par le script : `|| exit 0` ne se comporte pas de la même façon selon le shell sous Windows. Il est porté par la CI, qui lance cette étape en `continue-on-error`. Ruling C6 du ledger.

Lancer : `npx vitest run tests/harness/code-mort.test.ts`
Attendu : 2 tests passent.

- [ ] **Étape 6 : consigner les faux positifs**

Lancer : `npm run knip` sur le projet principal. Noter le nombre de signalements écartés et la raison de chacun **dans le rapport de tâche** — `docs/decisions.md` existe déjà (créé hors plan, il porte D1 à D17) : le rapport de tâche reste le lieu du détail, et la tâche 20 y consigne ce qui doit survivre au lot. Ruling C2 du ledger. Un détecteur qui se trompe est un détecteur qu'on cesse de lire.

- [ ] **Étape 7 : commit**

```bash
git add -A
git commit -m "éprouve la détection de code mort"
```

---

---

## Tâche 10 : Playwright et axe-core

**Fichiers :**
- Créer : `playwright.config.ts`, `tests/e2e/accessibilite.spec.ts`, `tests/harness/fixtures/bouton-sans-nom.html`, `tests/harness/accessibilite.test.ts`
- Modifier : `package.json`

**Interfaces :**
- Consomme : `lancerOutil`, le build de la tâche 1
- Produit : `npm run e2e`.

- [ ] **Étape 1 : écrire l'épreuve**

`tests/harness/accessibilite.test.ts` :

```typescript
import { existsSync } from 'node:fs'
import { describe, expect, it } from 'vitest'
import { lancerOutil } from './run-outil'

const FIXTURE = 'tests/harness/fixtures/bouton-sans-nom.html'

describe('garde-fou : accessibilité', () => {
  it('la fixture de violation existe', () => {
    expect(existsSync(FIXTURE), `Cible manquante : ${FIXTURE}`).toBe(true)
  })

  it('axe-core détecte un bouton sans nom accessible', () => {
    const r = lancerOutil(['npx', 'playwright', 'test', 'tests/e2e/fixture-a11y.spec.ts'])
    expect(r.code, `axe-core n'a pas vu la violation :\n${r.sortie}`).not.toBe(0)
    expect(r.sortie).toMatch(/button-name|violation/i)
  })
})
```

- [ ] **Étape 2 : lancer l'épreuve pour la voir échouer**

Lancer : `npx vitest run tests/harness/accessibilite.test.ts`
Attendu : ÉCHEC.

- [ ] **Étape 3 : installer Playwright et axe-core**

```bash
npm install -D @playwright/test @axe-core/playwright
npx playwright install --with-deps chromium webkit
```

- [ ] **Étape 4 : écrire `playwright.config.ts`**

```typescript
import { defineConfig, devices } from '@playwright/test'

export default defineConfig({
  testDir: './tests/e2e',
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 1 : 0,
  reporter: process.env.CI ? 'github' : 'list',
  use: { baseURL: 'http://localhost:4173', trace: 'on-first-retry' },
  projects: [
    { name: 'chromium', use: { ...devices['Desktop Chrome'] } },
    { name: 'webkit-mobile', use: { ...devices['iPhone 13'] } },
  ],
  webServer: {
    command: 'npm run build && npm run preview',
    url: 'http://localhost:4173',
    reuseExistingServer: !process.env.CI,
    timeout: 120000,
  },
})
```

Le second projet est `iPhone 13` et non un Chrome de bureau : `02-design.md` § 1 pose que le téléphone gouverne le design.

- [ ] **Étape 5 : écrire les deux tests de bout en bout**

`tests/e2e/accessibilite.spec.ts` — le vrai test du projet :

```typescript
import AxeBuilder from '@axe-core/playwright'
import { expect, test } from '@playwright/test'

test("la page d'accueil n'a aucune violation critique", async ({ page }) => {
  await page.goto('/')
  const resultats = await new AxeBuilder({ page })
    .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'wcag22aa'])
    .analyze()
  const critiques = resultats.violations.filter((v) => v.impact === 'critical' || v.impact === 'serious')
  expect(critiques, JSON.stringify(critiques, null, 2)).toHaveLength(0)
})
```

`tests/e2e/fixture-a11y.spec.ts` — l'épreuve, qui doit échouer :

```typescript
import AxeBuilder from '@axe-core/playwright'
import { expect, test } from '@playwright/test'
import { pathToFileURL } from 'node:url'
import { resolve } from 'node:path'

test('la fixture accessible doit être refusée par axe-core', async ({ page }) => {
  const chemin = pathToFileURL(resolve('tests/harness/fixtures/bouton-sans-nom.html')).href
  await page.goto(chemin)
  const resultats = await new AxeBuilder({ page }).analyze()
  expect(resultats.violations).toHaveLength(0)
})
```

Ce test est **conçu pour échouer** : il affirme qu'il n'y a aucune violation sur une page qui en contient une. L'épreuve de la tâche vérifie précisément cet échec.

- [ ] **Étape 6 : créer la fixture**

`tests/harness/fixtures/bouton-sans-nom.html` :

```html
<!doctype html>
<html lang="fr">
  <head><meta charset="UTF-8" /><title>fixture</title></head>
  <body>
    <!-- Violation délibérée : bouton sans nom accessible. -->
    <button type="button"></button>
  </body>
</html>
```

- [ ] **Étape 7 : ajouter le script et lancer l'épreuve**

```json
{ "scripts": { "e2e": "playwright test tests/e2e/accessibilite.spec.ts" } }
```

Le script `e2e` ne lance **que** le vrai test ; la fixture n'est appelée que par l'épreuve du harnais.

Lancer : `npx vitest run tests/harness/accessibilite.test.ts`
Attendu : 2 tests passent.

- [ ] **Étape 8 : commit**

```bash
git add -A
git commit -m "éprouve la détection des violations d'accessibilité"
```

---

---

## Tâche 11 : Solution .NET et les quatre projets

**Fichiers :**
- Créer : `global.json`, `back/Palier.sln`, les quatre projets, `back/tests-harness/references.test.mjs`

**Interfaces :**
- Consomme : rien
- Produit : la solution `back/Palier.sln`, compilable par `dotnet build back/Palier.sln`. Le graphe de références que toutes les tâches suivantes respectent.

- [ ] **Étape 1 : écrire l'épreuve, avant les projets**

`back/tests-harness/references.test.mjs` :

```javascript
import { readFileSync, existsSync } from 'node:fs'
import { describe, expect, it } from 'vitest'

const CSPROJ = 'back/Palier.Domain/Palier.Domain.csproj'

describe('garde-fou : pureté du domaine', () => {
  it('le projet Domain existe', () => {
    expect(existsSync(CSPROJ), `Cible manquante : ${CSPROJ}`).toBe(true)
  })

  it('Palier.Domain ne référence aucun projet', () => {
    const x = readFileSync(CSPROJ, 'utf8')
    expect(x, 'Domain doit rester sans référence de projet').not.toMatch(/<ProjectReference/)
  })

  it("Palier.Domain ne référence aucun paquet d'accès aux données, réseau ou UI", () => {
    const x = readFileSync(CSPROJ, 'utf8')
    for (const interdit of [
      'EntityFrameworkCore',
      'Npgsql',
      'Microsoft.AspNetCore',
      'System.Net.Http',
      'Dapper',
    ]) {
      expect(x, `Domain ne doit pas référencer ${interdit}`).not.toContain(interdit)
    }
  })
})
```

- [ ] **Étape 2 : lancer l'épreuve pour la voir échouer**

```bash
npx --prefix front vitest run --root .. back/tests-harness/references.test.mjs
```

Attendu : ÉCHEC — le projet n'existe pas.

*Note d'exécution : si l'invocation depuis `front/` est malcommode, ajouter à la tâche 9 un script racine `test:harness` qui lance vitest sur `back/tests-harness/` avec la configuration front. Le chemin exact est un détail d'outillage, pas une exigence.*

- [ ] **Étape 3 : figer la version du SDK**

`global.json` à la racine :

```json
{
  "sdk": {
    "version": "10.0.303",
    "rollForward": "latestFeature"
  }
}
```

`latestFeature` : les correctifs de sécurité passent, un changement de version majeure non.

- [ ] **Étape 4 : créer la solution et les quatre projets**

```bash
cd back
dotnet new sln --name Palier
dotnet new classlib --name Palier.Domain --output Palier.Domain
dotnet new classlib --name Palier.Application --output Palier.Application
dotnet new classlib --name Palier.Infrastructure --output Palier.Infrastructure
dotnet new web --name Palier.Api --output Palier.Api
dotnet sln add Palier.Domain Palier.Application Palier.Infrastructure Palier.Api
```

Supprimer les fichiers `Class1.cs` générés par les templates `classlib` — ce sont des résidus, et le lot 1a a montré que les résidus de génération se retrouvent en production si personne ne les enlève.

- [ ] **Étape 5 : établir le graphe de références**

```bash
cd back
dotnet add Palier.Application reference Palier.Domain
dotnet add Palier.Infrastructure reference Palier.Domain Palier.Application
dotnet add Palier.Api reference Palier.Domain Palier.Application Palier.Infrastructure
```

Aucune commande ne donne de référence à `Palier.Domain`. C'est l'invariant du lot.

- [ ] **Étape 6 : lancer l'épreuve pour la voir passer**

```bash
dotnet build back/Palier.sln
```

Attendu : compilation réussie. Puis relancer l'épreuve : 3 tests passent.

- [ ] **Étape 7 : provoquer la violation, pour vérifier que la barrière tient**

```bash
cd back
dotnet add Palier.Domain reference Palier.Infrastructure
dotnet build Palier.sln
```

Attendu : **échec de compilation** pour référence circulaire. C'est la preuve que la barrière n'est pas décorative. Annuler immédiatement :

```bash
dotnet remove Palier.Domain reference Palier.Infrastructure
dotnet build Palier.sln   # doit repasser au vert
```

Consigner la sortie de l'échec dans le rapport de tâche.

- [ ] **Étape 8 : commit**

```bash
git add -A
git commit -m "crée la solution et les quatre projets du backend"
```

---

---

## Tâche 12 : Rigueur globale — `Directory.Build.props`

**Fichiers :**
- Créer : `Directory.Build.props` (racine), `.editorconfig` (racine), `back/tests-harness/fixtures/Nullable.cs`, `back/tests-harness/rigueur.test.mjs`

**Interfaces :**
- Consomme : la solution de la tâche 2
- Produit : la rigueur appliquée à tous les projets, présents et futurs.

- [ ] **Étape 1 : écrire l'épreuve**

`back/tests-harness/rigueur.test.mjs` :

```javascript
import { existsSync } from 'node:fs'
import { describe, expect, it } from 'vitest'
import { lancerOutil } from '../../front/tests/harness/run-outil.js'

const FIXTURE = 'back/tests-harness/fixtures/Nullable.cs'

describe('garde-fou : rigueur du compilateur', () => {
  it('la fixture de violation existe', () => {
    expect(existsSync(FIXTURE), `Cible manquante : ${FIXTURE}`).toBe(true)
  })

  it('refuse un déréférencement possiblement nul', () => {
    const r = lancerOutil(['dotnet', 'build', 'back/tests-harness/fixtures/Fixtures.csproj'])
    expect(r.code, `Le compilateur a accepté la violation :\n${r.sortie}`).not.toBe(0)
    expect(r.sortie).toMatch(/CS8600|CS8602|CS8604/)
  })
})
```

- [ ] **Étape 2 : lancer l'épreuve pour la voir échouer**

Attendu : ÉCHEC — ni le fichier de rigueur ni la fixture n'existent.

- [ ] **Étape 3 : écrire `Directory.Build.props`**

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <ImplicitUsings>enable</ImplicitUsings>

    <!-- L'équivalent de "strict" en TypeScript -->
    <Nullable>enable</Nullable>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <WarningsNotAsErrors></WarningsNotAsErrors>

    <!-- Analyseurs de code : l'équivalent côté .NET de ce qu'Oxlint fait au front -->
    <EnableNETAnalyzers>true</EnableNETAnalyzers>
    <AnalysisLevel>latest-all</AnalysisLevel>
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>

    <!-- Reproductibilité et traçabilité -->
    <Deterministic>true</Deterministic>
    <ContinuousIntegrationBuild Condition="'$(CI)' == 'true'">true</ContinuousIntegrationBuild>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <NoWarn>$(NoWarn);CS1591</NoWarn>
  </PropertyGroup>
</Project>
```

`CS1591` (commentaire XML manquant) est la seule exception : `GenerateDocumentationFile` est activé pour que les analyseurs voient les signatures publiques, pas pour imposer un commentaire sur chaque membre.

- [ ] **Étape 4 : écrire `.editorconfig`**

```ini
root = true

[*]
charset = utf-8
end_of_line = lf
insert_final_newline = true
indent_style = space
trim_trailing_whitespace = true

[*.cs]
indent_size = 4
csharp_new_line_before_open_brace = all
csharp_style_namespace_declarations = file_scoped:error
dotnet_style_require_accessibility_modifiers = always:error
dotnet_diagnostic.IDE0005.severity = error

# Nommage — conventions .NET
dotnet_naming_rule.types_pascal.symbols = types
dotnet_naming_rule.types_pascal.style = pascal
dotnet_naming_rule.types_pascal.severity = error
dotnet_naming_symbols.types.applicable_kinds = class,struct,enum,property,method
dotnet_naming_style.pascal.capitalization = pascal_case

dotnet_naming_rule.champs_prives.symbols = champs_prives
dotnet_naming_rule.champs_prives.style = underscore_camel
dotnet_naming_rule.champs_prives.severity = error
dotnet_naming_symbols.champs_prives.applicable_kinds = field
dotnet_naming_symbols.champs_prives.applicable_accessibilities = private
dotnet_naming_style.underscore_camel.required_prefix = _
dotnet_naming_style.underscore_camel.capitalization = camel_case

[*.{ts,tsx,js,jsx,json,md,yml,yaml}]
indent_size = 2
```

- [ ] **Étape 5 : créer la fixture et son projet isolé**

`back/tests-harness/fixtures/Fixtures.csproj` :

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>
</Project>
```

Ce projet n'est **pas** ajouté à `Palier.sln` : il ne doit jamais être compilé par le build normal.

`back/tests-harness/fixtures/Nullable.cs` :

```csharp
namespace Fixtures;

// Violation délibérée : déréférencement d'une référence possiblement nulle.
// Ce projet est hors de Palier.sln et n'est compilé que par son épreuve.
public static class Nullable
{
    public static int Longueur(string? valeur)
    {
        return valeur.Length;
    }
}
```

- [ ] **Étape 6 : lancer l'épreuve pour la voir passer**

Attendu : 2 tests passent — le compilateur refuse `valeur.Length` sur un `string?`.

- [ ] **Étape 7 : vérifier que la solution reste compilable**

```bash
dotnet build back/Palier.sln
```

Attendu : compilation réussie, **zéro avertissement**. Si les analyseurs en remontent sur le code généré par les templates, les corriger — ne jamais les faire taire globalement.

- [ ] **Étape 8 : commit**

```bash
git add -A
git commit -m "impose la rigueur du compilateur et les conventions de style"
```

---

---

## Tâche 13 : xUnit et la première fonction du domaine

**Fichiers :**
- Créer : `back/Palier.Domain.Tests/`, `back/Palier.Domain/Energie/MetabolismeDeBase.cs`, son test
- Modifier : `back/Palier.sln`

**Interfaces :**
- Consomme : la solution, la rigueur
- Produit : `MetabolismeDeBase.MifflinStJeor(Sexe sexe, decimal poidsKg, decimal tailleCm, int age) → decimal`, consommée par le lot 3.

> Cette tâche installe l'outillage de test **et** la première fonction réelle, parce qu'un harnais de test sans rien à tester ne prouve rien.

- [ ] **Étape 1 : créer le projet de tests**

```bash
cd back
dotnet new xunit --name Palier.Domain.Tests --output Palier.Domain.Tests
dotnet sln add Palier.Domain.Tests
dotnet add Palier.Domain.Tests reference Palier.Domain
```

Supprimer le fichier de test généré par le template.

- [ ] **Étape 2 : écrire le test AVANT l'implémentation**

`back/Palier.Domain.Tests/Energie/MetabolismeDeBaseTests.cs` :

```csharp
using Palier.Domain.Energie;

namespace Palier.Domain.Tests.Energie;

public sealed class MetabolismeDeBaseTests
{
    // docs/04-nutrition.md § 1 :
    // Homme : 10 × poids + 6,25 × taille − 5 × âge + 5
    // Femme : 10 × poids + 6,25 × taille − 5 × âge − 161
    [Fact]
    public void MifflinStJeor_calcule_la_valeur_pour_un_homme()
    {
        var r = MetabolismeDeBase.MifflinStJeor(Sexe.Homme, poidsKg: 73m, tailleCm: 178m, age: 35m);
        // 730 + 1112,5 − 175 + 5 = 1672,5
        Assert.Equal(1672.5m, r);
    }

    [Fact]
    public void MifflinStJeor_calcule_la_valeur_pour_une_femme()
    {
        var r = MetabolismeDeBase.MifflinStJeor(Sexe.Femme, poidsKg: 60m, tailleCm: 165m, age: 30m);
        // 600 + 1031,25 − 150 − 161 = 1320,25
        Assert.Equal(1320.25m, r);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void MifflinStJeor_refuse_un_poids_non_positif(int poids)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => MetabolismeDeBase.MifflinStJeor(Sexe.Homme, poids, 178m, 35m));
    }
}
```

`decimal` et non `double` : `16-projet.md` § 2 interdit le flottant pour les quantités nutritionnelles, et la raison vaut aussi pour les calculs intermédiaires.

- [ ] **Étape 3 : lancer le test pour le voir échouer**

```bash
dotnet test back/Palier.Domain.Tests
```

Attendu : ÉCHEC de compilation — `MetabolismeDeBase` et `Sexe` n'existent pas.

- [ ] **Étape 4 : écrire l'implémentation minimale**

`back/Palier.Domain/Energie/Sexe.cs` :

```csharp
namespace Palier.Domain.Energie;

public enum Sexe
{
    Homme,
    Femme,
}
```

`back/Palier.Domain/Energie/MetabolismeDeBase.cs` :

```csharp
namespace Palier.Domain.Energie;

/// <summary>
/// Métabolisme de base. Voir docs/04-nutrition.md § 1.
/// Module pur : aucun accès réseau, base ou interface.
/// </summary>
public static class MetabolismeDeBase
{
    public static decimal MifflinStJeor(Sexe sexe, decimal poidsKg, decimal tailleCm, decimal age)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(poidsKg);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(tailleCm);
        ArgumentOutOfRangeException.ThrowIfNegative(age);

        var socle = (10m * poidsKg) + (6.25m * tailleCm) - (5m * age);
        return sexe switch
        {
            Sexe.Homme => socle + 5m,
            Sexe.Femme => socle - 161m,
            _ => throw new ArgumentOutOfRangeException(nameof(sexe)),
        };
    }
}
```

- [ ] **Étape 5 : lancer le test pour le voir passer**

```bash
dotnet test back/Palier.Domain.Tests
```

Attendu : 4 tests passent (2 faits + 2 cas de théorie).

- [ ] **Étape 6 : éprouver que le harnais de test mord**

Ajouter temporairement un test qui échoue, vérifier que `dotnet test` sort en code non nul, puis le retirer. Consigner la sortie dans le rapport.

- [ ] **Étape 7 : commit**

```bash
git add -A
git commit -m "installe xUnit et le calcul du métabolisme de base"
```

---

---

## Tâche 14 : Couverture à 100 % sur le domaine

**Fichiers :**
- Modifier : `back/Palier.Domain.Tests/Palier.Domain.Tests.csproj`
- Créer : `back/coverage.runsettings`, `back/tests-harness/couverture.test.mjs`

**Interfaces :**
- Consomme : le projet de tests de la tâche 4
- Produit : `dotnet test --settings back/coverage.runsettings`, en échec sous 100 % sur `Palier.Domain`.

- [ ] **Étape 1 : écrire l'épreuve**

`back/tests-harness/couverture.test.mjs` :

```javascript
import { existsSync, writeFileSync, rmSync } from 'node:fs'
import { describe, expect, it, afterAll } from 'vitest'
import { lancerOutil } from '../../front/tests/harness/run-outil.js'

const NON_TESTE = 'back/Palier.Domain/Energie/NonTeste.cs'

afterAll(() => rmSync(NON_TESTE, { force: true }))

describe('garde-fou : couverture du domaine', () => {
  it('le fichier de configuration existe', () => {
    expect(existsSync('back/coverage.runsettings'), 'Cible manquante').toBe(true)
  })

  it('refuse une fonction du domaine non couverte', () => {
    writeFileSync(
      NON_TESTE,
      `namespace Palier.Domain.Energie;\n\n` +
        `public static class NonTeste\n{\n` +
        `    public static decimal Double(decimal x) => x * 2m;\n}\n`,
    )
    const r = lancerOutil([
      'dotnet', 'test', 'back/Palier.Domain.Tests',
      '--settings', 'back/coverage.runsettings',
    ])
    expect(r.code, `Le seuil de couverture n'a pas mordu :\n${r.sortie}`).not.toBe(0)
    // Seconde assertion obligatoire : un code non nul prouve seulement que
    // quelque chose a échoué, pas que l'outil a refusé. Sans elle, l'épreuve
    // passe au vert quand l'outil est absent, indisponible ou mal appelé.
    expect(r.sortie).toMatch(/threshold|seuil|coverage/i)
  })
})
```

- [ ] **Étape 2 : lancer l'épreuve pour la voir échouer**

Attendu : ÉCHEC — aucun seuil n'est configuré, `dotnet test` passe malgré la fonction non couverte.

- [ ] **Étape 3 : ajouter coverlet au projet de tests**

```bash
cd back
dotnet add Palier.Domain.Tests package coverlet.collector
dotnet add Palier.Domain.Tests package coverlet.msbuild
```

- [ ] **Étape 4 : écrire `back/coverage.runsettings`**

```xml
<?xml version="1.0" encoding="utf-8"?>
<RunSettings>
  <DataCollectionRunSettings>
    <DataCollectors>
      <DataCollector friendlyName="XPlat code coverage">
        <Configuration>
          <Format>cobertura</Format>
          <Include>[Palier.Domain]*</Include>
          <Threshold>100</Threshold>
          <ThresholdType>line,branch,method</ThresholdType>
          <ThresholdStat>total</ThresholdStat>
        </Configuration>
      </DataCollector>
    </DataCollectors>
  </DataCollectionRunSettings>
</RunSettings>
```

Le seuil ne porte que sur `Palier.Domain` : `08-workflow.md` § 6 exige 100 % sur les modules de calcul, et **aucun seuil global** — un chiffre global pousse à tester ce qui est facile.

- [ ] **Étape 5 : lancer l'épreuve pour la voir passer**

Attendu : 2 tests passent — le seuil refuse la fonction non couverte, et la fixture est nettoyée après.

- [ ] **Étape 6 : vérifier que le domaine réel passe le seuil**

```bash
dotnet test back/Palier.Domain.Tests --settings back/coverage.runsettings
```

Attendu : succès, 100 % sur `Palier.Domain`.

- [ ] **Étape 7 : commit**

```bash
git add -A
git commit -m "impose une couverture totale sur le domaine"
```

---

---

## Tâche 15 : Format et style

**Fichiers :**
- Créer : `back/tests-harness/fixtures/MalFormate.cs.txt`, `back/tests-harness/format.test.mjs`

**Interfaces :**
- Consomme : `.editorconfig` de la tâche 3
- Produit : `dotnet format --verify-no-changes`, appelé par `verify`.

- [ ] **Étape 1 : écrire l'épreuve**

`back/tests-harness/format.test.mjs` :

```javascript
import { copyFileSync, rmSync, existsSync } from 'node:fs'
import { describe, expect, it, afterAll } from 'vitest'
import { lancerOutil } from '../../front/tests/harness/run-outil.js'

const SOURCE = 'back/tests-harness/fixtures/MalFormate.cs.txt'
const CIBLE = 'back/Palier.Domain/MalFormate.cs'

afterAll(() => rmSync(CIBLE, { force: true }))

describe('garde-fou : format du code', () => {
  it('la fixture existe', () => {
    expect(existsSync(SOURCE), `Cible manquante : ${SOURCE}`).toBe(true)
  })

  it('refuse un fichier mal formaté', () => {
    copyFileSync(SOURCE, CIBLE)
    const r = lancerOutil(['dotnet', 'format', 'back/Palier.sln', '--verify-no-changes'])
    expect(r.code, `dotnet format a accepté le fichier :\n${r.sortie}`).not.toBe(0)
    // Seconde assertion obligatoire : un code non nul prouve seulement que
    // quelque chose a échoué, pas que l'outil a refusé. Sans elle, l'épreuve
    // passe au vert quand l'outil est absent, indisponible ou mal appelé.
    expect(r.sortie).toMatch(/WHITESPACE|IDE\d{4}|formatted incorrectly/)
  })
})
```

La fixture porte l'extension `.cs.txt` : elle ne doit pas être compilée tant qu'elle n'est pas copiée par l'épreuve.

- [ ] **Étape 2 : lancer l'épreuve pour la voir échouer**

Attendu : ÉCHEC — la fixture n'existe pas.

- [ ] **Étape 3 : créer la fixture**

`back/tests-harness/fixtures/MalFormate.cs.txt` :

```
namespace Palier.Domain;
    public   static class MalFormate {
public static int Valeur( ) {return    1;}
        }
```

- [ ] **Étape 4 : lancer l'épreuve pour la voir passer**

Attendu : 2 tests passent.

- [ ] **Étape 5 : vérifier que le code réel est bien formaté**

```bash
dotnet format back/Palier.sln --verify-no-changes
```

Attendu : succès. Si des écarts apparaissent, lancer `dotnet format back/Palier.sln` puis recommencer.

- [ ] **Étape 6 : commit**

```bash
git add -A
git commit -m "éprouve le contrôle de format du backend"
```

---

---

## Tâche 16 : Contrôle des licences, npm et NuGet

**Fichiers :**
- Créer : `scripts/verifier-licences.mjs`, `back/tests-harness/licences.test.mjs`

**Interfaces :**
- Consomme : `front/package.json`, les `.csproj` du backend
- Produit : `node scripts/verifier-licences.mjs`, en échec si une dépendance sort de la liste blanche. Décision D13.

> **Pourquoi un script maison.** Dépendre d'un outil tiers dont il faudrait d'abord vérifier la licence, pour vérifier des licences, est circulaire. Ce script n'a aucune dépendance et couvre les deux écosystèmes.

- [ ] **Étape 1 : écrire l'épreuve**

`back/tests-harness/licences.test.mjs` :

```javascript
import { describe, expect, it } from 'vitest'
import { lancerOutil } from '../../front/tests/harness/run-outil.js'

describe('garde-fou : licences des dépendances', () => {
  it('accepte les dépendances actuelles', () => {
    const r = lancerOutil(['node', 'scripts/verifier-licences.mjs'])
    expect(r.code, `Une dépendance actuelle sort de la liste blanche :\n${r.sortie}`).toBe(0)
  })

  it('refuse un paquet sous licence réciproque', () => {
    const r = lancerOutil(['node', 'scripts/verifier-licences.mjs', '--tester', 'MediatR'])
    expect(r.code, `MediatR (RPL-1.5) a été accepté :\n${r.sortie}`).not.toBe(0)
    expect(r.sortie).toMatch(/MediatR/)
  })
})
```

Le second test utilise MediatR comme cas d'école : c'est le paquet qui a motivé cette décision, et sa licence est un fait vérifiable plutôt qu'une fixture inventée.

- [ ] **Étape 2 : lancer l'épreuve pour la voir échouer**

Attendu : ÉCHEC — le script n'existe pas.

- [ ] **Étape 3 : écrire `scripts/verifier-licences.mjs`**

```javascript
#!/usr/bin/env node
// Vérifie que chaque dépendance porte une licence permissive.
// Aucune dépendance : interroge directement les registres npm et NuGet.
// Décision D13 du journal — motivée par le passage de MediatR sous RPL-1.5.
import { readFileSync, readdirSync, statSync } from 'node:fs'
import { join } from 'node:path'

const PERMISES = [
  'MIT', 'Apache-2.0', 'BSD-2-Clause', 'BSD-3-Clause', 'ISC',
  'PostgreSQL', '0BSD', 'Unlicense', 'CC0-1.0', 'MIT-0',
]

const estPermise = (l) =>
  !!l && PERMISES.some((p) => l.toUpperCase().includes(p.toUpperCase()))

async function licenceNpm(nom) {
  const r = await fetch(`https://registry.npmjs.org/${encodeURIComponent(nom)}/latest`)
  if (!r.ok) return null
  const j = await r.json()
  return typeof j.license === 'string' ? j.license : j.license?.type ?? null
}

async function licenceNuget(nom) {
  const r = await fetch(
    `https://azuresearch-usnc.nuget.org/query?q=packageid:${encodeURIComponent(nom)}&prerelease=false`,
  )
  if (!r.ok) return null
  const j = await r.json()
  const d = (j.data ?? [])[0]
  if (!d) return null
  // Une expression SPDX absente signale une licence non standard : à examiner.
  return d.licenseExpression ?? null
}

function paquetsNpm() {
  const p = JSON.parse(readFileSync('front/package.json', 'utf8'))
  return Object.keys({ ...p.dependencies, ...p.devDependencies })
}

function paquetsNuget() {
  const noms = new Set()
  const parcourir = (d) => {
    for (const e of readdirSync(d)) {
      const chemin = join(d, e)
      if (statSync(chemin).isDirectory()) {
        if (e === 'bin' || e === 'obj' || e === 'node_modules') continue
        parcourir(chemin)
      } else if (e.endsWith('.csproj')) {
        const x = readFileSync(chemin, 'utf8')
        for (const m of x.matchAll(/PackageReference\s+Include="([^"]+)"/g)) noms.add(m[1])
      }
    }
  }
  parcourir('back')
  return [...noms]
}

const aTester = process.argv.indexOf('--tester')
const cibles =
  aTester !== -1
    ? [{ nom: process.argv[aTester + 1], source: 'nuget' }]
    : [
        ...paquetsNpm().map((nom) => ({ nom, source: 'npm' })),
        ...paquetsNuget().map((nom) => ({ nom, source: 'nuget' })),
      ]

const refuses = []
for (const { nom, source } of cibles) {
  const licence = source === 'npm' ? await licenceNpm(nom) : await licenceNuget(nom)
  if (!estPermise(licence)) {
    refuses.push(`${source.padEnd(6)} ${nom.padEnd(50)} ${licence ?? '(licence non standard)'}`)
  }
}

if (refuses.length > 0) {
  console.error('Dépendances hors liste blanche :\n')
  for (const l of refuses) console.error('  ' + l)
  console.error(
    `\nListe blanche : ${PERMISES.join(', ')}.\n` +
      "Une licence non standard n'est pas forcément interdite — elle doit être lue avant d'être admise.",
  )
  process.exit(1)
}

console.log(`${cibles.length} dépendances vérifiées, toutes sous licence permissive.`)
```

- [ ] **Étape 4 : lancer l'épreuve pour la voir passer**

Attendu : 2 tests passent — les dépendances actuelles sont acceptées, MediatR est refusé.

> Note : une licence renvoyée comme non standard n'est pas nécessairement interdite. Certains paquets Microsoft déclarent une URL au lieu d'une expression SPDX. Ceux-là doivent être lus une fois, puis inscrits dans une liste d'exceptions **avec le motif** — jamais ajoutés silencieusement à la liste blanche.

- [ ] **Étape 5 : commit**

```bash
git add -A
git commit -m "vérifie les licences des dépendances des deux écosystèmes"
```

---

---

## Tâche 17 : Vulnérabilités des dépendances

**Fichiers :**
- Créer : `back/tests-harness/vulnerabilites.test.mjs`
- Modifier : `package.json` racine

**Interfaces :**
- Consomme : la solution
- Produit : `npm run audit:back`, appelé par `verify`.

- [ ] **Étape 1 : écrire l'épreuve**

`back/tests-harness/vulnerabilites.test.mjs` :

```javascript
import { describe, expect, it } from 'vitest'
import { lancerOutil } from '../../front/tests/harness/run-outil.js'

describe('garde-fou : vulnérabilités des dépendances', () => {
  it('aucune vulnérabilité connue dans les paquets du backend', () => {
    const r = lancerOutil([
      'dotnet', 'list', 'back/Palier.sln', 'package', '--vulnerable', '--include-transitive',
    ])
    expect(r.code, `La commande a échoué :\n${r.sortie}`).toBe(0)
    expect(r.sortie, `Vulnérabilité détectée :\n${r.sortie}`).not.toMatch(/High|Critical/i)
  })
})
```

`dotnet list package --vulnerable` sort en code 0 même quand il trouve quelque chose : c'est la sortie qu'il faut examiner, pas le code de retour. C'est exactement le genre de contrôle qui approuve en silence si on ne le vérifie pas.

- [ ] **Étape 2 : lancer l'épreuve**

Attendu : succès sur les dépendances actuelles.

- [ ] **Étape 3 : ajouter le script**

```json
{ "scripts": { "audit:back": "dotnet list back/Palier.sln package --vulnerable --include-transitive" } }
```

- [ ] **Étape 4 : commit**

```bash
git add -A
git commit -m "surveille les vulnérabilités des paquets du backend"
```

---

---

## Tâche 18 : la commande unique `verify`, front et backend

**Fichiers :**
- Créer : `scripts/verify.mjs`
- Modifier : `package.json` racine
- Créer : `tests-harness/verify.test.mjs`

**Interfaces :**
- Consomme : tous les scripts des tâches 3 à 17
- Produit : `npm run verify` — **la seule commande** appelée par le hook de pré-envoi et par la CI.

> Cette tâche remplace deux définitions concurrentes qui existaient dans les plans séparés. Le point d'entrée unique ne peut pas être défini deux fois : c'est précisément le défaut qu'il existe pour empêcher.

- [ ] **Étape 1 : écrire l'épreuve**

`tests-harness/verify.test.mjs` :

```javascript
import { existsSync, readFileSync } from 'node:fs'
import { describe, expect, it } from 'vitest'

describe("garde-fou : point d'entrée unique", () => {
  it('le script verify existe', () => {
    expect(existsSync('scripts/verify.mjs'), 'Cible manquante : scripts/verify.mjs').toBe(true)
  })

  it('verify couvre les deux écosystèmes', () => {
    const s = readFileSync('scripts/verify.mjs', 'utf8')
    for (const etape of [
      'front:format', 'front:lint', 'front:typecheck', 'front:test', 'front:knip',
      'regles', 'back:format', 'back:build', 'back:test', 'licences', 'front:build',
    ]) {
      expect(s, `Contrôle manquant dans verify : ${etape}`).toContain(etape)
    }
  })

  it("le hook de pré-envoi n'appelle que verify", () => {
    const h = readFileSync('.husky/pre-push', 'utf8')
    expect(h).toContain('npm run verify')
    expect(h, "le hook délègue, il n'énumère pas").not.toMatch(/dotnet (build|test)|npm run (lint|typecheck)/)
  })
})
```

Le troisième test est ce qui empêche la dérive : le jour où quelqu'un ajoute un contrôle au hook sans le mettre dans `verify`, le local et la CI cessent de vérifier la même chose.

- [ ] **Étape 1 bis : l'épreuve des commandes, et non des outils**

`front/tests/harness/commandes.test.ts` :

```typescript
// @vitest-environment node
import { describe, expect, it } from 'vitest'
import { lancerOutil } from './run-outil'

// Aucune autre épreuve de cette suite ne lance un script npm : toutes appellent
// `npx <outil>` directement. Elles prouvent donc que les OUTILS refusent, jamais
// que les COMMANDES du projet refusent — or ce sont les scripts que le hook de
// pré-commit, `verify` et la CI exécutent réellement.
//
// Ce trou a laissé passer deux régressions, trouvées seulement en revue :
// un script `lint` réécrit sans `--ignore-pattern` (les fixtures faisaient
// rougir le lint du code sain), et un `test:harness` dont le drapeau
// `--exclude ''` ne réactivait pas ce qu'il prétendait réactiver.
describe('garde-fou : les commandes du projet, pas seulement les outils', () => {
  it('npm run lint est vert sur le code réel', () => {
    const r = lancerOutil(['npm', '--prefix', 'front', 'run', 'lint'], { cwd: '..' })
    expect(r.code, `Le script lint échoue sur le code sain :\n${r.sortie}`).toBe(0)
  })

  it('npm run typecheck est vert sur le code réel', () => {
    const r = lancerOutil(['npm', '--prefix', 'front', 'run', 'typecheck'], { cwd: '..' })
    expect(r.code, `Le script typecheck échoue sur le code sain :\n${r.sortie}`).toBe(0)
  })

  it('les fixtures restent visibles de leurs propres épreuves', () => {
    // Le pendant du test précédent. Si l'exclusion migrait de la ligne de
    // commande vers `ignorePatterns`, le lint resterait vert ET les fixtures
    // deviendraient invisibles : les épreuves passeraient au vert en ne
    // contrôlant rien. Ce test le rend impossible.
    const r = lancerOutil(['npx', 'oxlint', 'tests/harness/fixtures/any-explicite.ts'])
    expect(r.code, `La fixture est devenue invisible d'Oxlint :\n${r.sortie}`).not.toBe(0)
    expect(r.sortie).toContain('no-explicit-any')
  })

  it('test:harness voit toutes les épreuves, y compris celle d’accessibilité', () => {
    const r = lancerOutil(['npm', '--prefix', 'front', 'run', 'test:harness', '--', '--list'], {
      cwd: '..',
    })
    expect(r.code, `test:harness ne démarre pas :\n${r.sortie}`).toBe(0)
    expect(r.sortie, "l'épreuve d'accessibilité est exclue de test:harness").toContain(
      'accessibilite',
    )
  })
})
```

> **Ce fichier vit dans `front/tests/harness/` et lance des scripts npm.** Il n'y a
> pas de récursion : `lint` et `typecheck` ne relancent pas les tests. Ne jamais
> y appeler `npm run test` — la suite s'appellerait elle-même sans fin.

- [ ] **Étape 2 : lancer l'épreuve pour la voir échouer**

Attendu : ÉCHEC — `scripts/verify.mjs` n'existe pas.

- [ ] **Étape 3 : écrire `scripts/verify.mjs`**

```javascript
#!/usr/bin/env node
import { spawnSync } from 'node:child_process'

const SEUIL_MS = 90000 // deux harnais : seuil doublé par rapport au front seul

const etapes = [
  ['front:format', ['npm', '--prefix', 'front', 'run', 'format:check']],
  ['front:lint', ['npm', '--prefix', 'front', 'run', 'lint']],
  ['front:typecheck', ['npm', '--prefix', 'front', 'run', 'typecheck']],
  ['front:test', ['npm', '--prefix', 'front', 'run', 'test']],
  ['front:knip', ['npm', '--prefix', 'front', 'run', 'knip']],
  ['regles', ['node', 'scripts/regles-projet.mjs']],
  ['back:format', ['dotnet', 'format', 'back/Palier.sln', '--verify-no-changes']],
  ['back:build', ['dotnet', 'build', 'back/Palier.sln', '--no-incremental']],
  ['back:test', ['dotnet', 'test', 'back/Palier.sln', '--settings', 'back/coverage.runsettings']],
  ['licences', ['node', 'scripts/verifier-licences.mjs']],
  ['front:build', ['npm', '--prefix', 'front', 'run', 'build']],
]

const debut = Date.now()
const durees = []
let echec = null

for (const [nom, commande] of etapes) {
  const t0 = Date.now()
  const [bin, ...args] = commande
  const r = spawnSync(bin, args, { stdio: 'inherit', shell: process.platform === 'win32' })
  durees.push([nom, Date.now() - t0])
  if (r.status !== 0) {
    echec = nom
    break
  }
}

const total = Date.now() - debut
console.log('\n─── verify ───')
for (const [nom, ms] of durees) console.log(`  ${nom.padEnd(18)} ${(ms / 1000).toFixed(1)} s`)
console.log(`  ${'TOTAL'.padEnd(18)} ${(total / 1000).toFixed(1)} s`)

if (echec) {
  console.error(`\nÉCHEC sur : ${echec}`)
  process.exit(1)
}

if (total > SEUIL_MS) {
  console.warn(
    `\nAVERTISSEMENT : verify a pris ${(total / 1000).toFixed(1)} s, au-delà de ${SEUIL_MS / 1000} s.\n` +
      'Une boucle de rétroaction lente est un défaut à traiter — docs/08-workflow.md § 5.',
  )
}
```

Les épreuves du harnais ne figurent **pas** dans `verify` : elles font échouer des outils délibérément, et les mêler aux contrôles normaux rendrait la sortie illisible. Elles ont leur propre commande, appelée par la CI.

- [ ] **Étape 4 : ajouter les scripts racine**

```json
{
  "scripts": {
    "verify": "node scripts/verify.mjs",
    "test:harness": "npm --prefix front run test:harness && npm run test:harness:back",
    "test:harness:back": "node front/node_modules/vitest/vitest.mjs run --config back/vitest.config.mjs",
    "front": "npm --prefix front run"
  }
}
```

> **Corrigé après la tâche 17.** La version précédente lançait `vitest run tests-harness back/tests-harness`
> depuis la racine : ni Vitest ni configuration racine n'existent là. L'implémenteur du backend a dû
> créer `back/vitest.config.mjs` pour pouvoir lancer ses épreuves — sans quoi aucune d'elles n'était
> exécutable. Ce fichier n'était pas au plan ; il y entre ici.
>
> Trois choses à ne pas défaire dans cette configuration, chacune motivée par une mesure :
> - `root` est la racine du dépôt, pas `back/` : les épreuves adressent `back/Palier.sln` et
>   `scripts/verifier-licences.mjs` exactement comme les commandes que `verify` et la CI lancent.
> - l'objet est exporté brut, sans `defineConfig` : `vitest` n'est installé que dans
>   `front/node_modules`, hors de la chaîne de résolution d'un fichier de `back/`.
> - `fileParallelism: false` : trois épreuves déposent un fichier de violation dans
>   `back/Palier.Domain/` puis lancent MSBuild. En parallèle, la violation de l'une fait rougir
>   l'autre pour la mauvaise raison — et un jour verdir pour la mauvaise raison.
>
> `tests-harness/verify.test.mjs` vit à la racine et sera donc ramassé par la configuration
> backend : ajouter `'tests-harness/**/*.test.mjs'` à son `include`.

- [ ] **Étape 5 : lancer l'épreuve pour la voir passer**

Attendu : 3 tests passent.

- [ ] **Étape 6 : lancer `verify` et mesurer**

Lancer : `npm run verify`
Attendu : les dix étapes passent, la durée s'affiche. **Noter cette durée dans le rapport de lot** — c'est la mesure de référence de la boucle de rétroaction, et le seul chiffre qui dira si le harnais reste utilisable.

- [ ] **Étape 7 : commit**

```bash
git add -A
git commit -m "unifie la commande de vérification sur les deux écosystèmes"
```

---

## Tâche 19 : hooks Git et détection de secrets

**Fichiers :**
- Créer : `.husky/pre-commit`, `.husky/pre-push`, `.gitleaks.toml`, `tests/harness/fixtures/faux-secret.txt`, `tests/harness/secrets.test.ts`
- Modifier : `package.json`

**Interfaces :**
- Consomme : `npm run verify` de la tâche 10
- Produit : le refus au commit et au push.

- [ ] **Étape 1 : écrire l'épreuve**

`tests/harness/secrets.test.ts` :

```typescript
import { existsSync, readFileSync } from 'node:fs'
import { describe, expect, it } from 'vitest'
import { lancerOutil } from './run-outil'

const FIXTURE = 'tests/harness/fixtures/faux-secret.txt'

describe('garde-fou : secrets', () => {
  it('la fixture de violation existe', () => {
    expect(existsSync(FIXTURE), `Cible manquante : ${FIXTURE}`).toBe(true)
  })

  it('gitleaks détecte une clé au format reconnu', () => {
    const r = lancerOutil(['npx', 'gitleaks', 'detect', '--no-git', '--source', 'tests/harness/fixtures', '--redact'])
    expect(r.code, `gitleaks n'a rien vu :\n${r.sortie}`).not.toBe(0)
    // Seconde assertion obligatoire : un code non nul prouve seulement que
    // quelque chose a échoué, pas que l'outil a refusé. Sans elle, l'épreuve
    // passe au vert quand l'outil est absent, indisponible ou mal appelé.
    // Ce cas est le plus exposé : le plan prévient lui-même que `npx gitleaks`
    // n'expose pas forcément un binaire sur toutes les plateformes.
    expect(r.sortie).toMatch(/secret|leak/i)
  })

  it('les deux hooks existent', () => {
    expect(existsSync('.husky/pre-commit')).toBe(true)
    expect(existsSync('.husky/pre-push')).toBe(true)
  })

  it("le hook pre-push n'appelle que verify", () => {
    const hook = readFileSync('.husky/pre-push', 'utf8')
    expect(hook).toContain('npm run verify')
    // Le hook délègue, il n'énumère pas : sinon il diverge de la CI.
    expect(hook).not.toMatch(/npm run (lint|typecheck|build|knip)/)
  })
})
```

- [ ] **Étape 2 : lancer l'épreuve pour la voir échouer**

Lancer : `npx vitest run tests/harness/secrets.test.ts`
Attendu : ÉCHEC.

- [ ] **Étape 3 : installer Husky, lint-staged et gitleaks**

```bash
npm install -D husky lint-staged gitleaks
npx husky init
```

- [ ] **Étape 4 : écrire les hooks**

`.husky/pre-commit` :

```bash
npx lint-staged
npx gitleaks protect --staged --redact --config .gitleaks.toml
```

`.husky/pre-push` :

```bash
npm run verify
```

- [ ] **Étape 5 : configurer lint-staged et gitleaks**

Dans `package.json` :

```json
{
  "lint-staged": {
    "*.{ts,tsx}": ["oxlint --fix", "prettier --write"],
    "*.{json,md,css,html}": ["prettier --write"]
  }
}
```

`.gitleaks.toml` :

```toml
title = "palier"

[extend]
useDefault = true

[[rules]]
id = "cle-anthropic"
description = "Clé API Anthropic"
regex = '''sk-ant-[A-Za-z0-9_\-]{20,}'''

[[rules]]
id = "chaine-connexion-postgres"
description = "Chaîne de connexion PostgreSQL avec mot de passe"
regex = '''(?i)(Host|Server)=[^;]+;.*Password=[^;\s"']+'''

[[rules]]
id = "jeton-jwt"
description = "Jeton JWT en clair"
regex = '''eyJ[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}'''

[[rules]]
id = "cle-application-ovh"
description = "Clé applicative OVHcloud"
regex = '''(?i)ovh_(application|consumer)_(key|secret)\s*[=:]\s*[A-Za-z0-9]{16,}'''
```

- [ ] **Étape 6 : créer la fixture**

`tests/harness/fixtures/faux-secret.txt` :

```
# Faux secret, format valide, valeur inventée. Sert à prouver que gitleaks mord.
# Les motifs surveillés suivent l'architecture : clé de modèle, chaîne de
# connexion PostgreSQL, jeton JWT, clé applicative OVHcloud.
ANTHROPIC_API_KEY=sk-ant-api03-CECI-EST-UN-FAUX-SECRET-DE-TEST-0000000000
```

- [ ] **Étape 7 : lancer l'épreuve et vérifier le refus réel au commit**

Lancer : `npx vitest run tests/harness/secrets.test.ts`
Attendu : 3 tests passent.

Puis provoquer un refus réel :

```bash
echo "export const x: any = 1" > src/violation-temporaire.ts
git add src/violation-temporaire.ts
git commit -m "essai de refus"
```

Attendu : le commit est **refusé** par le hook. Nettoyer ensuite :

```bash
git reset HEAD src/violation-temporaire.ts && rm src/violation-temporaire.ts
```

- [ ] **Étape 8 : commit**

```bash
git add -A
git commit -m "installe les hooks de commit et de push"
```

---

## Tâche 20 : Documents de travail

**Fichiers :**
- Créer : `docs/gabarit-rapport-lot.md`, `docs/securite/asvs-l2.md`
- Modifier : `docs/decisions.md`

**Interfaces :**
- Consomme : les décisions prises pendant ce lot
- Produit : les documents lus au démarrage de chaque session.

> `docs/decisions.md` a été créé hors plan, au moment du changement d'architecture. Cette tâche ne le recrée pas : elle y **ajoute** les décisions prises pendant l'exécution du lot.

- [ ] **Étape 1 : écrire `docs/gabarit-rapport-lot.md`**

```markdown
# Gabarit — rapport de fin de lot

## Ce qui a changé
Fichiers, compteurs, avant/après.

## Ce qui a cassé
Y compris ce qui a été cassé puis rattrapé.

## Ce que je signale sans y avoir touché
Trouvé en chemin, hors périmètre.

## Le franchissement
La preuve que le garde-fou refuse, pas sa relecture. Pour chaque épreuve :
ce qui a été provoqué, ce qui était attendu, ce qui s'est produit.

## Ce qui n'a pas pu être vérifié
Nommément. Un rapport sans incertitude est un rapport incomplet.

## Mesures
Durée de `npm run verify`. Nombre de tests. Couverture du domaine.
Faux positifs écartés, avec leur motif.

## Skills et agents invoqués
Et pour ceux qui ne l'ont pas été, pourquoi.
```

- [ ] **Étape 2 : écrire `docs/securite/asvs-l2.md`**

En-tête portant la version exacte de l'ASVS retenue, relevée à la rédaction — les numérotations OWASP ont changé récemment, aucun identifiant ne se cite de mémoire.

Puis un tableau : identifiant · intitulé · état (`couverte` / `non applicable` / `prévue lot N`) · mécanisme ou test qui la prouve.

Renseigner à ce stade les exigences qui relèvent de la chaîne de build, de la gestion des secrets et de la configuration. Marquer `prévue lot 4` les exigences de contrôle d'accès, `prévue lot 5` celles d'authentification, `prévue lot 9` celles de journalisation et de supervision.

- [ ] **Étape 3 : compléter `docs/decisions.md`**

Y ajouter les décisions prises pendant l'exécution du lot, au même format que les précédentes — ce qui a été tranché, le motif, **ce qui la rouvrirait**. Au minimum : l'arbitrage `typescript-eslint` contre TypeScript 7, et le nombre de faux positifs Knip écartés avec leur raison.

- [ ] **Étape 4 : commit**

```bash
git add -A
git commit -m "ajoute le gabarit de rapport et le suivi des exigences ASVS"
```

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

## Auto-revue du plan

**Ce que ce plan remplace, et pourquoi.** Les deux plans précédents définissaient chacun `verify` et la CI. Le point d'entrée unique ne peut pas exister en deux exemplaires : le jour où l'un des deux gagne un contrôle que l'autre n'a pas, le local et la CI cessent de vérifier la même chose. C'est le défaut que la tâche 18 existe pour empêcher — il ne pouvait pas vivre dans le plan lui-même.

**Quatre tâches ont été réécrites, pas reprises.**

| Tâche | Ce qui l'a rendue caduque |
|---|---|
| 6 — frontière front/domaine | protégeait `src/core/` contre React ; protège désormais contre la duplication d'un calcul de conformité. `src/llm/` a disparu du front |
| 18 — `verify` | défini deux fois dans les plans séparés, à deux emplacements différents |
| 20 — documents | `docs/decisions.md` créé hors plan lors du changement d'architecture |
| 21 — CI | `vercel.json` caduc, cinq jobs au lieu de quatre, deux écosystèmes |

**Couverture de la spec du lot 1a** (harnais front) : socle → T1 · tests → T2 · typecheck → T4 · lint → T5, T6, T7 · format → T8 · code mort → T9 · accessibilité → T10 · hooks → T19 · documents → T20.

**Couverture de la spec d'architecture** (section 11, harnais .NET) : solution et références → T11 · rigueur du compilateur → T12 · xUnit → T13 · couverture → T14 · format → T15 · licences → T16 · vulnérabilités → T17.

**Les épreuves, une par garde-fou.** TypeScript strict (T4) · `any` explicite (T5) · nommage front (T5) · **pureté de `front/src/core/`** (T6) · **calcul de conformité dans le front** (T6) · i18n (T7) · couleurs hors jetons (T7) · Prettier (T8) · Knip (T9) · axe-core (T10) · **pureté de `Palier.Domain`** (T11) · **référence circulaire provoquée** (T11) · nullabilité (T12) · avertissement en erreur (T12) · test rouge (T13) · couverture (T14) · format C# (T15) · **licence réciproque** (T16) · vulnérabilité (T17) · point d'entrée unique (T18) · gitleaks (T19) · hooks (T19). **Vingt-deux épreuves.**

**Épreuve de la cible manquante :** présente en premier test de chaque fichier d'épreuve. Une fixture supprimée fait échouer son épreuve au lieu de la faire passer au vert par absence de sujet.

**Trois épreuves de la spec ne sont pas dans ce plan, délibérément :** « une donnée de santé dans un journal », « un utilisateur A lit une ligne de B » et « un nom franchit la frontière du modèle » exigent respectivement un journal applicatif, une base et un adaptateur LLM. Aucun n'existe au lot 1. Elles sont inscrites aux lots 4, 5 et 8.

**Testcontainers n'est pas installé ici**, pour la même raison : sans schéma ni EF Core, il n'y a rien à tester contre une base réelle. Lot 4.

**Cohérence des noms.** `lancerOutil` (T2) est consommé sous ce nom exact par les épreuves des tâches 4 à 19. `npm run verify` (T18) est appelé sous ce nom par T19 et T21. `MetabolismeDeBase.MifflinStJeor(Sexe, decimal, decimal, decimal)` (T13) garde cette signature au lot 3.

**Trois points à vérifier à l'exécution, signalés plutôt que découverts :**

1. **`typescript-eslint` contre TypeScript 7 — tranché.** Sa version 8.67.0, publiée le 10/08/2026, exige `typescript >=4.8.4 <6.1.0` ; TypeScript 7.0.2 est hors plage. Le front passe à **Oxlint**, dont le composant type-aware `tsgolint` est construit sur `typescript-go` et cible TypeScript 7 — ses versions suivent celles du compilateur. Ce que cela coûte : `no-restricted-syntax`, `naming-convention` et l'équivalent d'`eslint-plugin-i18next` n'existent pas dans Oxlint, et sont repris par le script de règles de projet de la tâche 7.
2. **L'import de `lancerOutil` traverse la frontière `front`/`back`.** Si `back/tests-harness/*.test.mjs` ne résout pas `../../front/tests/harness/run-outil.js`, la parade est une configuration vitest racine. Détail d'outillage, pas d'exigence.
3. **`gitleaks` distribué par npm** n'expose pas forcément un binaire sur toutes les plateformes. Si `npx gitleaks` échoue sous Windows, replier sur l'action GitHub officielle en CI et une installation locale pour le hook.
