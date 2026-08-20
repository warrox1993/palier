// @vitest-environment node
import { existsSync } from 'node:fs'
import { describe, expect, it } from 'vitest'
import { lancerOutil } from './run-outil'

const FIXTURES = [
  'tests/harness/fixtures/calcul-conformite.ts',
  'tests/harness/fixtures/couleur-en-dur.ts',
  'tests/harness/fixtures/chaine-en-dur.tsx',
]

describe('garde-fou : règles de projet', () => {
  it.each(FIXTURES)('la fixture %s existe', (f) => {
    expect(existsSync(f), `Cible manquante : ${f}`).toBe(true)
  })

  it('accepte le code réel du front', () => {
    const r = lancerOutil(['node', '../scripts/regles-projet.mjs'])
    expect(r.code, `Le front réel viole une règle de projet :\n${r.sortie}`).toBe(0)
  })

  it('refuse un calcul de conformité dans le front', () => {
    const r = lancerOutil([
      'node',
      '../scripts/regles-projet.mjs',
      '--fichier',
      'front/tests/harness/fixtures/calcul-conformite.ts',
    ])
    expect(r.code, `Le calcul de conformité a été accepté :\n${r.sortie}`).not.toBe(0)
    expect(r.sortie).toMatch(/Palier\.Domain/)
  })

  it('refuse une couleur hexadécimale hors jetons', () => {
    const r = lancerOutil([
      'node',
      '../scripts/regles-projet.mjs',
      '--fichier',
      'front/tests/harness/fixtures/couleur-en-dur.ts',
    ])
    expect(r.code, `La couleur a été acceptée :\n${r.sortie}`).not.toBe(0)
    expect(r.sortie).toMatch(/jeton/i)
  })

  it('refuse une chaîne de texte en dur dans le JSX', () => {
    const r = lancerOutil([
      'node',
      '../scripts/regles-projet.mjs',
      '--fichier',
      'front/tests/harness/fixtures/chaine-en-dur.tsx',
    ])
    expect(r.code, `La chaîne en dur a été acceptée :\n${r.sortie}`).not.toBe(0)
    expect(r.sortie).toMatch(/i18n/i)
  })
})
