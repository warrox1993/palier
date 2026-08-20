// @vitest-environment node
import { existsSync, rmSync, writeFileSync } from 'node:fs'
import { afterAll, describe, expect, it } from 'vitest'
import { lancerOutil } from './run-outil'

const IMPUR = 'tests/harness/fixtures/core-impur.ts'
const FLOTTANTE = 'tests/harness/fixtures/promesse-flottante.ts'
const MAL_EMPLOYEE = 'tests/harness/fixtures/promesse-mal-employee.ts'

describe('garde-fou : pureté de front/src/core/', () => {
  it('la fixture de violation existe', () => {
    expect(existsSync(IMPUR), `Cible manquante : ${IMPUR}`).toBe(true)
  })

  it('refuse un import de React depuis core/', () => {
    const r = lancerOutil(['npx', 'oxlint', IMPUR])
    expect(r.code, `Oxlint a accepté l'import impur :\n${r.sortie}`).not.toBe(0)
    expect(r.sortie).toMatch(/no-restricted-imports/)
  })
})

// Les cinq règles type-aware de `.oxlintrc.json` étaient supprimables sans
// qu'un test bouge : la seule épreuve du mode était `expect(code).toBe(0)` sur
// du code sain, qui reste vert quand la configuration est vidée. Même famille
// que le bloquant n° 3 de la revue précédente, pour une autre famille de règles.
//
// Mesuré en les retirant une à une du bloc `rules` : `no-floating-promises` et
// `await-thenable` continuent de mordre, rallumées par `categories.correctness`
// dès que `--type-aware` est actif. Les trois autres — `no-misused-promises`,
// `strict-boolean-expressions`, `no-unsafe-assignment` — ne tiennent qu'à leur
// ligne de configuration et disparaissent avec elle. Les deux fixtures couvrent
// les cinq, et l'épreuve du script `lint:types` plus bas couvre le drapeau
// `--type-aware` lui-même, que rien d'autre ne protège.
describe('garde-fou : Oxlint, règles type-aware', () => {
  it.each([FLOTTANTE, MAL_EMPLOYEE])('la fixture %s existe', (f) => {
    expect(existsSync(f), `Cible manquante : ${f}`).toBe(true)
  })

  it('refuse une promesse flottante', () => {
    const r = lancerOutil(['npx', 'oxlint', '--type-aware', FLOTTANTE])
    expect(r.code, `Oxlint a accepté la promesse flottante :\n${r.sortie}`).not.toBe(0)
    expect(r.sortie).toMatch(/no-floating-promises/)
  })

  // Le code de sortie ne suffit pas ici : `unicorn/no-unnecessary-await`, qui ne
  // demande aucune information de type, refuse déjà ce fichier. Seules les
  // assertions de motif prouvent que les règles type-aware sont bien en vie —
  // y compris les deux qui n'émettent qu'un avertissement et ne changent donc
  // jamais le code de sortie.
  it('signale les quatre autres règles type-aware', () => {
    const r = lancerOutil(['npx', 'oxlint', '--type-aware', MAL_EMPLOYEE])
    expect(r.code, `Oxlint a accepté les promesses mal employées :\n${r.sortie}`).not.toBe(0)
    expect(r.sortie).toMatch(/await-thenable/)
    expect(r.sortie).toMatch(/no-misused-promises/)
    expect(r.sortie).toMatch(/strict-boolean-expressions/)
    expect(r.sortie).toMatch(/no-unsafe-assignment/)
  })

  it('accepte le code réel du front, après avoir réellement lu des fichiers', () => {
    const r = lancerOutil([
      'npx',
      'oxlint',
      '--type-aware',
      '--ignore-pattern',
      'tests/harness/fixtures/**',
      '-f',
      'json',
      'src',
      'tests',
    ])
    expect(r.code, `Le mode type-aware a échoué sur du code réel :\n${r.sortie}`).toBe(0)
    // Assertion positive : un code 0 ne prouve pas qu'Oxlint ait regardé quoi
    // que ce soit. Le compte-rendu JSON dit combien de fichiers il a lus.
    const lus = /"number_of_files":\s*(\d+)/.exec(r.sortie)
    expect(lus, `Oxlint n'a pas rendu de compte-rendu JSON :\n${r.sortie}`).not.toBeNull()
    expect(Number(lus?.[1] ?? 0)).toBeGreaterThan(0)
  })
})

// `lint:types` ne visait que `src`. Mesuré : un `page.goto()` non attendu — le
// bug Playwright le plus courant, et une perte de données silencieuse dans la
// file de retry hors ligne — passait `lint` ET `lint:types` en code 0. Cette
// épreuve lance le script npm réel, pas l'outil : c'est la liste de cibles du
// script qu'elle protège, et rien d'autre ne le fait.
describe('garde-fou : lint:types couvre tests/ autant que src/', () => {
  const SONDE = 'tests/harness/sonde-promesse-flottante.ts'

  afterAll(() => {
    rmSync(SONDE, { force: true })
  })

  it('refuse une promesse flottante plantée dans tests/', () => {
    writeFileSync(
      SONDE,
      [
        '// Sonde temporaire écrite par oxlint-architecture.test.ts, retirée en afterAll.',
        'async function sonder(): Promise<void> {',
        '  await Promise.resolve()',
        '}',
        '',
        'export function declencherLaSonde(): void {',
        '  sonder()',
        '}',
        '',
      ].join('\n'),
      'utf8',
    )

    const r = lancerOutil(['npm', 'run', 'lint:types'])
    expect(r.code, `npm run lint:types n'a pas vu la sonde dans tests/ :\n${r.sortie}`).not.toBe(0)
    expect(r.sortie).toMatch(/no-floating-promises/)
    expect(r.sortie).toMatch(/sonde-promesse-flottante/)
  })

  it('la sonde est bien retirée entre deux épreuves', () => {
    // Franchit la branche de nettoyage : sans elle, un échec laisserait un
    // fichier en violation dans `tests/` et tous les contrôles suivants
    // rougiraient pour une raison qui n'a rien à voir.
    rmSync(SONDE, { force: true })
    expect(existsSync(SONDE), `La sonde ${SONDE} a survécu à son épreuve.`).toBe(false)
  })
})
