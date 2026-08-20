# Brief — Tâche 5

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

## Tâche 5 : Le projet de tests d'intégration, les trois rôles, et le tag unique

**Docker : REQUIS.** Première tâche de la voie Docker. Elle ne démarre qu'après la tâche 0.

**Fichiers :**

- Créer : `back/Palier.Database.Tests/Palier.Database.Tests.csproj`, `back/Palier.Database.Tests/BaseFixture.cs`, `back/Palier.Database.Tests/RolesTests.cs`, `db/amorcage/01-roles.sql`
- Modifier : `back/Palier.sln`

**Interfaces :**

- Consomme : `db/compose.yaml` (le tag, **lu**), la racine `db/`
- Produit : la fixture Testcontainers et les trois rôles. Consommés par les tâches 6 à 10.

> **Conséquence à ne pas édulcorer.** `scripts/verify.mjs` lance `dotnet test back/Palier.sln` sur la **solution entière**. Dès que ce projet y entre, `npm run verify` — donc `git push`, puisque `.husky/pre-push` n'appelle que lui — **exige un moteur de conteneurs**, sans qu'une ligne de `verify.mjs` ait changé. Le refuser demanderait un `--filter`, c'est-à-dire une **seconde liste de contrôles**, exactement ce que D25 existe pour empêcher. Le hook de **pré-commit** n'est pas touché : il ne lance que lint-staged et gitleaks. **Ce point est soumis au porteur** (question 2 en fin de plan) ; ne pas l'engager sans sa réponse.

- [ ] **Étape 1 : créer le projet et l'inscrire à la solution**

```bash
dotnet new xunit -o back/Palier.Database.Tests
dotnet sln back/Palier.sln add back/Palier.Database.Tests/Palier.Database.Tests.csproj
dotnet add back/Palier.Database.Tests package Testcontainers.PostgreSql
```

Versions relevées le 20/08/2026 : xUnit **2.9.3** — **aligné sur `Palier.Domain.Tests`**, deux versions de xUnit dans une même solution produisent des conflits de découverte —, `Testcontainers.PostgreSql` **4.14.0**, licence vérifiée par `node scripts/verifier-licences.mjs --tester Testcontainers.PostgreSql` (**code 0**, « toutes sous licence permissive »).

```bash
node scripts/verifier-licences.mjs
```

Attendu : code 0, et le compte de dépendances **augmente**. Un compte inchangé signifierait que le nouveau projet échappe au parcours — vérifier alors que `verifier-licences.mjs` voit bien `back/Palier.Database.Tests`.

**Aucune étape nouvelle dans `verify` (D25) :** le projet entre par `back/Palier.sln`, donc par `back:test`.

**Et il n'est pas soumis au seuil de couverture.** `back/coverage.runsettings` filtre `[Palier.Domain]*`, et le seuil de 100 % vit dans `back/Palier.Domain.Tests/Palier.Domain.Tests.csproj`, appliqué par `coverlet.msbuild`. **Ne pas ajouter ce projet au seuil** — un chiffre global pousse à tester ce qui est facile.

- [ ] **Étape 2 : `db/amorcage/01-roles.sql` — trois rôles, tous non superutilisateurs**

| Rôle                | Attributs                                                                                                          | Usage                                                      |
| ------------------- | ------------------------------------------------------------------------------------------------------------------ | ---------------------------------------------------------- |
| `palier_migrations` | `NOSUPERUSER NOBYPASSRLS`, propriétaire des tables                                                                 | `dotnet ef database update`, référentiel. **Jamais l'API** |
| `palier_app`        | `NOSUPERUSER NOBYPASSRLS NOCREATEDB NOCREATEROLE`, ne possède aucun objet, privilèges accordés **objet par objet** | l'API                                                      |
| `palier_sauvegarde` | `NOSUPERUSER`, **seul candidat à `BYPASSRLS`**                                                                     | `pg_dump` / `pg_restore` (tâche 10)                        |

> **Sans deux rôles distincts, les politiques ne font rien — silencieusement.** « Superusers and roles with the `BYPASSRLS` attribute always bypass the row security system when accessing a table. Table owners normally bypass row security as well » (`ddl-rowsecurity.html`, vérifié). C'est ce que `docs/03-donnees.md` § RLS désigne déjà comme « la première chose à éprouver, **avant les politiques elles-mêmes** ».

Le fichier est exécuté par le compte d'administration du conteneur. **C'est le seul usage de ce compte dans tout le lot.**

- [ ] **Étape 3 : la fixture LIT le tag dans `db/compose.yaml`**

`BaseFixture.cs` lit le tag `postgres:` de `db/compose.yaml` par expression régulière et le passe au `PostgreSqlBuilder`. **Jamais une seconde déclaration du numéro de version.**

> Deux déclarations du même tag divergeraient : c'est le défaut nommé par D25, et il se ferme par une **lecture** plutôt que par une discipline. Le local copie la production, jamais l'inverse (D34).

Si le fichier est introuvable ou ne porte aucun tag, la fixture **lève avec un message explicite** — quatrième question du franchissement : un contrôle qui n'a plus sa cible doit crier.

- [ ] **Étape 4 : écrire l'épreuve du rôle — la plus importante de la tâche**

`RolesTests.cs`, exécuté **sous `palier_app`** :

```
select current_user;                                    -- doit rendre 'palier_app'
select rolsuper, rolbypassrls from pg_roles
 where rolname = current_user;                          -- les deux DOIVENT être faux
```

> **Sans cette assertion, toutes les épreuves qui suivent seraient des faux verts d'une classe particulièrement traître : elles porteraient sur un rôle que la production n'utilisera jamais.** Une suite d'isolation exécutée sous le compte superutilisateur du conteneur passe intégralement au vert, et ne démontre rien.
>
> `pg_roles` est **publiquement lisible** — « a publicly readable view of pg_authid that blanks out the password field » — et expose bien `rolbypassrls`. L'assertion est donc exécutable **depuis le rôle applicatif lui-même**, sans privilège particulier.

Deux assertions : la valeur attendue **et** un message qui nomme la valeur trouvée. `Assert.False(rolbypassrls)` sans message oblige à rouvrir le test pour comprendre.

- [ ] **Étape 5 : franchissement — trois violations**

| #   | Violation à provoquer                                                                             | Refus attendu                                                                                                  |
| --- | ------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------- |
| 1   | Changer le tag dans `db/compose.yaml` sans toucher au test                                        | le test échoue **en nommant les deux valeurs** — celle attendue et celle lue                                   |
| 2   | Faire tourner la fixture sous le compte **superutilisateur** du conteneur au lieu de `palier_app` | `current_user` n'est pas `palier_app`, **ou** `rolsuper` est vrai → l'épreuve rougit en nommant le rôle trouvé |
| 3   | Retirer `NOBYPASSRLS` de `db/amorcage/01-roles.sql`                                               | l'épreuve rougit en nommant `rolbypassrls`                                                                     |

**La violation 2 est la plus importante du lot avec celle de la tâche 7.** La franchir en modifiant réellement la chaîne de connexion de la fixture, pas en imaginant le résultat.

- [ ] **Étape 6 : vérifier et commit**

```bash
dotnet test back/Palier.sln --settings back/coverage.runsettings
npm run verify
```

`verify` doit rester en **code 0** — l'étape `back:test` porte désormais Testcontainers, et sa durée augmente. Noter la nouvelle durée : elle sert à la tâche 12.

```bash
git commit -m "eprouve l isolation sous un role non superutilisateur" -- back db/amorcage
```

---

---
