# Brief — Tâche 6

> Extrait de `docs/superpowers/plans/2026-08-20-lot-2-socle-donnees.md`. C'est la source unique de tes exigences.
> Les valeurs exactes — code, chemins, chaînes de caractères — se reprennent **verbatim**.

## Contraintes globales

- **Node 24** (`.nvmrc`) et **.NET 10** (`global.json`). **npm** côté front, jamais pnpm ni yarn ; en CI `npm ci`.
- **Branche de travail :** `feat/lot-2-socle-donnees`. Dépôt distant `github.com/warrox1993/palier`, privé, branche par défaut `main`.
- **Messages de commit en français, à l'impératif, minuscule initiale.** Indexer des **chemins nommés** (`git commit -m <msg> -- <fichiers>`), jamais `git add -A` : plusieurs agents peuvent travailler dans le même worktree.
- **Deux assertions par épreuve, sans exception — D19.** Le **code de sortie** _et_ un **motif propre à l'outil**. Un code non nul prouve qu'il s'est passé quelque chose, jamais que l'outil a refusé : Prettier absent rend le code 2, Playwright sans test rend un code non nul, `gitleaks` introuvable rend 1 sous `cmd.exe`. Trois épreuves du lot 1 seraient passées au vert sans rien contrôler.
- **L'exclusion appartient à la commande, jamais au fichier de configuration — D20.** Quatre outils du lot 1 ont porté ce piège : un fichier ignoré par la configuration **reste ignoré même nommé explicitement en argument**. Pour chaque outil nouveau de ce lot, la question se **pose et se mesure** : _un fichier ignoré reste-t-il ignoré quand on le nomme en argument ?_
- **Aucun commentaire dans un fichier de configuration d'outil — D21.** Une clé inconnue peut désactiver la règle entière **en silence** : un `"_note"` a fait passer neuf violations à code 0. Le motif d'un choix vit dans l'épreuve qui le protège. Exception : les formats qui portent nativement des commentaires — YAML, `.mjs`, TOML — **à condition de l'avoir vérifié sur cet outil-là**.
- **Une branche jamais franchie est une branche qui ment.** Chaque épreuve de ce plan doit être **vue rouge** en provoquant sa violation, puis vue verte après le correctif. Une épreuve qui n'a été vue que verte n'a rien prouvé. Et **un réglage accepté sans erreur n'est pas un réglage appliqué** (P11) ; **un fichier correct dont le service est éteint ne protège rien** (D30).
- **Le franchissement en quatre questions :** _tourne-t-il_ · _mord-il_ · _refuse-t-il d'enregistrer à moitié_ · **crie-t-il quand il n'a plus de cible, ou que son fichier manque ?** Le premier test de chaque fichier d'épreuve vérifie que sa cible existe.
- **Aucune version de paquet figée dans les commandes d'installation.** Les versions citées dans le texte sont des **relevés du 20/08/2026**, à revérifier ; les fichiers de verrouillage figent.
- **Toute dépendance ajoutée passe la liste blanche de licences** — MIT, Apache-2.0, BSD, ISC, PostgreSQL (D13), par `node scripts/verifier-licences.mjs`.
- **`Palier.Domain` ne bouge pas.** Aucun calcul de conformité n'entre dans ce lot. La couverture à 100 % ne s'applique qu'à `[Palier.Domain]*` — `back/coverage.runsettings` le filtre, et **il ne faut pas y soumettre le nouveau projet de tests d'intégration**.
- **Aucun secret dans le dépôt.** Le mot de passe local du compose reste en clair — gitleaks l'accepte sous cette forme — mais **une chaîne de connexion complète `Host=…;Password=…` dans un fichier de code est refusée**. Les chaînes vont en `dotnet user-secrets` ou en variable d'environnement, jamais dans `appsettings.Development.json`.
- **Aucune chaîne de caractères en dur** côté front : tout passe par i18next, y compris les erreurs et les états vides.
- **`npm run verify` reste le point d'entrée unique — D25.** Aucun contrôle de ce lot ne crée une seconde liste. Le projet de tests d'intégration entre dans `back/Palier.sln`, donc dans l'étape `back:test`, donc dans `verify` — **sans étape nouvelle**.

### Les deux voies, et le blocage matériel

**Mesuré le 20/08/2026 sur le poste :** `wsl --status` rend « Le Sous-système Windows pour Linux n'est pas installé ». `Microsoft-Windows-Subsystem-Linux` et `Microsoft-Hyper-V` sont en **InstallState 2** (désactivés), seul `VirtualMachinePlatform` est à **1**. Le client `docker` 29.6.2 et `docker compose` v5.3.1 **répondent parfaitement** pendant que le démon est injoignable sur `npipe:////./pipe/dockerDesktopLinuxEngine`.

La tâche 0 exige des **droits administrateur et un redémarrage**. Personne d'autre que le porteur du projet ne peut la faire. Le plan est donc organisé en deux voies, et **la voie sèche se termine sans jamais attendre**.

| Tâche  | Titre court                                                         | Docker                                                                                  | Voie              |
| ------ | ------------------------------------------------------------------- | --------------------------------------------------------------------------------------- | ----------------- |
| **0**  | Prérequis matériel — WSL2 ou Hyper-V                                | c'est elle qui le débloque                                                              | **porteur**       |
| **1**  | Le dossier `db/` et son outillage                                   | non                                                                                     | sèche             |
| **2**  | `db/compose.yaml`, scripts `db:*`, branche « Docker éteint »        | **la branche de refus se franchit maintenant** ; la branche de succès attend la tâche 0 | sèche puis Docker |
| **3**  | Jetons, i18next, `chaine-en-dur` étendue, les trois exceptions Knip | non                                                                                     | sèche             |
| **4**  | Les trois dettes de l'intégration continue                          | non                                                                                     | sèche             |
| **5**  | Le projet de tests d'intégration, les trois rôles, le tag unique    | oui                                                                                     | Docker            |
| **6**  | La migration `SocleInitial`                                         | oui                                                                                     | Docker            |
| **7**  | Accesseurs, politiques, huit épreuves d'isolation                   | oui                                                                                     | Docker            |
| **8**  | La pose de l'identité côté C#, et la garde applicative              | oui                                                                                     | Docker            |
| **9**  | `GET /api/v1/sante`, `back/.env.example`, assertion de démarrage    | oui                                                                                     | Docker            |
| **10** | Sauvegarde et restauration sous `FORCE`                             | oui                                                                                     | Docker            |
| **11** | L'écran d'état, ses quatre états, le parcours au clavier            | oui                                                                                     | Docker            |
| **12** | Remesurer `verify` et arbitrer                                      | oui                                                                                     | Docker            |
| **13** | La reprise documentaire et le journal des décisions                 | non                                                                                     | sèche             |

**Ordre imposé :** 0 (en parallèle, par le porteur) · puis 1 → 2 → 3 → 4 sans attendre quoi que ce soit · puis 5 → 6 → 7 → 8 → 9 → 10 → 11 → 12 dès que la tâche 0 est franchie · puis 13.
Les tâches **3 et 4** sont indépendantes de 1 et 2 : elles peuvent partir en parallèle.

### Ce que ce lot n'écrit pas

Aucune authentification (les tables `AspNet*` sont **créées**, aucune ligne de code d'authentification n'est écrite) · aucun calcul de conformité · aucun appel à un modèle · ni Dexie ni service worker ni PWA · aucun déploiement, aucun `Dockerfile`, **aucun compte OVHcloud ouvert** · treize des dix-neuf tables · aucun écran produit · ni CQRS ni `Mediator.SourceGenerator` (D12 prévoit le repli « handlers écrits à la main » — **donc l'exception de licence que D13 renvoie au lot 2 n'est pas due, et il faut le dire au lieu de la laisser expirer en silence**) · pas de Tailwind (D42) · aucun formatage automatique du SQL (Prettier n'a aucun parseur SQL natif) · **aucune donnée réelle nulle part**.

---

## Tâche 6 : La migration `SocleInitial` — six tables, une vue, un `CHECK`, deux index, RLS forcée

**Docker : REQUIS.**

**Fichiers :**

- Créer : `back/Palier.Infrastructure/PalierDbContext.cs`, `back/Palier.Infrastructure/Identite/Utilisateur.cs`, les entités, `back/Palier.Infrastructure/Migrations/*_SocleInitial.cs`, `back/Palier.Database.Tests/SchemaTests.cs`
- Modifier : `back/Palier.Infrastructure/Palier.Infrastructure.csproj`

**Interfaces :**

- Consomme : la fixture et les rôles (tâche 5)
- Produit : le schéma. Consommé par les tâches 7 à 11.

- [ ] **Étape 1 : les paquets, et leurs licences**

```bash
dotnet add back/Palier.Infrastructure package Npgsql.EntityFrameworkCore.PostgreSQL
dotnet add back/Palier.Infrastructure package Microsoft.EntityFrameworkCore.Design
dotnet add back/Palier.Infrastructure package Microsoft.AspNetCore.Identity.EntityFrameworkCore
node scripts/verifier-licences.mjs
```

`Palier.Infrastructure` existe déjà et **ne porte aujourd'hui aucun paquet**. Versions relevées le 20/08/2026 : Npgsql **10.0.3**, `EntityFrameworkCore.Design` **10.0.11**. Les quatre licences ont été vérifiées le 20/08/2026, **code 0 sur chacune, aucune exception à créer**.

- [ ] **Étape 2 : `PalierDbContext` et le type de la clé — D35**

```csharp
public sealed class Utilisateur : IdentityUser<Guid> { }

public sealed class PalierDbContext(DbContextOptions<PalierDbContext> options)
    : IdentityDbContext<Utilisateur, IdentityRole<Guid>, Guid>(options)
{
}
```

> **Pourquoi `uuid` et pas le `text` par défaut d'Identity (D35).** Trois raisons cumulées : le schéma entier de `docs/03-donnees.md` est en `uuid` avec `gen_random_uuid()` ; la spec d'architecture § 8 prévoit des identifiants **UUID v7 générés côté client** pour l'idempotence de la file de retry ; et un `owner_id` en `text` alourdit tous les index de jointure du produit, dont `workouts (owner_id, started_at desc)`.
>
> Les **14** clauses `references auth.users` de `docs/03-donnees.md` deviennent `references "AspNetUsers"("Id")`, en `uuid`. Créer ces tables au lot 2 plutôt qu'au lot 4 évite une reprise de schéma portant sur quatorze clés étrangères — et `CLAUDE.md` § 6 interdit de changer le schéma seul après la première mise en production.
>
> **Ce qui rouvre D35 : rien.** Après la première migration appliquée en production, un changement de type de clé primaire n'est plus une décision, c'est une migration de données.

**Aucune ligne de code d'authentification n'est écrite.** Les tables `AspNet*` sont créées, c'est tout. Le lot 4 fait le reste.

- [ ] **Étape 3 : les six tables — une par forme du schéma (D39)**

| Table           | Forme                 | Points de vigilance                                             |
| --------------- | --------------------- | --------------------------------------------------------------- |
| `AspNet*`       | identité              | créées, sans politique (tâche 7)                                |
| `nutrient_refs` | référence publique    | clé primaire `nutrient` en `text`, pas d'`owner_id`             |
| `exercises`     | catalogue mixte       | `is_custom boolean default false`, `owner_id uuid` **nullable** |
| `workouts`      | possédée directe      | `energy_1_5 int check (energy_1_5 between 1 and 5)`             |
| `sets`          | possédée par jointure | `workout_id not null references workouts on delete cascade`     |
| `body_weight`   | possédée directe      | `unique (owner_id, measured_on)`                                |

Plus **deux index du schéma** : `workouts (owner_id, started_at desc)` et `sets (workout_id)`. Plus **une vue**, `weekly_volume`, écrite par `migrationBuilder.Sql(...)` **avec son `Down`**.

> **D39 est un écart à `07-roadmap.md` étape 1, qui dit « schéma complet ».** `CLAUDE.md` § 5 range la feuille de route **au-dessus** de `CLAUDE.md`. **Sans validation explicite du porteur, D39 n'est pas prise** (question 5 en fin de plan). Le motif : dix-neuf tables migrées d'un bloc, ce sont dix-neuf tests RLS sur des tables qu'aucun cas d'usage n'exerce — du garde-fou sans cible, exactement ce que D31 refuse. Une migration EF Core est **additive** : poser une table au moment du besoin ne coûte rien, à la condition que le **type de la clé** soit tranché une seule fois, ce que D35 fait.

- [ ] **Étape 4 : `enable` ET `force row level security` sur toutes les tables**

Écrits par `migrationBuilder.Sql(...)`, avec leur `Down`. **Sur toutes**, tables d'identité comprises (D38).

> **`FORCE` ajoute la seule chose que la séparation des rôles ne donne pas** : la protection contre un accès direct sous le rôle **propriétaire** — outil d'administration, restauration, identifiants fuités, tâche lancée à la main sur le VPS. C'est la **première** des trois familles que `03-donnees.md` nomme.
>
> **Son prix est réel et se paie à la tâche 10**, pas ici : sous `FORCE`, `pg_dump` échoue pour tout rôle qui ne contourne pas RLS, et `COPY FROM` est refusé à la restauration. **Question 3 en fin de plan.**

**Conséquence immédiate sur le référentiel :** le rôle `palier_migrations`, propriétaire, devient lui aussi sujet aux politiques. Les `INSERT` de `db/referentiel/` passent donc par le `WITH CHECK`. Les deux corrections réflexes — accorder `BYPASSRLS` au rôle de migration, ou `NO FORCE` le temps du chargement — **sont exactement les deux façons d'éteindre RLS sans que rien ne le signale**. Le référentiel s'insère sous une **politique d'écriture explicite** pour `palier_migrations`, écrite dans la même migration, jamais par contournement.

- [ ] **Étape 5 : écrire les épreuves de schéma, en forme de CATALOGUE**

`SchemaTests.cs` :

```sql
-- Toute table de `public` doit avoir RLS activée ET forcée.
select c.relname
  from pg_class c join pg_namespace n on n.oid = c.relnamespace
 where n.nspname = 'public' and c.relkind = 'r'
   and (not c.relrowsecurity or not c.relforcerowsecurity);
-- Attendu : zéro ligne. Sinon, l'épreuve échoue EN NOMMANT la ou les tables.
```

> **La forme catalogue est choisie délibérément.** Elle couvre les tables **futures** sans que personne ait à y penser, là où une liste écrite à la main vieillirait — et une liste qui vieillit passe au vert sur les tables qu'elle ne connaît pas. Même forme pour la vue : interroger `pg_views`, pas comparer un fichier.

- [ ] **Étape 6 : franchissement — quatre violations, six branches**

| #     | Violation à provoquer                           | Refus attendu                                                                                                                              |
| ----- | ----------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------ |
| 1     | Retirer `enable row level security` d'une table | l'épreuve de catalogue rougit **en nommant la table**                                                                                      |
| 1 bis | Retirer `force row level security` seulement    | rougit aussi — **les deux colonnes, pas une**                                                                                              |
| 2     | Modifier une entité sans générer la migration   | `dotnet ef migrations has-pending-model-changes` refuse. **C'est le seul garde-fou automatique de dérive entre le modèle C# et le schéma** |
| 3a    | Retirer la vue du `Up`                          | l'épreuve rougit                                                                                                                           |
| 3b    | Retirer le `Down` de la vue                     | une **seconde** épreuve, distincte, rougit — `Down` puis `Up` doit rendre l'état initial                                                   |
| 4     | Insérer `energy_1_5 = 6`                        | le moteur refuse l'insertion                                                                                                               |

> **Les violations 3a et 3b sont deux épreuves, pas une.** L'application démarrerait dans les deux cas, et c'est exactement le mode de défaillance que `03-donnees.md` nomme : « **un schéma qui compile, qui démarre, et qui n'applique ni les vues, ni RLS, ni les bornes. Rien ne le signale au démarrage.** »

- [ ] **Étape 7 : appliquer la migration de bout en bout, sans intervention manuelle**

```bash
npm run db:up
dotnet ef database update --project back/Palier.Infrastructure --startup-project back/Palier.Api --connection "<PalierMigrations>"
```

Attendu : le schéma est appliqué. **Aucune commande manuelle intercalée.** Si une étape manuelle est nécessaire, c'est un défaut du plan, pas une note de bas de page : la consigner et corriger.

- [ ] **Étape 8 : `db/README.md` gagne la section « appliquer », puis commit**

```bash
git commit -m "pose le socle initial du schema avec rls forcee" -- back db
```

---

---
