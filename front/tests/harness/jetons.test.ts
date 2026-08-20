// @vitest-environment node
import { describe, expect, it } from 'vitest'
import { existsSync, readFileSync } from 'node:fs'
import {
  appliquerJetons,
  couleurs,
  courbe,
  durees,
  espacement,
  jetons,
  rayon,
  tactile,
  tailles,
  variablesCss,
} from '../../src/ui/jetons'

/** Contrôle TRANSVERSE : il compare `src/ui/jetons.ts` au DOCUMENT qui fait
 * autorité, `docs/02-design.md` § 4 et D8. Une valeur recopiée à la main dans
 * deux fichiers diverge ; la divergence se ferme par une lecture. */
const DESIGN = '../docs/02-design.md'
const DECISIONS = '../docs/decisions.md'

const design = existsSync(DESIGN) ? readFileSync(DESIGN, 'utf8') : ''
const decisions = existsSync(DECISIONS) ? readFileSync(DECISIONS, 'utf8') : ''

describe('garde-fou : les jetons ne dérivent pas du document de design', () => {
  it('les deux documents de référence existent', () => {
    expect(existsSync(DESIGN), `Cible manquante : ${DESIGN}`).toBe(true)
    expect(existsSync(DECISIONS), `Cible manquante : ${DECISIONS}`).toBe(true)
  })

  it('les dix couleurs sont exactement celles du § 4', () => {
    // `| `surface-0` | `#14181D` | Fond général… |`
    const lues = new Map<string, string>()
    for (const m of design.matchAll(/^\|\s*`([\w-]+)`\s*\|\s*`(#[0-9A-Fa-f]{6})`\s*\|/gm)) {
      const [, nom, hex] = m
      if (nom === undefined || hex === undefined) continue
      lues.set(nom, hex)
    }
    // Sans cette borne, un tableau que le motif ne lit plus rendrait une carte
    // vide et la boucle suivante ne comparerait rien — ruling P12.
    expect(lues.size, `aucune couleur lue dans ${DESIGN}`).toBe(10)

    for (const [nom, hex] of lues) {
      expect(couleurs[nom as keyof typeof couleurs], `Jeton ${nom} absent ou divergent`).toBe(hex)
    }
    // Et dans l'autre sens : un jeton en trop dans le code est un jeton que le
    // document n'a jamais validé.
    expect(Object.keys(couleurs).sort()).toEqual([...lues.keys()].sort())
  })

  it("l'échelle typographique est celle du § 4", () => {
    const m = /Échelle typographique, ratio 1,25\s*:\s*([\d\s/]+)\./.exec(design)
    expect(m, `échelle typographique introuvable dans ${DESIGN}`).not.toBeNull()
    const lue = (m?.[1] ?? '').split('/').map((n) => Number(n.trim()))
    expect(lue.length, "l'échelle lue est vide").toBe(7)
    expect([...tailles]).toEqual(lue)
  })

  it('les trois durées et la courbe sont celles de D8', () => {
    expect(decisions, 'D8 ne cite plus ses trois durées').toMatch(
      /100 ms[\s\S]{0,120}150 ms[\s\S]{0,120}400 ms/,
    )
    expect([durees.retour, durees.validation, durees.repere]).toEqual([100, 150, 400])
    expect(decisions.includes(courbe), `la courbe ${courbe} n'est plus celle de D8`).toBe(true)
  })

  it('espacement, rayon et zone tactile sont ceux du § 4', () => {
    // Les espaces sont lus par `\s` et non recopiés : le document est écrit en
    // typographie française, où l'espace avant une unité peut être insécable.
    // Un U+00A0 glissé dans ce motif a fait rougir l'épreuve une première fois,
    // en accusant le document alors que le motif était fautif — ruling P22, la
    // forme inverse du faux vert.
    expect(design, 'la base 4 px a changé dans le document').toMatch(/Base\s+4\s*px/)
    expect(espacement).toBe(4)
    expect(design, 'le rayon a changé dans le document').toMatch(/Rayon de bordure \*\*2\s*px\*\*/)
    expect(rayon).toBe(2)
    expect(design, 'la zone tactile a changé dans le document').toMatch(
      /Zone tactile minimale \*\*48\s*×\s*48\s*px\*\*/,
    )
    expect(tactile).toEqual({ minimum: 48, ecart: 8 })
  })

  it('les variables CSS portent chaque couleur, et se posent sur la racine', () => {
    const variables = variablesCss()
    for (const nom of Object.keys(couleurs)) {
      expect(variables[`--couleur-${nom}`], `--couleur-${nom} n'est pas produite`).toBe(
        couleurs[nom as keyof typeof couleurs],
      )
    }
    expect(variables['--courbe']).toBe(courbe)

    // `appliquerJetons` est LANCÉE, pas relue : une fonction qu'aucune épreuve
    // n'appelle est une branche qui ment. Le faux élément suffit — c'est
    // `setProperty` qu'on vérifie, pas le navigateur.
    const posees = new Map<string, string>()
    const faux = { style: { setProperty: (n: string, v: string) => posees.set(n, v) } }
    appliquerJetons(faux as unknown as HTMLElement)
    expect(posees.size).toBe(Object.keys(variables).length)
    expect(posees.get('--rayon')).toBe('2px')
  })

  it("l'agrégat expose les mêmes valeurs que les jetons pris un à un", () => {
    expect(jetons.couleurs).toBe(couleurs)
    expect(jetons.courbe).toBe(courbe)
    expect(jetons.espacement).toBe(espacement)
  })
})
