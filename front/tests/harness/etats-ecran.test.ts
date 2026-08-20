import { cleanup, render } from '@testing-library/react'
import { createElement, type ComponentType } from 'react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import '../../src/lib/i18n'

/**
 * D31, LIVRABLE 1 — l'épreuve qui REFUSE un écran dépourvu d'état vide ou d'état
 * d'erreur, et qui NOMME lequel manque.
 *
 * D31 dit d'elle-même : « Si le lot 2 se termine sans ces trois livrables, cette
 * décision a échoué et il faut le dire au lieu de la reconduire. » Ce fichier
 * est le premier des trois.
 *
 * Elle ne lit AUCUN fichier source : elle MONTE chaque écran de `src/features/`
 * dans les conditions qui produisent chaque état, et regarde ce qui est rendu.
 * Un contrôle textuel serait vert sur un composant qui contient le mot
 * « erreur » dans un commentaire.
 *
 * LE CONTRAT : tout écran marque ses états par `data-etat`. C'est le seul point
 * commun exigible d'écrans qui n'ont ni les mêmes données, ni les mêmes
 * libellés, ni les mêmes déclencheurs.
 *
 * Les deux états sont éprouvés SÉPARÉMENT, et c'est le fond de l'affaire : une
 * épreuve qui ne teste que l'un des deux passe au vert le jour où l'autre est
 * retiré.
 */

/** Tous les écrans du dossier `features/`. `import.meta.glob` est résolu par
 * Vite à la compilation : un écran nouveau entre ici sans que personne
 * l'inscrive, et un écran renommé n'y laisse pas d'entrée morte. */
const ECRANS = import.meta.glob(['../../src/features/*/Ecran*.tsx', '!**/*.test.tsx'])

type Module = Record<string, unknown>

function nomDeLEcran(chemin: string): string {
  return chemin.replace(/^.*\/features\//, '').replace(/\/Ecran.*$/, '')
}

function composant(module: Module, chemin: string): ComponentType {
  const exportes = Object.entries(module).filter(([, v]) => typeof v === 'function')
  const premier = exportes[0]
  if (premier === undefined) {
    throw new Error(`${chemin} n'exporte aucun composant.`)
  }
  return premier[1] as ComponentType
}

/** Remplace `fetch` par une réponse maîtrisée. Le corps et l'en-tête sont ceux
 * de la vraie route : un bouchon plus permissif rendrait l'épreuve verte sur un
 * écran qui, en vrai, tomberait en erreur. */
function reponse(corps: unknown, statut = 200) {
  return new Response(JSON.stringify(corps), {
    status: statut,
    headers: { 'content-type': 'application/json' },
  })
}

beforeEach(() => {
  vi.stubGlobal('fetch', vi.fn())
})

afterEach(() => {
  cleanup()
  vi.unstubAllGlobals()
  vi.restoreAllMocks()
})

describe('garde-fou : tout écran porte un état vide et un état d’erreur — D31', () => {
  it('le parcours a trouvé au moins un écran', () => {
    // Quatrième question du franchissement. Sans cette borne, un dossier
    // renommé rendrait un glob vide et les deux épreuves suivantes seraient
    // vertes en n'ayant monté aucun composant.
    expect(
      Object.keys(ECRANS).length,
      'Aucun écran trouvé sous front/src/features/*/Ecran*.tsx : le contrôle n’a plus de cible.',
    ).toBeGreaterThan(0)
  })

  it.each(Object.keys(ECRANS))('%s porte un ÉTAT VIDE', async (chemin) => {
    const charger = ECRANS[chemin]
    if (charger === undefined) throw new Error(`glob incohérent : ${chemin}`)
    const module = (await charger()) as Module

    // Le socle répond, et il est VIDE : schéma migré, référentiel non chargé.
    vi.mocked(fetch).mockResolvedValue(
      reponse({
        schema: '20260820162305_SocleInitial',
        isBaseJoignable: true,
        referentielNutriments: 0,
      }),
    )

    render(createElement(composant(module, chemin)))

    const trouve = await attendre(() => document.querySelector('[data-etat="vide"]'))
    expect(
      trouve,
      `L'écran « ${nomDeLEcran(chemin)} » n'a pas d'ÉTAT VIDE.\n` +
        'Aucun élément ne porte `data-etat="vide"` quand la réponse ne contient rien à ' +
        'afficher. Un écran sans état vide montre une page blanche à un utilisateur qui ' +
        "n'a encore rien saisi — docs/08-workflow.md § 9.",
    ).not.toBeNull()
  })

  it.each(Object.keys(ECRANS))('%s porte un ÉTAT D’ERREUR', async (chemin) => {
    const charger = ECRANS[chemin]
    if (charger === undefined) throw new Error(`glob incohérent : ${chemin}`)
    const module = (await charger()) as Module

    // Le socle ne répond pas. C'est ce que `npm run db:down` produit pour de
    // vrai, et le parcours Playwright le provoque sur le vrai serveur.
    vi.mocked(fetch).mockRejectedValue(new Error('socle injoignable'))

    render(createElement(composant(module, chemin)))

    const trouve = await attendre(() => document.querySelector('[data-etat="erreur"]'))
    expect(
      trouve,
      `L'écran « ${nomDeLEcran(chemin)} » n'a pas d'ÉTAT D'ERREUR.\n` +
        'Aucun élément ne porte `data-etat="erreur"` quand l’appel échoue. Un écran sans ' +
        'état d’erreur reste bloqué sur son chargement, et l’utilisateur ne sait pas ' +
        "si c'est lent ou cassé.",
    ).not.toBeNull()
  })
})

/** Attend qu'un sélecteur apparaisse. `findBy*` de Testing Library ne sait
 * chercher que par rôle, texte ou étiquette ; le contrat porte sur un attribut,
 * et l'attribut est ce qui reste stable quand les libellés changent de langue. */
async function attendre(chercher: () => Element | null, essais = 50): Promise<Element | null> {
  for (let i = 0; i < essais; i += 1) {
    const trouve = chercher()
    if (trouve !== null) return trouve
    await new Promise((suite) => setTimeout(suite, 10))
  }
  return chercher()
}
