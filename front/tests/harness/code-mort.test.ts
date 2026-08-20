// @vitest-environment node
import { existsSync, readFileSync, rmSync, writeFileSync } from 'node:fs'
import { afterAll, describe, expect, it } from 'vitest'
import { lancerOutil } from './run-outil'

const FIXTURE = 'tests/harness/fixtures/export-orphelin.ts'

describe('garde-fou : code mort, configuration dédiée à la fixture', () => {
  it('la fixture de violation existe', () => {
    expect(existsSync(FIXTURE), `Cible manquante : ${FIXTURE}`).toBe(true)
  })

  it("signale un export que personne n'importe", () => {
    const r = lancerOutil(['npx', 'knip', '--config', 'knip.fixtures.json'])
    expect(r.code, `Knip n'a pas vu l'export orphelin :\n${r.sortie}`).not.toBe(0)
    expect(r.sortie).toMatch(/orphelin|unused|export/i)
  })
})

// `knip.fixtures.json` et `knip.json` ne partagent rien : entrées, périmètre et
// exclusions diffèrent entièrement. Or c'est `npm run knip` — donc `knip.json`
// — que `verify` et la CI appellent. La configuration bloquante n'était éprouvée
// par rien : on pouvait la vider sans qu'un test bouge.
describe('garde-fou : code mort, configuration réelle', () => {
  const SONDE = 'src/sonde-code-mort.ts'

  afterAll(() => {
    rmSync(SONDE, { force: true })
  })

  it('npm run knip signale un fichier orphelin dans src/', () => {
    writeFileSync(
      SONDE,
      [
        '// Sonde temporaire écrite par code-mort.test.ts, retirée en afterAll.',
        'export function personneNeMAppelle(): string {',
        "  return 'orphelin'",
        '}',
        '',
      ].join('\n'),
      'utf8',
    )

    const r = lancerOutil(['npm', 'run', 'knip'])
    expect(r.code, `npm run knip n'a pas vu le fichier orphelin :\n${r.sortie}`).not.toBe(0)
    // Deux motifs : un code non nul prouve seulement que quelque chose a
    // échoué. Le second nomme la cible, sans quoi l'épreuve resterait verte sur
    // un tout autre signalement — le ruling P10 à l'identique.
    expect(r.sortie).toMatch(/Unused files/)
    expect(r.sortie).toMatch(/sonde-code-mort/)
  })

  it('la sonde est bien retirée entre deux épreuves', () => {
    // Franchit la branche de nettoyage : un fichier orphelin oublié dans `src/`
    // ferait rougir `knip` pour tout le monde, sans rapport avec la cause.
    rmSync(SONDE, { force: true })
    expect(existsSync(SONDE), `La sonde ${SONDE} a survécu à son épreuve.`).toBe(false)
  })
})

// ⚠️ ÉPREUVE INVERSÉE — ne pas la « corriger ».
//
// Elle vérifie que l'exception restante de `knip.json` est ENCORE NÉCESSAIRE.
// Le jour où elle ne le sera plus, cette épreuve rougit et force à la
// retirer. C'est ce qui transforme une liste d'exceptions qu'on n'ose plus
// toucher en une exception qui annonce sa propre fin — D24 demandait une
// condition de sortie, pas une date qu'on repousse.
//
// Mesuré le 20/08/2026, `verify` vert par ailleurs : sans cette ligne,
// `npm run knip` signale `src/core/index.ts` en fichier inutilisé. UNE SEULE des
// deux conditions de sortie inscrites par D24 reste inatteinte :
//   - `src/core/` ne porte toujours aucun module importé par l'application ;
//   - l'exception `ignoreDependencies` sur les deux `@testing-library/*` A ÉTÉ
//     LEVÉE à la tâche 11 du lot 2 : `EcranEtat.test.tsx` et
//     `etats-ecran.test.ts` les emploient réellement, et knip reste vert sans
//     elle. L'échéance datée s'est tenue, elle n'a pas été reconduite.
describe('garde-fou : les exceptions de knip sont encore nécessaires', () => {
  const CONFIG_REEL = 'knip.json'
  const CONFIG_SANS = 'knip.sans-exceptions.json'

  afterAll(() => {
    rmSync(CONFIG_SANS, { force: true })
  })

  it('la cible existe', () => {
    expect(existsSync(CONFIG_REEL), `Cible manquante : ${CONFIG_REEL}`).toBe(true)
  })

  it('retirer la ligne restante fait ENCORE rougir knip, et pour la bonne raison', () => {
    // La configuration d'épreuve est DÉRIVÉE de la vraie, jamais recopiée :
    // deux déclarations divergent, et celle-ci deviendrait une épreuve qui
    // garde une configuration que personne n'utilise — le défaut exact que la
    // famille « configuration réelle » ci-dessus existe pour fermer.
    const brut = readFileSync(CONFIG_REEL, 'utf8')
    const conf: {
      entry: string[]
      ignoreDependencies?: string[]
    } = JSON.parse(brut.replace(/^\s*\/\/.*$/gm, ''))

    const avant = conf.entry.length
    conf.entry = conf.entry.filter((e) => e !== 'src/core/index.ts')
    expect(avant - conf.entry.length, "src/core/index.ts n'est plus dans entry").toBe(1)

    // L'exception `ignoreDependencies` A ÉTÉ RETIRÉE de la configuration réelle
    // à la tâche 11. Cette assertion garde le sens INVERSE de celle qu'elle
    // remplace : si quelqu'un la remettait, il faudrait le savoir, parce que
    // knip est vert sans elle — mesuré le 20/08/2026.
    expect(
      conf.ignoreDependencies,
      'knip.json porte de nouveau `ignoreDependencies`. Elle a été levée à la tâche 11 du ' +
        'lot 2 parce que knip restait vert sans elle : la remettre rendrait aveugle la ' +
        'détection de dépendances mortes, sans que rien ne le signale.',
    ).toBeUndefined()

    writeFileSync(CONFIG_SANS, JSON.stringify(conf, null, 2), 'utf8')
    const r = lancerOutil(['npx', 'knip', '--config', CONFIG_SANS])

    expect(
      r.code,
      'knip est VERT sans son exception : elle ne sert plus, il faut la RETIRER de ' +
        `${CONFIG_REEL} et supprimer cette épreuve.\n${r.sortie}`,
    ).not.toBe(0)
    // Et pour la bonne raison. Sans ce motif, l'épreuve resterait verte sur un
    // tout autre échec de knip — ruling P10.
    expect(r.sortie).toMatch(/src[\\/]core[\\/]index\.ts/)
  })

  it("nomme précisément ce que l'exception laisse passer, et ce qu'elle attrape", () => {
    // D24 écrit « aucun module de core/ n'est surveillé ». MESURÉ le
    // 20/08/2026, c'est plus étroit que cela, et la nuance décide de la
    // gravité :
    //   - un FICHIER orphelin déposé dans `src/core/` est bien signalé ;
    //   - un module RÉEXPORTÉ par `index.ts` que personne n'importe est
    //     entièrement invisible, code 0.
    // C'est donc le tonneau — le motif même autour duquel `core/` est conçu —
    // qui échappe, et non le dossier entier.
    const SONDE_CORE = 'src/core/sonde-reexportee.ts'
    const INDEX = 'src/core/index.ts'
    const indexSauve = readFileSync(INDEX, 'utf8')
    try {
      writeFileSync(SONDE_CORE, 'export const sondeCore = 1\n', 'utf8')
      const seul = lancerOutil(['npm', 'run', 'knip'])
      expect(
        seul.code,
        `Un fichier orphelin de core/ n'est plus signalé :\n${seul.sortie}`,
      ).not.toBe(0)
      expect(seul.sortie).toMatch(/sonde-reexportee/)

      writeFileSync(INDEX, "export * from './sonde-reexportee'\n", 'utf8')
      const reexporte = lancerOutil(['npm', 'run', 'knip'])
      expect(
        reexporte.code,
        'Le tonneau de core/ est désormais surveillé : la condition de sortie de ' +
          `D24 a changé, la reprendre.\n${reexporte.sortie}`,
      ).toBe(0)
    } finally {
      writeFileSync(INDEX, indexSauve, 'utf8')
      rmSync(SONDE_CORE, { force: true })
    }
  })
})
