# 07 — Séquence de construction

**On séquence la livraison, jamais la qualité.** Chaque étape ci-dessous se termine par un ensemble fini : testé, accessible, traduit, avec ses états d'erreur et sa gestion hors ligne. Une étape n'est pas close tant que la définition de terminé de `08-workflow.md` n'est pas cochée intégralement.

Aucune étape ne produit un prototype. L'ordre existe parce qu'on ne peut pas écrire dix mille lignes simultanément, pas parce qu'on accepterait de livrer à moitié.

Durée réaliste en solo avec Claude Code : **6 à 9 mois** jusqu'à l'ouverture payante. Toute estimation plus courte est une estimation fausse.

---

## Étape 0 — Avant tout code

Ces trois points conditionnent la partie nutrition. L'entraînement peut avancer en parallèle.

0. **Lire `16-projet.md`** — arborescence, conventions, variables d'environnement, glossaire. C'est le document qui évite à Claude Code d'inventer ses propres conventions.
1. **Avocat en droit de la santé numérique.** 300 à 500 €. Valider le positionnement, les CGU, la politique de confidentialité
2. **Diététicien agréé partenaire.** Accord écrit : périmètre de validation, responsabilité, rémunération ou participation
3. **Statut.** Indépendant complémentaire pour développer (200-300 €), **SRL avant l'ouverture payante** (1 500-2 500 €). Voir `13-juridique.md`
4. **Assurance responsabilité civile professionnelle** avec extension cyber, avant l'ouverture. 400-900 €/an
5. **Nom et dépôt de marque.** Recherche d'antériorité BOIP et EUIPO, puis dépôt Benelux en classes 9, 41 et 42 — 352 €. Voir `15-marque.md`
6. **Kinésithérapeute relecteur** pour les programmes adaptés aux contraintes. Distinct du diététicien
7. **Vérification des régions et DPA** de chaque fournisseur de modèle, et conception de la minimisation du contexte. Voir `13-juridique.md`

**Si le point 2 échoue, le projet devient un journal d'entraînement.**

Le point 7 est structurant : il conditionne l'architecture de la couche IA et doit être tranché avant d'écrire le premier appel au modèle. Autant le savoir en semaine 1 qu'en mois 6.

---

## Étape 1 — Harnais et socle

**Le harnais avant le produit.** TypeScript strict, ESLint, Prettier, Vitest, Playwright, hooks pre-commit et pre-push, CI GitHub Actions, axe-core. Voir `08-workflow.md`.

- Projet Supabase, **région Francfort ou Paris**
- Schéma complet, RLS sur chaque table dès la création, migrations versionnées
- **Auth : Google OAuth + email/mot de passe**, vérification d'email, limitation de débit, 2FA optionnelle
- Squelette PWA, jetons de design de `02-design.md`, i18n français/anglais en place
- Couche de résilience : Dexie, file de retry persistée, indicateur de synchronisation
- Déploiement Vercel, **spend limit activé**
- Abstraction `LLMProvider` avec les deux fournisseurs et journalisation des coûts

Livrable : inscription par les deux voies, données isolées, un appel modèle de test sur chaque fournisseur, CI verte, audit d'accessibilité au vert.

---

## Étape 1 bis — Contenu

En parallèle du développement, car c'est le poste le plus long et le plus sous-estimé :

- Catalogue de 250 à 400 exercices avec contre-indications
- Schémas vectoriels des mouvements — commencer par les 60 exercices les plus utilisés
- Programmes modèles, relus par le kinésithérapeute
- Pages éducatives
- Emails transactionnels, français et anglais

Compter 6 à 10 semaines de travail, étalées. Voir `14-contenu.md`.

---

## Étape 2 — Onboarding et entraînement

- Catalogue d'exercices avec contre-indications
- Programmes modèles par contrainte
- **Écran de séance**, selon la structure imposée de `02-design.md`
- Suggestion de progression, détection de plateau
- Ressenti par exercice
- Volume hebdomadaire, ratio tirage/poussée
- Courbes
- Onboarding six écrans, avec l'écran contraintes
- Structures full body, upper/lower, PPL et split selon la fréquence
- Répétition de séance, copie des charges, correction rétroactive, jour de repos
- Export JSON, CSV et PDF ; import Hevy et Strong
- Quatre états d'interface sur chaque écran

Livrable : le fondateur utilise l'application pour son propre bloc, pendant au moins trois semaines, sans autre outil.

**Ne pas passer à l'étape 3 avant que cette condition soit remplie.** Un produit qu'on ne veut pas utiliser soi-même ne se vend pas.

---

## Étape 3 — Nutrition, sans assistant

- Import CIQUAL et EFSA
- Intégration OpenFoodFacts, scan de code-barres
- Saisie manuelle et recherche
- Objectifs pré-remplis, modifiables, avec planchers de sécurité
- Agrégation alimentation + compléments
- **Composant « règle d'écart »**, l'élément signature
- Alerte sur dépassement des limites hautes
- Garde-fous TCA complets et testés

Livrable : validation écrite du diététicien sur les règles et les libellés.

---

## Étape 3 bis — Hydratation et confort nutrition

**Hydratation** : cible dynamique, saisie en un appui, contenants personnalisés, règle graduée. Peu de travail, fort effet sur l'usage quotidien — c'est le geste le plus fréquent du produit.

**Confort nutrition** :

Duplication de journée, repas enregistrés, recettes, suggestions contextuelles, prises de compléments récurrentes, favoris, recherche globale.

---

## Étape 4 — Vision

- Photo d'étiquette de complément, extraction des doses
- Photo de repas, avec dialogue de quantification
- Comparaison qualité/coût entre les deux fournisseurs sur cas réels

---

## Étape 5 — Progression

Radar à sept axes, six états dérivés, auto-tests de mobilité, table `progression_snapshots` avec les valeurs sources pour audit. Avatar 3D **non inclus** — reporté après l'ouverture.

---

## Étape 6 — Assistant

- Contexte mis en cache
- Routage par tâche
- Filtre de sortie, testé
- Détections prioritaires
- Mention IA

---

## Étape 7 — Compte, abonnement, administration

- Stripe Checkout et Customer Portal, webhooks idempotents, TVA
- Plan gratuit, échec de paiement, résiliation, suppression de compte
- Notifications, toutes désactivées par défaut sauf alertes de sécurité
- **Interface d'administration pour le diététicien**, journal des libellés versionné
- Support : signalements, centre d'aide
- Mesure : Plausible ou Umami auto-hébergé, tableau de bord de conformité
- Documents légaux : CGU, confidentialité, registre des traitements, AIPD
- **Vérification d'âge** : 16 ans minimum, nutrition verrouillée sous 18 ans
- Environnements recette et production séparés, supervision, sauvegardes testées
- Tests de charge, versionnement de l'API
- Procédures de modération et ressources d'urgence validées par un professionnel

---

## Étape 8 — Bêta fermée

Vingt utilisateurs recrutés dans le réseau existant : salles de sport, kinésithérapeutes, coachs de la région liégeoise. Gratuit, en échange de retours.

Mesure unique : **combien ont enregistré vingt séances ou trente journées alimentaires après huit semaines ?**

En dessous de la moitié, ne pas ouvrir le paiement — corriger d'abord.

---

## Étape 9 — Ouverture

- Stripe, 20 €/mois
- Français uniquement au lancement
- Anglais et autres marchés seulement une fois la rétention prouvée

**Le marché mondial dès le premier jour multiplie le risque juridique sans revenu en face.** Chaque pays a ses propres règles sur le conseil nutritionnel, et les États-Unis en ont une par État.

---

## Ce qui n'est pas au programme

Application Android native, fonctions communautaires, objets connectés, notation des produits, plans de repas générés, marque blanche.

Chacun de ces points peut devenir pertinent. Aucun ne l'est avant cent abonnés payants.

**L'avatar 3D en fait partie.** Le radar délivre l'essentiel de la satisfaction pour une fraction du travail. Construire l'avatar avant d'avoir des utilisateurs, c'est deux mois investis sans aucun retour.
