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

| Macronutriment | Fourchette                                                                                                                                                | Défaut              |
| -------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------- |
| Protéines      | 1,6 – 2,2 g/kg _(Morton 2018, **méta-analyse** — point d'inflexion 1,62 ; 2,2 = sommet de l'IC 95 %, pas un plafond physiologique et pas un référentiel)_ | 1,8 g/kg            |
| Lipides        | 0,8 – 1,2 g/kg _(**aucune source à ce jour** — à établir ou à réexprimer en % d'énergie, § 7)_                                                            | 1,0 g/kg            |
| Glucides       | réparti sous contrainte, **jamais le reste** — voir § 2 ter                                                                                               | calculé, ≥ 40 % AET |
| Fibres         | 25 – 35 g — **cible de planification, aucune limite haute n'existe**                                                                                      | 30 g                |
| Eau            | boissons : 1,4 – 1,6 L (femme), 1,75 – 2,0 L (homme), + estimation d'effort plafonnée — voir § 2 bis                                                      | calculé             |

**Deux niveaux de source, et ils ne se contredisent pas.** Le socle réglementaire européen — **ANSES 2016** — dit quelle répartition convient à une population ; la littérature d'entraînement — **Morton 2018**, et l'ISSN pour la position sur les protéines à l'exercice — dit combien de protéines construisent du muscle. Ce sont deux questions distinctes. Le socle gouverne la **contrainte**, la littérature gouverne la **demande**, et la contrainte peut réduire la demande — jamais l'inverse.

> **La position de l'ISSN sur les protéines et l'exercice n'a PAS été vérifiée à la source** au 25/08/2026 : le budget de recherche de la session était épuisé. Elle est nommée comme source à ajouter, et **aucune valeur ne lui est attribuée** tant qu'elle n'a pas été lue.

Chaque objectif s'affiche avec sa source :

> **Protéines — 131 g/jour**
> Valeur par défaut : 1,8 g/kg pour 73 kg
> Fourchette de référence : 117 – 160 g _(1,6 – 2,2 g/kg, recommandations en musculation)_
> `[modifier]`

**Ne jamais pré-remplir un objectif de perte de poids.** Le défaut est la maintenance. Si l'utilisateur veut un déficit ou un surplus, il le choisit dans une fourchette encadrée : −20 % à +15 % de la maintenance, jamais au-delà.

---

## 2 bis. Hydratation

### Cible

**La cible porte sur les boissons, et elle le dit.**

| Public       | Cible de boissons  | D'où elle vient                                    |
| ------------ | ------------------ | -------------------------------------------------- |
| Femme adulte | **1,4 – 1,6 L/j**  | AI EFSA d'eau totale (2,0 L), converti en boissons |
| Homme adulte | **1,75 – 2,0 L/j** | AI EFSA d'eau totale (2,5 L), converti en boissons |

L'EFSA fixe un **apport adéquat d'eau totale** — boissons **et** eau des aliments — de 2,0 L/j pour la femme et 2,5 L/j pour l'homme (2010, climat tempéré, activité modérée, NAP = 1,6). L'ANSES reprend ces valeurs telles quelles : « Cet apport satisfaisant concerne toutes les sources d'eau, c'est-à-dire l'eau de boisson, l'eau présente dans les autres boissons et l'eau contenue dans les aliments. »

Ce produit compte les **boissons** et ne compte pas l'eau des aliments. La conversion s'appuie sur l'ESPEN, qui écrit que « drinks or beverages account for **70 to 80 %** of fluid consumed » et retient 80 % pour poser un plancher de 1,6 L (femme) et 2,0 L (homme) — recommandation R61, grade B, consensus fort à 96 %. **La fourchette 70-80 % est reportée ici plutôt que refermée sur son sommet.** La borne basse de chaque cible est la lecture prudente, la borne haute est le plancher ESPEN.

**Trois réserves qui accompagnent ce chiffre et doivent rester attachées à lui :**

- R61 dit « should be **offered** at least » : c'est un minimum de **mise à disposition**, adressé à des soignants en gériatrie pour prévenir la sous-hydratation. Sa transposition à un adulte sportif est une décision, pas une évidence.
- L'ESPEN cite lui-même la dispersion des recommandations nationales de boissons : **1,0 à 2,2 L/j chez la femme, 1,0 à 3,0 L/j chez l'homme**. Il n'existe pas de valeur unique défendable.
- Le sexe est **la seule variable** que l'EFSA et l'ANSES retiennent chez l'adulte.

**Le coefficient par kilogramme est retiré.** `35 ml × poids` n'est pas un objectif de boissons : c'est une valeur d'**eau totale**. La seule table qui publie ce coefficient est la table D-A-CH, et l'intitulé de sa colonne le dit mot pour mot — « Wasserzufuhr durch **Getränke und feste Nahrung**, ml/kg u. Tag ». Boissons **et** aliments solides. L'appliquer à un compteur qui exclut l'eau des aliments compte deux fois les ≈ 875 ml/j que les aliments apportent (bilan hydrique D-A-CH de l'adulte : boissons 1 440 ml, aliments 875 ml, eau d'oxydation 335 ml, total 2 650 ml). **Le coefficient D-A-CH est de surcroît dérivé de la dépense énergétique — la table précise « für einen Energieumsatz von 11,1 MJ (2650 kcal) » — et non de la masse corporelle.** Aucun référentiel européen ne publie de ml/kg de boissons pour l'adulte.

Si une modulation individuelle est un jour souhaitée, la seule variable adossée à une source est l'**énergie**, pas le poids : le bilan D-A-CH donne 1 440 ml de boissons pour 2 650 kcal, soit **≈ 0,54 ml de boissons par kcal** (dérivation arithmétique explicite, pas une valeur publiée). À ne jamais confondre avec la règle « 1 ml/kcal », qui porte sur l'eau totale disponible, eau d'oxydation comprise.

### Majoration d'effort

```
séance          : + ~500 ml par heure, ESTIMATION, plafonnée
chaleur, cardio : ne se cumulent pas mécaniquement avec la précédente
```

La sudation va de **0,3 à 2,4 L/h** selon l'intensité, la durée, l'acclimatation et l'environnement (position AND / Dietitians of Canada / ACSM, 2016 — _prise de position expirée le 31/12/2019, non réémise ; à corroborer_). Pour une séance de musculation en salle, bas de fourchette, +500 ml/h est un ordre de grandeur plausible — **présenté comme une estimation, plafonné, et jamais empilé.**

Le consensus international sur l'hyponatrémie d'effort est explicite : « Because fluid losses through sweat and urine are highly dynamic and variable across individuals […], **recommending fixed ranges of fluid intake is not appropriate**. The most individualized hydration strategy before, during, and immediately following exercise is **to drink fluids when thirsty**. » (Hew-Butler et al., 2017, reprenant la 3ᵉ conférence de consensus, Carlsbad 2015.)

**La consigne de référence affichée est donc : boire à la soif.** Si une mesure est souhaitée, c'est la variation de masse corporelle avant/après séance, pas un forfait.

Ce n'est pas une précaution rédactionnelle. Le même consensus nomme les « **inappropriate hydration recommendations** » parmi les causes de l'apport excessif, et désigne comme les plus exposés le sportif **récréatif** et la **femme** — soit exactement le public de cette application. Une cible gonflée n'est pas une prudence du côté de la sécurité : elle est du côté du risque.

### Affichage

Même composant que le reste du produit : la règle graduée. Valeur, cible, position. Aucune injonction à boire.

**Le périmètre s'affiche avec le chiffre**, faute de quoi un utilisateur qui a lu « 2,5 litres par jour » ailleurs se croira en déficit permanent :

> Boissons : 1 250 ml sur une cible de 1 400 à 1 600 ml.
> L'eau contenue dans les aliments apporte en plus environ 0,8 litre par jour, non comptée ici.

### Il n'existe aucune limite haute pour l'eau

**Le champ `ul` de l'eau reste `NULL`.** Les deux référentiels le disent explicitement :

> « **No maximum daily amount of water that can be tolerated by a population group can be defined**, without taking into account individual and environmental factors. » — EFSA
> « Because healthy individuals have considerable ability to excrete excess water and thereby maintain water balance, **a Tolerable Upper Intake Level (UL) was not set for water.** » — IOM, 2005

Le garde-fou existe, mais **c'est un débit, pas un total journalier** : le rein excrète au maximum **0,7 à 1,0 L/heure**, chiffre identique chez l'EFSA et l'IOM. Le produit retient **0,7 L/h**, borne basse de l'intervalle, et l'énonce comme un débit :

> Vous avez enregistré 2 200 ml sur les deux dernières heures. Le rein élimine au plus 0,7 à 1 litre par heure, et ce plafond baisse pendant l'effort.

**Ce plafond ne s'applique pas tel quel pendant une séance.** La sécrétion non osmotique d'AVP à l'effort bloque l'élimination de l'eau libre, et l'hyponatrémie d'effort — sodium sanguin sous 135 mmol/L, symptômes surtout sous 130 — survient chez des sportifs qui n'ont jamais approché ce débit. Afficher « sous 1 L/h tout va bien » serait faux dans le contexte même où l'application ajoute ses majorations.

**Le titre « Une limite haute existe et doit être respectée » disparaît.** Il contredisait le § 3 du présent document, qui interdit d'inventer un seuil haut là où l'autorité n'en publie pas.

### Ce qui reste non compté, et qu'il faut dire

Le 95ᵉ percentile d'apport observé par l'EFSA — 3,1 L (femmes) et 4,0 L (hommes) d'eau **totale** — est une **dispersion d'apports observés**, jamais une cible ni un plafond autorisé. Il ne s'affiche pas.

Et l'écran d'hydratation devient une source de magnésium au sens du § 3 bis : l'eau est nommée dans le périmètre de la limite haute européenne, et une eau minérale du haut de la fourchette (jusqu'à 101 mg/L, données EFSA/SCF) apporte ≈ 150 mg de magnésium dissociable en 1,5 L, soit 60 % de la limite, sans aucun complément. **Aujourd'hui les contenants ne portent aucun profil minéral : l'eau est donc hors du calcul du magnésium ajouté, et le compteur le sous-estime d'autant.** C'est écrit ici plutôt que supposé en silence. Rattacher un profil minéral aux contenants est une décision ouverte.

### Ligne correspondante du tableau du § 2

| Macronutriment | Fourchette                                                                            | Défaut  |
| -------------- | ------------------------------------------------------------------------------------- | ------- |
| Eau            | boissons : 1,4 – 1,6 L (femme), 1,75 – 2,0 L (homme), + estimation d'effort plafonnée | calculé |

---

---

## 2 ter. La répartition des macronutriments — aucun poste n'est le reste

### Le défaut que cette règle ferme

Les glucides sont aujourd'hui « le reste des calories ». Rejoué avec les valeurs par défaut du document, ce calcul produit :

| Profil                                                               | Protéines       | Lipides         | Glucides            |
| -------------------------------------------------------------------- | --------------- | --------------- | ------------------- |
| Femme 73 kg, 168 cm, 35 a, FA 1,35, maintenance _(l'exemple du § 2)_ | 131 g = 27,0 E% | 73 g = 33,7 E%  | 192 g = **39,3 E%** |
| Femme 100 kg, 165 cm, 40 a, FA 1,25, −20 %                           | 180 g = 43,1 E% | 100 g = 53,9 E% | 12,6 g = **3,0 E%** |
| Femme 100 kg, 160 cm, 50 a, FA 1,25, −20 %                           | 180 g           | 100 g           | **−7,8 g**          |
| Femme 100 kg, 155 cm, 55 a, FA 1,25, −20 %                           | 180 g           | 100 g           | **−21,8 g**         |

Trois constats, dans cet ordre d'importance.

**Le nombre négatif est le symptôme, pas la maladie.** Quand le reste atteint zéro, les lipides sont déjà à 53,9 E% — l'EFSA plafonne son intervalle de référence à 35 E%, l'ANSES à 40 E%. **Rien, dans la méthode par soustraction, ne contraint les protéines et les lipides en part d'énergie.** Corriger la négativité sans corriger cela masquerait l'alarme.

**Le code ne produit pas un négatif : il lève une exception.** `back/Palier.Domain/Objectifs/CibleMacronutriments.cs` fait `if (calorieRestante < 0m) throw new ArgumentOutOfRangeException(...)`. Une utilisatrice de 100 kg, sédentaire, en déficit de 20 % fait planter le calcul. Et juste au-dessus du seuil, à 12,6 g de glucides, la fonction rend tranquillement une cible absurde **sans rien signaler**.

**Le domaine touché est borné, et il se dit avec un critère, pas avec une impression.** Le reste sort de l'intervalle dès que l'apport est inférieur à `(4·P + 9·L)·masse / 0,55`, soit **1 767 kcal à 60 kg, 2 150 kcal à 73 kg, 2 945 kcal à 100 kg** avec les défauts du document. Les profils touchés sont ceux à faible ratio kcal/kg : masse élevée, petite taille, âge élevé, sédentarité, déficit.

### Ce que disent les référentiels sur la forme de l'expression

**Nulle part les glucides ne sont un résidu — et cette méthode a déjà été abandonnée par une autorité.** L'EFSA écrit : « As energy balance is the ultimate goal, dietary reference values for carbohydrate intake **cannot be made without considering other energy delivering macronutrients** and will be given as percentage of total energy intake (E%). » Et l'ANSES, décrivant ses propres références antérieures : « Dans les recommandations précédentes (Afssa, 2001), la contribution des glucides à l'AET a été définie **pour compléter les apports énergétiques au-delà des apports en lipides et protéines**. » Elle les a remplacées en 2016 par un intervalle dérivé de la littérature. **La méthode par soustraction n'est pas inédite : elle est supersédée.**

| Grandeur                           | EFSA                                                   | ANSES                                              | IOM (américain)   |
| ---------------------------------- | ------------------------------------------------------ | -------------------------------------------------- | ----------------- |
| Glucides — intervalle de référence | 45 – 60 E% (2010)                                      | 40 – 55 E% (2016)                                  | 45 – 65 E% (AMDR) |
| Lipides — intervalle de référence  | 20 – 35 E% (2010)                                      | **35 – 40 E%** (2016)                              | 20 – 35 E% (AMDR) |
| Protéines — intervalle en E%       | **aucun**                                              | 10 – 20 E% (12 % si NAP < 1,5, F > 50 a, H > 60 a) | 10 – 35 E% (AMDR) |
| Protéines — ancrage en g/kg        | PRI 0,83 ; AR 0,66                                     | —                                                  | RDA 0,8           |
| Protéines — énoncé de sécurité     | pas d'UL ; « twice the PRI » (1,66 g/kg) considéré sûr | —                                                  | —                 |

**EFSA et ANSES se contredisent frontalement sur les lipides** : 20-35 E% et 35-40 E% ne se recoupent qu'au point unique de 35 E%. Le choix du référentiel gouvernant est structurant, et il n'est pas tranché ici.

Deux phrases à ne pas élider, parce qu'elles changent la conclusion opérationnelle :

> « Total fat intakes **> 35 E% may be compatible with both good health and normal body weight** depending on dietary patterns and **the level of physical activity**. » — EFSA, lipides
> « In Europe, adult protein intakes at the upper end (90-97,5ᵉ percentile) of the intake distributions have been reported to be **between 17 and 27 E%**. » — EFSA, protéines

L'EFSA assouplit elle-même sa borne haute lipidique **en fonction du niveau d'activité physique**, c'est-à-dire pour exactement le public de ce produit. Et l'exemple publié du § 2 — 73 kg à 1,8 g/kg, soit 27,0 E% de protéines — se situe à la borne haute de ce que l'EFSA rapporte comme consommation européenne observée, non au-delà.

### La règle de conception

**Principe : aucun macronutriment n'est le résidu. L'énergie se répartit entre trois postes sous contrainte explicite, et l'impossibilité devient un état nommé et affichable — jamais une exception, jamais un nombre absurde.**

Notation : `E` apport cible en kcal, `m` masse en kg, `P` et `L` les demandes de l'utilisateur en g/kg.

**Étape 0 — l'énergie d'abord, et son propre repère.** Calculer le TDEE et l'apport cible. Si la masse maigre est connue, évaluer la disponibilité énergétique `EA = (apport − coût de l'exercice) / masse maigre` et **l'afficher comme un continuum**, dans le vocabulaire du § 4 et de `01-conformite.md` : constat, référence, écart, aucune action recommandée. Si la masse maigre est inconnue, l'EA n'est pas calculable et le produit le dit plutôt que de la deviner.

**Étape 1 — tout convertir en part d'énergie avant de répartir.** Les demandes en g/kg deviennent des parts : `p = 4·P·m / E`, `l = 9·L·m / E`. Elles sont des **demandes**, pas des acquis.

**Étape 2 — test de faisabilité, posé comme une borne sur l'énergie.** Avec `c_ref` la borne basse glucidique du référentiel retenu, le budget non glucidique vaut `B = (1 − c_ref)·E`. La demande est honorable si :

```
4·P·m + 9·L·m ≤ B     ⟺     E ≥ (4·P·m + 9·L·m) / (1 − c_ref)
```

**Étape 3 — si elle ne l'est pas, les lipides cèdent en premier, les protéines ensuite.** L'ordre n'est pas arbitraire : sous restriction énergétique, la littérature sportive indique de **préserver voire d'augmenter** les protéines pour protéger la masse maigre. Et l'EFSA assouplit elle-même sa borne haute lipidique selon l'activité physique — l'ordre de cession se justifie par la protection de la masse maigre, non par une prétendue dureté de la borne lipidique.

```
lipides_g  ← max(plancher_lipides,  (B − 4·P·m) / 9)
protéines_g ← max(plancher_protéines, (B − 9·lipides_g) / 4)   si nécessaire
glucides_g  = (E − 4·protéines_g − 9·lipides_g) / 4
```

**Étape 4 — l'état est affiché, jamais silencieux.** Quand la contrainte mord, l'application **dit que la demande en g/kg n'a pas été honorée et pourquoi**. Rendre silencieusement d'autres chiffres que ceux demandés serait la défaillance silencieuse déguisée en correction.

**Étape 5 — le seul refus possible est arithmétique.** Si même aux planchers `4·plancher_protéines + 9·plancher_lipides > B`, aucune répartition n'existe à cet apport. Le produit **nomme l'état** — « à cet apport énergétique, aucune répartition tenable » — renvoie vers l'énergie et non vers les macros, et n'émet aucun nombre. **Ce n'est pas un refus de sécurité : c'est une impossibilité de calcul, et le libellé doit le dire ainsi.**

**La non-négativité devient un théorème.** Comme `p + l ≤ 1 − c_ref` par construction, `c ≥ c_ref > 0`. Deux interdits qui en découlent :

- **jamais `Math.Max(0, reste)`** — cela masquerait la brèche et livrerait un plan dont les macros ne somment plus aux calories ;
- **jamais de `throw`** sur un profil d'utilisateur banal. L'invariant `4·prot + 9·lip + 4·gluc = E` (à l'arrondi près) est testé, il n'est pas espéré.

### Ce que je n'ai PAS posé comme seuil, et pourquoi

C'est la partie la plus importante de cette section.

**Aucun plancher glucidique de sécurité.** L'EFSA a explicitement échoué à en fixer un : « data are not sufficient to define a Lower Threshold of Intake (LTI) for carbohydrates » et « there is an insufficient scientific basis for setting a Tolerable Upper Intake Level (UL) for total carbohydrates. The Panel therefore comes to the conclusion that **only a Reference Intake range can be given** ». `c_ref` est donc une **cible de planification**, jamais un seuil. En sortir produit un **état affiché** — « hors de l'intervalle de référence » — et **ne fait pas refuser un plan**. Refuser de servir est un acte de niveau sécurité : l'adosser à une valeur dont la source dit qu'elle n'en est pas une serait exactement l'inversion de grandeur que ce document combat.

**Pas de 130 g/j.** C'est la RDA **américaine** (IOM, 2005). L'EFSA cite le chiffre puis le disqualifie dans la phrase suivante : « An intake of 130 g per day […] has been estimated to be sufficient to cover the needs of glucose for the brain. **However, these levels of intake are not sufficient to meet energy needs** in the context of acceptable intake levels of fat and protein. » En faire un plancher européen cumulerait deux fautes.

**Pas de 50-100 g/j.** « Generally, an intake of 50 to 100 g per day will prevent ketosis » est un constat physiologique, sans aucun statut de référence nutritionnelle.

**Pas de seuil binaire à 30 kcal/kg de masse maigre.** La disponibilité énergétique est le seul repère de santé sourcé de ce dossier, mais la littérature courante lui a retiré son statut de seuil : la valeur « should not be interpreted as a rigid diagnostic criterion », elle « may not fully capture the complexity and variability of responses observed in free-living athletic populations », et il faut « interpret EA on a spectrum rather than applying fixed cut-offs ». Elle s'affiche donc en continuum, avec un **repère de vigilance** et non une limite — et la source d'origine précise que le concept « was first studied in females », ce que le produit doit dire.

**Pas de limite haute protéique.** « The available data are not sufficient to establish a Tolerable Upper Intake Level (UL) for protein. In adults an intake of twice the PRI is considered safe. » 1,66 g/kg est un **niveau sûr**, pas une UL : dépasser ce chiffre n'est pas dépasser une limite, et le traiter comme telle déclencherait une alerte que la science ne soutient pas.

**Pas de plafond lipidique dur à 35 E%.** L'EFSA l'assouplit elle-même selon le niveau d'activité physique. La borne sert à ordonner la cession, pas à alerter.

**Pas de limite haute pour l'eau, ni pour les fibres.** Aucune n'existe.

### Ligne correspondante du tableau du § 2

| Macronutriment | Fourchette                                                                                                                                                    | Défaut             |
| -------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------ |
| Protéines      | 1,6 – 2,2 g/kg _(Morton 2018, **méta-analyse**, point d'inflexion 1,62 g/kg ; 2,2 = sommet de l'IC 95 %, non un plafond physiologique et non un référentiel)_ | 1,8 g/kg           |
| Lipides        | 0,8 – 1,2 g/kg _(aucune source citée à ce jour — à établir ou à réexprimer en E%)_                                                                            | 1,0 g/kg           |
| Glucides       | réparti sous contrainte, **jamais le reste** — voir § 2 ter                                                                                                   | calculé, ≥ `c_ref` |
| Fibres         | 25 – 35 g — **cible de planification, aucune limite haute n'existe**                                                                                          | 30 g               |

**Sur les fibres, la ligne doit être honnête** : 25 g/j est l'**AI de l'EFSA**, fondé sur le seul critère du transit intestinal normal. L'EFSA reconnaît par ailleurs un bénéfice au-delà — « evidence of benefit to health […] at dietary fibre intakes greater than 25 g per day » — et l'IOM pose 38 g/j pour l'homme et 25 g/j pour la femme. **25-35 g est donc une cible de planification adossée à ces trois éléments, et non un intervalle EFSA.**

**Sur les lipides exprimés en g/kg** : cette forme fait dériver silencieusement la part d'énergie. 1,0 g/kg vaut 33,7 E% à 1 949 kcal et 24 E% à 3 000 kcal **pour la même personne**. Un défaut fixé en g/kg sort donc de tout intervalle de référence dès que l'énergie bouge — c'est le mécanisme même du défaut corrigé ici.

---

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

## 3 bis. Le magnésium — pourquoi 250 mg et 400 mg sont vrais tous les deux

### La question posée, et la réponse

**« La limite est aux alentours de 400 mg » : le nombre est réel, la grandeur est fausse.**

400 mg/j existe bel et bien. C'est l'**apport nutritionnel recommandé** (RDA) du Food and Nutrition Board de l'Institute of Medicine, pour l'homme de 19 à 30 ans, sur l'**apport total toutes sources**. C'est une quantité **à atteindre par l'alimentation** — l'inverse exact d'un plafond. L'intuition est renforcée par le voisinage : toutes les références d'apport de l'homme adulte se serrent autour de ce nombre.

**« La limite est de 250 mg » est vrai aussi, et ne contredit pas le premier.** 250 mg/j est la limite haute tolérable européenne, mais elle ne porte **pas** sur l'apport total : elle ne couvre que le magnésium **ajouté**. L'avis qui l'établit l'écrit lui-même :

> « The amounts in food and beverages were not measured or taken into account in the calculation of the NOAEL and therefore **the UL for Mg cannot be derived for the intake from all sources**. Based on a NOAEL of 250 mg Mg per day and an uncertainty factor of 1.0 an UL of 250 mg Mg per day can be established for readily dissociable magnesium salts (e.g., chloride, sulphate, aspartate, lactate) and compounds like MgO in nutritional supplements, water, or added to food and beverages. **This UL does not include Mg normally present in foods and beverages.** […] This UL holds for adults, including pregnant and lactating women, and children from 4 years on. »
> — SCF, _Opinion on the Tolerable Upper Intake Level of Magnesium_, SCF/CS/NUT/UPPLEV/54 Final, 2001, § 6

Les deux nombres ne mesurent pas le même objet. Un homme peut manger 420 mg de magnésium alimentaire tout en étant à 0 mg sur le compteur de la limite haute. **Un plafond inférieur à un apport recommandé n'est une absurdité que si l'on croit qu'ils portent sur la même chose.**

### Toutes les valeurs en circulation

| Valeur                     | Grandeur                                  | Périmètre — sur quoi elle porte                                                                                                                                                                                                          | Émetteur, année, population                                                                                         |
| -------------------------- | ----------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------- |
| **250 mg/j**               | **UL** — limite haute tolérable           | **Magnésium ajouté seulement** : sels facilement dissociables (chlorure, sulfate, aspartate, lactate) et MgO, dans les compléments, **l'eau**, ou ajoutés aux aliments et boissons. **Exclut** le magnésium naturellement présent        | SCF, 2001. Adultes, grossesse et allaitement compris, enfants **à partir de 4 ans**. Aucune valeur pour les 1-3 ans |
| **250 mg/j**               | UL, confirmation de fraîcheur             | Idem — la note (g) du tableau EFSA maintient la restriction                                                                                                                                                                              | EFSA, _Overview on Tolerable Upper Intake Levels_, **version 11, août 2025**, référence (SCF, 2001b)                |
| **250 mg/j** (« LSS »)     | UL, transposition française               | « La LSS s'applique au magnésium dissociable et à l'oxyde de magnésium consommé sous forme de compléments alimentaires ou ajouté aux boissons et aliments. **Elle n'inclut pas le magnésium naturellement présent dans les aliments.** » | ANSES, références actualisées au 21/02/2025, reprenant le SCF. Adultes                                              |
| **350 mg/j**               | **AI** — apport adéquat                   | **Apport total, toutes sources**                                                                                                                                                                                                         | EFSA NDA, 2015. **Hommes** adultes > 18 ans                                                                         |
| **300 mg/j**               | **AI**                                    | Apport total. S'applique aussi aux femmes enceintes et allaitantes                                                                                                                                                                       | EFSA NDA, 2015. **Femmes** adultes > 18 ans                                                                         |
| **380 mg/j** / 300 mg/j    | **AS** — apport satisfaisant              | Apport total                                                                                                                                                                                                                             | ANSES, page de références actualisée au **21/02/2025**. Hommes / femmes de 18 ans et plus                           |
| **420 mg/j** / 360 mg/j    | **AS**                                    | Apport total                                                                                                                                                                                                                             | ANSES, avis saisine 2012-SA-0103, **2016**, § 3.6.16 (reprend Afssa 2001). Hommes / femmes adultes                  |
| **350 mg/j** / 300 mg/j    | **AI** (« Schätzwert »)                   | Apport total                                                                                                                                                                                                                             | D-A-CH (DGE / ÖGE / SGE), révision **2021**. Adultes ≥ 19 ans                                                       |
| **400 mg/j**               | **RDA** — apport recommandé               | Apport total. **Quantité à atteindre, pas un plafond**                                                                                                                                                                                   | FNB / IOM, **1997**. Hommes **19-30 ans**, référentiel américain                                                    |
| **420 mg/j**               | **RDA**                                   | Apport total                                                                                                                                                                                                                             | FNB / IOM, 1997. Hommes 31-70 ans, référentiel américain                                                            |
| **310** / **320 mg/j**     | **RDA**                                   | Apport total                                                                                                                                                                                                                             | FNB / IOM, 1997. Femmes 19-30 / 31-70 ans, référentiel américain                                                    |
| 330 / 350 / 255 / 265 mg/j | **EAR** — besoin moyen                    | Apport total. Couvre 50 % de la population. **Ne s'affiche jamais comme cible individuelle**                                                                                                                                             | FNB / IOM, 1997. H 19-30 / H 31-70 / F 19-30 / F 31-70                                                              |
| **+ 35 mg/j**              | **EAR**, incrément de grossesse           | Apport total. C'est un incrément d'**EAR**, pas de RDA : dans le tableau source, la case RDA de la grossesse est un tiret                                                                                                                | FNB / IOM, 1997. Grossesse                                                                                          |
| **350 mg/j**               | **UL** — limite haute **américaine**      | « The ULs for magnesium represent intake **from a pharmacological agent only** and do not include intake from food and water. » **N'inclut pas l'eau** — contrairement au périmètre européen                                             | FNB / IOM, 1997, tables DRI. **À partir de 9 ans**, adultes compris                                                 |
| 150 – 500 mg/j             | _Acceptable Range of Intake_ — historique | Apport total. Notion antérieure, remplacée par l'AI de l'EFSA en 2015. **Ne pas utiliser**                                                                                                                                               | SCF, 31ᵉ série, 1993. Adultes                                                                                       |
| ≈ 360 – 365 mg/j           | **LOAEL** — valeur de dossier             | Sels dissociables seulement. Seuil le plus bas où l'effet laxatif apparaît. **Ne s'affiche pas, ne se compare à rien**                                                                                                                   | SCF, 2001, § 5. Adultes                                                                                             |
| **250 mg/j**               | **NOAEL** — valeur de dossier             | Sels dissociables seulement. Base de dérivation de l'UL, facteur d'incertitude 1,0, donc **UL = NOAEL, au même nombre**                                                                                                                  | SCF, 2001, § 5. Adultes, grossesse et allaitement compris                                                           |

**Écart non résolu, à trancher par le diététicien.** L'ANSES publie deux apports satisfaisants différents pour le magnésium selon le document consulté : 380/300 mg sur sa page de références actualisée au 21/02/2025, et 420/360 mg dans son avis de 2016. Les deux ont été lus à la source. Rien ne permet ici de dire lequel fait autorité aujourd'hui. Aucun des deux n'entre en base tant que ce point n'est pas tranché.

### Les cinq pièges que ces valeurs tendent

1. **Le magnésium est une exception à la définition même de l'UL, et l'exception est en note de bas de page.** L'en-tête du tableau EFSA définit l'UL comme « the maximum level of total chronic daily intake of a nutrient (**from all dietary sources**) ». Lire cet en-tête, puis lire « Magnesium 250 » dans la table, et conclure « 250 mg toutes sources » est un raisonnement parfaitement logique — et faux. Tout tient dans l'appel de note (g).
2. **Le nombre 350 vaut deux choses opposées.** 350 mg/j est l'apport adéquat européen de l'homme (à atteindre, toutes sources) **et** la limite haute américaine sur le supplémenté (à ne pas dépasser). Même nombre, sens inverse, périmètre inverse. Une valeur de magnésium égale à 350 est ininterprétable tant qu'on ne sait pas qui parle.
3. **250 et 350 ne diffèrent pas seulement par le nombre.** L'UL européen inclut l'eau de boisson ; l'UL américain l'exclut. Le périmètre est une propriété du couple (nutriment, autorité), jamais du nutriment seul.
4. **UL = NOAEL au même nombre.** Le facteur d'incertitude vaut 1,0. Une ligne de référence à 250 mg ne dit pas d'elle-même laquelle des deux grandeurs elle porte : c'est la colonne de statut qui le dit, pas la valeur.
5. **L'UL ne s'échelonne ni avec l'âge ni avec le poids au-dessus de 4 ans.** 250 mg vaut identiquement pour un enfant de 4 ans, un homme de 100 kg et une femme enceinte. Le SCF a explicitement refusé l'extrapolation au poids corporel — c'est la raison pour laquelle aucune valeur n'existe pour les 1-3 ans. **Un code qui mettrait cette valeur à l'échelle inventerait un seuil.**

### Ce que le SCF pose en condition d'application, et que le produit ne peut pas mesurer

> « While the UL is expressed as a daily intake it should be noted that most of the studies used in its derivation involved daily intake obtained from two or more doses. **Therefore the UL should apply to daily intake of Mg consumed on two or more occasions.** This is of greater importance for the sulphate salt than for other readily dissociable salts of Mg, given the additional osmotic effect of sulphate ion. »
> — SCF, 2001, § 7

Le produit additionne une journée. Il ne collecte pas le fractionnement des prises, et il ne doit pas prétendre le faire. **Conséquence : le libellé d'alerte reste descriptif et ne promet jamais l'innocuité en dessous de 250 mg.** C'est une limite d'attribution, elle se consigne comme telle.

### La règle de calcul à implémenter

**1. La référence porte un périmètre, et ce périmètre décide de l'opérande.**

```
Perimetre ∈ { ApportTotal, ApportAjoute }

ApportTotal   → zinc, vitamine A préformée, vitamine D, sélénium, vitamine B6
ApportAjoute  → magnésium, folates (acide folique et formes de synthèse autorisées)
```

Le champ n'est pas documentaire : il choisit la somme comparée. **Le domaine refuse de comparer une référence de périmètre « ajouté » à un apport total.**

**2. La provenance se capture à la saisie, jamais après coup.** `ApportAgrege` sépare aujourd'hui `Alimentation` et `Complements` — deux composantes ne suffisent pas. Quatre provenances :

```
AlimentNaturel   — CIQUAL, OpenFoodFacts, composition
AlimentEnrichi   — nutriment ajouté, lu sur l'étiquette
Complement       — dose élémentaire déclarée
Eau              — robinet et minérale
```

Une fois les milligrammes additionnés, l'information de provenance est perdue pour toujours. Elle ne se reconstruit pas.

**3. La comparaison choisit sa somme.**

```
comparer(apport, reference) :
  si reference.Statut ≠ LimiteHauteEtablie
      → aucun dépassement annonçable (un niveau sûr d'apport situe, il n'alerte pas)
  si reference.Perimetre = ApportTotal
      → somme = AlimentNaturel + AlimentEnrichi + Complement + Eau
  si reference.Perimetre = ApportAjoute
      → somme = AlimentEnrichi + Complement + Eau
  si une provenance du périmètre est indéterminée
      → AucuneReference — silence, et invitation à préciser l'étiquette
  sinon
      → comparer somme à reference.Valeur
```

**4. Ce qu'on n'additionne jamais à la ligne de sécurité du magnésium :** le magnésium naturel des aliments. Les tables de composition donnent un magnésium **total** sans distinguer la forme chimique : elles alimentent la ligne « apport », jamais la ligne « sécurité ».

**5. Ce qu'on ne compare jamais :**

- l'apport total à 250 mg ;
- l'apport adéquat (350/300) et la limite haute (250) sur la même règle graduée. **Ce sont deux questions distinctes** — « est-ce que je mange assez ? » et « est-ce que j'en rajoute trop ? » — et donc deux composants distincts ;
- une somme dont le périmètre est indéterminé. Dans le doute, silence.

**6. Le libellé d'alerte nomme son périmètre.** Le gabarit du zinc n'est pas réutilisable : « alimentation et compléments confondus » est vrai pour le zinc et faux pour le magnésium.

> Magnésium apporté par vos compléments et l'eau : 300 mg. La limite haute européenne est de 250 mg par jour pour le magnésium ajouté — elle ne porte pas sur le magnésium des aliments.

Jamais « réduis ton complément ».

**7. Deux limites d'attribution, écrites plutôt que supposées.** CIQUAL et OpenFoodFacts donnent le magnésium **total** d'un aliment, sans distinguer le naturellement présent de l'ajouté dans un produit enrichi : le compteur **sous-estime** la fraction ajoutée sur les aliments enrichis. Et la comparaison à 250 mg suppose un apport réparti sur au moins deux prises, ce que le produit ne mesure pas.

### Pourquoi 250 mg reste le garde-fou dont ce produit a besoin

Le public de cette application est exactement celui qui prend du magnésium en complément — crampes, sommeil, récupération. Les dosages du commerce dépassent couramment 250 mg de magnésium élément dès la première gélule _(observation de marché, non sourcée : à étayer sur un relevé d'étiquettes daté ou sur les doses maximales autorisées en compléments alimentaires en Belgique avant d'en faire un argument produit)_. **250 mg n'est pas une valeur marginale : c'est la bonne valeur, branchée sur la bonne colonne.**

Et l'effet critique se dit sans dramatiser : diarrhée légère, « completely reversible within 1 to 2 days », adaptation intestinale en quelques jours. L'hypermagnésémie toxique n'apparaît qu'au-delà de 2 500 mg, « doses exceeding the UL by a factor of more than 10 ». Le vrai groupe à risque est l'insuffisant rénal — que l'application **ne peut ni détecter ni interroger** sans franchir la ligne informer / prescrire de `01-conformite.md`.

### Ce que le § 4 doit devenir

La règle centrale — « additionner alimentation et compléments par nutriment, puis comparer aux références » — **est juste pour le zinc et fausse pour le magnésium.** L'exemple travaillé du zinc reste valable : son UL de 25 mg dérive bien d'apports totaux contrôlés (« 30 mg/day from supplements on top of 10 mg calculated from dietary intake estimates »). Le § 4 gagne une colonne de périmètre, il ne perd pas son exemple.

Sans cette correction, un homme mangeant exactement les 380 mg que l'ANSES lui recommande serait signalé à **152 % d'une « limite haute de sécurité »**. Sur une application qui promet d'éviter les excès, c'est une alerte qui pousse au déficit — et une alerte qui se déclenche chez tout le monde tous les jours entraîne l'utilisateur à l'ignorer le jour où elle est vraie.

---

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

---

## 7. Ce que ce travail ne peut pas trancher

Les points qui suivent ne relèvent pas d'une lecture de sources. Ils demandent un arbitrage de diététicien — ou du porteur du projet au titre du § 6 de `CLAUDE.md` — et **aucun code ne s'écrit dessus avant une décision datée dans `docs/decisions.md`.**

**1. Le référentiel qui gouverne.** EFSA et ANSES divergent frontalement : lipides 20-35 E% contre 35-40 E% (recoupement au point unique de 35), glucides 45-60 E% contre 40-55 E%, et l'EFSA ne pose **aucun** intervalle protéique en E% là où l'ANSES plafonne à 20 E%. Les deux sont légitimes pour un public belge. Le référentiel retenu gouverne `c_ref`, le plancher lipidique et les libellés à l'écran. Il doit être **choisi, nommé à l'écran et daté — jamais moyenné**.

**2. Le plafond protéique en pourcentage et le plancher en g/kg sont inconciliables pour certains profils.** L'ANSES plafonne à 20 E% ; la littérature sportive demande 1,2 à 2,0 g/kg et de préserver les protéines en déficit. Pour une personne lourde à faible apport, les deux ne tiennent pas ensemble. La méthode du § 2 ter privilégie l'ancrage en g/kg — ce qui reste dans l'AMDR américain (10-35 E%) mais sort de l'intervalle ANSES. **À valider explicitement.**

**3. Les bornes protéiques du produit dépassent les sources européennes.** 1,8 g/kg par défaut et 2,2 g/kg au plafond excèdent le seul énoncé de sécurité de l'EFSA sur les protéines (1,66 g/kg, « twice the PRI »). L'EFSA n'a fixé **aucune UL** protéique, donc ce n'est pas un dépassement de limite — mais le document ne doit pas laisser croire qu'un référentiel européen endosse 2,2 g/kg. **Ces bornes viennent d'une méta-analyse, Morton 2018, pas d'un référentiel.** Cette distinction de grandeur doit apparaître à l'écran.

**4. Les lipides à 0,8 – 1,2 g/kg n'ont aucune source.** Il faut soit les sourcer, soit les réexprimer en pourcentage d'énergie comme le font tous les référentiels.

**5. Quelle borne de la fourchette d'hydratation afficher.** La conversion eau totale → boissons vaut 70 à 80 % selon la source, et la dispersion internationale des recommandations de boissons va de 1,0 à 3,0 L/j chez l'homme. Le produit affiche aujourd'hui une fourchette ; si une cible ponctuelle est requise par l'interface, **le choix de la borne est une décision de santé**, pas un arrondi. Et la transposition d'une recommandation gériatrique (ESPEN R61) à un adulte sportif doit être assumée par écrit.

**6. L'eau et le magnésium.** L'eau est nommée dans le périmètre de la limite haute européenne du magnésium. Soit on rattache un profil minéral à chaque contenant, soit on assume que l'eau reste hors du calcul — **et on l'écrit**, parce qu'un périmètre incomplet qu'on croit complet est exactement le défaut que le § 3 bis corrige.

**7. Le désaccord ANSES sur le magnésium.** 380/300 mg (page de références, 21/02/2025) ou 420/360 mg (avis 2016) : les deux ont été lues à la source, rien ne dit ici laquelle fait autorité. Ce point est **ouvert**.

**8. Le changement de schéma, et il porte trois colonnes, pas deux.** `nutrient_refs` vaut aujourd'hui `(nutrient, label_fr, label_en, unit, ai_male, ai_female, ul, per_kg, source, source_year)`. Il manque `perimetre` et `forme_chimique`, que D51 avait déjà ouverts — **et il manque `statut`**. Sans lui, toute ligne dont `ul` est non nul devient une limite haute établie : les niveaux sûrs du fer (40 mg) et du manganèse (8 mg) produiraient un vocabulaire de dépassement que l'EFSA interdit. Le domaine modélise quatre statuts que la base ne sait pas exprimer. Second point, structurel : `ai_male`, `ai_female` et `ul` cohabitent dans **la même ligne** — la ligne magnésium vaut donc déjà `(350, 300, 250)`, cible au-dessus du plafond. **Faut-il les séparer en deux lignes ?** Le § 3 bis demande qu'ils ne partagent jamais la même règle graduée.

**9. La forme chimique n'est pas décorative.** La niacine vaut 10 mg en acide nicotinique et 900 mg en nicotinamide — un facteur 90 sur la même ligne d'étiquette. Le magnésium alimentaire est chélaté et peu dissociable ; les sels de compléments sont osmotiquement actifs. **C'est la dissociabilité, pas l'élément, qui fait l'effet.** Décider ce que la saisie collecte, et ce qu'elle fait quand la forme est absente.

**10. La vitamine A a été réévaluée en 2024, et sa population a changé dans le sens qui alerte davantage.** L'UL de 3 000 µg ER/j est retenue, mais elle s'applique désormais aux hommes, aux femmes en âge de procréer, enceintes, allaitantes **et ménopausées** — là où le SCF de 2002 restreignait les ménopausées à 1 500 µg ER/j. Sa restriction porte sur la **forme** (rétinol préformé, caroténoïdes exclus) et **non sur la source** : elle couvre bien l'alimentation et les compléments, périmètre `ApportTotal`. **La traiter comme le magnésium produirait l'erreur inverse — une sous-alerte sur le seul nutriment tératogène du lot.** Cette ligne demande sa propre passe de vérification à la source avant d'entrer en base.

**11. Le β-carotène n'a plus d'UL dérivable depuis 2024**, et l'avis porte un avertissement ciblé sur les fumeurs. L'application **ne peut pas le reprendre tel quel** sans franchir la ligne informer / prescrire de `01-conformite.md`. Arbitrage requis, pas décision technique.

**12. Les autres limites hautes du § 3 demandent leur propre passe de fraîcheur.** L'EFSA a révisé depuis 2023 la vitamine D, le fer, les folates, le manganèse, la vitamine B6, le sélénium et le rétinol — et **la révision de la vitamine E est en cours** (« A review of the ULs for vitamin E is on-going », note (f) de l'_Overview_ version 11). Le § 3 nomme la vitamine D, le fer, la B6 et les folates parmi les priorités V1.

**13. Toute formulation destinée à l'utilisateur qui touche à la nutrition ou à la santé.** Les libellés proposés dans les blocs ci-dessus sont des gabarits techniques montrant le périmètre à nommer. Ils ne sont pas validés.

**14. Les populations que le produit ne peut ni détecter ni interroger.** L'insuffisance rénale est le seul groupe réellement exposé au magnésium ; la grossesse, l'allaitement et les mineurs ont leurs propres valeurs. Ce que le produit fait de ces cas — et ce qu'il ne demande pas — n'est pas tranché ici.

---

**Sources primaires citées dans ces blocs** — SCF/CS/NUT/UPPLEV/54 Final (2001) ; EFSA _Overview on Tolerable Upper Intake Levels_, version 11 (août 2025) ; EFSA NDA, DRV magnésium (2015), eau (2010), glucides et lipides (2010), protéines (2012), fibres (2010), folates (2023), manganèse (2023), fer (2024), vitamine A préformée et β-carotène (2024) ; EFSA _DRVs summary report_ (2017) ; ANSES, références nutritionnelles en vitamines et minéraux (21/02/2025) et avis saisines 2012-SA-0103 et 2012-SA-0186 (2016) ; D-A-CH / DGE, référentiels magnésium (2021) et eau (Stand Ableitung 2000) ; IOM/FNB, DRI magnésium (1997), eau et macronutriments (2005) ; ESPEN, _Clinical nutrition and hydration in geriatrics_ (2022), R61 ; Hew-Butler et al., _Exercise-Associated Hyponatremia: 2017 Update_ ; AND / Dietitians of Canada / ACSM, _Nutrition and Athletic Performance_ (2016, **expirée le 31/12/2019**) ; Morton et al. (2018, méta-analyse).
