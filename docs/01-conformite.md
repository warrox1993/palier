# 01 — Conformité

Ce document prime sur toute considération de rapidité ou d'élégance technique. Une fonctionnalité non conforme ne se livre pas.

---

## 1. Le cadre

Le fondateur est établi en Belgique. Trois régimes s'appliquent, quel que soit le lieu d'hébergement.

**Profession réglementée.** En Belgique, le titre de diététicien est protégé par l'Arrêté Royal du 19 février 1997. Délivrer des conseils nutritionnels ou des plans alimentaires personnalisés sans la qualification requise expose à une qualification d'exercice illégal de la profession.

**RGPD.** L'article 3.2 rend le règlement applicable dès lors qu'un service est offert à des personnes situées dans l'Union, indépendamment du lieu d'établissement ou d'hébergement. Poids, mensurations, blessures et compléments constituent des données de santé au sens de l'article 9 — catégorie particulière, consentement explicite requis, analyse d'impact probablement obligatoire.

**Politique d'usage Anthropic.** Les recommandations en matière de santé figurent parmi les cas d'usage à haut risque. Deux obligations en découlent : un professionnel humain qualifié doit valider les conseils générés, et l'utilisateur final doit être informé qu'une IA participe à la production du contenu, au minimum en début de session.

---

## 2. La ligne : informer sans prescrire

C'est la règle centrale du produit. Elle s'applique à chaque écran, chaque libellé et chaque phrase générée.

| Autorisé — comparaison à une référence                             | Interdit — prescription               |
| ------------------------------------------------------------------ | ------------------------------------- |
| « Apport en protéines : 150 g. Référence pour 73 kg : 117-160 g. » | « Mange 200 g de poulet ce soir. »    |
| « Zinc total : 42 mg. Limite haute EFSA : 25 mg. »                 | « Arrête ton complément de zinc. »    |
| « Vitamine D : couverte par l'alimentation sur 30 jours. »         | « Prends 2000 UI par jour en hiver. » |
| « Trois séances cette semaine, 9 séries sur les pectoraux. »       | « Tu dois faire plus de pectoraux. »  |

**Formule canonique : un chiffre, une référence, un écart. Jamais une action.**

### Vocabulaire

Interdit dans toute interface et toute sortie du modèle : _tu devrais_, _je te conseille_, _prends_, _arrête_, _il faut que_, _carence_, _déficit_, _prescription_, _traitement_, _diagnostic_.

À utiliser : _écart à la référence_, _en dessous de la fourchette_, _au-dessus de la limite haute_, _valeur observée_, _référence publiée_.

Implémenter un **filtre de sortie** qui rejette et régénère toute réponse du modèle contenant le vocabulaire interdit dans un contexte prescriptif. Ce filtre est testé unitairement.

---

## 3. Architecture à deux couches

> **Complété le 20/08/2026.** Rien n'a été retiré. La décision **D11** de `docs/decisions.md`
> nomme cette section : la séparation décrite ci-dessous n'est plus seulement une consigne, elle
> est vérifiée par le compilateur. Le paragraphe de fin de section le dit.

Le LLM n'est jamais seul aux commandes.

**Couche déterministe — code, hors de portée du modèle**

- Références et limites hautes EFSA en base de données
- Fourchettes de macronutriments validées par un diététicien agréé
- Contre-indications par contrainte déclarée
- Seuils d'alerte et règles d'escalade
- Tous les calculs : besoins, agrégations, écarts, progression

**Couche LLM — explication et dialogue**

- Reçoit des valeurs déjà calculées, ne calcule rien
- Ne peut pas écrire en base sur les objectifs ni sur les compléments
- Sorties structurées quand la réponse alimente une interface
- Passe par le filtre de sortie

Cette séparation est ce qui rend la validation par un professionnel gérable à l'échelle : le diététicien valide les règles une fois, pas chaque message.

**Ce qui rend la couche déterministe étanche.** Le projet `Palier.Domain`, qui porte les références, les fourchettes, les contre-indications et tous les calculs, ne référence aucun autre projet de la solution — **D11**. Un calcul nutritionnel ne _peut donc pas_ atteindre la base, le réseau ou le modèle : l'impossibilité est vérifiée à la compilation, pas par une règle de style qu'un oubli suffirait à contourner.

---

## 4. Obligations à implémenter

> **Ligne « Hébergement » réécrite le 20/08/2026.** Elle donnait « Supabase région Francfort ou
> Paris ». C'était faux : la décision **D15** du 19/08 a retenu **OVHcloud**, et les décisions
> **D9**, **D14** et **D17** ont retiré Supabase de la pile entière — voir `docs/decisions.md`.
> Le motif de conformité en sort renforcé, pas affaibli : l'exigence n'est plus seulement « une
> région dans l'UE », c'est **un fournisseur de droit européen**.

| Obligation                 | Implémentation                                                                                                                                 |
| -------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------- |
| Validation professionnelle | Un diététicien agréé valide règles et modèles. Sans lui, la partie nutrition ne sort pas                                                       |
| Transparence IA            | Mention « assisté par IA » visible en début de session et dans l'assistant                                                                     |
| Hébergement                | **OVHcloud**, fournisseur de droit européen : base PostgreSQL managée et backend conteneurisé, dans l'UE. Aucune donnée de santé hors UE — D15 |
| Consentement               | Explicite, séparé, pour les données de santé. Refus possible sans perte d'accès au reste                                                       |
| Portabilité                | Export complet en un clic, sans condition, dès la V1                                                                                           |
| Suppression                | Effacement réel du compte et des données, pas un drapeau en base                                                                               |
| Journalisation             | Aucune donnée de santé dans les logs applicatifs                                                                                               |

**Pourquoi le fournisseur compte autant que la région.** Un hébergeur soumis au droit des États-Unis reste atteignable par le _CLOUD Act_, qui permet à une autorité américaine de réclamer des données détenues par une entreprise américaine **quel que soit le pays où elles sont stockées**. Une région européenne opérée par un fournisseur américain ne referme donc pas la question ; c'est le droit dont relève l'opérateur qui la referme. Pour des données de santé au sens de l'article 9 et une clientèle belge, c'est l'argument de conformité le plus solide dont dispose ce projet — **D15**.

**Ce que ce choix coûte, et qu'il faut assumer.** OVHcloud ne propose aucune plateforme .NET clé en main : le backend est conteneurisé sur un VPS ou une instance Public Cloud, donc administré — certificats, mises à jour, supervision, sauvegardes. Ces tâches sont désormais des obligations de sécurité au sens de l'article 32, pas de la simple exploitation. De même, depuis **D17**, la sécurité de l'authentification est un traitement opéré par le responsable de traitement et non un service délégué : `docs/13-juridique.md` doit en tenir compte dans l'AIPD et le registre.

---

## 5. Garde-fous troubles du comportement alimentaire

Une application de comptage calorique attire mécaniquement des personnes en TCA. C'est une obligation éthique autant qu'une protection juridique, et c'est le reproche principal adressé aux acteurs établis.

À implémenter dès la V1 :

- **Plancher calorique bloquant.** Impossible de fixer un objectif sous 1200 kcal (femme) ou 1500 kcal (homme). Non contournable
- **Plancher protéique et lipidique** empêchant l'élimination d'un macronutriment
- **Détection de perte de poids rapide** : plus de 1 % du poids par semaine sur trois semaines → message d'orientation
- **Détection de restriction sévère** : apports très en dessous du métabolisme de base répétés
- **Aucune classification d'aliments** en bons ou mauvais, aucun score, aucun système de points, aucune notion d'aliment « interdit »
- **Mode micronutriments seuls** : possibilité de masquer complètement calories et poids
- **Aucune série de jours consécutifs**, aucun badge, aucune récompense liée à la restriction
- Message d'orientation vers un professionnel, **jamais** de renforcement de la restriction

Ces règles sont testées unitairement et ne peuvent pas être désactivées par configuration.

### Arbitrage avec le système de progression

Le produit comporte un système de progression de type jeu de rôle (`docs/10-progression.md`). Cet arbitrage a été tranché ainsi :

**Autorisé** — progression calculée sur des données objectives de performance, représentation visuelle de l'évolution, déblocage d'éléments cosmétiques.

**Interdit, sans exception** — avatar reflétant la morphologie corporelle réelle, séries de jours consécutifs, régression après une interruption, gain lié à une restriction calorique, comparaison entre utilisateurs, état négatif ou culpabilisant, perte de points acquis.

En cas de doute sur une mécanique de jeu : elle est exclue.

---

## 6. Ce qui déclenche une escalade humaine

L'assistant doit interrompre le fil normal et orienter vers un professionnel de santé dans les cas suivants :

- Douleur articulaire persistante ou aggravée sur plusieurs séances
- Symptômes neurologiques : fourmillements, engourdissement, perte de force
- Douleur thoracique, vertiges à l'effort, perte de poids inexpliquée
- Signaux de trouble du comportement alimentaire
- Toute question relevant du diagnostic ou du traitement

Dans ces situations : message court, orientation claire, aucune tentative d'interprétation médicale.

---

## 7. Avant la première ligne de code

1. **Consultation d'un avocat en droit de la santé numérique.** Budget 300 à 500 €. Valider le positionnement, les CGU, la politique de confidentialité
2. **Accord écrit avec un diététicien agréé.** Périmètre de validation, responsabilité, rémunération ou participation
3. **Statut d'indépendant complémentaire** en Belgique : numéro d'entreprise, TVA, cotisations sociales trimestrielles. Compter 200 à 300 € de frais de démarrage

Ces trois points conditionnent le lancement de la partie nutrition. La partie entraînement peut avancer en parallèle.
