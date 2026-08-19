# Références visuelles et protocole de décision

**Date :** 19 août 2026
**Complète :** `docs/02-design.md`, qui reste prescriptif. Ce document ne le contredit jamais ; il lui donne un référentiel et une méthode.

---

## 1. Pourquoi ce document existe

Le porteur du projet ne se juge pas compétent en design et ne peut pas fournir de références. La méthode habituelle — « donnez-moi cinq produits qui vous plaisent » — est donc inapplicable.

On inverse : **la direction se dérive du produit, pas du goût**, et la validation se fait sur des maquettes rendues, pas sur des descriptions. Choisir entre deux images ne demande aucune compétence en design ; décrire une direction en demande beaucoup.

---

## 2. La direction, et son nom

`02-design.md` décrit une direction sans la nommer. Elle en a une, et il est utile de pouvoir la nommer pour juger un écran.

**Industrial / instrumentation**, croisée avec **monospace / technique** et la discipline de grille **suisse**.

Ce que cela signifie concrètement, et que `02-design.md` impose déjà :

| Trait | Où c'est écrit |
|---|---|
| Monospace sur **toutes** les valeurs numériques, sans exception | `02-design.md` § 4 |
| Couleurs qui **encodent** une information, jamais décoratives | § 3, code des disques olympiques |
| Rayon 2 px partout, aucune ombre portée | § 4 |
| Hiérarchie par niveaux de surface et bordures | § 4 |
| Trois animations dans toute l'application | § 6 |
| Densité et lisibilité avant l'ornement | § 1 |

Le document le formule ainsi : « un produit d'**instrumentation**, pas une application grand public arrondie ».

### Ce que cette direction n'est pas

La direction la plus proche du défaut des modèles génératifs est le « warm minimal » — fond crème, serif à fort contraste, accent terracotta. C'est exactement ce que `02-design.md` § 2 interdit en premier, en citant les codes hexadécimaux `#F4F1EA` et `#D97757`.

---

## 3. La référence maîtresse : Teenage Engineering

Fabricant suédois d'instruments de musique (OP-1, PO series). C'est la référence la plus proche de ce que le produit doit être, et elle a l'avantage d'être **inconnue du fitness** — aucune application concurrente ne ressemble à ça.

Ce qui s'y transpose directement :

- **L'usage exclusif du monospace communique précision, ingénierie et honnêteté technique.** C'est littéralement la règle de `02-design.md` sur JetBrains Mono, et sa justification est la même.
- **La contrainte comme esthétique.** L'OP-1 n'a qu'un écran et quatre boutons, et les professionnels le trouvent plus rapide qu'un logiciel aux possibilités illimitées. C'est l'argument du § 1 : « une interface élégante qui coûte deux secondes de plus par série est une mauvaise interface ».
- **Chaque élément sert une fonction.** Pas d'ornement, pas de décoration qui n'encode rien.
- **Un brutalisme qui reste chaleureux.** Lignes nettes, couleurs franches employées comme signaux, sérigraphie, étiquettes.

Ce qu'on ne transpose pas : les couleurs saturées et le côté jouet. Le produit s'utilise en salle, à luminosité réduite, sombre par défaut.

## 4. Références secondaires, par aspect

| Aspect | Où regarder | Ce qu'on y prend |
|---|---|---|
| Densité et hiérarchie de données | IBM Carbon | la discipline d'un système complet, pas son apparence |
| Étiquettes et relevés techniques | fiches techniques, appareils de mesure, dessin industriel | la sensation « fiche technique » |
| Couleur fonctionnelle | disques olympiques (déjà retenu), signalétique industrielle | la couleur qui dit quelque chose |
| Grille et alignement | tradition typographique suisse | la grille **est** le design |

---

## 5. Où chercher, et pour quoi

| Galerie | Contenu | Utilité ici |
|---|---|---|
| **Mobbin** | captures d'applications réellement livrées, mobiles et web, classées par écran | la plus utile : patterns d'écrans réels, pas des concepts |
| **Refero** | équivalent pour les produits web et SaaS | flux et composants réels |
| **Godly** | curation resserrée, motion design | juger le mouvement sans ouvrir cent sites |
| Awwwards, Lapa Ninja | pages d'accueil primées | **peu utile** : landing pages marketing, pas des écrans d'application |
| Typewolf | appariements typographiques réels | sortir du petit lot de polices sur-utilisées |

**Le piège à connaître :** les bibliothèques de composants et d'animations toutes faites — les collections d'effets Tailwind, les kits « modernes » — sont précisément la source du look générique. Elles livrent dégradés, glassmorphism, cartes arrondies à ombre douce. S'en servir reviendrait à réintroduire ce que le § 2 interdit.

---

## 6. S'inspirer, pas copier

S'approprier une **direction esthétique** — la sobriété technique, le monospace, la couleur fonctionnelle — est libre : une direction n'appartient à personne.

Reproduire une **interface identifiable** — la disposition exacte d'un écran reconnaissable, un jeu d'icônes propriétaire, une charte — expose à une action en contrefaçon ou en concurrence déloyale. `00-produit.md` rappelle déjà ce que trois ans de procédure ont coûté à un acteur du secteur.

Règle pratique : on note **pourquoi** un écran fonctionne, jamais **à quoi** il ressemble.

---

## 7. Protocole de décision

Puisque la direction ne peut pas être validée par description, elle l'est par comparaison.

1. **Rendre, toujours.** Quatre familles de défauts ne se voient pas dans le code : dominance de la palette, rythme des espacements, hiérarchie visuelle, mouvement. Aucun écran n'est déclaré terminé sans avoir été rendu et regardé. Le MCP Playwright sert à ça, et `02-design.md` § 7 le demande déjà.
2. **Deux ou trois maquettes, pas une.** Au lot 2, l'écran de séance est produit en plusieurs variantes réelles, conformes à la structure imposée du § 7. Le choix se fait à l'œil.
3. **Nommer les mouvements avant de coder.** Appariement typographique, position sur la couleur, position sur la mise en page, idée de mouvement, un détail signature. Cinq décisions explicites, pas un tâtonnement.
4. **Vérifier qu'on ne se répète pas.** Le mode d'échec suivant n'est pas le générique, c'est le « non-générique de bon goût » — le même petit lot de polices et de palettes que tout le monde emploie pour paraître original. Si une direction ressemble à la précédente, elle est refusée.

---

## 8. Contrôles ajoutés aux règles du lot 1

Sept défauts fréquents que `02-design.md` ne couvre pas, dont quatre sont mécanisables :

| Défaut | Traitement |
|---|---|
| Flèches Unicode collées aux libellés — « Commencer → » | règle de lint, bloquant |
| `backdrop-blur` réflexe | règle de lint, bloquant |
| Ombres portées, sous toutes leurs formes | règle de lint, bloquant — le § 4 les interdit déjà |
| Police hors des trois familles retenues | règle de lint, bloquant |
| Une seule largeur de conteneur pour toutes les sections | revue |
| Cartes à filet coloré sur un bord | revue |
| Avatars et médias de remplacement laissés en place | revue |

L'audit complet est confié au skill `avoid-ai-design`, en mode détection, à chaque fin de lot touchant l'interface.

---

## 9. Références réellement examinées — 19 août 2026

Captures prises et analysées, pas paraphrasées depuis une description.

### Applications du domaine

| App | Direction | Ce qu'on prend | Ce qu'on écarte |
|---|---|---|---|
| **MacroFactor** | noir profond, capitales condensées, un seul accent cuivre, lignes denses avec barre fine sous chacune | la direction visuelle entière — c'est `02-design.md` presque réalisé | rien de notable |
| **Strong** | iOS natif, bleu par défaut | la structure de saisie : grille `Série / Précédent / kg / Reps / ✓`, validation par coche et fond, minuteur en anneau | l'apparence : « propre » par absence de décision |
| **Cronometer** | barres horizontales, une couleur par macro, pourcentage à droite | la densité des listes de nutriments | l'anneau de calories et la palette arc-en-ciel sans dominante |
| **Gentler Streak** | orange vif, dégradés, mascotte illustrée | l'idée des cinq métriques en ligne avec micro-graphique | tout le reste : halo coloré, illustration, cartes ombrées |

### Produits hors domaine

| Produit | Enseignement |
|---|---|
| **Linear** | noir quasi pur, densité verticale maîtrisée, **aucune couleur décorative** — la couleur n'encode que le statut, par petites pastilles. Monospace réservé aux identifiants techniques. C'est la démonstration que retenue et densité peuvent coexister |
| **Railway** | une **illustration propriétaire peinte** donne une identité incopiable. Le principe est transposable : `14-contenu.md` prévoit déjà 60 schémas de mouvements au trait — c'est l'atout d'identité le plus sous-estimé du dossier |

### Le constat qui vaut plus que les captures

**Aucune des quatre applications du domaine n'affiche de limite haute.** Cronometer annonce « B12 290 % », MacroFactor « Omega-3 DHA 305 % » ; ni l'une ni l'autre n'indique si c'est un sujet. Ce sont des compteurs d'objectifs, pas des instruments de sécurité.

La règle graduée de `02-design.md` § 5 — valeur, fourchette, limite haute, position — **n'existe nulle part dans la catégorie**. C'est simultanément la différenciation produit, la différenciation visuelle et la position juridique défendue par `00-produit.md`. Le même écran, un régime opposé.

### Ressource retenue pour la suite

`recent.design` (ex-Godly) : curation quotidienne, filtrable par **Motion**, **Interface**, **Typography**, **App Screenshots**. C'est la source la plus dense pour observer le mouvement et les interfaces réelles.

**Limite à connaître :** une capture ne montre pas le mouvement. Les animations se jugent en regardant, pas en lisant une description — c'est le seul point de ce document qui demande un œil humain devant un écran.

---

## 10. Le mouvement — valeurs mesurées, non inventées

Relevé le 19 août 2026 en interrogeant directement le style calculé de trois références de premier plan. Ce ne sont pas des impressions : ce sont les valeurs que ces produits appliquent réellement.

| Référence | Courbe dominante | Durées principales | Variantes déclarées |
|---|---|---|---|
| **Linear** | `cubic-bezier(0.25, 0.46, 0.45, 0.94)` | 0,10 s · 0,16 s · 0,40 s | 20 courbes déclarées, **une seule utilisée** |
| **Stripe** | `cubic-bezier(0.25, 1, 0.5, 1)` | 0,30 s — 477 occurrences contre 58 à la suivante | 22 variantes, une écrasante |
| **Vercel** | `cubic-bezier(0.4, …)` | 0,10 s · 0,15 s | 17 variantes, 45 % sur la dominante |

### Trois constantes

1. **Une courbe domine massivement.** Linear déclare vingt courbes d'accélération dans ses variables CSS et n'en emploie qu'une. La discipline n'est pas d'avoir une belle bibliothèque : c'est de ne pas s'en servir.
2. **Toutes sont des sorties douces.** Départ rapide, arrivée en ralenti. Jamais d'entrée douce sur le mouvement courant — c'est ce qui donne la sensation de réponse immédiate au geste.
3. **Deux à trois durées, pas plus.** Les plus fréquentes sont courtes, 0,10 à 0,16 s. Les durées longues sont réservées aux grands changements : fonds, panneaux, un moment expressif.

### Confrontation avec `02-design.md` § 6

Les trois animations prévues résistent à l'épreuve :

| Animation | Durée prévue | Verdict |
|---|---|---|
| Le repère de la règle qui glisse | 400 ms | correspond au cran long des trois références. Justifié : c'est le seul moment expressif du produit |
| Validation d'une série, bordure au vert | 150 ms | correspond exactement au cran courant mesuré partout |
| Décompte du minuteur | linéaire | correct — un décompte non linéaire mentirait sur le temps écoulé |

**Ce que le § 6 ne prévoit pas :** le retour immédiat au doigt. Les trois références consacrent 0,10 s à la couleur au survol ou à l'appui. Sur un produit tenu d'une main entre deux séries, l'état actif est ce qui confirme que l'appui a été pris.

### L'échelle retenue

```
--duree-instant   : 100ms   couleur, état actif, retour au doigt
--duree-courante  : 150ms   validation d'une série, apparition d'un élément
--duree-longue    : 400ms   le repère de la règle, seul moment expressif
--courbe          : cubic-bezier(0.25, 0.46, 0.45, 0.94)   sortie douce, unique
```

Trois durées, une courbe, rien d'autre. Sous `prefers-reduced-motion`, tout passe à zéro — `11-qualite.md` l'impose déjà.

**Mécanisable :** une règle de lint refuse toute `transition-duration` et toute `cubic-bezier` qui ne soit pas l'un de ces jetons. C'est la réponse directe au défaut le plus courant du mouvement généré — des micro-interactions éparpillées sans langage commun.

---

## 11. Le mouvement en natif — Material 3 et Apple

Relevé le 19 août 2026 sur les documentations officielles. Une application native installée n'est pas instrumentable depuis ici ; les systèmes qui régissent le natif, eux, publient leurs valeurs.

### Material 3 — un système de jetons

Seize durées, quatre par famille :

| Famille | Valeurs |
|---|---|
| short | 50 · 100 · **150** · 200 ms |
| medium | 250 · 300 · 350 · **400** ms |
| long | 450 · 500 · 550 · 600 ms |
| extra-long | 700 · 800 · 900 · 1000 ms |

Cinq courbes :

```
Standard               cubic-bezier(0.2, 0.0, 0, 1.0)
Standard decelerate    cubic-bezier(0, 0, 0, 1)
Standard accelerate    cubic-bezier(0.3, 0, 1, 1)
Emphasized decelerate  cubic-bezier(0.05, 0.7, 0.1, 1.0)
Emphasized accelerate  cubic-bezier(0.3, 0.0, 0.8, 0.15)
```

Règles d'usage citées textuellement :

- « Les contrôles de sélection ont une durée courte de **200 ms** avec la courbe Standard »
- « Un bouton flottant qui se déploie en feuille utilise **400 ms** avec la courbe Emphasized »
- « Une carte qui s'ouvre en plein écran utilise **500 ms** »
- « Au-delà de 600 ms, c'est rare, et réservé aux transitions d'ambiance **sans intervention de l'utilisateur** »

### Apple — aucun chiffre, des principes

La documentation de mouvement d'Apple ne publie **aucune durée**. iOS anime par ressorts physiques — masse, raideur, amortissement — et non par durée fixe. Un ressort peut être interrompu et repris en cours de route, ce qu'une durée fixe ne permet pas.

Quatre principes en découlent, et deux touchent directement ce produit :

1. « **Dans les applications, évitez d'ajouter du mouvement aux interactions d'interface qui se produisent fréquemment.** Vous ne voulez généralement pas faire passer aux gens du temps supplémentaire à regarder un mouvement inutile chaque fois qu'ils interagissent. »
2. « **Laissez les gens annuler le mouvement.** Autant que possible, ne faites pas attendre la fin d'une animation avant de pouvoir agir, surtout s'ils doivent la subir plus d'une fois. »
3. « Visez la brièveté et la précision dans les animations de retour. »
4. « Rendez le mouvement optionnel — complétez le retour visuel par des alternatives comme **le retour haptique** et le son. »

### Ce que ça change pour ce produit

**Le premier principe d'Apple vise exactement le geste central du produit.** Une série se saisit quinze à trente fois par séance. C'est la définition d'une interaction fréquente. L'animation de validation prévue au § 6 de `02-design.md` reste légitime — c'est un retour, bref et précis — mais elle ne doit jamais retarder la saisie suivante, et sa durée penche vers le bas de la fourchette.

**Confrontation des trois sources sur la validation d'une série :**

| Source | Valeur |
|---|---|
| Web mesuré (Linear, Vercel) | 150-160 ms |
| Material 3, contrôles de sélection | 200 ms |
| `02-design.md` § 6 | 150 ms |

150 ms est un jeton officiel Material (`short3`) et correspond au web mesuré. La valeur du dossier tient. Material recommande 200 ms spécifiquement pour les cases à cocher ; l'écart est mince et la valeur basse sert mieux un geste répété trente fois.

**Le repère de la règle à 400 ms** correspond exactement au jeton `medium4` de Material, cité pour une expansion avec courbe Emphasized. Le dossier est aligné sans le savoir.

### Un manque signalé : le retour haptique

Apple recommande de compléter le retour visuel par l'haptique. Sur un produit tenu d'une main en salle, mains parfois moites, écran à luminosité réduite, une vibration brève à la validation d'une série vaudrait mieux qu'un changement de couleur qu'il faut regarder.

**Réserve technique, à vérifier avant de s'y engager :** l'API Vibration du web n'est pas prise en charge par Safari sur iOS. Une PWA ne peut donc pas produire de retour haptique sur iPhone. Le retour visuel reste le seul disponible sur cette plateforme, ce qui rend sa qualité d'autant plus importante.
