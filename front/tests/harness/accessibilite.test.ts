// @vitest-environment node
import { existsSync } from 'node:fs'
import { describe, expect, it } from 'vitest'
import { lancerOutil } from './run-outil'

const FIXTURE = 'tests/harness/fixtures/bouton-sans-nom.html'

describe('garde-fou : accessibilité', () => {
  it('la fixture de violation existe', () => {
    expect(existsSync(FIXTURE), `Cible manquante : ${FIXTURE}`).toBe(true)
  })

  // La fixture est écartée de `playwright.config.ts` par `testIgnore`, sinon
  // `npx playwright test` rend deux échecs par construction. La nommer en ligne
  // de commande ne suffirait PAS à la rouvrir : l'argument positionnel filtre la
  // liste déjà collectée. Elle vit donc sous sa propre configuration, comme
  // `knip.fixtures.json` — rulings P6 et P9.
  it('axe-core détecte un bouton sans nom accessible', () => {
    const r = lancerOutil(['npx', 'playwright', 'test', '--config=playwright.fixtures.config.ts'])
    expect(r.code, `axe-core n'a pas vu la violation :\n${r.sortie}`).not.toBe(0)
    expect(r.sortie).toMatch(/button-name|violation/i)
    // Sans ce second motif, l'épreuve resterait verte si Playwright ne trouvait
    // aucun test : « No tests found » sort aussi en code non nul.
    expect(r.sortie).toMatch(/fixture-a11y/)
  })
})
