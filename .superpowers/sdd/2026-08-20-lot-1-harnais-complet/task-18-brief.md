# Brief — Tâche 18

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
    "test:harness": "npm --prefix front run test:harness && vitest run tests-harness back/tests-harness",
    "front": "npm --prefix front run"
  }
}
```

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
