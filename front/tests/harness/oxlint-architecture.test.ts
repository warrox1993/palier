// @vitest-environment node
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
