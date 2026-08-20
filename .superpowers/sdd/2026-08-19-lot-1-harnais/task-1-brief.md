# Brief — Tâche 1

> Extrait de `docs/superpowers/plans/2026-08-19-lot-1-harnais.md`. C'est la source unique de tes exigences.
> Les valeurs exactes — code, chemins, chaînes de caractères — se reprennent **verbatim**.

## Contraintes globales

Ces règles s'appliquent à **toutes** les tâches sans être répétées.

- **Node 24** (`.nvmrc`), identique en local, en CI et sur Vercel. La documentation Vercel du 27/02/2026 donne 24.x comme version **par défaut** (24.x, 22.x et 20.x disponibles), et 24.16.0 est la version installée localement : aucun écart entre le poste, la CI et la cible de déploiement. Verrouillée aussi par `engines.node` dans `package.json`.
- **npm**, jamais pnpm ni yarn. En CI : `npm ci`, jamais `npm install`.
- **Aucune version de paquet figée dans ce plan.** Les installations se font sans numéro ; le lockfile verrouille.
- **Messages de commit en français, à l'impératif** — `16-projet.md` § 2. Exemple : `ajoute le calcul du TDEE adaptatif`.
- **Nommage** — fichiers `kebab-case.ts`, composants `PascalCase.tsx`, fonctions et variables `camelCase`, constantes `SCREAMING_SNAKE_CASE`, types `PascalCase` sans préfixe, booléens en `is`/`has`/`can`.
- **Tests à côté du source** — `<fichier>.test.ts`.
- **Aucun secret dans le dépôt.** `.env.example` versionné, `.env` ignoré.
- **Toute épreuve du harnais vit dans `tests/harness/`** ; les violations délibérées dans `tests/harness/fixtures/`, exclues de `tsconfig.json` et du build.
- **Une seule branche de travail** : `feat/lot-1-harnais`, fusionnée dans `main` en fin de lot.
- Le dépôt distant est `github.com/warrox1993/palier`, privé, branche par défaut `main`.

---

## Tâche 1 : Socle Vite + React + TypeScript strict

**Fichiers :**
- Créer : `package.json`, `.nvmrc`, `tsconfig.json`, `tsconfig.node.json`, `vite.config.ts`, `index.html`, `src/main.tsx`, `src/app/app.tsx`, `src/core/index.ts`, `.gitignore`, `.env.example`

**Interfaces :**
- Consomme : rien
- Produit : les scripts `npm run build`, `npm run dev`, `npm run typecheck`. Le dossier `src/core/` existe et n'exporte rien encore.

- [ ] **Étape 1 : créer la branche de travail**

```bash
git checkout -b feat/lot-1-harnais
```

- [ ] **Étape 2 : initialiser le paquet et installer le socle**

```bash
npm init -y
npm install react react-dom
npm install -D vite @vitejs/plugin-react typescript @types/react @types/react-dom
echo "24" > .nvmrc
```

- [ ] **Étape 3 : écrire `tsconfig.json` en mode strict**

```json
{
  "compilerOptions": {
    "target": "ES2022",
    "lib": ["ES2022", "DOM", "DOM.Iterable"],
    "module": "ESNext",
    "moduleResolution": "bundler",
    "jsx": "react-jsx",
    "strict": true,
    "noUncheckedIndexedAccess": true,
    "exactOptionalPropertyTypes": true,
    "noImplicitOverride": true,
    "noUnusedLocals": true,
    "noUnusedParameters": true,
    "noFallthroughCasesInSwitch": true,
    "isolatedModules": true,
    "verbatimModuleSyntax": true,
    "resolveJsonModule": true,
    "skipLibCheck": true,
    "noEmit": true,
    "baseUrl": ".",
    "paths": { "@/*": ["src/*"] }
  },
  "include": ["src", "tests"],
  "exclude": ["tests/harness/fixtures"]
}
```

`exclude` sur `tests/harness/fixtures` est essentiel : c'est ce qui permet d'héberger un `any` interdit dans le dépôt sans casser le typecheck du projet.

- [ ] **Étape 4 : écrire `vite.config.ts`**

```typescript
import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import { fileURLToPath, URL } from 'node:url'

export default defineConfig({
  plugins: [react()],
  resolve: {
    alias: { '@': fileURLToPath(new URL('./src', import.meta.url)) },
  },
  build: { outDir: 'dist', sourcemap: true },
})
```

- [ ] **Étape 5 : écrire le point d'entrée minimal**

`index.html` :

```html
<!doctype html>
<html lang="fr">
  <head>
    <meta charset="UTF-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>palier</title>
  </head>
  <body>
    <div id="root"></div>
    <script type="module" src="/src/main.tsx"></script>
  </body>
</html>
```

`src/main.tsx` :

```typescript
import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { App } from './app/app'

const conteneur = document.getElementById('root')
if (!conteneur) throw new Error('Élément racine introuvable')

createRoot(conteneur).render(
  <StrictMode>
    <App />
  </StrictMode>,
)
```

`src/app/app.tsx` :

```typescript
export function App() {
  return <main id="contenu">palier</main>
}
```

`src/core/index.ts` :

```typescript
// Modules purs : aucun accès réseau, base ou React.
// Ce fichier existe pour que la règle d'architecture ait une cible dès le lot 1.
export {}
```

- [ ] **Étape 6 : écrire `.gitignore` et `.env.example`**

`.gitignore` :

```
node_modules/
dist/
coverage/
playwright-report/
test-results/
.env
.env.local
*.local
.DS_Store
```

`.env.example` — reprendre **intégralement** la liste de `docs/16-projet.md` § 3, sans aucune valeur.

- [ ] **Étape 7 : ajouter les scripts au `package.json`**

```json
{
  "type": "module",
  "engines": { "node": "24.x" },
  "scripts": {
    "dev": "vite",
    "build": "vite build",
    "preview": "vite preview",
    "typecheck": "tsc --noEmit"
  }
}
```

- [ ] **Étape 8 : vérifier que tout tourne**

Lancer : `npm run typecheck && npm run build`
Attendu : aucune erreur, un dossier `dist/` produit.

- [ ] **Étape 9 : commit**

```bash
git add -A
git commit -m "installe le socle Vite React TypeScript strict"
```

---
