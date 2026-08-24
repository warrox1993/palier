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

| Fichier            | Source                                                                                                                                                       | Licence                             | Version ou millésime                | Date de relevé | URL                       |
| ------------------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------ | ----------------------------------- | ----------------------------------- | -------------- | ------------------------- |
| `02-exercises.sql` | **Rédigé pour ce projet** — contre-indications dérivées de `docs/05-entrainement.md` § 4, incréments de son § 3. **NON RELU par un professionnel de santé.** | Propriétaire — aucune donnée tierce | 60 mouvements prioritaires, jalon 1 | 24/08/2026     | — (aucune source externe) |

**La colonne Source dit ce qu'elle vaut, et c'est délibéré.** `docs/14-contenu.md`
§ 2 exige que les contre-indications soient « relues par un kinésithérapeute » ;
cette relecture n'a pas eu lieu. Le fichier porte la même mention en tête.

**Aucune source externe, et c'est un choix** — D66. Les bases d'exercices libres
sont anglophones, et leurs licences de type ODbL imposent le share-alike : un
catalogue enrichi de contre-indications et de consignes serait une base dérivée
au sens de la licence, à rouvrir en entier. `docs/17-donnees-sources.md` pose
d'ailleurs la règle qui l'interdit : « ne jamais fusionner les sources dans une
table unifiée enrichie ».

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
