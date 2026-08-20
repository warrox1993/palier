# Brief — Tâche 10

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

## Tâche 10 : Sauvegarde et restauration sous `FORCE` — le prix de D37, éprouvé et non supposé

**Docker : REQUIS.**

**Fichiers :**

- Créer : `back/Palier.Database.Tests/SauvegardeTests.cs`
- Modifier : `db/amorcage/01-roles.sql` (`palier_sauvegarde`), `db/README.md`, `back/.env.example`

**Interfaces :**

- Consomme : le schéma sous `FORCE` (tâche 6), les rôles (tâche 5)
- Produit : les trois commandes exactes de sauvegarde, et **la mesure qui décide de D37**.

> **Le prix de `FORCE`, nommé plutôt que découvert.** Sous `FORCE`, le propriétaire cesse de contourner RLS. `pg_dump` pose `row_security = off` par défaut, et « **If the user does not have sufficient privileges to bypass row security, then an error is thrown** » ; `--enable-row-security` impose un dump au format `INSERT`, puisque « **the COPY FROM during restore does not support row security** ». La sauvegarde et la restauration deviennent donc **un livrable éprouvé du lot 2**, et non une promesse trimestrielle qui rencontrerait ce mur au pire moment — `docs/14-contenu.md` § 7 : « Une sauvegarde jamais restaurée n'est pas une sauvegarde ».

- [ ] **Étape 1 : `palier_sauvegarde` et sa chaîne**

Créer le rôle dans `db/amorcage/01-roles.sql`, **seul candidat à `BYPASSRLS`**, et renseigner `ConnectionStrings__PalierSauvegarde` dans `back/.env.example`.

**L'épreuve tourne en rôles non superutilisateurs.** Le compte superutilisateur du conteneur ne sert **qu'à créer les rôles**, jamais à exécuter l'épreuve — sans quoi elle serait **verte chez nous et rouge en production**.

- [ ] **Étape 2 : la branche 1 — l'épreuve INVERSÉE**

`pg_dump` sous `palier_migrations`, tables sous `FORCE`.

**Attendu : L'ÉCHEC.**

> **C'est une épreuve inversée, et c'est voulu.** Elle prouve que la contrainte documentée est **réelle sur cette version du moteur**, au lieu de la croire. Une contrainte qu'on cite sans l'avoir provoquée est une citation, pas une mesure. **Annoter le test comme inversé**, sinon quelqu'un le « corrigera » en le rendant vert.

Deux assertions : l'échec **et** un motif nommant `row security` ou `permission denied`. Un `pg_dump` qui échoue pour une autre raison — mauvais port, rôle inexistant — passerait cette épreuve sans rien prouver.

- [ ] **Étape 3 : la branche 2 — le dump et la restauration doivent RÉUSSIR**

`pg_dump` sous `palier_sauvegarde` avec `BYPASSRLS`, puis `pg_restore` **dans une base neuve**.

**Attendu : succès, et le MÊME NOMBRE DE LIGNES de part et d'autre.** Compter, ne pas se fier au code de sortie : un dump vide restaure parfaitement.

- [ ] **Étape 4 : si la branche 2 ne peut pas être obtenue — remonter, ne pas contourner**

> « Only superuser roles or roles with `BYPASSRLS` can specify `BYPASSRLS` » (`sql-createrole.html`). Sur Testcontainers, le compte d'administration est superutilisateur : la création doit réussir. **Sur OVHcloud, ce n'est pas mesurable avant d'avoir une instance** — l'offre repose sur Aiven, dont le compte d'administration n'est pas superutilisateur.

Si la branche 2 échoue ici, **ne pas retirer `FORCE` et ne pas accorder `BYPASSRLS` au rôle propriétaire**. D37 remonte au porteur avec son prix chiffré, et l'arbitrage `FORCE` contre `pg_dump` se fait **au lot 2 sur un Testcontainer — pas au lot 9 sur la production, un soir de restauration**.

- [ ] **Étape 5 : `db/README.md` — les trois commandes exactes**

Dump · restauration **dans une base neuve** · réinitialisation. Verbatim, copiables, avec le rôle employé pour chacune et ce qui échoue si l'on se trompe de rôle. Un document de restauration qu'on lit pour la première fois pendant l'incident doit être exécutable sans réfléchir.

- [ ] **Étape 6 : commit**

```bash
git commit -m "eprouve la sauvegarde et la restauration sous force" -- back db
```

---

---
