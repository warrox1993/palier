// @vitest-environment node
import { existsSync, rmSync, writeFileSync } from 'node:fs'
import { afterAll, describe, expect, it } from 'vitest'
import { lancerOutil } from './run-outil'

const FIXTURE = 'tests/harness/fixtures/export-orphelin.ts'

describe('garde-fou : code mort, configuration dédiée à la fixture', () => {
  it('la fixture de violation existe', () => {
    expect(existsSync(FIXTURE), `Cible manquante : ${FIXTURE}`).toBe(true)
  })

  it("signale un export que personne n'importe", () => {
    const r = lancerOutil(['npx', 'knip', '--config', 'knip.fixtures.json'])
    expect(r.code, `Knip n'a pas vu l'export orphelin :\n${r.sortie}`).not.toBe(0)
    expect(r.sortie).toMatch(/orphelin|unused|export/i)
  })
})

// `knip.fixtures.json` et `knip.json` ne partagent rien : entrées, périmètre et
// exclusions diffèrent entièrement. Or c'est `npm run knip` — donc `knip.json`
// — que `verify` et la CI appellent. La configuration bloquante n'était éprouvée
// par rien : on pouvait la vider sans qu'un test bouge.
describe('garde-fou : code mort, configuration réelle', () => {
  const SONDE = 'src/sonde-code-mort.ts'

  afterAll(() => {
    rmSync(SONDE, { force: true })
  })

  it('npm run knip signale un fichier orphelin dans src/', () => {
    writeFileSync(
      SONDE,
      [
        '// Sonde temporaire écrite par code-mort.test.ts, retirée en afterAll.',
        'export function personneNeMAppelle(): string {',
        "  return 'orphelin'",
        '}',
        '',
      ].join('\n'),
      'utf8',
    )

    const r = lancerOutil(['npm', 'run', 'knip'])
    expect(r.code, `npm run knip n'a pas vu le fichier orphelin :\n${r.sortie}`).not.toBe(0)
    // Deux motifs : un code non nul prouve seulement que quelque chose a
    // échoué. Le second nomme la cible, sans quoi l'épreuve resterait verte sur
    // un tout autre signalement — le ruling P10 à l'identique.
    expect(r.sortie).toMatch(/Unused files/)
    expect(r.sortie).toMatch(/sonde-code-mort/)
  })

  it('la sonde est bien retirée entre deux épreuves', () => {
    // Franchit la branche de nettoyage : un fichier orphelin oublié dans `src/`
    // ferait rougir `knip` pour tout le monde, sans rapport avec la cause.
    rmSync(SONDE, { force: true })
    expect(existsSync(SONDE), `La sonde ${SONDE} a survécu à son épreuve.`).toBe(false)
  })
})
