# 04 — Logique nutritionnelle

Toutes les formules de ce document sont implémentées dans des modules purs et testés unitairement. **Le LLM ne calcule jamais ces valeurs** ; il les reçoit déjà calculées.

---

## 1. Dépense énergétique

### Métabolisme de base — Mifflin-St Jeor

```
Homme : 10 × poids(kg) + 6,25 × taille(cm) − 5 × âge + 5
Femme : 10 × poids(kg) + 6,25 × taille(cm) − 5 × âge − 161
```

Si le pourcentage de masse grasse est connu, **Katch-McArdle** est plus précis et le sexe disparaît de l'équation, puisqu'il est déjà contenu dans la composition corporelle :

```
370 + 21,6 × masse maigre(kg)
```

### Dépense totale

```
TDEE = MB × facteur d'activité + coût de l'entraînement
```

**Le facteur d'activité vient de deux questions concrètes**, posées à l'onboarding : temps de marche quotidien approximatif, et métier assis ou debout.

| Profil                               | Facteur |
| ------------------------------------ | ------- |
| Bureau, moins de 30 min de marche    | 1,25    |
| Bureau, 30 à 60 min de marche        | 1,35    |
| Mixte assis/debout, marche régulière | 1,45    |
| Métier debout ou très actif          | 1,60    |

**La lecture automatique des pas est impossible sur une application web.** L'API REST Google Fit est dépréciée fin 2026 et fermée aux nouvelles inscriptions depuis mai 2024 ; Health Connect stocke les données sur l'appareil et n'est accessible que depuis une application Android native ; HealthKit n'expose aucune API web. Une saisie manuelle hebdomadaire des pas reste proposée en option, sans être requise.

Ces catégories restent imprécises — c'est assumé, parce qu'elles ne servent que trois semaines : le TDEE adaptatif les remplace ensuite par une mesure réelle.

Coût de l'entraînement : environ 5 kcal par minute de musculation, ajouté au prorata des séances effectivement enregistrées.

### L'activité mentale n'entre pas dans le calcul

Le cerveau consomme environ 20 % du métabolisme de base, mais cette consommation est quasi constante. Une journée de travail intellectuel intense ajoute quelques dizaines de kilocalories, dans le bruit de mesure.

Le stress est capté comme **donnée de contexte** — pour expliquer une stagnation ou moduler l'entraînement — jamais comme facteur calorique. Aucun multiplicateur « charge mentale » ne doit exister dans le code.

### Marge d'erreur — affichage obligatoire

Les formules prédictives ont une erreur réelle de ±10 à 15 %. **Cette marge est affichée avec le chiffre, systématiquement.**

> Dépense estimée : **2 400 kcal**
> Fourchette réelle probable : 2 100 – 2 750
> Estimation de départ, corrigée sur vos données à partir de la semaine 3.

Un utilisateur qui prend le chiffre pour une vérité et ne bouge pas en trois semaines conclut que son corps est cassé, puis se désabonne.

### TDEE adaptatif — le vrai différenciateur

À partir de trois semaines de données (apports saisis + poids), la dépense se **calcule** au lieu de s'estimer :

```
TDEE réel ≈ apport moyen quotidien − (Δpoids sur la période × 7700 / jours)
```

Conditions de déclenchement : au moins 14 jours de saisie alimentaire jugée complète et 10 pesées sur la période. Lissage sur moyenne mobile, jamais sur des valeurs brutes.

La formule sert de point de départ en semaine 1. À la semaine 4, elle n'est plus utilisée du tout. C'est ce qui distingue un produit sérieux d'un calculateur en ligne.

---

## 2. Références de macronutriments

Pré-remplies, **toujours modifiables par l'utilisateur**.

| Macronutriment | Fourchette                          | Défaut   |
| -------------- | ----------------------------------- | -------- |
| Protéines      | 1,6 – 2,2 g/kg                      | 1,8 g/kg |
| Lipides        | 0,8 – 1,2 g/kg                      | 1,0 g/kg |
| Glucides       | le reste des calories               | calculé  |
| Fibres         | 25 – 35 g                           | 30 g     |
| Eau            | 35 ml/kg + compensation de l'effort | calculé  |

Chaque objectif s'affiche avec sa source :

> **Protéines — 131 g/jour**
> Valeur par défaut : 1,8 g/kg pour 73 kg
> Fourchette de référence : 117 – 160 g _(1,6 – 2,2 g/kg, recommandations en musculation)_
> `[modifier]`

**Ne jamais pré-remplir un objectif de perte de poids.** Le défaut est la maintenance. Si l'utilisateur veut un déficit ou un surplus, il le choisit dans une fourchette encadrée : −20 % à +15 % de la maintenance, jamais au-delà.

---

## 2 bis. Hydratation

Quatrième pilier, et le paramètre le plus négligé par la concurrence.

### Cible

```
base       = 35 ml × poids(kg)
entraînement = + 500 ml par heure de séance
chaleur     = + 500 ml si température élevée déclarée
cardio      = + 500 ml par sortie zone 2
```

Pour 73 kg : 2 555 ml de base, environ 3 000 ml un jour d'entraînement. La cible est **dynamique** et se recalcule chaque jour selon les séances enregistrées.

### Saisie

Un geste, depuis n'importe quel écran. Contenants configurables par l'utilisateur : verre 250 ml, bouteille 500 ml, gourde personnalisée, mug. Un appui = un ajout, appui long = correction.

Les boissons contenant de l'eau comptent au prorata : café et thé à 100 %, soupes et bouillons selon leur volume. Les aliments à forte teneur en eau ne sont pas comptabilisés — la précision serait illusoire.

### Affichage

Même composant que le reste du produit : la règle graduée. Valeur, cible, position. Aucune injonction à boire.

Une limite haute existe et doit être respectée : au-delà d'environ 1 litre par heure sur plusieurs heures, le risque d'hyponatrémie est réel. Le produit signale un apport inhabituellement élevé, sans dramatiser.

### Pourquoi c'est stratégique

Quelqu'un qui note son eau ouvre l'application cinq fois par jour. C'est le geste le plus fréquent du produit, celui qui installe l'habitude, et il coûte très peu à construire.

---

## 3. Micronutriments et limites hautes

Source : **EFSA Dietary Reference Values**, avec les _Tolerable Upper Intake Levels_.

**Les UL n'existent que pour une quinzaine de nutriments.** Pour tous les autres, aucun seuil haut ne doit être affiché, et il est formellement interdit d'en inventer un. Le champ `ul` reste `NULL` et l'interface n'affiche pas de limite.

Nutriments prioritaires pour la V1, ceux où la surcharge par complémentation est réelle :

| Nutriment                                         | Enjeu                                            |
| ------------------------------------------------- | ------------------------------------------------ |
| Zinc                                              | Excès chronique → déplétion en cuivre            |
| Vitamine B6                                       | Neuropathie périphérique au long cours           |
| Vitamine A (rétinol)                              | Tératogénicité, toxicité hépatique               |
| Vitamine D                                        | Hypercalcémie à très forte dose                  |
| Fer                                               | Accumulation chez l'homme non carencé            |
| Magnésium                                         | Effet laxatif, seuil de supplémentation distinct |
| Sélénium, iode, cuivre, calcium, folates, niacine | UL définis                                       |

---

## 4. La règle centrale du produit

**Additionner alimentation et compléments par nutriment, puis comparer aux références.**

C'est la fonctionnalité qui n'existe nulle part ailleurs, et c'est la raison d'être du produit.

```
Zinc — journée du 18 août
  alimentation      17,2 mg
  compléments       25,0 mg
  ─────────────────────────
  total             42,2 mg
  limite haute EFSA 25,0 mg     ← dépassement
```

**Affichage : chiffre, référence, écart. Aucune action recommandée.**

Le message d'alerte suit strictement le vocabulaire de `01-conformite.md` :

> Apport total en zinc : 42,2 mg. La limite haute européenne est de 25 mg par jour, alimentation et compléments confondus.

Jamais : « réduis ton complément de zinc ».

---

## 5. Sources de données

| Source         | Usage                                                    | Accès                 |
| -------------- | -------------------------------------------------------- | --------------------- |
| OpenFoodFacts  | Produits emballés par code-barres, base européenne       | API ouverte, gratuite |
| CIQUAL (ANSES) | Aliments bruts, composition détaillée en micronutriments | Téléchargement libre  |
| NUBEL          | Table belge, produits locaux                             | Selon licence         |
| EFSA DRV       | Références et limites hautes                             | Publication libre     |

**Ne pas noter, ne pas classer, ne pas juger les produits.** Yuka a été condamnée trois fois en première instance avant de gagner en appel — trois ans de procédure. Ce produit affiche des compositions et compare des totaux à des références. Le jugement porte sur l'apport de l'utilisateur, jamais sur le produit d'un industriel.

Saisie manuelle indispensable en repli : la couverture des produits belges dans OpenFoodFacts est incomplète.

**Licences : lire `17-donnees-sources.md` avant toute intégration.** Open Food Facts est sous ODbL, avec attribution et partage à l'identique. La règle qui en découle est structurante : **ne jamais fusionner les sources dans une table unifiée**, sous peine de créer une base dérivée soumise au share-alike.

---

## 6. Saisie par photo

### Aliments — la photo ouvre un dialogue, elle ne mesure pas

L'estimation de portion à partir d'une image reste très imprécise. Le flux correct :

1. Le modèle **identifie** les aliments présents — fiable
2. Il **demande les quantités** à l'utilisateur — « je vois du poulet, du riz et des brocolis ; tu peux me donner les quantités approximatives ? »
3. L'utilisateur répond en langage naturel
4. Le système propose des lignes pré-remplies, **que l'utilisateur valide**

L'erreur principale est ainsi éliminée, et la saisie reste plus rapide qu'une recherche manuelle. Ne jamais présenter une estimation visuelle comme une mesure.

Le code-barres reste le chemin par défaut : plus rapide et plus fiable.

### Compléments — le meilleur usage de la vision

Photo de l'étiquette, extraction des doses par nutriment, stockage dans `supplements.per_unit`.

**La photo n'est pas conservée** — D46. Elle traverse le modèle et disparaît ; seul le JSON extrait entre en base, après validation de schéma, contrôle de plausibilité contre les limites hautes de `nutrient_refs`, et confirmation par l'utilisateur (D47). La traçabilité ne repose plus sur l'image mais sur l'acte de validation : l'utilisateur a vu la dose et l'a confirmée.

**Les métadonnées EXIF sont retirées côté navigateur, avant l'envoi au modèle.** C'est désormais le seul moment où ce nettoyage peut avoir lieu — il n'y a plus d'étape ultérieure pour rattraper un oubli, et une photo de repas porte les coordonnées GPS du domicile.

C'est cette brique qui alimente la règle centrale du produit, et c'est là que personne ne fait mieux.

### Œufs et calibres

Le calibre compte : M ≈ 53-63 g, L ≈ 63-73 g, soit environ 1,5 g de protéines d'écart par œuf. Le catalogue distingue les calibres.

**Le mode bio ne modifie pas les macronutriments** — protéines et lipides sont pratiquement identiques. Ne pas créer de logique bio/non-bio sur la composition : c'est de la complexité pour zéro précision.
