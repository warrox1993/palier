# SDD ledger — plan: docs/superpowers/plans/2026-08-20-lot-1-harnais-complet.md

Worktree isolé : `.claude/worktrees/lot-1-execution`, branche `feat/lot-1-execution`, créée depuis `feat/lot-1-harnais` (13 commits) et non depuis `origin/main` — un worktree par défaut aurait perdu tout le lot.

## Scan de pré-vol — 20/08/2026

### Paires de tâches partageant un fichier ou une interface

| Paire           | Ce qui est produit / consommé                            | Constat                                                                             |
| --------------- | -------------------------------------------------------- | ----------------------------------------------------------------------------------- |
| T3 → T4 à T10   | l'emplacement de tout le front                           | **CONFLIT P1** — les tâches 4 à 10 viennent d'un plan où le front était à la racine |
| T2 → T4 à T19   | `lancerOutil(commande, options)`                         | OK, signature stable ; son emplacement dépend de P1                                 |
| T5 → T6         | `front/.oxlintrc.json`, section `rules` puis `overrides` | OK — T6 ajoute, n'écrase pas                                                        |
| T6 → T7         | la frontière front/domaine, en deux moitiés              | OK — T6 fait les imports, T7 les motifs. Complémentaires, pas redondantes           |
| T7 → T18        | `scripts/regles-projet.mjs`                              | **CONFLIT P2** — `verify` ne l'appelle pas                                          |
| T3 → T18        | `package.json` racine déclare `verify`                   | **CONFLIT P3** — le script n'existe qu'à T18                                        |
| T5 → T19        | `oxlint --fix` dans lint-staged                          | OK — Oxlint installé à T5, hooks à T19                                              |
| T11 → T12 à T17 | `back/Palier.sln`                                        | OK                                                                                  |
| T12 → T15       | `.editorconfig` lu par `dotnet format`                   | OK                                                                                  |
| T13 → T14       | `Palier.Domain.Tests` et son seuil de couverture         | OK                                                                                  |
| T18 → T19, T21  | `npm run verify`                                         | OK — un seul point de définition, c'est l'objet de la refonte                       |
| T9 → T18        | `front/knip.json` et son script                          | OK                                                                                  |
| T16 → T18       | `scripts/verifier-licences.mjs`                          | OK — présent dans `verify` sous le nom `licences`                                   |

### Cohérence interne de chaque tâche

| Tâche   | Constat                                                                                                                                  |
| ------- | ---------------------------------------------------------------------------------------------------------------------------------------- |
| 3       | OK — la seule à ne rien créer, et à tout déplacer. Comptage des tests avant/après explicite                                              |
| 4 à 10  | chemins relatifs sans préfixe : voir **P1**                                                                                              |
| 5, 6    | OK — noms de règles Oxlint vérifiés via Context7 (`typescript/no-explicit-any`, `eslint/no-restricted-imports`, `unicorn/filename-case`) |
| 7       | OK sur le fond ; le test appelle `node ../scripts/regles-projet.mjs`, chemin relatif dépendant de P1                                     |
| 11      | OK — la référence circulaire provoquée à l'étape 7 est le point fort du lot                                                              |
| 12 à 17 | OK — chemins depuis la racine, cohérents entre eux                                                                                       |
| 18      | voir **P2**                                                                                                                              |
| 19      | OK                                                                                                                                       |
| 20      | OK — `docs/decisions.md` existe déjà, la tâche y ajoute                                                                                  |
| 21      | OK — cinq jobs, empreintes SHA à relever                                                                                                 |

### Rulings

**Ruling P1 — le répertoire de travail est déclaré à chaque dispatch, il n'est pas déduit des chemins.**

Les tâches 4 à 10 sont reprises d'un plan où le front vivait à la racine ; leurs chemins (`tests/harness/…`, `tsconfig.fixtures.json`, `npm run typecheck`) sont donc corrects **si et seulement si** on les exécute depuis `front/`. Les tâches 11 à 21 utilisent des chemins depuis la racine (`back/Palier.sln`, `scripts/verify.mjs`).

Plutôt que de réécrire soixante chemins — opération mécanique à fort risque d'oubli silencieux — chaque dispatch porte son répertoire de travail :

- tâches 4 à 10 : `front/`
- tâches 3, 11 à 21 : la racine du dépôt

_Coût si erroné :_ un implémenteur créerait un fichier au mauvais endroit. Détecté immédiatement — l'épreuve de la tâche échouerait en ne trouvant pas sa cible, ce que le premier test de chaque épreuve vérifie explicitement.

**Ruling P2 — `verify` doit appeler `regles-projet.mjs`.**

La tâche 7 produit `scripts/regles-projet.mjs`, qui porte sept règles bloquantes — dont l'interdiction des calculs de conformité dans le front. La liste d'étapes de `verify` en tâche 18 ne le mentionne pas : le script existerait sans être appelé par rien, exactement le défaut que le 11/08 avait révélé sur un autre projet (un contrôle qui existe, fonctionne, et que rien n'appelle).

L'étape `['regles', ['node', 'scripts/regles-projet.mjs']]` est ajoutée à `verify`, entre `front:knip` et `back:format`. Le test de la tâche 18 qui vérifie la liste des contrôles doit inclure `regles`.

_Coût si erroné :_ aucun. Une étape de plus dans une liste.

**Ruling P3 — le trou de `verify` entre T3 et T18 est accepté et documenté.**

La tâche 3 crée un `package.json` racine déclarant `"verify": "node scripts/verify.mjs"`, script qui n'existe qu'à la tâche 18. Entre les deux, `npm run verify` échoue.

C'est acceptable : le hook de pré-envoi n'est installé qu'à la tâche 19, donc rien ne l'appelle avant. Mais l'implémenteur de la tâche 3 doit le savoir, sans quoi il tentera de « réparer » un script manquant. La mention est portée dans son dispatch.

_Coût si erroné :_ un implémenteur perdrait dix minutes à chercher un fichier absent.

## Exécution

Tâches 1 et 2 : livrées au lot précédent, aucun brief à produire (0 étape extraite, contrôle automatique).
19 briefs extraits, 128 étapes.

Task 3: rapport DONE_WITH_CONCERNS (commits b1f8875..fa94e82)
Vérifié par le contrôleur : `front/` existe, racine nettoyée, `package.json` d'orchestration présent, `npm --prefix front run test` → 1 fichier / 3 tests, identique à avant la réorganisation.

**Ruling P4 — trouvé par l'implémenteur, manqué par mon scan de pré-vol.**
L'étape 4 de la tâche 3 demandait de corriger `.husky/pre-commit` et `.husky/pre-push`. Ces fichiers n'existent pas : la tâche 11 de l'ancien plan, qui installait Husky, n'a jamais été exécutée, et dans le plan unifié c'est la tâche 19 qui le fait — après la réorganisation. L'étape était donc sans objet.
Le plan est corrigé : l'étape devient conditionnelle et explique pourquoi. La tâche 19 écrit déjà les hooks avec des chemins tenant compte de `front`/`back`.
_Coût si erroné :_ nul. Aucun hook n'existe, aucun n'est appelé avant la tâche 19.

**Ruling P5 — le commit scindé en deux est validé.**
Le brief prévoyait un commit unique. L'implémenteur en a fait deux, après avoir constaté qu'un commit unique cassait `git log --follow front/package.json` : recréer un `package.json` à la racine dans le même commit que le déplacement fait perdre la détection de renommage à git. Vérifié par `git diff --cached --find-renames`, corrigé, revérifié.
C'est exactement le raisonnement attendu : le brief disait quoi obtenir, pas comment tromper la détection de renommage.
_Coût si erroné :_ un commit de plus dans l'historique.

Task 3: signalé sans y toucher — `docs/16-projet.md` documente encore l'ancienne arborescence. Déjà inscrit dans la spec d'architecture § 13 parmi les sept documents à reprendre ; relève du contenu métier, donc de l'arbitrage du porteur du projet.

Task 3: revue — spec ✅, qualité approuvée, aucun critique ni important sur la tâche.
Le relecteur a vérifié indépendamment les deux affirmations du rapport : `git show --name-status -M b1f8875` donne 14 renommages R100 dont `package.json`, et `git log --follow front/package.json` restitue les 6 commits antérieurs. La scission en deux commits est confirmée justifiée.
Task 3: complete (commits b1f8875..fa94e82, revue propre)

Task 3: minor (deferred) : `front/package.json` garde `"version": "1.0.0"` face à `"0.1.0"` à la racine. Sans impact — aucun workspace npm déclaré. À trancher à la revue finale.
Task 3: minor (deferred) : le brief portait encore `git checkout -b feat/lot-1b-harnais-backend`, résidu du plan pré-fusion. Jamais exécuté, mais non signalé par l'implémenteur alors qu'il avait signalé l'étape 4 pour la même cause. Même origine que P4.
Task 3: important, hors périmètre : `docs/16-projet.md` § Arborescence montre encore `src/` et `tests/` à la racine. Aucune tâche du plan ne le corrige. Contenu métier — relève de l'arbitrage du porteur du projet, pas d'une correction silencieuse.

Task 4: revue — spec ✅, qualité approuvée, aucun critique ni important.
Le relecteur a vérifié les trois points sensibles indépendamment :

- l'épreuve discrimine la bonne raison d'échec : elle teste le code de sortie ET le motif du message. En ROUGE l'échec venait de TS5058 (config absente), qui ne correspond pas au motif — donc rouge pour la bonne raison, pas par accident.
- le test de cible manquante mord : la preuve n'est pas simulée, le rapport le montre rouge avant la création réelle de la fixture.
- `tsconfig.fixtures.json` isole : vérifié par `tsc --showConfig`, le champ `files` résolu ne contient que la fixture. Et `"exclude": []` n'est pas cosmétique — `extends` remplace `include`/`exclude` au lieu de les fusionner, sans quoi l'exclusion héritée aurait annulé l'inclusion.
  Task 4: complete (commit 7f93a99, revue propre)

Task 4: minor (deferred) : la vérification du motif d'erreur dépend du texte anglais de `tsc`. Aucune locale n'est fixée dans le dépôt aujourd'hui ; à surveiller si une locale française est introduite dans l'outillage.

Task 5: rapport terminé avec déviation (commit e553458). Oxlint 1.79.0, licence MIT.
Vérifié par le contrôleur : 9 tests verts (3 fichiers), `npm run lint` propre, les cinq règles du brief présentes dans `.oxlintrc.json` sous leurs noms Oxlint.

**Ruling P6 — la déviation est approuvée, et le plan avait tort. Erreur du contrôleur.**
Le brief affirmait que nommer explicitement un fichier en ligne de commande contourne `ignorePatterns`. C'est faux : l'implémenteur l'a vérifié empiriquement, et `--no-ignore` ne le contourne pas davantage. Conséquence si on l'avait suivi : les fixtures auraient été invisibles **pour leurs propres épreuves**, qui seraient passées au vert sans rien contrôler — le pire mode de défaillance possible pour cette suite.

Aggravant : j'avais déjà identifié exactement ce risque au scan de pré-vol du plan précédent, sous le ruling C5, en écrivant « ne pas les placer dans `ignores` de la flat config ». En réécrivant la tâche pour Oxlint, j'ai remis les fixtures dans `ignorePatterns` et reproduit le défaut que j'avais moi-même diagnostiqué.

Correctif retenu, celui de l'implémenteur : exclusion portée par `--ignore-pattern` dans le script `lint`, avec des **guillemets doubles** — sous `cmd.exe`, les guillemets simples ne délimitent pas et le motif échoue silencieusement. Le plan est corrigé aux trois endroits concernés.
_Coût si erroné :_ aucun. La configuration actuelle est vérifiée : lint propre sur le code réel, épreuves qui voient leurs cibles.

Task 5: minor (deferred) : incertitude sur la stabilité des noms internes affichés par `--print-config` (`no-unused-vars` sans préfixe `eslint/`) d'une version d'Oxlint à l'autre. Sans effet aujourd'hui.

## Revue complète du lot — 20/08/2026

Sept dimensions relues en parallèle, 20 constats soumis à un jury adversarial de
deux lentilles (reproduction, conséquence), 15 confirmés, 5 réfutés, plus une
critique de complétude qui a trouvé ce que la revue elle-même n'avait pas regardé.

**Verdict initial : le harnais ne protégeait réellement que sur trois garde-fous
sur huit.** Deux règles étaient supprimables sans qu'aucun test ne bouge, la
primitive partagée par toutes les épreuves rendait « refusé » quand l'outil
n'avait jamais tourné, et deux mécanismes déjà armés allaient dégrader la
protection sans un signal.

### Les quatre bloquants, corrigés et éprouvés (commit 63c8300)

| #   | Défaut                                                      | Preuve du correctif                                                      |
| --- | ----------------------------------------------------------- | ------------------------------------------------------------------------ |
| 1   | `--exclude ''` s'ajoute aux globs, il ne les remplace pas   | sonde `accessibilite.test.ts` : `test` → 3 fichiers, `test:harness` → 4  |
| 2   | `lancerOutil` déguisait 3 défaillances en refus             | les 4 épreuves rougissent quand on désarme ; celle du délai en 30 287 ms |
| 3   | `no-console`, `no-cycle`, `plugins` supprimables en silence | les 3 épreuves rougissent avec les règles retirées                       |
| 4   | `vitest.config.ts` hors typecheck et hors lint              | `testTimeouts` → TS2769 avec la suggestion exacte                        |

**Ruling P7 — le plan avait un quatrième endroit, et le ruling P6 en annonçait trois.**
L'étape 6 de la tâche 6 réécrivait le script `lint` sans `--ignore-pattern`,
défaisant P6 douze tâches avant qu'on ne s'en aperçoive. Le brief était déjà
extrait avec la valeur périmée. Corrigé aux deux endroits, brief régénéré, et
l'étape « vérifier que le front réel est propre » — présente en tâche 5,
disparue en tâche 6 — rétablie.
_Leçon :_ une correction de plan n'atteint son lecteur que si le brief est
régénéré. Le ledger ne remplace pas l'extraction.

**Ruling P8 — la doctrine des deux assertions devient uniforme.**
Quatre épreuves du plan (Prettier, couverture, `dotnet format`, gitleaks)
n'avaient qu'une assertion sur le code de sortie. Mesuré : Prettier n'étant pas
installé, l'épreuve T8 reproduite verbatim rend code 2 avec « No files matching
the pattern were found » — **au vert**, alors que Prettier n'a rien vérifié.
Les trois épreuves livrées s'en protégeaient déjà par une assertion sur le motif ;
les quatre autres l'ont désormais.

### Ce que la critique de complétude a ajouté

- **`npm run verify` est cassé** : `scripts/verify.mjs` n'a jamais existé. Connu
  et accepté (ruling P3), mais aucune dimension ne l'avait lancé.
- **Aucune des 22 épreuves ne lance un script npm** — elles éprouvent les
  _outils_, jamais les _commandes_ que le hook, `verify` et la CI exécutent
  réellement. Cause commune des bloquants 1 et 2. À traiter en tâche 18.
- **Lighthouse CI était absent du plan entier**, alors que `08-workflow.md` § 5
  en fait le 10e élément du harnais et § 9 un critère de sortie. Ajouté en T21.
- **Le ledger et les 21 briefs n'étaient pas versionnés** (`.gitignore` = `*`)
  alors qu'ils portent P1 à P8 et vivent dans un worktree qui se supprime. Corrigé.
- **Onze contradictions documentaires** entre le dossier et les décisions D1-D17
  (Supabase, Vercel, ESLint, Node 20, arborescence d'avant la tâche 3). Traitées
  en tâche 20 — ce n'est pas trancher, c'est propager une décision déjà prise.
- **Le coût de la boucle** : 15,4 s dont 10,7 s de jsdom, pour des épreuves qui
  ne touchent jamais le DOM. Passées en environnement node → 11,8 s.

### Ce qui n'a pas pu être vérifié

- Le comportement en CI sous Linux : la tâche 21 n'existe pas. Toutes les
  mesures sont sous Windows via cmd.exe. La branche `shell:false` de
  `lancerOutil` n'est pas couverte.
- Aucune violation provoquée dans un fichier **suivi par git** : la chaîne
  « fichier commité en violation → contrôle rouge » n'est pas éprouvée de bout
  en bout.
- Le backend entier, et les tâches 6 à 21 par construction.

_Incident de méthode, à ma charge :_ un script Python de correction du plan a
converti les 2 561 lignes du fichier en CRLF (`io.open` en mode `w` sans
`newline=''`). Détecté parce que le remplacement suivant ne trouvait plus son
motif, réparé avant tout commit. Sans ce hasard, `.gitattributes` l'aurait
masqué à la validation et le diff aurait été illisible.

## Tâches 6 à 10 — fil front, livrées

Commits `0b01150`, `3c66393`, `815990e`, `6567325`, `3aa3372`. Tests 20/3 → 34/7.
Installés : `oxlint-tsgolint` 7.0.2001, `prettier` 3.9.6, `knip` 6.32.2,
`jscpd` 5.0.16, `@playwright/test` 1.62.1, `@axe-core/playwright` 4.13.0.

**Ruling P9 — le ruling P6 vaut pour Prettier aussi, et le plan le reproduisait.**
L'étape 5 de la tâche 8 demandait de mettre les fixtures dans `.prettierignore`.
Mesuré par l'implémenteur : `prettier --check tests/harness/fixtures/format-casse.ts`
rend alors **code 0** avec « All matched files use Prettier code style! ». L'épreuve
serait passée au vert sur un fichier délibérément mal formaté. Exclusion portée par
les scripts via le motif nié `"!tests/harness/fixtures/**"`, et `.prettierignore`
porte un avertissement pour empêcher la régression.
_Leçon :_ P6 n'était pas une particularité d'Oxlint. **Tout outil qui a un fichier
d'exclusion a ce piège** — la question à poser à chaque nouvel outil est « un fichier
ignoré reste-t-il ignoré quand on le nomme explicitement ? », et la réponse est oui
pour Oxlint comme pour Prettier.

**Ruling P10 — une épreuve qui rougit ne rougit pas forcément pour sa raison.**
`knip.fixtures.json` tel qu'écrit au plan rougissait sur 11 fichiers et 2 dépendances :
l'épreuve serait passée **sans l'orphelin qu'elle prétend détecter**. Resserré à
`project: ["export-orphelin.ts"]` + `include: ["files"]`, puis vérifié par désarmement.
Même famille que P8, à l'autre bout : P8 dit qu'un code non nul ne prouve pas le refus ;
P10 dit qu'un rouge ne prouve pas la bonne cause.

Signalé sans y toucher : `scripts/*.mjs` à la racine n'est ni linté, ni formaté, ni
typé — Oxlint et Prettier vivent dans `front/` et ne remontent pas d'un cran.
À traiter en tâche 18 ou 21.

## Tâches 11 à 17 — fil backend, livrées

Commits `2931eea`, `9864f09`, `9ec8c49`, `318a59d`, `d32aa3d`, `7afc376`, `c089f11`.
13 épreuves vertes, solution à 0 avertissement, `Palier.Domain` à 100 % ligne,
branche et méthode. 24 dépendances vérifiées.

**Le franchissement de la référence circulaire, constaté :**

```
error MSB4006: Il existe une dépendance circulaire dans le graphique de
dépendance cible qui implique la cible "_GenerateRestoreProjectPathWalk".
```

C'est le garde-fou dont `01-conformite.md` § 3 dépend : un calcul de conformité ne
_peut pas_ atteindre la base. Vérifié en le provoquant, pas en lisant le `.csproj`.

**Ruling P11 — le seuil de couverture n'appliquait rien.**
Le collecteur VSTest de coverlet **ignore** `Threshold` : sa propre documentation
l'exclut. Une fonction non couverte sortait en **code 0**. Le seuil est déplacé dans
le `.csproj` via `coverlet.msbuild`, et `CollectCoverage=true` y est permanent — le
seuil ne peut plus être contourné en oubliant `--settings`.
_Leçon :_ un réglage accepté sans erreur par un fichier de configuration n'est pas
un réglage appliqué. C'est la même famille que « une règle mal nommée est acceptée
par `.oxlintrc.json` et ne fait rien ».

**Ruling P12 — une assertion négative est verte quand l'outil se tait.**
L'épreuve des vulnérabilités du plan affirmait `not.toMatch(/High|Critical/i)` sur du
texte libre : elle passait au vert si `dotnet list` n'imprimait rien du tout. Réécrite
sur le rapport JSON, elle affirme **positivement** que les cinq projets ont été
inspectés. Franchie avec `Newtonsoft.Json 12.0.3` (GHSA-5crp-9r3c-p9vr), retiré depuis.

**Six autres défauts du plan, mesurés et corrigés par l'implémenteur :**

1. `dotnet new sln` de .NET 10 crée un `.slnx`, pas `Palier.sln` — tout le plan aval
   en dépendait.
2. L'épreuve de rigueur restait **verte** quand on retirait `Directory.Build.props` :
   la fixture porte ses propres réglages. Troisième épreuve ajoutée sur `Palier.Domain`.
3. `.editorconfig` (T12) refusait les noms de test de T13 via CA1707. Exception
   `[**/*.Tests/**/*.cs]`, cette règle seule.
4. La fixture de couverture nommée `Double` était refusée par CA1720 : la compilation
   cassait **avant** toute mesure.
5. Mon motif `/threshold|seuil|coverage/i` matchait le **chemin**
   `back/coverage.runsettings` — faux positif de ma main. Remplacé par
   `/coverage is below the specified/i`.
6. `licenseExpression` n'existe pas sur l'API de recherche NuGet : les cinq paquets
   ressortaient « licence non standard ». Lu dans l'entrée de catalogue.

**Trouvé sur du code réel, pas sur des fixtures :** `Program.cs` en CRLF là où
`.editorconfig` exige LF, et le bras par défaut du `switch` sur `Sexe` jamais franchi
(91,66 % / 75 %). Quatre tests ajoutés.

**Ruling P13 — MPL-2.0 admise par exception nominative.**
Le contrôle de licences refuse `@axe-core/playwright`, or `CLAUDE.md` § 3 impose
axe-core nommément et `lightningcss` arrive en transitif de Vite 8. MPL-2.0 est un
copyleft **par fichier**, sans clause réseau : sa section 3.3 autorise la combinaison
avec du code propriétaire, la 3.2 n'oblige à publier que les fichiers modifiés. C'est
la différence de fond avec RPL-1.5, qui a motivé D13. Inscrite dans `EXCEPTIONS` avec
son motif et ce qui la rouvrirait — un besoin de patcher axe-core. La liste blanche
reste inchangée.
_Signalé :_ le contrôle ne lit que les dépendances **directes**. `lightningcss` lui
échappe aujourd'hui.

**Signalé, à traiter avant d'appliquer D12 :** MediatR et `Mediator.SourceGenerator`
publient tous deux un fichier de licence sans expression SPDX. Le contrôle les arrête
tous les deux — MediatR est donc refusé pour « pas d'expression », pas pour
« RPL-1.5 ». `Mediator.SourceGenerator` devra passer par `EXCEPTIONS` le jour où on
l'ajoutera.

## Revues des tâches 6 à 17, et leurs correctifs — 20/08/2026

Deux revues indépendantes, en lecture seule, chacune provoquant les violations
elle-même. Front : 3 critiques, 6 importants. Backend : **aucun critique**, 5 importants.
Épreuves après correctifs : **65 front, 19 backend**. Les 37 ajoutées couvrent toutes
des garde-fous qui existaient déjà et que rien ne surveillait.

**Ruling P9 étendu — le piège du fichier d'exclusion vaut pour TOUT outil.**
Constaté sur trois outils, aucune exception à ce jour :

| Outil                                                                                  | Ce qui trompe     | Effet                                                                                      |
| -------------------------------------------------------------------------------------- | ----------------- | ------------------------------------------------------------------------------------------ |
| Oxlint                                                                                 | `ignorePatterns`  | fichier ignoré même nommé en argument, `--no-ignore` ne l'annule pas                       |
| Prettier                                                                               | `.prettierignore` | `--check` sur la fixture rend **code 0** et « All matched files use Prettier code style! » |
| Playwright                                                                             | `testIgnore`      | l'argument positionnel filtre la liste **déjà collectée** → `Error: No tests found`        |
| La question à poser à chaque nouvel outil du harnais : *un fichier ignoré reste-t-il   |
| ignoré quand on le nomme explicitement ?* La réponse est oui partout jusqu'ici.        |
| La parade est toujours la même : **l'exclusion appartient à la commande qui n'en veut  |
| pas**, jamais au fichier de configuration — ou à une configuration dédiée à l'épreuve. |

**Ruling P14 — une clé inconnue peut désactiver la règle entière, en silence.**
Un `"_note"` posé dans les options de `no-restricted-imports` pour documenter le choix
de motifs a fait passer **9 violations à code 0**, sans erreur ni avertissement. Ce
n'est pas « la clé est ignorée » : c'est la règle qui cesse de s'appliquer.
_Conséquence de méthode :_ **aucun commentaire dans un fichier de configuration
d'outil.** Le motif d'un choix vit dans l'épreuve qui le protège, jamais dans le JSON
qu'il documente. Même famille que P11 (`Threshold` accepté et ignoré par le collecteur
VSTest) et que « une règle mal nommée est acceptée par `.oxlintrc.json` et ne fait rien ».

**Ruling P15 — Prettier neutralisait une règle bloquante.**
`chaine-en-dur` interdit la copie en dur dans le JSX — c'est ce qui garantit que tout
passe par i18next. Elle lisait **une ligne à la fois**, or Prettier (`printWidth: 100`)
coupe systématiquement le JSX : la violation disparaissait. Elle ne voyait pas non plus
`title`, `alt`, `placeholder`, `aria-label`, et produisait un faux positif sur
`(valeur: number) => valeur < 10`.
Deux garde-fous du même harnais, l'un annulant l'autre — et dans le sens où `npm run
format` est ce qu'on lance avant chaque commit.
Réécrite : analyse du fichier entier, ancrage sur `>…</` (la balise fermante distingue
un nœud de texte d'une comparaison), attributs cherchés dans les balises ouvrantes
seulement, commentaires neutralisés en conservant les décalages pour garder les
numéros de ligne justes.
_Limites assumées, à connaître :_ c'est une heuristique, pas un analyseur syntaxique.
Un nœud de texte contenant `<` ou `>` n'est pas vu ; une valeur d'attribut construite
par concaténation ou gabarit n'est pas vue ; seuls quatre attributs sont couverts.
L'alternative — l'API JS de TypeScript 7 — est exposée sous `unstable/*` et lance un
processus Go : trop fragile pour une règle bloquante.

**Ruling P2, deux occurrences de plus.** `lint:types` (dont les règles sont justifiées
par « des pertes de données silencieuses » dans la file de retry hors ligne),
`scripts/*.mjs` (ni linté, ni formaté, ni typé — alors que ces trois fichiers décident
si le reste du dépôt est conforme), et `audit:back` (dont le brief de la tâche 17
déclarait pourtant « appelé par `verify` »). Tous branchés.

**Ruling P16 — le contrôle de licences était aveugle à l'ordre des attributs XML.**
`/PackageReference\s+Include="…"/` exigeait que `Include` suive immédiatement le nom
d'élément. Mesuré à variable unique, même paquet, même fichier :

```
<PackageReference Version="5.7.0" Include="NHibernate" />  →  24 dépendances, toutes permissives.  code 0
<PackageReference Include="NHibernate" Version="5.7.0" />  →  nuget NHibernate LGPL-2.1-only        code 1
```

Une dépendance sous licence réciproque traversait D13 **pendant que le script annonçait
que tout était permissif**. Sur un contrôle juridique, un faux vert ne se tait pas :
il rassure. Corrigé, et le contrôle lit désormais aussi `Directory.Build.props` — un
paquet déclaré à la racine s'applique à tous les projets et n'apparaît dans aucun.

**Ce que NU1903 protège, et que personne n'avait nommé.** Ce n'est pas l'épreuve qui
arrête réellement un paquet vulnérable : c'est l'audit NuGet promu en erreur par
`TreatWarningsAsErrors`, qui casse la restauration avant que l'épreuve ne tourne. La
protection existait en double et la moitié efficace n'était documentée nulle part.
Elle l'est maintenant, dans le code que quelqu'un lira.

### Trouvé sur du code réel, pas sur des fixtures

- `App.tsx` portait une chaîne en dur, refusée par `chaine-en-dur`.
- `Program.cs` en CRLF là où `.editorconfig` exige LF.
- Le bras par défaut du `switch` sur `Sexe` jamais franchi : 91,66 % lignes / **75 %
  branches**, et non 100 %.
- `run-outil.ts:55` confondait `undefined` (tableau vide) et `''` — trouvé en étendant
  `lint:types` à `tests/`. Les deux cas ont désormais un test et un message distinct.
- Le `app.MapGet("/", () => "Hello World!")` du template répondait en clair sur la
  racine du domaine. Retiré ; `Palier.Api` n'expose plus aucune route jusqu'au lot 2.

### Incident du worktree partagé, à ne pas reproduire

**L'index git est partagé.** Deux agents commitant en parallèle : entre le `git add` de
l'un et son `git commit`, l'autre a indexé ses fichiers. Résultat, `468a7b4` porte
`back/Palier.Api/Program.cs` sous un message qui parle du front. Rien n'est perdu, le
contenu est correct, et l'historique n'a pas été réécrit — un `reset --soft` pendant
qu'un pair commite détruirait son travail.
**Parade, adoptée par les deux agents ensuite :** `git commit -m <msg> -- <chemins>`,
en une seule étape. La forme `git add` puis `git commit` laisse une fenêtre.

### Signalé, non traité

- **`scripts/*.mjs` n'a pas de `.prettierrc` à la racine.** Le formater depuis `front/`
  applique les **défauts** de Prettier — guillemets doubles, points-virgules —, l'inverse
  du style du dépôt. La tâche 18 devra ajouter une configuration à la racine avant de
  couvrir ces fichiers, sinon elle retournera leur style.
- **`.claude/settings.json`** est un second fichier suivi sans saut de ligne final.

## Tâches 18 à 21 — clôture du lot, 20/08/2026

Commits `25d985f`, `487a2f3`, `f6bae20`, `8f4b1d8`, `9cfcdb3`, `ba209e7`.
**115 épreuves de franchissement vertes** — 79 front sur 10 fichiers, 36 racine et
backend sur 9. `npm run verify` : **15 étapes, 129,1 s, code 0**.

**Ruling P17 — deux paquets npm du plan étaient des squats vides.**
`gitleaks` sur npm : version 1.0.0, dépôt `ycjcl868/gitleaks`, **aucun exécutable**,
un README pour tout contenu. `semgrep` sur npm : version 0.0.1. Le plan prescrivait
`npx gitleaks` et `npx semgrep` : le harnais aurait installé deux outils de sécurité
qui ne scannent rien, et les épreuves auraient dû être écrites autour de leur silence.
Remplacés par le binaire officiel (empreinte SHA-256 vérifiée, action composite en CI)
et `pipx install semgrep`.
_Leçon :_ pour un outil de sécurité, **vérifier que le paquet est le paquet officiel**
avant de l'installer. Un nom qui correspond ne prouve rien.

**Ruling P18 — `lancerOutil` ne lève PAS pour un binaire absent sous Windows.**
Mesuré : `cmd.exe` absorbe l'erreur et rend un code 1 ordinaire avec « n'est pas
reconnu en tant que commande interne », là où un `spawn` direct rendrait `ENOENT`.
La garde `if (r.error)` ajoutée aux correctifs de la revue **ne peut pas** attraper ce
cas quand `shell: true`. C'est la seconde assertion sur le motif (P8) qui l'a attrapé,
exactement comme elle est faite pour.
_Conséquence :_ la doctrine des deux assertions n'est pas une ceinture de sécurité
redondante sur Windows — c'est **la seule** protection contre « l'outil n'a pas tourné ».

**Ruling P19 — le harnais dépendait d'un état de machine.**
`npm run verify` échouait sur trois épreuves de secrets alors que le rapport de la
tâche 19 annonçait un code 0. Cause : `winget install Gitleaks.Gitleaks` réussit,
place le binaire sous `%LOCALAPPDATA%\Microsoft\WinGet\Packages\…` et **ne crée aucun
lien dans le PATH**. L'outil n'était appelable par son nom depuis aucun terminal.
Sur une machine neuve, `npm ci && npm run verify` échouait donc.
`scripts/outil-gitleaks.mjs` résout aux emplacements connus — PATH d'abord, puis les
dossiers de winget — et **lève** avec les commandes d'installation. Les deux branches
sont éprouvées en provoquant l'état, `LOCALAPPDATA` pointé sur un chemin inexistant.
_Leçon :_ un rapport d'agent qui dit « code 0 » se relance. Celui-ci était sincère —
son PATH portait le binaire au moment de la mesure — et faux vingt minutes plus tard.

**Ruling P20 — un `.prettierrc` manquait à la racine.**
Formater `scripts/*.mjs` depuis `front/` leur applique les **défauts** de Prettier —
guillemets doubles, points-virgules — soit l'inverse du style du dépôt. La tâche 18
aurait retourné le style des trois fichiers les plus critiques du projet. Configuration
posée à la racine **avant** le premier formatage, style vérifié sur diff.

### Ce que la mesure de `verify` dit

| étape            | s        |     | étape       | s         |
| ---------------- | -------- | --- | ----------- | --------- |
| front:format     | 2,6      |     | back:format | 20,6      |
| front:lint       | 2,3      |     | back:build  | 14,0      |
| front:lint:types | 2,9      |     | back:test   | 11,0      |
| front:typecheck  | 3,0      |     | licences    | 7,6       |
| **front:test**   | **39,9** |     | back:audit  | 12,7      |
| front:knip       | 5,0      |     | front:build | 2,8       |
| scripts:lint     | 2,2      |     |             |           |
| scripts:format   | 2,3      |     | **TOTAL**   | **129,1** |
| regles           | 0,3      |     |             |           |

**Le seuil de 90 s n'a pas été relevé** : l'avertissement se déclenche à chaque
exécution, et c'est le comportement voulu — `08-workflow.md` § 5 fait de la lenteur un
défaut à traiter, pas une contrainte à absorber. Le levier principal, sortir les
épreuves de franchissement de `front:test` (−39 s), **ne peut pas être appliqué
aujourd'hui** : `front/src/` ne contient aucun test, l'étape deviendrait vide et verte
sans rien contrôler. C'est un arbitrage pour le lot 2.
Gain obtenu au passage : `npm --prefix front exec --` coûtait 13,9 s de démarrage npm
pour 0,2 s d'outil ; l'appel direct au binaire ramène les deux étapes à 4,3 s.

### Trous fermés en chemin, sans y avoir été invité

- `scripts/verifier-licences.mjs` ne lisait que `front/package.json` : husky et
  lint-staged échappaient à D13. 24 → 27 dépendances vérifiées.
- **lint-staged aurait reformaté `format-casse.ts` au commit**, désarmant l'épreuve
  Prettier. Filtre ajouté, éprouvé, y compris sa branche de chemins Windows.
- Le hook de pré-commit a refusé le commit de la tâche 19 elle-même, parce que
  l'épreuve portait un littéral `sk-ant-…`. Assemblé depuis. Le garde-fou a mordu sur
  celui qui l'installait.

### À soumettre au porteur du projet

**D27 — `@lhci/cli` amène 10 vulnérabilités, dont 7 hautes.** La chaîne
`extract-zip → @puppeteer/browsers → puppeteer-core → lighthouse` porte un avis sur
`extract-zip` dont la plage est `*` : **aucune version corrigée, aucun `overrides` ne
peut le réparer**. Le déclarer en dépendance casserait `npm audit --audit-level=high`
du job `securite`, ou forcerait à en abaisser le seuil — ce qui masquerait de vraies
vulnérabilités du produit. Retenu : installation éphémère à version figée dans le seul
job `performance`. Le risque n'est pas éliminé, il est **déplacé hors de la vue de
`npm audit`**. C'est le point qui revient au porteur.

### Ce qui n'a été éprouvé nulle part

- **Rien n'a tourné sous Linux.** Toutes les mesures viennent de Windows via `cmd.exe`.
  Les motifs d'exclusion à guillemets doubles, la casse des chemins, `pipx`, la branche
  `shell: false` de `lancerOutil` et de `verify.mjs` : la CI est le premier endroit où
  ils seront éprouvés. `ci.yml` est validé **structurellement**, jamais exécuté.
- **Lighthouse CI n'a jamais tourné** : la configuration est vérifiée, pas la mesure.
- **semgrep n'a jamais tourné**, ni localement ni en CI.
- `core.hooksPath` est partagé entre tous les worktrees : `npx husky` l'a posé pour la
  checkout principale aussi, où `.husky/` n'existera qu'après fusion.
