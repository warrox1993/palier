# Brief — Tâche 16

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
