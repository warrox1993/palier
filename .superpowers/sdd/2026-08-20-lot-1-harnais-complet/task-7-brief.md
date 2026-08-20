# Brief — Tâche 7

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

## Tâche 7 : Règles de projet — ce qu'aucun linter ne connaît

**Fichiers :**
- Créer : `scripts/regles-projet.mjs`, `front/tests/harness/fixtures/calcul-conformite.ts`, `front/tests/harness/fixtures/couleur-en-dur.ts`, `front/tests/harness/fixtures/chaine-en-dur.tsx`, `front/tests/harness/regles-projet.test.ts`
- Modifier : `package.json` racine

**Interfaces :**
- Consomme : rien
- Produit : `node scripts/regles-projet.mjs`, appelé par `verify`.

> **Pourquoi un script plutôt qu'une règle de linter.** Oxlint ne fournit ni `no-restricted-syntax`, ni équivalent d'`eslint-plugin-i18next`, ni `naming-convention`. Ces trois besoins sont de la recherche de motifs dans le source — un script les couvre, et il est plus lisible qu'un sélecteur AST. La règle qui interdit les calculs de conformité passe de ceci :
>
> `Identifier[name=/^(mifflin|katch|tdee|limiteHaute|epley)/i]`
>
> à une phrase en clair dans un tableau de motifs, avec son message et son motif. C'est le même choix que pour le contrôle de licences : un outil dont on lit le code.

- [ ] **Étape 1 : écrire l'épreuve**

`front/tests/harness/regles-projet.test.ts` :

```typescript
import { existsSync } from 'node:fs'
import { describe, expect, it } from 'vitest'
import { lancerOutil } from './run-outil'

const FIXTURES = [
  'tests/harness/fixtures/calcul-conformite.ts',
  'tests/harness/fixtures/couleur-en-dur.ts',
  'tests/harness/fixtures/chaine-en-dur.tsx',
]

describe('garde-fou : règles de projet', () => {
  it.each(FIXTURES)('la fixture %s existe', (f) => {
    expect(existsSync(f), `Cible manquante : ${f}`).toBe(true)
  })

  it('accepte le code réel du front', () => {
    const r = lancerOutil(['node', '../scripts/regles-projet.mjs'])
    expect(r.code, `Le front réel viole une règle de projet :\n${r.sortie}`).toBe(0)
  })

  it('refuse un calcul de conformité dans le front', () => {
    const r = lancerOutil([
      'node', '../scripts/regles-projet.mjs',
      '--fichier', 'front/tests/harness/fixtures/calcul-conformite.ts',
    ])
    expect(r.code, `Le calcul de conformité a été accepté :\n${r.sortie}`).not.toBe(0)
    expect(r.sortie).toMatch(/Palier\.Domain/)
  })

  it('refuse une couleur hexadécimale hors jetons', () => {
    const r = lancerOutil([
      'node', '../scripts/regles-projet.mjs',
      '--fichier', 'front/tests/harness/fixtures/couleur-en-dur.ts',
    ])
    expect(r.code, `La couleur a été acceptée :\n${r.sortie}`).not.toBe(0)
    expect(r.sortie).toMatch(/jeton/i)
  })

  it('refuse une chaîne de texte en dur dans le JSX', () => {
    const r = lancerOutil([
      'node', '../scripts/regles-projet.mjs',
      '--fichier', 'front/tests/harness/fixtures/chaine-en-dur.tsx',
    ])
    expect(r.code, `La chaîne en dur a été acceptée :\n${r.sortie}`).not.toBe(0)
    expect(r.sortie).toMatch(/i18n/i)
  })
})
```

- [ ] **Étape 2 : lancer l'épreuve pour la voir échouer**

Attendu : ÉCHEC — le script n'existe pas.

- [ ] **Étape 3 : écrire `scripts/regles-projet.mjs`**

```javascript
#!/usr/bin/env node
// Règles propres à ce projet, qu'aucun linter généraliste ne connaît.
// Remplace no-restricted-syntax, eslint-plugin-i18next et naming-convention,
// absents d'Oxlint. Aucune dépendance : on lit ce qu'on exécute.
import { readFileSync, readdirSync, statSync } from 'node:fs'
import { join, extname, basename } from 'node:path'

const REGLES = [
  {
    id: 'calcul-de-conformite',
    motif:
      /\b(mifflin|katchMcArdle|tdeeAdaptatif|metabolismeDeBase|limiteHauteEfsa|plancherCalorique|plancherProteique|epleyCorrige)\w*/i,
    extensions: ['.ts', '.tsx'],
    message:
      'Les calculs de conformité vivent dans Palier.Domain, jamais dans le front. ' +
      "Le front affiche ce que l'API a calculé ; hors ligne, la dernière valeur connue avec son horodatage.",
  },
  {
    id: 'couleur-hors-jetons',
    motif: /#[0-9a-fA-F]{3}(?:[0-9a-fA-F]{3})?\b/,
    extensions: ['.ts', '.tsx', '.css'],
    exclure: [/src[\\/]ui[\\/]jetons\./],
    message: 'Aucune couleur hors des jetons de src/ui/jetons.ts — docs/02-design.md § 9.',
  },
  {
    id: 'ombre-ou-degrade',
    motif: /\b(box-shadow|drop-shadow|backdrop-blur|backdrop-filter|linear-gradient|radial-gradient)\b/,
    extensions: ['.ts', '.tsx', '.css'],
    message: 'Aucune ombre portée, aucun flou, aucun dégradé — docs/02-design.md § 2 et § 4.',
  },
  {
    id: 'fleche-unicode',
    motif: /["'`][^"'`]*[→←↑↓][^"'`]*["'`]/,
    extensions: ['.ts', '.tsx'],
    message: "Aucune flèche Unicode dans un libellé : utiliser une icône, ou rien.",
  },
  {
    id: 'chaine-en-dur',
    // Texte visible entre balises JSX : > Bonjour <
    motif: />\s*[A-Za-zÀ-ÿ][A-Za-zÀ-ÿ ,.'’!?-]{2,}\s*</,
    extensions: ['.tsx'],
    message:
      'Aucune chaîne de texte en dur : tout passe par i18next, y compris les erreurs et les états vides — CLAUDE.md § 4.',
  },
  {
    id: 'booleen-mal-nomme',
    motif: /\b(?:const|let)\s+(?!is|has|can|should)[a-z]\w*\s*:\s*boolean\b/,
    extensions: ['.ts', '.tsx'],
    message: "Un booléen se préfixe par is, has, can ou should — docs/16-projet.md § 2.",
  },
  {
    id: 'type-prefixe-i',
    motif: /\b(?:interface|type)\s+I[A-Z]\w*/,
    extensions: ['.ts', '.tsx'],
    message: 'Préfixe I interdit sur les types — docs/16-projet.md § 2.',
  },
]

const IGNORES = ['node_modules', 'dist', 'coverage', '.git', 'bin', 'obj', 'playwright-report']

function fichiers(racine) {
  const out = []
  const parcourir = (d) => {
    for (const e of readdirSync(d)) {
      if (IGNORES.includes(e)) continue
      const p = join(d, e)
      if (statSync(p).isDirectory()) parcourir(p)
      else out.push(p)
    }
  }
  parcourir(racine)
  return out
}

const iFichier = process.argv.indexOf('--fichier')
const cibles =
  iFichier !== -1
    ? [process.argv[iFichier + 1]]
    : fichiers('front/src').concat(fichiers('front/tests').filter((f) => !f.includes('fixtures')))

const violations = []
for (const f of cibles) {
  const ext = extname(f)
  let contenu
  try {
    contenu = readFileSync(f, 'utf8')
  } catch {
    console.error(`Fichier illisible : ${f}`)
    process.exit(2)
  }
  const lignes = contenu.split('\n')
  for (const regle of REGLES) {
    if (!regle.extensions.includes(ext)) continue
    if (regle.exclure?.some((r) => r.test(f))) continue
    lignes.forEach((ligne, i) => {
      if (ligne.trimStart().startsWith('//')) return
      if (regle.motif.test(ligne)) {
        violations.push({ fichier: f, ligne: i + 1, regle: regle.id, message: regle.message })
      }
    })
  }
}

if (violations.length > 0) {
  console.error(`${violations.length} violation(s) des règles de projet :\n`)
  for (const v of violations) {
    console.error(`  ${v.fichier}:${v.ligne}  [${v.regle}]`)
    console.error(`    ${v.message}\n`)
  }
  process.exit(1)
}

console.log(`${cibles.length} fichiers vérifiés, aucune violation des règles de projet.`)
```

- [ ] **Étape 4 : créer les trois fixtures**

`front/tests/harness/fixtures/calcul-conformite.ts` :

```typescript
export function mifflinStJeor(poidsKg: number, tailleCm: number, age: number): number {
  return 10 * poidsKg + 6.25 * tailleCm - 5 * age + 5
}
```

`front/tests/harness/fixtures/couleur-en-dur.ts` :

```typescript
export const fondInterdit = '#D97757'
```

`front/tests/harness/fixtures/chaine-en-dur.tsx` :

```typescript
export function Bouton() {
  return <button type="button">Enregistrer la séance</button>
}
```

Aucune de ces fixtures ne porte de commentaire explicatif en tête : le script ignore les lignes commentées, et un commentaire décrivant la violation la masquerait.

- [ ] **Étape 5 : ajouter le script racine**

```json
{ "scripts": { "regles": "node scripts/regles-projet.mjs" } }
```

- [ ] **Étape 6 : lancer l'épreuve pour la voir passer**

Attendu : 7 tests passent — les trois fixtures existent, le front réel est accepté, les trois violations sont refusées.

- [ ] **Étape 7 : mesurer les faux positifs**

Lancer `npm run regles` sur le front réel. Si des violations légitimes apparaissent — une couleur dans un commentaire de documentation, un texte de test — affiner le motif plutôt que d'ajouter une exception au fichier. **Consigner le nombre de motifs affinés dans le rapport de tâche** : un détecteur qui crie à tort est un détecteur qu'on cesse de lire.

- [ ] **Étape 8 : commit**

```bash
git add -A
git commit -m "ajoute les règles de projet que le linter ne couvre pas"
```

---
