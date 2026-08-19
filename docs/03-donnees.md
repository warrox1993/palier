# 03 — Modèle de données

PostgreSQL via Supabase. **Région Francfort ou Paris, choisie à la création du projet — ce choix est irréversible.**

**RLS activé sur chaque table dès sa création.** L'erreur classique consiste à développer sans RLS et à l'ajouter avant la mise en production ; le jour de l'ouverture, la base entière est exposée.

---

## Principes

- Relationnel, pas un document JSON par utilisateur. Le volume par groupe musculaire et les courbes par exercice deviennent des requêtes SQL triviales
- Toute table porte `owner_id` ou remonte à un propriétaire par jointure
- Horodatage en `timestamptz`, toujours
- Les valeurs de référence sont en base, jamais en dur dans le code

---

## Schéma

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

## RLS

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

---

## Migrations

Toute évolution de schéma passe par un fichier de migration versionné dans `supabase/migrations/`. Aucune modification manuelle via l'interface Supabase après la première mise en production.
