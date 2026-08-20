import { existsSync } from 'node:fs'
import { describe, expect, it } from 'vitest'
import configuration from '../.lintstagedrc.mjs'

// `front/tests/harness/fixtures/format-casse.ts` est délibérément mal formaté :
// c'est la cible de l'épreuve Prettier. Si lint-staged le passait à
// `prettier --write` au moment du commit, la fixture serait réparée et son
// épreuve passerait au vert sans plus rien contrôler — un garde-fou du harnais
// en désarmant un autre, exactement le ruling P15.
//
// Le filtre est ici éprouvé sur des chemins, sans toucher à l'index git :
// stager des fichiers depuis une épreuve pour observer le hook mettrait en jeu
// l'index d'un dépôt réel.
describe('garde-fou : lint-staged épargne les fixtures', () => {
  const motif = Object.keys(configuration)[0]
  const formater = configuration[motif]

  it('la configuration existe et porte une fonction', () => {
    expect(existsSync('.lintstagedrc.mjs'), 'Cible manquante : .lintstagedrc.mjs').toBe(true)
    expect(formater).toBeTypeOf('function')
  })

  it('un fichier ordinaire est bien confié à Prettier', () => {
    const commandes = formater(['C:/depot/front/src/app/App.tsx'])
    expect(commandes).toHaveLength(1)
    expect(commandes[0]).toContain('prettier')
    expect(commandes[0]).toContain('App.tsx')
  })

  it('une fixture du harnais front est écartée', () => {
    const commandes = formater(['C:/depot/front/tests/harness/fixtures/format-casse.ts'])
    expect(commandes, 'lint-staged reformaterait la fixture et désarmerait son épreuve').toEqual([])
  })

  it('une fixture du harnais backend est écartée', () => {
    const commandes = formater(['/depot/back/tests-harness/fixtures/Violation.cs'])
    expect(commandes).toEqual([])
  })

  it('les séparateurs Windows sont reconnus', () => {
    // lint-staged rend des chemins absolus ; sous Windows ils arrivent avec des
    // antislashs. Un filtre écrit sur les seules barres obliques laisserait
    // passer la fixture SUR LA MACHINE OÙ ELLE ARRIVE, et nulle part ailleurs.
    const commandes = formater(['C:\\depot\\front\\tests\\harness\\fixtures\\format-casse.ts'])
    expect(commandes, 'le filtre ne reconnaît pas les chemins Windows').toEqual([])
  })

  it('un lot mixte ne garde que ce qui doit être formaté', () => {
    const commandes = formater([
      'C:/depot/front/src/main.tsx',
      'C:/depot/front/tests/harness/fixtures/any-explicite.ts',
      'C:/depot/scripts/verify.mjs',
    ])
    expect(commandes).toHaveLength(1)
    expect(commandes[0]).toContain('main.tsx')
    expect(commandes[0]).toContain('verify.mjs')
    expect(commandes[0], 'la fixture est repartie avec le lot').not.toContain('any-explicite')
  })
})
