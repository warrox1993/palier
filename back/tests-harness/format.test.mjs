// @vitest-environment node
import { copyFileSync, rmSync, existsSync } from 'node:fs'
import { describe, expect, it, afterAll } from 'vitest'
import { lancerOutil } from '../../front/tests/harness/run-outil.js'

const SOURCE = 'back/tests-harness/fixtures/MalFormate.cs.txt'
const CIBLE = 'back/Palier.Domain/MalFormate.cs'

afterAll(() => rmSync(CIBLE, { force: true }))

describe('garde-fou : format du code', () => {
  it('la fixture existe', () => {
    expect(existsSync(SOURCE), `Cible manquante : ${SOURCE}`).toBe(true)
  })

  it('refuse un fichier mal formaté', () => {
    copyFileSync(SOURCE, CIBLE)
    const r = lancerOutil(['dotnet', 'format', 'back/Palier.sln', '--verify-no-changes'])
    expect(r.code, `dotnet format a accepté le fichier :\n${r.sortie}`).not.toBe(0)
    // Seconde assertion obligatoire : un code non nul prouve seulement que
    // quelque chose a échoué, pas que l'outil a refusé. Sans elle, l'épreuve
    // passe au vert quand l'outil est absent, indisponible ou mal appelé.
    expect(r.sortie).toMatch(/WHITESPACE|IDE\d{4}|formatted incorrectly/)
  })
})
