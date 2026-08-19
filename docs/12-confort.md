# 12 — Confort d'usage

Les fonctions qui font la différence entre une application utilisée trois semaines et une application utilisée un an. Aucune n'est optionnelle dans un produit fini.

---

## 1. Répétition et duplication

C'est le premier confort que réclament les utilisateurs de toute application de suivi.

### Nutrition
- **Dupliquer une journée** : reprendre l'intégralité des saisies d'un jour choisi
- **Repas enregistrés** : nommer un ensemble d'aliments (« mon petit-déjeuner »), le réutiliser en un geste
- **Recettes** : composition, nombre de portions, valeurs calculées par portion, réutilisable
- **Suggestions contextuelles** : les aliments les plus fréquents de l'utilisateur à ce repas, en tête de recherche
- **Prises de compléments récurrentes** : déclarées une fois, ajoutées automatiquement chaque jour, avec possibilité de sauter une journée

### Hydratation
- **Ajout en un appui** depuis n'importe quel écran, sans ouvrir la section nutrition
- Contenants personnalisés, réordonnables
- Correction par appui long, annulation immédiate

### Entraînement
- **Répéter la dernière séance** identique
- **Copier les charges** de la séance précédente en un geste
- **Séance libre** hors programme, pour dépanner
- **Correction rétroactive** : modifier une séance passée, avec recalcul des dérivés
- **Marquer un jour de repos** — ce n'est pas une absence, c'est une donnée

---

## 2. Import et export

- Export JSON complet, en un clic, sans condition, dès la V1
- Import du même format, avec prévisualisation et détection de doublons
- Export CSV des séries et des journées alimentaires
- **Import depuis Hevy et Strong** : ce sont les applications que quitteront les nouveaux utilisateurs. Sans cette passerelle, ils ne migrent pas
- Export PDF d'un bilan sur une période — utile à donner à un kiné ou un médecin

---

## 3. Recherche et navigation

- Recherche globale : exercice, aliment, séance, date
- Historique des recherches récentes
- Favoris sur aliments et exercices
- Navigation par date avec calendrier, retour à aujourd'hui toujours accessible
- Vue hebdomadaire condensée

---

## 4. Personnalisation

- Unités : kilogrammes ou livres, centimètres ou pouces
- Premier jour de la semaine
- Ordre des repas, renommage des repas
- Masquage des calories (mode micronutriments seuls — garde-fou TCA)
- Masquage du poids corporel
- Durées de repos par défaut
- Réorganisation des exercices dans une séance

---

## 5. États d'interface

Chaque écran traite explicitement quatre états. Un écran qui n'en traite qu'un n'est pas terminé.

| État | Traitement |
|---|---|
| Chargement | Squelette de contenu, jamais un tourniquet plein écran |
| Vide | Invitation à agir, avec ce que ça apportera |
| Erreur | Ce qui s'est passé, ce qu'on peut faire, un bouton pour réessayer |
| Contenu partiel | Afficher ce qui est disponible, signaler ce qui manque |

Exemple d'état vide correct : « Aucune séance enregistrée. La première sert de point de référence à toutes les suivantes. » Pas : « Oups, rien ici ! »

---

## 6. Performance

| Cible | Valeur |
|---|---|
| Premier affichage utile | < 1,5 s en 4G |
| Interaction à réponse visible | < 100 ms |
| Saisie d'une série enregistrée | Instantané, écriture locale |
| Poids du bundle initial | < 200 ko compressé |
| Lighthouse | > 90 sur toutes les catégories |

Découpage du code par route. Le module 3D de l'avatar est chargé à la demande, jamais dans le bundle initial.

---

## 7. Sécurité applicative

- En-têtes : CSP stricte, HSTS, X-Content-Type-Options, Referrer-Policy
- Limitation de débit sur les points sensibles : authentification, appels au modèle, téléversement d'images
- Validation des entrées côté serveur, systématiquement — jamais uniquement côté client
- Téléversements : type MIME vérifié, taille limitée, métadonnées EXIF supprimées (les photos contiennent des coordonnées GPS)
- Photos d'étiquettes stockées dans un bucket privé, accès par URL signée à durée limitée
- Dépendances auditées en CI, mise à jour des vulnérabilités critiques sous 7 jours
- Sauvegardes chiffrées, restauration testée — une sauvegarde jamais restaurée n'est pas une sauvegarde

---

## 8. Documents obligatoires

Rédigés avant l'ouverture au public, accessibles depuis l'application :

- Conditions générales d'utilisation
- Politique de confidentialité, mentionnant explicitement le traitement des données de santé et les sous-traitants
- Mentions légales avec numéro d'entreprise et TVA
- Registre des traitements (RGPD article 30)
- Analyse d'impact (AIPD)
- Page « comment fonctionne l'assistant » : ce qu'il fait, ce qu'il ne fait pas, quels modèles, quelles limites
