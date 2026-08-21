# SDD ledger — plan : docs/superpowers/plans/2026-08-21-lot-3-domaine.md

Branche : `feat/lot-3-domaine`. Session unique, en autonomie, du 21/08/2026 02 h 45 à 03 h 46.

---

## Ce qui a changé

**Livré :** `Palier.Domain` complet — 30 fichiers de domaine, 14 fichiers de tests, **176 tests unitaires**, couverture **100 % en lignes, branches et méthodes**, seuil appliqué par coverlet MSBuild.

| Dossier         | Contenu                                                                                                                                                        |
| --------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `Grandeurs/`    | `Masse` `Taille` `Age` `Energie` `Charge` `Repetitions` `Rir` `PourcentageMasseGrasse` `MasseNutriment` `VolumeEau` `Sexe` `ProvenanceMesure` `UniteNutriment` |
| `Depense/`      | `MetabolismeDeBase` (Mifflin-St Jeor, Katch-McArdle, `Estimer`), `NiveauActivite`, `DepenseEstimee`, `DepenseTotale`, `TdeeAdaptatif`                          |
| `Objectifs/`    | `CibleMacronutriments`, `PlancherCalorique`, `ApportCible`                                                                                                     |
| `Nutriments/`   | `StatutReference`, `ReferenceNutriment`, `ApportAgrege`, `ComparaisonReference`                                                                                |
| `Entrainement/` | `ForceEstimee`, `Fiabilite`, `VolumeParGroupe`, `DetectionPlateau`                                                                                             |
| `Securite/`     | `PerteDePoidsRapide`, `RestrictionSevere`                                                                                                                      |

**Six décisions consignées** — D48 à D53 dans `docs/decisions.md`, portant chacune son motif et ce qui la rouvrirait.

**Un garde-fou ajouté** — `back/tests-harness/references-nutriments.test.mjs` refuse qu'une valeur de référence sanitaire soit écrite en dur dans le domaine.

**Renommage structurel :** `Palier.Domain/Energie/` devient `Depense/`, et `Sexe` rejoint `Grandeurs/`.

---

## Ce qui a cassé

**1. Le type `Energie` entrait en collision avec l'espace de noms `Palier.Domain.Energie`.** Le compilateur tranchait en faveur de l'espace de noms, et l'erreur ne se lisait pas dans le code fautif — `Energie.DepuisKilocalories` était résolu comme un membre de `Palier.Domain.Tests.Energie`. Le cycle rouge l'a révélé à la tâche 2, alors que la structure tenait encore en deux fichiers. Le plan ne l'avait pas vu.

**2. J'ai renommé `Energie/` sans chercher qui lisait.** Deux épreuves du harnais écrivaient leurs sondes dans `back/Palier.Domain/Energie/` — `couverture.test.mjs` et `rigueur.test.mjs`. Le harnais backend est resté rouge jusqu'à la tâche 15, où je m'en suis aperçu en lançant `test:harness:back`. La règle « jamais de renommage sans avoir cherché qui lit » existe précisément pour ça, et je ne l'ai pas appliquée.

**3. Mon premier garde-fou ne mordait pas, et son épreuve de franchissement le cachait.** Le `\b` initial du motif le rendait aveugle à la forme `=> 25m` : une limite de mot exige une transition entre caractère de mot et non-mot, et `=` n'en est pas un. L'épreuve n'essayait que la forme `const` et restait verte, pendant que la violation la plus naturelle à écrire en C# moderne passait au travers. **Mesuré :** un témoin `public static decimal ZincUlMg => 25m;` posé dans le domaine réel a produit **5 tests verts**.

**4. Une `const decimal` publique fait chuter la couverture.** En C# une constante décimale n'est pas une constante IL : le compilateur produit un champ `static readonly` initialisé par un constructeur statique, que personne n'exécute puisque les lectures sont remplacées par le littéral à la compilation. Mesuré à **98,03 % de méthodes**. Et un test qui lit la constante n'y change rien — c'est justement la lecture qui est inlinée. Toutes les valeurs exposées du domaine sont donc des **propriétés statiques**.

**5. `CA2225` interdit une surcharge d'opérateur sans méthode nommée `Add`** — en anglais, dans un domaine écrit en français. L'opérateur `+` de `MasseNutriment` a été retiré au profit d'une méthode `Somme`.

**6. `CS1574` est une erreur, pas un avertissement** : un `<see cref="Charge" />` écrit avant l'existence de `Charge` a fait échouer la compilation.

**7. `IDE1006` exige le préfixe underscore** sur un champ statique privé ; `back:format` refusait le build sans lui.

---

## Ce que je signale sans y avoir touché

**1. La valeur MET exacte n'a pas pu être obtenue.** Les sources donnent **3,5 ou 5,8** pour le code « resistance training, multiple exercises, 8-15 reps at varied resistance », et **6,0 ou 9,0** pour l'effort vigoureux. Le site du Compendium et ses PDF refusent l'accès automatisé — HTTP 403, trois tentatives sur trois URL. La valeur doit être relevée à la main avant d'alimenter la base. Le domaine ne s'en trouve pas bloqué : il reçoit le MET en paramètre.

**2. Deux changements de schéma sont ouverts, et ils appartiennent au porteur du projet** (`CLAUDE.md` § 6) : la **forme chimique** et le **périmètre** dans `nutrient_refs` — sans eux, la niacine est indistinguable entre 10 mg et 900 mg — et la **provenance** de la mesure de masse grasse dans le profil, sans laquelle `Estimer` ne peut pas choisir sa formule.

**3. `verify` dure 264 s contre un seuil de 90 s**, et le dépôt le signale à chaque exécution. `harnais:back` en consomme 110 s à lui seul, soit 42 %.

**4. Le détecteur de tag PostgreSQL en est à son deuxième faux positif** — un commentaire de `CollationTests.cs` au lot 2, un worktree le 21/08. Le motif se répète : il cherche une chaîne sans distinguer une déclaration d'une mention. La correction d'hier a traité le périmètre, pas cette confusion.

**5. Les quatre planchers ne sont pas validés par un diététicien.** Les valeurs de `01-conformite.md` § 5 sont appliquées telles quelles. Deux d'entre elles ne sont **pas** dans le document et sont de mon fait : **80 % du métabolisme de base sur cinq jours consécutifs** pour la restriction sévère, là où le document dit seulement « apports très en dessous du métabolisme de base répétés ». Elles sont exposées en propriétés publiques pour être corrigées sans lire le code.

---

## Le franchissement

**Le seuil de couverture, éprouvé en conditions réelles.** Il a rougi **trois fois** au cours du lot, chaque fois sur un défaut distinct, et il a fallu le comprendre plutôt que le contourner :

| Ce qu'il a refusé                              | Ce que c'était vraiment                                                                                                                                                                                                                                                             |
| ---------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `DepenseEstimee.cctor` — 98,03 % de méthodes   | une `const decimal` publique et son constructeur statique mort                                                                                                                                                                                                                      |
| `ReferenceNutriment.get_Source` et `get_Annee` | deux propriétés que rien ne lisait — sa fonction de détecteur de code mort public                                                                                                                                                                                                   |
| `SeanceDExercice.get_Jour`                     | **un vrai défaut** : la détection de plateau ne triait pas, donc « les trois dernières séances » désignait les trois derniers éléments de la liste et non les trois plus récentes. Une collection remontée sans `ORDER BY` aurait conclu sur les mauvaises séances, silencieusement |

**Le garde-fou des références sanitaires, éprouvé sur le domaine réel.** Un fichier `Violation.cs` portant `public static decimal SeleniumUlUg => 255m;` a été écrit dans `back/Palier.Domain/Nutriments/`, ajouté à l'index git, et le contrôle a refusé avec le chemin exact et le message attendu :

```
× aucune limite haute ni niveau sûr n'est écrit dans Palier.Domain
  + "back/Palier.Domain/Nutriments/Violation.cs → public static decimal SeleniumUlUg => 255m;"
```

Le témoin retiré, `git status` rend **0 modification**.

**Les trois formes d'écriture sont éprouvées** — `const`, propriété expression `=>`, `static readonly` — parce que la première version ne voyait que la première. Et **les deux branches inverses** aussi : un commentaire citant la valeur passe, une valeur qui n'est pas une référence sanitaire (`EauMlParKg => 35m`) passe.

**La branche « plus de cible ».** Le parcours lit `git ls-files` ; hors d'un dépôt, l'appel rend **128** et le message porte le `fatal: not a git repository` de git, au lieu de rendre une liste vide qui aurait été verte en ne lisant rien. Une assertion refuse par ailleurs qu'il lise moins de dix fichiers.

**`npm run verify` : les seize étapes passent**, 264,5 s.

---

## Ce qui n'a pas pu être vérifié

**1. Aucun de ces calculs n'a été confronté à une donnée réelle.** Les valeurs de test viennent des documents du projet et de la littérature ; aucune n'a été comparée à une mesure de calorimétrie indirecte.

**2. La tâche 7 s'est écartée du cycle rouge-vert.** Le test et l'implémentation du TDEE adaptatif ont été écrits avant le premier lancement : le rouge n'a jamais été observé pour cette tâche. Les treize autres ont vu leur rouge, constaté par erreur de compilation.

**3. La couverture à 100 % ne dit rien de la justesse des formules.** Elle dit que chaque ligne et chaque branche sont exécutées, pas que le résultat est physiologiquement correct. Les valeurs attendues des tests ont été calculées à la main depuis les formules des documents.

**4. Le garde-fou des références ne couvre que `back/Palier.Domain/`.** Une valeur d'UL écrite en dur dans `Palier.Application` ou `Palier.Api` ne serait vue par personne.

**5. La liste des valeurs surveillées est celle du 21/08/2026.** Une révision future de l'EFSA introduira une valeur que ce détecteur ne connaît pas.

---

## Les skills invoqués

`superpowers:brainstorming` a classé le lot en architectural et imposé la spec avant le plan. `superpowers:writing-plans` a produit les seize tâches. `superpowers:executing-plans` a conduit l'exécution. `superpowers:systematic-debugging` a servi une fois — sur le détecteur qui ne mordait pas — et il a évité de conclure au hasard : la mesure a montré que le motif était en cause, pas le parcours de fichiers.

`superpowers:subagent-driven-development` **n'a pas été utilisé** : la consigne de session interdit d'appeler l'outil d'agents sans demande explicite. L'exécution s'est faite en ligne.
