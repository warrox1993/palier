import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { lireEtatDuSocle, SocleInjoignableError } from './etat'

/**
 * Le module d'accès, éprouvé SANS React. Les trois formes de réponse qui ne
 * doivent pas passer pour un état sont ici, et pas dans le composant : un
 * module pur se teste plus vite et plus précisément qu'un rendu.
 */

function reponse(corps: unknown, statut = 200, type = 'application/json') {
  return new Response(JSON.stringify(corps), {
    status: statut,
    headers: { 'content-type': type },
  })
}

beforeEach(() => {
  vi.stubGlobal('fetch', vi.fn())
})

afterEach(() => {
  vi.unstubAllGlobals()
  vi.restoreAllMocks()
})

describe("lecture de l'état du socle", () => {
  it('rend les trois champs quand la route répond', async () => {
    vi.mocked(fetch).mockResolvedValue(
      reponse({ schema: 'S1', isBaseJoignable: true, referentielNutriments: 42 }),
    )

    const etat = await lireEtatDuSocle()

    expect(etat.schema).toBe('S1')
    expect(etat.isBaseJoignable).toBe(true)
    expect(etat.referentielNutriments).toBe(42)
  })

  it('refuse un 503 — la base ne répond pas', async () => {
    vi.mocked(fetch).mockResolvedValue(
      reponse({ schema: '', isBaseJoignable: false, referentielNutriments: 0 }, 503),
    )

    await expect(lireEtatDuSocle()).rejects.toBeInstanceOf(SocleInjoignableError)
  })

  it('refuse une page HTML rendue en 200 — le piège du serveur de prévisualisation', () => {
    // MESURÉ : `vite preview` rend `index.html` avec un code 200 sur toute route
    // inconnue. Sans le contrôle du type de contenu, l'écran croirait avoir reçu
    // un état — et un `JSON.parse` d'HTML lèverait une erreur qui ne nommerait
    // rien.
    vi.mocked(fetch).mockResolvedValue(
      new Response('<!doctype html><html lang="fr"></html>', {
        status: 200,
        headers: { 'content-type': 'text/html' },
      }),
    )

    return expect(lireEtatDuSocle()).rejects.toBeInstanceOf(SocleInjoignableError)
  })

  it('refuse un corps JSON auquel il manque un champ', async () => {
    vi.mocked(fetch).mockResolvedValue(reponse({ schema: 'S1' }))

    await expect(lireEtatDuSocle()).rejects.toBeInstanceOf(SocleInjoignableError)
  })
})
