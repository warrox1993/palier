# Brief — Tâche 2

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

## Tâche 2 : `db/compose.yaml`, les scripts `db:*`, et la branche « Docker éteint »

**Docker : la branche de refus se franchit MAINTENANT, sans Docker. La branche de succès attend la tâche 0.**

**Fichiers :**

- Créer : `db/compose.yaml`, `tests-harness/db.test.mjs`
- Modifier : `package.json` (racine — `db:up`, `db:down`, `db:reset`), `db/README.md`, `DEMARRAGE.md` § 0

**Interfaces :**

- Consomme : la racine `db/` (tâche 1)
- Produit : `npm run db:up` · `db:down` · `db:reset`, et **le seul endroit du dépôt où le tag PostgreSQL est écrit**. Consommé par les tâches 5 (qui **lit** ce tag) et 11.

> **Saisir la branche pendant qu'elle est disponible.** « Docker éteint » est la branche qu'on ne provoque jamais, parce qu'elle ne survient qu'un jour de panne. **Elle est l'état courant de la machine.** L'étape 3 la franchit avant que la tâche 0 ne la referme. Après la tâche 0, la reproduire demandera d'arrêter Docker Desktop à la main — faisable, mais on ne le fait jamais.

- [ ] **Étape 1 : écrire l'épreuve, et la voir échouer**

`tests-harness/db.test.mjs` — trois tests, dans cet ordre :

```javascript
// 1. la cible existe
//    db/compose.yaml présent, sinon échec explicite

// 2. le tag est écrit UNE SEULE FOIS dans le dépôt
//    grep 'postgres:' sur db/compose.yaml rend exactement une occurrence,
//    et aucun autre fichier du dépôt ne porte 'postgres:18'

// 3. le service porte le nom qu'attendent les scripts db:*
//    le nom du service lu dans db/compose.yaml est celui que
//    package.json passe à `docker compose ... <service>`
```

Le paquet `yaml` est déjà déclaré dans le `package.json` de la **racine** — `tests-harness/` l'importe ailleurs. Ne pas l'ajouter au front.

Lancer : `npm run test:harness:back`
Attendu : **ÉCHEC** — `db/compose.yaml` n'existe pas.

- [ ] **Étape 2 : écrire `db/compose.yaml` et les trois scripts**

`db/compose.yaml` : **un seul service**, `image: postgres:18.6` (D34), volume nommé `palier-db-data`, `ports: ['5432:5432']`. Le port est libre sur le poste — rien n'y écoute, aucun service `*postgres*` n'existe, mesuré le 20/08/2026.

> **Pourquoi 18.6, et pourquoi pas `alpine` (D34).** Les Public Cloud Databases d'OVHcloud offrent les versions 14, 15, 16, 17 et **18** (relevé du 20/08/2026). `gen_random_uuid()`, employé partout dans le schéma, est en cœur depuis la 13, sans extension. `alpine` est écarté pour une raison **mesurable et non esthétique** : musl n'implémente pas `LC_COLLATE` comme glibc, le tri y est octet par octet, et l'instance managée tourne sur glibc — un `ORDER BY` sur un nom d'exercice trierait différemment sur le poste et en production, **ce qui est exactement la divergence qu'une base locale existe pour supprimer**.
>
> **Ce qui n'est délibérément pas étendu :** D28 épingle les actions GitHub par empreinte parce qu'une étiquette mutable y exécute du code avec accès au dépôt et aux secrets. Le conteneur PostgreSQL n'exécute rien qui touche au dépôt et ne voit aucun secret ; la mutabilité du tag `18.6` y est un **bénéfice**, puisqu'elle apporte les correctifs.

`package.json` (racine) :

```json
{
  "scripts": {
    "db:up": "docker compose -f db/compose.yaml up -d",
    "db:down": "docker compose -f db/compose.yaml down",
    "db:reset": "docker compose -f db/compose.yaml down -v"
  }
}
```

`db:reset` porte `-v` : c'est **la seule commande du projet qui détruit des données**. Elle est nommée dans `db/README.md` avec cette phrase, pas seulement listée.

**Le mot de passe local reste en clair dans le compose.** gitleaks l'accepte sous cette forme — mais il **refuse** une chaîne de connexion `Host=…;Password=…` dans un fichier de code. Les chaînes vont donc en `dotnet user-secrets` ou en variable d'environnement, **jamais dans `appsettings.Development.json`**. Les deux branches se franchissent à l'étape 5.

- [ ] **Étape 3 : franchir la branche « Docker éteint » — MAINTENANT**

> Ne pas remettre cette étape après la tâche 0. C'est la seule occasion où l'état est gratuit.

```bash
npm run db:up
```

**Refus attendu :** code non nul, **et** un message qui nomme Docker Desktop et la façon de le démarrer. Si `docker compose` rend une erreur brute de socket, **envelopper les trois scripts** dans un `scripts/db.mjs` qui teste d'abord `docker info` et rend un message utilisable — le message d'erreur natif est illisible pour quelqu'un qui découvre le blocage à 23 h.

Coller la sortie exacte au journal du lot : c'est la moitié rouge que rien ne reproduira gratuitement.

- [ ] **Étape 4 : franchir la divergence de nom de service**

Renommer le service dans `db/compose.yaml` sans toucher aux scripts. **Refus attendu :** `npm run test:harness:back` rougit sur le test 3 en nommant **les deux** valeurs — celle du compose et celle du `package.json`. Remettre le nom.

C'est la même parade que D34 sur le tag : une divergence entre deux déclarations se ferme par une **lecture**, jamais par une discipline.

- [ ] **Étape 5 : franchir les deux branches de gitleaks**

1. Vérifier que le mot de passe du compose **ne déclenche pas** gitleaks : `gitleaks dir --no-banner -v --config .gitleaks.toml db` → **code 0**. Si la règle mord ici, elle est trop large et il faut le savoir avant d'y coller un secret réel.
2. Écrire une chaîne de connexion complète — `Host=localhost;Database=palier;Username=palier_app;Password=…` — dans un `.cs`, l'indexer, tenter le commit. **gitleaks doit refuser.** Retirer.

**Les deux branches, pas une.** Une règle qui refuse tout est aussi inutile qu'une règle qui n'attrape rien.

- [ ] **Étape 6 : `DEMARRAGE.md` § 0 gagne Docker en prérequis**

Au même titre que gitleaks (D23), avec **la commande d'installation** et la mention que le démon doit tourner, pas seulement le client :

```bash
docker info --format '{{.ServerVersion}}'   # doit rendre une version, pas une erreur
```

Écrire pourquoi `docker --version` ne suffit pas. C'est le fait mesuré du 20/08/2026, et il coûterait une demi-journée à quelqu'un qui ne l'a pas lu.

- [ ] **Étape 7 : commit**

```bash
git commit -m "leve la base locale et eprouve la panne du moteur" -- db package.json tests-harness/db.test.mjs DEMARRAGE.md
```

- [ ] **Étape 8 : APRÈS la tâche 0 — franchir la branche de succès**

```bash
npm run db:up
docker compose -f db/compose.yaml ps
npm run db:down
```

Attendu : le conteneur démarre, `ps` le montre sain, `down` l'arrête. **Une branche éprouvée ne dispense pas de l'autre :** la branche de refus prouve que la panne est lisible, la branche de succès prouve que la commande sert à quelque chose.

---

---
