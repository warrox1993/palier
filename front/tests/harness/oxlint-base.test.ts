// @vitest-environment node
import { existsSync } from 'node:fs'
import { describe, expect, it } from 'vitest'
import { lancerOutil } from './run-outil'

const ANY = 'tests/harness/fixtures/any-explicite.ts'
const NOMMAGE = 'tests/harness/fixtures/nommage-Invalide.ts'
const CONSOLE = 'tests/harness/fixtures/console-interdit.ts'
const CYCLE_A = 'tests/harness/fixtures/cycle-a.ts'
const CYCLE_B = 'tests/harness/fixtures/cycle-b.ts'
const SANS_ALT = 'tests/harness/fixtures/image-sans-alt.tsx'

describe('garde-fou : Oxlint, règles syntaxiques', () => {
  it.each([ANY, NOMMAGE, CONSOLE, CYCLE_A, CYCLE_B, SANS_ALT])('la fixture %s existe', (f) => {
    expect(existsSync(f), `Cible manquante : ${f}`).toBe(true)
  })

  it('refuse un any explicite', () => {
    const r = lancerOutil(['npx', 'oxlint', ANY])
    expect(r.code, `Oxlint a accepté le any :\n${r.sortie}`).not.toBe(0)
    expect(r.sortie).toContain('no-explicit-any')
  })

  it('refuse un nom de fichier hors kebab-case et PascalCase', () => {
    const r = lancerOutil(['npx', 'oxlint', NOMMAGE])
    expect(r.code, `Oxlint a accepté le nommage :\n${r.sortie}`).not.toBe(0)
    expect(r.sortie).toMatch(/filename-case/)
  })

  // Les trois épreuves qui suivent existent parce qu'une mesure différentielle
  // a montré que ces règles étaient supprimables sans qu'aucun test ne bouge :
  // avec un bloc `rules` vide, seul `no-unused-vars` subsistait (porté par
  // `categories.correctness`). Une règle qui ne tient qu'à sa ligne de
  // configuration, et dont la suppression est indétectable, ne protège rien.
  it('refuse un appel à la console', () => {
    const r = lancerOutil(['npx', 'oxlint', CONSOLE])
    expect(r.code, `Oxlint a accepté le console.log :\n${r.sortie}`).not.toBe(0)
    expect(r.sortie).toMatch(/no-console/)
  })

  it('refuse un cycle d’import', () => {
    const r = lancerOutil(['npx', 'oxlint', CYCLE_A, CYCLE_B])
    expect(r.code, `Oxlint a accepté le cycle :\n${r.sortie}`).not.toBe(0)
    expect(r.sortie).toMatch(/no-cycle/)
  })

  // Celle-ci surveille le tableau `plugins`, pas le bloc `rules` : les règles
  // d'accessibilité statique arrivent par `plugins` + `categories.correctness`,
  // sans être nommées nulle part. Retirer `"jsx-a11y"` du tableau les
  // supprimerait toutes sans un signal.
  it('refuse une image sans texte alternatif', () => {
    const r = lancerOutil(['npx', 'oxlint', SANS_ALT])
    expect(r.code, `Oxlint a accepté l’image sans alt :\n${r.sortie}`).not.toBe(0)
    expect(r.sortie).toMatch(/alt-text/)
  })
})
