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

<!-- Écrit à la tâche 6 du lot 2. -->

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
