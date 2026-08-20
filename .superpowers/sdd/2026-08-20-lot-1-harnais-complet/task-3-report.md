# Rapport — Tâche 3 : Réorganiser en `front/` et `back/`

Statut : **DONE_WITH_CONCERNS** (voir « Mes doutes » — rien de bloquant, mais deux écarts au brief à valider et un point hors périmètre à signaler).

## Ce qui a changé

### Déplacés vers `front/` (via `git mv`, historique préservé)

| Racine (avant) | `front/` (après) |
|---|---|
| `package.json` | `front/package.json` |
| `package-lock.json` | `front/package-lock.json` |
| `tsconfig.json` | `front/tsconfig.json` |
| `tsconfig.node.json` | `front/tsconfig.node.json` |
| `vite.config.ts` | `front/vite.config.ts` |
| `vitest.config.ts` | `front/vitest.config.ts` |
| `index.html` | `front/index.html` |
| `src/app/App.tsx`, `src/core/index.ts`, `src/main.tsx`, `src/vite-env.d.ts` | `front/src/...` |
| `tests/harness/run-outil.test.ts`, `tests/harness/run-outil.ts` | `front/tests/harness/...` |
| `.env.example` | `front/.env.example` |

`.prettierrc` et `.prettierignore` : **n'existaient pas** dans ce worktree (Tâche 8 « Prettier » n'a pas encore été exécutée). Le brief prévoyait déjà ce cas avec `git mv .prettierrc .prettierignore front/ 2>/dev/null || true` — la commande n'a rien fait, correctement.

Contenu interne des fichiers déplacés : **non modifié**. `vite.config.ts`, `vitest.config.ts`, `tsconfig.json`, `tsconfig.node.json` n'utilisent que des chemins relatifs (`./src`, `src/**`, `tests/harness/**`) — vérifiés par lecture, aucun chemin absolu ou remontant à la racine.

### Créé

- `package.json` (racine) — orchestration seule, contenu strictement identique au JSON verbatim du brief (aucune dépendance, scripts `verify` / `front` / `front:dev`).

### Modifié

- `.gitignore` — chemins racine remplacés par leurs équivalents `front/` et `back/`, contenu identique au verbatim du brief.

### Non modifié / non déplacé (hors périmètre du brief, laissé en l'état)

`.gitattributes`, `.nvmrc`, `CLAUDE.md`, `DEMARRAGE.md`, `README.md`, `docs/`, `.claude/`, `.superpowers/`.

## Le compte de tests, avant et après

**Avant (étape 1, à la racine, avant tout déplacement) :**

```
> palier@1.0.0 typecheck
> tsc --noEmit && tsc -p tsconfig.node.json --noEmit
(aucune sortie = 0 erreur)

> palier@1.0.0 build
> vite build
✓ 15 modules transformed.
dist/index.html                 0.31 kB │ gzip:  0.22 kB
dist/assets/index-DuzKoTp-.js 190.50 kB │ gzip: 60.02 kB │ map: 841.96 kB
✓ built in 353ms

> palier@1.0.0 test
> vitest run
 Test Files  1 passed (1)
      Tests  3 passed (3)
   Duration  20.53s
```

**Après (étape 6, depuis `front/`, après déplacement + commits) :**

```
> palier@1.0.0 typecheck
> tsc --noEmit && tsc -p tsconfig.node.json --noEmit
(aucune sortie = 0 erreur)

> palier@1.0.0 build
> vite build
✓ 15 modules transformed.
dist/index.html                 0.31 kB │ gzip:  0.22 kB
dist/assets/index-DuzKoTp-.js 190.50 kB │ gzip: 60.02 kB │ map: 841.96 kB
✓ built in 227ms

> palier@1.0.0 test
> vitest run
 Test Files  1 passed (1)
      Tests  3 passed (3)
   Duration  4.49s
```

**1 fichier de test, 3 tests — identique avant/après.** Modules transformés (15) et poids du bundle identiques au bit près. Revérifié une troisième fois après les deux commits (état final propre) : mêmes résultats.

## Ce qui a cassé (et a été rattrapé)

**Le commit unique de l'étape 7 aurait cassé `git log --follow` sur `front/package.json`, sans erreur visible.**

Séquence du problème : l'étape 2 déplace `package.json` vers `front/package.json` (staged comme renommage). L'étape 3 recrée ensuite un `package.json` à la racine, au même chemin que celui tout juste vidé. Si les deux changements sont indexés puis committés ensemble, git ne voit plus une suppression à la racine (le chemin existe encore, avec un contenu différent) : il classe `front/package.json` en **nouveau fichier** et `package.json` (racine) en **modifié**, deux entrées sans lien de renommage.

Vérifié concrètement avant de committer :

```
git diff --cached --find-renames=50% --summary -- package.json front/package.json
create mode 100644 front/package.json
```

Aucune ligne « rename » — la preuve que `git log --follow front/package.json` n'aurait montré que le commit de la réorganisation, pas les six commits d'historique antérieurs (installation du socle Vite, TypeScript 7, Vitest, etc.). C'est exactly le défaut que l'étape 2 du brief cherche à éviter (« l'historique de chaque fichier reste attaché »).

**Correction :** `git reset` (désindexation, aucune perte), puis deux commits au lieu d'un :

1. `b1f8875` — `réorganise le dépôt en front et back` : uniquement le déplacement pur (`git add front/ <chemins racine effacés>` + `git rm --cached package.json` pour que la racine soit bien vue comme « supprimée », pas « modifiée »). Les 14 fichiers, y compris `package.json`, apparaissent en `rename ... (100%)`.
2. `fa94e82` — `ajoute le package.json racine d'orchestration et corrige .gitignore` : le nouveau `package.json` (réellement neuf, sans historique à préserver) et `.gitignore`.

Revérifié après coup :

```
git log --follow --oneline front/package.json
b1f8875 réorganise le dépôt en front et back
fe33880 installe Vitest et l'utilitaire de franchissement
2a769c2 corrige l'identité du paquet et relie tsconfig.node.json au typecheck
745ce96 revient à TypeScript 7 (sans version figée, conforme au brief)
ac2aedd aligne TypeScript et @types/node sur les versions du template Vite officiel
fb6ef6c installe le socle Vite React TypeScript strict
```

L'historique complet remonte bien. Même vérification faite sur `front/src/app/App.tsx` (remonte jusqu'au renommage `app.tsx` → `App.tsx` puis au commit d'installation du socle).

C'est un écart à l'étape 7 du brief (qui prévoit un seul commit) — assumé et documenté ici plutôt que silencieux, conformément à la règle « ne jamais trancher silencieusement ».

## Ce que je signale sans y avoir touché

1. **`.husky/pre-commit` et `.husky/pre-push` n'existent pas dans ce worktree.** Le brief liste ces deux fichiers sous « Modifier », mais il n'y a rien à modifier : ni `.husky/`, ni aucune trace de Husky (confirmé par `find . -iname "*husky*"`, rien hors `node_modules`). Vérification faite dans `docs/superpowers/plans/2026-08-20-lot-1-harnais-complet.md` : c'est la **Tâche 19** (« hooks Git et détection de secrets ») qui installe Husky (`npm install -D husky lint-staged gitleaks` puis `npx husky init`) et écrit ces deux fichiers pour la première fois — avec un contenu légèrement différent de celui du brief de la Tâche 3 (`npx lint-staged` sans `--prefix front`, puisque Husky/lint-staged y seront installés à la racine, pas dans `front/`). Le brief de la Tâche 3 a visiblement été extrait d'une version antérieure du plan où le harnais Husky précédait la réorganisation ; dans le plan « complet » actuel, l'ordre s'est inversé (Tâche 3 avant Tâche 19) sans que ce brief soit corrigé en conséquence — contrairement au traitement réservé à `.prettierrc`/`.prettierignore`/`.env.example`, protégés par des gardes d'existence dans le même brief.
   Je n'ai **pas** créé ces fichiers moi-même : ils seraient non fonctionnels à ce stade (Husky n'est pas installé, `core.hooksPath` n'est pas configuré), et leur contenu correct appartient à la Tâche 19, qui les écrit avec un test dédié (`tests/harness/secrets.test.ts`, à la racine). Les créer maintenant aurait risqué un contenu divergent de celui que la Tâche 19 doit produire et vérifier.
   **Ce point mériterait une décision explicite** : soit le brief de la Tâche 19 suffit tel quel (probable, vu qu'il est autonome et déjà correct), soit le brief de la Tâche 3 doit être corrigé dans le plan source pour ne plus lister l'étape 4 comme obligatoire.

2. **`docs/16-projet.md` documente encore l'ancienne arborescence** (`src/`, `tests/` à la racine, sans `front/`). Vérifié : ni la Tâche 3, ni la Tâche 20 (« Documents de travail », qui crée `docs/gabarit-rapport-lot.md` et `docs/securite/asvs-l2.md`, et complète `docs/decisions.md`) ne mettent à jour ce document. Aucune tâche du plan ne semble porter cette mise à jour — à confirmer avec le directeur de plan.

3. **`front/package.json` garde `"name": "palier"` et `"version": "1.0.0"`**, désormais dupliqué avec le `"palier"` / `"0.1.0"` du `package.json` racine. Sans conséquence pratique (pas d'espace de travail npm déclaré reliant les deux), mais à signaler pour information.

## Le franchissement

- **Tourne-t-il ?** Oui — `npm --prefix front run typecheck|build|test` s'exécutent tous les trois sans erreur, à froid (après `npm ci`) comme après les deux commits.
- **Mord-il ?** Testé activement, pas seulement lu : `npm run verify` (racine) a été lancé volontairement pour confirmer l'échec attendu (`scripts/verify.mjs` manquant, mentionné dans le brief comme normal à ce stade) — code de sortie 1, message `Cannot find module '...\scripts\verify.mjs'`. Comportement conforme, non « réparé » comme demandé.
- **Provoqué, pas seulement constaté :** le nouveau `.gitignore` a été vérifié en le mettant en situation réelle — `npm ci` puis `npm run build` exécutés **dans `front/`** après le déplacement, puis `git status` : `front/node_modules/` et `front/dist/` n'apparaissent pas comme non suivis. Ce n'est pas une relecture du fichier, c'est la preuve que le motif ignore bien le bon dossier.
- **Refuse-t-il d'enregistrer à moitié ?** Le risque de renommage cassé (voir « Ce qui a cassé ») a été détecté avant le commit, avec l'instrument adéquat (`git diff --cached --find-renames`), pas en relisant le diff à l'œil — puis re-vérifié après coup avec `git log --follow` sur deux fichiers de nature différente (config et composant React).

## Ce qui n'a pas pu être vérifié

- Le comportement du hook `pre-push` (`npm run verify`) en conditions réelles — impossible tant que `.husky/` et `scripts/verify.mjs` n'existent pas (Tâches 19 et 18).
- L'effet du nouveau `.gitignore` sur `back/**/bin/` et `back/**/obj/` — aucun projet `back/` n'existe encore (Tâche 11), donc rien à provoquer pour l'instant.
- Aucun test front n'a été perdu ni ajouté silencieusement au-delà de ce qui est visible dans `vitest run` — mais je n'ai pas de mécanisme indépendant pour prouver que 3 est *le compte attendu dans l'absolu* (seulement qu'il est identique avant/après ce déplacement, ce qui est la garantie demandée).

## Skills invoqués

Aucun skill Superpowers invoqué directement (`systematic-debugging`, `test-driven-development`, etc.) : la tâche est une réorganisation de fichiers sans écriture de code produit, hors périmètre de TDD. La vigilance méthodologique s'est portée sur la vérification empirique du renommage git (mesure avec son instrument, `git diff --find-renames`) plutôt que sur un skill formel.

## Commits

| SHA | Message |
|---|---|
| `b1f8875` | `réorganise le dépôt en front et back` |
| `fa94e82` | `ajoute le package.json racine d'orchestration et corrige .gitignore` |

`git status` final : working tree propre.
