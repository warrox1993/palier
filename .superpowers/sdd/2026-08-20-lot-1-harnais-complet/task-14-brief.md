# Brief — Tâche 14

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

## Tâche 14 : Couverture à 100 % sur le domaine

**Fichiers :**
- Modifier : `back/Palier.Domain.Tests/Palier.Domain.Tests.csproj`
- Créer : `back/coverage.runsettings`, `back/tests-harness/couverture.test.mjs`

**Interfaces :**
- Consomme : le projet de tests de la tâche 4
- Produit : `dotnet test --settings back/coverage.runsettings`, en échec sous 100 % sur `Palier.Domain`.

- [ ] **Étape 1 : écrire l'épreuve**

`back/tests-harness/couverture.test.mjs` :

```javascript
import { existsSync, writeFileSync, rmSync } from 'node:fs'
import { describe, expect, it, afterAll } from 'vitest'
import { lancerOutil } from '../../front/tests/harness/run-outil.js'

const NON_TESTE = 'back/Palier.Domain/Energie/NonTeste.cs'

afterAll(() => rmSync(NON_TESTE, { force: true }))

describe('garde-fou : couverture du domaine', () => {
  it('le fichier de configuration existe', () => {
    expect(existsSync('back/coverage.runsettings'), 'Cible manquante').toBe(true)
  })

  it('refuse une fonction du domaine non couverte', () => {
    writeFileSync(
      NON_TESTE,
      `namespace Palier.Domain.Energie;\n\n` +
        `public static class NonTeste\n{\n` +
        `    public static decimal Double(decimal x) => x * 2m;\n}\n`,
    )
    const r = lancerOutil([
      'dotnet', 'test', 'back/Palier.Domain.Tests',
      '--settings', 'back/coverage.runsettings',
    ])
    expect(r.code, `Le seuil de couverture n'a pas mordu :\n${r.sortie}`).not.toBe(0)
    // Seconde assertion obligatoire : un code non nul prouve seulement que
    // quelque chose a échoué, pas que l'outil a refusé. Sans elle, l'épreuve
    // passe au vert quand l'outil est absent, indisponible ou mal appelé.
    expect(r.sortie).toMatch(/threshold|seuil|coverage/i)
  })
})
```

- [ ] **Étape 2 : lancer l'épreuve pour la voir échouer**

Attendu : ÉCHEC — aucun seuil n'est configuré, `dotnet test` passe malgré la fonction non couverte.

- [ ] **Étape 3 : ajouter coverlet au projet de tests**

```bash
cd back
dotnet add Palier.Domain.Tests package coverlet.collector
dotnet add Palier.Domain.Tests package coverlet.msbuild
```

- [ ] **Étape 4 : écrire `back/coverage.runsettings`**

```xml
<?xml version="1.0" encoding="utf-8"?>
<RunSettings>
  <DataCollectionRunSettings>
    <DataCollectors>
      <DataCollector friendlyName="XPlat code coverage">
        <Configuration>
          <Format>cobertura</Format>
          <Include>[Palier.Domain]*</Include>
          <Threshold>100</Threshold>
          <ThresholdType>line,branch,method</ThresholdType>
          <ThresholdStat>total</ThresholdStat>
        </Configuration>
      </DataCollector>
    </DataCollectors>
  </DataCollectionRunSettings>
</RunSettings>
```

Le seuil ne porte que sur `Palier.Domain` : `08-workflow.md` § 6 exige 100 % sur les modules de calcul, et **aucun seuil global** — un chiffre global pousse à tester ce qui est facile.

- [ ] **Étape 5 : lancer l'épreuve pour la voir passer**

Attendu : 2 tests passent — le seuil refuse la fonction non couverte, et la fixture est nettoyée après.

- [ ] **Étape 6 : vérifier que le domaine réel passe le seuil**

```bash
dotnet test back/Palier.Domain.Tests --settings back/coverage.runsettings
```

Attendu : succès, 100 % sur `Palier.Domain`.

- [ ] **Étape 7 : commit**

```bash
git add -A
git commit -m "impose une couverture totale sur le domaine"
```

---

---
