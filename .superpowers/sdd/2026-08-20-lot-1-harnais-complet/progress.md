# SDD ledger — plan: docs/superpowers/plans/2026-08-20-lot-1-harnais-complet.md

Worktree isolé : `.claude/worktrees/lot-1-execution`, branche `feat/lot-1-execution`, créée depuis `feat/lot-1-harnais` (13 commits) et non depuis `origin/main` — un worktree par défaut aurait perdu tout le lot.

## Scan de pré-vol — 20/08/2026

### Paires de tâches partageant un fichier ou une interface

| Paire | Ce qui est produit / consommé | Constat |
|---|---|---|
| T3 → T4 à T10 | l'emplacement de tout le front | **CONFLIT P1** — les tâches 4 à 10 viennent d'un plan où le front était à la racine |
| T2 → T4 à T19 | `lancerOutil(commande, options)` | OK, signature stable ; son emplacement dépend de P1 |
| T5 → T6 | `front/.oxlintrc.json`, section `rules` puis `overrides` | OK — T6 ajoute, n'écrase pas |
| T6 → T7 | la frontière front/domaine, en deux moitiés | OK — T6 fait les imports, T7 les motifs. Complémentaires, pas redondantes |
| T7 → T18 | `scripts/regles-projet.mjs` | **CONFLIT P2** — `verify` ne l'appelle pas |
| T3 → T18 | `package.json` racine déclare `verify` | **CONFLIT P3** — le script n'existe qu'à T18 |
| T5 → T19 | `oxlint --fix` dans lint-staged | OK — Oxlint installé à T5, hooks à T19 |
| T11 → T12 à T17 | `back/Palier.sln` | OK |
| T12 → T15 | `.editorconfig` lu par `dotnet format` | OK |
| T13 → T14 | `Palier.Domain.Tests` et son seuil de couverture | OK |
| T18 → T19, T21 | `npm run verify` | OK — un seul point de définition, c'est l'objet de la refonte |
| T9 → T18 | `front/knip.json` et son script | OK |
| T16 → T18 | `scripts/verifier-licences.mjs` | OK — présent dans `verify` sous le nom `licences` |

### Cohérence interne de chaque tâche

| Tâche | Constat |
|---|---|
| 3 | OK — la seule à ne rien créer, et à tout déplacer. Comptage des tests avant/après explicite |
| 4 à 10 | chemins relatifs sans préfixe : voir **P1** |
| 5, 6 | OK — noms de règles Oxlint vérifiés via Context7 (`typescript/no-explicit-any`, `eslint/no-restricted-imports`, `unicorn/filename-case`) |
| 7 | OK sur le fond ; le test appelle `node ../scripts/regles-projet.mjs`, chemin relatif dépendant de P1 |
| 11 | OK — la référence circulaire provoquée à l'étape 7 est le point fort du lot |
| 12 à 17 | OK — chemins depuis la racine, cohérents entre eux |
| 18 | voir **P2** |
| 19 | OK |
| 20 | OK — `docs/decisions.md` existe déjà, la tâche y ajoute |
| 21 | OK — cinq jobs, empreintes SHA à relever |

### Rulings

**Ruling P1 — le répertoire de travail est déclaré à chaque dispatch, il n'est pas déduit des chemins.**

Les tâches 4 à 10 sont reprises d'un plan où le front vivait à la racine ; leurs chemins (`tests/harness/…`, `tsconfig.fixtures.json`, `npm run typecheck`) sont donc corrects **si et seulement si** on les exécute depuis `front/`. Les tâches 11 à 21 utilisent des chemins depuis la racine (`back/Palier.sln`, `scripts/verify.mjs`).

Plutôt que de réécrire soixante chemins — opération mécanique à fort risque d'oubli silencieux — chaque dispatch porte son répertoire de travail :

- tâches 4 à 10 : `front/`
- tâches 3, 11 à 21 : la racine du dépôt

*Coût si erroné :* un implémenteur créerait un fichier au mauvais endroit. Détecté immédiatement — l'épreuve de la tâche échouerait en ne trouvant pas sa cible, ce que le premier test de chaque épreuve vérifie explicitement.

**Ruling P2 — `verify` doit appeler `regles-projet.mjs`.**

La tâche 7 produit `scripts/regles-projet.mjs`, qui porte sept règles bloquantes — dont l'interdiction des calculs de conformité dans le front. La liste d'étapes de `verify` en tâche 18 ne le mentionne pas : le script existerait sans être appelé par rien, exactement le défaut que le 11/08 avait révélé sur un autre projet (un contrôle qui existe, fonctionne, et que rien n'appelle).

L'étape `['regles', ['node', 'scripts/regles-projet.mjs']]` est ajoutée à `verify`, entre `front:knip` et `back:format`. Le test de la tâche 18 qui vérifie la liste des contrôles doit inclure `regles`.

*Coût si erroné :* aucun. Une étape de plus dans une liste.

**Ruling P3 — le trou de `verify` entre T3 et T18 est accepté et documenté.**

La tâche 3 crée un `package.json` racine déclarant `"verify": "node scripts/verify.mjs"`, script qui n'existe qu'à la tâche 18. Entre les deux, `npm run verify` échoue.

C'est acceptable : le hook de pré-envoi n'est installé qu'à la tâche 19, donc rien ne l'appelle avant. Mais l'implémenteur de la tâche 3 doit le savoir, sans quoi il tentera de « réparer » un script manquant. La mention est portée dans son dispatch.

*Coût si erroné :* un implémenteur perdrait dix minutes à chercher un fichier absent.

## Exécution

Tâches 1 et 2 : livrées au lot précédent, aucun brief à produire (0 étape extraite, contrôle automatique).
19 briefs extraits, 128 étapes.

Task 3: rapport DONE_WITH_CONCERNS (commits b1f8875..fa94e82)
Vérifié par le contrôleur : `front/` existe, racine nettoyée, `package.json` d'orchestration présent, `npm --prefix front run test` → 1 fichier / 3 tests, identique à avant la réorganisation.

**Ruling P4 — trouvé par l'implémenteur, manqué par mon scan de pré-vol.**
L'étape 4 de la tâche 3 demandait de corriger `.husky/pre-commit` et `.husky/pre-push`. Ces fichiers n'existent pas : la tâche 11 de l'ancien plan, qui installait Husky, n'a jamais été exécutée, et dans le plan unifié c'est la tâche 19 qui le fait — après la réorganisation. L'étape était donc sans objet.
Le plan est corrigé : l'étape devient conditionnelle et explique pourquoi. La tâche 19 écrit déjà les hooks avec des chemins tenant compte de `front`/`back`.
*Coût si erroné :* nul. Aucun hook n'existe, aucun n'est appelé avant la tâche 19.

**Ruling P5 — le commit scindé en deux est validé.**
Le brief prévoyait un commit unique. L'implémenteur en a fait deux, après avoir constaté qu'un commit unique cassait `git log --follow front/package.json` : recréer un `package.json` à la racine dans le même commit que le déplacement fait perdre la détection de renommage à git. Vérifié par `git diff --cached --find-renames`, corrigé, revérifié.
C'est exactement le raisonnement attendu : le brief disait quoi obtenir, pas comment tromper la détection de renommage.
*Coût si erroné :* un commit de plus dans l'historique.

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
*Coût si erroné :* aucun. La configuration actuelle est vérifiée : lint propre sur le code réel, épreuves qui voient leurs cibles.

Task 5: minor (deferred) : incertitude sur la stabilité des noms internes affichés par `--print-config` (`no-unused-vars` sans préfixe `eslint/`) d'une version d'Oxlint à l'autre. Sans effet aujourd'hui.
