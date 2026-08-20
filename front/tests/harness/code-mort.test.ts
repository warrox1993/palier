// @vitest-environment node
import { existsSync } from 'node:fs'
import { describe, expect, it } from 'vitest'
import { lancerOutil } from './run-outil'

const FIXTURE = 'tests/harness/fixtures/export-orphelin.ts'

describe('garde-fou : code mort', () => {
  it('la fixture de violation existe', () => {
    expect(existsSync(FIXTURE), `Cible manquante : ${FIXTURE}`).toBe(true)
  })

  it("signale un export que personne n'importe", () => {
    const r = lancerOutil(['npx', 'knip', '--config', 'knip.fixtures.json'])
    expect(r.code, `Knip n'a pas vu l'export orphelin :\n${r.sortie}`).not.toBe(0)
    expect(r.sortie).toMatch(/orphelin|unused|export/i)
  })
})
