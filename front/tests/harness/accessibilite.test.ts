// @vitest-environment node
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
