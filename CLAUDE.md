# CLAUDE.md — Règles de travail permanentes

Ce fichier est lu au démarrage de chaque session. Il prime sur toute autre instruction, sauf demande explicite et contraire de l'utilisateur dans la conversation.

---

## 1. Méthode de travail — non négociable

### Lire d'abord

`docs/16-projet.md` fixe l'arborescence, les conventions de nommage, les variables d'environnement et le glossaire. Ne rien inventer qui y figure déjà.

### Superpowers est obligatoire

Ce projet se construit avec le framework **Superpowers** (Jesse Vincent / Prime Radiant).

```
/plugin marketplace add obra/superpowers-marketplace
/plugin install superpowers@superpowers-marketplace
```

Vérifier au démarrage de chaque session que les commandes sont disponibles via `/help`. **Leur nom exact varie selon la version installée** — la version courante expose `brainstorming`, `writing-plans` et `executing-plans`. Ce qui compte est la fonction, pas le libellé :

| Fonction                       | Commande (à confirmer via `/help`) |
| ------------------------------ | ---------------------------------- |
| Clarification avant conception | `brainstorming`                    |
| Plan d'implémentation          | `writing-plans`                    |
| Exécution par lots             | `executing-plans`                  |

Si les noms diffèrent encore, utiliser ceux que `/help` affiche et le signaler.

**Séquence imposée pour toute fonctionnalité :** brainstorm → plan validé par l'utilisateur → exécution. Jamais de code avant qu'un plan ait été approuvé.

Ajouter `EnterPlanMode` à la deny list des permissions : le mode Plan natif de Claude Code entre en conflit avec les workflows Superpowers.

### Agents : utiliser l'existant, ne rien créer

**Interdiction stricte de créer un agent, une skill ou un sous-agent personnalisé.**

Utiliser exclusivement les agents et skills déjà publiés sur GitHub — Superpowers et les marketplaces existantes. Si un besoin semble appeler un agent qui n'existe pas :

1. Chercher d'abord dans les marketplaces publiques
2. Si rien ne convient, le signaler à l'utilisateur et attendre sa décision
3. Ne jamais créer l'agent de sa propre initiative

Cette règle existe parce qu'un agent maison n'est ni testé, ni maintenu, ni partagé — c'est de la dette technique déguisée en outillage.

### Produit fini, jamais prototype

Chaque tâche livrée est finie : testée, accessible, traduite, avec ses états de chargement, d'erreur et vide. La définition de terminé de `docs/08-workflow.md` fait foi, et ses **douze** points sont cochés avant tout passage à la suite.

On séquence la livraison, jamais la qualité. « On finira plus tard » n'existe pas dans ce projet.

### Agentique, pas vibecoding

| Interdit                                   | Attendu                                           |
| ------------------------------------------ | ------------------------------------------------- |
| Écrire du code dès la première réponse     | Comprendre, spécifier, planifier, puis exécuter   |
| Enchaîner les fonctionnalités sans tests   | TDD — RED, GREEN, REFACTOR                        |
| « Ça devrait marcher »                     | Vérification effective avant de déclarer terminé  |
| Corriger un symptôme                       | `systematic-debugging` — cause racine en 4 phases |
| Avancer seul sur une décision structurante | Poser la question à l'utilisateur                 |

Les skills Superpowers `test-driven-development`, `systematic-debugging`, `verification-before-completion` et `requesting-code-review` s'activent automatiquement. Ne pas les contourner.

**Tout code écrit avant son test est supprimé et réécrit.**

En février 2026, Karpathy a renommé le « vibe coding » en **agentic engineering** : la boucle recherche → plan → exécution → revue → livraison, avec tests et revue à chaque étape. C'est cette méthode qui s'applique ici, et `docs/08-workflow.md` la détaille.

### Harnais avant produit

Aucune fonctionnalité n'est écrite avant que TypeScript strict, **Oxlint**, Prettier, Vitest, Playwright, les hooks pre-commit et pre-push, la CI et axe-core ne soient en place.

> **Oxlint, et non ESLint.** `typescript-eslint` déclare `typescript >=4.8.4 <6.1.0` en dépendance de pair : il est incompatible avec TypeScript 7, que ce projet utilise. Oxlint couvre les mêmes règles via `oxlint-tsgolint`, construit sur TypeScript 7. Ce document disait ESLint jusqu'au 20/08/2026.

Des boucles de rétroaction rapides conditionnent tout le reste : si le build est lent, l'agent tourne en rond.

### Contexte

Toute investigation dépassant trois lectures de fichiers part en sous-agent, avec une question précise, un périmètre borné et un format de rapport attendu. Le contexte principal reste propre — vingt lectures suivies d'une tentative de planification avec ce bruit chargé est l'erreur la plus coûteuse du travail agentique.

### Rythme

- Un lot de travail = une fonctionnalité vérifiable de bout en bout
- Revue de code entre chaque tâche, les problèmes critiques bloquent la suite
- Commit à chaque tâche terminée, message explicite
- Branche par fonctionnalité, `finishing-a-development-branch` en fin de parcours

---

## 2. Le projet en une phrase

Application **web** de suivi de musculation et d'apports nutritionnels, avec un assistant conversationnel, dont la promesse est **d'éviter les excès et les blessures**, pas d'optimiser la performance.

Documents de référence, à lire avant de commencer :

| Fichier                      | Contenu                                                                   |
| ---------------------------- | ------------------------------------------------------------------------- |
| `docs/00-produit.md`         | Cible, promesse, périmètre V1, ce qui est hors périmètre                  |
| `docs/01-conformite.md`      | Cadre juridique, ligne informer/prescrire, garde-fous                     |
| `docs/02-design.md`          | Direction artistique, jetons, structure de l'écran de séance              |
| `docs/03-donnees.md`         | Schéma PostgreSQL, RLS                                                    |
| `docs/04-nutrition.md`       | Formules, références EFSA, logique de calcul                              |
| `docs/05-entrainement.md`    | Volume, progression, adaptation par contrainte                            |
| `docs/06-ia.md`              | Architecture LLM, mix de modèles, prompts, coûts                          |
| `docs/07-roadmap.md`         | Séquence de construction                                                  |
| `docs/08-workflow.md`        | **Ingénierie agentique : boucle, harnais, vérification**                  |
| `docs/09-comptes.md`         | Auth, onboarding, abonnement, notifications, support, admin               |
| `docs/10-progression.md`     | Radar, états, avatar — et leurs garde-fous                                |
| `docs/11-qualite.md`         | Résilience, accessibilité, i18n, mesure                                   |
| `docs/12-confort.md`         | Duplication, import/export, états d'interface, performance, sécurité      |
| `docs/13-juridique.md`       | Mineurs, transferts vers les modèles, structure, assurance, partenariats  |
| `docs/14-contenu.md`         | Catalogue d'exercices, programmes, emails, environnements, supervision    |
| `docs/15-marque.md`          | Nom, dépôt, classes, identité                                             |
| `docs/16-projet.md`          | **Arborescence, conventions, variables d'environnement, seed, glossaire** |
| `docs/17-donnees-sources.md` | **Licences ODbL, attribution, interdiction de fusionner les sources**     |

---

## 3. Stack

> Cette section a été réécrite le 20/08/2026 pour appliquer les décisions **D9, D10, D11, D14, D15, D16 et D17** de `docs/decisions.md`, tranchées le 19/08. Elle décrivait encore une pile Supabase + Vercel abandonnée. Le journal des décisions fait foi ; en cas d'écart, c'est lui qu'il faut lire.

| Couche          | Choix                                                            | Contrainte                                                                                                                                  |
| --------------- | ---------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------- |
| Front           | React + Vite + TypeScript, **CSS écrit à la main**               | **Web d'abord**, conçu pour petits écrans. Pas de Tailwind ni d'aucune bibliothèque de composants — D42. PWA installable **reportée** — D45 |
| Backend         | **ASP.NET Core (.NET 10), Clean Architecture en quatre projets** | `Palier.Domain` ne référence aucun autre projet — D11                                                                                       |
| Base de données | PostgreSQL, via **EF Core**                                      | Hébergée en Europe. Les vues et contraintes passent par `migrationBuilder.Sql` — D14                                                        |
| Auth            | **ASP.NET Identity** : Google OAuth + email/mot de passe         | Pas de lien magique. Les sept exigences de `docs/09-comptes.md` § 1 sont à implémenter — D17                                                |
| Répartition     | `Mediator.SourceGenerator` (MIT)                                 | **MediatR est interdit** : RPL-1.5, incompatible avec un service commercial — D12                                                           |
| État serveur    | TanStack Query                                                   | Mutations optimistes                                                                                                                        |
| Cache local     | IndexedDB (Dexie)                                                | **Lot 7, pas avant** — D45. Écriture immédiate, réseau en arrière-plan, file de retry persistée — `docs/11-qualite.md` § 1                  |
| Hébergement     | **OVHcloud**                                                     | Fournisseur européen, hors portée du Cloud Act. Backend conteneurisé sur VPS ou Public Cloud — D15                                          |
| Domaine         | **Front et API sous le même domaine**                            | Le cookie de rafraîchissement reste de même site — D16                                                                                      |
| Paiement        | Stripe                                                           | À partir de la V2 seulement                                                                                                                 |
| LLM             | API Claude + Gemini                                              | Abstraction multi-fournisseur obligatoire, voir `docs/06-ia.md`                                                                             |
| i18n            | i18next                                                          | Français et anglais dès la première ligne                                                                                                   |
| Tests           | Vitest + Playwright + axe-core, xUnit côté backend               | En place avant la première fonctionnalité                                                                                                   |
| Mesure          | Plausible ou Umami auto-hébergé                                  | UE, sans donnée de santé                                                                                                                    |

**Ce que le changement d'architecture a déplacé.** La sécurité ne repose plus sur les politiques RLS du moteur PostgreSQL mais sur un point de contrôle applicatif : l'API est le seul chemin vers les données. RLS reste activé en défense en profondeur, il n'est plus la ligne unique. L'authentification devient un traitement que vous opérez, non un service délégué — `docs/13-juridique.md` doit en tenir compte dans l'AIPD et le registre.

**Portabilité :** le backend est conteneurisé, l'état vit dans PostgreSQL. Aucune dépendance à un service propriétaire d'hébergeur. Un changement de fournisseur doit rester possible en une journée.

---

## 4. Règles de code

- TypeScript strict, `any` interdit sauf justification en commentaire
- Aucune clé, aucun secret dans le dépôt. Variables d'environnement uniquement
- Aucune donnée de santé dans les logs
- Les calculs nutritionnels et de progression vivent dans des modules purs et testés, jamais dans les composants
- Le LLM ne calcule jamais : il reçoit des valeurs déjà calculées (voir `docs/01-conformite.md`)
- Messages de commit en français, à l'impératif
- Aucune chaîne de caractères en dur : tout passe par i18next, y compris erreurs et états vides
- Aucun libellé nutritionnel en dur : ils vivent en base, versionnés et validés (voir `docs/09-comptes.md`)

### KISS et DRY, dans cet ordre

**KISS.** La solution la plus simple qui répond au besoin, pas la plus élégante ni la plus extensible. Un besoin futur supposé ne justifie aucune abstraction aujourd'hui.

**DRY, au sens strict : une connaissance, un endroit.** Pas « deux blocs se ressemblent, donc on factorise ». Deux choses qui se ressemblent aujourd'hui et divergeront demain doivent rester séparées — factoriser prématurément coûte plus cher que dupliquer. C'est pourquoi D6 laisse `jscpd` en avertissement et non en blocage.

**Quand les deux se contredisent, KISS gagne.** Une abstraction qui évite trois lignes de répétition mais oblige à comprendre deux fichiers pour lire un traitement est un mauvais échange.

**Le critère, une seule question à se poser :** _si cette règle change, les deux endroits doivent-ils changer ensemble ?_

- **Oui → factoriser, sans hésiter et sans limite.** Un calcul de conformité, un seuil de sécurité, une formule nutritionnelle, un format de date : une seule implémentation, testée une fois. Deux copies d'une même connaissance, c'est la garantie qu'une des deux sera corrigée sans l'autre — et sur des valeurs de santé, c'est un défaut qui se voit chez l'utilisateur.
- **Non → laisser séparé, même si le code se ressemble.** Deux validations qui vérifient toutes deux « une valeur positive » — l'une pour un poids corporel, l'autre pour une charge soulevée — ne portent pas la même connaissance. Les factoriser crée un couplage entre deux règles indépendantes : le jour où l'une accepte les décimales et pas l'autre, l'abstraction se paie en contorsions.

La ressemblance du code n'est pas le critère. **Le critère est le destin commun.**

### Ne jamais faire confiance à l'utilisateur — la règle de base

**Toute donnée qui vient de l'extérieur est hostile jusqu'à preuve du contraire.** Cela vaut pour un formulaire, une URL, un en-tête HTTP, un fichier importé, une réponse d'API tierce, un jeton, et la sortie d'un modèle de langage. Aucune exception, y compris pour l'utilisateur authentifié : un compte volé est un utilisateur authentifié.

Ce que cela impose, partout et sans discussion :

- **La validation se fait au serveur.** Celle du navigateur est un confort d'affichage, jamais une protection : elle se contourne avec les outils de développement, un client HTTP, ou un navigateur modifié. Une règle qui n'existe qu'au front n'existe pas.
- **Valider à la frontière, une seule fois, puis faire confiance au type.** L'entrée devient un type du domaine — `Charge`, `Repetitions`, `AdresseEmail` — qui ne peut pas être construit invalide. Le reste du code n'a plus à se défendre.
- **Autoriser explicitement, jamais interdire.** Une liste blanche de ce qui est permis, pas une liste noire de ce qui est refusé : on n'énumère jamais complètement ce qu'un attaquant peut inventer.
- **Aucune requête construite par concaténation.** Paramètres liés systématiquement, y compris pour un identifiant qui « vient forcément de chez nous ».
- **L'identité ne se lit jamais dans la requête.** Ni dans le corps, ni dans un paramètre, ni dans un en-tête que le client contrôle. Elle vient du jeton vérifié côté serveur — c'est le fondement de D36, où l'identité posée au moteur PostgreSQL vient du contexte authentifié et de nulle part ailleurs.
- **L'autorisation se vérifie à chaque accès**, pas seulement à l'affichage du bouton. Cacher une action dans l'interface ne la rend pas indisponible.
- **La sortie d'un modèle de langage est une entrée utilisateur.** Elle est filtrée avant d'atteindre l'écran (`docs/01-conformite.md`), et elle ne décide jamais d'un calcul.
- **Ne jamais renvoyer à l'utilisateur ce qui l'aiderait à attaquer** : trace d'exception, requête SQL, chemin de fichier, version d'un composant. Le journal détaillé côté serveur, le message générique côté client — et **aucune donnée de santé dans les journaux**.

### Sécurité — c'est une priorité, pas une étape

La sécurité se traite à chaque ligne, pas à la fin. Elle prime sur la vitesse de livraison, et le porteur du projet accepte explicitement le coût : `npm run verify` prend cinq minutes et c'est assumé.

**Le code lui-même, pas seulement la chaîne d'outils.** Un pipeline exemplaire ne protège pas d'une requête non paramétrée, d'une autorisation oubliée ou d'une donnée de santé écrite dans un journal. Le harnais attrape ce qui est mécanique ; le reste demande de la vigilance à l'écriture.

### Dépendances — la doctrine

**npm est un vecteur d'attaque actif.** Des paquets sont compromis chaque semaine, et le vecteur habituel est le script `postinstall`, qui s'exécute avant que quiconque ait lu le code.

1. **Éviter d'ajouter une dépendance.** Une dépendance directe en amène des dizaines de transitives — 4 déclarées côté front en amènent 130. C'est l'arbre entier qui est la surface d'attaque, pas ce qu'on déclare.
2. **Mais ne JAMAIS remplacer une dépendance mature par du code maison au nom de la sécurité.** Un parseur YAML écrit à la main serait buggé, donc moins sûr que le paquet qu'il remplace. La règle est : _retirer ce qui ne sert pas, verrouiller le reste._
3. **`--ignore-scripts` à l'installation.** Le paquet est téléchargé, son code reste inerte tant que rien ne l'importe. C'est la différence entre subir un paquet malveillant et l'exécuter avec ses droits.
4. **Vérifier la provenance** : `npm audit signatures` contrôle les attestations cryptographiques des paquets construits en CI publique.
5. **Un délai avant adoption.** Dependabot a un `cooldown` de 3 à 30 jours selon la gravité — un paquet compromis est généralement retiré en quelques heures.
6. **Toute nouvelle dépendance directe demande une décision datée** dans `docs/decisions.md`, avec son motif, ce qu'elle remplace, et ce qui la rouvrirait.

Ce qu'on ne réécrit pas, parce que le coût dépasserait le risque : Vite, TypeScript, Oxlint, Prettier, Vitest, Playwright, EF Core, Npgsql.

### Code mort — aucune tolérance, et la détection est mécanique

Le code mort n'est pas une question de propreté. C'est un mensonge sur ce que le programme fait : il fait croire qu'un chemin existe, il se fait lire en revue, il se fait maintenir, et il masque le code vivant.

**La règle : on ne supprime pas le code mort, on rend impossible de l'écrire.** Trois détecteurs, chacun sur son domaine, tous bloquants :

| Domaine                  | Détecteur                                         | Ce qu'il attrape                                                                       |
| ------------------------ | ------------------------------------------------- | -------------------------------------------------------------------------------------- |
| TypeScript, JavaScript   | `knip`                                            | fichiers orphelins, exports jamais importés, dépendances déclarées et jamais utilisées |
| C# — membres privés      | Roslyn : `IDE0051`, `IDE0052`, `CA1823`, `CS0169` | champ, méthode ou propriété privée jamais lus                                          |
| C# — `using`             | `IDE0005`                                         | import inutile                                                                         |
| C# — membres **publics** | **la couverture à 100 %**                         | un membre public que rien n'appelle n'est pas couvert, donc le seuil échoue            |

Ce dernier point mérite d'être compris : Roslyn **ne peut pas** signaler un membre public non utilisé — par construction, il pourrait l'être depuis l'extérieur de l'assemblage. C'est le seuil de couverture qui fait ce travail, et c'est une des raisons d'être des 100 % exigés sur `Palier.Domain`.

**Conséquence directe, à connaître :** tout projet backend sans seuil de couverture n'a **aucune détection de code mort public**. Aujourd'hui, seul `Palier.Domain` en a un. Chaque projet qui acquiert des tests acquiert son seuil dans le même geste — sans quoi le trou reste ouvert et personne ne le voit.

**Aucune exception sans échéance.** Une exception à `knip` — ou à n'importe quel détecteur — s'accompagne d'une décision datée qui dit ce qui la ferme, et d'une épreuve **inversée** qui rougira le jour où elle deviendra inutile. Sans cela, une exception posée « le temps de » devient permanente : c'est le mécanisme de D24, et il vaut pour tous les détecteurs.

### Ce qu'un garde-fou doit être

Ces règles viennent du lot 1, où chacune a été payée au moins une fois. Le détail et les mesures sont dans `.superpowers/sdd/2026-08-20-lot-1-harnais-complet/progress.md` (rulings P1 à P23).

- **Un garde-fou non éprouvé ment.** On provoque la violation qu'il doit refuser, on constate le refus, et l'épreuve reste dans la suite. Un contrôle dont on n'a jamais vu le rouge ne protège rien.
- **Deux assertions par épreuve** : le code de sortie **et** un motif propre à la cause. Un code non nul ne prouve pas que l'outil a refusé — un binaire absent, un mauvais chemin ou une cible manquante en rendent un aussi.
- **Une branche jamais franchie est une branche qui ment.** Y compris la branche « l'outil n'est pas là » et la branche « la cible a disparu ».
- **L'exclusion appartient à la commande, jamais au fichier de configuration.** Cinq outils ont porté ce piège : un fichier ignoré par la configuration reste ignoré même nommé explicitement en argument, et la fixture devient invisible pour sa propre épreuve.
- **Aucun commentaire dans un fichier de configuration d'outil.** Une clé inconnue peut désactiver la règle entière en silence — mesuré, neuf violations passées à zéro. Le motif d'un choix vit dans l'épreuve qui le protège.
- **Un réglage accepté n'est pas un réglage appliqué**, et **un fichier correct dont le service est éteint ne protège rien**. On interroge le système, on ne relit pas le fichier.
- **Vérifier à la source, jamais de mémoire.** Une version, une API, une limite de plateforme affirmées de mémoire sont périmées par construction. Context7 ou la documentation officielle avant d'écrire.

---

## 5. Hiérarchie en cas de contradiction

Ce dossier a été écrit par itérations. Des contradictions résiduelles sont possibles. **Ne jamais trancher silencieusement : signaler, proposer, attendre.**

Ordre de priorité quand deux documents divergent :

1. **`docs/01-conformite.md`** — prime sur tout. Une règle de conformité ne cède jamais
2. **Le document spécialisé** — `09-comptes.md` fait autorité sur l'authentification, `02-design.md` sur le visuel, `03-donnees.md` sur le schéma
3. **`docs/07-roadmap.md`** — pour l'ordre et le contenu des étapes
4. **`CLAUDE.md`** — synthèse, donc le plus susceptible d'être en retard sur les documents spécialisés

Autrement dit : ce fichier prime sur les **règles de travail**, jamais sur le **contenu métier**. En cas de conflit sur un point produit, le document spécialisé gagne.

---

## 6. Ce qui doit déclencher une question à l'utilisateur

Ne jamais trancher seul sur :

- L'ajout d'une fonctionnalité hors du périmètre V1
- Toute formulation destinée à l'utilisateur final qui touche à la nutrition ou à la santé
- Un changement de schéma de base après la première mise en production
- L'ajout d'une dépendance lourde
- Un arbitrage entre rapidité de livraison et conformité — **la conformité gagne toujours, mais l'utilisateur doit être informé du coût**
- **Toute contradiction détectée entre deux documents** — la signaler avec les références exactes, proposer la résolution, attendre validation
