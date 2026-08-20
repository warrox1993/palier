// @vitest-environment node
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
