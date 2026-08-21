// @vitest-environment node
import { spawnSync } from 'node:child_process'
import { existsSync, readFileSync, rmSync, writeFileSync } from 'node:fs'
import { extname } from 'node:path'
import { describe, expect, it } from 'vitest'

const DOSSIER = 'back/Palier.Domain/'

// Les valeurs de référence de l'EFSA en vigueur au 21/08/2026, dans les unités
// où elles sont publiées. Une constante qui porterait l'une d'elles à côté d'un
// nom de nutriment figerait dans du code compilé une valeur qui bouge : la
// limite de la vitamine B6 est passée de 25 à 12 mg en 2023, celle du sélénium
// de 300 à 255 µg, et le fer a perdu la sienne au profit d'un simple niveau sûr
// d'apport. Un produit qui aurait figé la B6 à 25 mg laisserait passer sans
// rien dire un apport de 20 mg, soit 167 % de la limite en vigueur.
const VALEURS_DE_REFERENCE = [
  '12', // vitamine B6, mg
  '25', // zinc, mg
  '40', // fer, mg — niveau sûr d'apport
  '100', // vitamine D, µg
  '250', // magnésium, mg
  '255', // sélénium, µg
  '600', // iode, µg
  '900', // nicotinamide, mg
  '1000', // folates, µg
  '2500', // calcium, mg
  '3000', // vitamine A préformée, µg ER
]

const NOMS_DE_NUTRIMENTS =
  /zinc|selenium|s[ée]l[ée]nium|iron|magnesium|magn[ée]sium|iode|iodine|calcium|cuivre|copper|folate|niacin|nicotinamide|vitamine?[ _]?[abcde]\d?|b6|manganese|mangan[èe]se|dha/i

// Le `\b` initial d'une première version rendait ce motif aveugle à la forme
// `=> 25m` : une limite de mot exige une transition entre caractère de mot et
// non-mot, et `=` n'est pas un caractère de mot. La violation la plus naturelle
// à écrire en C# moderne passait donc au travers, pendant que l'épreuve de
// franchissement, qui n'essayait que la forme `const`, restait verte. Mesuré le
// 21/08/2026.
const DECLARATION = /(?:\bconst\b|\bstatic\s+readonly\b|=>)\s*[^;]*?(\d+(?:\.\d+)?)m\b/

/**
 * Les fichiers que git SUIT sous `back/Palier.Domain/`, et non ceux que le
 * disque porte. Un parcours du disque compterait comme faisant partie du
 * domaine toute copie locale — worktree, sauvegarde, clone — et la liste
 * d'exclusions qui tenterait de les énumérer courrait après le `.gitignore`
 * sans jamais le rattraper. C'est ce qui est arrivé le 21/08/2026 à
 * `tests-harness/db.test.mjs`, qui s'est vu lui-même dans un worktree ; la
 * correction est appliquée ici d'emblée plutôt que réapprise.
 */
function sourcesDuDomaine() {
  const git = spawnSync('git', ['ls-files', '-z', DOSSIER], { encoding: 'utf8' })

  if (git.status !== 0) {
    throw new Error(
      `git ls-files a rendu ${git.status} : ${(git.stderr ?? '').trim() || '(rien sur stderr)'}. ` +
        "Ce contrôle lit l'index git ; privé de dépôt il n'a plus de cible, et il le dit.",
    )
  }

  return git.stdout
    .split('\0')
    .filter(Boolean)
    .filter((f) => extname(f) === '.cs')
}

/**
 * Les déclarations portant une valeur de référence sanitaire. On ne cherche pas
 * le nombre seul — `30m` apparaît partout — mais la conjonction d'une valeur
 * publiée et d'un nom de nutriment sur la même déclaration.
 */
function constantesSuspectes(source) {
  const suspectes = []

  for (const ligne of source.split('\n')) {
    // Les commentaires citent ces valeurs pour expliquer la règle : c'est leur
    // rôle, et les confondre avec du code rendrait ce garde-fou inutilisable.
    const nue = ligne.trim()
    if (nue.startsWith('//') || nue.startsWith('*')) continue

    const declaration = DECLARATION.exec(ligne)
    if (!declaration) continue
    if (!NOMS_DE_NUTRIMENTS.test(ligne)) continue
    if (!VALEURS_DE_REFERENCE.includes(declaration[1])) continue

    suspectes.push(nue)
  }

  return suspectes
}

function violationsDuDomaine() {
  return sourcesDuDomaine().flatMap((f) =>
    constantesSuspectes(readFileSync(f, 'utf8')).map((ligne) => `${f} → ${ligne}`),
  )
}

describe('garde-fou : aucune valeur de référence en dur dans le domaine', () => {
  it('la cible existe', () => {
    expect(existsSync(DOSSIER), `Cible manquante : ${DOSSIER}`).toBe(true)
  })

  it("le parcours lit l'index git et y trouve des sources", () => {
    // Sans cette assertion, un parcours cassé rendrait une liste vide et
    // l'épreuve suivante serait verte en ne lisant rien.
    expect(
      sourcesDuDomaine().length,
      "le parcours du domaine n'a lu aucun fichier",
    ).toBeGreaterThan(10)
  })

  it("aucune limite haute ni niveau sûr n'est écrit dans Palier.Domain", () => {
    expect(
      violationsDuDomaine(),
      'Les valeurs de référence vivent en base, versionnées et datées. ' +
        "docs/04-nutrition.md § 3 : « il est formellement interdit d'en inventer un ».",
    ).toEqual([])
  })

  // Épreuve de franchissement, sur un fichier RÉEL et non sur le motif seul.
  // Les deux formes sont éprouvées : une première version du détecteur voyait
  // `const` et manquait `=>`, et le test qui n'essayait que `const` restait
  // vert pendant que la violation la plus naturelle passait.
  it.each([
    ['const', '    public const decimal ZincUl = 25m;'],
    ['propriété expression', '    public static decimal ZincUlMg => 25m;'],
    ['static readonly', '    public static readonly decimal SeleniumUl = 255m;'],
  ])('refuse une valeur de référence écrite en %s', (_forme, ligne) => {
    const source = `namespace Palier.Domain.Nutriments;\n\npublic static class T\n{\n${ligne}\n}\n`
    const trouvees = constantesSuspectes(source)

    expect(trouvees.length, `la violation « ${ligne.trim()} » doit être vue`).toBe(1)
    expect(trouvees[0]).toContain('25')
  })

  it('laisse passer un commentaire qui cite la valeur', () => {
    const source = '    /// <item>zinc 25 mg, vitamine B6 12 mg depuis 2023</item>\n'
    expect(constantesSuspectes(source)).toEqual([])
  })

  it("laisse passer une valeur qui n'est pas une référence sanitaire", () => {
    const source = '    public static decimal EauMlParKg => 35m;\n'
    expect(constantesSuspectes(source)).toEqual([])
  })

  // Le détecteur écrit une violation dans le domaine RÉEL, la fait voir à git,
  // et vérifie que le contrôle complet rougit. Provoquer l'état plutôt que le
  // simuler : c'est la seule branche qui se déclenchera le jour venu.
  it('refuse une violation réellement posée dans le domaine', () => {
    const temoin = 'back/Palier.Domain/Nutriments/TemoinDeFranchissement.cs'

    writeFileSync(
      temoin,
      'namespace Palier.Domain.Nutriments;\n\n' +
        'public static class TemoinDeFranchissement\n{\n' +
        '    public static decimal ZincUlMg => 25m;\n}\n',
    )

    try {
      const ajout = spawnSync('git', ['add', '-N', temoin], { encoding: 'utf8' })
      expect(ajout.status, `git add a rendu ${ajout.status} : ${ajout.stderr}`).toBe(0)
      expect(sourcesDuDomaine()).toContain(temoin)

      const violations = violationsDuDomaine()
      expect(violations.length, 'le garde-fou doit voir la violation posée').toBe(1)
      expect(violations[0]).toContain('TemoinDeFranchissement')
    } finally {
      spawnSync('git', ['rm', '--cached', '--quiet', temoin], { encoding: 'utf8' })
      rmSync(temoin, { force: true })
    }
  })
})
