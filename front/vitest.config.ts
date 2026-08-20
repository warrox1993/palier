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
    // L'exclusion de l'épreuve d'accessibilité est portée par le script `test`,
    // pas par cette configuration : le drapeau `--exclude` de Vitest AJOUTE aux
    // globs déclarés ici, il ne les remplace pas. La porter ici la rendrait
    // impossible à réactiver — y compris pour `test:harness`, qui existe
    // précisément pour la lancer. Même raisonnement que le ruling P6 pour
    // Oxlint : l'exclusion appartient à la commande qui n'en veut pas.
    exclude: ['**/node_modules/**'],
    testTimeout: 60000,
  },
})
