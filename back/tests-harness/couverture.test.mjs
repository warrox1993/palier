// @vitest-environment node
import { existsSync, readFileSync, writeFileSync, rmSync } from 'node:fs'
import { describe, expect, it, afterAll } from 'vitest'
import { lancerOutil } from '../../front/tests/harness/run-outil.js'

const NON_TESTE = 'back/Palier.Domain/Depense/NonTeste.cs'

// C'est ce projet, et non `back/coverage.runsettings`, qui porte le seuil : le
// collecteur VSTest de coverlet ignore `Threshold` en silence (ruling P11).
const PORTEUR_DU_SEUIL = 'back/Palier.Domain.Tests/Palier.Domain.Tests.csproj'

afterAll(() => rmSync(NON_TESTE, { force: true }))

describe('garde-fou : couverture du domaine', () => {
  // La sentinelle surveillait `back/coverage.runsettings`, sous le libellé
  // « Cible manquante ». Depuis le déplacement du seuil, ce fichier ne porte
  // plus aucun garde-fou : il ne décrit que le format du rapport. On pouvait
  // donc vider le seuil sans qu'aucune sentinelle ne bouge. Elle surveille
  // désormais le fichier qui l'applique, et la valeur, pas seulement l'existence.
  it('le seuil de couverture est déclaré là où il est appliqué', () => {
    expect(existsSync(PORTEUR_DU_SEUIL), `Cible manquante : ${PORTEUR_DU_SEUIL}`).toBe(true)
    const projet = readFileSync(PORTEUR_DU_SEUIL, 'utf8')
    expect(projet, `Le seuil de 100 % a disparu de ${PORTEUR_DU_SEUIL}`).toMatch(
      /<Threshold>100<\/Threshold>/,
    )
    expect(projet, `Le seuil ne couvre plus ligne, branche et méthode`).toMatch(
      /<ThresholdType>line,branch,method<\/ThresholdType>/,
    )
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
      `namespace Palier.Domain.Depense;\n\n` +
        `public static class NonTeste\n{\n` +
        `    public static decimal Doubler(decimal x) => x * 2m;\n}\n`,
    )
    const r = lancerOutil([
      'dotnet',
      'test',
      'back/Palier.Domain.Tests',
      '--settings',
      'back/coverage.runsettings',
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
