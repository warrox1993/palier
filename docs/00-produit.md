# 00 — Produit

## Le problème

Le marché du suivi de musculation est saturé et mature : Fitbod, FitnessAI, Dr. Muscle, Alpha Progression, Juggernaut AI. Ces produits sont bons, notés autour de 4,8, et vendus 12 à 15 $/mois. Les attaquer sur « programmes générés par IA » est perdu d'avance.

Deux manques persistent, et ce sont eux qui définissent ce produit.

### Manque 1 — Personne n'additionne l'assiette et les gélules

Toutes les apps comptent les macronutriments. Certaines comptent les micronutriments alimentaires. Presque aucune n'additionne **alimentation + compléments** pour signaler un dépassement des limites de sécurité.

C'est pourtant là qu'est le risque réel : un utilisateur qui prend un multivitamine, une gélule de zinc et mange des huîtres se retrouve à 42 mg de zinc pour une limite européenne de 25 mg — et le zinc en excès chronique déplète le cuivre. Même logique pour la B6 (neuropathie périphérique au long cours), la vitamine A, le fer chez un homme non carencé.

Personne ne l'alerte. C'est le cœur du produit.

### Manque 2 — Personne n'adapte à une contrainte physique

Blessure ancienne, arthrose, hernie, prothèse, retour après grossesse, sortie de kinésithérapie. Les générateurs d'exercices proposent du développé militaire à quelqu'un qui ne doit pas lever les bras au-dessus de la tête.

Un LLM comprend « j'ai mal ici quand je fais ça ». Un algorithme de sélection d'exercices, non.

## La promesse

**Vois tout. Décide toi-même.**

La seule application qui additionne ton entraînement, ton assiette, tes compléments et ton eau, et te montre où tu te situes par rapport aux références publiées.

La performance et la santé sont les **bénéfices** ; la complétude de la mesure est la promesse. Cette distinction n'est pas cosmétique : « optimise tes apports » promet un résultat de santé et place l'éditeur en conseiller, ce qui est réglementé. « Voici ta valeur, voici la référence, voici l'écart » est une information. Même écran, même donnée, régime juridique opposé.

**Quatre piliers, une seule vue :**

| Pilier              | Ce que l'application mesure                                                       |
| ------------------- | --------------------------------------------------------------------------------- |
| **Entraînement**    | Charges, volume par muscle, progression, équilibre, force estimée                 |
| **Macronutriments** | Protéines, lipides, glucides, fibres, contre les fourchettes                      |
| **Micronutriments** | 40 nutriments, **alimentation et compléments cumulés**, contre les limites hautes |
| **Hydratation**     | Apport quotidien contre 35 ml/kg, majoré les jours d'entraînement                 |

Le positionnement reste défensif dans son exécution — éviter les excès, éviter les blessures — mais ambitieux dans sa promesse.

## La cible

**Le produit** s'adresse à toute personne qui veut savoir ce qu'elle mange et suivre son entraînement, quel que soit son niveau.

**Le lancement** ne s'adresse pas à tout le monde. Un produit pour tous, lancé pour tous, à budget zéro, n'est installé par personne. Le canal de départ est le réseau existant du fondateur : salles de sport, kinésithérapeutes et coachs de la région liégeoise. Français d'abord, anglais ensuite.

Cinquante utilisateurs payants recrutés par un kiné valent mieux que dix mille visiteurs anonymes.

## Produit fini, pas prototype

**Chaque fonctionnalité livrée est finie** : testée, accessible, traduite, avec ses états de chargement, d'erreur et vide, et sa mesure.

> **La gestion hors ligne n'y figure plus depuis le 20/08/2026 — D45.** Le produit est une application **web**, consultée dans un navigateur, et c'est la seule cible du moment. La résilience réseau de `11-qualite.md` § 1 reste voulue, mais elle devient un **chantier daté** (lot 7) au lieu d'une condition que chaque écran doit remplir dès sa première ligne. Une exigence portée par la définition de fini bloque toutes les livraisons ; portée par un lot, elle en bloque une. Il n'existe pas de « on finira plus tard » — c'est la définition de la dette qu'on refuse.

Cela ne veut pas dire que tout s'écrit simultanément, ce qui est matériellement impossible. L'ordre de construction est défini dans `07-roadmap.md`. La différence tient en une phrase : **on séquence la livraison, jamais la qualité.**

## Périmètre complet

### Entraînement

- Journal de séances : charge, répétitions, RIR — saisie en moins de trois interactions
- Programmes modèles adaptés par contrainte déclarée (cervicale, lombaire, épaule, genou)
- Suggestion de progression sur règles déterministes
- Volume hebdomadaire par groupe musculaire, fourchette de référence 10-20 séries
- Ratio tirage/poussée
- Ressenti par exercice : ça va / bof / gêne, avec retrait proposé après deux gênes
- Courbes : charge, répétitions, volume, force estimée
- Minuteur de repos

### Nutrition

- Saisie par recherche texte, code-barres et photo
- Saisie des compléments par photo d'étiquette avec extraction des doses
- Agrégation alimentation + compléments par nutriment
- Comparaison aux références EFSA, alerte sur dépassement des limites hautes
- Objectifs pré-remplis et modifiables par l'utilisateur
- Historique et tendances

### Hydratation

- Cible dynamique : 35 ml/kg, majorée les jours d'entraînement et de cardio
- Saisie en un appui depuis tout écran, contenants personnalisés
- Boissons comptabilisées au prorata de leur teneur en eau
- Signalement d'un apport inhabituellement élevé (risque d'hyponatrémie)

### Assistant

- Explique les chiffres affichés
- Répond aux questions générales d'entraînement et de nutrition
- **Aucun pouvoir d'action** : ne modifie pas un objectif, ne recommande pas un complément, ne génère pas de plan de repas

### Compte et accès

Google OAuth et email/mot de passe, vérification d'email, 2FA optionnelle, sessions listées, récupération manuelle. Onboarding en six écrans. Abonnement Stripe avec essai de 14 jours, plan gratuit conservant la saisie d'entraînement et l'historique complet. Détail dans `09-comptes.md`.

### Progression

Radar à sept axes calculés sur données réelles, six états dérivés, avatar cosmétique en V2. Détail et garde-fous dans `10-progression.md`.

### Qualité de service

Résilience réseau avec file de retry persistée, accessibilité WCAG 2.2 AA vérifiée, français et anglais, mesure d'usage respectueuse de la vie privée hébergée en UE. Détail dans `11-qualite.md`.

### Confort

Duplication de journée, repas enregistrés, recettes, répétition de séance, import depuis Hevy et Strong, export JSON, CSV et PDF, unités impériales, masquage des calories. Détail dans `12-confort.md`.

### Administration

Interface de validation pour le diététicien, journal versionné des libellés, revue des sorties du modèle, file de signalements, tableau de bord de conformité.

## Hors périmètre

Plans de repas générés, recommandation de compléments, objectifs caloriques prescrits, application Android native, fonctions communautaires, intégration d'objets connectés, notation de la qualité des produits alimentaires, lecture automatique des pas (techniquement impossible sur le web).

**Sur la notation des produits :** exclue définitivement. Yuka a été condamnée trois fois en première instance en 2021 pour dénigrement avant de gagner en appel — trois ans de procédure. Ce produit affiche des compositions, il ne juge aucune marque.

## Modèle économique

Abonnement, 20 €/mois. Positionnement assumé au-dessus de la concurrence, justifié par la personnalisation conversationnelle et la couverture des compléments.

Sur 20 € : TVA belge 21 %, frais Stripe, environ 3 €/mois de coût IA avec le mix de modèles décrit dans `06-ia.md`. Marge nette autour de 12 €.

Seuil de rentabilité approximatif : 100 à 150 abonnés.

## Critère de réussite

Pas le nombre de fonctionnalités. Pas l'élégance du code.

**Trente utilisateurs qui ont enregistré au moins vingt séances ou trente journées alimentaires après huit semaines.**

Tout ce qui ne sert pas cet objectif est du décor.
