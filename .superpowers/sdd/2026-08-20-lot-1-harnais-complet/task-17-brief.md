# Brief — Tâche 17

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

## Tâche 17 : Vulnérabilités des dépendances

**Fichiers :**
- Créer : `back/tests-harness/vulnerabilites.test.mjs`
- Modifier : `package.json` racine

**Interfaces :**
- Consomme : la solution
- Produit : `npm run audit:back`, appelé par `verify`.

- [ ] **Étape 1 : écrire l'épreuve**

`back/tests-harness/vulnerabilites.test.mjs` :

```javascript
import { describe, expect, it } from 'vitest'
import { lancerOutil } from '../../front/tests/harness/run-outil.js'

describe('garde-fou : vulnérabilités des dépendances', () => {
  it('aucune vulnérabilité connue dans les paquets du backend', () => {
    const r = lancerOutil([
      'dotnet', 'list', 'back/Palier.sln', 'package', '--vulnerable', '--include-transitive',
    ])
    expect(r.code, `La commande a échoué :\n${r.sortie}`).toBe(0)
    expect(r.sortie, `Vulnérabilité détectée :\n${r.sortie}`).not.toMatch(/High|Critical/i)
  })
})
```

`dotnet list package --vulnerable` sort en code 0 même quand il trouve quelque chose : c'est la sortie qu'il faut examiner, pas le code de retour. C'est exactement le genre de contrôle qui approuve en silence si on ne le vérifie pas.

- [ ] **Étape 2 : lancer l'épreuve**

Attendu : succès sur les dépendances actuelles.

- [ ] **Étape 3 : ajouter le script**

```json
{ "scripts": { "audit:back": "dotnet list back/Palier.sln package --vulnerable --include-transitive" } }
```

- [ ] **Étape 4 : commit**

```bash
git add -A
git commit -m "surveille les vulnérabilités des paquets du backend"
```

---

---
