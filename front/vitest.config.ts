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
    // `allowOnly: false` — le défaut de Vitest est `!process.env.CI`, donc les
    // `.only` sont TOLÉRÉS en local, c'est-à-dire dans `npm run verify` et dans
    // le hook de pré-envoi. Mesuré le 20/08/2026 : un seul `it.only` oublié fait
    // sauter 5 épreuves du fichier concerné, et la suite sort en **code 0**.
    // Un garde-fou désarmé qui affiche vert est le défaut que ce dépôt chasse.
    allowOnly: false,
  },
})
