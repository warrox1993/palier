# Lot 2 — Socle de données — Design

**Date :** 20 août 2026
**Statut :** design, en attente de validation du porteur du projet
**Complète :** `docs/superpowers/specs/2026-08-19-architecture-backend-csharp-design.md`, dont il **réécrit la ligne 108** (§ 5, « Transaction — les commandes seulement, jamais les requêtes »)
**Décisions sources :** `docs/decisions.md`, D1 à D31 · décisions **portées par ce lot** : D32 à D43, inscrites au journal le 20/08/2026 avant exécution — donc des intentions tant que les tâches ne les ont pas mesurées
**Plan d'exécution :** `docs/superpowers/plans/2026-08-20-lot-2-socle-donnees.md`

---

## 1. Objet

Le lot 1 a livré le harnais — 118 épreuves de franchissement, `npm run verify` en seize étapes, une intégration continue à six jobs. **Aucune fonctionnalité produit n'existe** : `front/src/` ne contient que `main.tsx`, `app/App.tsx` et un `core/index.ts` dont le contenu entier est `export {}` ; `back/Palier.Api/Program.cs` n'expose aucune route et son commentaire dit textuellement que « elles arrivent avec le lot 2 ».

Le lot 2 est le **socle de données**, et il est prouvé de bout en bout par un écran d'état. Il ferme les deux points que `docs/03-donnees.md` déclare bloquants — le type de la clé d'`AspNetUsers`, et le mécanisme par lequel l'identité de l'utilisateur parvient au moteur PostgreSQL — et il honore les trois livrables que D31 a datés du premier écran.

Ce document dit **quoi** et **pourquoi**. Le plan dit **comment**, tâche par tâche, et il est autoportant.

---

## 2. Ce que le lot 2 livre

| Livrable                     | Contenu                                                                                                                                                           |
| ---------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `db/`                        | `compose.yaml`, `amorcage/`, `referentiel/`, `demonstration/`, `SOURCES.md`, `README.md`. Aucun `.csproj`                                                         |
| Base locale                  | PostgreSQL **18.6** levé par `npm run db:up`, un seul service, volume nommé                                                                                       |
| Rôles                        | `palier_migrations`, `palier_app`, `palier_sauvegarde` — **tous non superutilisateurs**                                                                           |
| EF Core                      | `Npgsql.EntityFrameworkCore.PostgreSQL`, `Microsoft.EntityFrameworkCore.Design`, `Microsoft.AspNetCore.Identity.EntityFrameworkCore` dans `Palier.Infrastructure` |
| Migration `SocleInitial`     | six tables, une vue, un `CHECK`, deux index, `enable` **et** `force row level security` sur toutes, les politiques                                                |
| Mécanisme d'identité         | `set_config('app.utilisateur', $1, true)` en portée transaction, **doublé d'une garde applicative**                                                               |
| `back/Palier.Database.Tests` | Testcontainers, isolation éprouvée sur un vrai moteur, **en rôle `palier_app`**                                                                                   |
| `GET /api/v1/sante`          | première route du projet, avec une assertion de démarrage qui **refuse de servir** si RLS ne mord pas                                                             |
| Front                        | `front/src/ui/jetons.ts`, i18next câblé, un écran d'état à **quatre** états dont l'erreur se provoque                                                             |
| Dettes du lot 1              | les trois exceptions Knip de D24, l'état du service Dependabot de D30, le job `franchissement` qui saute au lieu d'échouer                                        |

**Les six tables** sont une représentante de chacune des cinq formes du schéma : identité (`AspNet*`), référence publique (`nutrient_refs`), catalogue mixte (`exercises`), possédée directe (`workouts`, `body_weight`), possédée par jointure (`sets`). Chaque **forme de politique** est donc éprouvée dès ce lot, et le lot suivant ajoute des tables sans inventer de mécanisme.

---

## 3. Ce qui est hors périmètre, et pourquoi

| Hors périmètre                                             | Motif                                                                                                                                                                                                                                                                                                                                                    |
| ---------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Toute authentification**                                 | Ni Identity configuré, ni Google OAuth, ni mot de passe, ni HaveIBeenPwned, ni 2FA TOTP, ni magasin de sessions, ni rotation de jeton. Les tables `AspNet*` sont **créées** — les 14 clés étrangères `owner_id` en dépendent et le type de leur clé doit être tranché ici — mais aucune ligne de code d'authentification n'est écrite. Lot 4             |
| **Tout calcul de conformité**                              | `Palier.Domain` ne bouge pas au-delà de Mifflin-St Jeor. Ni valeur EFSA, ni fourchette de macronutriments, ni plancher de sécurité. Lot 3                                                                                                                                                                                                                |
| **Tout appel à un modèle**                                 | `ILlmProvider` n'existe pas, aucune clé de fournisseur n'est configurée, aucun journal d'appels n'est créé. Lot 8                                                                                                                                                                                                                                        |
| **Dexie, file de retry, service worker, PWA**              | La file de retry rejoue des appels **à l'API** : elle suppose l'API, donc elle suit. L'écran d'état lit l'API en direct ; base arrêtée, il montre son erreur, pas une valeur en cache. Lot 7                                                                                                                                                             |
| **Tout déploiement**                                       | Pas de VPS, pas de Caddy, pas de `Dockerfile`, pas de compose de production, pas d'environnement de recette, **aucun compte OVHcloud ouvert à ce lot**. Le compose du lot 2 est local et rien d'autre. Lot 9                                                                                                                                             |
| **Le schéma complet**                                      | Six tables sur dix-neuf. Une seule des trois vues ; `daily_intake`, « la vue centrale du produit », attend le lot nutrition. Les treize autres tables arrivent avec le cas d'usage qui les exige (D39)                                                                                                                                                   |
| **Tout écran produit**                                     | Ni onboarding, ni écran de séance, ni règle graduée, ni jeu de composants `ui/`. Un écran, pas une bibliothèque. Lot 6                                                                                                                                                                                                                                   |
| **Compte de démonstration, historique de quatre semaines** | `docs/16-projet.md` § 4 les prévoit ; ils supposent l'authentification                                                                                                                                                                                                                                                                                   |
| **CQRS et `Mediator.SourceGenerator`**                     | Une route ne justifie pas un générateur de source. D12 prévoit explicitement le repli « handlers écrits à la main ». **Conséquence directe : l'exception de licence que D13 renvoie au lot 2 n'est pas due**, puisque le paquet n'est pas installé. Elle est redatée au lot où CQRS arrive, et il faut l'écrire au lieu de la laisser expirer en silence |
| **Tailwind**                                               | D42 : ne rien installer laisse les deux voies ouvertes ; l'installer les ferme au moment où l'on a le moins d'information                                                                                                                                                                                                                                |
| **Formatage automatique du SQL**                           | Prettier n'a aucun parseur SQL natif. La cohérence de style repose sur `.editorconfig` et la relecture                                                                                                                                                                                                                                                   |
| **Toute donnée réelle**                                    | Pas de restauration de dump de production en local (D40), pas de mesure sur l'instance OVHcloud — qui n'existe pas encore                                                                                                                                                                                                                                |

---

## 4. Le blocage matériel, mesuré aujourd'hui

**WSL n'est pas installé sur le poste.** Mesuré le 20/08/2026 :

- `wsl --status` → « Le Sous-système Windows pour Linux n'est pas installé » ;
- `Win32_OptionalFeature` : `Microsoft-Windows-Subsystem-Linux` en **InstallState 2** (désactivé), `Microsoft-Hyper-V` en **2**, seul `VirtualMachinePlatform` à **1** ;
- le client `docker` 29.6.2 et `docker compose` v5.3.1 **répondent parfaitement** pendant que le démon est injoignable sur `npipe:////./pipe/dockerDesktopLinuxEngine`.

C'est exactement le genre de faux signal que ce projet chasse : l'outil répond, la fonction est éteinte. Même classe que D30, où le fichier Dependabot était juste et le service désactivé.

**Conséquences pour le plan :**

1. L'activation exige des **droits administrateur et un redémarrage**. Personne d'autre que le porteur du projet ne peut la faire. C'est la tâche 0, et elle est hors des deux voies.
2. Le plan est organisé en **deux voies**. La _voie sèche_ — tâches 1 à 4 — ne demande aucun moteur de conteneurs et représente quatre tâches de travail réel. La _voie Docker_ — tâches 5 à 12 — attend la tâche 0. La tâche 13 est documentaire.
3. **Une branche devient provocable maintenant, et il faut la saisir.** « Docker éteint » est la branche qu'on ne provoque jamais parce qu'elle ne survient qu'un jour de panne. Elle est l'état courant de la machine : la tâche 2 la franchit avant la tâche 0, pas après.

---

## 5. Le mécanisme d'identité (D36) — la pièce centrale

Avec un backend séparé (D9), `auth.uid()` n'existe plus : cette fonction était fournie par Supabase Auth et lisait la revendication du jeton présenté **au moteur**. Trois occurrences subsistent dans `docs/03-donnees.md` ; elles doivent être substituées une fois et en connaissance de cause.

Le mécanisme retenu tient en **six pièces, dont aucune n'est optionnelle** :

1. Chaque cas d'usage — **commandes ET requêtes** — s'exécute dans une transaction ouverte par un comportement du pipeline `Palier.Application`, et l'identité est posée juste après le `BEGIN` par `select set_config('app.utilisateur', {identifiant}, true)`, l'identifiant en **paramètre lié**, en `Guid.ToString()` — jamais un `uuid`, `set_config` attend un `text`.
2. Le comportement **refuse avant d'ouvrir la transaction** quand l'identité manque, avec le nom du cas d'usage dans le message. La fonction SQL est le filet, jamais le garde unique.
3. Côté base, **deux** accesseurs dans un schéma `app` : `app.utilisateur()` qui **lève** (`errcode 28000`, message sans aucune valeur) pour les tables strictement privées, et `app.utilisateur_ou_null()` qui rend NULL, réservé aux tables à branche publique.
4. Les politiques appellent l'accesseur **enveloppé dans un sous-select** — `using (owner_id = (select app.utilisateur()))` — pour forcer une évaluation unique par instruction plutôt qu'une par ligne.
5. Le pipeline passe par `Database.CreateExecutionStrategy().ExecuteAsync(...)`.
6. `idle_in_transaction_session_timeout` est posé sur le rôle applicatif.

**Pourquoi celui-là.** C'est le seul mécanisme dont le nettoyage est garanti par le **moteur** et non par le pilote. Vérifié mot pour mot à la source (`functions-admin.html`) : « If `is_local` is `true`, the new value will only apply during the current transaction. » Aucune valeur ne peut survivre au `COMMIT` ni au `ROLLBACK`, quels que soient `No Reset On Close`, le multiplexing d'Npgsql, ou un pool PgBouncer en mode transaction — que la documentation OVHcloud décrit par ses propres mots comme « the default transaction-based pooling », et où la matrice de PgBouncer marque `SET/RESET` comme « Never ».

**La défaillance possible est donc l'identité absente, jamais l'identité d'un autre.** Un paramètre posé au bail de connexion ou un `SET ROLE` par utilisateur perdent cette garantie et fuient **en silence** : ni erreur, ni journal, ni test rouge.

---

## 6. Les trois amendements bloquants du jury

Trois jurés ont examiné ce mécanisme de façon adversariale. **Les trois le tiennent** — aucun n'a trouvé de scénario de fuite entre utilisateurs au niveau du moteur. Mais tous trois ont refusé le **plan de vérification** initial, et trois amendements sont bloquants avant adoption.

### 6.1 Les épreuves ne distinguaient pas `true` de `false`

Le troisième argument de `set_config` porte toute la sûreté du dispositif. Avec `false`, la valeur passe en portée **session** et l'on retombe exactement sur le mécanisme écarté : nettoyage délégué à Npgsql, annulé par `No Reset On Close`, le multiplexing, ou un PgBouncer en mode transaction.

L'épreuve du pool — « deux identités successives sur la même connexion physique, la seconde ne voit jamais les lignes de la première » — **passe au vert avec `false`** : la seconde requête pose sa propre identité et écrase la précédente. Le test est vert, le mécanisme est cassé.

> Toute la sûreté du dispositif tient à **un littéral booléen dans une ligne de C# que rien ne regarde**. C'est la signature exacte du faux vert que le lot 1 a chassé vingt-trois fois.

**L'épreuve qui manquait, et qui est décisive :** connexion unique (`Pooling=false`), `BEGIN` · `set_config(..., true)` · `select` · `COMMIT`, puis sur la **même** connexion et sans rien poser, `current_setting('app.utilisateur', true)` doit rendre NULL ou vide **et** une requête sur une table peuplée doit lever `28000`. Cette épreuve rougit sur `false`. Aucune des sept épreuves initiales ne le faisait.

_Variante nommée et écartée :_ `SET LOCAL` porte la localité dans sa **syntaxe** — elle ne peut pas être fausse — mais n'accepte aucun paramètre lié et obligerait à concaténer un identifiant dans du SQL, sur le chemin qui existe précisément pour se protéger de l'injection. L'arbitrage réel est « argument lié » contre « localité syntaxique » ; il est tranché en faveur de l'argument lié, et **l'épreuve remplace la garantie syntaxique**.

### 6.2 « L'échec crie » est faux sur une table vide — et c'est la branche du jour J

`ddl-rowsecurity.html`, verbatim : l'expression d'une politique « will be evaluated **for each row** prior to any conditions or functions coming from the user's query ».

Par ligne. Donc : **zéro ligne parcourue → zéro évaluation → aucune exception, zéro ligne rendue.** Sur une table vide — un compte neuf, une table fraîchement créée, les premiers jours de production — l'identité manquante est rigoureusement **indiscernable** de « cet utilisateur n'a pas de données ».

Pire : la loudness devient **dépendante du plan d'exécution**. Avec l'index `workouts (owner_id, started_at desc)`, le planificateur peut hisser la fonction `stable` en clé de parcours d'index et l'évaluer une fois au démarrage — donc lever même sur table vide. En parcours séquentiel sur table vide, non. **Une propriété de sécurité qui dépend du plan n'est pas une propriété.**

Trois conséquences, toutes reprises au plan :

1. **Cesser d'écrire que l'échec crie inconditionnellement.** La promesse est abandonnée, pas contournée.
2. L'épreuve « sans identité → erreur SQL » s'appelle explicitement **« sur table peuplée »** et porte le motif en commentaire, sinon elle passe pour la mauvaise raison.
3. **La garde applicative en amont** devient obligatoire : le pipeline refuse un cas d'usage sans identité **avant** d'ouvrir la transaction, avec le nom du cas d'usage. Sur une table vide, c'est le seul garde qui parle.

_Corollaire de performance :_ la question « une fois par instruction ou une fois par ligne ? » est **tranchée par la documentation : par ligne**. D'où le sous-select du point 4 de D36, qui force un InitPlan. Le plafond de 100 ms de `docs/08-workflow.md` § 6 n'est pas acquis pour autant, notamment sur `sets` dont la politique est un `exists` sur `workouts` : la politique de `workouts` s'applique à son tour dans la sous-requête.

### 6.3 Les tables d'identité seraient les seules sans barrière

La parade évidente — pas de RLS sur les tables `AspNet*`, ou une politique `using (true)` — ferait de la **seule table sans barrière de ligne** celle qui portera les empreintes de mots de passe, les secrets TOTP, les jetons de rafraîchissement et les sessions. C'est-à-dire l'endroit où un filtre oublié ou un `FromSqlRaw` produirait la fuite la plus coûteuse du produit, et le seul endroit où la défense en profondeur serait absente.

**Retenu (D38) :** `enable` **et** `force row level security` sur les tables `AspNet*`, et **aucune politique**. « If no policy exists for the table, a default-deny policy is used, meaning that no rows are visible or can be modified » (`ddl-rowsecurity.html`, vérifié). Conséquence assumée et **éprouvée** : `palier_app` ne lit et n'écrit strictement rien dans ces tables.

Or le chemin de connexion lit `AspNetUsers` **par email avant que la moindre identité existe** : il ne peut pas passer par `app.utilisateur()`. Le lot 4 doit donc concevoir ce chemin explicitement — rôle dédié avec sa politique, ou fonction `SECURITY DEFINER` au périmètre minimal, **jamais une pose de l'identité d'autrui**. Poser le refus au lot 2 garantit qu'il sera _conçu_ et non _découvert_ au premier `dotnet ef database update`.

### 6.4 Les deux points non bloquants, inscrits au plan

| Point                            | Ce qui entre au plan                                                                                                                                                                                                                                                                                                                                                                                               |
| -------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| **Le seed sous `FORCE`**         | Le rôle propriétaire devient lui aussi sujet aux politiques. Les deux corrections réflexes — `BYPASSRLS` au rôle de migration, ou `NO FORCE` le temps du seed — sont **les deux façons d'éteindre RLS sans que rien ne le signale**. Le référentiel `nutrient_refs` s'insère donc sous une politique d'écriture explicite, jamais par contournement                                                                |
| **Le `DbContext` hors pipeline** | Rien n'empêche structurellement un `IHostedService`, une tâche de fond, un contrôle de santé ou une file de rejeu d'ouvrir un `DbContext` sans transaction — donc sans identité, donc silencieux sur table vide. Un test de réflexion d'une trentaine de lignes, **sans dépendance nouvelle**, ferme la classe entière. C'est la différence entre « le pipeline le fait » et « **rien d'autre ne peut le faire** » |

### 6.5 Les deux ajouts du troisième juré

| Ajout                                              | Motif                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                 |
| -------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **L'assertion au démarrage contre la base réelle** | Sept épreuves vertes sur un Testcontainer ne démontrent **rien** sur l'instance OVHcloud, où deux inconnues décident si RLS mord _du tout_ : le compte d'administration a-t-il `BYPASSRLS`, et les tables créées par les migrations appartiennent-elles au rôle de l'API ? C'est la réponse directe au « à traiter au lot 2 » que D30 laisse en suspens : une preuve sur le **traitement**, pas sur le code — la seule qu'un auditeur puisse lire                                                     |
| **La journalisation, dont l'épreuve n'existe pas** | Le mécanisme crée un chemin d'erreur SQL — `PostgresException` portée par l'événement `CommandError` d'EF Core, qui journalise le `CommandText`, sur des tables nommées `body_weight` et `intake_entries`. `01-conformite.md` § 4 exige « aucune donnée de santé dans les logs applicatifs », la spec d'architecture § 11 liste cette épreuve comme _à écrire_, et `grep -rn SensitiveDataLogging back/ --include=*.cs` rend **zéro**. Elle appartient au **même lot** que le mécanisme, pas au lot 9 |

---

## 7. Les rôles, `FORCE`, et le coût nommé (D37)

Trois rôles, **tous non superutilisateurs** :

| Rôle                | Usage                                                             | Attributs                                                                                       |
| ------------------- | ----------------------------------------------------------------- | ----------------------------------------------------------------------------------------------- |
| `palier_migrations` | propriétaire des tables, `dotnet ef database update`, référentiel | sans `BYPASSRLS`. **Jamais l'API**                                                              |
| `palier_app`        | l'API                                                             | ne possède aucun objet, sans `BYPASSRLS`, `NOCREATEDB NOCREATEROLE`, privilèges objet par objet |
| `palier_sauvegarde` | `pg_dump` / `pg_restore`                                          | **seul candidat à `BYPASSRLS`**                                                                 |

**Sans deux rôles distincts, les politiques ne font rien — silencieusement.** « Superusers and roles with the `BYPASSRLS` attribute always bypass the row security system when accessing a table. Table owners normally bypass row security as well » (`ddl-rowsecurity.html`, vérifié). C'est ce que `docs/03-donnees.md` § RLS désigne déjà comme « la première chose à éprouver, avant les politiques elles-mêmes ».

`FORCE` ajoute la seule chose que la séparation des rôles ne donne pas : la protection contre un accès direct **sous le rôle propriétaire** — outil d'administration, restauration, identifiants fuités, tâche lancée à la main sur le VPS. C'est la **première** des trois familles que `03-donnees.md` nomme.

**Son coût, nommé plutôt que découvert.** Sous `FORCE`, le propriétaire cesse de contourner RLS. `pg_dump` pose `row_security = off` et « If the user does not have sufficient privileges to bypass row security, then an error is thrown » ; `--enable-row-security` impose un dump au format `INSERT`, puisque « the COPY FROM during restore does not support row security ». **La sauvegarde et la restauration deviennent un livrable éprouvé du lot 2**, et non une promesse trimestrielle qui rencontrerait ce mur au pire moment — `docs/14-contenu.md` § 7 : « Une sauvegarde jamais restaurée n'est pas une sauvegarde ».

**Et le prix se mesure sur Testcontainers, pas sur la production.** La tâche 10 franchit les deux branches : `pg_dump` sous `palier_migrations` **doit échouer** (épreuve inversée, délibérée : elle prouve que la contrainte documentée est réelle sur cette version du moteur), et `pg_dump` sous `palier_sauvegarde` avec `BYPASSRLS` puis `pg_restore` dans une base neuve **doit réussir**, avec le même nombre de lignes de part et d'autre. Si la seconde branche ne peut pas être obtenue, D37 remonte au porteur avec son prix chiffré — au lot 2 sur un conteneur, pas au lot 9 sur la production, un soir de restauration.

---

## 8. Les cinq formes de table et leurs politiques

`docs/08-workflow.md` § 6 exige « test RLS vert pour **chaque** table ». Trois familles n'entrent pas dans le modèle « A ne lit jamais une ligne de B », et **sans liste nommée, la même épreuve prouve deux choses contradictoires selon la table qu'on lui donne**.

| Forme                 | Table du lot 2            | Politique                                                                                                                                 | Ce que l'épreuve doit attendre                                                                    |
| --------------------- | ------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------- |
| Identité              | `AspNet*`                 | **aucune** — refus par défaut (D38)                                                                                                       | zéro ligne en lecture, refus en écriture, sous `palier_app`                                       |
| Référence publique    | `nutrient_refs`           | lecture publique, écriture refusée                                                                                                        | les lignes sortent sans identité ; l'écriture est refusée                                         |
| Catalogue mixte       | `exercises`               | **deux politiques permissives distinctes** — `is_custom = false` d'une part, `owner_id = (select app.utilisateur_ou_null())` d'autre part | sans identité, les lignes publiques sortent **sans erreur** ; les lignes possédées ne sortent pas |
| Possédée directe      | `workouts`, `body_weight` | `using (owner_id = (select app.utilisateur()))` + `with check` identique                                                                  | A ne lit pas B ; A n'insère pas au nom de B                                                       |
| Possédée par jointure | `sets`                    | `exists` sur `workouts`                                                                                                                   | A ne voit aucune série d'une séance de B                                                          |

**Pourquoi deux politiques plutôt qu'un `OR`.** La documentation ne garantit **aucun court-circuit** : les expressions « will be evaluated for each row » et les politiques permissives multiples sont combinées par `OR` **sans ordre garanti**. Avec un `OR` unique, la levée de `app.utilisateur()` peut donc partir sur une ligne publique et transformer toute lecture de catalogue anonyme en erreur 500. Deux politiques permissives séparées, l'une sans accesseur, l'autre avec l'accesseur **qui rend NULL**, suppriment le problème au lieu de parier sur un plan.

---

## 9. Le front : ce que D31 exige, et ce qui l'a rendu possible

D31 a daté trois livrables « avec le premier écran du lot 2, pas après lui », et dit d'elle-même que **les reconduire serait son échec**.

| Livrable D31                                                                  | Où il atterrit                                                                           |
| ----------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------- |
| Une épreuve qui refuse un écran dépourvu d'état vide ou d'état d'erreur       | tâche 11, **deux branches éprouvées séparément** — l'épreuve doit nommer _lequel_ manque |
| Un parcours Playwright au clavier seul                                        | tâche 11                                                                                 |
| `i18next` installé et câblé, `chaine-en-dur` étendue aux constantes exportées | tâche 3                                                                                  |

**L'écran d'état est un instrument, et sa fin de vie est datée (D41).** C'est le plus petit écran qui ait honnêtement quatre états, et surtout **le seul dont l'état d'erreur se provoque** : `npm run db:down`, recharger. Pas un `mock`, pas une donnée simulée — l'état réel. C'est la règle du projet : _provoquer l'absence, pas seulement constater la présence_. Au lot 4 il passe derrière l'authentification et un rôle d'administration ; au lot 6, la question « le garde-t-on ou le supprime-t-on » est **reprise explicitement** et tranchée, pas laissée à l'inertie.

**Les jetons ont enfin une cible (D42).** La règle `couleur-hors-jetons` de `scripts/regles-projet.mjs` porte l'exclusion `/src[\\/]ui[\\/]jetons\./` — qui pointe aujourd'hui **un fichier qui n'existe pas**. La règle n'a donc aucune cible et ne protège rien. `front/src/ui/jetons.ts` la lui donne.

---

## 10. Les décisions à écrire — D32 à D43

Ce lot produit douze entrées du journal des décisions. **Elles ne sont pas écrites par cette spec** ; elles ont été portées à `docs/decisions.md` le 20/08/2026 (commit `26773be`), **avant l'exécution du lot**. Ce sont donc des intentions tant que les tâches 1 à 12 ne les ont pas mesurées : la tâche 13 les confronte à ce que le lot a réellement constaté, et corrige ce que l'exécution contredit.

| #       | Titre                                                                                              | Ce qu'elle tranche                                                                                                                                                                                                                                                                                           |
| ------- | -------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| **D32** | Arborescence à trois racines ; les migrations restent dans `back/`                                 | `db/` ne porte **aucun `.csproj`** : mesuré, un projet sous `db/` échapperait entièrement à D13 (`verifier-licences.mjs` parcourt littéralement `parcourir('back')`) et ses binaires deviendraient suivis (`.gitignore` n'ignore que `back/**/bin/` et `back/**/obj/`) — deux angles morts pour un rangement |
| **D33** | Docker : oui, au lot 2, et pour PostgreSQL seul                                                    | Un compose local et Testcontainers. **Pas** de conteneur backend en développement, **pas** de `Dockerfile`, **pas** de compose de production                                                                                                                                                                 |
| **D34** | PostgreSQL 18.6, image Debian, **un seul endroit où le tag est écrit**                             | `alpine` écarté pour une raison mesurable : musl n'implémente pas `LC_COLLATE` comme glibc, et l'instance managée tourne sur glibc. Testcontainers **lit** le tag dans le compose au lieu de le redéclarer                                                                                                   |
| **D35** | La clé d'`AspNetUsers` est un `uuid` ; les tables d'identité naissent au lot 2                     | Ferme le point 2 de « Ce qui n'est pas tranché ici » de `03-donnees.md`, qui l'exige « avant la première migration »                                                                                                                                                                                         |
| **D36** | L'identité par `set_config(..., true)`, en portée transaction, **doublée d'une garde applicative** | Voir § 5 et § 6                                                                                                                                                                                                                                                                                              |
| **D37** | Trois rôles, trois chaînes, `FORCE ROW LEVEL SECURITY`, et une assertion contre la base réelle     | Voir § 7                                                                                                                                                                                                                                                                                                     |
| **D38** | Les tables d'identité naissent en refus par défaut ; leur chemin d'accès est conçu au lot 4        | Voir § 6.3                                                                                                                                                                                                                                                                                                   |
| **D39** | Le schéma arrive par tranches, avec le cas d'usage qui l'exige                                     | **Écart à `07-roadmap.md` étape 1 : à valider par le porteur**                                                                                                                                                                                                                                               |
| **D40** | Aucune donnée de production sur un poste de développement                                          | Aucun document du dossier ne le dit aujourd'hui. L'interdiction ne coûte rien tant que la base est vide, et se paierait très cher après le premier incident                                                                                                                                                  |
| **D41** | L'écran d'état est un instrument, et sa fin de vie est datée                                       | Voir § 9                                                                                                                                                                                                                                                                                                     |
| **D42** | Pas de Tailwind au lot 2                                                                           | **Choix par défaut, valable jusqu'à réponse du porteur**                                                                                                                                                                                                                                                     |
| **D43** | La feuille de route porte les durées constatées, lot par lot                                       | D9 et D17 ont une condition de réouverture qu'**aucun instrument ne peut déclencher** : rien dans ce dépôt ne mesure le temps écoulé par étape                                                                                                                                                               |

---

## 11. Les dettes du lot 1 que ce lot doit solder

| Dette                                                   | Origine                                            | Où elle est soldée                                                           |
| ------------------------------------------------------- | -------------------------------------------------- | ---------------------------------------------------------------------------- |
| Les trois exceptions Knip                               | D24, « le lot 2, pour les trois lignes à la fois » | tâche 3                                                                      |
| L'état du service Dependabot                            | D30, « à traiter au lot 2 »                        | tâche 4                                                                      |
| Le job `franchissement` qui **saute** au lieu d'échouer | dernière réserve du journal du lot 1               | tâche 4                                                                      |
| Les trois livrables de D31                              | D31, « avec le premier écran, pas après lui »      | tâches 3 et 11                                                               |
| Le levier `front:test` de D26                           | D26, arbitrage renvoyé au porteur                  | tâche 12                                                                     |
| L'exception de licence CQRS                             | D13                                                | **non due** : le paquet n'est pas installé. À redater, pas à laisser expirer |

---

## 12. Critère de fin

Vérifiable de bout en bout, et en une session :

1. `npm run db:up` puis `dotnet ef database update --connection <PalierMigrations>` appliquent le schéma **sans intervention manuelle** ;
2. `npm run verify` sort en **code 0**, toutes étapes vertes, `back:test` incluant les épreuves d'isolation sur Testcontainers et `front:test` celles de l'écran ;
3. l'API démarre sous `palier_app` et `GET /api/v1/sante` rend la version de migration appliquée ; on lui donne la chaîne `PalierMigrations` et **elle refuse de démarrer** ;
4. l'écran d'état affiche son contenu, puis `npm run db:down` et rechargement affichent son **état d'erreur réel**, pas un simulacre ;
5. **chacune des épreuves de franchissement du lot a été vue rouge** en provoquant sa violation, et le journal du lot le consigne violation par violation ;
6. `docs/decisions.md` porte D32 à D43, et `docs/03-donnees.md` ne contient plus aucun `auth.uid()` ni aucun point ouvert sur ces deux sujets.

---

## 13. Risques

| Risque                                                          | Portée                                        | Parade inscrite au plan                                                                                                                                       |
| --------------------------------------------------------------- | --------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Le blocage WSL dure**                                         | tout le lot                                   | Deux voies. Quatre tâches de travail réel sans Docker, et la branche « Docker éteint » franchie pendant que l'état est disponible                             |
| **Le littéral `true` de `set_config` régresse**                 | isolation de données de santé                 | L'épreuve qui discrimine `is_local`, sur connexion unique — tâche 7, épreuve 2                                                                                |
| **Une table future naît sans RLS**                              | isolation                                     | L'épreuve interroge `pg_class` **en forme de catalogue**, jamais une liste écrite à la main : elle couvre les tables futures sans que personne ait à y penser |
| **Le rôle de l'API devient propriétaire, ou gagne `BYPASSRLS`** | RLS ne mord plus **du tout**, silencieusement | L'assertion de démarrage refuse de servir — tâche 9                                                                                                           |
| **Un `DbContext` ouvert hors pipeline**                         | identité absente, silencieuse sur table vide  | Test de réflexion — tâche 8                                                                                                                                   |
| **`EnableRetryOnFailure` activé un soir de panne**              | _toutes_ les requêtes lèvent d'un coup        | `CreateExecutionStrategy` obligatoire, et l'épreuve qui **l'active dans la fixture** pour prouver que le passage est réel                                     |
| **`FORCE` rend `pg_dump` impossible sur OVHcloud**              | sauvegarde                                    | Mesuré sur Testcontainers à la tâche 10 ; si la branche « dump réussi » n'est pas obtenue, l'arbitrage remonte avec son prix                                  |
| **Une donnée de santé part dans un journal**                    | conformité, article 9                         | Deux épreuves à la tâche 9 : le journal, et la configuration de **production** construite avec `EnableSensitiveDataLogging`                                   |
| **`npm run verify` — donc `git push` — exige désormais Docker** | la boucle de rétroaction                      | Assumé et soumis. Le hook de pré-**commit** n'est pas touché                                                                                                  |
| **Le seuil de couverture à 100 % appliqué au nouveau projet**   | faux blocage                                  | `back/coverage.runsettings` filtre `[Palier.Domain]*` : le seuil ne s'applique pas au projet d'intégration, et **il ne faut pas l'y appliquer**               |

---

## 14. Ce qui est soumis au porteur du projet

Aucun de ces points n'est tranché par cette spec. `CLAUDE.md` § 6 les range parmi ce qui ne se décide pas seul.

1. **Activer WSL2 ou Hyper-V — bloquant, et personne d'autre ne peut le faire.** Droits administrateur et redémarrage. Tout le lot 2 en dépend, et **rien ne le signale aujourd'hui**.
2. **Accepter que `npm run verify` — donc `git push` — exige désormais un moteur de conteneurs.** `scripts/verify.mjs` lance `dotnet test back/Palier.sln` sur la solution **entière** : dès que le projet d'intégration y entre, la boucle exige Docker sans qu'une ligne de `verify.mjs` ait changé. Le refuser demanderait un `--filter`, c'est-à-dire une **seconde liste de contrôles**, exactement ce que D25 existe pour empêcher.
3. **`FORCE ROW LEVEL SECURITY` : oui ou non, en connaissance du prix.** Sans lui, un accès direct sous le rôle propriétaire n'est pas protégé. Avec lui, `pg_dump` échoue pour tout rôle qui ne contourne pas RLS. Or OVHcloud écrit que la création d'utilisateurs se fait « with default admin roles and privileges » et que « the only specific privilege you can set is `replication` » : **ce point n'est pas mesurable avant d'avoir une instance**.
4. **Docker Desktop ou Podman Desktop.** Docker Desktop est gratuit sous les deux seuils (moins de 250 salariés **et** moins de 10 M$ de chiffre d'affaires). Podman et Rancher Desktop suppriment la question de licence pour toujours, au prix d'une configuration de Testcontainers (`DOCKER_HOST`, `TESTCONTAINERS_RYUK_DISABLED=true` en rootless). **Si l'idée d'un seuil contractuel à surveiller déplaît, c'est maintenant qu'il faut choisir Podman, pas dans deux ans.**
5. **L'écart au « schéma complet » de la feuille de route (D39).** `CLAUDE.md` § 5 range `07-roadmap.md` **au-dessus** de `CLAUDE.md`. Sans validation explicite, D39 n'est pas prise.
6. **Tailwind, oui ou non (D42).** `CLAUDE.md` § 3 l'annonce dans la pile ; il n'est pas installé. C'est une dépendance lourde au sens de `CLAUDE.md` § 6, et elle rendrait `couleur-hors-jetons` **aveugle sans rien signaler** — des classes utilitaires ne portent aucun `#RRGGBB`.
7. **D27 — Lighthouse CI, toujours sans réponse depuis le lot 1.** Une chaîne à sept vulnérabilités hautes, dont une sans version corrigée (`extract-zip`, plage `*`), s'exécute dans le job `performance` avec accès au dépôt en lecture. Le risque n'est pas éliminé, il est **déplacé hors de la vue de `npm audit`**.
8. **Le formatage des documents.** 20 fichiers Markdown du dépôt échouent à `prettier --check` aujourd'hui — dont `CLAUDE.md`, `README.md`, `docs/16-projet.md`, `docs/08-workflow.md` et les deux specs — et **aucune étape de `verify` ni aucun job de la CI ne les regarde**. `.lintstagedrc.mjs` les reformaterait au commit puisque son motif porte `md`, ce qui rend la non-conformité **invisible et intermittente**. Deux voies : étendre le périmètre de formatage à `docs/` et à la racine — ce qui reformate 20 fichiers d'un coup, **y compris `CLAUDE.md`, qui est votre document** — ou déclarer la documentation hors périmètre et l'**écrire**, plutôt que de laisser une zone aveugle non nommée.
9. **Corriger `docs/03-donnees.md` § RLS — contenu métier.** La phrase qui range « une injection SQL ou une requête brute » parmi ce que RLS protège est trop généreuse, et elle le reste avec ce mécanisme : un SQL injecté peut **reposer** `app.utilisateur`. RLS y protège du **filtre oublié**, pas de l'attaquant délibéré. Soit la ligne est corrigée, soit elle est assumée comme une intention et non une garantie — les deux se disent, aucune ne se suppose.
10. **La contradiction entre `08-workflow.md` § 6 (« test RLS vert pour chaque table ») et les trois familles qui n'entrent pas dans le modèle.** La liste doit être nommée dans `03-donnees.md`, avec la forme de politique de chacune (§ 8 de ce document en donne le contenu).
11. **Deux points d'étape 0 devenus des prérequis contractuels du lot 4, à engager maintenant à cause de leur délai :** le **DPA OVHcloud** — dont la signature dépend du statut, donc les points 3 et 8 de l'étape 0 sont **couplés** et leur ordre n'est pas libre — et un **fournisseur d'email transactionnel européen avec DPA**, sans lequel la vérification d'email de `09-comptes.md` § 1 n'a aucun moyen d'envoi (`IEmailSender<TUser>` n'a aucune implémentation utilisable en production).
12. **Trois questions de schéma que le lot 2 n'a pas le droit de trancher seul :**
    - (a) `programs.owner_id` est `not null references auth.users`, mais `docs/16-projet.md` § 4 prévoit **9 programmes modèles** livrés : le schéma n'a aujourd'hui **aucune place pour un programme sans propriétaire** ;
    - (b) `03-foods.sql` (300 aliments CIQUAL) est-il du **référentiel de production** ou un jeu de développement ? La réponse change le fichier qui l'applique et le moment ;
    - (c) `docs/16-projet.md` § 2 interdit le préfixe `I` sur les types **sans distinguer le front du backend** — or les analyseurs Roslyn du dépôt, avec `AnalysisLevel: latest-all` et `TreatWarningsAsErrors`, **exigent** ce préfixe sur les interfaces C#. La spec d'architecture § 13 signale déjà l'absence de conventions C# dans ce document.

---

## 15. Ce qui n'a pas pu être vérifié

- **Rien de ce lot n'a tourné.** Le démon Docker est injoignable : aucune des épreuves d'isolation, aucun `pg_dump`, aucune assertion de démarrage n'a été exécutée. Tout ce qui touche au moteur est un **raisonnement sur la documentation**, à provoquer.
- **Aucun PostgreSQL local n'existe** sur le poste : ni `psql`, ni `pg_ctl`, ni service Windows. Le port 5432 est libre, rien n'y écoute.
- **L'instance OVHcloud n'existe pas.** Deux faits en dépendent et ne sont pas mesurables aujourd'hui : le compte d'administration a-t-il `BYPASSRLS`, et un rôle `BYPASSRLS` y est-il créable ? C'est exactement pourquoi l'assertion de démarrage existe, et pourquoi la branche « dump réussi » se mesure sur Testcontainers.
- **Les droits par défaut de `GITHUB_TOKEN`** ne couvrent peut-être pas la lecture de `/repos/…/vulnerability-alerts` et `/automated-security-fixes`. Non mesuré. Si le job ne peut pas lire cet état, la vérification manuelle entre au `docs/gabarit-rapport-lot.md` **plutôt que d'être abandonnée**.
- **La durée de `verify` est périmée.** La seule mesure consignée est **129,1 s pour 15 étapes** (`progress.md`) et `scripts/verify.mjs` en porte **16** aujourd'hui. L'écart doit être remesuré avant d'être invoqué.
- **Rien n'a jamais tourné sous Linux** hors CI, et la CI est le seul endroit où les motifs d'exclusion, la casse des chemins et les branches `shell: false` sont éprouvés.
- **Le coût réel du mécanisme n'est pas mesuré.** Il ajoute **trois** allers-retours par cas d'usage — `BEGIN`, `set_config`, `COMMIT` — pas un. À mesurer contre le plafond de 100 ms de `08-workflow.md` § 6, pas à supposer.
