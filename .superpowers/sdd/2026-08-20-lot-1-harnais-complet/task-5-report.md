# Rapport — Tâche 5 : Oxlint, socle et règles syntaxiques

Statut : **terminé, avec une déviation documentée par rapport au contenu verbatim du brief** (voir § 5). Les 8 étapes sont faites, les 9 tests du harnais passent (5 préexistants + 4 nouveaux), le lint global est propre.

## 1. Ce qui a été implémenté

Fichiers créés :
- `front/tests/harness/oxlint-base.test.ts` — verbatim du brief, non modifié (confirmé par `git show HEAD:front/tests/harness/oxlint-base.test.ts`).
- `front/tests/harness/fixtures/any-explicite.ts` — verbatim du brief.
- `front/tests/harness/fixtures/nommage-Invalide.ts` — verbatim du brief.
- `front/.oxlintrc.json` — verbatim du brief **sauf** `ignorePatterns` (voir § 5).

Fichier modifié :
- `front/package.json` — ajout du script `lint`, avec une formulation différente de celle du brief (voir § 5) ; ajout d'`oxlint` en devDependency (`package-lock.json` mis à jour en conséquence).

Aucun fichier hors de cette liste n'a été touché.

## 2. Version d'Oxlint installée

```
$ npx oxlint --version
Version: 1.79.0
```

Dans `package.json` : `"oxlint": "^1.79.0"` (installé sans numéro figé, comme demandé — `npm add -D oxlint`, le lockfile fige la résolution).

Licence : `MIT` (lue dans `node_modules/oxlint/package.json`) — conforme à la liste blanche D13 (MIT, Apache-2.0, BSD, ISC, PostgreSQL).

## 3. Preuve TDD — ROUGE puis VERT

**ROUGE** (étape 2, épreuve écrite, rien installé) :

```
$ npm run test:harness -- oxlint-base
...
 ❯ tests/harness/oxlint-base.test.ts (4 tests | 4 failed)
     × la fixture tests/harness/fixtures/any-explicite.ts existe
     × la fixture tests/harness/fixtures/nommage-Invalide.ts existe
     × refuse un any explicite
     × refuse un nom de fichier hors kebab-case et PascalCase

 Test Files  1 failed | 2 passed (3)
      Tests  4 failed | 5 passed (9)
```

Les 4 nouveaux tests échouent (fixtures absentes, et `npx oxlint` a dû télécharger le paquet à la volée — `npm warn exec The following package was not found and will be installed: oxlint@1.79.0` — preuve qu'il n'était pas encore une dépendance du projet). Les 5 tests préexistants restent verts.

**Après installation + config verbatim du brief + fixtures + script** (étape 6, avant correctif) — encore ROUGE, mais pour une raison différente :

```
$ npm run test:harness -- oxlint-base
...
     × refuse un any explicite
       expected 'No files found to lint. Please check …' to contain 'no-explicit-any'
     × refuse un nom de fichier hors kebab-case et PascalCase
       expected 'No files found to lint. Please check …' to match /filename-case/

 Test Files  1 failed | 2 passed (3)
      Tests  2 failed | 7 passed (9)
```

Investigation détaillée en § 5. Après correctif :

**VERT** (étape 6, après correctif) :

```
$ npm run test:harness -- oxlint-base
 Test Files  3 passed (3)
      Tests  9 passed (9)
```

**VERT confirmé à froid, après le commit** (nouvelle exécution, aucune mise en cache) :

```
$ npm run test:harness
 Test Files  3 passed (3)
      Tests  9 passed (9)
   Duration  16.58s
EXIT_HARNESS=0

$ npm run lint
> oxlint --ignore-pattern "tests/harness/fixtures/**" src tests
EXIT_LINT=0

$ npm run typecheck
> tsc --noEmit && tsc -p tsconfig.node.json --noEmit
EXIT_TYPECHECK=0

$ npm test
 Test Files  3 passed (3)
      Tests  9 passed (9)
EXIT_TEST=0
```

## 4. Preuve que chaque règle du brief s'applique réellement

D'abord, vérification que les 5 règles sont bien **reconnues** par Oxlint (pas silencieusement droppées comme le brief le redoute pour un nom mal écrit) :

```
$ npx oxlint --print-config | <extraction JSON des 5 clés>
typescript/no-explicit-any => "deny"
unicorn/filename-case      => ["deny",[{"cases":{"kebabCase":true,"pascalCase":true}}]]
import/no-cycle             => "deny"
```

Pour `eslint/no-unused-vars` et `eslint/no-console`, la clé apparaît dans la config résolue **sans** le préfixe `eslint/` (`no-unused-vars`, `no-console` — Oxlint normalise en interne le nom du plugin ESLint core), mais avec la même sévérité `"deny"` : la règle est bien active, ce n'est qu'un détail d'affichage interne, pas un rejet.

Ensuite, preuve empirique que chacune **mord** :

- `typescript/no-explicit-any` — épreuve `oxlint-base.test.ts`, test « refuse un any explicite » : vert, sortie contient `no-explicit-any`.
- `unicorn/filename-case` — épreuve `oxlint-base.test.ts`, test « refuse un nom de fichier... » : vert, sortie matche `/filename-case/`.
- `eslint/no-unused-vars`, `eslint/no-console`, `import/no-cycle` — pas de fixture dédiée dans le brief pour ces trois-là. Vérifiées manuellement avec des fichiers temporaires **hors dépôt suivi**, créés dans `front/_verif_tmp/` puis supprimés avant le commit (aucune trace dans `git status`) :

```
=== no-unused-vars ===
_verif_tmp/no-unused.ts:2:9: error eslint(no-unused-vars): Variable 'inutilisee' is declared but never used.
EXIT=1

=== no-console ===
_verif_tmp/no-console.ts:2:3: error eslint(no-console): Unexpected console statement.
EXIT=1

=== import/no-cycle ===
(cycle-a.ts <-> cycle-b.ts, deux fichiers en réexport mutuel)
_verif_tmp/cycle-b.ts:1:19: error import(no-cycle): Dependency cycle detected
_verif_tmp/cycle-a.ts:1:19: error import(no-cycle): Dependency cycle detected
EXIT=1
```

Ce dernier test a d'abord été lancé avec le flag CLI `--import-plugin` par précaution, puis rejoué **sans** ce flag pour confirmer que le plugin déclaré dans `.oxlintrc.json` (`"plugins": [..., "import", ...]`) suffit seul — c'est bien le cas, donc rien à changer dans le script `lint` ni dans l'épreuve pour cette règle.

Les 5 règles du brief sont donc actives et mordent réellement, aucune n'est acceptée-sans-effet.

## 5. La déviation — investigation et correctif

**Ce qui a cassé, et comment je l'ai rattrapé.** Le brief affirme, à l'étape 4 : « Les épreuves les atteignent en nommant leur chemin explicitement, ce qui contourne l'ignore. » Cette affirmation ne tient pas empiriquement pour Oxlint 1.79.0.

Démarche de vérification (root-cause avant correctif, pas de correctif au jugé) :

1. `.oxlintrc.json` verbatim du brief installé, `npx oxlint tests/harness/fixtures/any-explicite.ts` lancé directement (hors harnais) → `No files found to lint. Please check your paths and ignore patterns.`, code 1.
2. Test d'isolation : `.oxlintrc.json` renommé temporairement (donc sans `ignorePatterns`) → la même commande trouve le fichier (code 0). Confirme que c'est bien `ignorePatterns` la cause, pas un autre mécanisme (`.gitignore`, chemin mal résolu, etc.).
3. `npx oxlint --print-config` confirme que `ignorePatterns` contient bien `tests/harness/fixtures/**` tel qu'écrit, sans transformation surprenante.
4. `npx oxlint --no-ignore tests/harness/fixtures/any-explicite.ts` → même échec. Le flag documenté comme désactivant « tous les mécanismes d'ignore » ne débloque pas ce cas.
5. Documentation officielle interrogée via Context7 (`/websites/oxc_rs_guide_usage`, pages `linter/ignore-files.html`, `linter/config-file-reference.html`, et la page miroir du formateur qui partage le même mécanisme) : « The ignorePatterns setting is the recommended approach for excluding files... Files matched by these patterns cannot be formatted, even if they are explicitly specified. » — comportement volontaire, pas un bug de version.

**Correctif appliqué** : déplacer l'exclusion des fixtures hors du fichier de config persistant, vers un flag CLI propre au script `lint` — mécanisme distinct qui ne s'applique qu'à l'invocation qui le porte, donc invisible pour l'appel brut `npx oxlint <chemin>` que fait l'épreuve.

- `front/.oxlintrc.json` : `"ignorePatterns": ["dist/**", "coverage/**"]` (l'entrée `tests/harness/fixtures/**` retirée).
- `front/package.json` : `"lint": "oxlint --ignore-pattern \"tests/harness/fixtures/**\" src tests"` au lieu de `"oxlint src tests"`.

**Un deuxième problème découvert en testant ce correctif** : ma première tentative utilisait des guillemets simples (`--ignore-pattern 'tests/harness/fixtures/**'`), cohérent avec le style déjà présent dans `test:harness` (`--exclude ''`). `npm run lint` s'est alors mis à échouer en rapportant des erreurs sur les fixtures elles-mêmes — le motif littéral `'tests/harness/fixtures/**'` (apostrophes incluses) avait été transmis à Oxlint par `cmd.exe` (le shell que npm utilise par défaut sur Windows, qui ne traite pas les guillemets simples comme du quoting), donc il ne matchait rien. Remplacé par des guillemets doubles, qui sont interprétés à l'identique par `cmd.exe` et par `sh`/`bash` : `npm run lint` repasse vert (code 0), vérifié aussi depuis la racine du worktree via `npm --prefix front run lint`.

Les deux comportements demandés par le brief sont donc vérifiés séparément et positivement :
- lint global propre : `npm run lint` → code 0, aucune sortie.
- l'épreuve voit sa cible : les 2 tests concernés passent, sortie contenant bien `no-explicit-any` / `filename-case`.

**Conséquence pour les tâches 6 et 7** (qui « enrichissent `.oxlintrc.json` sans le recréer ») : le fichier final ne contient plus l'entrée fixtures dans `ignorePatterns` — l'exclusion des fixtures pour le lint courant vit désormais dans le script `lint` de `package.json`, pas dans le JSON. À prendre en compte si ces tâches ajoutent elles-mêmes des règles ou touchent au script `lint`.

## 6. Constats de relecture

- **Complétude** — les 8 étapes du brief sont faites : épreuve écrite, ROUGE constaté, Oxlint installé, config écrite (avec la déviation ci-dessus), fixtures créées, script ajouté et épreuve verte, lint global vérifié propre, commit fait.
- **Les règles s'appliquent-elles vraiment ?** — oui, les 5, prouvé en § 4 (2 par les fixtures officielles du brief, 3 par vérification manuelle temporaire nettoyée avant commit). Vérifié aussi qu'aucune n'est silencieusement acceptée (`--print-config` montre les 5 à `"deny"`).
- **Discipline** — aucune règle, aucun plugin, aucune fixture, aucun script au-delà de ce que le brief demande. Le seul écart est mécanique (où vit l'exclusion des fixtures), pas une addition de périmètre.
- **Non-régression** — les 5 tests préexistants passent toujours (confirmé à chaque exécution : 5/5 avant, 9/9 après). `npm run typecheck` et `npm test` (suite complète) repassés à froid après le commit, tous deux au vert.

## 7. Doutes

- **La déviation § 5 est la seule chose qui mérite vraiment votre arbitrage.** J'ai signalé, proposé un correctif validé empiriquement, et je l'ai appliqué plutôt que de bloquer — parce que le résultat est un échec *bruyant* et bien compris (pas le cas silencieux qu'un nom de règle mal écrit aurait produit), qu'il est entièrement réversible, et que « vérifier que les deux comportements fonctionnent » faisait explicitement partie de la tâche demandée. Si vous préférez un autre mécanisme (par exemple un fichier `.oxlintignore` séparé via `--ignore-path`, qui évite tout guillemet dans `package.json`), je peux basculer — je ne l'ai pas fait par défaut pour ne pas ajouter un fichier hors de la liste du brief sans validation.
- Le format `--print-config` montre `no-unused-vars`/`no-console` sans le préfixe `eslint/` dans la config résolue. Je le lis comme une normalisation interne inoffensive (la sévérité et le comportement sont corrects), mais je ne l'avais jamais rencontré avant cette tâche et je ne suis pas certain que ce soit garanti stable d'une version à l'autre.
- Je n'ai pas testé le comportement d'Oxlint sur `back/` ni sur aucun fichier C# — hors périmètre de cette tâche, mentionné pour mémoire.
- Le `--ignore-pattern` avec guillemets doubles a été vérifié sous Git Bash (l'outil Bash de cette session) et fonctionne. Je ne l'ai pas testé sous PowerShell natif ni sous un `cmd.exe` invoqué hors Git Bash — la CI (à définir dans une tâche ultérieure) tournera probablement sous Linux, où les guillemets doubles sont également corrects, donc le risque me semble faible mais je ne l'ai pas vérifié sur les trois environnements.

## 8. Skills Superpowers

- **Invoqué** : `superpowers:verification-before-completion` — chargé avant la rédaction de ce rapport pour cadrer la vérification finale. Je l'ai invoqué après avoir déjà committé plutôt qu'avant, ce qui n'est pas l'ordre que le skill prescrit (« before committing ») ; je l'ai compensé en relançant une vérification à froid *après* le commit (§ 3) pour m'assurer que l'état réellement committé est bien celui qui est vert, plutôt que de me fier à l'état d'avant-commit.
- **Non invoqués formellement, mais démarche suivie en pratique** :
  - `systematic-debugging` — je n'ai pas chargé le skill via l'outil, mais la démarche du § 5 (isoler la cause avant de corriger : test avec/sans config, `--print-config`, `--no-ignore`, documentation officielle, puis seulement ensuite le correctif) correspond à sa logique de cause racine avant remède. Je le signale plutôt que de prétendre l'avoir chargé.
  - `test-driven-development` — non chargé explicitement, mais l'ordre rouge/vert imposé par le brief a été respecté à la lettre (§ 3), y compris un second rouge intermédiaire imprévu (l'épisode `ignorePatterns`) avant le vert final.
  - `brainstorming` / `writing-plans` — non pertinents ici : cette tâche exécute une étape déjà planifiée et validée en amont (le plan `2026-08-20-lot-1-harnais-complet.md`), je n'ai pas eu de décision de conception à instruire.
  - `requesting-code-review` — non invoqué : le brief indique explicitement que la revue est un travail séparé, fait par l'agent appelant après ce rapport.
