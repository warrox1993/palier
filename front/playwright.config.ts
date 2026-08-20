import { defineConfig, devices } from '@playwright/test'

export default defineConfig({
  testDir: './tests/e2e',
  // Les fixtures du harnais sont conçues pour échouer. Sans cette exclusion,
  // `npx playwright test` — ce que lance un développeur ou une extension d'IDE
  // par défaut — rend deux échecs par construction, sur du dépôt sain.
  // Elles restent éprouvées, par `playwright.fixtures.config.ts`.
  testIgnore: /fixture-.*\.spec\.ts/,
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 1 : 0,
  reporter: process.env.CI ? 'github' : 'list',
  use: { baseURL: 'http://localhost:4173', trace: 'on-first-retry' },
  projects: [
    { name: 'chromium', use: { ...devices['Desktop Chrome'] } },
    { name: 'webkit-mobile', use: { ...devices['iPhone 13'] } },
  ],
  webServer: {
    command: 'npm run build && npm run preview',
    url: 'http://localhost:4173',
    reuseExistingServer: !process.env.CI,
    timeout: 120000,
  },
})
