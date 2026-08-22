import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { AuthentificationRequiseError, lireEtatDuSocle, SocleInjoignableError } from './etat'

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

  it("distingue un REFUS D'AUTHENTIFICATION d'une base injoignable", async () => {
    // D41 a ferme `/api/v1/sante` derriere l'authentification. Sans cette
    // distinction, l'ecran envoie le developpeur relancer un conteneur qui
    // tourne deja -- mesure le 22/08/2026, message a l'appui.
    vi.mocked(fetch).mockResolvedValue(reponse({}, 401))

    await expect(lireEtatDuSocle()).rejects.toBeInstanceOf(AuthentificationRequiseError)
  })

  it('traite un 403 comme le meme refus : le droit manque, pas la base', async () => {
    vi.mocked(fetch).mockResolvedValue(reponse({}, 403))

    await expect(lireEtatDuSocle()).rejects.toBeInstanceOf(AuthentificationRequiseError)
  })

  it("garde un 503 du cote de la base : c'est bien elle qui ne repond pas", async () => {
    // La borne. Sans elle, faire de TOUT echec un refus d'authentification
    // passerait les deux epreuves ci-dessus sans rien distinguer.
    vi.mocked(fetch).mockResolvedValue(reponse({}, 503))

    const faute = await lireEtatDuSocle().catch((raison: unknown) => raison)

    expect(faute).toBeInstanceOf(SocleInjoignableError)
    expect(faute).not.toBeInstanceOf(AuthentificationRequiseError)
  })
})
