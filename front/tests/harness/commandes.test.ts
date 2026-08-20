// @vitest-environment node
import { readFileSync } from 'node:fs'
import { describe, expect, it } from 'vitest'
import { lancerOutil } from './run-outil'

/**
 * Découpe une ligne de commande npm en arguments, guillemets respectés.
 * `--exclude "tests/harness/accessibilite.test.ts"` doit rester UN argument :
 * découpé sur les espaces, le motif partirait en deux et la collecte
 * silencieusement changerait de sens.
 */
function decouperArguments(ligne: string): string[] {
  const args: string[] = []
  const motif = /"([^"]*)"|'([^']*)'|(\S+)/g
  let trouve: RegExpExecArray | null = motif.exec(ligne)
  while (trouve !== null) {
    args.push(trouve[1] ?? trouve[2] ?? trouve[3] ?? '')
    trouve = motif.exec(ligne)
  }
  return args
}

/**
 * Rend la liste des fichiers que le script npm nommé collecterait — sans les
 * exécuter. Les arguments sont lus dans `package.json`, pas recopiés ici : une
 * copie serait une seconde source de vérité, donc une divergence en attente.
 */
function fichiersCollectes(nomScript: string): string {
  const pkg = JSON.parse(readFileSync('package.json', 'utf8')) as {
    scripts: Record<string, string | undefined>
  }
  const script = pkg.scripts[nomScript]
  expect(script, `Script absent de front/package.json : ${nomScript}`).toBeTypeOf('string')
  expect(script, `${nomScript} n'est plus un lancement de Vitest`).toMatch(/^vitest run\b/)
  const args = decouperArguments((script ?? '').replace(/^vitest run\s*/, ''))
  const r = lancerOutil(['npx', 'vitest', 'list', '--filesOnly', ...args])
  expect(r.code, `La collecte de ${nomScript} échoue :\n${r.sortie}`).toBe(0)
  return r.sortie
}

// Aucune autre épreuve de cette suite ne lance un script npm : toutes appellent
// `npx <outil>` directement. Elles prouvent donc que les OUTILS refusent, jamais
// que les COMMANDES du projet refusent — or ce sont les scripts que le hook de
// pré-commit, `verify` et la CI exécutent réellement.
//
// Ce trou a laissé passer deux régressions, trouvées seulement en revue :
// un script `lint` réécrit sans `--ignore-pattern` (les fixtures faisaient
// rougir le lint du code sain), et un `test:harness` dont le drapeau
// `--exclude ''` ne réactivait pas ce qu'il prétendait réactiver.
describe('garde-fou : les commandes du projet, pas seulement les outils', () => {
  it('npm run lint est vert sur le code réel', () => {
    const r = lancerOutil(['npm', '--prefix', 'front', 'run', 'lint'], { cwd: '..' })
    expect(r.code, `Le script lint échoue sur le code sain :\n${r.sortie}`).toBe(0)
  })

  it('npm run typecheck est vert sur le code réel', () => {
    const r = lancerOutil(['npm', '--prefix', 'front', 'run', 'typecheck'], { cwd: '..' })
    expect(r.code, `Le script typecheck échoue sur le code sain :\n${r.sortie}`).toBe(0)
  })

  it('les fixtures restent visibles de leurs propres épreuves', () => {
    // Le pendant du test précédent. Si l'exclusion migrait de la ligne de
    // commande vers `ignorePatterns`, le lint resterait vert ET les fixtures
    // deviendraient invisibles : les épreuves passeraient au vert en ne
    // contrôlant rien. Ce test le rend impossible.
    const r = lancerOutil(['npx', 'oxlint', 'tests/harness/fixtures/any-explicite.ts'])
    expect(r.code, `La fixture est devenue invisible d'Oxlint :\n${r.sortie}`).not.toBe(0)
    expect(r.sortie).toContain('no-explicit-any')
  })

  it('test:harness voit toutes les épreuves, y compris celle d’accessibilité', () => {
    // La collecte des deux scripts est comparée dans les deux sens. Le drapeau
    // `--exclude` de Vitest AJOUTE aux globs de la configuration : un
    // `--exclude ''` mal formé laisse `test` vert tout en collectant ce qu'il
    // prétend écarter, et à l'inverse une exclusion migrée dans
    // `vitest.config.ts` rendrait l'épreuve d'accessibilité inatteignable de
    // `test:harness` — qui existe précisément pour la lancer.
    const harness = fichiersCollectes('test:harness')
    expect(harness, "l'épreuve d'accessibilité est exclue de test:harness").toContain(
      'accessibilite',
    )

    const normal = fichiersCollectes('test')
    expect(normal, "l'épreuve d'accessibilité est retombée dans `test`").not.toContain(
      'accessibilite',
    )
    // Sans cette assertion, une collecte vide passerait au vert : `test`
    // n'aurait plus rien à exclure parce qu'il n'aurait plus rien du tout.
    expect(normal, 'la collecte de `test` est vide : elle ne contrôle plus rien').toContain(
      '.test.ts',
    )
  })

  it('les scripts de la racine sont lintés et formatés', () => {
    // `scripts/*.mjs` porte les règles bloquantes du projet — `regles-projet.mjs`,
    // `verifier-licences.mjs`, `verify.mjs`. Oxlint et Prettier vivent dans
    // `front/` et ne remontent pas d'un cran : ces fichiers étaient les moins
    // surveillés du dépôt et parmi les plus critiques.
    const lint = lancerOutil(['npm', 'run', 'lint:scripts'], { cwd: '..' })
    expect(lint.code, `lint:scripts échoue :\n${lint.sortie}`).toBe(0)
    // Seconde assertion (P8) : un code 0 ne prouve pas que l'outil a regardé
    // quelque chose. Mesuré le 20/08/2026 sur une cible absente — oxlint rend
    // « No files found to lint », prettier rend « No files matching the pattern
    // were found » SUIVI de « All matched files use Prettier code style! ».
    // Le jour où le motif de la commande cesse d'atteindre `scripts/`, c'est
    // cette assertion qui le dit, pas le code de sortie.
    expect(lint.sortie, "Oxlint n'a inspecté aucun fichier de scripts/").not.toMatch(
      /No files found to lint/i,
    )

    const format = lancerOutil(['npm', 'run', 'format:scripts'], { cwd: '..' })
    expect(format.code, `format:scripts échoue :\n${format.sortie}`).toBe(0)
    expect(format.sortie, "Prettier n'a inspecté aucun fichier de scripts/").not.toMatch(
      /No files matching the pattern/i,
    )
  })
})
