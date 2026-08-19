# 02 — Direction artistique

Ce document est prescriptif. Il n'est pas une source d'inspiration mais un système de contraintes. Toute couleur, toute taille de police et tout espacement doivent en découler.

---

## 1. Le contexte réel d'utilisation

Avant toute considération esthétique, décrire honnêtement le moment d'usage :

**Téléphone tenu d'une main, entre deux séries, dans une salle de sport.** Écran parfois consulté avec les mains moites, éclairage agressif, luminosité réduite par l'économie d'énergie, quarante-cinq secondes avant la série suivante.

Le second contexte est le canapé, le soir : consultation des courbes, saisie du repas, conversation avec l'assistant.

**Le premier contexte gouverne le design. Le second l'habille.**

Une interface élégante qui coûte deux secondes de plus par série est une mauvaise interface. Une interface fonctionnelle mais banale ne sera pas ouverte le troisième mois. Les deux échouent.

---

## 2. Interdits explicites

Ces directions sont reconnaissables comme des sorties génériques d'IA. Elles sont exclues quel que soit l'argument :

- Fond crème (#F4F1EA ou proche) avec serif à fort contraste et accent terracotta (#D97757 ou proche)
- Fond noir profond avec un unique accent vert acide ou vermillon
- Mise en page « broadsheet » : filets d'un pixel, aucun arrondi, colonnes serrées façon journal
- Dégradés violet-bleu sur les boutons et les cartes
- Grand nombre en gros, petit label dessous, trois statistiques de soutien, accent en dégradé
- Marqueurs numérotés 01 / 02 / 03 quand le contenu n'est pas une séquence
- Icônes Lucide par défaut sur chaque titre de section
- Ombres portées douces et génériques sur toutes les cartes

Également exclus : les modes clair et sombre traités comme deux thèmes équivalents. Ce produit est **sombre par défaut**, pour une raison fonctionnelle — la salle de sport et le soir.

---

## 3. Ancrage dans le sujet

Le vocabulaire visuel vient du monde de la charge et de la mesure : disques, incréments, paliers, écarts à une référence. Ce sont des **données comparées à des seuils**, pas des scores.

**Le disque olympique** est l'objet le plus caractéristique de cet univers, et son code couleur est une convention internationale réelle, pas une invention décorative :

| Masse | Couleur |
|---|---|
| 25 kg | rouge |
| 20 kg | bleu |
| 15 kg | jaune |
| 10 kg | vert |
| 5 kg | blanc |

Ce code est utilisé pour **encoder de l'information**, jamais comme décoration : le vert et le bleu signalent une valeur dans la fourchette, le jaune un écart, le rouge un dépassement. Un utilisateur qui fréquente une salle reconnaît ce langage sans qu'on le lui explique.

---

## 4. Système de jetons

### Couleurs

| Nom | Hex | Usage |
|---|---|---|
| `surface-0` | `#14181D` | Fond général. Ardoise profonde, jamais du noir pur |
| `surface-1` | `#1D232A` | Cartes, panneaux |
| `surface-2` | `#262E37` | Champs de saisie, éléments actifs |
| `line` | `#333D48` | Bordures, séparateurs |
| `ink` | `#E9E7E2` | Texte principal. Blanc cassé, pas de blanc pur |
| `ink-muted` | `#8A96A3` | Texte secondaire, labels |
| `signal-under` | `#3D82C4` | En dessous de la référence (bleu, disque 20 kg) |
| `signal-ok` | `#4FA37A` | Dans la fourchette (vert, disque 10 kg) |
| `signal-over` | `#F2C230` | Écart notable (jaune, disque 15 kg) |
| `signal-alert` | `#D64541` | Dépassement de limite haute (rouge, disque 25 kg) |

Quatre couleurs de signal, jamais employées pour autre chose que du signal. Aucun accent décoratif supplémentaire.

**Accessibilité :** l'information ne repose jamais sur la couleur seule. Chaque état porte aussi un libellé textuel et une position sur une échelle.

### Typographie

Trois rôles, trois familles. Ne pas réutiliser les combinaisons habituelles (Inter partout, ou Playfair + Inter).

| Rôle | Famille | Usage |
|---|---|---|
| Display | **Barlow Condensed** 600/700, capitales, interlettrage serré | Titres d'écran, noms d'exercices |
| Texte | **Inter** 400/500/600 | Corps, libellés, explications de l'assistant |
| Données | **JetBrains Mono** 600/700 | **Toutes les valeurs numériques, sans exception** |

La règle de la police monospace pour les chiffres n'est pas cosmétique : les charges, les répétitions et les apports se comparent verticalement d'une séance à l'autre. Des chiffres à chasse variable rendent cette comparaison illisible.

Échelle typographique, ratio 1,25 : 11 / 13 / 15 / 19 / 24 / 30 / 38.

### Espacement et formes

Base 4 px. Rayon de bordure **2 px** partout — un produit d'instrumentation, pas une application grand public arrondie. Aucune ombre portée ; la hiérarchie passe par les niveaux de surface et les bordures.

Zone tactile minimale **48 × 48 px**. Espacement vertical minimal entre deux cibles tactiles : 8 px.

---

## 5. Élément signature

**La règle graduée d'écart.**

Chaque valeur mesurée — un apport, un volume hebdomadaire, une charge — s'affiche sur une règle horizontale portant sa fourchette de référence et, quand elle existe, sa limite haute. Le repère de l'utilisateur se place dessus.

```
protéines                                    148 g
├──────────────[███████████████]──────────────┤
0            117              160           250
                    ↑ toi
     en dessous      dans la fourchette      au-delà
```

C'est l'expression visuelle directe de la règle produit — *un chiffre, une référence, un écart, jamais une action* — et c'est le composant réutilisé partout : macronutriments, micronutriments, hydratation, volume d'entraînement, compléments.

Un seul composant, quatre piliers. C'est ce qui donne au produit sa cohérence visuelle : quel que soit l'écran, on lit la même chose de la même façon.

Une seule audace visuelle dans tout le produit : cette règle. Tout le reste reste discret et disciplinée.

---

## 6. Mouvement

Trois animations dans toute l'application, pas une de plus :

1. **Le repère de la règle** glisse jusqu'à sa position à l'affichage — 400 ms, courbe d'accélération douce. C'est le seul moment expressif
2. **Validation d'une série** : la bordure du champ passe au vert en 150 ms. Aucun autre retour
3. **Décompte du minuteur de repos** : progression linéaire, sans effet

Aucune animation d'entrée de page, aucun défilement révélant des éléments, aucune particule, aucun effet de survol décoratif. `prefers-reduced-motion` respecté partout.

---

## 7. Écran de séance — structure imposée

C'est l'écran le plus utilisé du produit. Sa disposition est fixée :

```
┌────────────────────────────────────┐
│ SÉANCE B · CHARNIÈRE      sem. 3   │  ← contexte, discret
├────────────────────────────────────┤
│ SOULEVÉ ROUMAIN HALTÈRES           │  ← display, capitales
│ 3 × 10 · repos 2 min · RIR 4       │  ← mono, prescription
│                                    │
│ dernière fois  22kg × 10 @4        │  ← mono, la donnée la
│                22kg × 10 @4        │    plus consultée
│                                    │
│ → aujourd'hui  24 kg (+2)          │  ← suggestion, vert
│                                    │
│   charge      reps      rir        │
│ 1 [  24  ]  [  10  ]  [  4  ]      │  ← 48px de haut minimum
│ 2 [      ]  [      ]  [      ]     │
│ 3 [      ]  [      ]  [      ]     │
│                                    │
│ [ ça va ] [ bof ] [ gêne ]         │  ← ressenti
├────────────────────────────────────┤
│  1:47   [60s] [90s] [2min] [3min]  │  ← minuteur, fixé en bas
└────────────────────────────────────┘
```

**« Dernière fois » est l'information la plus consultée de tout le produit.** Elle est visible sans défilement, sans dépliage, sans interaction. La suggestion du jour vient juste en dessous.

Aucune fenêtre modale de confirmation pour valider une série. La friction tue la saisie.

---

## 8. Écriture d'interface

- Voix active, phrase à l'infinitif ou à l'impératif : « Enregistrer la séance », jamais « Soumettre »
- Une action garde le même nom du bouton jusqu'au message de confirmation
- Les états vides sont des invitations à agir, pas des messages d'humeur : « Aucune séance enregistrée. La première sert de point de départ à toutes les suivantes. »
- Les erreurs disent ce qui s'est passé et quoi faire. Elles ne s'excusent pas
- Aucun point d'exclamation, aucun emoji, aucune félicitation automatique
- Le vocabulaire de `01-conformite.md` s'applique intégralement à toute l'interface

---

## 9. Contrôle qualité

Avant de considérer un écran terminé :

- [ ] Utilisable d'une seule main sur un écran de 375 px de large
- [ ] Toutes les valeurs numériques en police monospace
- [ ] Aucune couleur hors des jetons définis
- [ ] Toutes les cibles tactiles à 48 px minimum
- [ ] Focus clavier visible
- [ ] `prefers-reduced-motion` respecté
- [ ] Contraste vérifié à luminosité réduite
- [ ] Aucun vocabulaire prescriptif
- [ ] Une seule chose mémorable par écran ; retirer le reste
