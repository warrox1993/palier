# Brief — Tâche 11

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

## Tâche 11 : hooks Git et détection de secrets

**Fichiers :**
- Créer : `.husky/pre-commit`, `.husky/pre-push`, `.gitleaks.toml`, `tests/harness/fixtures/faux-secret.txt`, `tests/harness/secrets.test.ts`
- Modifier : `package.json`

**Interfaces :**
- Consomme : `npm run verify` de la tâche 10
- Produit : le refus au commit et au push.

- [ ] **Étape 1 : écrire l'épreuve**

`tests/harness/secrets.test.ts` :

```typescript
import { existsSync, readFileSync } from 'node:fs'
import { describe, expect, it } from 'vitest'
import { lancerOutil } from './run-outil'

const FIXTURE = 'tests/harness/fixtures/faux-secret.txt'

describe('garde-fou : secrets', () => {
  it('la fixture de violation existe', () => {
    expect(existsSync(FIXTURE), `Cible manquante : ${FIXTURE}`).toBe(true)
  })

  it('gitleaks détecte une clé au format reconnu', () => {
    const r = lancerOutil(['npx', 'gitleaks', 'detect', '--no-git', '--source', 'tests/harness/fixtures', '--redact'])
    expect(r.code, `gitleaks n'a rien vu :\n${r.sortie}`).not.toBe(0)
  })

  it('les deux hooks existent', () => {
    expect(existsSync('.husky/pre-commit')).toBe(true)
    expect(existsSync('.husky/pre-push')).toBe(true)
  })

  it("le hook pre-push n'appelle que verify", () => {
    const hook = readFileSync('.husky/pre-push', 'utf8')
    expect(hook).toContain('npm run verify')
    // Le hook délègue, il n'énumère pas : sinon il diverge de la CI.
    expect(hook).not.toMatch(/npm run (lint|typecheck|build|knip)/)
  })
})
```

- [ ] **Étape 2 : lancer l'épreuve pour la voir échouer**

Lancer : `npx vitest run tests/harness/secrets.test.ts`
Attendu : ÉCHEC.

- [ ] **Étape 3 : installer Husky, lint-staged et gitleaks**

```bash
npm install -D husky lint-staged gitleaks
npx husky init
```

- [ ] **Étape 4 : écrire les hooks**

`.husky/pre-commit` :

```bash
npx lint-staged
npx gitleaks protect --staged --redact --config .gitleaks.toml
```

`.husky/pre-push` :

```bash
npm run verify
```

- [ ] **Étape 5 : configurer lint-staged et gitleaks**

Dans `package.json` :

```json
{
  "lint-staged": {
    "*.{ts,tsx}": ["eslint --fix", "prettier --write"],
    "*.{json,md,css,html}": ["prettier --write"]
  }
}
```

`.gitleaks.toml` :

```toml
title = "palier"

[extend]
useDefault = true

[[rules]]
id = "cle-anthropic"
description = "Clé API Anthropic"
regex = '''sk-ant-[A-Za-z0-9_\-]{20,}'''

[[rules]]
id = "cle-supabase-service"
description = "Clé de service Supabase"
regex = '''eyJ[A-Za-z0-9_\-]{20,}\.[A-Za-z0-9_\-]{20,}\.[A-Za-z0-9_\-]{20,}'''
```

- [ ] **Étape 6 : créer la fixture**

`tests/harness/fixtures/faux-secret.txt` :

```
# Faux secret, format valide, valeur inventée. Sert à prouver que gitleaks mord.
ANTHROPIC_API_KEY=sk-ant-api03-CECI-EST-UN-FAUX-SECRET-DE-TEST-0000000000
```

- [ ] **Étape 7 : lancer l'épreuve et vérifier le refus réel au commit**

Lancer : `npx vitest run tests/harness/secrets.test.ts`
Attendu : 3 tests passent.

Puis provoquer un refus réel :

```bash
echo "export const x: any = 1" > src/violation-temporaire.ts
git add src/violation-temporaire.ts
git commit -m "essai de refus"
```

Attendu : le commit est **refusé** par le hook. Nettoyer ensuite :

```bash
git reset HEAD src/violation-temporaire.ts && rm src/violation-temporaire.ts
```

- [ ] **Étape 8 : commit**

```bash
git add -A
git commit -m "installe les hooks de commit et de push"
```

---
