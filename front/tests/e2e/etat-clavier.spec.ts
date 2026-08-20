import AxeBuilder from '@axe-core/playwright'
import { expect, test } from '@playwright/test'

/**
 * D31, LIVRABLE 2 — le parcours AU CLAVIER SEUL sur le premier écran.
 *
 * D31 constatait que « axe-core couvre l'accessibilité statique du DOM ; le
 * parcours au clavier se teste sur un parcours, et il n'y en a aucun ». Il y en
 * a un.
 *
 * Aucun `page.click`, aucun `focus()` : uniquement `keyboard.press`. Une épreuve
 * qui poserait le focus par programme sauterait précisément l'ordre de
 * tabulation qu'elle prétend vérifier — un élément inatteignable au clavier la
 * passerait au vert.
 *
 * L'ÉTAT ATTEINT ICI EST L'ÉTAT D'ERREUR, ET C'EST DÉLIBÉRÉ. Le serveur de
 * prévisualisation ne sert que le paquet du front ; `GET /api/v1/sante` n'y
 * répond pas, l'appel échoue, et l'écran affiche sa panne. C'est donc l'état
 * d'erreur — le plus difficile à obtenir sur un écran ordinaire — qui est
 * parcouru au clavier et passé à axe-core, sans qu'aucune donnée n'ait été
 * simulée.
 */

test.describe("l'écran d'état se parcourt au clavier seul", () => {
  test('le lien d’évitement puis l’action sont atteints par TAB, et l’action répond à ENTRÉE', async ({
    page,
    browserName,
  }) => {
    // Safari n'inclut pas les boutons dans l'ordre de tabulation tant que
    // « Full Keyboard Access » est éteint, et le projet `webkit-mobile` décrit
    // un iPhone, qui n'a pas de clavier physique. Le parcours est donc mesuré
    // sur Chromium ; axe-core, lui, tourne sur les deux (voir plus bas).
    test.skip(browserName !== 'chromium', 'parcours clavier mesuré sur Chromium')

    await page.goto('/')

    // L'écran est arrivé à un état stable : sans cette attente, la tabulation
    // partirait pendant le chargement et mesurerait un autre DOM.
    await expect(page.locator('[data-etat="erreur"]')).toBeVisible()

    // (1) Premier TAB : le lien d'évitement. C'est le premier élément
    // focalisable du document, et son absence obligerait à traverser toute la
    // navigation à chaque page.
    await page.keyboard.press('Tab')
    await expect(page.locator('a:focus')).toHaveAttribute('href', '#contenu')

    // (2) Deuxième TAB : l'action de l'écran. Aucun élément focalisable ne
    // s'intercale.
    await page.keyboard.press('Tab')
    const action = page.locator('button:focus')
    await expect(action).toBeVisible()

    // (3) Le repère de focus est VISIBLE. Un `outline: none` rendrait le
    // parcours possible et inutilisable — et axe-core ne le voit pas.
    const contour = await action.evaluate((element) => {
      const style = globalThis.getComputedStyle(element)
      return `${style.outlineStyle}|${style.outlineWidth}`
    })
    expect(contour, 'le bouton focalisé ne porte aucun contour visible').not.toContain('none')

    // (4) ENTRÉE actionne. L'écran repart en chargement, puis retombe en erreur
    // — la base n'est toujours pas là. Ce qui est prouvé, c'est que la touche
    // DÉCLENCHE quelque chose : un `<div onClick>` ne répondrait pas.
    const requetes: string[] = []
    page.on('request', (requete) => {
      if (requete.url().includes('/api/v1/sante')) requetes.push(requete.url())
    })
    await page.keyboard.press('Enter')
    await expect.poll(() => requetes.length).toBeGreaterThan(0)
  })

  test('axe-core ne trouve aucune violation sur cet écran', async ({ page }) => {
    await page.goto('/')
    await expect(page.locator('[data-etat="erreur"]')).toBeVisible()

    const resultats = await new AxeBuilder({ page })
      .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'wcag22aa'])
      .analyze()

    // Sans cette borne, une analyse qui n'aurait rien parcouru rendrait zéro
    // violation et passerait au vert — ruling P12.
    expect(resultats.passes.length, "axe-core n'a évalué aucune règle").toBeGreaterThan(0)

    const critiques = resultats.violations.filter(
      (v) => v.impact === 'critical' || v.impact === 'serious',
    )
    expect(critiques, JSON.stringify(critiques, null, 2)).toHaveLength(0)
  })
})
