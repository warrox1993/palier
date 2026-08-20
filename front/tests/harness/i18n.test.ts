// @vitest-environment node
import { describe, expect, it } from 'vitest'
import { existsSync, readFileSync } from 'node:fs'
import i18next, { LANGUES } from '../../src/lib/i18n'

const FR = 'src/locales/fr.json'
const EN = 'src/locales/en.json'

/** Toutes les clés d'un objet imbriqué, à plat : `sante.etat.vide`. Comparer
 * les objets entiers rendrait « les objets diffèrent » ; comparer des chemins
 * permet de NOMMER l'écart, ce qu'exige la seconde assertion de D19. */
function clefsAPlat(objet: unknown, prefixe = ''): string[] {
  if (typeof objet !== 'object' || objet === null) return [prefixe]
  return Object.entries(objet as Record<string, unknown>).flatMap(([clef, valeur]) =>
    clefsAPlat(valeur, prefixe === '' ? clef : `${prefixe}.${clef}`),
  )
}

function charger(chemin: string): unknown {
  return JSON.parse(readFileSync(chemin, 'utf8'))
}

describe('garde-fou : parité des locales', () => {
  it('les deux fichiers de locale existent', () => {
    expect(existsSync(FR), `Cible manquante : ${FR}`).toBe(true)
    expect(existsSync(EN), `Cible manquante : ${EN}`).toBe(true)
  })

  it('les deux catalogues portent rigoureusement les mêmes clés', () => {
    const fr = clefsAPlat(charger(FR)).sort()
    const en = clefsAPlat(charger(EN)).sort()

    // Sans cette borne, deux catalogues VIDES seraient rigoureusement égaux et
    // l'épreuve verte — un vert qui ne contrôle rien.
    expect(fr.length, `${FR} ne porte aucune clé`).toBeGreaterThan(0)

    // Les deux sens, séparément et nommément. Une comparaison à sens unique
    // laisse passer la moitié des écarts, et un `toEqual` sur les tableaux
    // dirait « les tableaux diffèrent » sans dire lequel manque où.
    const manquantesEn = fr.filter((c) => !en.includes(c))
    const manquantesFr = en.filter((c) => !fr.includes(c))
    expect(manquantesEn, `Clés présentes dans ${FR} et absentes de ${EN} :`).toEqual([])
    expect(manquantesFr, `Clés présentes dans ${EN} et absentes de ${FR} :`).toEqual([])
  })

  it('aucune valeur de traduction n’est vide', () => {
    // Une clé présente des deux côtés mais vide passe la parité et n'affiche
    // rien à l'écran : la parité seule ne suffit pas.
    for (const chemin of [FR, EN]) {
      const vides = clefsAPlat(charger(chemin)).filter((clef) => {
        const valeur = clef
          .split('.')
          .reduce<unknown>(
            (n, part) => (n as Record<string, unknown> | undefined)?.[part],
            charger(chemin),
          )
        return typeof valeur !== 'string' || valeur.trim() === ''
      })
      expect(vides, `Clés vides ou non textuelles dans ${chemin} :`).toEqual([])
    }
  })
})

// L'instance est LANCÉE, pas relue. Un `i18n.ts` qui n'initialise rien laisse
// `t('commun.chargement')` rendre la clé brute à l'écran — un défaut visible en
// production et invisible pour une épreuve qui se contenterait de lire le
// fichier.
describe('garde-fou : i18next est réellement câblé', () => {
  it('les deux langues déclarées sont chargées, et le repli est le français', () => {
    expect([...LANGUES]).toEqual(['fr', 'en'])
    expect(i18next.isInitialized, "i18next n'a pas été initialisé").toBe(true)
    expect(i18next.options.fallbackLng).toEqual(['fr'])
  })

  it('une clé se résout dans les deux langues, et diffère entre elles', () => {
    const enFrancais = i18next.getFixedT('fr')('commun.erreur.reessayer')
    const enAnglais = i18next.getFixedT('en')('commun.erreur.reessayer')
    // Une clé non résolue est rendue TELLE QUELLE par i18next : sans cette
    // assertion, un catalogue vide passerait pour un catalogue traduit.
    expect(enFrancais).not.toBe('commun.erreur.reessayer')
    expect(enAnglais).not.toBe('commun.erreur.reessayer')
    expect(enFrancais).not.toBe(enAnglais)
  })

  it('une clé absente se voit, au lieu de rendre une chaîne vide', () => {
    expect(i18next.getFixedT('fr')('commun.clef.qui.nexiste.pas')).toBe(
      'commun.clef.qui.nexiste.pas',
    )
  })
})
