# Brief — Tâche 9

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

## Tâche 9 : `GET /api/v1/sante`, `back/.env.example`, et l'assertion de démarrage

**Docker : REQUIS.**

**Fichiers :**

- Créer : `back/.env.example`, l'`IHostedService` d'assertion, `back/Palier.Api.Tests/` ou l'extension de `back/Palier.Database.Tests/`, les épreuves de journalisation
- Modifier : `back/Palier.Api/Program.cs`

**Interfaces :**

- Consomme : le pipeline (tâche 8), les politiques (tâche 7)
- Produit : `GET /api/v1/sante`. Consommé par la tâche 11.

- [ ] **Étape 1 : `back/.env.example` — il N'EXISTE PAS**

Seul `front/.env.example` est présent dans le dépôt (vérifié). `docs/16-projet.md` § 3 le **documente** ; il n'a jamais été écrit.

Le créer avec les **trois** chaînes de D37, en reprenant le modèle de `docs/16-projet.md` § 3 pour le reste :

```bash
# Base de données PostgreSQL — trois rôles, trois chaînes (D37)
ConnectionStrings__Palier=
ConnectionStrings__PalierMigrations=
ConnectionStrings__PalierSauvegarde=
```

**Aucune valeur.** `.env` n'est jamais versionné ; ce fichier-ci l'est. Et **aucune chaîne complète ne va dans `appsettings.Development.json`** : `dotnet user-secrets` ou variable d'environnement.

- [ ] **Étape 2 : la première route du projet**

`back/Palier.Api/Program.cs` n'expose **aucune route** aujourd'hui, et son commentaire dit textuellement que « elles arrivent avec le lot 2 ». Remplacer ce commentaire par la route, et **pas seulement l'ajouter** — un commentaire qui décrit un futur devenu présent est un mensonge à retardement.

`GET /api/v1/sante` rend : la **version de migration appliquée**, la base joignable, le **nombre de lignes de `nutrient_refs`**. **Aucun compte, aucune donnée de santé.**

`/api/v1` **dès la première route** : `14-contenu.md` § 8 décrit le versionnement rétroactif comme « un problème insoluble », et le poser maintenant coûte zéro.

**Pas de CQRS, pas de `Mediator.SourceGenerator` :** un handler écrit à la main, ce que D12 prévoit explicitement en repli — « une cinquantaine de lignes, zéro dépendance ». **Conséquence directe : l'exception de licence que D13 renvoie au lot 2 n'est pas due**, puisque le paquet n'est pas installé. La redater au lot où CQRS arrive, à la tâche 13, **au lieu de la laisser expirer en silence**.

- [ ] **Étape 3 : l'assertion de démarrage — trois requêtes de catalogue, et le refus de servir**

Un `IHostedService` exécute les trois requêtes sur la connexion **réelle** et **refuse de démarrer** si l'une échoue :

```sql
-- 1. le rôle de l'API ne contourne pas RLS
select rolsuper, rolbypassrls from pg_roles where rolname = current_user;

-- 2. le rôle de l'API ne possède aucune table
--    (sinon FORCE est la seule barrière restante)
select c.relname from pg_class c join pg_namespace n on n.oid = c.relnamespace
 where n.nspname = 'public' and c.relkind = 'r'
   and pg_get_userbyid(c.relowner) = current_user;

-- 3. toute table a RLS activée ET forcée
select c.relname from pg_class c join pg_namespace n on n.oid = c.relnamespace
 where n.nspname = 'public' and c.relkind = 'r'
   and (not c.relrowsecurity or not c.relforcerowsecurity);
```

> **C'est le cœur de D37, et il vient de D30.** Sept épreuves vertes sur un Testcontainer ne démontrent **rien** sur l'instance OVHcloud, où deux inconnues décident si RLS mord _du tout_ : le compte d'administration a-t-il `BYPASSRLS`, et les tables créées par les migrations appartiennent-elles au rôle de l'API ? La page « Capabilities » d'OVHcloud dit que la création d'utilisateurs se fait « with default admin roles and privileges » et que « the only specific privilege you can set is `replication` » — le rôle applicatif restreint **ne peut donc pas** être créé par l'interface, il l'est par SQL depuis le compte d'administration.
>
> **Une assertion au démarrage est la seule preuve qui porte sur le TRAITEMENT plutôt que sur le code.** À l'auditeur, on ne montre plus « sept tests verts en CI » mais « le service refuse de démarrer si l'isolation n'est pas en place, et voici la ligne de journal de chaque déploiement ». C'est ce que D30 laisse explicitement « à traiter au lot 2 ».

Le message de refus **nomme** : le rôle, la table possédée, ou la table sans `FORCE`. Un « assertion de démarrage échouée » sans nom force à rouvrir le code sur un serveur, un soir de déploiement.

**Cet `IHostedService` prend un `DbContext` — et le test de réflexion de la tâche 8 l'interdit.** Ce n'est pas un accident : c'est le seul chemin légitime hors pipeline du lot, et il doit être **nommément exempté** dans le test, avec le motif écrit dans l'épreuve (D21 : le motif vit dans l'épreuve, pas dans la configuration). Une exemption non nommée est une porte laissée ouverte.

- [ ] **Étape 4 : les deux épreuves de journalisation, qui N'EXISTENT PAS**

La spec d'architecture § 11 les liste comme épreuves **à écrire**. Mesuré : `back/tests-harness/` ne contient que `couverture`, `format`, `licences`, `references`, `rigueur`, `vulnerabilites`, et `grep -rn SensitiveDataLogging back/ --include=*.cs` rend **zéro**.

> **Pourquoi elles appartiennent à CE lot et pas au lot 9.** Le mécanisme crée un chemin d'erreur SQL : chaque défaut d'identité devient une `PostgresException` portée par l'événement `CommandError` d'EF Core, **qui journalise le `CommandText`** — sur des tables nommées `body_weight`, `intake_entries`, `exercise_feedback`. `01-conformite.md` § 4 : « Aucune donnée de santé dans les logs applicatifs ». La règle existe, rien ne l'observe.

Deux épreuves :

1. **Une donnée de santé dans un journal** — poids, apport, identifiant d'utilisateur → l'épreuve refuse.
2. **La configuration de PRODUCTION construite avec `EnableSensitiveDataLogging` actif** → l'épreuve refuse. Un appel conditionné à `IsDevelopment()` qui fuit en production est **exactement ce que rien n'attrape** : construire la configuration de production et asserter l'option à faux.

**À signaler sans le traiter ici :** côté serveur, `log_min_error_statement` vaut `error` par défaut — l'instruction fautive part dans les journaux **d'OVHcloud, sous-traitant**. Paramétrée, donc sans valeurs, ce qui est acceptable ; mais cela impose de vérifier sur l'instance réelle que `log_statement` et la journalisation des paramètres sont éteints, et de l'annexer à l'AIPD. **Fait d'instance, pas de dépôt** : à porter au journal du lot, pas à faire semblant de régler.

- [ ] **Étape 5 : franchissement — cinq violations**

| #   | Violation à provoquer                                                                 | Refus attendu                                                                               |
| --- | ------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------- |
| 1   | Démarrer l'API avec `ConnectionStrings__PalierMigrations`                             | le service **refuse de démarrer**, en nommant la table dont `current_user` est propriétaire |
| 2   | Accorder `BYPASSRLS` au rôle applicatif                                               | refuse, **en nommant le rôle**                                                              |
| 3   | Retirer `force row level security` d'une table                                        | refuse, **en nommant la table**                                                             |
| 4   | Écrire un poids, un apport ou un identifiant d'utilisateur dans un journal            | l'épreuve de journalisation refuse                                                          |
| 5   | Construire la configuration de **production** avec `EnableSensitiveDataLogging` actif | l'épreuve refuse                                                                            |

> **Les violations 1 à 3 sont la réponse directe à D30 :** elles portent sur l'**état du service**, pas sur le contenu d'un fichier, et **ce sont les seules preuves qui vaudront quelque chose sur l'instance OVHcloud**.

- [ ] **Étape 6 : vérifier de bout en bout**

```bash
npm run db:up
dotnet run --project back/Palier.Api
curl http://localhost:<port>/api/v1/sante
```

Attendu : la version de migration appliquée, la base joignable, le compte de `nutrient_refs`. Puis relancer avec la chaîne `PalierMigrations` : **refus de démarrer**.

- [ ] **Étape 7 : commit**

```bash
git commit -m "expose la route de sante et refuse de servir sans isolation" -- back
```

---

---
