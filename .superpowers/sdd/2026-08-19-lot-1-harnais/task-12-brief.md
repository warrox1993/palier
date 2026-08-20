# Brief — Tâche 12

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

## Tâche 12 : documents de travail

**Fichiers :**
- Créer : `docs/decisions.md`, `docs/gabarit-rapport-lot.md`, `docs/securite/asvs-l2.md`

**Interfaces :**
- Consomme : les décisions de la spec, § 2
- Produit : les trois documents lus au démarrage de chaque session.

- [ ] **Étape 1 : écrire `docs/decisions.md`**

Une entrée par décision, avec ce format exact :

```markdown
# Journal des décisions

**Lu au démarrage de chaque session, pas rempli à la fin.**
Une décision qu'on ne relit pas au démarrage se reprend.

## D1 — Nom du projet : palier
**Tranché le :** 19/08/2026
**Motif :** recommandation de `15-marque.md` § 3, cohérent avec le système de progression.
**Ce qui la rouvrirait :** une antériorité trouvée au registre BOIP ou EUIPO, ou une priorité donnée à l'international.

## D2 — Gestionnaire de paquets : npm
**Tranché le :** 19/08/2026
**Motif :** cohérent avec `DEMARRAGE.md`, déjà autorisé dans les permissions, détecté nativement par Vercel.
**Ce qui la rouvrirait :** une durée de `verify` durablement au-delà du seuil imputable à l'installation des dépendances.
```

Compléter avec D3 à D7 : fonctions serveur dans `api/`, référentiel ASVS L2 + Top 10 CI/CD, franchissement permanent, sévérité à deux niveaux, découpage de l'étape 1 en sept lots. Chacune avec sa date, son motif et sa condition de réouverture.

- [ ] **Étape 2 : écrire `docs/gabarit-rapport-lot.md`**

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

## Skills et agents invoqués
Et pour ceux qui ne l'ont pas été, pourquoi.
```

- [ ] **Étape 3 : écrire `docs/securite/asvs-l2.md`**

En-tête portant la version exacte de l'ASVS retenue, relevée au moment de la rédaction, puis un tableau : identifiant de l'exigence · intitulé · état (`couverte` / `non applicable` / `prévue lot N`) · mécanisme ou test qui la prouve.

Renseigner à ce stade les exigences qui relèvent de la chaîne de build et de la gestion des secrets — elles sont couvertes par ce lot. Marquer les exigences de contrôle d'accès `prévue lot 3`, celles d'authentification `prévue lot 4`, celles de journalisation `prévue lot 7`.

- [ ] **Étape 4 : commit**

```bash
git add -A
git commit -m "ajoute le journal des décisions, le gabarit de rapport et le suivi ASVS"
```

---
