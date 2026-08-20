# Brief — Tâche 20

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

## Tâche 20 : Documents de travail

**Fichiers :**
- Créer : `docs/gabarit-rapport-lot.md`, `docs/securite/asvs-l2.md`
- Modifier : `docs/decisions.md`

**Interfaces :**
- Consomme : les décisions prises pendant ce lot
- Produit : les documents lus au démarrage de chaque session.

> `docs/decisions.md` a été créé hors plan, au moment du changement d'architecture. Cette tâche ne le recrée pas : elle y **ajoute** les décisions prises pendant l'exécution du lot.

- [ ] **Étape 1 : écrire `docs/gabarit-rapport-lot.md`**

```markdown
# Gabarit — rapport de fin de lot

## Ce qui a changé
Fichiers, compteurs, avant/après.

## Ce qui a cassé
Y compris ce qui a été cassé puis rattrapé.

## Ce que je signale sans y avoir touché
Trouvé en chemin, hors périmètre.

## Le franchissement
La preuve que le garde-fou refuse, pas sa relecture. Pour chaque épreuve :
ce qui a été provoqué, ce qui était attendu, ce qui s'est produit.

## Ce qui n'a pas pu être vérifié
Nommément. Un rapport sans incertitude est un rapport incomplet.

## Mesures
Durée de `npm run verify`. Nombre de tests. Couverture du domaine.
Faux positifs écartés, avec leur motif.

## Skills et agents invoqués
Et pour ceux qui ne l'ont pas été, pourquoi.
```

- [ ] **Étape 2 : écrire `docs/securite/asvs-l2.md`**

En-tête portant la version exacte de l'ASVS retenue, relevée à la rédaction — les numérotations OWASP ont changé récemment, aucun identifiant ne se cite de mémoire.

Puis un tableau : identifiant · intitulé · état (`couverte` / `non applicable` / `prévue lot N`) · mécanisme ou test qui la prouve.

Renseigner à ce stade les exigences qui relèvent de la chaîne de build, de la gestion des secrets et de la configuration. Marquer `prévue lot 4` les exigences de contrôle d'accès, `prévue lot 5` celles d'authentification, `prévue lot 9` celles de journalisation et de supervision.

- [ ] **Étape 3 : compléter `docs/decisions.md`**

Y ajouter les décisions prises pendant l'exécution du lot, au même format que les précédentes — ce qui a été tranché, le motif, **ce qui la rouvrirait**. Au minimum : l'arbitrage `typescript-eslint` contre TypeScript 7, et le nombre de faux positifs Knip écartés avec leur raison.

- [ ] **Étape 4 : commit**

```bash
git add -A
git commit -m "ajoute le gabarit de rapport et le suivi des exigences ASVS"
```

---
