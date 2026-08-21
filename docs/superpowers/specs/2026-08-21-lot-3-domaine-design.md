# Lot 3 — `Palier.Domain` : le domaine complet

**Date :** 21 août 2026
**Statut :** premier jet, en attente de relecture
**Décisions sources :** D11 (le domaine ne référence rien), spec `2026-08-19-architecture-backend-csharp-design.md` § 4
**Documents métier :** `01-conformite.md` § 5, `04-nutrition.md`, `05-entrainement.md`

---

## 1. Objet

Ce lot livre `Palier.Domain` en entier : les grandeurs typées, les entités, les neuf calculs purs et les quatre planchers de sécurité. Le domaine est aujourd'hui amorcé — `MetabolismeDeBase.MifflinStJeor` et `Sexe`, en `decimal` nu.

La couverture exigée est de **100 %**. Sur ce projet elle n'est pas un confort : c'est le seul détecteur de code mort **public**, ce dont Roslyn est incapable par construction (`CLAUDE.md` § 4).

Toutes les formules de ce document ont été recoupées contre la littérature primaire le 21/08/2026. Ce qui suit distingue systématiquement **ce qui est confirmé**, **ce qui est contredit** et **ce qui reste à trancher**.

---

## 2. Les limites hautes ne sont pas des constantes

C'est le résultat le plus structurant de la vérification, et il commande le modèle de données avant de commander le code.

### Quatre statuts, pas deux

L'EFSA ne produit pas une seule sorte de valeur. Un produit qui les confondrait annoncerait un dépassement là où la science n'en définit aucun.

| Statut                      | Ce que cela autorise à dire                                                                                               |
| --------------------------- | ------------------------------------------------------------------------------------------------------------------------- |
| **UL établie**              | Comparer, et signaler un dépassement                                                                                      |
| **Safe level of intake**    | Situer l'apport. **Pas** de dépassement : l'avis dit que « le niveau où le risque commence à augmenter n'est pas défini » |
| **Aucune valeur dérivable** | Rien afficher, même si des données existent par ailleurs                                                                  |
| **Nutriment jamais évalué** | Rien afficher                                                                                                             |

### L'état au 21/08/2026, vérifié à la source

**Valeurs révisées récemment — celles qui piègent :**

| Nutriment       | Valeur en vigueur | Statut     | Avis | Remplace                        |
| --------------- | ----------------- | ---------- | ---- | ------------------------------- |
| Vitamine B6     | **12 mg/j**       | UL         | 2023 | 25 mg/j (SCF, 2000)             |
| Sélénium        | **255 µg/j**      | UL         | 2023 | 300 µg/j (SCF, 2000)            |
| Fer             | **40 mg/j**       | safe level | 2024 | 45 mg/j — valeur **américaine** |
| Manganèse       | **8 mg/j**        | safe level | 2023 | —                               |
| DHA supplémenté | **1 g/j**         | safe level | 2026 | — (adopté le 15/12/2025)        |

**Valeurs confirmées ou retenues :**

| Nutriment               | Valeur      | Statut        | Avis                |
| ----------------------- | ----------- | ------------- | ------------------- |
| Vitamine D              | 100 µg/j    | UL            | 2023                |
| Vitamine A préformée    | 3 000 µg RE | UL retenue    | 2024                |
| Vitamine E              | 300 mg/j    | UL retenue    | 2024                |
| Folates (acide folique) | 1 000 µg/j  | UL retenue    | 2023                |
| Zinc                    | 25 mg/j     | UL            | SCF, 2006           |
| Calcium                 | 2 500 mg/j  | UL            | 2012                |
| Cuivre                  | 5 mg/j      | UL            | SCF, 2003           |
| Iode                    | 600 µg/j    | UL            | SCF, 2002           |
| Magnésium               | 250 mg/j    | UL            | SCF, 2001           |
| **Vitamine C**          | **aucune**  | non dérivable | 2004, confirmé 2024 |

### Deux pièges que la valeur seule ne porte pas

**La forme chimique change la limite.** La niacine a **deux UL** : 10 mg/j pour l'acide nicotinique, **900 mg/j pour le nicotinamide**. Un facteur 90 entre deux formes du même nutriment. Une étiquette de complément porte l'une ou l'autre ; les confondre produit soit une alerte absurde, soit un silence dangereux.

**La source change le périmètre.** L'UL du magnésium ne vaut que pour les **sels solubles** des compléments, sans restriction sur l'apport alimentaire. Celle de la vitamine C, quand elle est évoquée, porte sur l'apport **supplémentaire**, pas total.

### Ce que le domaine en déduit

`Palier.Domain` **ne connaît aucune valeur de limite haute**. Il reçoit une référence et compare. Les valeurs vivent dans `nutrient_refs`, versionnées, datées et sourcées — ce que `03-donnees.md` prévoit déjà.

Une constante de limite haute écrite dans le code serait une bombe à retardement : un produit qui aurait figé la B6 à 25 mg laisserait passer sans rien dire un apport de 20 mg, soit **167 % de la limite en vigueur**. Une épreuve du harnais doit refuser qu'on en écrive une.

La référence transmise au domaine porte donc : la valeur, **son statut**, sa forme chimique applicable, son périmètre (total ou supplémenté), sa source et sa date.

---

## 3. Ce que la vérification des formules a trouvé

Trois écarts avec les documents du projet. Ils sont **signalés, non tranchés** — `CLAUDE.md` § 6.

### Écart 1 — Mifflin-St Jeor est déconseillée pour la population cible

`04-nutrition.md` § 1 pose Mifflin-St Jeor comme formule principale. La méta-analyse de référence sur les sportifs (Sports Medicine, 2023) conclut l'inverse : cinq équations satisfont les critères d'exactitude **sans différence significative** avec la mesure — Cunningham 1980, Cunningham 1991, Harris-Benedict, De Lorenzo et Ten-Haaf — tandis que Mifflin-St Jeor **sous-estime significativement** et figure parmi celles à éviter.

**Ten-Haaf 2014** porte précisément sur des _recreational athletes_ de 18 à 35 ans et place 80,2 % des sujets à ±10 % de la mesure, contre 40,7 à 63,7 % pour les autres équations. Formules exactes, en kcal/24 h :

```
Ten-Haaf (poids, taille, âge, sexe)
  11,936 × poids(kg) + 587,728 × taille(m) − 8,129 × âge
  + 191,027 × sexe(H=1, F=0) + 29,279

Ten-Haaf (masse maigre)
  22,771 × MM(kg) + 484,264
```

**À trancher :** la cible du produit est le pratiquant qui veut éviter les excès et les blessures, pas l'athlète de performance — mais c'est bien une population qui s'entraîne, et Ten-Haaf a été construite sur elle.

### Écart 2 — aucune formule ne relie charge et répétitions à une dépense

La question posée était de calculer l'apport calorique à partir de `Charge`, `Repetitions` et `Rir`. **La réponse de la littérature est négative.**

La revue systématique de 2024 sur les méthodes d'estimation de la dépense en musculation recense la calorimétrie indirecte, le lactate sanguin, les moniteurs portables et les METs — **aucune formule fondée sur charge × déplacement × répétitions, ni sur le volume de charge**.

La seule voie praticable reste le MET : **1 MET = 1 kcal/kg/h**, la musculation valant 3,5 · 5,0 · 6,0 selon l'intensité au Compendium, pour une variabilité mesurée de 3,0 à 8,0. Les mesures directes donnent 2,7 à 11 kcal/min chez l'homme et 2,3 à 5,2 chez la femme.

**Conséquence :** le forfait de 5 kcal/min de `04-nutrition.md` § 1 ne distingue ni le sexe ni la masse corporelle. Pour une femme de 55 kg il sort par le haut de la fourchette mesurée. `MET × Masse × durée` le rend dépendant de la masse, une donnée que le produit possède déjà.

**Ce qui n'est pas acquis :** relier le MET au RIR serait séduisant — un RIR bas signale un effort proche de l'échec — mais **aucune source ne le valide**. À ne pas implémenter.

### Écart 3 — le ratio tirage/poussée de 1,3 est une convention

`05-entrainement.md` § 4 fixe une cible minimale de 1,3. La littérature ne soutient aucune valeur précise : les recommandations vont de 1:1 à 3:1 selon les auteurs, et la pertinence même d'un ratio fixe est débattue. Une mesure sur sédentaires donne un rapport tirage/poussée de 1,36 chez l'homme et 2,69 chez la femme — c'est un **constat de population**, pas une cible thérapeutique.

**Proposition :** garder 1,3 comme paramètre de référence, jamais comme seuil de santé, et ne pas l'exprimer comme une constante du domaine.

### Ce que la vérification a confirmé

| Élément                                                       | Verdict                                                                                                                                                                                                                           |
| ------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Mifflin-St Jeor, forme `10 × P + 6,25 × T − 5 × A + 5 / −161` | Exacte — forme canonique de l'article de 1990                                                                                                                                                                                     |
| Katch-McArdle `370 + 21,6 × MM`                               | Exacte, et **identique à Cunningham 1991 révisée**. Cunningham 1980 est `500 + 22 × MM`, une autre équation — ne pas confondre                                                                                                    |
| Protéines 1,6 à 2,2 g/kg                                      | Fondé : Morton 2018 place le point d'inflexion à **1,62 g/kg**, l'intervalle de confiance à 95 % montant à 2,2                                                                                                                    |
| Planchers 1 200 kcal femme et 1 500 homme                     | Bornes **basses** des recommandations cliniques (1 200-1 500 femme, 1 500-1 800 homme)                                                                                                                                            |
| Eau, 35 ml/kg                                                 | Cohérent avec l'EFSA : 2,0 L femme et 2,5 L homme correspondent à 57 et 71 kg                                                                                                                                                     |
| Epley et Brzycki                                              | Précises **sous 10 répétitions**. Le projet signale la marge au-delà de 12 : plus permissif que la littérature                                                                                                                    |
| Coefficient 7 700 kcal/kg                                     | Règle de Wishnofsky, largement critiquée en **prédiction**. L'usage du projet est **rétrospectif** — inférer une dépense passée — ce qui est un autre problème, mais la composition de la perte affecte quand même le coefficient |

---

## 4. La chaîne de l'apport calorique

Où chaque grandeur intervient réellement.

```
  Masse, Taille, Age, Sexe ─────────────────▶ métabolisme de base
  PourcentageMasseGrasse ──▶ masse maigre ──▶ métabolisme de base (plus précis)

  métabolisme de base × facteur d'activité (1,25 · 1,35 · 1,45 · 1,60)
        +
  MET × Masse × durée de séance
        =
  dépense totale, RENDUE AVEC SA FOURCHETTE ±10 à 15 %
        ± ajustement choisi, borné à −20 % / +15 %
        =
  apport cible
        ▼
  plancher appliqué en DERNIER, non contournable
```

**Le choix de la formule suit la donnée disponible**, ce qui règle l'écart 1 sans élire un vainqueur universel :

| Donnée disponible                  | Formule                              | Motif                                                                                                 |
| ---------------------------------- | ------------------------------------ | ----------------------------------------------------------------------------------------------------- |
| Pourcentage de masse grasse connu  | Katch-McArdle / Cunningham 1991      | Sans biais significatif chez le sportif ; le sexe sort de l'équation, la composition le contient déjà |
| Poids, taille, âge, sexe seulement | **à trancher** — Ten-Haaf ou Mifflin | Ten-Haaf est validée sur la population cible ; Mifflin-St Jeor est ce que porte `04-nutrition.md`     |

**Ce que ces grandeurs ne font pas.** `Charge`, `Repetitions` et `Rir` **n'entrent pas** dans le calcul calorique — aucune formule validée ne les y relie. Elles servent à la force estimée, au volume par groupe musculaire et à la détection de plateau. Les faire entrer dans une dépense reviendrait à inventer un chiffre de santé, ce que `01-conformite.md` interdit.

**Après trois semaines, tout ceci est remplacé** par le TDEE adaptatif, qui mesure au lieu d'estimer et refuse de conclure sous 14 jours de saisie et 10 pesées.

---

## 5. Les grandeurs typées

Un type par grandeur, chacun portant ses propres bornes. Le générique a été écarté parce qu'il aurait imposé un plafond commun : `Masse` est un poids corporel, `Charge` est une barre, et les deux n'ont pas le même domaine de validité.

`Masse` · `Taille` · `Age` · `Energie` · `Charge` · `Repetitions` · `Rir` · `PourcentageMasseGrasse` · `MasseNutriment` · `VolumeEau`

Chacun est un `readonly record struct` construit par une fabrique qui refuse l'invalide. Un type du domaine ne peut pas exister dans un état incorrect : c'est la traduction de « valider à la frontière, puis faire confiance au type » (`CLAUDE.md` § 4).

`MasseNutriment` porte son unité — g, mg, µg — parce qu'une UL s'exprime dans l'une des trois et qu'une conversion implicite est une erreur d'un facteur mille.

---

## 6. Les trois retours qui protègent une règle de conformité

Les points où un type mal choisi produit une faute, pas un défaut.

| La règle                                                   | Le type naïf                                                | Le type retenu                                                                        |
| ---------------------------------------------------------- | ----------------------------------------------------------- | ------------------------------------------------------------------------------------- |
| « marge de ±10 à 15 %, **affichée systématiquement** »     | `decimal` : la fourchette devient facultative, donc oubliée | `DepenseEstimee` porte estimation **et** bornes — impossible de l'afficher sans elles |
| « interdit d'inventer une limite haute »                   | `decimal` à zéro : tout devient un dépassement              | Comparaison à **quatre** issues, selon le statut de la référence (§ 2)                |
| « au-delà de 15 répétitions effectives, ne rien afficher » | `decimal` : il faut bien rendre un nombre, donc un faux     | `ForceEstimee` rend une estimation **avec sa fiabilité**, ou rien                     |

---

## 7. Les quatre planchers

Exprimés comme **invariants**, non comme validations : un objectif sous le plancher ne se construit pas, il n'est pas refusé après coup.

1. **Plancher calorique** — 1 200 kcal femme, 1 500 homme. Non contournable, y compris par configuration.
2. **Planchers protéique et lipidique** — empêchent l'élimination d'un macronutriment.
3. **Perte de poids rapide** — plus de 1 % du poids par semaine sur trois semaines.
4. **Restriction sévère** — apports répétés très en dessous du métabolisme de base.

Les points 3 et 4 produisent un **code d'orientation**, jamais une phrase : les libellés vivent en base et passent par i18next.

---

## 8. Ce que le domaine ne contient pas

Aucun libellé, aucune traduction, aucune valeur de limite haute, aucun accès base, réseau ou interface. `Palier.Domain` ne référence aucun projet — la compilation le garantit, et le harnais l'éprouve déjà.

Aucune classification d'aliment, aucun score, aucune notion d'aliment interdit, aucune série de jours consécutifs (`01-conformite.md` § 5).

---

## 9. Ce que ce lot casse

`MetabolismeDeBase.MifflinStJeor` change de signature — `decimal` nus vers grandeurs typées — avec son test. C'est son unique appelant, vérifié le 21/08/2026.

---

## 10. Points ouverts, à trancher par le porteur du projet

1. **Mifflin-St Jeor ou Ten-Haaf** comme formule sans composition corporelle (§ 3).
2. **Le forfait de 5 kcal/min** remplacé par `MET × Masse × durée` (§ 3).
3. **Le seuil de fiabilité du 1RM** : 12 répétitions effectives dans le projet, 10 dans la littérature (§ 3).
4. **La forme chimique et le périmètre** deviennent des colonnes de `nutrient_refs` — c'est un changement de schéma, donc une décision (§ 2).
5. **La validation du diététicien** reste due. Elle ne bloque pas ces calculs ; elle bloquera leur affichage.

Ces cinq points appellent une entrée datée dans `docs/decisions.md` une fois tranchés.

---

## 11. Sources

**Métabolisme de repos**

- [Accuracy of Resting Metabolic Rate Prediction Equations in Athletes: A Systematic Review with Meta-analysis](https://pmc.ncbi.nlm.nih.gov/articles/PMC10687135/) — Sports Medicine, 2023
- [Accuracy of Resting Metabolic Rate Prediction Equations in Sport Climbers](https://pmc.ncbi.nlm.nih.gov/articles/PMC10001726/) — 2023
- [Bias and accuracy of resting metabolic rate equations in non-obese and obese adults](https://www.sciencedirect.com/science/article/abs/pii/S0261561413001003)

**Dépense en musculation**

- [Methods to Assess Energy Expenditure of Resistance Exercise: A Systematic Scoping Review](https://pmc.ncbi.nlm.nih.gov/articles/PMC11393209/) — 2024
- [Energy Cost of Resistance Exercises: an Update](https://www.ncbi.nlm.nih.gov/pmc/articles/PMC3588900/)

**Limites hautes EFSA**

- [Vitamine B6 — UL 12 mg/j](https://pmc.ncbi.nlm.nih.gov/articles/PMC10189633/) — 2023
- [Sélénium — UL 255 µg/j](https://pmc.ncbi.nlm.nih.gov/articles/PMC9854220/) — 2023
- [Fer — safe level 40 mg/j, aucune UL](https://pmc.ncbi.nlm.nih.gov/articles/PMC11167337/) — 2024
- [Manganèse — safe level 8 mg/j](https://www.ncbi.nlm.nih.gov/pmc/articles/PMC10704406/) — 2023
- [Folate — UL 1 000 µg/j retenue](https://www.ncbi.nlm.nih.gov/pmc/articles/PMC10641704/) — 2023
- [Vitamine A préformée et β-carotène — UL 3 000 µg RE retenue](https://www.ncbi.nlm.nih.gov/pmc/articles/PMC11154838/) — 2024
- [Vitamine E — UL retenues](https://efsa.onlinelibrary.wiley.com/doi/10.2903/j.efsa.2024.8953) — 2024
- [DHA supplémenté — safe level 1 g/j](https://efsa.onlinelibrary.wiley.com/doi/10.2903/j.efsa.2026.9858) — adopté le 15/12/2025
- [Vitamine C — aucune UL dérivable](https://www.efsa.europa.eu/en/efsajournal/pub/59) — 2004
- [Guidance for establishing and applying tolerable upper intake levels](https://efsa.onlinelibrary.wiley.com/doi/10.2903/j.efsa.2024.9052) — 2024

**Macronutriments**

- [Morton et al. 2018 — méta-analyse protéines](https://www.ncbi.nlm.nih.gov/pmc/articles/PMC5867436/)

**Coefficient de perte de masse**

- [Why is the 3500 kcal per pound weight loss rule wrong?](https://www.researchgate.net/publication/239943510_Why_is_the_3500_kcal_per_pound_weight_loss_rule_wrong)
