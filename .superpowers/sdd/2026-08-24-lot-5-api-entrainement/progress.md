# Lot 5 — journal de bord

**Exécuté en autonomie**, dans la nuit du 23 au 24 août 2026, pendant que le
porteur du projet dormait. Sa demande : « tu peux commencer le point 5 de façon
autonome ».

**État : livré.** Huit tâches, vingt-trois routes, deux tables nouvelles. Ce
qui reste à décider est au § 5, et rien n'y est bloquant pour la suite.

---

## 1. Ce qui est livré

| #   | Tâche                                    | Routes | Épreuves |
| --- | ---------------------------------------- | ------ | -------- |
| 1   | La séance — ouvrir, lire, clore, effacer | 4      | 10       |
| 2   | La pagination par curseur                | 1      | 8        |
| 3   | Les séries — ajouter, retirer            | 2      | 28       |
| 4   | Le poids corporel et son constat         | 2      | 28       |
| 5   | Force estimée, plateau, volume           | 2      | 18       |
| 6   | Le catalogue mixte                       | 3      | 38       |
| 7   | Le ressenti par exercice                 | 2      | 31       |
| 8   | Les contraintes déclarées                | 3      | 21       |

**796 épreuves au total** — 205 au domaine, 175 à l'application, 416 à la base
— contre 545 avant le lot. Couverture : **100 %** sur `Palier.Domain` et
`Palier.Application`, **98,24 / 81,62 / 92,75** sur les adaptateurs, pour des
seuils à 96 / 79 / 71. `npm run verify` passe en 465 s, seize étapes.

**Le premier tiers du lot n'a demandé aucune table nouvelle.** Le socle avait
déjà `workouts`, `sets`, `body_weight`, `exercises` et la vue `weekly_volume` ;
ce qui manquait entre le schéma et l'API, c'étaient les cas d'usage —
`Palier.Application` ne portait aucun gestionnaire, et l'attribut
`[GestionnaireDeCasDUsage]` n'était porté par personne.

**Deux tables nouvelles, et deux seulement** — `exercise_feedback` et
`user_constraints`, chacune avec sa politique RLS, ses trois rôles et son
épreuve d'isolation. C'est D39 appliquée à la lettre ; onze tables restent à
venir avec les lots qui les exigeront.

---

## 2. Ce que les épreuves ont attrapé, et qui aurait échappé

### La garde de perte de poids était SILENCIEUSE

Le défaut le plus grave du lot, et il ne venait pas du code écrit cette nuit.

`PerteDePoidsRapide` existait depuis le lot 3, éprouvée, couverte à 100 %. Elle
**suppose que ses entrées sont hebdomadaires** : elle compare des valeurs
consécutives et lit chaque écart comme « une semaine ». La série brute de
`body_weight` est quotidienne chez qui pèse tous les jours.

L'effet n'est pas un faux positif, c'est un **silence**. La variation d'un jour
à l'autre reste sous le seuil de 1 %, donc la boucle sort à la première
comparaison et rend `false`. La détection serait restée muette exactement chez
les utilisateurs les plus assidus — ceux qui surveillent leur poids de près,
c'est-à-dire la population que `01-conformite.md` § 5 nomme comme celle qu'il
faut protéger. **Aucune épreuve n'aurait rougi** : le domaine faisait
correctement ce qu'on lui demandait, sur des entrées qui ne voulaient pas dire
ce qu'il croyait.

D61 tranche : moyenne hebdomadaire, semaines ISO.

### Les listes du corps JSON arrivent à `null`

`CreationDExercice.MusclesSecondaires` est déclaré non-nullable. Un client qui
omet le champ — ce qui est légitime, tous les exercices n'ont pas de muscle
secondaire — produit `null`, et l'annotation de nullabilité n'y change rien :
elle ne survit pas au passage par le réseau. Lire `.Count` dessus rendait 500
sur une requête valide.

Trouvé par le seuil de couverture, qui signalait une branche du motif
`is not { Count: > 0 }` jamais franchie.

### Le drapeau d'échauffement n'était lu nulle part

Deux accesseurs d'`AjoutDeSerie` n'apparaissaient dans aucune épreuve. Rien
n'aurait rougi si le gestionnaire avait ignoré `Echauffement` en écrivant en
base — et une série d'échauffement comptée comme série dure fausse le volume
hebdomadaire et gonfle l'estimation de force.

### `const decimal` plombe la couverture

`IncrementMaximal` restait éternellement non couverte. Un `decimal` n'est pas
une constante de compilation en IL : le compilateur engendre un constructeur
statique jamais exécuté, et lire la constante depuis une épreuve n'y change
rien, puisque la valeur est inlinée à l'appel. Remplacée par une propriété,
comme le domaine le fait déjà pour ses seuils. Les `const int` voisines n'ont
pas ce défaut.

### Retirer une série par la mauvaise séance

RLS garantit que la série appartient à l'appelant ; il ne garantit **pas**
qu'elle appartient à la séance nommée dans l'URL. Sans la clause `WorkoutId`,
`DELETE /seances/{A}/series/{s}` effaçait une série de la séance B — ses
propres données, mais pas celles qu'il visait, et sans le moindre signal.
L'épreuve provoque ce cas avec les deux séances du **même** utilisateur, pour
que RLS laisse passer.

---

## 3. Ce que Context7 a donné, et qui n'aurait pas été trouvé de mémoire

La pagination par curseur composite demandait de comparer `(started_at, id)`.
`Guid` n'implémente pas `<` en C#, et `CompareTo` n'est pas garanti traduisible.

Npgsql expose `EF.Functions.LessThan` sur un **tuple**, traduit en comparaison
de row values PostgreSQL — `(started_at, id) < (@a, @b)` — qui se sert de
l'index `workouts (owner_id, started_at desc)` posé par D39, là où un `OR`
force souvent un parcours.

---

## 4. Les codes de refus, et pourquoi ils diffèrent

Trois situations, trois codes, et le choix n'est pas mécanique.

| Situation                          | Code | Motif                                                          |
| ---------------------------------- | ---- | -------------------------------------------------------------- |
| La séance d'autrui                 | 404  | Son existence est un secret. Un 403 la confirmerait            |
| L'exercice personnalisé d'autrui   | 404  | Idem — sinon la route devient un oracle d'existence            |
| Le catalogue **public**            | 403  | L'appelant vient de le lire. Un 404 serait un mensonge         |
| Un exercice utilisé par des séries | 409  | Ni permission ni absence : un conflit d'état                   |
| L'exercice inconnu dans un corps   | 400  | La ressource visée par l'URL existe ; c'est la valeur qui faut |

Le gestionnaire rend `null` pour les deux causes indiscernables — « n'existe
pas » et « n'est pas à vous » — de sorte que l'API ne peut pas les distinguer
même par erreur.

---

## 5. Ce qui attend une décision du porteur

**Rien de ceci ne bloque le lot 6.**

1. **Le filtrage du catalogue par les contraintes.** `05-entrainement.md` § 4
   décrit un filtrage automatique, mais laisse ouvert ce qu'on fait d'un
   exercice contre-indiqué : l'**exclure**, ou l'**afficher marqué**. Les deux
   se défendent — exclure protège, marquer informe — et le choix se voit par
   l'utilisateur. L'épreuve
   `Declarer_une_contrainte_ne_FILTRE_PAS_encore_le_catalogue` rougira le jour
   où le filtrage arrivera, avec un message qui explique pourquoi.

2. **D39**, considérée comme prise (D62). Trois lots s'appuient dessus. Le coût
   de revenir en arrière augmente à chaque lot.

3. **D61 — la moyenne hebdomadaire.** Elle touche un garde-fou de santé, et
   elle a été prise faute d'avoir pu demander un avis clinique. Le
   kinésithérapeute que `14-contenu.md` prévoit est la bonne source.

4. **`/code-review ultra`** sur le lot — lui seul peut le lancer.

---

## 6. Ce que le lot NE fait pas, et pourquoi

- **Les seuils du ressenti** — « 2 `pain` consécutifs → orientation vers un
  professionnel ». Ils produisent des messages destinés à l'utilisateur, dont
  l'un touche à la santé. Le libellé vit en base, versionné et validé. La règle
  arrive avec l'écran qui la montre.
- **Le ratio tirage/poussée.** `exercises` ne porte aucune colonne disant si un
  mouvement tire ou pousse, et le déduire des muscles serait faux. La colonne se
  pose avec le catalogue — étape 1 bis — où le rôle se renseigne exercice par
  exercice. D62.
- **Aucun écran.** Ils appartiennent au lot 6.
- **Rien de la nutrition.** Elle est fermée à deux verrous depuis le lot 4.

---

## 7. Un fait à signaler, non expliqué

`dotnet test back/Palier.sln --settings back/coverage.runsettings` — la
commande exacte de l'étape `back:test` — a **échoué une fois** sur le seuil de
couverture de `Palier.Database.Tests`, en annonçant un total sous 96 %. Relancé
isolément, le même projet mesure **98,24 %**, et `npm run verify` a passé six
fois de suite avant et après.

**Je n'ai pas la cause.** L'hypothèse la plus plausible est une contention entre
les trois projets de test lancés en parallèle sur la solution, alors qu'un autre
`dotnet test` tournait encore — mais c'est une hypothèse, pas une mesure, et ce
dépôt refuse de présenter l'une pour l'autre. C'est écrit ici pour que la
prochaine occurrence ne soit pas prise pour la première.
