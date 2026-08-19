# 16 — Structure de projet et conventions

Claude Code n'invente pas les conventions : elles doivent être écrites. Ce document les fixe.

---

## 1. Arborescence

```
/
├── CLAUDE.md
├── README.md
├── docs/
├── .env.example                 # jamais .env
├── .github/workflows/
│   ├── ci.yml                   # lint, types, tests, build, a11y
│   └── deploy.yml
├── supabase/
│   ├── migrations/              # horodatées, versionnées
│   ├── seed/
│   │   ├── 01-nutrient-refs.sql # références EFSA
│   │   ├── 02-exercises.sql     # catalogue
│   │   ├── 03-foods.sql         # échantillon CIQUAL
│   │   └── 04-programs.sql      # programmes modèles
│   └── tests/                   # tests de politiques RLS
├── src/
│   ├── app/                     # routes
│   ├── features/                # par domaine métier
│   │   ├── workout/
│   │   ├── nutrition/
│   │   ├── supplements/
│   │   ├── hydration/
│   │   ├── progression/
│   │   ├── assistant/
│   │   └── account/
│   ├── core/                    # modules purs, testés, sans dépendance UI
│   │   ├── energy.ts            # TDEE, Mifflin, Katch, adaptatif
│   │   ├── macros.ts
│   │   ├── micros.ts            # agrégation, comparaison aux UL
│   │   ├── hydration.ts
│   │   ├── volume.ts            # séries par muscle
│   │   ├── progression.ts       # 1RM, suggestion, plateau
│   │   ├── xp.ts
│   │   └── guards.ts            # planchers de sécurité
│   ├── llm/
│   │   ├── provider.ts          # interface
│   │   ├── anthropic.ts
│   │   ├── google.ts
│   │   ├── router.ts            # routage par tâche
│   │   └── output-filter.ts     # filtre prescriptif
│   ├── ui/                      # composants du système de design
│   ├── lib/                     # supabase, dexie, i18n, sync
│   └── locales/
│       ├── fr.json
│       └── en.json
└── tests/
    ├── unit/
    ├── integration/
    ├── e2e/
    └── compliance/              # suite bloquante
```

**Règle structurante :** `core/` ne contient que des fonctions pures, sans accès réseau ni base ni React. C'est ce qui rend les calculs testables à 100 % et auditables par le diététicien.

---

## 2. Conventions de code

| Sujet | Règle |
|---|---|
| Fichiers | `kebab-case.ts` |
| Composants | `PascalCase.tsx` |
| Fonctions et variables | `camelCase` |
| Constantes | `SCREAMING_SNAKE_CASE` |
| Tables et colonnes SQL | `snake_case` |
| Types | `PascalCase`, préfixe interdit (`IUser` non) |
| Booléens | `is`, `has`, `can` |
| Fonctions asynchrones | Nom au verbe : `fetchWorkouts`, pas `workoutsData` |
| Tests | `<fichier>.test.ts` à côté du source |
| Commits | Français, impératif : `ajoute le calcul du TDEE adaptatif` |
| Branches | `feat/`, `fix/`, `chore/`, `docs/` |

**Unités : tout est stocké en SI.** Kilogrammes, centimètres, millilitres, grammes, secondes. La conversion en unités impériales se fait à l'affichage uniquement, jamais en base.

**Dates :** `timestamptz` en base, ISO 8601 en transit, formatage localisé à l'affichage seulement.

**Nombres :** jamais de `float` pour une quantité nutritionnelle en base — `numeric`. L'arrondi se fait à l'affichage.

---

## 3. Variables d'environnement

`.env.example` versionné, `.env` jamais. Chaque variable documentée.

```bash
# Supabase
VITE_SUPABASE_URL=
VITE_SUPABASE_ANON_KEY=
SUPABASE_SERVICE_ROLE_KEY=        # serveur uniquement, jamais exposée au client

# Modèles — serveur uniquement
ANTHROPIC_API_KEY=
GOOGLE_API_KEY=
LLM_DEFAULT_PROVIDER=google
LLM_MONTHLY_BUDGET_EUR=

# Stripe
STRIPE_SECRET_KEY=
STRIPE_WEBHOOK_SECRET=
VITE_STRIPE_PUBLISHABLE_KEY=

# Sources de données
OPENFOODFACTS_USER_AGENT=

# Observabilité
SENTRY_DSN=
VITE_ANALYTICS_URL=

# Application
VITE_APP_URL=
VITE_DEFAULT_LOCALE=fr
```

**Aucune clé de modèle ni de service ne doit être accessible côté client.** Tout appel aux modèles passe par une fonction serveur. Une clé préfixée `VITE_` est publique par construction.

---

## 4. Données de départ (seed)

Sans seed, l'application est inutilisable en développement et Claude Code ne peut rien vérifier.

| Fichier | Contenu | Source |
|---|---|---|
| `01-nutrient-refs.sql` | Références et limites hautes, ~40 nutriments | EFSA DRV |
| `02-exercises.sql` | 60 exercices prioritaires puis extension | Rédigé, relu par le kiné |
| `03-foods.sql` | 300 aliments courants | CIQUAL |
| `04-programs.sql` | 9 programmes modèles | Rédigés, relus |

Un compte de démonstration avec 4 semaines de données réalistes est indispensable pour tester les courbes, le radar et le TDEE adaptatif. **Sans historique, la moitié des écrans ne peut pas être vérifiée.**

---

## 5. Glossaire

Vocabulaire partagé entre le code, l'interface et les documents. Les termes anglais restent en anglais dans le code, français dans l'interface.

| Terme | Définition |
|---|---|
| **RIR** | *Reps In Reserve*. Répétitions restantes à la fin d'une série |
| **Série dure** | Série de travail hors échauffement, comptée dans le volume |
| **Volume** | Nombre de séries dures par muscle et par semaine. Primaire = 1, secondaire = 0,5 |
| **Tonnage** | Charge × répétitions cumulées sur une séance |
| **1RM estimé** | Charge maximale théorique, calculée par Epley corrigé du RIR. Jamais testée |
| **Bloc** | Cycle de 4 à 6 semaines suivi d'un allègement |
| **Allègement** | Semaine à volume et charge réduits |
| **Mouvement socle** | Exercice fixe sur un bloc, support de la mesure de progression |
| **UL** | *Tolerable Upper Intake Level*. Limite haute de sécurité EFSA |
| **AI** | *Adequate Intake*. Apport adéquat de référence |
| **TDEE** | Dépense énergétique totale quotidienne |
| **TDEE adaptatif** | Dépense calculée sur les données réelles, remplace la formule dès la semaine 3 |
| **Contrainte** | Limitation physique déclarée par l'utilisateur |
| **Écart** | Différence entre valeur observée et référence. **Jamais appelé « déficit » ou « carence »** |
| **Règle graduée** | Composant signature : valeur, fourchette, position |
| **Axe** | Une des sept dimensions du radar de progression |
| **Filtre de sortie** | Contrôle rejetant toute formulation prescriptive du modèle |
| **Plancher** | Seuil de sécurité non contournable |

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
