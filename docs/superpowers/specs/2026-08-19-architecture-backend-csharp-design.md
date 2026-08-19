# Architecture — backend C# séparé

**Date :** 19 août 2026
**Statut :** design, en attente de relecture
**Remplace :** la pile décrite dans `CLAUDE.md` § 3
**Décisions sources :** `docs/decisions.md`, entrées D9 à D17

---

## 1. Objet

Le dossier initial prévoyait une application cliente parlant directement à Supabase, protégée par les politiques RLS de PostgreSQL. Cette architecture est remplacée par **trois composants distincts** : un front React, une API C#, une base PostgreSQL, le tout hébergé chez OVHcloud.

Ce document décrit l'architecture cible. Il ne redéfinit pas le produit : `00-produit.md`, `01-conformite.md`, `04-nutrition.md`, `05-entrainement.md` et `10-progression.md` restent intégralement valables — ce sont les documents qui disent *quoi*, et le quoi ne change pas.

---

## 2. Ce qui change, ce qui reste

### Reste inchangé

| Élément | Où |
|---|---|
| Le produit, sa promesse, son périmètre | `00-produit.md` |
| La ligne informer / prescrire et ses garde-fous | `01-conformite.md` |
| Toutes les formules nutritionnelles et d'entraînement | `04-nutrition.md`, `05-entrainement.md` |
| La direction artistique et l'écran de séance | `02-design.md` |
| Le front React, TypeScript, PWA, i18n, Dexie | inchangé |
| Le harnais front déjà installé | lot 1, tâches 1 et 2 livrées |

### Change

| Élément | Avant | Après |
|---|---|---|
| Accès aux données | client → Supabase | client → API C# → PostgreSQL |
| Authentification | Supabase Auth | ASP.NET Identity |
| Protection des données | RLS comme unique barrière | autorisation applicative, RLS en défense de profondeur |
| Hébergement | Vercel + Supabase | OVHcloud, front et API sous le même domaine |
| Fonctions serveur | `api/` en TypeScript sur Vercel | contrôleurs de l'API C# |
| Calculs métier | `src/core/` en TypeScript | `Palier.Domain` en C# |

**Le front conserve ses propres modules purs** pour ce qui doit rester réactif hors ligne — conversions d'unités, formatage, agrégations d'affichage. Les calculs qui engagent la conformité — besoins énergétiques, comparaison aux limites hautes EFSA, planchers de sécurité — vivent **exclusivement** dans `Palier.Domain`. Un calcul de conformité ne peut pas exister à deux endroits : il y aurait deux vérités.

---

## 3. Structure des projets

```
palier/
├── front/                          # l'application React existante
│   ├── src/{app,features,core,ui,lib,locales}/
│   └── tests/{unit,e2e,harness}/
├── back/
│   ├── Palier.Domain/              # entités, invariants, CALCULS PURS
│   ├── Palier.Application/         # cas d'usage, ports, validation
│   ├── Palier.Infrastructure/      # EF Core, identité, adaptateurs LLM
│   ├── Palier.Api/                 # endpoints, injection, intergiciels
│   ├── Palier.Domain.Tests/        # couverture 100 %
│   ├── Palier.Application.Tests/
│   ├── Palier.Api.Tests/           # tests d'intégration, base réelle
│   └── Palier.sln
├── deploy/
│   ├── docker-compose.yml
│   ├── Caddyfile
│   └── Dockerfile
└── docs/
```

### Le sens des références

```
Palier.Domain          ──▶  (aucune référence)
Palier.Application     ──▶  Domain
Palier.Infrastructure  ──▶  Domain, Application
Palier.Api             ──▶  Domain, Application, Infrastructure
```

**`Palier.Domain` ne référence aucun projet et aucun paquet d'accès aux données.** C'est la traduction structurelle de `01-conformite.md` § 3 et de `16-projet.md` § 1 : un calcul nutritionnel ne peut pas atteindre la base, le réseau ou l'interface. Ce n'est plus une convention vérifiée par un outil de style — c'est une impossibilité à la compilation.

Cette règle est éprouvée par une épreuve du harnais : ajouter une référence de `Domain` vers `Infrastructure` doit faire échouer la compilation.

---

## 4. Le domaine

`Palier.Domain` contient trois familles de choses, et rien d'autre.

**Les entités et objets-valeurs** — `Workout`, `Set`, `Exercise`, `IntakeEntry`, `NutrientRef`, `UserTargets`. Les unités sont typées : une masse n'est pas un `decimal` nu mais un objet-valeur qui interdit les valeurs négatives et porte son unité. `16-projet.md` § 2 impose le stockage en SI ; le typage l'impose au compilateur.

**Les calculs purs**, traduits de `04-nutrition.md` et `05-entrainement.md` : métabolisme de base (Mifflin-St Jeor, Katch-McArdle), dépense totale, TDEE adaptatif, agrégation des nutriments alimentation + compléments, comparaison aux limites hautes, volume par groupe musculaire, 1RM estimé par Epley corrigé du RIR, détection de plateau.

**Les planchers de sécurité** de `01-conformite.md` § 5 : plancher calorique bloquant, planchers protéique et lipidique, détection de perte de poids rapide, détection de restriction sévère. Ils sont exprimés comme des invariants du domaine, non comme des validations d'entrée — une valeur qui les viole ne peut pas être construite.

> `08-workflow.md` § 6 exige une couverture de **100 %** sur ces modules. C'est tenable précisément parce qu'ils sont purs : aucune base, aucun réseau, aucun simulacre.

---

## 5. Les cas d'usage

`Palier.Application` expose des **commandes** (qui modifient) et des **requêtes** (qui lisent), chacune avec son handler.

La répartition passe par `Mediator.SourceGenerator` (licence MIT, vérifiée), qui génère le code à la compilation. Trois raisons plutôt que MediatR : la licence de MediatR interdit un usage commercial propriétaire sans achat, la génération à la compilation est plus rapide que la réflexion, et les piles d'appel restent lisibles au débogueur.

Un pipeline de comportements enveloppe chaque handler, dans cet ordre :

1. **Validation** — FluentValidation (Apache-2.0, vérifiée), échec avant toute exécution
2. **Autorisation** — le demandeur a-t-il le droit sur cette ressource
3. **Transaction** — les commandes seulement, jamais les requêtes
4. **Journalisation** — durée, résultat, **jamais de donnée de santé**, comme l'exige `01-conformite.md` § 4

Cette dernière contrainte a une épreuve dédiée dans le harnais : un journal contenant un poids, un apport ou une contrainte déclarée doit faire échouer un test.

---

## 6. Données et migrations

**PostgreSQL managé chez OVHcloud.** Le schéma reste celui de `03-donnees.md` — il est bon, et il a été écrit en SQL.

EF Core gère les entités et les migrations. Ce qu'il ne gère pas passe par `migrationBuilder.Sql(...)` dans la migration correspondante :

- les trois vues — `daily_intake` (« la vue centrale du produit »), `weekly_volume`, `exercise_progression`
- les contraintes `CHECK` (`num_nonnulls(food_id, supplement_id) = 1`, les bornes d'énergie, les valeurs d'énumération)
- les index composites
- les politiques RLS

**Sur RLS.** Avec un backend, l'API est le seul client de la base et l'autorisation se fait en amont. RLS n'est donc plus la barrière principale — elle devient une **défense de profondeur**, et elle est conservée. Motif : si l'API a un défaut d'autorisation, le moteur PostgreSQL limite encore les dégâts. Cela suppose que l'API se connecte avec un rôle applicatif restreint et non avec le propriétaire de la base — ce point est vérifié par une épreuve.

Les tests de politiques de `08-workflow.md` § 6 sont conservés tels quels : « un utilisateur A ne doit jamais lire une ligne de B », par table.

**Trois tables absentes de `03-donnees.md`** et exigées ailleurs, à créer :

| Table | Exigée par |
|---|---|
| journal versionné des libellés | `09-comptes.md` § 6 — « aucun texte nutritionnel en dur dans le code » |
| journal des appels au modèle | `06-ia.md` § 2 — « sans cette table, aucun arbitrage n'est possible » ; `13-juridique.md` § 2 |
| tables d'identité ASP.NET | conséquence de D17 |

---

## 7. Authentification

ASP.NET Identity, avec les sept exigences de `09-comptes.md` § 1. Ce qu'Identity fournit et ce qui reste à écrire :

| Exigence | Fourni | À écrire |
|---|---|---|
| Email et mot de passe, hachage | ✅ | — |
| Minimum 10 caractères | ✅ configurable | — |
| Contrôle HaveIBeenPwned par préfixe de hachage | ❌ | validateur de mot de passe |
| Vérification d'email | ✅ jetons | l'envoi et le blocage de la nutrition |
| Google OAuth | ✅ | la fusion avec un compte email existant |
| 5 tentatives par IP **et** par compte sur 15 minutes | partiel | limitation par IP |
| Verrouillage temporaire progressif | ✅ par compte | la progressivité |
| 2FA TOTP | ✅ | les codes de secours |
| Sessions listées, déconnexion de tous les appareils | ❌ | table de sessions |
| Rotation du jeton de rafraîchissement à chaque usage | ❌ | à implémenter |

**Le jeton d'accès est court, le jeton de rafraîchissement vit dans un cookie `httpOnly`, `Secure`, `SameSite=Strict`.** Ce dernier point n'est possible que parce que le front et l'API partagent le domaine (D16) — c'est le bénéfice concret de cette décision.

**Récupération de compte :** procédure manuelle, jamais automatique. `09-comptes.md` § 1 le dit sans détour : « c'est le vecteur d'attaque classique. »

---

## 8. L'API

**REST, sous `/api/v1`.** Le versionnement dès le premier jour, comme le demande `14-contenu.md` § 8 : « le jour où une application native arrive, une API non versionnée devient un problème insoluble. »

Minimal APIs plutôt que contrôleurs : moins de cérémonie, et la validation par point d'entrée reste explicite.

**Contrat d'erreur uniforme** — `ProblemDetails` (RFC 7807), avec un code d'erreur stable que le front traduit via i18next. Aucun message d'erreur en français ou en anglais ne vient du backend : `CLAUDE.md` § 4 impose que toute chaîne passe par i18next, et cela vaut aussi pour les erreurs.

**Ce qui ne passe jamais par l'API :** les valeurs de référence nutritionnelles ne sont pas calculées côté client, et les libellés nutritionnels ne sont pas écrits en dur dans le front. Les deux viennent de la base, versionnés et validés par le diététicien.

**Le mode hors ligne est conservé.** Le front continue d'écrire dans Dexie et de rejouer sa file de retry, comme le décrit `11-qualite.md` § 1. Ce qui change : la file rejoue des appels à l'API au lieu d'appels à Supabase. Les identifiants sont générés côté client (UUID v7) pour que la reprise soit idempotente — une opération rejouée deux fois ne crée pas deux lignes.

---

## 9. La couche modèle

Inchangée dans son principe, déplacée dans son emplacement. `Palier.Infrastructure` porte l'abstraction que `06-ia.md` § 2 impose :

```csharp
public interface ILlmProvider
{
    string Name { get; }
    Task<CompletionResponse> Complete(CompletionRequest req, CancellationToken ct);
    Task<CompletionResponse> Vision(VisionRequest req, CancellationToken ct);
}
```

Deux implémentations, un routage par tâche configurable sans recompilation, et le journal des appels — fournisseur, modèle, tokens, coût, latence, type de tâche — **jamais le contenu du prompt ni de la réponse**, comme l'impose `13-juridique.md` § 2.

**Le filtre de sortie** de `01-conformite.md` § 6 vit dans `Palier.Domain` : c'est une fonction pure sur une chaîne, testable exhaustivement, et elle doit être hors de portée de tout ce qui pourrait la contourner.

**La minimisation du contexte** de `13-juridique.md` § 2 est appliquée dans `Palier.Application`, avant l'appel : jamais de nom, jamais d'email, jamais d'identifiant de compte. Une épreuve du harnais vérifie qu'aucune donnée directement identifiante ne franchit cette frontière.

---

## 10. Déploiement

```
                    ┌──────────────────────────┐
   navigateur ──────▶  Caddy (TLS automatique) │
                    └────────┬─────────────────┘
                             │ même domaine
                 ┌───────────┴────────────┐
                 ▼                        ▼
        fichiers statiques        Palier.Api (conteneur)
         du front React                   │
                                          ▼
                          PostgreSQL managé OVHcloud
```

**VPS ou instance Public Cloud OVH**, Docker Compose, Caddy en frontal pour les certificats — il les obtient et les renouvelle seul. Le front est construit en fichiers statiques et servi par Caddy ; l'API est un conteneur.

**Ce que cela demande, et qui n'existait pas avant :** mises à jour du système, supervision, sauvegardes vérifiées, redémarrage après incident. `14-contenu.md` § 7 exige déjà une restauration testée trimestriellement — c'est désormais votre responsabilité pleine.

**Trois environnements**, comme l'impose `14-contenu.md` § 5 : local (Docker Compose avec PostgreSQL en conteneur), recette (base distincte), production. Aucune migration n'atteint la production sans être passée en recette.

**En-têtes de sécurité** portés par Caddy : CSP stricte, HSTS, X-Content-Type-Options, Referrer-Policy, Permissions-Policy. Vérifiés par un test de bout en bout, comme le prévoyait déjà le plan du lot 1.

---

## 11. Le harnais .NET

Le harnais front reste intégralement valable. Il lui faut un jumeau côté backend.

| Élément | Rôle |
|---|---|
| `TreatWarningsAsErrors` | un avertissement du compilateur est une erreur |
| `Nullable: enable` | l'équivalent de `strict` en TypeScript |
| Analyseurs Roslyn (`AnalysisLevel: latest-all`) | l'équivalent d'ESLint |
| `dotnet format --verify-no-changes` | l'équivalent de Prettier |
| xUnit (Apache-2.0) | tests unitaires et d'intégration |
| Couverture, seuil 100 % sur `Palier.Domain` | `08-workflow.md` § 6 |
| Testcontainers | tests d'intégration sur un vrai PostgreSQL, pas un simulacre |
| Contrôle des licences | liste blanche, échec si une dépendance en sort (D13) |
| Analyse de vulnérabilités | `dotnet list package --vulnerable`, en échec |

**Un seul point d'entrée, comme côté front :** `npm run verify` à la racine enchaîne le front puis le backend. C'est cette commande unique qu'appellent le hook de pré-envoi et la CI — jamais une liste dupliquée, sinon les deux divergent.

**Les épreuves de franchissement** passent de douze à une vingtaine. Les nouvelles, côté backend :

| Garde-fou | Violation provoquée | Attendu |
|---|---|---|
| Pureté du domaine | référence de `Domain` vers `Infrastructure` | la compilation échoue |
| Nullabilité | déréférencement possiblement nul | la compilation échoue |
| Avertissements | avertissement du compilateur | la compilation échoue |
| Format | fichier mal formaté | `dotnet format --verify-no-changes` échoue |
| Couverture | fonction du domaine non testée | le seuil échoue |
| Journalisation | une donnée de santé dans un journal | le test échoue |
| Isolation des données | l'utilisateur A lit une ligne de B | le test échoue |
| Minimisation | un nom ou un email franchit la frontière du modèle | le test échoue |
| Licences | dépendance hors liste blanche | la CI échoue |

---

## 12. Ce que la conformité gagne et ce qu'elle perd

**Gagne :** la frontière entre le calcul et le modèle devient une impossibilité de compilation. Le filtre de sortie, les planchers de sécurité et les références EFSA vivent dans un projet qui ne peut atteindre ni le réseau ni la base. `01-conformite.md` § 3 est mieux servi qu'avant.

**Perd :** l'authentification cesse d'être un service délégué pour devenir un traitement que vous opérez. `13-juridique.md` doit en tenir compte — l'AIPD et le registre des traitements changent de contenu, et la responsabilité en cas de fuite d'identifiants vous revient.

**Neutre :** l'hébergement européen était déjà exigé. OVHcloud le sert mieux qu'un fournisseur américain avec des régions européennes, puisqu'il n'y a plus de Cloud Act à documenter.

---

## 13. Documents du dossier à reprendre

Ces documents contiennent désormais des affirmations fausses. **Aucun ne sera modifié sans validation** — `CLAUDE.md` § 6 l'interdit sur le contenu métier.

| Document | Ce qui devient faux |
|---|---|
| `CLAUDE.md` § 3 | toute la ligne « Stack » : backend, auth, hébergement, portabilité Cloudflare |
| `03-donnees.md` | RLS présentée comme la barrière principale ; trois tables manquantes |
| `09-comptes.md` § 1 | « toutes deux gérées par Supabase Auth » |
| `16-projet.md` | arborescence, variables d'environnement, conventions de nommage C# absentes |
| `08-workflow.md` | pipeline, serveurs MCP, critères de sortie par domaine |
| `13-juridique.md` | liste des sous-traitants, périmètre de l'AIPD |
| `07-roadmap.md` | découpage des étapes |

**La clause de portabilité de `CLAUDE.md` tombe :** « une migration vers Cloudflare doit rester possible en une journée » — Cloudflare Workers n'exécute pas .NET. Elle doit être supprimée ou remplacée par une clause de portabilité par conteneur, qui est mieux servie : une image Docker se déplace d'un hébergeur à l'autre.

---

## 14. Nouveau découpage

| Lot | Contenu | Dépend de |
|---|---|---|
| **1a** | Harnais front — **livré** (tâches 1 et 2), reste 11 tâches | — |
| **1b** | Harnais backend : solution, quatre projets, analyseurs, xUnit, Testcontainers, épreuves | .NET installé ✅ |
| **2** | Socle applicatif front : PWA, routage, jetons de design, i18n, quatre états | 1a |
| **3** | Domaine : entités, calculs purs, planchers de sécurité, filtre de sortie — **100 % de couverture** | 1b |
| **4** | Données : EF Core, migrations, schéma, vues et RLS en SQL, seed | 3 |
| **5** | Authentification : Identity, OAuth, 2FA, sessions, limitation de débit | 4 |
| **6** | API : cas d'usage, endpoints, contrat d'erreur, versionnement | 3, 4, 5 |
| **7** | Résilience front : Dexie, file de retry idempotente, indicateur | 2, 6 |
| **8** | Couche modèle : `ILlmProvider`, routage, journal des coûts, minimisation | 3, 6 |
| **9** | Déploiement : Docker, Caddy, OVH, trois environnements, supervision | tous |

Le lot 3 est le premier où l'orchestration multi-agents rapporte réellement : les calculs sont indépendants les uns des autres, un agent par famille de formules.

---

## 15. Points ouverts

| Point | Bloque | Détail |
|---|---|---|
| `typescript-eslint` incompatible TypeScript 7 | lot 1a, tâche 4 | `peerDependencies: typescript >=4.8.4 <6.1.0`, vérifié. Redescendre en TypeScript 6 ou changer de linter |
| Produit OVH exact | lot 9 | VPS, instance Public Cloud ou Kubernetes managé — à trancher au déploiement |
| Avatar « mode Miroir » | lot ultérieur | `10-progression.md` § 7 contredit `01-conformite.md` § 5 et sa propre § 1 |
| Coût IA : 3 €/mois ou moins d'1 € | aucun | `00-produit.md` et `06-ia.md` divergent d'un facteur trois |
| Définition de « V1 » et « V2 » | lot 9 | jamais définies, alors que plusieurs décisions s'y réfèrent |
| Recherche d'antériorité BOIP et EUIPO | dépôt de marque | `15-marque.md` § 4 la demande avant de s'attacher au nom |

---

## 16. Risques

**Le coût de l'authentification est sous-estimé par construction.** Dix exigences dont plusieurs ne sont pas fournies par ASP.NET Identity. C'est du code de sécurité, sur une application qui traite des données de santé, écrit par une personne seule. Mitigation : le lot 5 lui est entièrement consacré, et il ne se termine pas sans revue de sécurité dédiée.

**L'exploitation d'un VPS est un travail récurrent, pas une tâche.** Il n'apparaît dans aucun lot parce qu'il n'a pas de fin. Mitigation : Docker Compose et Caddy réduisent la surface ; la restauration testée trimestriellement est inscrite au calendrier plutôt que laissée à l'intention.

**Deux harnais coûtent plus que deux fois un.** Ils doivent rester cohérents : mêmes seuils, même point d'entrée, mêmes épreuves conceptuelles. Mitigation : `npm run verify` unique, et une seule CI.

**La duplication des calculs entre front et backend est le risque le plus insidieux.** Un besoin de réactivité hors ligne poussera à recalculer côté client ce que le domaine calcule déjà. Deux implémentations d'une même règle de conformité, c'est deux vérités, et celle qui s'affiche n'est pas forcément celle qui a été validée par le diététicien. Mitigation : les calculs de conformité ne sont jamais dupliqués ; le front affiche ce que l'API a calculé, et son mode hors ligne montre la dernière valeur connue avec son horodatage plutôt qu'une valeur recalculée.
