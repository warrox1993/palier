// @vitest-environment node
import { existsSync, rmSync, writeFileSync } from 'node:fs'
import { afterAll, describe, expect, it } from 'vitest'
import { lancerOutil } from '../../front/tests/harness/run-outil.js'

const FIXTURE = 'back/tests-harness/fixtures/Nullable.cs'
const HERITE = 'back/Palier.Domain/ChampInutilise.cs'

afterAll(() => rmSync(HERITE, { force: true }))

describe('garde-fou : rigueur du compilateur', () => {
  it('la fixture de violation existe', () => {
    expect(existsSync(FIXTURE), `Cible manquante : ${FIXTURE}`).toBe(true)
  })

  it('refuse un déréférencement possiblement nul', () => {
    const r = lancerOutil(['dotnet', 'build', 'back/tests-harness/fixtures/Fixtures.csproj'])
    expect(r.code, `Le compilateur a accepté la violation :\n${r.sortie}`).not.toBe(0)
    expect(r.sortie).toMatch(/CS8600|CS8602|CS8604/)
  })

  // `Fixtures.csproj` déclare lui-même `Nullable` et `TreatWarningsAsErrors` :
  // son refus prouve que le compilateur sait refuser, pas que
  // `Directory.Build.props` existe. Mesuré : l'épreuve ci-dessus reste VERTE
  // quand on retire le fichier de la racine — elle ne contrôlait donc pas le
  // garde-fou que la tâche installe.
  // `Palier.Domain`, lui, ne déclare ni `TreatWarningsAsErrors` ni niveau
  // d'analyse. Un simple avertissement n'y devient une erreur que par le
  // fichier de la racine : c'est ce chemin-là qu'il faut franchir.
  it('impose la rigueur aux projets qui ne la déclarent pas eux-mêmes', () => {
    writeFileSync(
      HERITE,
      'namespace Palier.Domain;\n\n' +
        'public static class ChampInutilise\n{\n' +
        '    private static int _jamaisUtilise;\n}\n',
    )
    const r = lancerOutil(['dotnet', 'build', 'back/Palier.Domain/Palier.Domain.csproj'])
    expect(
      r.code,
      `Un avertissement est resté un avertissement — Directory.Build.props ne mord pas :\n${r.sortie}`,
    ).not.toBe(0)
    expect(r.sortie).toMatch(/CS0169|CA1823/)
  })
})
