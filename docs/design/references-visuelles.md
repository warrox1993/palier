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
