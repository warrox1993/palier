// @vitest-environment node
import { existsSync, writeFileSync, rmSync } from 'node:fs'
import { describe, expect, it, afterAll } from 'vitest'
import { lancerOutil } from '../../front/tests/harness/run-outil.js'

const NON_TESTE = 'back/Palier.Domain/Energie/NonTeste.cs'

afterAll(() => rmSync(NON_TESTE, { force: true }))

describe('garde-fou : couverture du domaine', () => {
  it('le fichier de configuration existe', () => {
    expect(existsSync('back/coverage.runsettings'), 'Cible manquante').toBe(true)
  })

  it('refuse une fonction du domaine non couverte', () => {
    // La fonction s'appelle `Doubler` et non `Double` comme le prévoyait le
    // plan : CA1720 refuse un identificateur qui porte un nom de type, et la
    // rigueur de la tâche 12 en fait une erreur. La compilation échouait donc
    // AVANT toute mesure de couverture — code non nul, seuil jamais atteint.
    // La seconde assertion l'a montré ; sans elle, l'épreuve passait au vert
    // en ne contrôlant rien.
    writeFileSync(
      NON_TESTE,
      `namespace Palier.Domain.Energie;\n\n` +
        `public static class NonTeste\n{\n` +
        `    public static decimal Doubler(decimal x) => x * 2m;\n}\n`,
    )
    const r = lancerOutil([
      'dotnet', 'test', 'back/Palier.Domain.Tests',
      '--settings', 'back/coverage.runsettings',
    ])
    expect(r.code, `Le seuil de couverture n'a pas mordu :\n${r.sortie}`).not.toBe(0)
    // Seconde assertion obligatoire : un code non nul prouve seulement que
    // quelque chose a échoué, pas que l'outil a refusé. Sans elle, l'épreuve
    // passe au vert quand l'outil est absent, indisponible ou mal appelé.
    //
    // Le motif du plan, /threshold|seuil|coverage/i, était trop large : mesuré,
    // il matchait le CHEMIN « back/coverage.runsettings » recopié dans le
    // message d'erreur quand le fichier n'existait pas encore. L'épreuve
    // passait au vert alors qu'aucune couverture n'avait été mesurée. Le motif
    // porte désormais sur le verdict de coverlet, qu'aucun chemin ne peut
    // contenir par accident.
    expect(r.sortie).toMatch(/coverage is below the specified/i)
  })
})
