Research complete (search budget was exhausted early; I worked through Europe PMC, EFSA and WHO direct fetches instead). Here is the report.

---

# Audit d'absence — `docs/04-nutrition.md`

**Axe : ce qui manque.** Le document est bien écrit et plusieurs de ses partis pris sont justes (marge d'erreur affichée, refus d'inventer un UL, photo qui ouvre un dialogue au lieu de mesurer, refus de noter les produits). Les manques ci-dessous ne sont pas des raffinements : **sept d'entre eux cassent la fonctionnalité que le document désigne lui-même comme la raison d'être du produit**, ou touchent une population que le brief nomme explicitement (reprise après blessure, grossesse, prothèse).

Classement par gravité. Chaque entrée : ce qui manque → pourquoi ça compte → ce que ça coûte.

---

## TIER 1 — Casse la fonctionnalité centrale, ou dangereux

### 1. La portée du UL : « apport total » vs « apport supplémentaire seul » — **DANGEREUX, et invalide la règle centrale**

Le § 4 pose la règle centrale : _« Additionner alimentation et compléments par nutriment, puis comparer aux références. »_ Appliquée telle quelle, elle est **scientifiquement fausse pour trois des nutriments que le § 3 liste en priorité**.

Le guidance EFSA 2024 sur l'établissement des UL est explicite. Le UL porte par défaut sur _« the maximum level of total chronic daily intake of a nutrient (from all dietary sources) »_, **mais** EFSA nomme les exceptions :

| Nutriment     | Portée réelle du UL                                                                                                                                                                                        | Conséquence sur la règle centrale                                                               |
| ------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------- |
| **Magnésium** | UL 250 mg/j — sels rapidement dissociables et oxyde **ajoutés aux aliments ou en compléments**. Le magnésium naturellement présent dans l'alimentation est **exclu**                                       | Additionner le Mg alimentaire (~300-400 mg/j courant) déclenche une alerte permanente et fausse |
| **Folates**   | UL 1000 µg/j — s'applique à _« the combined intake of folic acid, (6S)-5-methyltetrahydrofolic acid glucosamine and l-5-methyltetrahydrofolic acid calcium salts »_. **Pas** au folate alimentaire naturel | Idem : fausse alerte                                                                            |
| **Niacine**   | ULs **distincts** pour l'acide nicotinique et le nicotinamide, effets indésirables différents                                                                                                              | Un seul champ `ul` par nutriment ne peut pas représenter ça                                     |

Le document sent le problème sans le traiter — il écrit dans son tableau _« Magnésium — seuil de supplémentation distinct »_ — mais la règle centrale du § 4, elle, ne connaît qu'une addition et une comparaison.

**Ce qui manque dans le modèle de données :** un champ de portée par nutriment (`ul_scope`: `total` | `supplemental_only`) et un champ de forme chimique (`ul_form`). Sans eux, `nutrient_refs` ne peut pas porter la vérité.

**Coût :** la fonctionnalité présentée comme « ce que personne ne fait ailleurs » produit des faux positifs sur trois nutriments courants. Un utilisateur alerté à tort sur son magnésium arrête un complément qui ne posait aucun problème — c'est exactement le préjudice que la ligne informer/prescrire de `01-conformite.md` cherche à éviter, obtenu par un défaut de modélisation plutôt que par une phrase interdite.

> Sources : [EFSA, _Guidance for establishing and applying tolerable upper intake levels for vitamins and essential minerals_, 2024, PMC11538927](https://pmc.ncbi.nlm.nih.gov/articles/PMC11538927/) ; [EFSA, _Scientific opinion on the tolerable upper intake level for folate_, 2023, PMC10641704](https://pmc.ncbi.nlm.nih.gov/articles/PMC10641704/)

---

### 2. Aucune couche de conversion d'unités ni de formes chimiques — **DANGEREUX**

Le § 6 fait extraire par un modèle de vision les doses d'une étiquette de complément vers `supplements.per_unit`, puis compare aux limites hautes. **Le document ne dit pas un mot des unités.** Or les étiquettes réelles portent :

- Vitamine A en **UI** ou en **µg RE/RAE** — le UL est 3000 µg RE/j (EFSA 2024, valeur _retenue_, applicable y compris aux femmes en âge de procréer et enceintes). Une étiquette « 10 000 IU » vaut ~3000 µg RE : au UL exactement. Lue comme « 10 000 » contre un seuil de 3000, elle déclenche une alerte d'un facteur 3,3 dans le mauvais sens
- Vitamine D en **UI** ou **µg** (facteur 40)
- Folates en **µg** vs **µg DFE** (facteur 1,7 entre folate alimentaire et acide folique)
- Niacine en **mg** vs **mg NE**
- Vitamine E en **UI** vs **mg α-TE**, avec des facteurs différents selon la forme naturelle ou synthétique
- Magnésium : la dose élémentaire vs la masse du sel (l'oxyde titre ~60 % de Mg élémentaire, le citrate ~16 %)

Ajoutez les étiquettes américaines (%DV sur base US) que porte une part importante des compléments de musculation achetés en ligne.

**Coût :** c'est le chemin le plus court vers un chiffre faux **présenté avec autorité**, dans la seule brique où le produit prétend être meilleur que tout le monde. Combiné au point 1, la table `nutrient_refs` a besoin de trois colonnes qu'aucun document ne prévoit aujourd'hui : unité canonique, portée du UL, forme chimique.

---

### 3. Apport DÉCLARÉ vs apport MESURÉ — le biais est nommé nulle part, et il propage dans le calcul le plus important du produit — **DANGEREUX**

Le § 1 pose le TDEE adaptatif comme « le vrai différenciateur » :

```
TDEE réel ≈ apport moyen quotidien − (Δpoids × 7700 / jours)
```

`apport moyen quotidien` est un apport **déclaré**. La littérature est massive et ancienne :

- Livingstone et al., _BMJ_ 1990 : apports enregistrés 9,66 MJ/j contre 12,15 MJ/j mesurés à l'eau doublement marquée — **~20 % de sous-déclaration**
- Poslusna et al., _Br J Nutr_ 2009 (revue systématique, 493 citations) : ~30 % de sous-déclarants, sous-estimation moyenne ~15 %
- Capling et al., _Nutrients_ 2017 (méta-analyse, 11 études DLW) : _« mean EI was under-estimated by 19 % »_
- Heitmann & Lissner, _BMJ_ 1995 : _« Degree of obesity was positively associated with underreporting »_ — le biais **croît avec l'IMC**

Conséquence arithmétique directe : si l'apport est sous-déclaré de 20 %, le « TDEE réel » calculé est sous-estimé d'environ 20 %. Le produit propose ensuite un déficit de −20 % _sur ce chiffre déjà faussé_. **Les deux erreurs se composent** : l'utilisateur reçoit une cible qui peut représenter un déficit réel de 35-40 %, présentée comme « mesurée sur vos données » — donc plus crédible que l'estimation initiale, qui elle portait honnêtement sa fourchette.

Le document contient un seul garde-fou, et c'est un mot non défini : _« au moins 14 jours de saisie alimentaire jugée complète »_. **Jugée complète par qui, sur quel critère ?** C'est le mot porteur de toute la fonctionnalité et il est vide.

**Ce qui manque :** un test de plausibilité. Le standard de la discipline est le **cut-off de Goldberg** (rapport EI:BMR comparé au PAL attendu), utilisé dans 46 % des études qui traitent le sujet. Il fournit exactement le critère que « jugée complète » laisse en blanc, et il est calculable avec les données que le produit possède déjà.

**Ce qui manque aussi :** la règle de traitement des jours non saisis. Moyenner uniquement les jours saisis est le bug classique — on saisit moins les jours d'excès. Le document ne dit pas si un jour vide est ignoré, imputé, ou invalide la période.

**Coût :** le différenciateur revendiqué du produit est un amplificateur de restriction chez les utilisateurs qui sous-déclarent le plus — et la sous-déclaration corrèle avec l'IMC et avec les troubles du comportement alimentaire, c'est-à-dire précisément les deux populations que `01-conformite.md` § 5 s'engage à protéger.

> Sources : [Livingstone et al., BMJ 1990, PMC1662510](https://pmc.ncbi.nlm.nih.gov/articles/PMC1662510/) ; [Heitmann & Lissner, BMJ 1995, PMC2550989](https://pmc.ncbi.nlm.nih.gov/articles/PMC2550989/) ; [Capling et al., Nutrients 2017, PMC5748763](https://pmc.ncbi.nlm.nih.gov/articles/PMC5748763/) ; Poslusna et al., Br J Nutr 2009, PMID 19594967

---

### 4. Tout ce qu'un complément de musculation contient et qui n'est pas un nutriment — **DANGEREUX**

Le § 6 stocke « les doses par nutriment » et les contrôle « contre les limites hautes de `nutrient_refs` ». Or l'étiquette d'un pre-workout, d'un brûleur ou d'un multivitamines sportif porte majoritairement des substances **qui n'ont pas de ligne dans `nutrient_refs`** : caféine, créatine, bêta-alanine, taurine, extraits végétaux, ashwagandha, yohimbine, extrait de thé vert.

Le document ne dit pas ce qu'elles deviennent. Le comportement par défaut — les ignorer silencieusement — rend la promesse « on additionne tout » **fausse pour exactement les produits que l'utilisateur photographie**.

**Le cas le plus net est la caféine**, et il est chiffré. EFSA, opinion de mai 2015 :

- **200 mg en dose unique** (≈3 mg/kg pc) sans préoccupation chez l'adulte sain
- **400 mg/jour** réparti dans la journée, population adulte générale
- **200 mg/jour toutes sources confondues** chez la femme enceinte ou allaitante
- **3 mg/kg pc/jour** chez l'enfant et l'adolescent

Le produit suit déjà le café (il le compte à 100 % dans l'hydratation, § 2 bis) **et** les compléments. Il a donc les deux moitiés de l'addition et ne la fait pas. Le cumul pre-workout + café + boisson énergisante est le dépassement le plus banal de cette population. C'est un seuil chiffré, publié, applicable, qui tombe pile dans l'architecture existante — et il est absent.

À signaler aussi : EFSA a émis une opinion sur les catéchines du thé vert où l'EGCG en complément à forte dose est associé à un signal d'hépatotoxicité (à vérifier à la source avant implémentation — je n'ai pas pu confirmer la valeur exacte dans cette session). Et rien n'est dit du risque de contamination des compléments par des substances dopantes, sujet non trivial pour une application de musculation.

**Coût :** promesse centrale littéralement fausse sur la catégorie de produits la plus consommée par la cible, et absence du seul seuil de sécurité aigu réellement atteignable dans un usage normal.

> Source : [EFSA, avis sur la sécurité de la caféine, 2015](https://www.efsa.europa.eu/en/topics/topic/caffeine)

---

### 5. Aucun champ « médicaments » — **DANGEREUX**

Le document ne mentionne aucune saisie de traitement en cours. Le produit calcule pourtant des totaux de micronutriments et suit des compléments — c'est-à-dire qu'il observe **une moitié d'une interaction dont il ignore l'autre moitié**.

Interaction quantifiée, directement pertinente en Belgique où les AVK (acénocoumarol, warfarine) sont largement prescrits : Couris et al. (2006) mesurent qu'_« a weekly change of 714 µg dietary vitamin K significantly altered weekly INR by 1 unit »_. Un utilisateur sous AVK qui augmente ses légumes verts — comportement que l'application encourage structurellement — déplace son INR. L'application peut le voir venir et ne le voit pas.

D'autres familles à instruire (je n'ai pas pu les confirmer à la source dans cette session, à traiter comme piste à vérifier, pas comme acquis) : chélation de la lévothyroxine par le calcium et le fer, chélation des quinolones et cyclines par les cations divalents, potassium avec IEC/ARA-II/épargneurs potassiques, vitamine D avec thiazidiques.

**Ce qui manque n'est pas un moteur d'interactions** — ce serait hors périmètre, hors compétence, et probablement du dispositif médical. Ce qui manque est une **décision écrite** : soit on demande les traitements en cours et on définit ce qu'on en fait (au minimum : ne pas encourager, orienter), soit on ne les demande pas et on l'assume explicitement dans les CGU et à l'écran. Aujourd'hui le document ne choisit ni l'un ni l'autre, donc l'implémentation choisira toute seule.

**Coût :** un utilisateur sous traitement reçoit des comparaisons de micronutriments présentées comme neutres, dans un contexte où elles ne le sont pas.

> Source : Couris R et al., _Int J Vitam Nutr Res_ 2006, PMID 16941417

---

### 6. Le plancher calorique est absolu là où le risque est relatif — **DANGEREUX**

`01-conformite.md` § 5 pose 1200 kcal (femme) / 1500 kcal (homme), non contournable. C'est une bonne intention avec une mauvaise unité : **le seuil ne dépend ni de la corpulence ni du volume d'entraînement**.

Le concept clinique de référence est la **disponibilité énergétique** — apport moins dépense d'exercice, rapporté à la masse maigre. Le consensus CIO sur le RED-S (2023, version la plus récente) situe la faible disponibilité énergétique **sous 30 kcal/kg de masse maigre/jour**, contre ~45 kcal/kg MM/j en zone saine.

Conséquence concrète : un homme de 95 kg avec 75 kg de masse maigre qui s'entraîne cinq fois par semaine passe le plancher de 1500 kcal **haut la main** tout en étant très en dessous de 30 kcal/kg MM/j une fois l'exercice retranché. Le garde-fou est vert, la situation est un RED-S.

Le produit dispose déjà de tous les termes : masse maigre (§ 1, Katch-McArdle), coût de l'entraînement (§ 1, 5 kcal/min). **Il ne lui manque que la division.**

Manque associé, du même ordre : le § 5 de `01-conformite.md` liste des signaux de TCA mais **pas l'aménorrhée ni les troubles du cycle**, qui sont en pratique le marqueur clinique le plus utilisé de faible disponibilité énergétique.

**Coût :** le produit dont la promesse est « éviter les excès » ne détecte pas la forme de carence énergétique la plus spécifique à sa propre population.

> Source : Mountjoy M et al., _IOC consensus statement on REDs_, Br J Sports Med 2023

---

### 7. Grossesse et allaitement : absents, alors que le brief les nomme — **DANGEREUX**

Le public inclut explicitement « des gens qui reprennent après […] une grossesse ». Le document ne contient aucun chemin grossesse/post-partum/allaitement. Ce qui manque :

- **Mifflin-St Jeor n'est pas validée en grossesse.** EFSA (DRV énergie, 2013) traite l'énergie de la grossesse et de l'allaitement comme un **incrément séparé** — dépôt tissulaire, production lactée — dérivé de l'eau doublement marquée et d'estimations factorielles. Appliquer l'équation seule sous-estime par construction
- **Le TDEE adaptatif est inapplicable** : la prise de poids gestationnelle est physiologique, la formule `Δpoids × 7700` l'interprétera comme un surplus calorique et poussera vers un déficit
- **La vitamine A devient une contre-indication dure** et pas un simple UL : le UL de 3000 µg RE/j est maintenu (EFSA 2024) et s'applique nommément aux femmes enceintes, avec la tératogénicité comme effet critique — que le document mentionne d'ailleurs dans son tableau, sans en tirer de chemin produit
- **La caféine passe à 200 mg/j** (EFSA 2015), soit la moitié du seuil adulte
- Le plancher calorique de `01-conformite.md`, et toute proposition de déficit, doivent être **désactivés**, pas ajustés

**Ce qui manque au minimum :** une question de dépistage, et un basculement en mode suivi sans cible énergétique ni déficit, avec orientation. C'est peu coûteux à construire et ça ferme un risque majeur.

**Coût :** c'est le seul manque du document capable de nuire à un tiers qui n'est pas l'utilisateur.

> Sources : [EFSA, UL vitamine A préformée et β-carotène, 2024, PMC11154838](https://pmc.ncbi.nlm.nih.gov/articles/PMC11154838/) ; EFSA DRV énergie 2013, PMC13159830

---

## TIER 2 — Erreur systématique, non détectable par l'utilisateur

### 8. La constante 7700 kcal/kg n'est pas discutée, et elle est fausse pour la population du produit

`Δpoids × 7700` est la règle de Wishnofsky (3500 kcal/livre). Hall (2008) montre qu'elle _« approximately matches the predicted energy density of lost weight in obese subjects with an initial body fat above 30 kg but **overestimates** the cumulative energy deficit required per unit weight loss for people with lower initial body fat »_.

Autrement dit : **elle est calibrée pour l'obésité, et le produit cible des pratiquants de musculation** — population à faible masse grasse, donc celle où le biais est maximal. Le tissu maigre porte ~1100 kcal/kg contre ~7700 pour le tissu adipeux.

Deux problèmes s'ajoutent sur la fenêtre de 3 semaines choisie par le document :

- Le Δpoids à 3 semaines est **dominé par le glycogène et l'eau**, pas par la masse grasse — surtout chez quelqu'un qui vient de commencer ou de reprendre l'entraînement, ce qui est le cas d'usage central du produit
- Hall et al. (_Lancet_ 2011) : la réponse pondérale à un changement d'apport est lente, **demi-vie de l'ordre d'un an**, avec adaptation métabolique. Trois semaines ne sont pas un régime permanent

**Ce qui manque :** l'énoncé des hypothèses de la constante, et une fourchette d'incertitude sur le TDEE adaptatif.

Ce dernier point est une **incohérence interne du document**, et elle est frappante : le § 1 impose l'affichage d'une marge (±10-15 %) sur l'estimation prédictive, avec un excellent argument. Puis il présente le TDEE adaptatif comme « une mesure réelle », **sans aucune marge**, alors qu'il empile deux erreurs (apport déclaré, cf. point 3, et constante énergétique) dont la résultante est plausiblement _plus large_ que celle de la formule qu'il remplace. Le document affiche l'incertitude du chiffre le plus honnête et la cache sur le plus fragile.

> Sources : [Hall KD, Int J Obes 2008, PMC2376744](https://pmc.ncbi.nlm.nih.gov/articles/PMC2376744/) ; [Hall KD et al., Lancet 2011, PMC3880593](https://pmc.ncbi.nlm.nih.gov/articles/PMC3880593/)

### 9. « g/kg » — de quel poids ?

Le § 2 fixe protéines 1,6-2,2 g/kg et lipides 0,8-1,2 g/kg sans jamais dire **quel poids**. Poids total, masse maigre, ou poids ajusté ? Pour une personne à 120 kg avec 45 % de masse grasse, 2,2 g/kg de poids total = 264 g/j ; rapporté à la masse maigre, ~145 g/j. Le document trancherait aussi la question du dénominateur de l'hydratation (35 ml/kg) et du plancher du point 6.

**Coût :** l'écart entre les deux lectures atteint 100 g de protéines par jour chez le même utilisateur. C'est le genre d'ambiguïté qu'un développeur tranche silencieusement à l'implémentation, et que personne ne rattrape.

### 10. La provenance du % de masse grasse qui alimente Katch-McArdle

Le § 1 affirme que Katch-McArdle est « plus précis » si la masse grasse est connue. **Elle n'est jamais « connue », elle est mesurée** — et par quoi ? Une balance à impédancemétrie grand public a une erreur qui, propagée dans `370 + 21,6 × masse maigre`, peut dépasser l'avantage revendiqué sur Mifflin-St Jeor. Le document ne demande ni la méthode, ni ne propage l'incertitude.

C'est le même angle mort que le point 3 : **le document ne distingue jamais une donnée mesurée d'une donnée déclarée ou estimée**, alors que c'est le biais structurant de toute la discipline. Cela vaut aussi pour le poids (balance non calibrée, heure de pesée, hydratation).

### 11. Fraîcheur des références EFSA : aucun mécanisme, alors que les valeurs bougent — **PÉRIMÉ en puissance**

Le § 3 dit « Source : EFSA DRV » et `17-donnees-sources.md` prévoit un champ année dans `nutrient_refs`. **Aucun des deux ne prévoit de processus de révision.** Or EFSA a rouvert l'ensemble des UL et en révise depuis 2023 :

| Nutriment                         | Année | Ce qui change                                           |
| --------------------------------- | ----- | ------------------------------------------------------- |
| **Vitamine B6**                   | 2023  | UL **abaissé de 25 mg/j (SCF 2000) à 12 mg/j**          |
| Vitamine D                        | 2023  | Révisé                                                  |
| Folates                           | 2023  | UL 1000 µg/j maintenu, portée précisée (cf. point 1)    |
| Manganèse                         | 2023  | Nouveau UL                                              |
| Vitamine A préformée / β-carotène | 2024  | 3000 µg RE/j maintenu ; **aucun UL pour le β-carotène** |
| Fer                               | 2024  | Révisé                                                  |
| Vitamine E                        | 2024  | Révisé                                                  |
| DHA supplémentaire                | 2026  | Nouveau                                                 |

La B6 est le cas qui fait mal : le document la liste en priorité pour la neuropathie périphérique, et la valeur encore majoritairement citée sur le web est l'ancienne, **plus de deux fois trop permissive**. Un seed construit « de mémoire » ou depuis un site de vulgarisation posera 25 mg et sous-alertera.

**Ce qui manque :** une date de consultation et une échéance de revue par ligne de `nutrient_refs`, et une épreuve qui rougit quand une référence dépasse son échéance. C'est exactement la doctrine « aucune exception sans échéance » de `CLAUDE.md` appliquée aux données au lieu du code.

> Source : [EFSA, UL vitamine B6, 2023, PMC10189633](https://pmc.ncbi.nlm.nih.gov/articles/PMC10189633/)

### 12. La réévaluation dans le temps — une cible de janvier vaut-elle en juin ?

Le document couvre les 3 premières semaines avec soin, puis **s'arrête**. Rien sur :

- **La péremption d'une cible.** 1,8 g/kg calculé sur 73 kg donne 131 g. À 66 kg, la cible est 119 g. Le document ne dit pas si le recalcul est automatique, silencieux, ou soumis à confirmation — or recalculer en silence une cible de santé pendant une perte de poids est précisément le mécanisme qui accompagne une restriction vers le bas sans que personne ne l'ait décidé
- **La re-mesure du TDEE adaptatif.** Calculé une fois en semaine 3, jamais réévalué ? L'adaptation métabolique le déplace, et le document cite lui-même Hall implicitement en parlant de « mesure réelle »
- **Les ruptures.** Blessure et immobilisation font chuter la dépense fortement et brutalement — c'est le scénario central du produit (« reprennent après une blessure ») et il n'a pas de chemin
- **La saisonnalité, le changement de métier, la grossesse** (point 7)

**Coût :** une cible périmée est plus dangereuse qu'une cible absente, parce qu'elle porte l'autorité du calcul.

### 13. Le cycle menstruel — absent, et il corrompt le calcul central

Rien dans le document. Trois conséquences :

- La rétention hydrique cyclique se compte en **kilos**, et le TDEE adaptatif lit un Δpoids sur 3 semaines. Selon la phase de début et de fin de fenêtre, le signal utile est **noyé**. Une fenêtre de 21 jours est de surcroît désalignée d'un cycle typique
- Le métabolisme de repos varie légèrement selon la phase
- Les besoins en fer diffèrent, et le document liste le fer en priorité UL sans jamais évoquer le versant apport

**Coût :** sur la moitié des utilisateurs, le différenciateur du produit mesure du bruit et l'appelle « votre dépense réelle ».

---

## TIER 3 — Contenu absent que le brief nomme explicitement

### 14. Allergènes et régimes d'éviction

Absents intégralement. Il manque deux choses distinctes :

**a) La position juridique sur la donnée allergène.** Le règlement (UE) n° 1169/2011 impose la déclaration des substances de son annexe II (14 catégories). Mais le produit **n'est pas l'exploitant du secteur alimentaire** : il redistribue de la donnée Open Food Facts, qui est **contributive et faillible**. `17-donnees-sources.md` traite les licences avec un grand soin et ne dit **rien** du risque allergène. Une fiche produit affichée sans avertissement, avec un champ allergène incomplet issu d'une contribution bénévole, est le pire couple risque/valeur du produit. Il faut une position écrite (au minimum : ne pas afficher de champ allergène, ou l'afficher avec une réserve explicite et un renvoi à l'emballage).

**b) Ce que l'éviction change aux calculs.** Le document n'a aucune notion de schéma alimentaire. Conséquences non traitées : végétalisme → B12 (le seul nutriment où une carence est franchement invalidante, et le produit calcule justement des totaux de micronutriments), fer non héminique et zinc à biodisponibilité réduite, oméga-3 ; sans lactose/sans produits laitiers → calcium et iode ; sans gluten → fibres et vitamines B.

**Coût :** (a) est un risque de sécurité et de responsabilité ; (b) fait manquer au produit les carences les plus prévisibles de sa base d'utilisateurs, alors qu'il est outillé pour les voir.

### 15. Jeûne et fenêtres alimentaires

Absent. Ce que ça casse concrètement :

- **La frontière du « jour ».** Toute l'agrégation est journalière (§ 4, « journée du 18 août »). Une fenêtre 20h-4h répartit un repas sur deux jours calendaires : les totaux quotidiens deviennent faux, les alertes UL se déclenchent ou se ratent sur un artefact de découpage. Il faut une notion de jour utilisateur, pas de minuit serveur
- **Le Ramadan** — public belge concerné, sur un mois entier. L'hydratation ne peut pas être ingérée le jour ; le § 2 bis recalcule une cible dynamique quotidienne et affiche un écart. Le produit dira à un jeûneur qu'il est en dessous de sa cible d'eau **chaque jour pendant un mois**. Le document promet « aucune injonction à boire », mais un écart affiché en rouge est une injonction sans verbe
- **Le TDEE adaptatif** apparie apport et poids au jour ; un apport concentré déplace le poids sans déplacer le bilan
- **Le recouvrement avec la restriction.** Le jeûne est un habillage fréquent d'une conduite restrictive. `01-conformite.md` § 5 détecte la restriction sévère par les apports : un jeûne déclaré doit-il désarmer, atténuer ou laisser intact ce détecteur ? Question non posée

### 16. L'alcool

Absent partout, y compris là où son absence est visible : le § 2 bis énumère les boissons qui comptent dans l'hydratation (café et thé à 100 %) et **ne dit rien de l'alcool**. Ce qui manque :

- Sa valeur énergétique (7 kcal/g nominal, avec la question de l'énergie réellement métabolisable) et le fait qu'il n'entre dans aucun des trois macronutriments du § 2 — un modèle qui répartit les calories en P/L/G n'a pas de case pour lui, et « le reste des calories » en glucides le rendra invisible ou le comptera comme sucre
- Le passage unités/volumes/degré, source d'erreur classique
- Son statut hydrique, opposé à celui du café
- L'interdiction en grossesse, et l'interaction avec de nombreux traitements

Position OMS à citer et que je n'ai pas pu récupérer dans cette session (page déplacée) : l'OMS a publié en janvier 2023 une déclaration selon laquelle aucun niveau de consommation n'est sans risque, l'éthanol étant classé cancérogène du groupe 1 par le CIRC. **À vérifier à la source avant rédaction** — mais la conséquence de conception tient sans elle : `01-conformite.md` interdit de prescrire, donc le produit ne peut que compter et comparer. Encore faut-il qu'il compte.

### 17. Sodium, sucres ajoutés, acides gras saturés et trans

Le tableau du § 2 s'arrête aux protéines, lipides, glucides, fibres, eau. Manquent quatre références publiées, chiffrées, applicables :

|                           | Référence                                                                                               | Source                                        |
| ------------------------- | ------------------------------------------------------------------------------------------------------- | --------------------------------------------- |
| **Sodium**                | < 2 g/j sodium = < 5 g/j sel                                                                            | OMS, fiche mise à jour 11/05/2026             |
| **Sucres libres**         | < 10 % de l'énergie ; conditionnel < 5 %                                                                | OMS, fiche « alimentation saine », 26/01/2026 |
| **Sucres ajoutés/libres** | Aucun UL établissable ; apport _« as low as possible in the context of a nutritionally adequate diet »_ | EFSA 2022                                     |
| **AGS / trans**           | ≤ 10 % E / ≤ 1 % E                                                                                      | OMS, 26/01/2026                               |

Le sodium mérite un traitement particulier, parce que son absence crée une **contradiction interne** : le § 2 bis évoque le risque d'hyponatrémie au-delà d'~1 L/h. L'hyponatrémie est un problème de **rapport eau/sodium**. Le produit suit l'eau au geste près et ne suit pas le sodium — il ne peut donc pas évaluer le risque qu'il annonce surveiller. Il lui manque la moitié de l'équation qu'il cite.

Sur les sucres ajoutés, un manque de données à acter : CIQUAL et Open Food Facts fournissent les **sucres totaux**, pas les sucres **ajoutés**. Le document doit dire ce qu'il affiche et ce qu'il ne peut pas afficher, plutôt que laisser l'implémentation confondre les deux.

> Sources : [OMS, réduction du sel](https://www.who.int/news-room/fact-sheets/detail/salt-reduction) ; [OMS, alimentation saine](https://www.who.int/news-room/fact-sheets/detail/healthy-diet) ; EFSA, avis sur les sucres alimentaires, 2022

### 18. Les fibres : une quantité, et rien d'autre

La fourchette 25-35 g / défaut 30 g est **cohérente avec les références** (OMS : au moins 25 g/j pour l'adulte). Le chiffre n'est pas en cause — c'est tout ce qui l'entoure qui manque :

- **La montée progressive.** Passer de 12 à 30 g du jour au lendemain parce qu'une jauge est en dessous de sa cible produit des symptômes digestifs immédiats. Une cible affichée sans notion de progressivité est une invitation à ce saut
- **Le couplage avec l'eau**, alors que le produit suit précisément l'hydratation et pourrait le faire
- **L'interaction fibres/phytates–minéraux**, qui réduit l'absorption du zinc et du fer. C'est piquant : le zinc est l'exemple emblématique du § 4. Le produit additionnera un apport en zinc dont il ignore que la biodisponibilité varie fortement avec la matrice
- **Les contre-indications** : poussée de MICI, syndrome occlusif, post-opératoire, régimes pauvres en FODMAP. Une cible de fibres poussée sans réserve chez quelqu'un « qui reprend après une prothèse » — donc possiblement en post-opératoire — est un contresens

### 19. Protéines : aucun dépistage rénal, aucune modulation par l'âge

Le défaut de 1,8 g/kg et le plafond de 2,2 g/kg sont posés pour une population implicite : adulte jeune en bonne santé. Manquent deux garde-fous :

- **Fonction rénale.** Un apport protéique élevé chez une personne à fonction rénale réduite — souvent non diagnostiquée — n'est pas anodin, et les recommandations néphrologiques vont dans le sens inverse. Je n'ai pas pu confirmer les valeurs KDIGO exactes dans cette session (à instruire avant rédaction), mais le manque tient sans le chiffre : **il n'y a aucune question de dépistage**, donc aucun chemin
- **Âge.** Frankenfield et al. (2005), la revue systématique de référence sur les équations prédictives, conclut que Mifflin-St Jeor est la plus fiable mais que _« older adults and US-residing ethnic minorities were underrepresented both in the development of predictive equations and in validation studies »_, justifiant _« a high level of suspicion regarding the accuracy of the equations »_ chez le sujet âgé. Le document reprend l'équation sans reprendre sa réserve, et ne pose **aucune borne d'âge** — ni haute, ni basse (les mineurs sont traités dans `13-juridique.md`, mais la nutrition n'en tire aucune conséquence de calcul)

**Coût :** le produit applique une équation hors de son domaine de validation à une population qu'il déclare cibler, sans le dire.

> Source : Frankenfield D et al., _J Am Diet Assoc_ 2005

### 20. Les déclencheurs d'orientation propres à la nutrition

`01-conformite.md` § 6 liste des motifs d'escalade, tous à dominante musculo-squelettique ou neurologique. Les motifs **nutritionnels** manquent, alors que c'est le document nutrition qui devrait les fournir :

- Aménorrhée ou trouble du cycle (cf. point 6)
- Vomissements, usage de laxatifs ou de diurétiques
- Grossesse déclarée (cf. point 7)
- Traitement en cours déclaré (cf. point 5)
- Insuffisance rénale ou hépatique connue
- Antécédent de TCA déclaré
- Dépassement **répété** d'un UL, par opposition au dépassement ponctuel que le § 4 affiche
- Urines foncées avec douleurs musculaires après reprise intense (tableau de rhabdomyolyse) — scénario réaliste pour un public qui reprend

**Ce qui manque aussi, et qui est structurel :** le document ne dit jamais **vers qui** on oriente, ni comment. « Message d'orientation vers un professionnel » n'est pas implémentable en l'état. Quel professionnel, avec quel texte, une seule fois ou à chaque occurrence, avec ou sans trace ? Sans réponse, chaque écran inventera la sienne, et le vocabulaire de `01-conformite.md` sera respecté par accident plutôt que par construction.

### 21. Deux régimes juridiques absents du cadre

`01-conformite.md` nomme trois régimes (profession réglementée, RGPD, politique Anthropic). Deux manquent, et tous deux sont déclenchés par des choix qui vivent dans `04-nutrition.md` :

- **Règlement (UE) 2017/745 (MDR), règle 11.** Keutzer & Simonsson (2020) notent que _« In comparison to the previous MDD, the MDR is more stringent, especially regarding the classification of health apps and software »_. Une application de bien-être qui compare à des références reste en principe hors dispositif médical — mais la frontière est réelle, et le produit s'en rapproche à chaque fois qu'il calcule une cible individualisée et signale un dépassement. Il faut une position écrite, comme `17-donnees-sources.md` § 6 en prévoit déjà une pour l'ODbL. C'est exactement la même méthode, appliquée à une question qui n'a pas été posée
- **Règlement (CE) n° 1924/2006, allégations nutritionnelles et de santé.** La couche LLM explique les nutriments. Dès qu'elle écrit « le zinc contribue à… », elle produit une allégation de santé, dont la formulation est encadrée et dont seules les versions autorisées au registre européen sont utilisables. Le filtre de sortie de `01-conformite.md` est construit pour attraper le vocabulaire **prescriptif** ; il n'attrapera pas une allégation non autorisée, qui est un problème distinct

> Source : [Keutzer & Simonsson, JMIR mHealth uHealth 2020, PMC7381013](https://pmc.ncbi.nlm.nih.gov/articles/PMC7381013/)

---

## Points à vérifier avant rédaction (non confirmés dans cette session)

Le budget de recherche web a été épuisé tôt ; ces points sont des pistes, pas des acquis :

1. **Hydratation, 35 ml/kg.** Le § 2 bis produit 2 555 ml de _boissons_ pour 73 kg. Les valeurs de référence EFSA pour l'eau portent, sauf erreur, sur l'**eau totale**, aliments compris (~20-30 % de l'apport). Si c'est exact, la cible du document est systématiquement haute par rapport à sa source apparente, et le § 2 bis ne dit jamais sur quelle base EFSA il s'appuie. **À confronter à l'avis EFSA sur les DRV pour l'eau (2010).**
2. **EGCG / extrait de thé vert** — valeur exacte du seuil EFSA (point 4).
3. **KDIGO / protéines en insuffisance rénale** — valeurs exactes (point 19).
4. **OMS janvier 2023 sur l'alcool** — page déplacée, formulation exacte à récupérer (point 16).
5. **Coût de l'entraînement, 5 kcal/min** (§ 1) — affirmé sans source dans le document. **NON SOURCÉ** en l'état ; à instruire, d'autant qu'il alimente le TDEE et, si le point 6 est retenu, le calcul de disponibilité énergétique.

---

## Ce qui manque en une phrase

Le document décrit avec soin **un utilisateur adulte, en bonne santé, non traité, non enceinte, qui mange trois repas entre le lever et le coucher, déclare honnêtement ce qu'il mange, et ne boit pas d'alcool.** Le brief du produit décrit quelqu'un d'autre.

Les trois manques à traiter en premier, parce qu'ils cassent la fonctionnalité que le document désigne comme sa raison d'être : **la portée des UL (point 1)**, **les unités et formes chimiques (point 2)**, et **l'écart entre apport déclaré et apport réel (point 3)**. Les trois sont dans le même chemin de code, et les trois produisent des chiffres faux affichés avec autorité.
