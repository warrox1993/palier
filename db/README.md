# `db/` — la base locale, ses données et son outillage

Cette racine ne porte **aucun `.csproj`** (D32). Les migrations EF Core, le
`DbContext`, les entités et les tests de politiques vivent sous `back/`. Ce
dossier ne contient que du SQL, du YAML et du Markdown.

> **Pourquoi.** `scripts/verifier-licences.mjs` parcourt littéralement `back` :
> un projet posé sous `db/` échapperait entièrement au contrôle de licences de
> D13 **sans un message**. Et `.gitignore` n'ignore que `back/**/bin/` et
> `back/**/obj/` : les binaires de `db/` deviendraient des fichiers suivis au
> premier `git add -A`. Deux angles morts pour gagner un rangement est le
> mauvais échange sur un projet dont la leçon centrale est « un fichier de
> configuration juste dont le périmètre est faux ne dit rien ».

## Les trois dossiers, et ce qui les sépare

| Dossier          | Ce qu'il porte                                                                                     | Où cela arrive                      |
| ---------------- | -------------------------------------------------------------------------------------------------- | ----------------------------------- |
| `amorcage/`      | rôles et privilèges, exécutés **avant** les migrations par le compte d'administration du conteneur | conteneur local et instance managée |
| `referentiel/`   | **données de production** : valeurs EFSA, catalogue d'exercices, programmes modèles                | **en production**                   |
| `demonstration/` | jeu de développement **entièrement synthétique**                                                   | **jamais en production**            |

**Aucune donnée réelle n'entre jamais dans `demonstration/` — D40.** Restaurer
un dump de production, ou tout extrait de celui-ci, sur une machine de
développement ou dans un volume Docker local est interdit : c'est le seul
chemin par lequel des données de l'article 9 atterriraient sur un portable que
le projet ne chiffre pas et ne supervise pas. Toute enquête sur des données
réelles se fait sur le serveur, sous journalisation.

**Tout fichier de `referentiel/` porte une ligne dans `SOURCES.md`** — nom,
source, licence, millésime, date de relevé, URL. Ce n'est pas une convention :
`npm run regles` refuse le dépôt si un `*.sql` de `referentiel/` n'y figure
pas, en nommant le fichier. C'est l'application de `docs/17-donnees-sources.md`,
qui interdit de fusionner des sources aux licences différentes sans les tracer.

## Lever

```bash
npm run db:up      # lève le service palier-db en arrière-plan
npm run db:down    # l'arrête, EN CONSERVANT les données
```

Le service s'appelle `palier-db`, il écoute sur `5432`, et son volume nommé est
`palier-db-data`. Ces trois valeurs sont déclarées dans `compose.yaml` et nulle
part ailleurs.

**Le moteur doit tourner, pas seulement le client.** `docker --version` répond
parfaitement pendant que le démon est injoignable — c'est la classe de faux vert
que ce projet traque. `npm run db:up` teste `docker info` avant toute chose et
refuse avec la marche à suivre plutôt qu'avec un nom de tuyau Windows :

```
db : le moteur Docker ne répond pas. Le CLIENT répond parfaitement —
`docker --version` réussit — mais le DÉMON est injoignable, et c'est lui qui
lève les conteneurs.
```

Le code de sortie est **3**, distinct du 1 que rend un échec de `compose` :
« le moteur est éteint » et « compose a échoué » n'appellent pas le même geste.

## Appliquer

Le schéma vit dans `back/Palier.Infrastructure/Migrations/`, en migrations EF
Core versionnées — **D14**. Aucune modification manuelle du schéma, par quelque
console que ce soit.

```bash
npm run db:up
# La variable porte quatre couples, séparés par des points-virgules :
#   Host=localhost · Port=5432 · Database=palier · Username=palier_migrations
# suivis du mot de passe local, celui de `amorcage/01-roles.sql`.
export ConnectionStrings__PalierMigrations=…
dotnet ef database update \
  --project back/Palier.Infrastructure --startup-project back/Palier.Infrastructure
```

**La chaîne passe par la variable d'environnement, jamais par un fichier du
dépôt.** `.gitleaks.regles.toml` porte la règle `chaine-connexion-postgres`, et
elle refuse un fichier suivi qui porterait une chaîne complète, mot de passe
compris.

**Le motif n'est PAS recopié ici, et l'exemple ci-dessus est délibérément
décomposé.** Mesuré le 20/08/2026 : écrite d'un seul tenant, la ligne
d'exemple déclenchait la règle sur ce fichier même, et le commit était refusé
— trois fois. La parade retenue est de ne pas écrire la chaîne ; élargir
l'exception aurait rendu la règle aveugle sur tout le dossier, ce qui est
exactement ce que `db/compose.yaml` explique déjà pour lui-même.

**Le rôle est `palier_migrations`, jamais l'API et jamais l'administrateur.**
C'est lui qui devient propriétaire des tables, et c'est cette propriété qui rend
`force row level security` observable. Migrer sous un autre compte produirait un
schéma dont personne n'est propriétaire au sens du produit — et des politiques
qui ne mordent pas.

### Les rôles avant le schéma

`amorcage/` est monté en lecture seule dans `/docker-entrypoint-initdb.d/` :
l'image y exécute les `*.sql` **par ordre alphabétique et sous le compte
d'administration**, avant que la base accepte la moindre connexion. C'est le
seul usage de ce compte.

**L'entrypoint ne les exécute que sur un répertoire de données VIDE.** Sur un
volume déjà créé — donc sur toute machine où `db:up` a déjà tourné une fois — il
faut :

```bash
npm run db:reset    # DÉTRUIT les données locales
npm run db:up
```

Sans cela, la migration échoue sur `role "palier_migrations" does not exist`, et
le message ne dit pas quoi faire. Mesuré le 20/08/2026 : le montage manquait, la
base locale ne portait que `palier_admin`, et l'étape « appliquer sans commande
manuelle intercalée » était impossible.

### Ce que la migration écrit à la main

EF Core ne pilote ni les vues, ni RLS, ni les privilèges. `SocleInitial` les
écrit par `migrationBuilder.Sql(...)`, **chacun avec son `Down`** :

| Objet                                      | Pourquoi à la main                                                                                                                                                                                                                                              |
| ------------------------------------------ | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| la vue `weekly_volume`                     | EF ne génère pas de vue. Elle porte `security_invoker = true` — sans quoi elle filtrerait avec les droits de son propriétaire, et non de l'appelant                                                                                                             |
| `enable` **et** `force row level security` | sur les **treize** tables de `public`, historique des migrations compris. `enable` seul laisse le propriétaire contourner ses propres politiques                                                                                                                |
| la politique `migrations_referentiel`      | sous `FORCE`, le propriétaire est lui aussi soumis aux politiques. Le chargement du référentiel passe donc par une politique **explicite**, jamais par `BYPASSRLS` ni par un `NO FORCE` temporaire — les deux façons d'éteindre RLS sans que rien ne le signale |
| les `grant` de `palier_app`                | objet par objet — D37. Aucun `alter default privileges` : il accorderait d'avance sur des tables que personne n'a encore relues                                                                                                                                 |

Les tables `AspNet*` reçoivent RLS **et aucune politique** — D38. Elles naissent
en refus par défaut, pour tout le monde, propriétaire compris. Le chemin de
connexion qui lit `AspNetUsers` par email avant toute identité casse donc fermé
et bruyant ; **le lot 4 doit le concevoir**, et non le découvrir.

### Comment l'identité parvient au moteur — D36

```
BEGIN
select set_config('app.utilisateur', <identifiant>, true)   ← le troisième argument
… le cas d'usage …
COMMIT
```

**Le troisième argument est `true`, et c'est là que tient toute la sûreté du
dispositif.** « If `is_local` is `true`, the new value will only apply during
the current transaction » : aucune valeur ne peut survivre au `COMMIT` ni au
`ROLLBACK`, quels que soient `No Reset On Close`, le multiplexing d'Npgsql ou un
PgBouncer en mode transaction. **La défaillance possible est donc l'identité
absente, jamais l'identité d'un autre.**

À `false`, la valeur passe en portée **session**, survit à la connexion rendue
au pool, et l'utilisateur suivant hérite de l'identité du précédent — sans
erreur, sans journal. **Mesuré le 20/08/2026 : sur les vingt et une épreuves du
projet, vingt restent VERTES avec `false`**, y compris « deux identités
successives sur la même connexion physique ». Une seule le distingue :
`La_portee_transaction_ne_survit_pas_au_commit_sur_la_meme_connexion`, qui
ouvre une connexion `Pooling=false` et relit `current_setting` après le
`COMMIT`. Ne pas l'affaiblir.

Côté base, **deux** accesseurs dans le schéma `app` :

| Fonction                    | Comportement                                 | Pour                       |
| --------------------------- | -------------------------------------------- | -------------------------- |
| `app.utilisateur()`         | **lève** `28000`, message sans aucune valeur | tables strictement privées |
| `app.utilisateur_ou_null()` | rend `NULL`                                  | tables à branche publique  |

**Deux, et non un.** Une politique de catalogue public qui appellerait la
fonction qui lève transformerait toute lecture anonyme en erreur 500 — mesuré :
en fusionnant les deux politiques d'`exercises` en un seul `OR`, la lecture
d'une ligne **publique** rend `28000`. La documentation ne garantit aucun ordre
d'évaluation, et se fier au court-circuit serait une supposition déguisée en
protection.

Chaque politique enveloppe l'accesseur dans un **sous-select** —
`using (owner_id = (select app.utilisateur()))`. L'expression d'une politique
est évaluée **pour chaque ligne** ; `(select …)` force un InitPlan évalué une
fois par instruction. On garde le `plpgsql` qui lève _et_ la vitesse.

> **Ce que ce dispositif n'achète PAS, et qu'il ne faut pas croire.** « L'échec
> crie » est **faux sur une table vide** : zéro ligne parcourue, zéro
> évaluation, aucune exception. Sur un compte neuf — donc les premiers jours de
> production — « identité absente » et « cet utilisateur n'a pas de données »
> sont indiscernables. La garde applicative du pipeline est ce qui ferme ce
> trou. La fonction SQL est le **filet**, jamais le garde unique.

**La migration suppose que les rôles existent.** Un `grant … to palier_app` sur
une base sans ce rôle échoue en le nommant. C'est le couplage voulu entre
`amorcage/` et les migrations : il est bruyant, et il est écrit ici.

## Réinitialiser

```bash
npm run db:reset
```

**`db:reset` est la seule commande du projet qui détruit des données.** Elle
passe `-v` à `docker compose down` : le volume `palier-db-data` est supprimé
avec le conteneur, et tout ce que la base contenait disparaît. Il n'y a pas de
confirmation et il n'y a pas de retour en arrière. `db:down`, lui, arrête le
conteneur en conservant le volume — c'est la commande de tous les jours.

## Sauvegarder et restaurer

<!-- Écrit à la tâche 10 du lot 2. -->

## Ce que le formatage automatique ne couvre pas

`npm run format:scripts` passe Prettier sur les `.yaml`, `.yml`, `.md` et
`.json` de ce dossier. **Le `.sql` en est dehors : Prettier n'a aucun parseur
SQL natif.** La cohérence de style du SQL repose sur le bloc `[*.sql]` de
`.editorconfig` et sur la relecture — c'est écrit ici plutôt que laissé croire
à une couverture qui n'existe pas.
