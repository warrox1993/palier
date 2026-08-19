# 09 — Comptes, onboarding, abonnement

---

## 1. Authentification

**Pas de lien magique.** Deux voies, toutes deux gérées par Supabase Auth.

### Google OAuth
Bouton en premier. Récupère email, nom et photo. Aucun scope supplémentaire demandé — pas d'accès aux contacts, au calendrier ni à quoi que ce soit d'autre.

### Email et mot de passe
- Minimum 10 caractères, vérification contre une liste de mots de passe compromis (l'API HaveIBeenPwned expose un contrôle par préfixe de hachage, sans transmettre le mot de passe)
- Hachage bcrypt géré par Supabase — ne jamais réimplémenter
- **Vérification d'email obligatoire** avant accès aux fonctions nutrition
- Réinitialisation par jeton à usage unique, valable 1 heure
- Limitation : 5 tentatives par IP et par compte sur 15 minutes, puis verrouillage temporaire progressif
- 2FA par TOTP, optionnelle, disponible dès la V1 dans les réglages

### Récupération de compte
Perte d'accès à l'email : procédure manuelle, avec vérification d'éléments du compte (date de création, dernières séances). Jamais automatique — c'est le vecteur d'attaque classique.

### Session
Jeton d'accès de courte durée, jeton de rafraîchissement en cookie httpOnly, rotation à chaque usage. Déconnexion de tous les appareils disponible dans les réglages. Liste des sessions actives avec appareil et date.

### Fusion de comptes
Si un utilisateur crée un compte email puis se connecte via Google avec la même adresse, proposer la liaison des deux méthodes sur un seul compte plutôt que de créer un doublon.

---

## 2. Onboarding

Six écrans, aucun sautable sauf mention contraire. Objectif : que le premier écran de séance soit utilisable immédiatement après.

| # | Écran | Contenu |
|---|---|---|
| 1 | Bienvenue | Ce que fait l'app en une phrase. Mention « assisté par IA » |
| 2 | Profil | Sexe, date de naissance, taille, poids |
| 3 | Activité | Temps de marche quotidien, métier assis ou debout. Pas de catégories floues |
| 4 | Contraintes | « Une blessure, une douleur ou une limitation ? » Régions, sévérité. **Sautable** |
| 5 | Objectif | Prise de masse, maintien, perte. Avec la marge d'erreur du calcul affichée |
| 6 | Programme | Fréquence réaliste (3 à 7), puis proposition de structure |

**Écran 4 est le plus important du produit.** C'est lui qui active le différenciateur. Le formuler comme une aide, jamais comme un questionnaire médical : « pour qu'on évite de te proposer des exercices qui te font mal ».

**Consentement données de santé** présenté à l'écran 2, séparé des CGU, refusable. En cas de refus : accès à l'entraînement, pas à la nutrition.

À la fin : la première séance est prête, et l'utilisateur y est envoyé directement. Pas de tableau de bord vide.

### Reprise d'onboarding
Interruption possible à tout moment ; l'état est sauvegardé et repris à l'écran suivant.

---

## 3. Abonnement

### Structure

| Plan | Prix | Contenu |
|---|---|---|
| Essai | 14 jours | Tout, sans carte bancaire |
| Complet | 20 €/mois | Tout |
| Complet annuel | 200 €/an | Deux mois offerts |
| Après essai, sans paiement | Gratuit | **Lecture seule + saisie d'entraînement illimitée** |

**Le plan gratuit conserve la saisie d'entraînement et l'accès complet à l'historique.** Deux raisons : ne jamais prendre en otage les données de quelqu'un, et garder l'app installée — un utilisateur qui continue à noter ses séances revient. Ce qui bascule derrière le paiement : nutrition, assistant, analyse, progression RPG.

### Mécanique
- Stripe Checkout et Customer Portal — ne pas construire d'écran de facturation maison
- Webhooks pour les changements d'état, avec vérification de signature et idempotence
- TVA belge 21 %, Stripe Tax pour la gestion multi-pays
- Facture PDF automatique, accessible dans le compte

### Échec de paiement
Relance à J+1, J+3, J+7 par email. Bandeau discret dans l'app. Passage en gratuit à J+10, **sans suppression d'aucune donnée**. Reprise immédiate au paiement.

### Résiliation
En deux clics, sans appel ni email. Accès maintenu jusqu'à la fin de la période payée. Question facultative sur le motif — c'est la source d'information la plus utile du produit.

### Suppression de compte
Distincte de la résiliation. Confirmation par saisie de l'email. Export proposé avant. Effacement réel sous 30 jours, purge des sauvegardes comprise.

---

## 4. Notifications

Toutes désactivées par défaut, activées une par une. Notifications web push, plus email pour les sujets de compte.

| Notification | Déclencheur | Défaut |
|---|---|---|
| Rappel de séance | Jour habituel, heure choisie | Désactivé |
| Rappel de pesée | Matin, jours choisis | Désactivé |
| Dépassement de limite haute | À la saisie | **Activé** — c'est une alerte de sécurité |
| Fin de bloc | Semaine 5 ou 6 | Activé |
| Mensurations | 28 jours | Désactivé |
| Compte et paiement | Événement | Activé, non désactivable |

**Interdits :** aucune notification de série interrompue, aucun rappel culpabilisant, aucune relance après une période d'inactivité du type « tu nous manques ». Une personne qui arrête a ses raisons.

Plage horaire respectée : rien entre 21 h et 7 h, sauf réglage explicite.

---

## 5. Support

- **Signaler un problème** depuis n'importe quel écran : capture automatique du contexte technique, sans données de santé
- **Signaler un aliment erroné** : remontée vers OpenFoodFacts et correction locale immédiate
- **Signaler un exercice mal classé** : contre-indication manquante ou muscle mal attribué. Prioritaire — c'est un sujet de sécurité
- Centre d'aide : quelques pages, pas un wiki
- Délai de réponse annoncé et tenu

---

## 6. Interface d'administration

Nécessaire dès la V1 pour le diététicien partenaire.

- **Validation des règles** : références nutritionnelles, fourchettes, seuils, libellés. Chaque règle porte un état (brouillon, validé, date, validateur)
- **Journal des libellés** : toute formulation destinée à l'utilisateur passe par cette table, versionnée. Aucun texte nutritionnel en dur dans le code
- **Revue des sorties du modèle** : échantillon des réponses de l'assistant, avec signalement possible
- **File des signalements** : aliments, exercices, bugs
- **Tableau de bord de conformité** : nombre de rejets du filtre de sortie, escalades déclenchées, alertes TCA

Accès réservé, journalisé, avec 2FA obligatoire.
