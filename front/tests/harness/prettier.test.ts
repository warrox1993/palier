// @vitest-environment node
import { existsSync } from 'node:fs'
import { describe, expect, it } from 'vitest'
import { lancerOutil } from './run-outil'

const FIXTURE = 'tests/harness/fixtures/format-casse.ts'

describe('garde-fou : Prettier', () => {
  it('la fixture de violation existe', () => {
    expect(existsSync(FIXTURE), `Cible manquante : ${FIXTURE}`).toBe(true)
  })

  it('refuse un fichier mal formaté', () => {
    const r = lancerOutil(['npx', 'prettier', '--check', FIXTURE])
    expect(r.code, `Prettier a accepté le fichier :\n${r.sortie}`).not.toBe(0)
    // Seconde assertion obligatoire : un code non nul prouve seulement que
    // quelque chose a échoué, pas que Prettier a refusé. Sans elle, l'épreuve
    // passe au vert quand l'outil est simplement absent — mesuré.
    expect(r.sortie).toMatch(/Code style issues/)
  })
})
