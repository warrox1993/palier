// @vitest-environment node
import { existsSync, rmSync, writeFileSync } from 'node:fs'
import { afterEach, describe, expect, it } from 'vitest'
import { lancerOutil } from '../../front/tests/harness/run-outil.js'

const FIXTURE = 'back/tests-harness/fixtures/Nullable.cs'
const HERITE = 'back/Palier.Domain/ChampInutilise.cs'

// L'exception CA1707 du `.editorconfig` doit couvrir les DEUX profondeurs d'un
// projet de tests. Ces deux sondes les provoquent l'une et l'autre.
const SONDE_RACINE = 'back/Palier.Domain.Tests/SondeRacineTests.cs'
const SONDE_SOUS_DOSSIER = 'back/Palier.Domain.Tests/Energie/SondeSousDossierTests.cs'

const classeSonde = (espace, nom, corps) =>
  `namespace ${espace};\n\npublic sealed class ${nom}\n{\n${corps}    [Fact]\n` +
  `    public void Sonde_au_nom_xunit()\n    {\n        Assert.Equal(2m, 1m + 1m);\n    }\n}\n`

// Nettoyage après CHAQUE épreuve, et non après toutes : `ChampInutilise.cs`
// laissé en place ferait échouer la compilation de `Palier.Domain.Tests`, qui
// référence `Palier.Domain`. L'épreuve suivante rougirait alors pour la
// violation de la précédente — verte ou rouge, mais jamais pour sa cause.
afterEach(() => {
  rmSync(HERITE, { force: true })
  rmSync(SONDE_RACINE, { force: true })
  rmSync(SONDE_SOUS_DOSSIER, { force: true })
})

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

  // L'exception CA1707 du `.editorconfig` s'écrivait `[**/*.Tests/**/*.cs]`, ce
  // qui ne couvre que les SOUS-DOSSIERS. Mesuré le 20/08/2026 :
  // `Palier.Domain.Tests/SondeRacineTests.cs` sortait en `error CA1707`,
  // `Palier.Domain.Tests/Energie/SondeSousDossierTests.cs` passait. Le seul
  // fichier de tests existant vivant dans `Energie/`, le trou était invisible —
  // et le prochain fichier créé à l'endroit le plus naturel, la racine du
  // projet, aurait cassé le build avec le nommage que la tâche 13 impose.
  it('admet le nommage xUnit à la racine du projet de tests comme en sous-dossier', () => {
    writeFileSync(SONDE_RACINE, classeSonde('Palier.Domain.Tests', 'SondeRacineTests', ''))
    writeFileSync(
      SONDE_SOUS_DOSSIER,
      classeSonde('Palier.Domain.Tests.Energie', 'SondeSousDossierTests', ''),
    )
    const r = lancerOutil(['dotnet', 'build', 'back/Palier.Domain.Tests/Palier.Domain.Tests.csproj'])
    expect(
      r.code,
      `Un nom de test xUnit a été refusé — l'exception CA1707 ne couvre pas les deux profondeurs :\n${r.sortie}`,
    ).toBe(0)
    // Seconde assertion : un code 0 prouve seulement que rien n'a échoué. Sans
    // ce motif, l'épreuve resterait verte si MSBuild n'avait rien compilé du
    // tout — sondes non prises en compte, projet introuvable, build à vide.
    expect(r.sortie, `Le projet de tests n'a pas été compilé :\n${r.sortie}`).toMatch(
      /Palier\.Domain\.Tests\.dll/,
    )
  })

  // Et l'exception ne désarme QUE CA1707. Les mêmes fichiers, aux mêmes deux
  // endroits, portant cette fois un champ privé inutilisé, doivent être
  // refusés : sans quoi l'exception aurait ouvert bien plus qu'une porte.
  it("n'étend l'exception à aucune autre règle, ni à la racine ni en sous-dossier", () => {
    const champ = '    private readonly int _jamaisUtilise;\n\n'
    writeFileSync(SONDE_RACINE, classeSonde('Palier.Domain.Tests', 'SondeRacineTests', champ))
    writeFileSync(
      SONDE_SOUS_DOSSIER,
      classeSonde('Palier.Domain.Tests.Energie', 'SondeSousDossierTests', champ),
    )
    const r = lancerOutil(['dotnet', 'build', 'back/Palier.Domain.Tests/Palier.Domain.Tests.csproj'])
    expect(r.code, `Le champ inutilisé a été accepté :\n${r.sortie}`).not.toBe(0)
    // Les deux profondeurs doivent être nommées dans le refus. Une seule
    // suffirait à faire échouer la build, et l'autre passerait inaperçue.
    for (const sonde of ['SondeRacineTests.cs', 'SondeSousDossierTests.cs']) {
      expect(r.sortie, `${sonde} n'est pas refusé :\n${r.sortie}`).toMatch(
        new RegExp(`${sonde.replace('.', '\\.')}[^\\n]*(CS0169|CA1823)`),
      )
    }
  })
})
