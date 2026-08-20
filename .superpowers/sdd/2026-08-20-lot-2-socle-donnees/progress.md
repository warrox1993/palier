# SDD ledger — plan : docs/superpowers/plans/2026-08-20-lot-2-socle-donnees.md

Worktree partagé : `.claude/worktrees/lot-1-execution`, branche `main`. **Plusieurs agents y travaillent en même temps**, et cela a eu des conséquences mesurables — voir « Incidents du worktree partagé ».

---

## Tâches 9 à 13 — clôture du lot, 20/08/2026

### Tâche 9 — `GET /api/v1/sante`, `back/.env.example`, et l'assertion de démarrage

**Livré.** `back/.env.example` (les trois chaînes de D37, aucune valeur), `back/Palier.Api/Composition.cs`, `back/Palier.Api/Socle/` (trois fichiers), et la première route du projet — `/api/v1` **dès la première route**.

**L'assertion de démarrage refuse de servir, et elle refuse AVANT que le serveur écoute.** Elle implémente `IHostedLifecycleService` et travaille dans `StartingAsync`, pas dans `StartAsync` : `GenericWebHostService` — Kestrel — est enregistré par `WebApplication.CreateBuilder`, donc **avant** elle. Dans `StartAsync`, le refus tomberait après le début de l'écoute.

Mesure, chaîne du rôle propriétaire :

```
Palier.Api.Socle.IsolationNonGarantieException: L'API refuse de servir : l'isolation par
ligne n'est pas garantie sur cette instance. Le rôle « palier_migrations » est PROPRIÉTAIRE
de 13 table(s) de `public` : AspNetRoleClaims, AspNetRoles, … workouts. « Table owners
normally bypass row security as well » — …
   at Palier.Api.Socle.AssertionDIsolation.VerifierAsync(…)
   at Palier.Api.Socle.AssertionAuDemarrage.StartingAsync(…)
```

`grep -c "Now listening" api-refus.log` → **0**. Kestrel n'a jamais écouté.

Et sous la bonne chaîne, la ligne de journal de déploiement, **avant** l'ouverture du port :

```
info: Palier.Api.Socle.AssertionAuDemarrage[2037]
      Isolation vérifiée sur la base réelle : rôle « palier_app », 13 table(s) de `public`,
      toutes avec RLS activée et forcée, aucune possédée par ce rôle.
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5025
```

**UNE QUATRIÈME REQUÊTE A ÉTÉ AJOUTÉE, et c'est la plus importante.** Le brief en prévoyait trois. Sur une base **non migrée**, les trois listes sortent vides et les trois contrôles passent au vert **sans avoir rien regardé** : l'API démarrerait, sur une base sans schéma, en ayant « vérifié » l'isolation. La quatrième compte les tables de `public` et refuse à zéro. Elle est franchie sur une base neuve **réellement créée**, pas sur une liste vide construite à la main.

**Écart au brief, signalé.** L'étape 2 demandait que la route rende « la version de migration appliquée ». Mesuré sous `palier_app` :

```
ERROR:  permission denied for table __EFMigrationsHistory
```

Le rôle n'avait ni le privilège ni de politique — sous `FORCE`, il faut les deux. La migration accorde donc `select` et une politique de **lecture seule** sur cette seule table. C'est une modification d'un artefact de la tâche 6 depuis la tâche 9 ; elle est écrite en toutes lettres dans la migration.

**Les deux épreuves de journalisation existent, et une TROISIÈME les garde.** Elles ne lisent aucun fichier : elles provoquent un vrai échec SQL sous `palier_app`, capturent ce qu'EF Core a réellement écrit, et cherchent le poids et l'identifiant dedans. L'épreuve **inversée** — `EnableSensitiveDataLogging` actif — vérifie que le détecteur les **trouve** quand ils sont là. Sans elle, la première serait verte parce qu'EF ne journalise jamais les paramètres, et non parce que la configuration l'en empêche.

La troisième **construit la configuration de PRODUCTION** de l'API — pas une copie — et assertionne `IsSensitiveDataLoggingEnabled` à faux. C'est ce qui a imposé de sortir la composition de `Program.cs` : recopier ces lignes dans l'épreuve aurait éprouvé la copie.

**Le trou du test de réflexion est fermé.** Il couvrait constructeur, propriété et champ ; `void Poser(PalierDbContext)` passait intégralement. Il couvre désormais **quatre** voies, et porte une liste d'**exemptions nommées** avec leur motif écrit dans l'épreuve (D21), gardée par une épreuve qui refuse une exemption dont la cible n'existe plus.

### Tâche 10 — sauvegarde et restauration sous `FORCE`

**Le verdict sur `BYPASSRLS`, mesuré dans les deux sens.** Il est **NÉCESSAIRE**. Le tableau complet est dans D37 et dans `db/README.md`.

**Trois choses que le plan ne savait pas :**

1. **`BYPASSRLS` contourne les POLITIQUES, jamais les PRIVILÈGES.** `palier_sauvegarde` n'avait **aucun** `select` : il existait, portait l'attribut, et ne pouvait rien sauvegarder.
2. **Les séquences aussi** — `AspNetRoleClaims_Id_seq` bloque le dump même quand toutes les tables sont accordées.
3. **UN DUMP RATÉ RESSEMBLE À UN DUMP** : 40 829 octets contre 41 287. `ls -l` ne les distingue pas ; seul le code de sortie le fait.

**La branche « dump réussi » EST obtenue**, et la restauration aussi — base neuve, sous `palier_migrations`, même nombre de lignes, de tables, de politiques, et **zéro table sans `FORCE`**. `FORCE` ne s'échange contre rien sur Testcontainers. La seule inconnue restante est l'instance managée, et elle n'est pas mesurable avant de l'avoir.

**La sauvegarde et la restauration se font sous DEUX rôles différents.** `palier_sauvegarde` sait lire et ne crée rien ; `palier_migrations` crée et, sous `FORCE`, ne lit pas tout. Écrit dans `db/README.md` avec les trois commandes exactes et le tableau « ce qui échoue si l'on se trompe de rôle ».

### Tâche 11 — l'écran d'état, ses quatre états, le parcours au clavier

**D31 est honorée. Les trois livrables sont là.** Le verdict est écrit en toutes lettres dans D31 elle-même, pas seulement ici.

**La violation 2 — la seule qui compte — a été PROVOQUÉE**, sur un vrai navigateur, un vrai serveur de développement et une vraie API :

```
1. base levée, écran chargé       -> contenu
2. npm run db:down ...
3. conteneur arrêté, après action -> erreur
   texte affiché                  -> Le socle de données ne répond pas
                                     La base est arrêtée ou injoignable. …
4. npm run db:up ...
5. base relevée, après action     -> contenu
```

L'action a été déclenchée **au clavier** (`Tab`, `Tab`, `Entrée`). Aucun rechargement truqué, aucun bouchon.

**Ce que le franchissement a appris, et qui n'était pas prévu.** `vite preview` rend `index.html` avec un code **200** sur toute route inconnue : sans contrôle du type de contenu, l'écran aurait cru recevoir un état. Le module d'accès refuse donc un corps qui n'est pas du JSON — et ce refus donne aux épreuves Playwright un état d'erreur **déterministe**, sans qu'aucune donnée soit simulée. `vite.config.ts` renvoie `/api` vers l'API en **développement seulement** ; la prévisualisation n'est délibérément pas renvoyée.

**Knip a retrouvé ses deux dépendances.** L'exception `ignoreDependencies` est **levée** : les deux `@testing-library/*` sont réellement employées, et `npm run knip` reste vert sans elle. L'épreuve inversée de `code-mort.test.ts` a été reprise dans l'autre sens — elle refuse désormais que l'exception **revienne**. Il reste une exception, `src/core/index.ts`, dont la condition de sortie est inchangée.

### Tâche 12 — remesurer `verify`

Cinq exécutions : **278,1 s**, **276,8 s**, **338,0 s**, **340,2 s** et **281,7 s**, seize étapes, toutes vertes. La mesure de référence est **280 s ± 3 s** ; les deux valeurs à 338-340 s ont tourné pendant qu'un second agent compilait dans le même worktree. Le tableau étape par étape est dans **D26**, remesurée.

**Aucun levier n'a été appliqué** — ils appartiennent au porteur. Les deux violations du franchissement ont été provoquées :

| Violation                                                             | Refus obtenu                                                                                                           |
| --------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------- |
| retirer l'étape `regles` de `scripts/verify.mjs`                      | `AssertionError: Contrôle manquant dans verify : regles` — l'étape est **nommée**                                      |
| faire énumérer `dotnet test` au hook de pré-envoi au lieu de déléguer | `AssertionError: le hook délègue, il n'énumère pas` — le garde-fou de D25 mord **après** l'ajout de Docker à la boucle |

`scripts/verify.mjs` et `tests-harness/verify.test.mjs` sont **inchangés** : rien à committer pour cette tâche.

### Tâche 13 — reprise documentaire

`docs/03-donnees.md` : les deux points ouverts **fermés** par D35 et D36 · les trois `auth.uid()` **substitués**, une fois, avec les trois changements que la substitution impose (`force`, `to palier_app`, sous-select) · la **liste nommée** des tables hors du modèle `owner_id` avec la forme de politique de chacune, les sept tables d'identité comprises.

`docs/16-projet.md` : l'arborescence gagne la racine `db/` et `back/.env.example` · le § 4 sépare **référentiel** et **démonstration**, que le mot « seed » confondait.

`docs/securite/asvs-l2.md` : V8 déplace RLS du lot 4 au **lot 2**, et V16 gagne les deux épreuves de journalisation.

`docs/decisions.md` : D26, D31, D34, D36, D37 et D41 confrontées à ce que le lot a mesuré.

`DEMARRAGE.md` § 0 et la spec d'architecture § 5 ligne 108 étaient **déjà à jour** — vérifié, rien réécrit.

---

## Incidents du worktree partagé, à ne pas reproduire

1. **`git commit -m … -- <chemins>` ne prend QUE les fichiers déjà suivis.** Un fichier **neuf** nommé en argument est ignoré **en silence**, sans un message. `back/Palier.Database.Tests/SauvegardeTests.cs` — les cinq épreuves de la tâche 10 — est resté dehors, et il a fallu un commit de rattrapage. Corollaire : `git add` explicite avant, toujours.
2. **Un pathspec trop large emporte le travail d'un autre agent.** `git commit -- front` a fait entrer dans lint-staged un `front/src/sonde-autofix.ts` posé par un agent voisin, avec une violation délibérée dedans : le hook a refusé, et le commit a été annulé. Les commits suivants nomment les chemins **un par un**.
3. **L'index est partagé.** À deux reprises, `CLAUDE.md` et `docs/decisions.md` se sont trouvés **indexés** sans que ce soit mon fait. `git commit -- <chemins>` les a laissés tranquilles ; `git add -A` les aurait emportés.

---

## Ce que je signale sans y avoir touché

1. **`npm run verify` était ROUGE à HEAD** avant la tâche 9, et personne ne l'avait vu. `tests-harness/db.test.mjs` refusait une **ligne de commentaire** de `CollationTests.cs` qui recopiait le tag de l'image (D34). Corrigé en tâche 9, mais le fait compte : le lot 2 a passé au moins un commit avec une boucle rouge.
2. **`db/amorcage/01-roles.sql` porte trois mots de passe locaux en clair.** Le fichier est censé servir aussi l'instance managée. Le remède est écrit dans le fichier — « ces trois lignes sont suivies d'un `alter role … password …` lancé depuis un secret » — mais **rien ne l'applique et rien ne le vérifie**. À traiter au lot qui ouvre l'instance, pas ici.
3. **Le préfixe `I` sur les interfaces.** `docs/16-projet.md` § 2 ligne 108 : « Types | `PascalCase`, préfixe interdit (`IUser` non) », **sans distinguer le front du backend**. Or les analyseurs Roslyn du dépôt, avec `AnalysisLevel: latest-all` et `TreatWarningsAsErrors`, **exigent** ce préfixe (CA1715) et rendent le projet incompilable sans lui. La règle `type-prefixe-i` de `scripts/regles-projet.mjs` ne porte que sur `.ts` et `.tsx` : **rien ne casse aujourd'hui**. La contradiction est entière et elle est écrite dans le document — **signalée, non tranchée**.
4. **`log_min_error_statement` vaut `error` par défaut côté serveur.** L'instruction fautive part donc dans les journaux **d'OVHcloud, sous-traitant**. Paramétrée, donc sans valeurs, ce qui est acceptable — mais cela impose de vérifier **sur l'instance réelle** que `log_statement` et la journalisation des paramètres sont éteints, et de l'annexer à l'AIPD. **Fait d'instance, pas de dépôt.**
5. **20 fichiers Markdown du dépôt échouent à `prettier --check`**, dont `CLAUDE.md` et `README.md`, et **aucune étape de `verify` ni aucun job de la CI ne les regarde**. Pire : `.lintstagedrc.mjs` les reformate au commit, ce qui rend la non-conformité **invisible et intermittente**. Question 8 du plan, toujours ouverte.
6. **`docs/03-donnees.md` sur l'injection SQL.** La phrase qui range « une injection SQL ou une requête brute » parmi ce que RLS protège est **trop généreuse** avec le mécanisme de D36 : un SQL injecté peut **reposer** `app.utilisateur`. Contenu métier — soumis, pas corrigé.
7. **`docs/07-roadmap.md` reste une PROPOSITION.** Les sept points de la reprise y sont écrits (bandeau daté, étape scindée 1a/1b, table D43, durée 8-12 mois, quatre points d'étape 0, stockage des photos, jugements conservés mot pour mot). `CLAUDE.md` § 6 interdit de la valider seul : elle attend le porteur, et **D39 n'est pas prise** tant qu'il n'a pas répondu.

---

## Ce qui n'a pas pu être vérifié

1. **Rien de ce lot n'a tourné sur l'instance managée**, qui n'existe pas. L'assertion de démarrage existe précisément parce que sept épreuves vertes sur un conteneur ne démontrent rien là-bas.
2. **La création de `palier_sauvegarde` avec `BYPASSRLS` sur OVHcloud.** « Only superuser roles or roles with `BYPASSRLS` can specify `BYPASSRLS` », et le compte d'administration d'Aiven n'est pas superutilisateur. Non mesurable avant d'avoir une instance.
3. **Le plafond de 100 ms de `docs/08-workflow.md` § 6 n'est pas acquis** par la mesure des trois allers-retours : conteneur local, boucle locale, quelques dizaines de lignes.
4. **Le parcours au clavier n'est mesuré que sur Chromium.** Safari n'inclut pas les boutons dans l'ordre de tabulation tant que « Full Keyboard Access » est éteint, et le projet `webkit-mobile` décrit un iPhone, qui n'a pas de clavier physique. axe-core, lui, tourne sur les deux.
5. **L'écran n'a été vu que par une machine.** Aucun jugement visuel n'a été porté sur le rendu — `docs/design/references-visuelles.md` § 1 dit que la validation se fait sur des rendus, pas sur des descriptions, et cette validation n'a pas eu lieu.
6. **La méthode de la tâche 9 s'est écartée du cycle rouge-vert.** Le code de la route et de l'assertion a été écrit **avant** ses épreuves ; le rouge a été obtenu ensuite, en **désarmant** chaque garde-fou un par un. Les quatre contrôles de l'assertion ont été neutralisés simultanément et les quatre épreuves de violation ont rougi ensemble — chacune sur un message qui lui est propre, mais le rouge n'a pas été vu contrôle par contrôle. Les tâches 10 à 12 ont suivi le cycle dans le bon ordre.
