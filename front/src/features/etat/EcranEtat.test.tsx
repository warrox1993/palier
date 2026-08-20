import '@testing-library/jest-dom/vitest'
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import i18next from '../../lib/i18n'
import { EcranEtat } from './EcranEtat'

/**
 * Les QUATRE états de l'écran, et le bouton qui relance l'appel.
 *
 * `tests/harness/etats-ecran.test.ts` garde la RÈGLE — tout écran porte un état
 * vide et un état d'erreur. Ce fichier-ci éprouve CET écran : les deux autres
 * états, les libellés traduits, et le fait que l'action rejoue réellement
 * l'appel au lieu d'afficher un bouton décoratif.
 */

function reponse(corps: unknown, statut = 200) {
  return new Response(JSON.stringify(corps), {
    status: statut,
    headers: { 'content-type': 'application/json' },
  })
}

const SOCLE_PLEIN = { schema: 'S1', isBaseJoignable: true, referentielNutriments: 42 }
const SOCLE_VIDE = { schema: 'S1', isBaseJoignable: true, referentielNutriments: 0 }

beforeEach(() => {
  vi.stubGlobal('fetch', vi.fn())
})

afterEach(() => {
  cleanup()
  vi.unstubAllGlobals()
  vi.restoreAllMocks()
})

describe("l'écran d'état", () => {
  it("montre l'état de CHARGEMENT tant que l'appel n'a pas rendu", () => {
    // La promesse ne se résout jamais : l'état de chargement est OBSERVÉ, pas
    // supposé à partir d'un rendu qui aurait déjà reçu sa réponse.
    vi.mocked(fetch).mockReturnValue(new Promise(() => undefined))

    render(<EcranEtat />)

    expect(document.querySelector('[data-etat="chargement"]')).toBeInTheDocument()
    expect(screen.getByText(i18next.t('commun.chargement'))).toBeInTheDocument()
  })

  it("montre l'état de CONTENU et la valeur rendue par la route", async () => {
    vi.mocked(fetch).mockResolvedValue(reponse(SOCLE_PLEIN))

    render(<EcranEtat />)

    await waitFor(() => {
      expect(document.querySelector('[data-etat="contenu"]')).toBeInTheDocument()
    })
    expect(screen.getByText('42')).toBeInTheDocument()
    expect(screen.getByText('S1')).toBeInTheDocument()
  })

  it("montre l'état VIDE quand le référentiel est à zéro ligne, et NON une erreur", async () => {
    vi.mocked(fetch).mockResolvedValue(reponse(SOCLE_VIDE))

    render(<EcranEtat />)

    await waitFor(() => {
      expect(document.querySelector('[data-etat="vide"]')).toBeInTheDocument()
    })
    // Les deux ensemble : un écran qui crierait « erreur » sur un référentiel
    // vide ferait paniquer pour un état parfaitement normal du lot 2.
    expect(document.querySelector('[data-etat="erreur"]')).not.toBeInTheDocument()
  })

  it("montre l'état d'ERREUR, annoncé aux techniques d'assistance", async () => {
    vi.mocked(fetch).mockRejectedValue(new Error('socle injoignable'))

    render(<EcranEtat />)

    await waitFor(() => {
      expect(document.querySelector('[data-etat="erreur"]')).toBeInTheDocument()
    })
    // `role="alert"` : sans lui, un lecteur d'écran ne dit rien de la panne à
    // qui ne regarde pas la zone concernée.
    expect(screen.getByRole('alert')).toBeInTheDocument()
    expect(screen.getByText(i18next.t('etat.erreur.titre'))).toBeInTheDocument()
  })

  it("l'action REJOUE l'appel, elle n'est pas décorative", async () => {
    vi.mocked(fetch)
      .mockRejectedValueOnce(new Error('socle injoignable'))
      .mockResolvedValue(reponse(SOCLE_PLEIN))

    render(<EcranEtat />)
    await waitFor(() => {
      expect(document.querySelector('[data-etat="erreur"]')).toBeInTheDocument()
    })

    fireEvent.click(screen.getByRole('button', { name: i18next.t('etat.actualiser') }))

    await waitFor(() => {
      expect(document.querySelector('[data-etat="contenu"]')).toBeInTheDocument()
    })
    expect(vi.mocked(fetch)).toHaveBeenCalledTimes(2)
  })
})
