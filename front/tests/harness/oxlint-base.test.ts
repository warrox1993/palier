import { existsSync } from 'node:fs'
import { describe, expect, it } from 'vitest'
import { lancerOutil } from './run-outil'

const ANY = 'tests/harness/fixtures/any-explicite.ts'
const NOMMAGE = 'tests/harness/fixtures/nommage-Invalide.ts'

describe('garde-fou : Oxlint, règles syntaxiques', () => {
  it.each([ANY, NOMMAGE])('la fixture %s existe', (f) => {
    expect(existsSync(f), `Cible manquante : ${f}`).toBe(true)
  })

  it('refuse un any explicite', () => {
    const r = lancerOutil(['npx', 'oxlint', ANY])
    expect(r.code, `Oxlint a accepté le any :\n${r.sortie}`).not.toBe(0)
    expect(r.sortie).toContain('no-explicit-any')
  })

  it('refuse un nom de fichier hors kebab-case et PascalCase', () => {
    const r = lancerOutil(['npx', 'oxlint', NOMMAGE])
    expect(r.code, `Oxlint a accepté le nommage :\n${r.sortie}`).not.toBe(0)
    expect(r.sortie).toMatch(/filename-case/)
  })
})
