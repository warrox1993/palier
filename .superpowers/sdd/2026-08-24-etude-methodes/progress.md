# Deux études en fan-out — les méthodes, et le sexe

_24/08/2026. Demandées par le porteur : « lance des recherches /workflows complète afin d'aller chercher différents types d'entraînement pour tout type de personnes », puis « attention que ce sont les entraînements homme femme »._

**64 agents, 7,6 millions de jetons, 1 h 45. Chaque conclusion contestée par un agent sceptique remontant aux sources primaires.**

---

## 1. Ce que les deux dossiers contiennent

| Dossier                                     | Contenu                                                                                    |
| ------------------------------------------- | ------------------------------------------------------------------------------------------ |
| `synthese-methodes.md`                      | 14 familles, **203 méthodes** identifiées, ce qui sert le produit et ce qu'il faut écarter |
| `../2026-08-24-etude-sexe/synthese-sexe.md` | 14 axes, **151 différences examinées**, et la réponse : aucune ne justifie deux programmes |

Ces fichiers sont des **dossiers de décision**, pas du contenu. Rien n'en a été copié dans le produit.

---

## 2. Ce que les sceptiques ont cassé

C'est la valeur principale du dispositif, et elle n'était pas prévisible. La note qui ouvre le dossier sur le sexe :

> Les analyses d'axes ont été **systématiquement plus affirmatives que leurs sources**. Les sceptiques ont vérifié à la source et relevé au moins huit erreurs factuelles vérifiables, plusieurs citations tronquées et un usage récurrent de « aucune différence » pour désigner des résultats seulement non significatifs.

Trois exemples qui circulent partout dans le secteur :

- **Tabata.** L'article original ne publie **aucun pourcentage** de gain de VO2max — les « +13 % », « +14 % » et « +23 % » sont des divisions faites par des tiers. Tabata lui-même a écrit en 2019 et 2024 que seul le déroulé temporel a été retenu.
- **Le nordic curl « −50 % de blessures ».** La réévaluation de 2021 montre que 5 essais sur 15 randomisaient réellement l'exercice, et conclut à un effet **inconclusif**.
- **La méta-analyse la plus citée sur les adolescents.** Son résumé annonce des effets « modérés par le sexe » ; le texte intégral donne **p = 0,92**, et la seule comparaison significative repose sur 2 études, 24 filles — et **favorise les filles**. Le dossier la qualifie de « source savante la plus détournée du secteur ».

Une méta-analyse sur le Copenhagen a même été **rétractée en 2026** pour analyse erronée.

---

## 3. Une erreur que j'avais transmise au porteur

Je lui avais présenté l'étude Carneiro 2024 comme « volume strictement égalisé — 75 séries par semaine dans les deux groupes ». Le sceptique est remonté à la source :

> volume total **non apparié à 16 % près** (reconnu par les auteurs), masse maigre et poids **non rapportés**, écarts-types supérieurs aux moyennes, une seule citation, aucune réplication. **À traiter comme une hypothèse, jamais comme une règle.**

Le résumé de recherche disait « volume-matched » ; les auteurs eux-mêmes disent l'inverse. **Un résumé n'est pas une source**, et c'est la leçon de méthode de ces deux journées.

Conséquence : on ne peut pas étiqueter le full body comme meilleur pour la perte de masse grasse.

---

## 4. Le résultat le plus important, et il n'était pas cherché

> **Aucune méthode de ce corpus n'a été évaluée sur la survenue de blessures chez une personne qui reprend après une blessure, une grossesse ou une pose de prothèse.**

Les 10 essais du Copenhagen portent **exclusivement** sur des athlètes masculins. La revue de référence sur les blessures des recrues pompiers n'a identifié **aucune** étude. Zéro étude de kettlebell en post-partum ou après arthroplastie.

Le public que `00-produit.md` place au cœur de la cible **n'a jamais été mesuré**. Tout jugement de risque de ce dossier repose sur des marqueurs indirects et du raisonnement mécanique — jamais sur des blessures observées dans la population visée.

---

## 5. Ce qui a été appliqué

### D78 — le sexe ne décide rien de l'entraînement

La conclusion est sans nuance, et les résultats nuls reposent sur des effectifs très supérieurs à ceux des différences alléguées : 7 289 personnes pour la relation charge-répétitions, 78 études pour le cycle menstruel, contre `n = 42` pour l'écart de plus grande ampleur.

**Le dossier réclamait nommément une épreuve**, et c'était le bon geste :

> Cette absence doit être défendue explicitement — écrite comme une contrainte d'architecture, avec une épreuve automatisée qui échoue si quelqu'un branche un jour le sexe sur un minuteur de repos, un volume suggéré ou un catalogue d'exercices.

`AUCUN_type_d_entrainement_ne_prend_le_SEXE_en_dependance` parcourt constructeurs, propriétés, champs et méthodes. **Éprouvée par provocation** : une sonde posée dans `Palier.Application.Entrainement` a été refusée sur ses deux voies, nommément. Une seconde épreuve garde le garde-fou — elle vérifie qu'il inspecte réellement des types et que `Sexe` est toujours reconnu.

**Pourquoi une épreuve et non un commentaire :** brancher le sexe sur une décision d'entraînement ne casserait rien, ne lèverait rien, et passerait toutes les autres épreuves. Le défaut serait invisible au compilateur et visible seulement à l'écran, sous la forme d'un stéréotype que le produit aurait fabriqué lui-même.

### La note du programme « une séance » corrigée

Elle affirmait qu'une séance hebdomadaire fonctionne, sans réserve. Le dossier appelle cette généralisation **la plus dangereuse de tout le corpus** : les doses de maintien réussissent chez les 20-35 ans et **échouent** chez les 60-75 ans.

La note porte désormais la réserve d'âge, et D77 aussi. Le « Weekend Warrior » que D77 invoquait est également corrigé : c'est 150 min modérées ou 75 min vigoureuses concentrées en une ou deux séances, pas une séance courte.

---

## 6. Ce qui a été DÉLIBÉRÉMENT écarté de l'application

**Aucun des 203 méthodes n'entre au produit.** Onze programmes et 255 exercices attendent déjà un relecteur ; chaque ajout allonge cette file. Le porteur avait d'ailleurs tranché en ce sens — « par méthode nommée, plus tard » — avant de demander la recherche.

**Aucun chiffre de la littérature n'est écrit dans le produit.** Les deux dossiers portent des dizaines de valeurs ; une vingtaine se sont révélées fausses à la vérification. Elles servent à décider, pas à afficher.

**L'entraînement militaire n'entre pas non plus**, alors qu'il était demandé. Les formats « crucible » — 6 à 50 h d'effort continu avec privation de sommeil, sans dépistage médical décrit — ont un profil de risque documenté dans les populations analogues : rhabdomyolyse, hypothermie, hyponatrémie, fractures de fatigue.

---

## 7. La TROISIÈME instabilité du même mode

`npm run verify` a échoué une fois sur `harnais:back`, à
`back/tests-harness/vulnerabilites.test.mjs:80` : **la restauration NuGet** a rendu un code non nul, pas l'énumération des vulnérabilités. Relancé isolément, le harnais rend **60 épreuves sur 60**.

**L'épreuve a fait exactement ce qu'il faut.** Elle vérifie que la restauration a réussi AVANT d'énumérer, parce qu'une énumération sur restauration ratée ne prouverait rien. Une épreuve à une seule assertion serait passée au vert en mentant.

**C'est la troisième occurrence du même mode**, et il faut désormais le dire comme un défaut et non comme une curiosité :

| Quand        | Où             | Ce qui a échoué                              |
| ------------ | -------------- | -------------------------------------------- |
| 24/08, lot 5 | `back:test`    | seuil de couverture, 98,24 % mesuré en isolé |
| 24/08, matin | `front:test`   | `lint:types` rend une sortie partielle       |
| 24/08, soir  | `harnais:back` | restauration NuGet                           |

Les trois partagent une signature : **un outil externe lancé depuis une épreuve, sous charge parallèle, rend un code non nul sans avoir fait son travail**. Mesuré ce matin sur l'état antérieur à toute modification : un échec sur trois passages.

**Je n'ai pas la cause.** L'hypothèse plausible reste la contention, et c'est une hypothèse. Ce dépôt refuse de la présenter pour une mesure.
