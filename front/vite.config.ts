import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import { fileURLToPath, URL } from 'node:url'

export default defineConfig({
  plugins: [react()],
  resolve: {
    alias: { '@': fileURLToPath(new URL('./src', import.meta.url)) },
  },
  // En production, front et API partagent le domaine — D16 — et `/api` se
  // résout tout seul. Le serveur de DÉVELOPPEMENT, lui, écoute sur un autre
  // port que `dotnet run` : sans ce renvoi, l'écran d'état ne pourrait jamais
  // parler à l'API en local, et son état de contenu ne serait qu'une hypothèse.
  //
  // Le port est celui de `back/Palier.Api/Properties/launchSettings.json`.
  // `VITE_API_PROXY` permet de le déplacer sans toucher ce fichier.
  //
  // `preview` N'EST PAS renvoyé, délibérément : les épreuves Playwright y
  // tournent, et l'API n'y répond pas. Elles atteignent donc l'état d'erreur de
  // façon déterministe, sans qu'aucune donnée n'ait été simulée.
  server: {
    proxy: {
      '/api': {
        target: process.env.VITE_API_PROXY ?? 'http://localhost:5025',
        changeOrigin: false,
      },
    },
  },
  build: { outDir: 'dist', sourcemap: true },
})
