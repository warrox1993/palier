# 08 — Workflow d'ingénierie agentique

Ce document définit **comment** on construit. Il est aussi contraignant que les spécifications produit.

---

## 1. Vocabulaire et cadre

En février 2026, Karpathy a renommé le « vibe coding » qu'il avait proposé un an plus tôt en **agentic engineering** : la pratique a mûri d'un prompt unique sans plan ni test vers une méthodologie structurée.

| Vibe coding | Agentic engineering |
|---|---|
| Prompt unique, pas de plan | Recherche → plan → exécution → revue → livraison |
| Pas de tests | TDD, la boucle de vérification est le cœur |
| « Ça a l'air fini » | Un contrôle exécutable : tests, build, capture d'écran |
| Contexte pollué par 20 lectures | Sous-agents qui absorbent le travail bruyant |
| Bon pour un prototype | Tient à l'échelle d'un produit |

**Ce projet fait de l'agentic engineering. Toute session qui dérive vers le vibe coding est interrompue et reprise depuis le plan.**

### Ce qui est couvert par l'abonnement

Depuis le 4 avril 2026, Anthropic ne couvre plus l'usage des abonnements Pro et Max via des **harnesses tiers** (OpenClaw, OpenCode, NanoClaw et assimilés) qui passaient par l'OAuth de Claude Code. Le CLI Claude Code officiel reste couvert.

| Couvert par l'abonnement Max | Non couvert |
|---|---|
| Claude Code CLI officiel | Harnesses externes tiers |
| Plugins, skills, sous-agents, hooks **dans** Claude Code | Frameworks agents autonomes hors Claude Code |
| Serveurs MCP appelés depuis Claude Code | — |

**Conséquence pratique : tout le workflow décrit ici tourne dans Claude Code officiel.** Le développement est donc couvert par l'abonnement. Seule l'API de production (les appels que fait l'application pour ses utilisateurs) est facturée séparément.

---

## 2. La boucle, dans l'ordre

### Recherche
Toute investigation demandant plus de trois lectures de fichiers part en **sous-agent**. Le contexte principal reste propre : vingt lectures et douze recherches textuelles dans la session principale, puis une tentative de planification avec ce bruit encore chargé, est l'erreur la plus coûteuse du travail agentique.

Une exploration non bornée lit des centaines de fichiers, remplit la fenêtre et ne produit rien d'exploitable. **Toute recherche est bornée avant d'être lancée** : question précise, périmètre de fichiers, format de rapport attendu.

### Plan
La commande de planification. Découpage en tâches assez précises pour qu'un exécutant sans contexte puisse les réaliser. **Validé par l'utilisateur avant toute écriture de code.**

### Exécution
La commande d'exécution, par lots. Une tâche = un test qui échoue, le code minimal, le test qui passe, un commit.

### Revue
Revue entre chaque tâche, contre le plan. Les problèmes critiques bloquent la suite.

### Livraison
`finishing-a-development-branch` : vérification des tests, stratégie de fusion, nettoyage du worktree.

---

## 3. Installation — l'outillage exact

### Framework principal

```bash
/plugin marketplace add obra/superpowers-marketplace
/plugin install superpowers@superpowers-marketplace
```

Superpowers (Jesse Vincent / Prime Radiant) a dépassé 94 000 étoiles et a été accepté dans la marketplace officielle d'Anthropic.

**Les noms de commandes varient selon la version.** La version courante expose `brainstorming`, `writing-plans` et `executing-plans` ; des versions antérieures utilisaient le préfixe `/superpowers:`. Vérifier via `/help` au démarrage et utiliser ce qui s'affiche.

| Fonction | Commande courante |
|---|---|
| Clarification avant conception | `brainstorming` |
| Plan d'implémentation | `writing-plans` |
| Exécution par lots | `executing-plans` |

Skills qui s'activent automatiquement : `test-driven-development`, `systematic-debugging`, `verification-before-completion`, `requesting-code-review`, `finishing-a-development-branch`, `subagent-driven-development`.

**Configuration requise :** ajouter `EnterPlanMode` à la deny list des permissions. Le mode Plan natif de Claude Code entre en conflit avec les workflows Superpowers.

### Plugins officiels Anthropic

Marketplace `claude-plugins-official`, auto-enregistrée :

| Plugin | Usage dans ce projet |
|---|---|
| `code-review` | Revue entre tâches |
| `security-guidance` | OWASP, code sécurisé |
| `pr-review-toolkit` | Revue de branche avant fusion |
| `commit-commands` | Messages de commit structurés |

### Où chercher des agents et skills supplémentaires

**Règle absolue : n'installer que des agents existants et actifs. Ne jamais en créer.**

| Source | Nature |
|---|---|
| `claude-plugins-official` | Marketplace Anthropic, première source |
| `obra/superpowers-marketplace` | Superpowers et plugins liés |
| `hesreallyhim/awesome-claude-code` | Liste canonique, curation humaine |
| `punkpeye/awesome-mcp-servers` | Catalogue MCP de référence |
| `composio-community/awesome-claude-plugins` | Curation communautaire vérifiée |
| `rohitg00/awesome-claude-code-toolkit` | Agents, skills, commandes, configs MCP |
| `davila7/claude-code-templates` (aitmpl.com) | Catalogue et CLI |
| `claudepluginhub.com` | Annuaire indépendant, notation |

**Filtre avant toute installation :** date du dernier commit, nombre d'utilisateurs, lecture du code du hook ou de la skill. Une liste mise à jour hier est actuelle ; une liste d'il y a un an est de l'archéologie. L'écosystème bouge vite et certaines listes sont auto-générées sans vérification.

**Ne jamais installer un plugin sans avoir lu ce qu'il exécute.** Un hook a un accès shell.

---

## 4. Serveurs MCP du projet

Les skills apportent la connaissance, MCP apporte l'action. Serveurs à configurer, avec permissions minimales :

| Serveur | Usage | Permissions |
|---|---|---|
| **PostgreSQL MCP** | Schéma, requêtes, inspection | **Lecture seule, jamais la production.** Les migrations passent par EF Core (D14), pas par MCP |
| **GitHub MCP** | Issues, PR, Actions | Dépôt du projet uniquement |
| **Playwright MCP** ou **Chrome DevTools MCP** | Tests de bout en bout, captures, audit visuel | Local |
| **Context7 MCP** | Documentation à jour des bibliothèques | Lecture |
| **Sentry MCP** | Erreurs de production | Lecture |
| **Stripe MCP** | Objets de facturation, en test uniquement | Clés de test |
| **Figma MCP** | Si des maquettes existent | Lecture |

**Interdits :** aucun serveur MCP avec accès en écriture sur la base de production. Aucun serveur non audité. Les clés vivent dans l'environnement, jamais dans un fichier versionné.

---

## 5. Harnais déterministe

Un agent est fiable dans la mesure où son environnement l'est. **Ces éléments sont en place avant la première fonctionnalité.**

| Élément | Rôle |
|---|---|
| TypeScript strict | Erreurs avant exécution |
| Oxlint + Prettier, pre-commit | Style non négociable. **Oxlint et non ESLint** : `typescript-eslint` déclare `typescript <6.1.0` et est incompatible avec TypeScript 7. `oxlint-tsgolint` couvre les règles type-aware |
| Vitest | Tests unitaires rapides |
| Playwright | Bout en bout sur les parcours critiques |
| axe-core | Accessibilité, échec du build sur violation critique |
| Hook pre-commit | Refuse le commit si tests ou lint échouent |
| Hook pre-push | Refuse le push si le build échoue |
| CI GitHub Actions | Lint, types, tests, build, a11y, audit de dépendances |
| Migrations versionnées | Aucune modification manuelle du schéma |
| Lighthouse CI | Seuil de performance |
| `npm audit` / Dependabot | Vulnérabilités |
| **`dotnet format` + analyseurs** | Style et rigueur du backend, `TreatWarningsAsErrors` |
| **Couverture 100 % sur `Palier.Domain`** | Le domaine porte les calculs de conformité : aucune ligne non testée |
| **Contrôle des licences** | npm et NuGet, liste blanche. Motivé par la découverte de MediatR sous RPL-1.5 — D13 |
| **Épreuves de franchissement** | Chaque garde-fou ci-dessus a une épreuve qui provoque la violation qu'il doit refuser, et qui reste dans la suite — D5 |

**Des boucles de rétroaction rapides conditionnent tout.** Compilation rapide, tests rapides, outils qui ne pendent pas. Si le build prend deux minutes, l'agent tourne en rond. Investir dans la vitesse du harnais est prioritaire sur toute fonctionnalité.

---

## 6. Pipeline par domaine

Chaque domaine a sa séquence, ses vérifications et son critère de sortie. **Aucun domaine n'est considéré terminé sans son contrôle exécutable.**

### Base de données

1. Écrire la migration avec EF Core (`dotnet ef migrations add`), dans `back/Palier.Infrastructure/Migrations/`
2. **RLS activée dans la même migration que la création de table** — jamais après
3. Écrire un test de politique : un utilisateur A ne doit jamais lire une ligne de B
4. Appliquer en local, puis en recette
5. Vérifier les index sur les colonnes de filtre et de tri
6. `EXPLAIN ANALYZE` sur les requêtes des écrans principaux

**Critère de sortie :** test RLS vert pour chaque table, aucune requête au-delà de 100 ms sur un jeu de données réaliste.

### Backend et API

1. Schéma de validation (Zod) défini avant le point d'entrée
2. Test unitaire du module métier — pur, sans base
3. Test d'intégration du point d'entrée
4. Validation systématique côté serveur, jamais uniquement côté client
5. Gestion d'erreur typée, aucun `catch` silencieux
6. Limitation de débit sur les points sensibles

**Critère de sortie :** couverture des modules de calcul à 100 % (nutrition, progression, XP), test d'intégration vert, aucune donnée de santé dans les logs.

### Frontend et design

1. Lire `02-design.md` **avant** d'écrire un composant
2. N'utiliser que les jetons définis — aucune couleur, taille ou rayon hors système
3. Construire les quatre états : chargement, vide, erreur, contenu partiel
4. Vérifier au clavier, puis au lecteur d'écran
5. Capture d'écran comparée à la structure imposée pour l'écran de séance
6. Vérifier à 375 px de large, une main, luminosité réduite

**Critère de sortie :** axe-core sans violation, Lighthouse supérieur à 90, capture d'écran validée, toutes les chaînes en i18n.

### Sécurité

1. `security-guidance` et un scanner OWASP à chaque fin de lot
2. Aucun secret dans le dépôt — vérification automatisée
3. En-têtes : CSP stricte, HSTS, X-Content-Type-Options, Referrer-Policy
4. Téléversements : type MIME vérifié, taille limitée, **EXIF supprimé** (les photos contiennent des coordonnées GPS)
5. URL signées à durée limitée pour les fichiers privés
6. Audit des dépendances, vulnérabilités critiques corrigées sous 7 jours

**Critère de sortie :** scan OWASP sans finding critique, aucun secret détecté, en-têtes vérifiés en production.

### Conformité — spécifique à ce projet

1. Tout libellé destiné à l'utilisateur passe par la table de libellés versionnée
2. Le filtre de sortie du modèle a ses tests, cas positifs et négatifs
3. Les planchers de sécurité ont leurs tests et ne sont pas contournables par configuration
4. Aucun calcul nutritionnel ou de progression dans un composant — modules purs uniquement
5. Vérifier qu'aucune donnée directement identifiante ne part vers un fournisseur de modèle

**Critère de sortie :** suite de tests de conformité verte. **Elle bloque le déploiement au même titre que les tests unitaires.**

### Performance

1. Découpage du code par route, module 3D en chargement différé
2. Budget de bundle vérifié en CI
3. Écriture optimiste sur toute saisie
4. Requêtes mises en cache via TanStack Query, invalidation explicite
5. Images en formats modernes, dimensionnées

**Critère de sortie :** premier affichage utile sous 1,5 s en 4G simulée, interaction sous 100 ms, bundle initial sous 200 ko compressé.

---

## 7. Sous-agents

**Règle absolue : aucun agent, skill ou sous-agent personnalisé n'est créé.** Utiliser exclusivement Superpowers et les agents publiés sur les marketplaces publiques.

Si un besoin semble appeler un agent inexistant : chercher dans les marketplaces listées en section 3, puis signaler à l'utilisateur et attendre sa décision.

Quand un sous-agent existant est employé : contexte isolé, permissions d'outils restreintes au strict nécessaire, tâche bornée, rapport structuré en retour. Les agents spécifiques à une tâche donnent de meilleurs résultats que les agents généralistes — la spécificité améliore la sélection d'outils et resserre le contexte.

Pour un bug simple ou une petite fonctionnalité, une session unique va plus vite qu'un sous-agent.

---

## 8. Parallélisme

Les worktrees Git isolent les travaux concurrents : une branche par fonctionnalité, un worktree par agent, un seul domaine par agent. C'est ce qui évite les conflits de fusion plutôt que de les résoudre.

Le fan-out — une invocation isolée par fichier sur une liste générée à l'avance — est réservé aux tâches mécaniques de masse. Tester sur deux ou trois fichiers avant de lancer à l'échelle.

---

## 9. Définition de terminé

« Ça a l'air fini » est un signal faible. **Chaque tâche livre un contrôle que l'agent peut exécuter lui-même.**

- [ ] Test écrit avant le code, ayant échoué puis réussi
- [ ] Lint et types sans erreur
- [ ] Build réussi
- [ ] Parcours vérifié manuellement ou par test de bout en bout
- [ ] Revue passée, aucun problème critique
- [ ] Aucune régression sur la suite existante
- [ ] Accessible au clavier, axe-core vert, contraste vérifié
- [ ] États de chargement, erreur, vide et partiel traités
- [ ] Textes en français et en anglais, aucune chaîne en dur
- [ ] Aucune donnée de santé dans les logs
- [ ] Tests de conformité verts si la tâche touche nutrition, progression ou assistant
- [ ] Commit avec message explicite

**Une tâche qui ne coche pas ces douze cases n'est pas terminée.** Il n'y a pas de « on finira plus tard ».

---

## 10. Gestion du contexte

Chaque agent travaille dans une fenêtre finie. Une fois qu'elle se remplit d'impasses, la qualité chute quel que soit le modèle. **Corriger une mauvaise direction tôt coûte moins cher que de la laisser se propager.**

En pratique : `CLAUDE.md` court et dense, recherche en sous-agents, session longue redémarrée plutôt que prolongée, plan écrit sur disque pour survivre à la fin d'une session.

---

## 11. Interdits

- Coder sans plan validé
- Marquer une tâche terminée sans contrôle exécutable
- Laisser un test échouer « temporairement »
- Écrire du code avant son test — ce code est supprimé, pas corrigé
- Corriger un symptôme sans remonter à la cause
- Créer un agent, une skill ou un hook maison
- Installer un plugin sans avoir lu ce qu'il exécute
- Connecter un serveur MCP en écriture sur la production
- Explorer sans borne
- Reporter l'accessibilité, la traduction ou la gestion d'erreur
