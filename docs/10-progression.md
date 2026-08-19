# 10 — Système de progression

L'objectif : rendre l'évolution lisible et engageante, dans l'esprit d'un jeu de rôle, **sans reproduire les mécaniques qui rendent les applications de fitness nocives**.

Ce document arbitre explicitement la tension avec `01-conformite.md`. Cet arbitrage n'est pas négociable et prime sur toute considération d'engagement.

---

## 1. La règle qui gouverne tout

**L'avatar ne représente jamais la morphologie corporelle réelle de l'utilisateur.**

Un personnage qui grossit, maigrit ou s'affine selon le poids est un déclencheur direct de dysmorphie et de troubles du comportement alimentaire. C'est vrai dans les deux sens — voir son avatar « grossir » pendant une prise de masse volontaire et parfaitement saine suffit à faire abandonner un programme et à installer une surveillance anxieuse du corps.

L'avatar évolue par **équipement, posture, aura et effets visuels**. Comme dans un jeu de rôle : le personnage gagne de l'armure, pas de la graisse. La morphologie est choisie librement à la création et ne change plus jamais, sauf action explicite de l'utilisateur.

---

## 2. Mécaniques exclues — vue d'ensemble

Le détail par mécanique figure en section 6. Récapitulatif :

| Exclu | Raison |
|---|---|
| Série de jours consécutifs | Punit la maladie, les vacances, la vie |
| Régression visible après une pause | Transforme l'app en source de culpabilité |
| Gain lié à une restriction calorique | Récompense directe du comportement à risque |
| Classement, comparaison entre utilisateurs | Comparaison sociale, terrain des TCA |
| État « malade », « faible », « gros » | L'app ne diagnostique pas — interdit par `01-conformite.md` |
| Perte de points | On ne retire jamais ce qui a été acquis |
| Notification de série interrompue | Interdit, voir `09-comptes.md` |

**Ce qui reste, et qui suffit :** la progression visible, la lisibilité immédiate, l'envie d'ouvrir l'application.

---

## 3. Les sept axes

Chaque axe se calcule uniquement à partir de données réelles. Aucun n'est déclaratif.

| Axe | Calcul | Fenêtre |
|---|---|---|
| **Force** | Moyenne des 1RM estimés sur les mouvements socles, rapportée au poids de corps, normalisée par sexe et âge | 8 semaines |
| **Endurance musculaire** | Volume hebdomadaire total + capacité en séries de 15+ répétitions | 4 semaines |
| **Endurance cardio** | Minutes en zone 2 par semaine, rapportées à la cible de 60-90 min | 4 semaines |
| **Équilibre** | Ratio tirage/poussée, symétrie gauche/droite sur les unilatéraux, nombre de groupes musculaires dans la fourchette 10-20 | 4 semaines |
| **Récupération** | Sommeil déclaré, énergie, proportion de ressentis positifs | 3 semaines |
| **Nutrition** | Pourcentage de nutriments dans leur fourchette, **jamais** l'écart calorique | 4 semaines |
| **Constance** | Séances effectuées sur séances prévues, en moyenne glissante | 8 semaines |

**La dextérité est écartée** : rien ne la mesure en musculation. L'inclure produirait une valeur inventée.

**La mobilité mériterait d'y figurer** mais aucune donnée ne l'alimente aujourd'hui. Deux options à trancher : auto-tests trimestriels (flexion avant, rotation thoracique, squat profond, test épaule) notés par l'utilisateur, ou report à une version ultérieure. Recommandation : auto-tests, ils coûtent peu et enrichissent réellement le profil.

### Échelle

De 1 à 100 par axe, avec un niveau global dérivé. Progression logarithmique : rapide au début, lente ensuite — c'est la réalité de l'adaptation à l'entraînement, et ça évite l'effet plafond.

**Un axe sans données affiche « données insuffisantes », jamais zéro.** Un utilisateur qui ne fait pas de cardio n'est pas mauvais en cardio : il n'a pas de données.

---

## 4. États

Six états dérivés, formulés sans jugement.

| État | Déclencheur |
|---|---|
| En progression | Charges ou volume en hausse sur 3 semaines |
| En maintien | Stable, tout dans les fourchettes |
| Charge élevée | Volume haut + énergie basse + sommeil court, simultanément |
| Apports sous la fourchette | Plusieurs jours consécutifs sous les références |
| En pause | Aucune séance depuis 10 jours |
| Données incomplètes | Saisie insuffisante pour conclure |

« Charge élevée » et « apports sous la fourchette » sont des constats chiffrés, présentés avec les données qui les fondent. Jamais de recommandation attachée — la règle informer/prescrire s'applique intégralement ici.

**Aucun état n'est négatif.** « En pause » est neutre et le reste.

---

## 5. V1 — La fiche de personnage

Un radar seul ne suffit pas. La V1 livre une **fiche de personnage complète**, consultable avec plaisir, qui ne parle jamais du corps — uniquement de ce que le corps fait.

| Bloc | Contenu |
|---|---|
| **Identité** | Nom choisi, niveau, saison en cours, temps d'entraînement cumulé |
| **Radar** | Heptagone des 7 axes, tracé fantôme de l'état d'il y a 4 semaines superposé |
| **Attributs** | Chaque axe déplié : valeur, évolution, et **les données qui l'ont fait bouger** |
| **Records** | Meilleure charge par mouvement, plus gros tonnage, plus longue série, plus longue sortie |
| **Palier** | Ce qui fera monter le prochain axe, exprimé en données concrètes |
| **Chronologie** | Les jalons franchis, dans l'ordre, avec leur date |
| **Quêtes** | Les objectifs actifs de la saison |

Direction visuelle conforme à `02-design.md` : fond ardoise, display en Barlow Condensed capitales, toutes les valeurs en monospace, palette des disques olympiques pour les états. Le radar se dessine en 400 ms à l'ouverture — c'est la seule animation expressive de l'écran.

---

## 6. Système de fidélisation

### Ce que dit la recherche

**La gamification fonctionne, et ce n'est pas une opinion.** Une méta-analyse de 16 essais randomisés portant sur 2 407 participants trouve un effet petit à moyen sur l'activité physique (Hedges g = 0,42), maintenu face à des groupes contrôle actifs et persistant au suivi. Une méta-analyse de 2024 dans *eClinicalMedicine* confirme des améliorations sur l'activité, le poids, l'IMC et le tour de taille. Les essais BE FIT et STEP UP (JAMA Internal Medicine) mesurent 920 à 950 pas quotidiens supplémentaires par rapport aux contrôles.

Les applications sans gamification perdent l'essentiel de leurs utilisateurs vers la semaine 20.

### La leçon Fitocracy

Fitocracy avait exactement ce système : XP par séance, niveaux, quêtes, communauté. Un million d'utilisateurs, plus d'engagement que Twitter à son pic. L'application est aujourd'hui morte.

**La cause n'est pas la gamification, c'est sa staticité.** XP, niveaux et quêtes étaient brillants mais figés : ils n'ont jamais évolué pour accompagner de nouveaux styles d'entraînement, une progression de long terme ou des cycles saisonniers. Résultat : la motivation plafonnait tôt, les vétérans n'avaient plus de raison de monter, et les débutants n'avaient pas de rampe d'accès adaptée.

Second problème : les points de Fitocracy récompensaient le **tonnage absolu**, ce qui favorise mécaniquement les gros gabarits et décourage les débutants et les femmes.

**Trois conséquences de conception, non négociables :**
1. L'XP mesure l'effort **relatif à soi**, jamais une valeur absolue
2. Le système ne plafonne jamais — saisons et progression infinie
3. Les quêtes sont **adaptatives**, générées depuis les données réelles

### XP

L'XP récompense le comportement, jamais le résultat corporel.

| Source | Attribution |
|---|---|
| Séance terminée | Base fixe, identique pour tous |
| Progression sur un exercice | Proportionnelle au gain **relatif à son propre historique** |
| Volume dans la fourchette cible | Bonus si le groupe musculaire entre dans 10-20 séries |
| Ratio tirage/poussée équilibré | Bonus hebdomadaire |
| Journée alimentaire complète | Base fixe — la complétion, jamais le contenu |
| Nutriment ramené dans sa fourchette | Bonus, y compris **vers le haut** |
| Auto-test de mobilité | Base fixe, trimestriel |
| Ressenti renseigné | Petit bonus — on récompense la donnée qui protège |

**Jamais d'XP pour :** un déficit calorique, une perte de poids, une restriction, une séance supplémentaire au-delà du volume recommandé, une série de jours consécutifs.

Un débutant qui passe de 20 à 22 kg gagne autant qu'un pratiquant confirmé qui passe de 100 à 110 kg. C'est le même progrès relatif.

### Niveaux et saisons

Progression logarithmique, sans plafond. Au-delà du niveau 50, le système bascule en **saisons** de 8 à 12 semaines, calées sur les blocs d'entraînement.

Chaque saison apporte un thème, un jeu de quêtes renouvelé et des débloquables cosmétiques propres. C'est la réponse directe au plafonnement qui a tué Fitocracy : il y a toujours quelque chose devant, sans jamais retirer ce qui a été acquis.

Une saison manquée ne pénalise rien. Les récompenses passées restent disponibles ultérieurement.

### Quêtes adaptatives — le différenciateur

Les quêtes sont **générées depuis les données réelles de l'utilisateur**, dans des limites déterministes fixées par le code. Le modèle propose la formulation, le code valide la faisabilité et la sécurité.

| Type | Exemple |
|---|---|
| Progression | « Passer 4 séries de tirage horizontal à 32 kg cette semaine » |
| Équilibre | « Ramener les ischios dans la fourchette 10-20 séries » |
| Couverture | « Compléter 5 journées alimentaires » |
| Exploration | « Essayer une variante de l'exercice noté bof trois fois » |
| Technique | « Enregistrer le RIR sur toutes les séries de la semaine » |
| Récupération | « Poser un jour de repos après trois séances » |

**Règles :** toujours atteignables au vu de l'historique, jamais liées à une restriction, jamais plus de trois actives, expiration silencieuse sans pénalité, et refus possible d'une quête proposée.

Une quête qui pousserait au-delà du volume recommandé ou sous un plancher de sécurité est bloquée par le code avant d'être affichée.

### Débloquables

**Cosmétiques uniquement.** Tenues et équipements de l'avatar, environnements de fond, cadres de fiche, titres, palettes alternatives.

Aucune fonctionnalité n'est jamais verrouillée derrière une progression : verrouiller une fonction utile revient à punir celui qui en a le plus besoin.

### Rappels de progression

À la place des séries de jours et des relances culpabilisantes : un **récapitulatif hebdomadaire** livré le même jour chaque semaine, montrant ce qui a bougé, les quêtes accomplies et l'axe qui a le plus progressé. Neutre quand la semaine a été vide.

### Exclu définitivement

| Mécanique | Raison |
|---|---|
| Classements, comparaison entre utilisateurs | Comparaison sociale, terrain des TCA |
| Séries de jours consécutifs | Punit la maladie et les vacances |
| Perte de niveau ou d'XP | On ne retire jamais l'acquis |
| Récompense liée au poids ou au déficit | Récompense directe du comportement à risque |
| Notification de relance culpabilisante | Voir `09-comptes.md` |
| Monnaie achetable contre de l'argent réel | Le produit est un abonnement, pas un jeu à microtransactions |

---

## 7. V2 — L'avatar, en deux modes

La recherche empirique ne dit pas « jamais d'avatar corporel ». Elle dit que **le résultat dépend entièrement du cadrage et de la personne**.

Une étude d'exposition à un avatar anthropométrique 3D relève une augmentation de l'écart perçu d'image corporelle et une baisse de la satisfaction corporelle, effets **intensifiés chez les femmes**, tandis que **les hommes montraient une intention accrue de s'entraîner** ; les auteurs appellent à la prudence. Une seconde étude montre qu'un programme d'acceptation corporelle proposé **avant** l'exposition sert de tampon psychologique et produit ensuite de l'auto-acceptation.

Et l'effet Proteus renverse l'intuition : en réalité virtuelle, les participants incarnant un avatar musclé percevaient **moins d'effort et avaient une fréquence cardiaque plus basse** qu'en incarnant un avatar peu musclé et plus gras. **L'avatar aspirationnel motive davantage que l'avatar réaliste.**

### Mode Archétype — par défaut

Représentation stylisée dont la carrure suit les **axes de performance**, jamais le poids : la force élargit les épaules, l'endurance affine la posture, l'équilibre redresse le maintien. Évolution par équipement, aura et effets.

Cohérent avec l'effet Proteus, sans aucun risque.

### Mode Miroir — activé explicitement

Pour ceux qui le veulent, avec quatre garde-fous :

1. **Construit sur les mensurations réelles saisies** — taille, bras, cuisse, poitrine — jamais sur un pourcentage de graisse extrapolé. Les méthodes accessibles de mesure du taux de graisse ont 3 à 8 points d'erreur : un avatar bâti dessus n'est pas le corps de l'utilisateur, c'est une fiction assez réaliste pour déclencher la comparaison et trop fausse pour être honnête
2. **Écran de cadrage à l'activation**, expliquant ce que la représentation montre et ne montre pas — le tampon psychologique documenté
3. **Aucune comparaison imposée.** Le côte-à-côte existe, l'utilisateur va le chercher
4. **Désactivation immédiate**, sans perte de données, retour à l'Archétype

Détection silencieuse : consultation répétée du mode Miroir combinée à une restriction alimentaire → retour automatique en Archétype et proposition d'orientation.

Aucun élément dénudé, aucune accentuation de zones corporelles, aucune animation de transformation. Ready Player Me ou équivalent pour la base technique.

---

## 8. Structure de données

```sql
create table progression_snapshots (
  id            uuid primary key default gen_random_uuid(),
  owner_id      uuid not null references auth.users on delete cascade,
  computed_at   timestamptz not null default now(),
  axes          jsonb not null,   -- { "force": 42, "endurance_musc": 55, ... }
  state         text not null,    -- état dérivé
  inputs        jsonb not null,   -- valeurs sources, pour audit et explication
  unique (owner_id, computed_at)
);

create table avatar_config (
  owner_id      uuid primary key references auth.users on delete cascade,
  body_type     text,             -- choisi une fois
  presentation  text,
  unlocked      text[] default '{}',
  equipped      jsonb default '{}'
);

create table xp_events (
  id            uuid primary key default gen_random_uuid(),
  owner_id      uuid not null references auth.users on delete cascade,
  occurred_at   timestamptz not null default now(),
  source        text not null,   -- 'workout'|'progression'|'volume'|'nutrition_day'|'quest'...
  amount        int not null check (amount > 0),  -- jamais négatif
  ref_id        uuid,
  detail        jsonb
);

create table quests (
  id            uuid primary key default gen_random_uuid(),
  owner_id      uuid not null references auth.users on delete cascade,
  season_id     uuid references seasons,
  kind          text not null,   -- 'progression'|'equilibre'|'couverture'|'exploration'|'technique'|'recuperation'
  title         text not null,
  target        jsonb not null,  -- critère vérifiable par le code
  status        text not null default 'active',  -- 'active'|'done'|'expired'|'declined'
  created_at    timestamptz default now(),
  completed_at  timestamptz
);

create table seasons (
  id            uuid primary key default gen_random_uuid(),
  label         text not null,
  starts_on     date not null,
  ends_on       date not null,
  theme         text
);

create table unlockables (
  id            uuid primary key default gen_random_uuid(),
  code          text unique not null,
  category      text not null,   -- 'tenue'|'environnement'|'cadre'|'titre'|'palette'
  season_id     uuid references seasons,
  requirement   jsonb not null
);

create table mobility_tests (
  id            uuid primary key default gen_random_uuid(),
  owner_id      uuid not null references auth.users on delete cascade,
  tested_on     date not null,
  forward_fold  int, thoracic_rotation int, deep_squat int, shoulder int
);
```

`inputs` est indispensable : un utilisateur doit pouvoir demander pourquoi son axe force a bougé, et l'assistant doit pouvoir l'expliquer à partir de valeurs réelles.

Recalcul à chaque séance enregistrée et une fois par jour.
