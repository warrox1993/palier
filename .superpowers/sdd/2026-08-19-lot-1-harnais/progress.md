# SDD ledger — plan: docs/superpowers/plans/2026-08-19-lot-1-harnais.md

## Scan de pré-vol — 19/08/2026

### Paires de tâches partageant un fichier ou une interface

| Paires | Ce qui est produit / consommé | Constat |
|---|---|---|
| T1 → T2,T4,T7,T8,T9,T10,T11 | `package.json`, champ `scripts` | OK — ajouts successifs, aucun écrasement |
| T2 → T3,T4,T5,T6,T7,T8,T9,T11 | `lancerOutil(commande, options)` | OK — signature identique partout |
| T4 → T5 | `eslint.config.js`, bloc architecture inséré | OK — insertion avant `prettier` |
| T4 → T6 | `eslint.config.js`, bloc design inséré + import `i18next` en tête | OK, mais fragile : l'import doit être ajouté au même moment que le bloc |
| T5 → T6 | `eslint.config.js`, ordre des blocs | OK — blocs disjoints par `files` |
| T1 → T3 | `tsconfig.json` (exclut les fixtures) → `tsconfig.fixtures.json` (extends) | OK |
| T7 → T10 | script `format:check` consommé par `verify` | OK |
| T10 → T11 | `npm run verify` appelé par `.husky/pre-push` | **CONFLIT C1** |
| T10 → T13 | `npm run verify` appelé par la CI | **CONFLIT C1** |
| T9 → T13 | script `e2e` consommé par le job `e2e` | OK |
| T8 → T12 | faux positifs Knip à consigner dans `docs/decisions.md` | **CONFLIT C2** |
| T2 → T13 | `vitest.config.ts` inclut `tests/harness/**` ; le job `qualite` lance `verify` sans navigateurs | **CONFLIT C3** |

### Cohérence interne de chaque tâche

| Tâche | Constat |
|---|---|
| T1 | OK — les fichiers créés correspondent aux étapes |
| T2 | OK, mais l'étape 5 demande de voir échouer un test dont l'implémentation vient d'être écrite à l'étape 3 : **CONFLIT C4** (ordre rouge-vert inversé) |
| T3 | OK — épreuve avant fixture, cible manquante testée |
| T4 | Étape 7 conditionnelle sur `ignores` vs `--no-ignore` : **CONFLIT C5** |
| T5 | OK |
| T6 | OK |
| T7 | OK — l'ordre `.prettierignore` après l'épreuve est explicite |
| T8 | OK sur la structure ; `jscpd src \|\| exit 0` fragile sous Windows : **CONFLIT C6** |
| T9 | OK — le test-fixture est conçu pour échouer, c'est explicite |
| T10 | Voir C1 |
| T11 | OK |
| T12 | OK |
| T13 | Voir C3 |

### Rulings

**Ruling C1 — les tests de T10 sur le hook et la CI migrent vers T11 et T13.**
L'épreuve de T10 lit `.husky/pre-push` et `.github/workflows/ci.yml`, qui n'existent qu'aux tâches 11 et 13. Écrite telle quelle, elle échoue de façon irrécupérable. T10 conserve « verify existe » et « verify enchaîne les six contrôles » ; le test du hook rejoint T11, celui de la CI rejoint T13.
*Coût si erroné :* nul — les trois tests existent toujours, seul leur emplacement change.

**Ruling C2 — T8 ne consigne rien dans `docs/decisions.md`, qui n'existe qu'en T12.**
T8 note le compte de faux positifs Knip dans son rapport de tâche ; T12 les reprend dans le journal.
*Coût si erroné :* le compte pourrait être perdu si T12 ne le reprend pas. Mitigé par le rapport de tâche, conservé dans le workspace.

**Ruling C3 — `npm run test` ne lance pas les épreuves qui exigent Playwright.**
`vitest.config.ts` incluait `tests/harness/**` ; `verify` appelle `test` ; le job `qualite` de la CI n'installe pas les navigateurs. L'épreuve d'accessibilité de T9 y échouerait pour une raison sans rapport avec ce qu'elle mesure. Séparation : `test` couvre `src/**` et `tests/harness/**` **sauf** `accessibilite.test.ts` ; un script `test:harness` couvre l'ensemble et n'est appelé que par le job `franchissement`, qui installe les navigateurs.
*Coût si erroné :* si la séparation est mal faite, une épreuve cesse d'être exécutée sans que personne le voie — exactement le défaut que cette suite combat. Le job `franchissement` lance `tests/harness` en entier, ce qui referme le risque.

**Ruling C4 — l'ordre des étapes de T2 est corrigé : test d'abord, implémentation ensuite.**
Le plan faisait écrire `run-outil.ts` à l'étape 3 puis « vérifier qu'il échoue » à l'étape 5, avec une note en rattrapage. Le cycle rouge-vert n'admet pas de note en rattrapage : les étapes sont réordonnées.
*Coût si erroné :* nul.

**Ruling C5 — les fixtures ne vont pas dans `ignores` de la flat config.**
Le comportement de `--no-ignore` face au champ `ignores` d'ESLint 9 n'est pas garanti. À la place, le script `lint` cible explicitement `src` et exclut les fixtures par `--ignore-pattern` ; les épreuves pointent le fichier de fixture directement.
*Coût si erroné :* si `--ignore-pattern` ne suffit pas, `npm run lint` remonte les violations délibérées et devient rouge en permanence. Détecté immédiatement à T4.

**Ruling C6 — `jscpd` ne s'appuie pas sur `|| exit 0`.**
Sous Windows, le script npm passe par cmd et le repli d'échec n'est pas garanti. Le caractère non bloquant est porté par la CI, qui lance `npm run jscpd` en `continue-on-error`.
*Coût si erroné :* jscpd bloquerait le job qualité alors que la spec le classe en avertissement. Corrigé en une ligne.

## Exécution

Ruling (outillage) : `scripts/task-brief` ne reconnaît que les titres « Task N » en anglais ; le plan est en français. Extraction par `extraire-brief.mjs` (dans le workspace), qui produit le même artefact — texte intégral de la tâche précédé des contraintes globales. Coût si erroné : un brief incomplet passerait inaperçu ; mitigé par un contrôle du nombre d'étapes extraites, qui échoue si aucune n'est trouvée.

Task 1: dispatché (implémenteur sonnet, BASE c39a394)
Task 1: rapport DONE_WITH_CONCERNS (commits fb6ef6c..ac2aedd)

Constats du contrôleur, vérifiés sur le dépôt et non sur le rapport :
- `npm run typecheck` code 0 ; `npm run build` produit dist/ en 201 ms, 60,02 ko compressés (budget : 200 ko)
- les 8 fichiers du brief sont présents ; `.nvmrc` = 24 ; `src/core/index.ts` conforme
- `tsconfig.json` porte bien `"exclude": ["tests/harness/fixtures"]` — la condition des tâches 3 à 11 est remplie
- **DÉFAUT : `package.json` porte `"name": "appmuscu"` et la description du README**, hérités de `npm init -y`, plus `main`, `directories`, `keywords`, `license: ISC`. Le projet s'appelle `palier`.

Déviations du brief à arbitrer après la revue :
- `baseUrl` retiré de `tsconfig.json` (TS5101 en TypeScript 6+) ; `paths` conservé en `./src/*`
- `typescript` installé en `^6.0.3` au lieu de sans version
- `@types/node` ajouté, absent du brief
- `tsconfig.node.json` rempli d'un contenu que le brief ne spécifiait pas

**Signalement — messages reçus par le sous-agent.** Son rapport mentionne « deux messages reçus en cours de tâche » lui demandant de privilégier les versions LTS et des exemples GitHub réels, puis une « application ultra solide ». Ces messages ne sont pas passés par la conversation principale : le contrôleur ne peut ni les vérifier ni les tenir pour validés. Les changements qu'ils ont motivés sont jugés sur leur seul mérite technique, comme toute autre déviation. À porter à la connaissance du porteur du projet.

Task 1: relancé par l'utilisateur en direct → 3e commit 745ce96 (retour à TypeScript ^7.0.2, sans version figée).
État final vérifié par le contrôleur : arbre propre, typecheck / tsconfig.node / build tous verts, aucun .mcp.json créé.
La revue dispatchée portait sur c39a394..ac2aedd et est donc périmée d'un commit : son verdict sur `typescript@^6` (version figée) est caduc, 745ce96 l'ayant annulé. Paquet complet régénéré sur c39a394..745ce96.
Ruling : ne pas relancer une revue complète pour un commit de retour arrière — le verdict entrant est lu en écartant ce seul point, et le défaut restant (`package.json` name/description) entre dans la boucle de correction. Coût si erroné : un défaut introduit par 745ce96 passerait inaperçu ; mitigé par le fait que ce commit ne touche que package.json et package-lock.json, que le contrôleur a lus.

Task 1: revue — spec ❌ (1 critique), qualité approuvée. Le relecteur a détecté 745ce96 de lui-même et l'a intégré.

Ruling N1 — `src/app/app.tsx` est renommé `src/app/App.tsx`.
`16-projet.md` § 2 impose `PascalCase.tsx` aux composants ; le fichier exporte `App`, un composant. La contradiction vient de mon brief, qui donnait le chemin en minuscules — le finding est fondé et le plan a tort. Le dossier `src/app/` reste en minuscules : c'est un répertoire de routes, pas un composant.
Coût si erroné : aucun à ce stade ; le même renommage coûterait un import cassé par fichier plus tard.

Ruling N2 — `typecheck` couvre les deux configurations TypeScript.
`tsconfig.node.json` type-checke réellement mais aucune commande ne l'appelle : `vite.config.ts` n'est donc typé par rien d'automatique. Le script devient `tsc --noEmit && tsc -p tsconfig.node.json --noEmit`, ce qui le fait entrer dans `verify` et dans la CI sans effort supplémentaire.
Coût si erroné : une erreur de type dans un fichier de configuration passerait jusqu'au build.

Ruling N3 — les mineurs de `package.json` sont corrigés avec le critique.
Le skill réserve la boucle aux critiques et importants, les mineurs allant au ledger. Ici `description`, `main`, `author`, `license` et `keywords` sont dans le même fichier et la même ligne d'édition que le `name` critique : les séparer coûterait un second aller-retour pour un gain nul.
Coût si erroné : élargissement marginal du périmètre d'un correctif, visible au diff.

Task 1: fix round 1/5 — 4 constats traités selon vérification du contrôleur, re-revue ciblée dispatchée (commits 2a769c2..851fb6f)
Vérifié sur le dépôt : name=palier, description juste, license UNLICENSED, résidus npm init retirés, renommage enregistré par git en R100 (app.tsx -> App.tsx), import corrigé dans main.tsx, typecheck enchaîne les deux tsconfig, typecheck et build verts, arbre propre.

Ruling N4 — `"private": true` est conservé.
Ajouté par l'implémenteur sans instruction, signalé par lui comme un doute. C'est le complément standard d'`UNLICENSED` sur un dépôt privé : il empêche une publication npm accidentelle. Le coût est nul, le bénéfice réel, et le retirer demanderait un aller-retour pour rien.
Coût si erroné : aucun tant que le paquet n'est pas destiné à npm — ce qu'il n'est pas.

Ruling N5 — les cinq commits de la tâche 1 ne sont pas écrasés.
L'implémenteur propose de les squasher. L'historique porte la trace d'un incident réel (TypeScript 7 refusant `baseUrl`) et de son diagnostic ; l'écraser effacerait ce que la prochaine session devra savoir si le problème réapparaît.
Coût si erroné : un historique un peu bavard sur la première tâche du projet.

Task 1: fix round 1/5 (4 traités, 0 ouvert ; commits 2a769c2..851fb6f) — re-revue : aucune casse
Task 1: complete (commits fb6ef6c..851fb6f, revue propre)
Task 1: minor (deferred) : package-lock.json ne mirrore pas `private: true` dans packages[""] — sans effet, npm publish lit package.json. À signaler à la revue finale.
