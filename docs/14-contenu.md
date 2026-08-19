# 14 — Contenu et exploitation

Le contenu est le poste le plus sous-estimé du projet. Il ne se génère pas en une nuit et conditionne la crédibilité du produit.

---

## 1. Catalogue d'exercices

**Cible : 250 à 400 exercices.** En dessous de 150, le produit paraît vide face à la concurrence.

Chaque entrée porte : nom français et anglais, équipement, muscles primaires et secondaires, caractère unilatéral, incrément de charge par défaut, régions contre-indiquées, consignes d'exécution, erreurs fréquentes, variantes liées.

### Illustrations — le vrai coût

Aucune application de musculation n'est crédible sans représentation visuelle des mouvements. Quatre options :

| Option | Coût | Délai | Remarque |
|---|---|---|---|
| Banque d'illustrations sous licence | 500-3 000 € | Immédiat | Vérifier la licence commerciale et la couverture |
| Illustrations sur mesure | 15-40 €/exercice | 2-3 mois | Cohérence visuelle totale |
| Animations 3D sous licence | 1 000-5 000 € | Immédiat | Plus lisible, plus lourd |
| Schémas vectoriels simples maison | Temps interne | 3-4 semaines | Cohérent avec la direction sobre du produit |

**Recommandation : schémas vectoriels au trait, réalisés en interne**, cohérents avec `02-design.md` — deux positions par mouvement, départ et fin, plus une flèche de trajectoire. Sobre, léger, distinctif, et sans dépendance à une licence.

Ne jamais utiliser d'images trouvées sur internet. Le contentieux en droit d'auteur sur les photos de fitness est fréquent et coûteux.

### Priorisation
Commencer par les 60 mouvements couvrant 90 % des programmes. Le reste s'ajoute progressivement.

---

## 2. Programmes modèles

Écrits par le fondateur, **relus par un kinésithérapeute** pour la partie contraintes. Ce n'est pas la même validation que celle du diététicien pour la nutrition, et elle est tout aussi nécessaire.

| Programme | Fréquence | Contrainte visée |
|---|---|---|
| Reprise | 3 | Aucune, retour après arrêt |
| Reprise cervicale | 3 | Cervicale |
| Reprise lombaire | 3 | Lombaire |
| Épaule ménagée | 3-4 | Épaule |
| Genou ménagé | 3-4 | Genou |
| Full body | 3 | Aucune |
| Upper / Lower | 4 | Aucune |
| PPL | 5-6 | Aucune |
| Split | 6 | Aucune |

Chaque programme porte une note expliquant ses choix. Un utilisateur qui comprend pourquoi un exercice est absent l'accepte ; sinon il le rajoute et se blesse.

---

## 3. Contenu éducatif

Pages courtes, accessibles depuis les écrans concernés :

- Comment évaluer son RIR
- Pourquoi la surcharge progressive prime sur le choix des exercices
- À quoi sert un allègement
- Ce que mesure et ne mesure pas une estimation de 1RM
- Pourquoi la moyenne hebdomadaire du poids plutôt que la pesée du jour
- Ce qu'est une limite haute EFSA
- Pourquoi l'application ne recommande jamais de complément
- Comment fonctionne l'assistant, et ses limites

**Ton : informatif, jamais prescriptif.** Le vocabulaire de `01-conformite.md` s'applique.

---

## 4. Emails transactionnels

| Email | Déclencheur |
|---|---|
| Vérification d'adresse | Inscription |
| Bienvenue | Vérification faite |
| Réinitialisation de mot de passe | Demande |
| Fin d'essai approchant | J-3 |
| Échec de paiement | J+1, J+3, J+7 |
| Passage en gratuit | J+10 |
| Confirmation de résiliation | Demande |
| Export prêt | Demande |
| Récapitulatif hebdomadaire | Hebdomadaire, désactivable |

Français et anglais. Ton sobre, aucune relance culpabilisante, lien de désabonnement sur tout ce qui n'est pas transactionnel. Fournisseur en UE (Brevo, Postmark région UE) avec DPA.

---

## 5. Environnements et livraison

| Environnement | Usage |
|---|---|
| Local | Développement, Supabase local |
| Recette | Projet Supabase distinct, données de test |
| Production | Données réelles, accès restreint |

**Aucune migration n'atteint la production sans être passée en recette.** Les migrations sont versionnées, réversibles quand c'est possible, et testées sur une copie de la structure de production.

Livraison continue depuis la branche principale, avec possibilité de retour arrière immédiat.

---

## 6. Supervision

| Sujet | Outil | Alerte |
|---|---|---|
| Disponibilité | Sonde externe | Indisponibilité > 2 min |
| Erreurs applicatives | Sentry UE, masquage strict | Nouvelle erreur, pic |
| Base de données | Supabase | Connexions, lenteurs, quota |
| Coût des modèles | Table interne | Dépassement du seuil quotidien |
| Files de synchronisation | Interne | Opération en échec depuis > 24 h |
| Conformité | Interne | Pic de rejets du filtre, escalades |

Alertes envoyées sur un canal unique, avec des seuils réalistes — une alerte qui se déclenche tous les jours cesse d'être lue.

---

## 7. Sauvegardes et reprise

- Sauvegardes automatiques quotidiennes, rétention 30 jours
- **Restauration testée trimestriellement** sur l'environnement de recette. Une sauvegarde jamais restaurée n'est pas une sauvegarde
- Objectif de perte de données maximale : 24 h. Objectif de remise en service : 4 h
- Procédure de reprise écrite, exécutable par quelqu'un d'autre que le fondateur

**Continuité :** si le fondateur devient indisponible, un document scellé contient accès, procédures et contacts. Sur un produit payant traitant des données de santé, ce n'est pas une précaution excessive.

---

## 8. Charge et performance

Tests de charge avant l'ouverture : 100 utilisateurs simultanés, pic de saisie en soirée. Vérifier les index, les requêtes lentes, la limitation de débit sur les appels aux modèles.

Versionnement de l'API interne dès le départ (`/v1/`), même en solo. Le jour où une application native arrive, une API non versionnée devient un problème insoluble.
