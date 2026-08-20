# 03 — Modèle de données

> **En-tête, section RLS et section Migrations réécrits le 20/08/2026.** La version précédente
> annonçait « PostgreSQL via Supabase », fondait la sécurité du produit sur les seules politiques
> RLS et rangeait les migrations dans `supabase/migrations/`. Les décisions **D9, D14, D15 et
> D17** du 19/08 ont remplacé cette pile — voir `docs/decisions.md`. **Le schéma SQL lui-même
> n'a pas été touché** : table par table, il reste valable. Deux points ne sont pas tranchables
> depuis les décisions existantes ; ils sont signalés en fin de document plutôt que devinés ici.

PostgreSQL **managé chez OVHcloud** — **D15**. Fournisseur de droit européen, donc hors de portée du _CLOUD Act_ américain : pour des données de santé au sens de l'article 9, c'est l'argument de conformité le plus solide. `docs/01-conformite.md` § 4 fait foi sur ce point et prime sur ce document.

**L'API est le seul chemin vers les données — D9.** Le client ne parle plus à PostgreSQL : il parle au backend ASP.NET Core, qui seul détient la chaîne de connexion. L'autorisation — _cet utilisateur a-t-il le droit de lire cette ligne ?_ — se décide désormais dans `Palier.Application`, cas d'usage par cas d'usage.

**RLS reste activée sur chaque table dès sa création, en défense en profondeur.** Le motif d'origine n'a pas bougé : développer sans RLS et l'ajouter avant la mise en production expose la base entière le jour de l'ouverture. Ce que le changement d'architecture déplace — et ce qu'il ne déplace pas — est traité à la section RLS.

---

## Principes

- Relationnel, pas un document JSON par utilisateur. Le volume par groupe musculaire et les courbes par exercice deviennent des requêtes SQL triviales
- Toute table porte `owner_id` ou remonte à un propriétaire par jointure
- Horodatage en `timestamptz`, toujours
- Les valeurs de référence sont en base, jamais en dur dans le code

---

## Schéma

> **`auth.users` est un reste de Supabase, et il n'est pas réécrit ici.** Le schéma `auth` était
> créé par Supabase Auth. La décision **D17** le remplace par ASP.NET Identity, dont la table
> d'utilisateurs est `AspNetUsers`. Les **14** clauses `references auth.users` ci-dessous sont
> conservées telles quelles : le nom de la table est décidé, le **type de sa clé** ne l'est pas
> — voir « Ce qui n'est pas tranché ici », en fin de document. Une substitution faite avant cet
> arbitrage devrait être refaite.

```sql
-- ============ PROFIL ============
create table profiles (
  id                uuid primary key references auth.users on delete cascade,
  display_name      text,
  sex               text check (sex in ('m','f')),
  birth_date        date,
  height_cm         numeric,
  activity_profile  text,             -- 'bureau_faible'|'bureau_modere'|'mixte'|'debout'
  manual_steps      int,              -- saisie manuelle optionnelle
  locale            text default 'fr',
  onboarded_at      timestamptz,
  created_at        timestamptz default now()
);

-- Contraintes physiques déclarées (cervicale, lombaire, épaule, genou...)
create table user_constraints (
  id          uuid primary key default gen_random_uuid(),
  owner_id    uuid not null references auth.users on delete cascade,
  region      text not null,
  severity    text check (severity in ('leger','modere','strict')),
  note        text,
  declared_at timestamptz default now()
);

-- ============ EXERCICES ============
create table exercises (
  id                 uuid primary key default gen_random_uuid(),
  name               text not null,
  equipment          text,
  primary_muscles    text[] not null,
  secondary_muscles  text[] default '{}',
  is_unilateral      boolean default false,
  default_increment  numeric default 2.5,
  -- régions contre-indiquées : filtrage automatique
  contraindicated_for text[] default '{}',
  is_custom          boolean default false,
  owner_id           uuid references auth.users on delete cascade
);

-- ============ PROGRAMMES ============
create table programs (
  id         uuid primary key default gen_random_uuid(),
  owner_id   uuid not null references auth.users on delete cascade,
  name       text not null,
  is_active  boolean default true,
  created_at timestamptz default now()
);

create table program_days (
  id         uuid primary key default gen_random_uuid(),
  program_id uuid not null references programs on delete cascade,
  label      text not null,
  position   int  not null
);

create table program_exercises (
  id              uuid primary key default gen_random_uuid(),
  program_day_id  uuid not null references program_days on delete cascade,
  exercise_id     uuid not null references exercises,
  position        int  not null,
  target_sets     int  not null,
  target_reps_min int  not null,
  target_reps_max int,
  target_rir      int  not null,
  rest_seconds    int  not null,
  note            text
);

-- ============ SÉANCES ============
create table workouts (
  id              uuid primary key default gen_random_uuid(),
  owner_id        uuid not null references auth.users on delete cascade,
  program_day_id  uuid references program_days,
  started_at      timestamptz not null default now(),
  ended_at        timestamptz,
  sleep_hours     numeric,
  energy_1_5      int check (energy_1_5 between 1 and 5),
  note            text
);

create table sets (
  id          uuid primary key default gen_random_uuid(),
  workout_id  uuid not null references workouts on delete cascade,
  exercise_id uuid not null references exercises,
  set_index   int  not null,
  weight_kg   numeric,
  reps        int,
  rir         int,
  is_warmup   boolean default false,
  logged_at   timestamptz not null default now()
);

create table exercise_feedback (
  id          uuid primary key default gen_random_uuid(),
  workout_id  uuid not null references workouts on delete cascade,
  exercise_id uuid not null references exercises,
  rating      text check (rating in ('good','meh','pain'))
);

-- ============ CORPS ============
create table body_weight (
  id          uuid primary key default gen_random_uuid(),
  owner_id    uuid not null references auth.users on delete cascade,
  measured_on date not null,
  weight_kg   numeric not null,
  unique (owner_id, measured_on)
);

create table measurements (
  id          uuid primary key default gen_random_uuid(),
  owner_id    uuid not null references auth.users on delete cascade,
  measured_on date not null,
  arm_cm numeric, chest_cm numeric, waist_cm numeric, thigh_cm numeric
);

-- Progression, avatar et mobilité : voir docs/10-progression.md
-- Files de synchronisation et signalements : voir docs/11-qualite.md

create table cardio_sessions (
  id           uuid primary key default gen_random_uuid(),
  owner_id     uuid not null references auth.users on delete cascade,
  performed_at timestamptz not null default now(),
  minutes      int not null,
  modality     text
);

-- ============ NUTRITION ============
-- Catalogue : OpenFoodFacts, CIQUAL, NUBEL, ou saisie utilisateur
create table foods (
  id           uuid primary key default gen_random_uuid(),
  source       text not null,             -- 'off' | 'ciqual' | 'nubel' | 'user'
  source_ref   text,                      -- code-barres ou identifiant source
  name         text not null,
  brand        text,
  per_100      jsonb not null,            -- { "energy_kcal":..., "protein_g":..., "zinc_mg":... }
  is_custom    boolean default false,
  owner_id     uuid references auth.users on delete cascade
);

create table supplements (
  id           uuid primary key default gen_random_uuid(),
  owner_id     uuid not null references auth.users on delete cascade,
  name         text not null,
  brand        text,
  per_unit     jsonb not null,            -- doses par gélule/dose
  unit_label   text,                      -- 'gélule', 'dose', 'ml'
  photo_path   text,                      -- étiquette d'origine, traçabilité
  created_at   timestamptz default now()
);

create table intake_entries (
  id            uuid primary key default gen_random_uuid(),
  owner_id      uuid not null references auth.users on delete cascade,
  consumed_on   date not null,
  meal          text,                     -- 'matin' | 'midi' | 'soir' | 'collation'
  food_id       uuid references foods,
  supplement_id uuid references supplements,
  quantity      numeric not null,
  unit          text not null,            -- 'g' | 'ml' | 'unité'
  entry_method  text,                     -- 'search' | 'barcode' | 'photo' | 'manual'
  logged_at     timestamptz default now(),
  check (num_nonnulls(food_id, supplement_id) = 1)
);

-- ============ HYDRATATION ============
create table hydration_entries (
  id          uuid primary key default gen_random_uuid(),
  owner_id    uuid not null references auth.users on delete cascade,
  consumed_on date not null,
  volume_ml   int not null check (volume_ml > 0),
  beverage    text default 'eau',      -- 'eau'|'cafe'|'the'|'bouillon'|'autre'
  water_ratio numeric default 1.0,     -- part comptabilisée
  logged_at   timestamptz default now()
);

create table hydration_containers (
  id          uuid primary key default gen_random_uuid(),
  owner_id    uuid not null references auth.users on delete cascade,
  label       text not null,
  volume_ml   int not null,
  position    int not null
);

create index on hydration_entries (owner_id, consumed_on);

-- ============ RÉFÉRENCES ============
-- Valeurs EFSA. En base, jamais en dur.
create table nutrient_refs (
  nutrient      text primary key,         -- 'zinc_mg', 'protein_g'...
  label_fr      text not null,
  label_en      text not null,
  unit          text not null,
  ai_male       numeric,                  -- apport adéquat
  ai_female     numeric,
  ul            numeric,                  -- limite haute, NULL si inexistante
  per_kg        boolean default false,
  source        text not null,
  source_year   int
);

-- Objectifs de l'utilisateur : pré-remplis, modifiables
create table user_targets (
  id           uuid primary key default gen_random_uuid(),
  owner_id     uuid not null references auth.users on delete cascade,
  nutrient     text not null references nutrient_refs,
  target_value numeric not null,
  is_custom    boolean default false,     -- true si modifié par l'utilisateur
  set_at       timestamptz default now(),
  unique (owner_id, nutrient)
);
```

---

## RLS — défense en profondeur, non plus ligne unique

> **Réécrit le 20/08/2026.** Cette section fondait la sécurité du produit sur les seules
> politiques du moteur. C'était juste tant que le client parlait directement à PostgreSQL : RLS
> était alors la seule barrière, et elle vivait là où un oubli ne peut pas la contourner. La
> décision **D9** interpose un backend. **RLS ne disparaît pas : elle change de rôle.** Les
> politiques écrites plus bas sont inchangées.

**Ce que RLS protège encore.** Elle est la dernière barrière quand la première a cédé, dans trois familles de cas :

- **un accès direct à la base**, qui ne passe pas par l'API — outil d'administration, restauration de sauvegarde, identifiants fuités, tâche d'exploitation lancée à la main sur le VPS ;
- **une erreur de l'API** — un filtre sur le propriétaire oublié dans une requête EF Core, un cas d'usage nouveau écrit sans ce filtre, une jointure qui élargit le résultat sans que personne le voie ;
- **une injection SQL ou une requête brute** (`FromSqlRaw`, Dapper) qui échapperait au filtrage de la couche applicative.

Ces trois familles ont ceci de commun qu'**aucun test d'API ne les voit**. C'est la raison pour laquelle la ligne est conservée alors qu'elle n'est plus le chemin nominal.

**À une condition, sans laquelle elle ne mord pas :** l'API se connecte avec un **rôle applicatif restreint**, jamais avec le propriétaire de la base ni un rôle `BYPASSRLS` — un propriétaire de table ignore les politiques de sa propre table. C'est la première chose à éprouver, avant les politiques elles-mêmes (`docs/superpowers/specs/2026-08-19-architecture-backend-csharp-design.md` § 6).

**Ce qu'elle ne protège plus à elle seule.** Tout le reste, c'est-à-dire l'essentiel de l'autorisation réelle du produit : les rôles, l'accès d'un administrateur au support, la lecture du catalogue public, un partage éventuel, et les règles de `docs/01-conformite.md` — planchers non contournables, filtre de sortie, escalade. Une politique « `owner_id` égale l'utilisateur courant » ne sait rien dire de ces cas. **Tenir RLS pour suffisante serait désormais une faute de conception**, alors que c'était la bonne réponse avant D9. L'autorisation applicative est une responsabilité du backend, portée par le comportement d'autorisation du pipeline, et elle se teste là.

**Les tests de politiques restent exigés, et pèsent plus qu'avant.** `docs/08-workflow.md` § 6 demande que la politique naisse dans la même migration que la table, et qu'un test vérifie qu'un utilisateur A ne lit jamais une ligne de B. Cette exigence ne s'allège pas, pour une raison mécanique : le chemin nominal passant par l'API, **plus aucun test fonctionnel ne franchira RLS**. Sans test dédié — lancé contre la base, avec une identité, hors du backend — une politique cassée resterait verte jusqu'au jour où elle devait servir. Une défense en profondeur que rien n'éprouve est une défense qu'on croit avoir.

`docs/decisions.md` **D9** conserve l'avis donné avant la décision : l'accès direct concentrait la sécurité dans le moteur, où elle ne peut pas être contournée par oubli. Cet avis a été exposé et la décision inverse prise en connaissance de cause. Il n'est pas rouvert ici — il explique pourquoi ces tests comptent double.

### Les politiques

Modèle à appliquer à chaque table possédant `owner_id` :

```sql
alter table <table> enable row level security;

create policy "<table>_own" on <table>
  for all using (auth.uid() = owner_id)
  with check (auth.uid() = owner_id);
```

Pour `sets` et `exercise_feedback`, qui n'ont pas de `owner_id` direct :

```sql
create policy "sets_own" on sets for all
  using (exists (
    select 1 from workouts w
    where w.id = sets.workout_id and w.owner_id = auth.uid()
  ));
```

`exercises` et `foods` sont lisibles par tous quand `is_custom = false`, et restreints au propriétaire sinon. `nutrient_refs` est en lecture publique, écriture interdite via l'API.

> **`auth.uid()` n'existe plus, et rien ne la remplace encore.** Cette fonction était fournie par
> Supabase Auth : elle lisait la revendication du jeton présenté **au moteur**. Avec un backend
> qui se connecte par une chaîne de connexion ordinaire (**D14**), PostgreSQL ne voit plus un
> utilisateur mais un rôle applicatif. **Aucune décision ne dit comment l'identité lui parvient**,
> et ce document ne le tranche pas. Les trois occurrences ci-dessus sont laissées en l'état pour
> que la substitution soit faite une fois, en connaissance de cause — voir « Ce qui n'est pas
> tranché ici ».

---

## Index

```sql
create index on sets (exercise_id, logged_at desc);
create index on sets (workout_id);
create index on workouts (owner_id, started_at desc);
create index on intake_entries (owner_id, consumed_on);
create index on body_weight (owner_id, measured_on);
create index on foods (source, source_ref);
```

---

## Vues utiles

- `weekly_volume` — séries dures par muscle et par semaine, en dépliant `primary_muscles` (coefficient 1) et `secondary_muscles` (coefficient 0,5)
- `daily_intake` — agrégation par nutriment et par jour, **aliments et compléments confondus**. C'est la vue centrale du produit
- `exercise_progression` — meilleure série par exercice et par séance, avec force estimée

**Ces trois vues ne sont pas générées par EF Core — D14.** Elles sont écrites en SQL dans la migration qui les introduit, par `migrationBuilder.Sql(...)`, avec le `Down` qui les supprime. `daily_intake`, « la vue centrale du produit », est le premier objet concerné.

---

## Migrations

> **Réécrit le 20/08/2026.** Le mécanisme change, l'intention ne change pas : le schéma vit dans
> le dépôt, versionné, et ne se modifie jamais à la main sur un serveur.

Toute évolution de schéma passe par une migration EF Core versionnée dans le dépôt — **D14** : `dotnet ef migrations add <Nom>`, qui produit un fichier dans `back/Palier.Infrastructure/Migrations/`. **Aucune modification manuelle du schéma en production**, par quelque interface ou console que ce soit. C'est la règle d'origine mot pour mot ; seul l'outil a changé, et `supabase/migrations/` n'existe plus.

**Ce qu'EF Core ne pilote pas, et qui doit donc être écrit à la main dans la migration — D14 :**

| Objet                                             | Comment                                                   |
| ------------------------------------------------- | --------------------------------------------------------- |
| Les trois vues de la section précédente           | `migrationBuilder.Sql(...)`, avec le `Down` correspondant |
| `enable row level security` et les politiques RLS | idem                                                      |
| Les contraintes `CHECK` du schéma ci-dessus       | idem                                                      |

Le SQL de ce document ne disparaît pas : **il migre dans les migrations**. Le mode de défaillance à surveiller est précis — une migration générée sans ces trois blocs produit un schéma qui compile, qui démarre, et qui n'applique **ni les vues, ni RLS, ni les bornes**. Rien ne le signale au démarrage.

---

## Ce qui n'est pas tranché ici

Deux points restent ouverts au 20/08/2026. Aucune décision de `docs/decisions.md` ne permet de les fermer, et `CLAUDE.md` § 6 interdit de le faire en silence.

**1. Comment le moteur connaît l'utilisateur, maintenant qu'`auth.uid()` n'existe plus.** Sans réponse, les politiques écrites plus haut ne sont pas applicables telles quelles. Deux mécanismes existent, ils ne sont pas équivalents et **ni l'un ni l'autre n'est retenu ici** : un paramètre de session posé par le backend à chaque requête, lu par la politique ; ou un rôle PostgreSQL par utilisateur. Le premier est simple mais ne vaut que si le backend ne peut pas oublier de le poser ; le second ne passe pas à l'échelle d'un service grand public. À arbitrer avant le lot 4.

**2. La table d'utilisateurs et le type de sa clé.** **D17** décide ASP.NET Identity, donc `AspNetUsers`. Elle ne dit pas si l'identifiant reste un `uuid` — ce qu'exige `IdentityUser<Guid>` — ou devient le `text` que la configuration par défaut d'Identity produit. Les 14 clauses `references auth.users` du schéma en dépendent, ainsi que tous les `owner_id`. À arbitrer avant la première migration.

**Signalé sans y avoir touché — trois tables manquent à ce document**, exigées ailleurs dans le dossier et repérées par la spec d'architecture du 19/08 : le journal versionné des libellés (`09-comptes.md` § 6), le journal des appels au modèle (`06-ia.md` § 2 et `13-juridique.md` § 2) et les tables d'identité ASP.NET (conséquence de **D17**). Les écrire est un travail de schéma, hors du périmètre de cette reprise.
