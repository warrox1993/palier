import { defineConfig, devices } from '@playwright/test'

// Configuration dédiée aux fixtures du harnais — les specs qui DOIVENT échouer.
// `playwright.config.ts` les écarte par `testIgnore`, sans quoi `npx playwright
// test`, la commande par défaut d'un développeur ou d'une extension d'IDE, rend
// deux échecs par construction. Même raisonnement que `knip.fixtures.json`.
//
// Ne PAS chercher à réactiver la fixture depuis la configuration principale en
// la nommant en ligne de commande : l'argument positionnel de Playwright filtre
// la liste déjà collectée, il ne rouvre pas ce que `testIgnore` a retiré.
// Rulings P6 et P9 — tout outil qui a une exclusion a ce piège.
//
// Aucun `webServer` ici : la fixture charge un fichier local par `file://`, la
// démarrer coûterait un build complet pour rien.
export default defineConfig({
  testDir: './tests/e2e',
  testMatch: /fixture-.*\.spec\.ts/,
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  reporter: 'list',
  use: { trace: 'off' },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
})
