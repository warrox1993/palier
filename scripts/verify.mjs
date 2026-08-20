#!/usr/bin/env node
import { spawnSync } from 'node:child_process'

const SEUIL_MS = 90000 // deux harnais : seuil doublé par rapport au front seul

const etapes = [
  ['front:format', ['npm', '--prefix', 'front', 'run', 'format:check']],
  ['front:lint', ['npm', '--prefix', 'front', 'run', 'lint']],
  ['front:lint:types', ['npm', '--prefix', 'front', 'run', 'lint:types']],
  ['front:typecheck', ['npm', '--prefix', 'front', 'run', 'typecheck']],
  ['front:test', ['npm', '--prefix', 'front', 'run', 'test']],
  ['front:knip', ['npm', '--prefix', 'front', 'run', 'knip']],
  ['scripts:lint', ['npm', 'run', 'lint:scripts']],
  ['scripts:format', ['npm', 'run', 'format:scripts']],
  ['regles', ['node', 'scripts/regles-projet.mjs']],
  ['back:format', ['dotnet', 'format', 'back/Palier.sln', '--verify-no-changes']],
  ['back:build', ['dotnet', 'build', 'back/Palier.sln', '--no-incremental']],
  ['back:test', ['dotnet', 'test', 'back/Palier.sln', '--settings', 'back/coverage.runsettings']],
  ['licences', ['node', 'scripts/verifier-licences.mjs']],
  ['back:audit', ['npm', 'run', 'audit:back']],
  ['front:build', ['npm', '--prefix', 'front', 'run', 'build']],
]

const surWindows = process.platform === 'win32'

// Depuis Node 24 (DEP0190), un tableau d'arguments passé avec `shell: true`
// n'est plus quoté par Node : les éléments sont concaténés bruts. Un argument
// contenant une espace ou un guillemet se scinderait donc en silence, et
// l'étape contrôlerait autre chose que ce qu'elle annonce. Aucune commande de
// cette liste n'en contient ; ce garde-fou refuse le jour où l'une en portera.
for (const [nom, [, ...args]] of etapes) {
  for (const arg of args) {
    if (/[\s"]/.test(arg)) {
      throw new Error(
        `Étape « ${nom} » : l'argument ${JSON.stringify(arg)} contient une espace ou un ` +
          'guillemet. Sous Windows, `shell: true` le scinderait sans avertissement — ' +
          'passer par un script npm plutôt que par un argument composé.',
      )
    }
  }
}

const debut = Date.now()
const durees = []
let echec = null

for (const [nom, commande] of etapes) {
  const t0 = Date.now()
  // Sous Windows, `shell: true` reste nécessaire pour résoudre les `.cmd`
  // installés par npm. La commande y est passée en chaîne unique : le tableau
  // d'arguments déclenche DEP0190, et Node ne les échapperait pas de toute
  // façon. La boucle de contrôle ci-dessus garantit qu'aucun argument ne
  // contient d'espace ni de guillemet, donc que le recollage est fidèle.
  const [bin, ...args] = commande
  const r = surWindows
    ? spawnSync(commande.join(' '), [], { stdio: 'inherit', shell: true })
    : spawnSync(bin, args, { stdio: 'inherit', shell: false })
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
