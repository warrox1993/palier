# 16 — Structure de projet et conventions

Claude Code n'invente pas les conventions : elles doivent être écrites. Ce document les fixe.

---

## 1. Arborescence

> **Réécrite le 20/08/2026.** La version précédente plaçait `src/`, `tests/` et `.env.example`
> à la racine et décrivait un dossier `supabase/`. La tâche 3 du lot 1 a scindé le dépôt en
> `front/` et `back/`, et les décisions D9, D10 et D14 ont remplacé Supabase par un backend
> .NET avec EF Core. Cette section était fausse en entier.

```
/
├── CLAUDE.md
├── README.md
├── DEMARRAGE.md
├── package.json                     # orchestration seule : verify, front, back
├── global.json                      # version du SDK .NET
├── .nvmrc                           # 24
├── docs/
│   ├── decisions.md                 # journal des décisions, lu au démarrage
│   └── superpowers/                 # specs et plans d'exécution
├── .github/
│   ├── workflows/ci.yml             # front, backend, sécurité, e2e, performance, franchissement
│   └── dependabot.yml
├── scripts/                         # outillage transverse, sans dépendance
│   ├── verify.mjs                   # LE point d'entrée unique du harnais
│   ├── regles-projet.mjs            # ce qu'aucun linter ne connaît
│   └── verifier-licences.mjs        # npm et NuGet, liste blanche
├── front/
│   ├── package.json
│   ├── .env.example                 # jamais .env
│   ├── vite.config.ts
│   ├── vitest.config.ts
│   ├── playwright.config.ts
│   ├── .oxlintrc.json
│   ├── src/
│   │   ├── app/                     # routes
│   │   ├── features/                # par domaine métier
│   │   │   ├── workout/
│   │   │   ├── nutrition/
│   │   │   ├── supplements/
│   │   │   ├── hydration/
│   │   │   ├── progression/
│   │   │   ├── assistant/
│   │   │   └── account/
│   │   ├── core/                    # modules purs d'AFFICHAGE, sans dépendance UI
│   │   │   ├── unites.ts            # conversions SI ↔ affichage
│   │   │   ├── formatage.ts
│   │   │   └── agregations.ts       # regroupements pour les graphiques
│   │   ├── ui/                      # composants du système de design
│   │   ├── lib/                     # client API, dexie, i18n, sync
│   │   └── locales/
│   │       ├── fr.json
│   │       └── en.json
│   └── tests/
│       ├── harness/                 # les épreuves de franchissement du harnais
│       │   ├── run-outil.ts         # la primitive partagée
│       │   └── fixtures/            # les violations délibérées
│       ├── e2e/
│       └── a11y/
└── back/
    ├── Palier.sln
    ├── Directory.Build.props        # rigueur globale, TreatWarningsAsErrors
    ├── coverage.runsettings         # seuil 100 % sur Palier.Domain
    ├── Palier.Domain/               # AUCUNE référence de projet — vérifié par le compilateur
    │   ├── Energie/                 # TDEE, Mifflin, Katch, adaptatif
    │   ├── Macros/
    │   ├── Micros/                  # agrégation, comparaison aux limites hautes
    │   ├── Hydratation/
    │   ├── Entrainement/            # volume, 1RM, progression, plateau
    │   └── Garde-fous/              # planchers de sécurité
    ├── Palier.Application/          # cas d'usage, CQRS via Mediator.SourceGenerator
    ├── Palier.Infrastructure/       # EF Core, migrations, modèles, identité
    ├── Palier.Api/                  # exposition HTTP
    ├── Palier.Domain.Tests/         # xUnit, couverture 100 %
    ├── Palier.Database.Tests/       # Testcontainers, politiques RLS, sauvegarde
    ├── .env.example                 # les trois chaînes de D37 — AUCUNE valeur
    └── tests-harness/               # épreuves de franchissement du backend
```

**Une TROISIÈME racine est née au lot 2 — D32 :**

```
db/                                  # AUCUN .csproj : du SQL, du YAML, du Markdown
├── compose.yaml                     # la base locale. Le tag de l'image est écrit ICI et nulle part ailleurs
├── amorcage/                        # rôles et privilèges, exécutés AVANT les migrations
│   └── 01-roles.sql                 # les trois rôles de D37, aucun superutilisateur
├── referentiel/                     # données de PRODUCTION — voir § 4
├── demonstration/                   # jeu de développement synthétique — voir § 4
├── SOURCES.md                       # attribution de chaque fichier de referentiel/
└── README.md                        # lever, appliquer, sauvegarder, restaurer, réinitialiser
```

> **Pourquoi `db/` ne porte aucun projet .NET.** `scripts/verifier-licences.mjs` parcourt
> littéralement `back` : un `.csproj` posé sous `db/` échapperait au contrôle de licences de D13
> **sans un message**. Et `.gitignore` n'ignore que `back/**/bin/` et `back/**/obj/`. Les
> migrations EF Core, le `DbContext` et les tests de politiques restent donc sous `back/`.

**La règle structurante a changé de lieu, pas de nature.**

Les calculs de conformité — énergie, macronutriments, micronutriments, hydratation,
volume, progression, planchers de sécurité — vivent **exclusivement dans
`Palier.Domain`**. Ce projet ne référence aucun autre projet de la solution : un
calcul nutritionnel ne _peut pas_ atteindre la base ou le réseau, et l'impossibilité
est vérifiée par le compilateur, non par une règle de style. `01-conformite.md` § 3
en dépend directement, et c'est ce qui rend ces calculs testables à 100 % et
auditables par le diététicien.

`front/src/core/` reste légitime pour ce qui ne décide de rien : conversions
d'unités, formatage, agrégations d'affichage. **Aucun calcul de conformité n'y est
recopié** — c'est une règle bloquante de `scripts/regles-projet.mjs`, pas une
recommandation.

---

## 2. Conventions de code

| Sujet                  | Règle                                                      |
| ---------------------- | ---------------------------------------------------------- |
| Fichiers               | `kebab-case.ts`                                            |
| Composants             | `PascalCase.tsx`                                           |
| Fonctions et variables | `camelCase`                                                |
| Constantes             | `SCREAMING_SNAKE_CASE`                                     |
| Tables et colonnes SQL | `snake_case`                                               |
| Types                  | `PascalCase`, préfixe interdit (`IUser` non)               |
| Booléens               | `is`, `has`, `can`                                         |
| Fonctions asynchrones  | Nom au verbe : `fetchWorkouts`, pas `workoutsData`         |
| Tests                  | `<fichier>.test.ts` à côté du source                       |
| Commits                | Français, impératif : `ajoute le calcul du TDEE adaptatif` |
| Branches               | `feat/`, `fix/`, `chore/`, `docs/`                         |

**Unités : tout est stocké en SI.** Kilogrammes, centimètres, millilitres, grammes, secondes. La conversion en unités impériales se fait à l'affichage uniquement, jamais en base.

**Dates :** `timestamptz` en base, ISO 8601 en transit, formatage localisé à l'affichage seulement.

**Nombres :** jamais de `float` pour une quantité nutritionnelle en base — `numeric`. L'arrondi se fait à l'affichage.

---

## 3. Variables d'environnement

`.env.example` versionné, `.env` jamais. Chaque variable documentée.

Deux fichiers distincts : `front/.env.example` pour ce que le navigateur reçoit,
`back/.env.example` pour ce qu'il ne doit jamais voir. La séparation physique vaut
mieux qu'une convention de préfixe — **une clé préfixée `VITE_` est publique par
construction**, et rien n'empêche d'en préfixer une par erreur.

`front/.env.example` :

```bash
# API — même domaine que le front (D16), donc chemin relatif en production
VITE_API_URL=/api
VITE_APP_URL=
VITE_DEFAULT_LOCALE=fr
VITE_ANALYTICS_URL=
VITE_STRIPE_PUBLISHABLE_KEY=
```

`back/.env.example` — **rien de tout cela n'atteint le navigateur** :

```bash
# Le coffre OVHcloud KMS (D59). CES CINQ VARIABLES SONT L'AMORÇAGE : elles ne
# peuvent pas aller au coffre, ce sont elles qui l'ouvrent. Tout le reste des
# SECRETS y a migré — les quatre chaînes de connexion, JWT_SIGNING_KEY et
# GOOGLE_OAUTH_CLIENT_SECRET —, et ce qui n'est pas secret reste ici.
OKMS_ENDPOINT=
OKMS_ID=
OKMS_KEY_ID=
OKMS_CLIENT_ID=
OKMS_CLIENT_SECRET=

# Ce qui n'est PAS secret et reste donc dans l'environnement : l'identifiant
# client Google est public par construction — il apparaît dans l'URL de
# redirection OAuth.
GOOGLE_OAUTH_CLIENT_ID=

# Modèles — serveur uniquement
ANTHROPIC_API_KEY=
GOOGLE_API_KEY=
LLM_DEFAULT_PROVIDER=google
LLM_MONTHLY_BUDGET_EUR=

# Stripe — V2 seulement
STRIPE_SECRET_KEY=
STRIPE_WEBHOOK_SECRET=

# Sources de données
OPENFOODFACTS_USER_AGENT=

# Observabilité
SENTRY_DSN=
```

**Poser la première clé de données** — une fois, avant le premier démarrage, et
de nouveau à chaque rotation :

```bash
dotnet run --project back/Palier.Api -- poser-cle-de-donnees
```

L'API n'en crée jamais : une table `cles_de_donnees` vide **refuse le
démarrage**. Une API qui fabriquerait sa clé quand elle n'en trouve pas en
fabriquerait une chaque fois qu'elle démarre contre une base qu'elle ne lit pas,
et rendrait illisibles, en silence, tous les secrets chiffrés par la précédente.

**Aucune clé de modèle ni de service ne doit être accessible côté client.** Tout
appel aux modèles passe par le backend, jamais par le navigateur. Ce n'est pas
seulement une règle de sécurité : `01-conformite.md` § 3 exige que le modèle
reçoive des valeurs **déjà calculées** par `Palier.Domain`. Un appel direct depuis
le client court-circuiterait le calcul autant que la clé.

---

## 4. Données de départ — RÉFÉRENTIEL et DÉMONSTRATION, jamais « seed »

> **Repris le 20/08/2026, au lot 2.** Le mot « seed » confondait deux choses qui n'ont ni le même
> destin, ni le même risque, ni le même dossier. Elles sont séparées — physiquement, dans `db/`.

|                 | `db/referentiel/`                                                                                        | `db/demonstration/`                              |
| --------------- | -------------------------------------------------------------------------------------------------------- | ------------------------------------------------ |
| Ce que c'est    | **données de production** : valeurs EFSA, catalogue d'exercices, programmes modèles                      | jeu de développement **entièrement synthétique** |
| Où cela arrive  | **en production**, chargé sous `palier_migrations`                                                       | **jamais en production**                         |
| Attribution     | **obligatoire** : une ligne dans `db/SOURCES.md` — nom, source, licence, millésime, date de relevé, URL  | aucune : rien n'en vient                         |
| Ce qui le garde | `npm run regles` refuse le dépôt si un `*.sql` de `referentiel/` n'a pas sa ligne, en nommant le fichier | D40                                              |

**Aucune donnée réelle n'entre jamais dans `demonstration/` — D40.** Restaurer un dump de
production, ou tout extrait de celui-ci, sur une machine de développement est interdit : c'est le
seul chemin par lequel des données de l'article 9 atterriraient sur un portable que le projet ne
chiffre pas et ne supervise pas.

### Le référentiel

| Fichier                | Contenu                                      | Source                   |
| ---------------------- | -------------------------------------------- | ------------------------ |
| `01-nutrient-refs.sql` | Références et limites hautes, ~40 nutriments | EFSA DRV                 |
| `02-exercises.sql`     | 60 exercices prioritaires puis extension     | Rédigé, relu par le kiné |
| `03-foods.sql`         | 300 aliments courants                        | CIQUAL                   |
| `04-programs.sql`      | 9 programmes modèles                         | Rédigés, relus           |

**Deux de ces quatre lignes ne sont PAS tranchées, et le lot 2 n'a pas le droit de les trancher
seul** — signalées ici plutôt que devinées :

- `03-foods.sql` est-il du **référentiel de production** ou un jeu de développement ? La réponse
  change le dossier qui le porte et le moment où il s'applique.
- `04-programs.sql` livre **9 programmes modèles**, mais `programs.owner_id` est
  `not null references auth.users` : le schéma n'a **aucune place pour un programme sans
  propriétaire**.

### La démonstration

Un compte de démonstration avec 4 semaines de données réalistes est indispensable pour tester les courbes, le radar et le TDEE adaptatif. **Sans historique, la moitié des écrans ne peut pas être vérifiée.** Il est **entièrement synthétique**, et il vit dans `db/demonstration/`.

---

## 5. Glossaire

Vocabulaire partagé entre le code, l'interface et les documents. Les termes anglais restent en anglais dans le code, français dans l'interface.

| Terme                | Définition                                                                                  |
| -------------------- | ------------------------------------------------------------------------------------------- |
| **RIR**              | _Reps In Reserve_. Répétitions restantes à la fin d'une série                               |
| **Série dure**       | Série de travail hors échauffement, comptée dans le volume                                  |
| **Volume**           | Nombre de séries dures par muscle et par semaine. Primaire = 1, secondaire = 0,5            |
| **Tonnage**          | Charge × répétitions cumulées sur une séance                                                |
| **1RM estimé**       | Charge maximale théorique, calculée par Epley corrigé du RIR. Jamais testée                 |
| **Bloc**             | Cycle de 4 à 6 semaines suivi d'un allègement                                               |
| **Allègement**       | Semaine à volume et charge réduits                                                          |
| **Mouvement socle**  | Exercice fixe sur un bloc, support de la mesure de progression                              |
| **UL**               | _Tolerable Upper Intake Level_. Limite haute de sécurité EFSA                               |
| **AI**               | _Adequate Intake_. Apport adéquat de référence                                              |
| **TDEE**             | Dépense énergétique totale quotidienne                                                      |
| **TDEE adaptatif**   | Dépense calculée sur les données réelles, remplace la formule dès la semaine 3              |
| **Contrainte**       | Limitation physique déclarée par l'utilisateur                                              |
| **Écart**            | Différence entre valeur observée et référence. **Jamais appelé « déficit » ou « carence »** |
| **Règle graduée**    | Composant signature : valeur, fourchette, position                                          |
| **Axe**              | Une des sept dimensions du radar de progression                                             |
| **Filtre de sortie** | Contrôle rejetant toute formulation prescriptive du modèle                                  |
| **Plancher**         | Seuil de sécurité non contournable                                                          |

---

## 6. Ce que Claude Code doit demander avant de trancher

Ne jamais décider seul sur :

- Toute formulation destinée à l'utilisateur touchant à la nutrition ou à la santé
- L'ajout d'une fonctionnalité hors périmètre
- Un changement de schéma après la première mise en production
- L'ajout d'une dépendance lourde ou d'un service tiers
- L'installation d'un plugin ou d'un serveur MCP non listé dans `08-workflow.md`
- Un arbitrage entre rapidité et conformité — **la conformité gagne, mais l'utilisateur est informé du coût**
- Toute valeur de référence nutritionnelle absente de `nutrient_refs`

**En cas de doute sur une règle de sécurité ou de conformité : s'arrêter et demander.** Une question coûte cinq minutes, une violation coûte le projet.
