import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { App } from './app/App'

const conteneur = document.getElementById('root')
if (!conteneur) throw new Error('Élément racine introuvable')

createRoot(conteneur).render(
  <StrictMode>
    <App />
  </StrictMode>,
)
