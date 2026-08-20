# Brief — Tâche 0

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

## Tâche 0 : Prérequis matériel — activer WSL2 ou Hyper-V

> **PORTEUR DU PROJET UNIQUEMENT.** Droits administrateur et redémarrage. Aucun agent ne peut la faire. **Toute la voie Docker en dépend, et rien ne le signale aujourd'hui.**

**Docker :** c'est cette tâche qui le débloque.

**Fichiers :**

- Créer : aucun
- Modifier : `.superpowers/sdd/2026-08-20-lot-2-socle-donnees/progress.md` — les deux états mesurés, avant et après

**Interfaces :**

- Consomme : rien
- Produit : un démon Docker joignable. Prérequis des tâches 5 à 12, et de la seconde branche de la tâche 2.

- [ ] **Étape 1 : consigner l'état AVANT, avec son instrument**

```powershell
wsl --status
Get-CimInstance Win32_OptionalFeature | Where-Object Name -in 'Microsoft-Windows-Subsystem-Linux','Microsoft-Hyper-V','VirtualMachinePlatform' | Select-Object Name, InstallState
docker --version
docker compose version
docker info --format '{{.ServerVersion}}'
```

Attendu aujourd'hui, mesuré le 20/08/2026 : `wsl --status` rend « Le Sous-système Windows pour Linux n'est pas installé » ; les deux premières fonctionnalités sont en **InstallState 2**, `VirtualMachinePlatform` à **1** ; le client répond `29.6.2` et Compose `v5.3.1` ; **`docker info` échoue** sur `npipe:////./pipe/dockerDesktopLinuxEngine`.

> **C'est le faux signal que ce projet chasse.** `docker --version` répond parfaitement pendant que le démon est injoignable. Trois commandes disent « installé » et une seule dit la vérité. Coller la sortie des cinq au journal du lot : c'est la moitié « rouge » du franchissement de cette tâche, et elle n'est disponible qu'aujourd'hui.

- [ ] **Étape 2 : activer, en administrateur**

```powershell
wsl --install
```

Puis **redémarrer le poste**. Démarrer Docker Desktop et attendre que la baleine soit stable.

_Variante :_ si le porteur choisit Podman ou Rancher Desktop plutôt que Docker Desktop — voir le point 4 des questions ouvertes en fin de plan — l'installation diffère, et Testcontainers demandera `DOCKER_HOST` vers la socket Podman ainsi que `TESTCONTAINERS_RYUK_DISABLED=true` en rootless. **Ce choix se fait maintenant, pas dans deux ans.**

- [ ] **Étape 3 : franchissement — les deux états, pas un seul**

```bash
docker info --format '{{.ServerVersion}}'
```

|                     | Attendu                                                                                           |
| ------------------- | ------------------------------------------------------------------------------------------------- |
| **Avant** (étape 1) | erreur de connexion au démon, code non nul, message nommant `npipe` ou `dockerDesktopLinuxEngine` |
| **Après**           | une version, code 0                                                                               |

Les deux assertions de D19 : le **code de sortie** et un **motif**. Un code 0 sans version ne prouve rien. Consigner les deux sorties au journal.

- [ ] **Étape 4 : vérifier que le moteur sait réellement tirer une image**

```bash
docker run --rm postgres:18.6 postgres --version
```

Attendu : `postgres (PostgreSQL) 18.6`. Un démon qui répond mais ne peut pas tirer d'image — proxy, quota, réseau — ferait échouer les tâches 5 à 12 pour une raison qu'on croirait résolue.

---

---
