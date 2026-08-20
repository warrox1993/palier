# Brief — Tâche 19

> Extrait de `docs/superpowers/plans/2026-08-20-lot-1-harnais-complet.md`. C'est la source unique de tes exigences.
> Les valeurs exactes — code, chemins, chaînes de caractères — se reprennent **verbatim**.

## Contraintes globales

- **Node 24** (`.nvmrc`) et **.NET 10** (`global.json`, SDK 10.0.303, `rollForward: latestFeature`).
- **npm** côté front, jamais pnpm ni yarn. En CI : `npm ci`, jamais `npm install`.
- **Aucune version de paquet figée dans ce plan.** Les installations se font sans numéro ; les fichiers de verrouillage figent.
- **Toute dépendance ajoutée doit passer la liste blanche de licences** — MIT, Apache-2.0, BSD, ISC, PostgreSQL. Décision D13 du journal. MediatR est exclu : sa licence RPL-1.5 obligerait à publier le code source d'un service commercial.
- **Messages de commit en français, à l'impératif** — `16-projet.md` § 2.
- **Nommage front** : fichiers `kebab-case.ts`, composants `PascalCase.tsx`, fonctions et variables `camelCase`, constantes `SCREAMING_SNAKE_CASE`, booléens en `is`/`has`/`can`.
- **Nommage C#** : fichiers et types en `PascalCase`, un type public par fichier, champs privés en `_camelCase`.
- **Tests à côté du source** côté front — `<fichier>.test.ts`.
- **Aucun secret dans le dépôt.** `.env.example` versionné, `.env` ignoré.
- **`front/tests/harness/fixtures/` et `back/tests-harness/fixtures/`** hébergent les violations délibérées, exclues du typecheck et du build.
- **`Palier.Domain` ne référence aucun projet** et aucun paquet d'accès aux données, réseau ou UI. Traduction de `01-conformite.md` § 3.
- **Couverture 100 % sur `Palier.Domain`** — `08-workflow.md` § 6. Aucun seuil global ailleurs.
- **Une seule branche de travail** : `feat/lot-1-harnais`, déjà active.
- Dépôt distant : `github.com/warrox1993/palier`, privé, branche par défaut `main`.

---

## Tâche 19 : hooks Git et détection de secrets

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
    // Seconde assertion obligatoire : un code non nul prouve seulement que
    // quelque chose a échoué, pas que l'outil a refusé. Sans elle, l'épreuve
    // passe au vert quand l'outil est absent, indisponible ou mal appelé.
    // Ce cas est le plus exposé : le plan prévient lui-même que `npx gitleaks`
    // n'expose pas forcément un binaire sur toutes les plateformes.
    expect(r.sortie).toMatch(/secret|leak/i)
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
    "*.{ts,tsx}": ["oxlint --fix", "prettier --write"],
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
id = "chaine-connexion-postgres"
description = "Chaîne de connexion PostgreSQL avec mot de passe"
regex = '''(?i)(Host|Server)=[^;]+;.*Password=[^;\s"']+'''

[[rules]]
id = "jeton-jwt"
description = "Jeton JWT en clair"
regex = '''eyJ[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}'''

[[rules]]
id = "cle-application-ovh"
description = "Clé applicative OVHcloud"
regex = '''(?i)ovh_(application|consumer)_(key|secret)\s*[=:]\s*[A-Za-z0-9]{16,}'''
```

- [ ] **Étape 6 : créer la fixture**

`tests/harness/fixtures/faux-secret.txt` :

```
# Faux secret, format valide, valeur inventée. Sert à prouver que gitleaks mord.
# Les motifs surveillés suivent l'architecture : clé de modèle, chaîne de
# connexion PostgreSQL, jeton JWT, clé applicative OVHcloud.
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
