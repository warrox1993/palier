# Le système de programmes

_24/08/2026. Fait suite au catalogue de 255 exercices, et répond à la seconde
moitié d'une demande dont la première était le catalogue : « met en place un
système pour que les utilisateurs puissent introduire des noms d'exercices afin
qu'ils puissent créer leur programme avec l'aide du logiciel »._

---

## 1. Le point que le dépôt refusait de trancher seul

`docs/16-projet.md` § 4 portait, depuis le lot 2, une ligne explicite :

> `04-programs.sql` livre **9 programmes modèles**, mais `programs.owner_id` est
> `not null references auth.users` : le schéma n'a **aucune place pour un
> programme sans propriétaire**.

Le même paragraphe ajoutait que « le lot 2 n'a pas le droit de les trancher
seul ». **Le porteur du projet a tranché le 24/08/2026** — la forme « catalogue
mixte », déjà en vigueur sur `exercises`.

Les deux autres formes ont été présentées avec leur coût, et écartées pour ce
qu'elles coûtaient :

| Forme               | Ce qu'elle coûtait                                                                             |
| ------------------- | ---------------------------------------------------------------------------------------------- |
| Table séparée       | Six tables au lieu de trois, et toute évolution du modèle faite **deux fois**                  |
| Utilisateur système | Une identité qui n'est celle de personne dans `AspNetUsers`, que D38 tient hors des politiques |

La forme retenue donne en plus ce qui a emporté la décision : **copier un modèle
ne traduit rien**. C'est la même forme, avec un propriétaire au lieu de nul.

---

## 2. Ce qui est livré

### Le schéma

Trois tables, conformes à `docs/03-donnees.md` lignes 84 à 110, plus ce que les
neuf modèles exigent : `slug` (D68), colonnes bilingues suffixées (D67),
fréquence, contrainte visée, et la note que `14-contenu.md` § 2 rend obligatoire.

**RLS activée ET forcée sur les trois**, avec **neuf politiques** :

- `programs` — deux politiques séparées, jamais un `OR` dans une seule
- `program_days` et `program_exercises` — forme « possédée INDIRECTE » : elles ne
  portent aucun `owner_id`, et leur appartenance se lit en remontant au
  programme. Le dupliquer aurait créé deux vérités sur la même appartenance.

**Cinq contraintes `CHECK`, chacune éprouvée par provocation** — y compris les
DEUX branches de `ck_programs_proprietaire`, parce qu'une branche jamais
franchie est une branche qui ment.

### Le référentiel

`db/referentiel/04-programs.sql` — **9 programmes, 36 séances, 157 poses**.

Le fichier est **engendré**, et son générateur **refuse de produire** un
programme qui ménage une contrainte tout en contenant un mouvement
contre-indiqué pour elle. Les exclusions dérivent du tableau de
`05-entrainement.md` § 4 ; les cibles et les structures, de son § 1.

Le fichier porte en outre un bloc `do $$ … raise exception` qui **compte** les
lignes posées et lève si le compte ne tombe pas juste. Sans lui, un slug
d'exercice mal orthographié aurait donné une séance amputée **sans la moindre
erreur** : la jointure ne trouve rien, elle n'insère rien, et `psql` rend 0.

### L'API

Six routes, plus la recherche ajoutée au catalogue :

| Route                         | Ce qu'elle fait                             |
| ----------------------------- | ------------------------------------------- |
| `GET /exercices?recherche=`   | **Le geste que la demande réclamait** — D73 |
| `GET /programmes`             | Les modèles ET les siens, sans leur contenu |
| `GET /programmes/{id}`        | Le contenu, exercices **marqués** — D75     |
| `POST /programmes`            | Créer                                       |
| `PUT /programmes/{id}`        | Remplacer le contenu en entier              |
| `POST /programmes/{id}/copie` | Partir d'un modèle — D74                    |
| `DELETE /programmes/{id}`     | Supprimer, cascade comprise                 |

**Un programme s'écrit en entier**, jamais par morceaux. Six routes fines —
ajouter, retirer, monter, descendre — auraient chacune eu à renuméroter ce
qu'elles touchent, et six implémentations de la même renumérotation auraient
divergé. Le coût est réel et assumé : deux écrans ouverts, le dernier qui
enregistre écrase l'autre. C'est le comportement d'un document.

---

## 3. Ce que les épreuves ont trouvé, et que la relecture n'aurait pas vu

### Le joker `%` traversait

`EF.Functions.Like(SearchKey, "%" + terme + "%")` avec un terme réduit à `%`
donne `%%%` : le catalogue entier sort. Ce n'est pas une injection — le motif
reste un paramètre lié — mais c'est la même erreur de fond : **une entrée qui
décide de ce que la requête fait**. Un `_` remplaçait de même n'importe quel
caractère.

L'épreuve `Le_caractere_JOKER_est_cherche_LITTERALEMENT` l'a attrapé au premier
passage. `CleDeRecherche.MotifPourLike` échappe désormais `\`, `%` et `_` —
**la contre-oblique en premier**, sans quoi on double-échapperait les jokers
qu'on vient d'introduire.

Le caractère d'échappement par défaut de `LIKE` a été **mesuré sur le cluster du
projet**, pas supposé : `'TAUX 50%' like '%\%%'` rend vrai, `'SQUAT BARRE'` rend
faux.

### Le seuil de couverture a servi de détecteur, encore

`Palier.Application` est tombé sous 100 % sur six accesseurs jamais lus par ses
propres épreuves. Ils ne sont pas morts — l'Infrastructure les lit — mais leur
absence signalait une épreuve manquante : celle de **l'ordre des paramètres**.
`ExerciceDeSeanceValide` en porte sept, presque tous du même type : intervertir
`RirCible` et `ReposSecondes` compilerait, passerait toutes les épreuves de
refus, et donnerait des séances de deux secondes de repos avec quatre-vingt-dix
répétitions en réserve.

### Le garde-fou d'architecture a refusé deux helpers, et il avait raison

`ArchitectureTests` interdit qu'un type prenne `PalierDbContext` sans être un
gestionnaire : « Un type qui tient le contexte hors de ce chemin interroge la
base SANS identité — le moteur le signale sur une table peuplée, et se tait sur
une table vide. »

Deux opérations partagées entre la création et le remplacement étaient écrites
en **méthodes statiques prenant le contexte en paramètre**. Le compilateur pose
ce paramètre en champ de la machine d'état asynchrone qu'il engendre, et
l'épreuve a vu `CreerUnProgramme+<ExercicesVisiblesAsync>d__4 (champ
« contexte »)`.

**L'exemption nommée aurait été le mauvais geste**, et pour une raison qui vaut
d'être retenue : elle aurait porté sur un nom **engendré** — `d__4` — qui change
au moindre remaniement. Le garde-fou serait devenu muet sans que personne le
remarque, et une exemption qui ne protège plus rien est pire qu'une absence
d'exemption, parce qu'elle rassure.

Les deux méthodes sont devenues des **méthodes d'instance**. La machine d'état
capture désormais `this` — un `CreerUnProgramme`, qui EST un gestionnaire — et
`RemplacerUnProgramme` l'emprunte par injection ordinaire.

### Deux épreuves manquaient, sur le chemin dangereux

La lecture du programme d'autrui était éprouvée ; **l'écriture ne l'était pas**.
Un `PUT` qui aboutirait réécrirait le programme de quelqu'un d'autre, là où une
lecture ne fait que le montrer. Idem pour la copie.

`Remplacer_le_programme_D_AUTRUI_rend_404` vérifie en outre que **le programme
de A est intact après coup** : le code de retour seul ne le dirait pas, puisqu'une
écriture partielle suivie d'un échec rendrait aussi 404.

### Les notes citaient le dépôt

Vingt-trois références à `docs/05-entrainement.md` figuraient dans le fichier de
référentiel — dont dix-huit **dans les notes affichées à l'utilisateur**. Une
note qui dit « `docs/05-entrainement.md` § 4 nomme les trois » montre la
plomberie du projet à quelqu'un qui voulait savoir pourquoi un mouvement est
absent.

Les notes sont reformulées ; l'en-tête du fichier garde ses références, parce
qu'il s'adresse à qui relit le contenu — le kinésithérapeute, notamment.

### Une branche morte évitée par la conception

Les gestionnaires reçoivent `ProgrammeValide`, jamais `EcritureDeProgramme`.
Prendre l'écriture brute aurait obligé chacun à porter une branche « et si
c'était invalide ? » que la route rend inatteignable — une branche morte que le
seuil aurait signalée à juste titre.

---

## 4. Corrections en cours de route

| Ce qui a été corrigé                       | Pourquoi                                                                                                                                                      |
| ------------------------------------------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **D73 affirmait une contre-vérité**        | « PostgreSQL refuse `LIKE` sur une collation non déterministe » — **faux en 18.6**, mesuré. Le motif de la décision a été réécrit sur les deux vraies raisons |
| `ToLowerInvariant` → `ToUpperInvariant`    | CA1308 refuse la mise en minuscules pour une normalisation ; la colonne du moteur a suivi                                                                     |
| L'entité s'appelle `TrainingProgram`       | `Program` est le type d'entrée engendré par `Palier.Api`, et `ArchitectureTests` s'en sert                                                                    |
| `SeanceRendue` → `SeanceDeProgrammeRendue` | Le nom était pris par les séances réelles                                                                                                                     |
| Six gestionnaires non enregistrés          | Le conteneur les déclare un par un ; l'attribut ne les scanne pas                                                                                             |

---

## 5. Ce qui reste dû, et qui doit se voir

**Les neuf programmes ne sont PAS relus par un kinésithérapeute**, alors que
`14-contenu.md` § 2 l'exige en toutes lettres : « relus par un kinésithérapeute
pour la partie contraintes […] tout aussi nécessaire » que la relecture du
diététicien pour la nutrition.

La réserve pèse le plus sur **cinq d'entre eux** — Reprise, Reprise cervicale,
Reprise lombaire, Épaule ménagée, Genou ménagé. Ce sont ceux qu'on propose à
quelqu'un qui revient de blessure, c'est-à-dire exactement la population que
`00-produit.md` place au cœur de la cible.

La mention figure en tête du fichier de référentiel, dans `db/SOURCES.md`, dans
`docs/decisions.md` et ici. C'est la seule façon honnête de livrer un contenu de
santé qui attend encore sa validation.

**Aucun écran.** `07-roadmap.md` les place à l'étape 2, et le front ne consomme
aujourd'hui aucune route d'entraînement.
