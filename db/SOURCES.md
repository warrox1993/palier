# `db/referentiel/` — attribution, fichier par fichier

**Une ligne par fichier `*.sql` de `db/referentiel/`, sans exception.** Ce
tableau est la cible de la règle `source-non-attribuee` de
`scripts/regles-projet.mjs` : un fichier de référentiel qui n'y figure pas fait
échouer `npm run regles`, donc `npm run verify`, donc `git push`.

C'est l'application de `docs/17-donnees-sources.md`. Le point critique y est
écrit noir sur blanc : **ne jamais fusionner les sources dans une table unifiée
enrichie** — c'est exactement ce qui créerait une base dérivée au sens de
l'ODbL, avec l'obligation de share-alike qui l'accompagne. Une source dont la
licence n'est pas tracée est une source qu'on ne peut plus séparer des autres
le jour où il le faut.

| Fichier | Source | Licence | Version ou millésime | Date de relevé | URL |
| ------- | ------ | ------- | -------------------- | -------------- | --- |

_Aucun fichier de référentiel n'est encore livré : les données arrivent avec le
cas d'usage qui les exige (D39). Le tableau est vide, pas absent — le contrôle
a besoin de sa cible, et une cible absente se signale au lieu de se remplacer._

## Ce qui doit figurer dans chaque colonne

| Colonne              | Ce qu'elle porte                                                                             |
| -------------------- | -------------------------------------------------------------------------------------------- |
| Fichier              | le nom exact, tel qu'il apparaît dans `db/referentiel/` — c'est lui que la règle recherche   |
| Source               | l'organisme, pas le site de reprise : « ANSES / CIQUAL », jamais « trouvé sur un dépôt npm » |
| Licence              | l'identifiant SPDX quand il existe, sinon le nom exact de la licence et son lien             |
| Version ou millésime | le numéro publié par la source ; pour CIQUAL, l'année de la table                            |
| Date de relevé       | le jour où le fichier a été téléchargé, au format `JJ/MM/AAAA`                               |
| URL                  | l'adresse de téléchargement d'origine, celle qui permet de refaire le relevé                 |

**`demonstration/` n'entre pas dans ce tableau.** Son contenu est entièrement
synthétique et n'arrive jamais en production (D40) : il n'a pas de source à
attribuer. `amorcage/` non plus — ce sont des rôles et des privilèges, pas des
données.
