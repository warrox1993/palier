# 17 — Licences des sources de données

**Point critique.** Les bases alimentaires ne sont pas librement réutilisables sans conditions. Une erreur ici contamine juridiquement tout le produit.

---

## 1. Open Food Facts — licence ODbL

La base est publiée sous **Open Database License**, les contenus individuels sous *Database Contents License*, et les photos de produits sous **Creative Commons Attribution ShareAlike**.

L'usage commercial est autorisé, à conditions. Elles sont au nombre de trois :

| Obligation | Contenu |
|---|---|
| **Attribution** | Toute utilisation publique de la base, ou d'une œuvre produite à partir d'elle, doit être attribuée, avec mention claire de la licence |
| **Share-alike** | Toute version **adaptée** de la base utilisée publiquement doit être offerte sous ODbL |
| **Keep open** | Une base redistribuée ne peut être verrouillée technologiquement |

Et deux conditions d'API : **User-Agent personnalisé obligatoire** identifiant l'application, et respect des limites de débit par point d'entrée.

### La distinction qui décide de tout

L'ODbL sépare deux régimes :

**Œuvre produite** — une application qui interroge la base et **affiche** des résultats. L'attribution suffit. L'application n'a pas à devenir open source.

**Base dérivée** — une base modifiée, enrichie ou **combinée avec d'autres sources**, utilisée publiquement. Le share-alike s'applique : elle doit être publiée sous ODbL.

Open Food Facts est explicite sur le point de la combinaison : si vous combinez leurs données avec d'autres bases, la base résultante doit être publiée en open data, et vous ne pouvez combiner qu'avec des sources autorisant cette redistribution.

### Conséquence sur l'architecture

**Ne jamais fusionner les sources dans une table unifiée enrichie.** C'est exactement ce qui créerait une base dérivée.

Architecture à respecter :

- La table `foods` conserve `source` et `source_ref` sur **chaque** enregistrement — déjà prévu dans `03-donnees.md`
- Les enregistrements Open Food Facts restent **tels quels**, sans enrichissement, sans correction, sans fusion avec CIQUAL
- Un aliment CIQUAL et un aliment Open Food Facts sont deux lignes distinctes, jamais réconciliées
- Les aliments créés par l'utilisateur sont marqués `source = 'user'` et n'héritent d'aucune donnée Open Food Facts
- Le cache local sert la performance, pas la construction d'une base parallèle
- **Aucun export de la base agrégée** n'est proposé aux utilisateurs

L'export personnel d'un utilisateur (ses propres saisies) reste évidemment possible : ce sont ses données, pas la base.

### Attribution à implémenter

- Mention visible sur chaque fiche produit issue d'Open Food Facts, avec lien vers la fiche d'origine
- Page « sources de données » listant chaque base, sa licence et son lien
- Mention dans les CGU et la politique de confidentialité

Les contributeurs acceptent d'être crédités par un lien vers le produit auquel ils ont contribué. Ce lien est donc dû.

### Photos

Les images produits sont sous **CC BY-SA** et peuvent contenir des éléments graphiques protégés par le droit d'auteur — logos, marques. Recommandation : **ne pas afficher les photos Open Food Facts.** Le gain visuel ne justifie pas le risque, et le design du produit n'en a pas besoin.

---

## 2. CIQUAL — ANSES

Table de composition nutritionnelle française, la plus complète en micronutriments pour les aliments bruts.

**À vérifier avant intégration :** les conditions exactes de réutilisation publiées par l'ANSES au moment du développement, notamment la licence applicable et les modalités d'attribution. Ne pas présumer d'une licence ouverte sans l'avoir lue.

Si la licence est de type Licence Ouverte Etalab, elle est compatible avec un usage commercial moyennant attribution, **sans clause de partage à l'identique** — ce qui la rend plus simple à utiliser qu'ODbL, mais rend d'autant plus importante la non-fusion avec Open Food Facts.

---

## 3. NUBEL — Belgique

Table de composition belge. **Modèle de licence à vérifier** : NUBEL a historiquement fonctionné sur un modèle payant ou sous convention, contrairement aux sources ouvertes.

À traiter comme une option, pas comme un acquis. CIQUAL couvre l'essentiel des aliments bruts consommés en Belgique.

---

## 4. EFSA — références nutritionnelles

Les *Dietary Reference Values* et les *Tolerable Upper Intake Levels* sont des publications scientifiques officielles de l'Autorité européenne de sécurité des aliments.

Vérifier les conditions de réutilisation des publications EFSA et citer systématiquement la source et l'année dans la table `nutrient_refs` — le champ est déjà prévu. L'affichage doit indiquer l'origine de chaque référence.

---

## 5. Règles opérationnelles

| Règle | Motif |
|---|---|
| User-Agent identifiant l'application sur tous les appels Open Food Facts | Exigence de l'API |
| Respect strict des limites de débit, avec mise en cache raisonnable | Exigence de l'API |
| Chaque enregistrement porte sa source et sa licence | Traçabilité et conformité |
| Aucune fusion inter-sources | Évite la base dérivée |
| Aucun export de la base agrégée | Évite la redistribution |
| Pas d'affichage des photos Open Food Facts | Risque de droits tiers |
| Page « sources » publique et à jour | Attribution |
| Signalement d'une donnée erronée remonté à Open Food Facts | Réciprocité, et c'est apprécié |

Open Food Facts indique apprécier — sans l'exiger — d'être informé des réutilisations. Le faire est peu coûteux et construit une relation utile avec un projet dont le produit dépend.

---

## 6. Point à valider avec l'avocat

La question à poser explicitement, parce qu'elle conditionne l'architecture :

> Une application qui interroge Open Food Facts, met les réponses en cache sans les modifier, et les affiche à côté de données issues d'autres sources maintenues dans des tables séparées, constitue-t-elle une œuvre produite ou une base dérivée au sens de l'ODbL ?

La réponse attendue est « œuvre produite », mais elle doit être écrite par quelqu'un qui engage sa responsabilité, pas présumée.
