import { defineConfig } from 'vitest/config'
import react from '@vitejs/plugin-react'
import { fileURLToPath, URL } from 'node:url'

export default defineConfig({
  plugins: [react()],
  resolve: {
    alias: { '@': fileURLToPath(new URL('./src', import.meta.url)) },
  },
  test: {
    environment: 'jsdom',
    globals: true,
    include: ['src/**/*.test.{ts,tsx}', 'tests/harness/**/*.test.ts'],
    // L'epreuve d'accessibilite lance Playwright : elle n'appartient qu'a
    // test:harness, execute par le job de CI qui installe les navigateurs.
    exclude: ['**/node_modules/**', 'tests/harness/accessibilite.test.ts'],
    testTimeout: 60000,
    coverage: {
      provider: 'v8',
      include: ['src/core/**'],
      thresholds: { lines: 100, functions: 100, branches: 100, statements: 100 },
    },
  },
})
