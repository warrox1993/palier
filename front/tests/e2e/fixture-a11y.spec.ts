import AxeBuilder from '@axe-core/playwright'
import { expect, test } from '@playwright/test'
import { pathToFileURL } from 'node:url'
import { resolve } from 'node:path'

test('la fixture accessible doit être refusée par axe-core', async ({ page }) => {
  const chemin = pathToFileURL(resolve('tests/harness/fixtures/bouton-sans-nom.html')).href
  await page.goto(chemin)
  const resultats = await new AxeBuilder({ page }).analyze()
  expect(resultats.violations).toHaveLength(0)
})
