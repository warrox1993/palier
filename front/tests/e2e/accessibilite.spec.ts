import AxeBuilder from '@axe-core/playwright'
import { expect, test } from '@playwright/test'

test("la page d'accueil n'a aucune violation critique", async ({ page }) => {
  await page.goto('/')
  const resultats = await new AxeBuilder({ page })
    .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'wcag22aa'])
    .analyze()
  const critiques = resultats.violations.filter(
    (v) => v.impact === 'critical' || v.impact === 'serious',
  )
  expect(critiques, JSON.stringify(critiques, null, 2)).toHaveLength(0)
})
