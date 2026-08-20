# Brief — Tâche 10

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

## Tâche 10 : la commande unique `verify`

**Fichiers :**
- Créer : `scripts/verify.mjs`, `tests/harness/verify.test.ts`
- Modifier : `package.json`

**Interfaces :**
- Consomme : tous les scripts des tâches 1 à 9
- Produit : `npm run verify` — **la seule commande** que le hook pre-push et la CI appellent.

- [ ] **Étape 1 : écrire l'épreuve**

`tests/harness/verify.test.ts` :

```typescript
import { existsSync, readFileSync } from 'node:fs'
import { describe, expect, it } from 'vitest'

describe('garde-fou : point d\'entrée unique', () => {
  it('le script verify existe', () => {
    expect(existsSync('scripts/verify.mjs'), 'Cible manquante : scripts/verify.mjs').toBe(true)
  })

  it('verify enchaîne les six contrôles attendus', () => {
    const s = readFileSync('scripts/verify.mjs', 'utf8')
    for (const etape of ['format:check', 'lint', 'typecheck', 'test', 'knip', 'build']) {
      expect(s, `Contrôle manquant dans verify : ${etape}`).toContain(etape)
    }
  })
})
```

**Ruling C1 du ledger :** les deux tests qui vérifient que le hook pre-push et la CI n'appellent que `verify` ne peuvent pas vivre ici — ces fichiers n'existent qu'aux tâches 11 et 13. Ils y sont écrits, et c'est là qu'ils empêchent la dérive : le jour où quelqu'un ajoute un contrôle à la CI sans le mettre dans `verify`, le local et la CI cessent de vérifier la même chose.

- [ ] **Étape 2 : lancer l'épreuve pour la voir échouer**

Lancer : `npx vitest run tests/harness/verify.test.ts`
Attendu : ÉCHEC — `scripts/verify.mjs` n'existe pas.

- [ ] **Étape 3 : écrire `scripts/verify.mjs`**

```javascript
#!/usr/bin/env node
import { spawnSync } from 'node:child_process'

const SEUIL_MS = 45000

const etapes = [
  ['format:check', ['npm', 'run', 'format:check']],
  ['lint', ['npm', 'run', 'lint']],
  ['typecheck', ['npm', 'run', 'typecheck']],
  ['test', ['npm', 'run', 'test']],
  ['knip', ['npm', 'run', 'knip']],
  ['build', ['npm', 'run', 'build']],
]

const debut = Date.now()
const durees = []
let echec = null

for (const [nom, commande] of etapes) {
  const t0 = Date.now()
  const [bin, ...args] = commande
  const r = spawnSync(bin, args, { stdio: 'inherit', shell: process.platform === 'win32' })
  const ms = Date.now() - t0
  durees.push([nom, ms])
  if (r.status !== 0) {
    echec = nom
    break
  }
}

const total = Date.now() - debut
console.log('\n─── verify ───')
for (const [nom, ms] of durees) console.log(`  ${nom.padEnd(14)} ${(ms / 1000).toFixed(1)} s`)
console.log(`  ${'TOTAL'.padEnd(14)} ${(total / 1000).toFixed(1)} s`)

if (echec) {
  console.error(`\nÉCHEC sur : ${echec}`)
  process.exit(1)
}

if (total > SEUIL_MS) {
  console.warn(
    `\nAVERTISSEMENT : verify a pris ${(total / 1000).toFixed(1)} s, au-delà du seuil de ${SEUIL_MS / 1000} s.\n` +
      'Une boucle de rétroaction lente est un défaut à traiter — docs/08-workflow.md § 5.',
  )
}
```

Le dépassement de seuil avertit sans bloquer : un harnais lent est un défaut, pas une erreur.

- [ ] **Étape 4 : ajouter le script**

```json
{ "scripts": { "verify": "node scripts/verify.mjs" } }
```

- [ ] **Étape 5 : lancer verify et mesurer**

Lancer : `npm run verify`
Attendu : les six contrôles passent, la durée totale s'affiche. Noter cette durée dans le rapport de lot.

- [ ] **Étape 6 : commit**

```bash
git add -A
git commit -m "ajoute la commande de vérification unique"
```

---
