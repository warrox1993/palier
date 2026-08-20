// @vitest-environment node
import { afterAll, describe, expect, it } from 'vitest'
import { cpSync, existsSync, mkdirSync, mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join, resolve } from 'node:path'
import { lancerOutil } from './run-outil'

const SCRIPT = '../scripts/regles-projet.mjs'

/** Le script résout ses cibles depuis la racine du dépôt, jamais depuis le
 * répertoire courant : ses arguments `--fichier` portent donc le préfixe
 * `front/` alors que ces épreuves tournent depuis `front/`. */
const dansLaRacine = (nom: string) => `front/tests/harness/fixtures/${nom}`

const FIXTURES = [
  'calcul-conformite.ts',
  'couleur-en-dur.ts',
  'chaine-en-dur.tsx',
  'chaine-en-dur-multiligne.tsx',
  'attribut-copie.tsx',
  'litteral-jsx.tsx',
  'comparaison-numerique.tsx',
  'chaine-en-commentaire.tsx',
  'ombre-portee.css',
  'fleche-unicode.tsx',
  'booleen-mal-nomme.ts',
  'type-prefixe-i.ts',
]

function surFixture(nom: string) {
  return lancerOutil(['node', SCRIPT, '--fichier', dansLaRacine(nom)])
}

describe('garde-fou : règles de projet', () => {
  it.each(FIXTURES)('la fixture %s existe', (f) => {
    const chemin = `tests/harness/fixtures/${f}`
    expect(existsSync(chemin), `Cible manquante : ${chemin}`).toBe(true)
  })

  it('accepte le code réel du front', () => {
    const r = lancerOutil(['node', SCRIPT])
    expect(r.code, `Le front réel viole une règle de projet :\n${r.sortie}`).toBe(0)
    expect(r.sortie).toMatch(/aucune violation des règles de projet/)
  })

  it('refuse un calcul de conformité dans le front', () => {
    const r = surFixture('calcul-conformite.ts')
    expect(r.code, `Le calcul de conformité a été accepté :\n${r.sortie}`).not.toBe(0)
    expect(r.sortie).toMatch(/Palier\.Domain/)
  })

  it('refuse une couleur hexadécimale hors jetons', () => {
    const r = surFixture('couleur-en-dur.ts')
    expect(r.code, `La couleur a été acceptée :\n${r.sortie}`).not.toBe(0)
    expect(r.sortie).toMatch(/jeton/i)
  })

  it('refuse une ombre portée', () => {
    const r = surFixture('ombre-portee.css')
    expect(r.code, `L'ombre portée a été acceptée :\n${r.sortie}`).not.toBe(0)
    expect(r.sortie).toMatch(/ombre-ou-degrade/)
  })

  it('refuse une flèche Unicode dans un libellé', () => {
    const r = surFixture('fleche-unicode.tsx')
    expect(r.code, `La flèche a été acceptée :\n${r.sortie}`).not.toBe(0)
    expect(r.sortie).toMatch(/fleche-unicode/)
  })

  it('refuse un booléen sans préfixe is, has, can ou should', () => {
    const r = surFixture('booleen-mal-nomme.ts')
    expect(r.code, `Le booléen mal nommé a été accepté :\n${r.sortie}`).not.toBe(0)
    expect(r.sortie).toMatch(/booleen-mal-nomme/)
  })

  it('refuse un type préfixé par I', () => {
    const r = surFixture('type-prefixe-i.ts')
    expect(r.code, `Le préfixe I a été accepté :\n${r.sortie}`).not.toBe(0)
    expect(r.sortie).toMatch(/type-prefixe-i/)
  })
})

// Cette famille existe parce que la règle, appliquée ligne à ligne, était muette
// sur tout composant réel : Prettier coupe `<button …>Texte</button>` en trois
// lignes dès 100 colonnes, et aucune des trois ne portait plus la violation.
// Elle criait en revanche sur `valeur < 10`, où aucune chaîne n'existe.
describe('garde-fou : chaînes de texte en dur', () => {
  it('refuse une chaîne en dur sur une seule ligne', () => {
    const r = surFixture('chaine-en-dur.tsx')
    expect(r.code, `La chaîne en dur a été acceptée :\n${r.sortie}`).not.toBe(0)
    expect(r.sortie).toMatch(/i18n/i)
  })

  it('refuse un nœud de texte JSX coupé sur plusieurs lignes par Prettier', () => {
    const r = surFixture('chaine-en-dur-multiligne.tsx')
    expect(r.code, `Le texte multiligne a été accepté :\n${r.sortie}`).not.toBe(0)
    expect(r.sortie).toMatch(/nœud de texte JSX « Enregistrer la séance »/)
    // Le numéro de ligne est ce qui rend le signalement actionnable : la
    // violation est sur la ligne 7 du fichier, pas sur celle de la balise.
    expect(r.sortie).toMatch(/chaine-en-dur-multiligne\.tsx:7/)
  })

  it('refuse les quatre attributs qui portent de la copie', () => {
    const r = surFixture('attribut-copie.tsx')
    expect(r.code, `Les attributs de copie ont été acceptés :\n${r.sortie}`).not.toBe(0)
    expect(r.sortie).toMatch(/4 violation\(s\)/)
    expect(r.sortie).toMatch(/attribut placeholder="Poids en kilogrammes"/)
    expect(r.sortie).toMatch(/attribut aria-label="Poids du jour"/)
    expect(r.sortie).toMatch(/attribut title="Saisir le poids de la séance"/)
    expect(r.sortie).toMatch(/attribut alt="Graphique de progression"/)
  })

  it('refuse une chaîne littérale glissée dans une accolade JSX', () => {
    const r = surFixture('litteral-jsx.tsx')
    expect(r.code, `Le littéral JSX a été accepté :\n${r.sortie}`).not.toBe(0)
    expect(r.sortie).toMatch(/littéral dans une accolade JSX/)
  })

  // Non-régression. `const estBas = (valeur: number) => valeur < 10` déclenchait
  // l'ancienne règle : un « > », des lettres, un « < ». Une règle qui crie où il
  // ne faut pas est une règle qu'on cesse de lire.
  it('accepte une comparaison numérique, qui ne contient aucune chaîne', () => {
    const r = surFixture('comparaison-numerique.tsx')
    expect(r.code, `Une comparaison numérique a été refusée :\n${r.sortie}`).toBe(0)
    expect(r.sortie).toMatch(/aucune violation des règles de projet/)
  })

  it("accepte de la copie qui vit dans un commentaire, puisqu'elle n'atteint pas l'écran", () => {
    const r = surFixture('chaine-en-commentaire.tsx')
    expect(r.code, `Un commentaire a été refusé :\n${r.sortie}`).toBe(0)
    expect(r.sortie).toMatch(/aucune violation des règles de projet/)
  })
})

// Un contrôle qui plante rend le même silence qu'un contrôle qui approuve — et
// pire, avec le code 1 il rend le même verdict qu'une violation. Les deux
// pannes ci-dessous sortaient en 1 avec une pile Node : `verify` aurait annoncé
// « violation des règles de projet » alors qu'aucun fichier n'avait été lu.
describe('garde-fou : le contrôle crie quand il ne peut pas tourner', () => {
  let arbreNu = ''

  afterAll(() => {
    if (arbreNu !== '') rmSync(arbreNu, { recursive: true, force: true })
  })

  it('refuse proprement --fichier sans valeur, avec un code distinct des violations', () => {
    const r = lancerOutil(['node', SCRIPT, '--fichier'])
    expect(r.code, `--fichier sans valeur n'a pas été refusé :\n${r.sortie}`).toBe(2)
    expect(r.sortie).toMatch(/--fichier employée sans valeur/)
  })

  it('refuse proprement un fichier inexistant', () => {
    const r = lancerOutil(['node', SCRIPT, '--fichier', 'front/src/inexistant.tsx'])
    expect(r.code, `Un fichier inexistant n'a pas été refusé :\n${r.sortie}`).toBe(2)
    expect(r.sortie).toMatch(/fichier illisible/)
  })

  it('crie quand il n’a plus de cible : arborescence sans front/src', () => {
    arbreNu = mkdtempSync(join(tmpdir(), 'regles-projet-'))
    mkdirSync(join(arbreNu, 'scripts'))
    cpSync(resolve(SCRIPT), join(arbreNu, 'scripts', 'regles-projet.mjs'))

    const r = lancerOutil(['node', join(arbreNu, 'scripts', 'regles-projet.mjs')])
    expect(r.code, `Une arborescence sans front/src n'a pas été signalée :\n${r.sortie}`).toBe(2)
    expect(r.sortie).toMatch(/répertoire introuvable/)
    expect(r.sortie).not.toMatch(/ENOENT/)
  })
})
