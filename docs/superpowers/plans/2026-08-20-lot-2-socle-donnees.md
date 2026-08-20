# Lot 2 — Socle de données — Plan d'implémentation

> **Pour les exécutants agentiques :** SOUS-SKILL REQUISE — utiliser `superpowers:subagent-driven-development` (recommandé) ou `superpowers:executing-plans` pour exécuter ce plan tâche par tâche. Les étapes utilisent la syntaxe à cases (`- [ ]`) pour le suivi.

**But :** poser le socle de données du projet `palier` — une base locale reproductible, un schéma migré dont les politiques **mordent**, l'isolation entre deux utilisateurs prouvée sur un vrai moteur, une route de santé qui refuse de servir si l'isolation n'est pas en place, et un écran à quatre états dont l'erreur se **provoque** au lieu de se simuler.

**Spec :** `docs/superpowers/specs/2026-08-20-lot-2-socle-donnees-design.md`
**Décisions sources :** `docs/decisions.md` D1 à D31. Ce lot en écrit douze de plus, D32 à D43, à la tâche 13.

**Ce que le lot 1 a laissé :** le harnais complet — 118 épreuves de franchissement, `npm run verify` en seize étapes, une CI à six jobs. Et **aucune fonctionnalité produit** : `front/src/` ne contient que `main.tsx`, `app/App.tsx` et un `core/index.ts` dont le contenu entier est `export {}` ; `back/Palier.Api/Program.cs` n'expose aucune route.

---

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

## Structure des fichiers

| Fichier                                                                                                  | Responsabilité                                                      | Tâche  |
| -------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------- | ------ |
| `db/README.md`                                                                                           | lever, appliquer, réinitialiser, sauvegarder                        | 1, 10  |
| `db/SOURCES.md`                                                                                          | licence, version et date, **fichier par fichier**                   | 1      |
| `db/compose.yaml`                                                                                        | un seul service PostgreSQL, **le seul endroit où le tag est écrit** | 2      |
| `db/amorcage/01-roles.sql`                                                                               | les trois rôles non superutilisateurs                               | 5      |
| `db/referentiel/`                                                                                        | données de production (EFSA, exercices, programmes)                 | 1, 6   |
| `db/demonstration/`                                                                                      | jeu de développement **entièrement synthétique**                    | 1      |
| `back/Palier.Infrastructure/PalierDbContext.cs`                                                          | `IdentityDbContext<Utilisateur, IdentityRole<Guid>, Guid>`          | 6      |
| `back/Palier.Infrastructure/Migrations/`                                                                 | `SocleInitial` : tables, vue, `CHECK`, index, RLS, politiques       | 6, 7   |
| `back/Palier.Application/…/IdentiteBehavior`                                                             | la pose de l'identité et la garde applicative                       | 8      |
| `back/Palier.Api/Program.cs`                                                                             | `GET /api/v1/sante` et l'assertion de démarrage                     | 9      |
| `back/Palier.Database.Tests/`                                                                            | l'isolation éprouvée sur un vrai moteur, **en `palier_app`**        | 5 → 10 |
| `back/.env.example`                                                                                      | **n'existe pas** — créé ici, avec les trois chaînes de D37          | 9      |
| `front/src/ui/jetons.ts`                                                                                 | la cible que `couleur-hors-jetons` attend et qui n'existe pas       | 3      |
| `front/src/lib/i18n.ts`, `front/src/locales/{fr,en}.json`                                                | i18next câblé                                                       | 3      |
| `front/src/features/etat/`                                                                               | l'écran d'état et ses quatre états                                  | 11     |
| `scripts/regles-projet.mjs`                                                                              | + `source-non-attribuee`, + `chaine-en-dur` sur les constantes      | 1, 3   |
| `.github/workflows/ci.yml`                                                                               | `franchissement` qui **échoue**, état du service Dependabot         | 4      |
| `docs/decisions.md` · `docs/07-roadmap.md` · `docs/03-donnees.md` · `docs/16-projet.md` · `DEMARRAGE.md` | la reprise documentaire                                             | 13     |

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

## Tâche 1 : Le dossier `db/` et son outillage

**Docker : non requis.** Première tâche de la voie sèche.

**Fichiers :**

- Créer : `db/README.md`, `db/SOURCES.md`, `db/amorcage/.gitkeep`, `db/referentiel/.gitkeep`, `db/demonstration/.gitkeep`, `front/tests/harness/fixtures/99-sonde.sql`
- Modifier : `package.json` (racine — `format:scripts`, `format:scripts:fix`), `.github/dependabot.yml`, `.editorconfig`, `scripts/regles-projet.mjs`, `front/tests/harness/regles-projet.test.ts`, `.gitignore` (si nécessaire)

**Interfaces :**

- Consomme : `lancerOutil` (`front/tests/harness/run-outil.ts`), `scripts/regles-projet.mjs`
- Produit : la racine `db/` et sa règle d'attribution. Consommée par les tâches 2, 5, 6 et 10.

> **Pourquoi `db/` ne porte aucun `.csproj` (D32).** Mesuré configuration par configuration. Un `db/` de SQL, YAML et Markdown n'oblige à toucher que **trois** fichiers. Un `db/` portant un `.csproj` en casse **cinq de plus, dont deux en silence** : `scripts/verifier-licences.mjs` parcourt littéralement `parcourir('back')` — un projet sous `db/` échapperait entièrement à D13 **sans un message** ; et `.gitignore` n'ignore que `back/**/bin/` et `back/**/obj/` — les binaires de `db/` deviendraient des fichiers suivis au premier `git add -A`. Les migrations EF Core, le `DbContext`, les entités et les tests de politiques restent sous `back/`.

- [ ] **Étape 1 : écrire l'épreuve de la règle `source-non-attribuee`, et la voir échouer**

Ajouter dans `front/tests/harness/regles-projet.test.ts` :

```typescript
describe('garde-fou : toute source de données est attribuée', () => {
  it('la fixture de violation existe', () => {
    expect(existsSync('tests/harness/fixtures/99-sonde.sql'), 'Cible manquante').toBe(true)
  })

  it('refuse un fichier de référentiel absent de db/SOURCES.md', () => {
    // La règle est TRANSVERSE : elle compare deux fichiers, elle ne cherche pas
    // un motif dans un seul. Elle se lance donc sans --fichier.
    const r = lancerOutil(['node', SCRIPT, '--source', 'tests/harness/fixtures/99-sonde.sql'])
    expect(r.code, `Le fichier non attribué a été accepté :\n${r.sortie}`).not.toBe(0)
    // Seconde assertion : le message doit NOMMER le fichier. Un code non nul
    // seul ne prouve pas que c'est cette règle-là qui a mordu — D19.
    expect(r.sortie).toMatch(/99-sonde\.sql/)
    expect(r.sortie).toMatch(/SOURCES\.md/)
  })
})
```

Lancer : `npm --prefix front run test:harness -- regles-projet`
Attendu : **ÉCHEC** — la règle n'existe pas.

- [ ] **Étape 2 : créer l'arborescence et les deux documents**

```bash
mkdir -p db/amorcage db/referentiel db/demonstration
touch db/amorcage/.gitkeep db/referentiel/.gitkeep db/demonstration/.gitkeep
```

`db/README.md` — quatre sections, écrites au fil des tâches : **lever** (tâche 2), **appliquer** (tâche 6), **réinitialiser** (tâche 2), **sauvegarder et restaurer** (tâche 10). À la tâche 1, poser les titres et la phrase qui distingue les trois dossiers :

- `amorcage/` — rôles et privilèges, exécuté **avant** les migrations, par le compte d'administration du conteneur ;
- `referentiel/` — **données de production** : valeurs EFSA, catalogue d'exercices, programmes modèles. Elles arrivent en production ;
- `demonstration/` — jeu de développement **entièrement synthétique**. Il n'arrive jamais en production, et **aucune donnée réelle n'y entre jamais** (D40).

> Le mot « seed » de `docs/16-projet.md` § 4 confond ces deux derniers. La distinction se règle à la tâche 13, dans le document ; ici on écrit la structure qui la porte.

`db/SOURCES.md` — un tableau, **une ligne par fichier** de `referentiel/` : nom du fichier · source · licence · version ou millésime · date de relevé · URL. C'est la cible de la règle de l'étape 3, et l'application de `docs/17-donnees-sources.md`.

- [ ] **Étape 3 : ajouter la règle `source-non-attribuee` à `scripts/regles-projet.mjs`**

Cette règle **ne rentre pas dans la structure `REGLES`** : les règles existantes cherchent un motif dans un fichier, celle-ci compare deux fichiers. Elle s'écrit comme un **contrôle transverse**, exécuté après la boucle par fichier, et elle alimente le même tableau `violations` — un seul point de sortie, un seul format de message.

Ce qu'elle fait :

1. lister les `*.sql` de `db/referentiel/` (racine `db/` ajoutée au parcours) ;
2. lire `db/SOURCES.md` ;
3. pour chaque fichier dont le **nom** n'apparaît pas dans `SOURCES.md`, pousser une violation dont le message nomme **le fichier** et **`db/SOURCES.md`** ;
4. `--source <chemin>` permet de la lancer sur une cible unique, ce dont l'épreuve a besoin.

**Et elle doit crier quand elle n'a plus de cible** — quatrième question du franchissement : si `db/SOURCES.md` n'existe pas, elle **abandonne** avec `CODE_USAGE`, elle ne rend pas « aucune violation ». Un contrôle qui approuve parce qu'il n'a rien trouvé rend le même silence qu'un contrôle qui approuve pour de bon.

**Aucun commentaire n'est ajouté à un fichier de configuration** au passage (D21) : `regles-projet.mjs` est un `.mjs`, il en porte déjà et cela a été vérifié sur cet outil-là.

- [ ] **Étape 4 : créer la fixture et voir l'épreuve passer**

`front/tests/harness/fixtures/99-sonde.sql` — un fichier trivial (`select 1;`) qui **n'est jamais listé dans `db/SOURCES.md`**.

> **La fixture vit dans `front/tests/harness/fixtures/`, jamais dans `db/referentiel/`.** Une fixture posée dans le vrai dossier ferait échouer `npm run regles` sur le dépôt réel — le garde-fou refuserait le projet lui-même.

Lancer : `npm --prefix front run test:harness -- regles-projet`
Attendu : les deux tests passent, **et** `node scripts/regles-projet.mjs` sans argument reste à **code 0** sur le dépôt réel.

- [ ] **Étape 5 : mesurer la question de D20 sur ce périmètre, et consigner la réponse**

> **La réponse a été « oui » pour les quatre outils du lot 1. Elle doit être MESURÉE ici, pas supposée.**

```bash
node scripts/regles-projet.mjs --source db/referentiel/99-sonde.sql
```

Question : _un fichier exclu du parcours reste-t-il exclu quand on le nomme explicitement en argument ?_ Coller la réponse au journal du lot, quelle qu'elle soit. Si l'argument explicite l'emporte, D20 gagne sa première exception et il faut l'écrire.

- [ ] **Étape 6 : étendre le formatage à `db/`, sans le SQL**

`package.json` (racine) :

```json
{
  "scripts": {
    "format:scripts": "node front/node_modules/prettier/bin/prettier.cjs --check \"scripts/**/*.mjs\" \"tests-harness/**/*.mjs\" \".lintstagedrc.mjs\" \"db/**/*.{yaml,yml,md,json}\"",
    "format:scripts:fix": "node front/node_modules/prettier/bin/prettier.cjs --write \"scripts/**/*.mjs\" \"tests-harness/**/*.mjs\" \".lintstagedrc.mjs\" \"db/**/*.{yaml,yml,md,json}\""
  }
}
```

**Le `.sql` reste dehors : Prettier n'a aucun parseur SQL natif.** Sa cohérence de style repose sur `.editorconfig` et la relecture — et cela s'écrit, plutôt que de laisser croire à une couverture qui n'existe pas.

- [ ] **Étape 7 : `.editorconfig` et Dependabot**

`.editorconfig` n'a aujourd'hui que `[*]`, `[*.cs]`, `[**/*.Tests/**.cs]` et `[*.{ts,tsx,js,jsx,json,md,yml,yaml}]`. Ajouter un bloc `[*.sql]` : indentation, fin de ligne, encodage.

`.github/dependabot.yml` — ajouter un cinquième bloc, **avec le même délai de refroidissement que les autres** :

```yaml
- package-ecosystem: docker-compose
  directory: /db
  schedule:
    interval: weekly
  cooldown:
    default-days: 7
    semver-major-days: 30
    semver-minor-days: 7
    semver-patch-days: 3
```

Le fichier YAML porte déjà des commentaires et cela a été vérifié : la note explicative y est permise.

- [ ] **Étape 8 : franchissement complet, puis commit**

| Violation à provoquer                                                 | Refus attendu                                                                                      |
| --------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------- |
| Déposer `db/referentiel/99-sonde.sql` sans ligne dans `db/SOURCES.md` | `npm run regles` sort en **code non nul** **et** le message nomme `99-sonde.sql` — puis le retirer |
| Déposer un `db/compose.yaml` mal indenté                              | `npm run format:scripts` refuse **et** nomme le fichier — puis le retirer                          |
| Supprimer `db/SOURCES.md`                                             | `npm run regles` **crie** avec le code d'usage, il ne rend pas « aucune violation »                |

Lancer ensuite `npm run verify` : l'étape `regles` et l'étape `scripts:format` doivent rester vertes sur le dépôt réel.

```bash
git commit -m "ouvre la racine db et attache ses sources" -- db package.json .github/dependabot.yml .editorconfig scripts/regles-projet.mjs front/tests/harness
```

---

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

## Tâche 3 : Jetons, i18next, `chaine-en-dur` étendue, et la fermeture des trois exceptions Knip

**Docker : non requis.** Parallélisable avec les tâches 1, 2 et 4.

**Fichiers :**

- Créer : `front/src/ui/jetons.ts`, `front/src/lib/i18n.ts`, `front/src/locales/fr.json`, `front/src/locales/en.json`, `front/tests/harness/fixtures/chaine-en-constante.ts`, `front/tests/harness/i18n.test.ts`
- Modifier : `front/package.json`, `front/knip.json`, `scripts/regles-projet.mjs`, `front/tests/harness/regles-projet.test.ts`, `front/tests/harness/code-mort.test.ts`, `front/src/app/App.tsx`

**Interfaces :**

- Consomme : `lancerOutil`, `scripts/regles-projet.mjs`
- Produit : `jetons` (les jetons de design), `t()` d'i18next. Consommés par la tâche 11.

> **C'est ici que D31 commence à être honorée.** D31 dit d'elle-même que reconduire ses trois livrables serait son échec. Deux d'entre eux atterrissent ici, le troisième à la tâche 11.

- [ ] **Étape 1 : `front/src/ui/jetons.ts` — la cible qui n'existe pas**

D'après `docs/02-design.md` § 4 et D8, **verbatim** :

| Jeton                                                   | Valeur                                       |
| ------------------------------------------------------- | -------------------------------------------- |
| `surface-0` `surface-1` `surface-2`                     | `#14181D` `#1D232A` `#262E37`                |
| `line`                                                  | `#333D48`                                    |
| `ink` `ink-muted`                                       | `#E9E7E2` `#8A96A3`                          |
| `signal-under` `signal-ok` `signal-over` `signal-alert` | `#3D82C4` `#4FA37A` `#F2C230` `#D64541`      |
| Échelle typographique                                   | 11 / 13 / 15 / 19 / 24 / 30 / 38             |
| Base d'espacement                                       | 4 px                                         |
| Rayon de bordure                                        | **2 px** partout                             |
| Zone tactile minimale                                   | 48 × 48 px, espacement vertical minimal 8 px |
| Durées                                                  | 100 ms · 150 ms · 400 ms                     |
| Courbe                                                  | `cubic-bezier(0.25, 0.46, 0.45, 0.94)`       |

Le module exporte ces valeurs **et génère les variables CSS** que consommeront les feuilles de style. Pas de Tailwind (D42).

> **Cette règle ne protégeait rien jusqu'à aujourd'hui.** `couleur-hors-jetons` porte l'exclusion `/src[\\/]ui[\\/]jetons\./`, qui pointe un fichier **qui n'existe pas**. Une règle dont l'exclusion vise le vide s'applique partout et n'attrape rien, faute de code à lire. Le fichier lui donne enfin une cible — et l'étape 6 vérifie que l'exclusion mord bien sur lui et **seulement** sur lui.

- [ ] **Étape 2 : écrire l'épreuve de parité des locales, et la voir échouer**

`front/tests/harness/i18n.test.ts` :

```typescript
// 1. les deux fichiers de locale existent
// 2. l'ensemble des clés de fr.json est RIGOUREUSEMENT égal à celui de en.json
//    — dans les deux sens : une clé en trop d'un côté est une violation autant
//    qu'une clé manquante de l'autre
// 3. le message d'échec NOMME la ou les clés en écart, jamais un simple
//    « les objets diffèrent » : c'est la seconde assertion de D19 appliquée à
//    une comparaison de structures
```

Lancer : `npm --prefix front run test:harness -- i18n`
Attendu : **ÉCHEC** — les fichiers n'existent pas.

- [ ] **Étape 3 : installer et câbler i18next**

```bash
npm --prefix front install i18next react-i18next
```

Versions relevées sur le registre npm le 20/08/2026 : `i18next` **26.4.0**, `react-i18next` **17.0.12**, toutes deux **MIT**. Ne pas figer ces numéros dans la commande — le fichier de verrouillage fige.

```bash
node scripts/verifier-licences.mjs
```

Attendu : code 0. Si l'une des deux n'est pas sur la liste blanche de D13, **arrêter et signaler** — on n'ajoute pas d'exception sans l'inscrire au journal avec sa condition de sortie.

`front/src/lib/i18n.ts` câble `fr` et `en`, avec le français par défaut (`VITE_DEFAULT_LOCALE=fr` existe déjà dans `front/.env.example`).

- [ ] **Étape 4 : étendre `chaine-en-dur` aux constantes exportées**

**C'est le trou mesuré au lot 1 :** une chaîne d'interface déplacée dans une constante exportée échappe entièrement à la règle, qui ne regarde que le JSX. D31 livrable 3.

`front/tests/harness/fixtures/chaine-en-constante.ts` :

```typescript
export const MESSAGE_VIDE = 'Aucune séance enregistrée'
```

Étendre `chaine-en-dur` dans `scripts/regles-projet.mjs` : extensions `.ts` **et** `.tsx`, et une passe sur les **constantes exportées** dont la valeur est un littéral de texte contenant au moins un mot de trois lettres. Les mécanismes de neutralisation existants sont **réutilisés, pas réécrits** : `masquerCommentaires` (une chaîne dans un commentaire n'atteint pas l'écran), `MOT_DE_COPIE`, `CODE_DANS_LE_TEXTE`.

> **Chercher avant d'écrire.** `chainesEnDur()` existe déjà et porte quatre détecteurs, dont la neutralisation des commentaires et l'exclusion des comparaisons numériques. Réinventer ces heuristiques produirait deux vérités divergentes dans le même fichier.

Ajouter le test correspondant dans `regles-projet.test.ts`, **deux assertions** : code non nul **et** motif nommant `i18next`.

**Faux positif à borner explicitement :** `front/src/ui/jetons.ts` porte des noms de jetons et des valeurs, pas de la copie. Vérifier que la règle étendue **ne mord pas** dessus, et si elle mord, borner par la **commande**, jamais par une clé ajoutée au fichier de configuration (D20, D21).

- [ ] **Étape 5 : fermer les trois exceptions Knip — D24, « les trois lignes à la fois »**

`front/knip.json` :

1. **`src/core/index.ts` sort de `entry`.** Tant qu'il y est, **aucun module de `core/` n'est surveillé** par la détection de code mort. La condition de sortie inscrite par D24 est « dès que `core/` portera des modules réellement importés » — ce lot les lui donne.
2. **`ignoreDependencies` perd `@testing-library/react` et `@testing-library/jest-dom`** : le premier test de rendu de la tâche 11 les rend réellement utilisés.

> Si `src/core/` ne porte toujours aucun module importé à la fin de ce lot, **le dire au journal** au lieu de reconduire l'exception. Une liste d'exceptions qu'on n'a pas datées devient une liste qu'on n'ose plus toucher.

- [ ] **Étape 6 : franchissement — quatre violations, sept branches**

| #     | Violation à provoquer                                             | Refus attendu                                                                                      |
| ----- | ----------------------------------------------------------------- | -------------------------------------------------------------------------------------------------- |
| 1     | Écrire `#3D82C4` dans un composant de `front/src/`                | `npm run regles` refuse **et** le message nomme le jeton — la règle a enfin une cible              |
| 1 bis | Écrire `#3D82C4` **dans `front/src/ui/jetons.ts`**                | **accepté**, code 0 — l'exclusion mord sur le bon fichier, et sur lui seul                         |
| 2     | Ajouter une clé dans `fr.json` sans son équivalent dans `en.json` | l'épreuve refuse **en nommant la clé**                                                             |
| 2 bis | L'inverse — une clé dans `en.json` absente de `fr.json`           | refuse aussi. **Les deux sens** : une comparaison à sens unique laisse passer la moitié des écarts |
| 3     | Déplacer une chaîne d'interface dans une constante exportée       | `chaine-en-dur` refuse **et** nomme `i18next`                                                      |
| 4a    | Retirer les trois lignes d'exception Knip                         | `npm run front knip` doit être **VERT** — ce qui prouve qu'elles ne sont plus nécessaires          |
| 4b    | Réintroduire un export orphelin dans `front/src/core/`            | Knip doit **refuser** — ce qui prouve que `core/` n'échappe plus à la détection                    |

La violation 4 est **en deux temps, et les deux comptent** : la première prouve que l'exception était devenue inutile, la seconde que sa suppression a rendu la surveillance réelle. Retirer une exception sans la seconde branche ne prouve rien.

- [ ] **Étape 7 : vérifier et commit**

```bash
npm run verify
```

`front:knip` doit être verte **sans** les exceptions retirées. Puis :

```bash
git commit -m "installe les jetons et i18next, et ferme les trois exceptions knip" -- front scripts/regles-projet.mjs
```

---

---

## Tâche 4 : Les trois dettes de l'intégration continue

**Docker : non requis.** Parallélisable avec les tâches 1 à 3.

**Fichiers :**

- Créer : aucun
- Modifier : `.github/workflows/ci.yml`, `tests-harness/ci.test.mjs`, éventuellement `docs/gabarit-rapport-lot.md`

**Interfaces :**

- Consomme : `lancerOutil`, le paquet `yaml` de la racine
- Produit : un job `franchissement` qui **échoue** au lieu de sauter, et une étape qui interroge l'**état du service** Dependabot.

> **Trois dettes datées, toutes du lot 1.** (a) le job `franchissement` **saute** au lieu d'échouer quand l'un des cinq jobs dont il dépend tombe — dernière réserve du journal du lot 1, correctif d'une ligne ; (b) l'état du service Dependabot, que D30 renvoie explicitement au lot 2 ; (c) si les droits par défaut de `GITHUB_TOKEN` ne suffisent pas — **ce qui est possible et n'a pas été mesuré** — la vérification manuelle entre au gabarit de rapport de lot **plutôt que d'être abandonnée**.

- [ ] **Étape 1 : écrire les deux épreuves, et les voir échouer**

Dans `tests-harness/ci.test.mjs` :

```javascript
// 1. le job `franchissement` porte une condition qui le fait ÉCHOUER quand un
//    de ses `needs` échoue — pas seulement `needs:`, qui produit un job GRIS.
//    Assertion : la valeur `if:` du job contient `always()`, et au moins une
//    étape teste explicitement le résultat des jobs amont.
//
// 2. le job `securite` porte une étape qui interroge
//    /repos/.../vulnerability-alerts ET /automated-security-fixes.
//    Assertion : les deux chemins d'API apparaissent, et l'étape n'est pas
//    marquée `continue-on-error`.
```

Lancer : `npm run test:harness:back`
Attendu : **ÉCHEC** sur les deux.

- [ ] **Étape 2 : faire échouer `franchissement` au lieu de le faire sauter**

Aujourd'hui, `franchissement` déclare `needs: [front, backend, securite, e2e, performance]` et rien d'autre. Quand l'un des cinq tombe, GitHub **saute** le job : il apparaît gris, pas rouge.

> **Pourquoi c'est grave ici et pas ailleurs.** D29 rappelle qu'**aucune barrière côté serveur n'existe** : GitHub Free ne donne ni branches protégées ni rulesets sur un dépôt privé. Le job `franchissement` est ce qui _devrait_ dire « les garde-fous ont tourné ». Un job gris se lit comme « pas concerné », et une fusion passe sans qu'aucune épreuve de franchissement n'ait tourné.

Le correctif : `if: always()` sur le job, plus une étape qui **échoue explicitement** si l'un des `needs` n'est pas `success`. Nommer les cinq résultats dans le message — un « un job amont a échoué » sans nom oblige à rouvrir l'interface.

- [ ] **Étape 3 : interroger l'état du SERVICE Dependabot, pas le fichier**

D30 : le fichier `.github/dependabot.yml` était juste et **ne servait à rien**, parce que la fonctionnalité qu'il paramètre était désactivée en amont. `GET /repos/…/vulnerability-alerts` rendait **404** et `/automated-security-fixes` rendait `{"enabled": false}`.

Ajouter au job `securite` une étape qui interroge les deux points d'entrée et **échoue** si l'un des deux n'est pas actif.

- [ ] **Étape 4 : mesurer les droits de `GITHUB_TOKEN` — et écrire la réponse**

> **Non mesuré à ce jour.** Les droits par défaut de `GITHUB_TOKEN` ne couvrent peut-être pas cette lecture.

Lancer le job. Deux issues, toutes deux acceptables, **aucune silencieuse** :

- **Si l'appel réussit :** ajouter les permissions minimales explicites au job, et **vérifier qu'il échoue toujours** quand le service est éteint (étape 5, violation 2).
- **Si l'appel est refusé faute de droits :** ne pas abandonner le contrôle. Inscrire la vérification manuelle au `docs/gabarit-rapport-lot.md`, avec les deux commandes `gh api` exactes et le résultat attendu, et l'écrire au journal du lot comme une **limite mesurée**, pas comme un oubli.

- [ ] **Étape 5 : franchissement — quatre branches**

| #     | Violation à provoquer                                                            | Refus attendu                                                                   |
| ----- | -------------------------------------------------------------------------------- | ------------------------------------------------------------------------------- |
| 1     | Faire échouer volontairement le job `backend` (une commande `exit 1` temporaire) | `franchissement` est **ROUGE**, pas gris                                        |
| 1 bis | Rétablir                                                                         | `franchissement` est **vert** — et il a réellement lancé `npm run test:harness` |
| 2     | Désactiver les alertes Dependabot par l'API, relancer le job                     | l'étape **refuse**, en nommant lequel des deux points d'entrée est éteint       |
| 2 bis | Les réactiver, relancer                                                          | l'étape est **verte**                                                           |

> **Les deux branches de la violation 2, pas une.** La moitié « le service est éteint » est exactement celle qui avait échappé au lot 1 **pendant vingt-quatre heures**, pendant qu'une épreuve verte lisait un fichier correct. Une épreuve qui n'a jamais vu l'état éteint ne prouve pas qu'elle le verrait.

- [ ] **Étape 6 : commit**

```bash
git commit -m "fait echouer le job de franchissement et interroge le service dependabot" -- .github/workflows/ci.yml tests-harness/ci.test.mjs docs/gabarit-rapport-lot.md
```

---

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

## Tâche 7 : Les deux accesseurs, les politiques des cinq formes, et les huit épreuves d'isolation

**Docker : REQUIS. C'est le cœur du lot.**

**Fichiers :**

- Créer : `back/Palier.Database.Tests/IsolationTests.cs`
- Modifier : la migration `SocleInitial` (schéma `app`, accesseurs, politiques), `db/README.md`

**Interfaces :**

- Consomme : le schéma (tâche 6), la fixture en `palier_app` (tâche 5)
- Produit : les politiques et les accesseurs. Consommés par les tâches 8 à 11.

- [ ] **Étape 1 : le schéma `app` et ses DEUX accesseurs**

| Fonction                    | Comportement                                              | Pour                       |
| --------------------------- | --------------------------------------------------------- | -------------------------- |
| `app.utilisateur()`         | **lève**, `errcode 28000`, message **sans aucune valeur** | tables strictement privées |
| `app.utilisateur_ou_null()` | rend **NULL**                                             | tables à branche publique  |

Toutes deux lisent `current_setting('app.utilisateur', true)` — avec `missing_ok` à **`true`** : « If there is no such setting, `current_setting` throws an error **unless `missing_ok` is supplied and is `true`** (in which case NULL is returned) ». Sans ce second argument, la fonction lèverait une erreur **différente** de celle qu'on veut, et le message ne dirait pas ce qu'on croit.

> **Deux accesseurs, pas un.** Une politique de catalogue public qui appelle une fonction **qui lève** transforme toute lecture anonyme en erreur 500. Et le contournement naïf — `using (is_custom = false or owner_id = app.utilisateur())` — ne marche pas : la documentation **ne garantit aucun court-circuit**, elle dit que les expressions « will be evaluated for each row », et les politiques permissives multiples sont combinées par `OR` **sans ordre garanti**. La levée peut donc partir sur une ligne publique.

**Le message de la levée ne porte aucune valeur.** Ni identifiant, ni donnée. `01-conformite.md` § 4 : « Aucune donnée de santé dans les logs applicatifs ». La tentation d'y ajouter l'identifiant « pour déboguer » sera forte — l'épreuve de journalisation de la tâche 9 la garde.

- [ ] **Étape 2 : les politiques, chacune enveloppée dans un sous-select**

| Table                     | Politique                                                                                                                                       |
| ------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------- |
| `body_weight`, `workouts` | `using (owner_id = (select app.utilisateur()))` et `with check` identique                                                                       |
| `sets`                    | `using (exists (select 1 from workouts w where w.id = sets.workout_id and w.owner_id = (select app.utilisateur())))`                            |
| `exercises`               | **deux politiques permissives distinctes** : l'une `using (is_custom = false)`, l'autre `using (owner_id = (select app.utilisateur_ou_null()))` |
| `nutrient_refs`           | lecture publique, **écriture refusée**                                                                                                          |
| `AspNet*`                 | **aucune politique** — refus par défaut (D38)                                                                                                   |

> **Le sous-select n'est pas une optimisation, c'est ce qui supprime un faux arbitrage.** La documentation tranche la question laissée ouverte : l'expression est évaluée **par ligne**. `(select …)` force un **InitPlan** évalué une fois par instruction. On garde le `plpgsql` qui lève **et** la vitesse ; on n'échange pas la conformité contre la latence.
>
> **Réserve à connaître :** l'InitPlan étant évalué paresseusement, il **aggrave** le trou de la table vide. Raison de plus pour la garde applicative de la tâche 8.

**`AspNet*` sans politique, et c'est délibéré (D38).** « If no policy exists for the table, a default-deny policy is used, meaning that no rows are visible or can be modified. » La parade évidente — pas de RLS sur ces tables, ou `using (true)` — ferait de la **seule table sans barrière de ligne** celle qui portera les empreintes de mots de passe, les secrets TOTP, les jetons de rafraîchissement et les sessions. Le chemin de connexion lit `AspNetUsers` **par email avant que la moindre identité existe** : il ne peut pas passer par `app.utilisateur()`. **Le lot 4 doit concevoir ce chemin explicitement** — rôle dédié avec sa politique, ou fonction `SECURITY DEFINER` au périmètre minimal, **jamais une pose de l'identité d'autrui**. Poser le refus ici garantit qu'il sera _conçu_ et non _découvert_.

- [ ] **Étape 3 : `idle_in_transaction_session_timeout` sur le rôle applicatif**

`alter role palier_app set idle_in_transaction_session_timeout = …`

Motif : un cas d'usage appelant un modèle laisserait la transaction **ouverte pendant l'appel réseau** — _idle in transaction_, VACUUM bloqué, connexions épuisées. Aucun appel de modèle n'existe à ce lot ; la borne se pose maintenant parce qu'elle coûte une ligne et qu'elle ne se posera plus jamais au bon moment.

Et **un réglage accepté sans erreur n'est pas un réglage appliqué** (P11) : vérifier par `select setting from pg_settings` sur une connexion `palier_app`, pas en relisant la commande.

- [ ] **Étape 4 : écrire les huit épreuves d'isolation — toutes sur un vrai moteur, en `palier_app`**

`IsolationTests.cs`. Chaque test porte **deux assertions** : le comportement attendu **et** le motif (code SQL, nombre de lignes, nom de table).

| #     | Épreuve                                                        | Attendu                                                                                                                          |
| ----- | -------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------- |
| **1** | Table **non vide**, aucune identité posée                      | **erreur SQL `28000`**, jamais zéro ligne                                                                                        |
| **2** | `set_config(..., false)` au lieu de `true`                     | après `COMMIT`, sur la **même connexion**, `current_setting('app.utilisateur', true)` rend NULL ou vide, **et** une requête lève |
| **3** | A lit une ligne de B                                           | zéro ligne                                                                                                                       |
| **4** | A insère au nom de B                                           | refus par `WITH CHECK`                                                                                                           |
| **5** | `sets` : A ne voit aucune série d'une séance de B              | zéro ligne                                                                                                                       |
| **6** | Deux identités successives sur la **même connexion physique**  | la seconde ne voit **jamais** les lignes de la première                                                                          |
| **7** | `exercises` lu **sans identité**                               | les lignes `is_custom = false` **sortent, sans erreur** ; les lignes possédées ne sortent pas                                    |
| **8** | `palier_app` lit `AspNetUsers` → zéro ligne ; y insère → refus | les deux                                                                                                                         |

**L'épreuve 1 s'appelle explicitement « sur table peuplée » et porte son motif en commentaire :**

```csharp
// SUR TABLE PEUPLÉE, et le nom du test le dit.
// `ddl-rowsecurity.html` : l'expression d'une politique « will be evaluated
// FOR EACH ROW ». Sur une table VIDE, la politique n'est JAMAIS évaluée :
// aucune exception, zéro ligne. Cette épreuve y passerait pour la mauvaise
// raison. Le trou de la table vide est fermé par la garde applicative de la
// tâche 8, pas ici.
```

**L'épreuve 2 est la plus décisive du lot, et elle manquait à toutes les propositions antérieures.**

```
1. connexion unique, pooling désactivé (Pooling=false), ou une seule connexion physique
2. BEGIN ; select set_config('app.utilisateur', <A>, true) ; select ... ; COMMIT
3. sur la MÊME connexion, sans rien poser :
   select current_setting('app.utilisateur', true)   →  DOIT rendre NULL ou ''
   select * from workouts (table NON VIDE)           →  DOIT lever 28000
```

> **Pourquoi elle est décisive.** Le moteur ne garantit **rien du tout** si le troisième argument vaut `false` : la valeur passe en portée session, et l'on retombe exactement sur le mécanisme écarté — nettoyage délégué à Npgsql, annulé par `No Reset On Close`, le multiplexing, ou un PgBouncer en mode transaction, où la matrice marque `SET/RESET` comme « Never ».
>
> **Et l'épreuve 6 passe au vert avec `false`** : la seconde requête pose sa propre identité, écrase la précédente, et ne voit que ses lignes. Le test est vert, le mécanisme est cassé. Sans l'épreuve 2, **toute la sûreté du dispositif tient à un littéral booléen dans une ligne de C# que rien ne regarde** — la signature exacte du faux vert que le lot 1 a chassé vingt-trois fois.
>
> _Variante nommée et écartée :_ `SET LOCAL` porte la localité dans sa **syntaxe** et ne peut pas être fausse — mais elle n'accepte aucun paramètre lié et obligerait à concaténer un identifiant dans du SQL, **sur le chemin qui existe précisément pour se protéger de l'injection**. L'arbitrage est « argument lié » contre « localité syntaxique » ; il est tranché en faveur de l'argument lié, **et l'épreuve 2 remplace la garantie syntaxique**.

**Détail qui échoue bruyamment mais qu'il vaut mieux écrire :** `set_config` attend un `text` en deuxième argument. Un `Guid` passé en paramètre EF part en `uuid` et ne trouve pas la fonction. `.ToString()` est **obligatoire**.

- [ ] **Étape 5 : franchissement — les huit épreuves vues ROUGES**

Chacune se franchit en désarmant son correctif, **une par une** :

| #    | Ce qu'on désarme                                                                     | L'épreuve doit rougir                                                                                                     |
| ---- | ------------------------------------------------------------------------------------ | ------------------------------------------------------------------------------------------------------------------------- |
| 1    | retirer la politique de `workouts`                                                   | oui                                                                                                                       |
| 2    | passer le troisième argument de `set_config` à `false`                               | **oui — c'est le franchissement le plus important du lot**                                                                |
| 3, 4 | remplacer `using` par `using (true)`                                                 | oui                                                                                                                       |
| 5    | retirer le `exists` de `sets`                                                        | oui                                                                                                                       |
| 6    | poser l'identité hors transaction                                                    | oui                                                                                                                       |
| 7    | fusionner les deux politiques d'`exercises` en un seul `OR` avec `app.utilisateur()` | l'épreuve rougit, ou devient **instable selon le plan** — les deux sont des échecs, et l'instabilité est le pire des deux |
| 8    | ajouter `using (true)` sur `AspNetUsers`                                             | oui                                                                                                                       |

> **Une branche jamais franchie est une branche qui ment.** L'épreuve doit **rougir quand on désarme le correctif**, pas seulement passer quand tout va bien. Consigner les huit rouges au journal du lot, violation par violation. Une épreuve qui n'a été vue que verte n'a rien prouvé.

- [ ] **Étape 6 : mesurer le coût, plutôt que le supposer**

Le mécanisme ajoute **trois** allers-retours par cas d'usage — `BEGIN`, `set_config`, `COMMIT` — **pas un**. Mesurer la durée d'une requête sur `sets` avec un `exists` sur `workouts`, dont la politique de `workouts` s'applique **à son tour** dans la sous-requête. Comparer au plafond de **100 ms** de `docs/08-workflow.md` § 6.

> Le plafond **n'est pas acquis**, et la mesure porte sur Testcontainers, pas sur l'instance managée qui n'existe pas. Écrire les deux chiffres avec leur instrument, et dire ce qu'ils ne prouvent pas.

- [ ] **Étape 7 : commit**

```bash
git commit -m "eprouve l isolation des cinq formes de table" -- back db
```

---

---

## Tâche 8 : La pose de l'identité côté C#, et la garde applicative en amont

**Docker : REQUIS.**

**Fichiers :**

- Créer : le comportement de pipeline dans `back/Palier.Application/`, `back/Palier.Application.Tests/` ou `back/tests-harness/` pour le test de réflexion, `back/Palier.Database.Tests/GardeApplicativeTests.cs`
- Modifier : `back/Palier.Api/Program.cs` (injection), `docs/superpowers/specs/2026-08-19-architecture-backend-csharp-design.md` **ligne 108**

**Interfaces :**

- Consomme : les accesseurs et politiques (tâche 7)
- Produit : le seul endroit du code où l'identité est posée. Consommé par la tâche 9.

- [ ] **Étape 1 : réécrire la ligne 108 de la spec d'architecture — dans CETTE tâche**

`docs/superpowers/specs/2026-08-19-architecture-backend-csharp-design.md` § 5 porte : « **Transaction — les commandes seulement, jamais les requêtes** ».

> **Elle est incompatible avec ce mécanisme**, et la laisser produirait une politique qui **mord sur les écritures et disparaît sur les lectures** — le chemin le moins dangereux gardé, le plus dangereux ouvert. La réécriture appartient à cette tâche, pas à la tâche 13 : une spec qui contredit le code qu'on est en train d'écrire se corrige au moment où on l'écrit.

Nouvelle règle : **commandes ET requêtes** s'exécutent dans une transaction ouverte par le comportement de pipeline.

- [ ] **Étape 2 : le comportement de pipeline**

Trois choses, dans cet ordre :

1. **Refuser AVANT d'ouvrir la transaction** quand l'identité manque, avec **le nom du cas d'usage** dans le message ;
2. ouvrir la transaction, puis `select set_config('app.utilisateur', {identifiant}, true)` — identifiant en **paramètre lié**, `Guid.ToString()` ;
3. le tout dans `Database.CreateExecutionStrategy().ExecuteAsync(...)`.

> **Pourquoi la garde applicative n'est pas une ceinture de plus.** Sur une table **vide**, l'accesseur n'est jamais appelé : identité absente et « cet utilisateur n'a pas de données » redeviennent **indiscernables**. Et la loudness devient **dépendante du plan** — avec l'index `workouts (owner_id, started_at desc)`, le planificateur peut hisser la fonction `stable` en clé de parcours et lever même sur table vide ; en parcours séquentiel, non. **Une propriété de sécurité qui dépend du plan d'exécution n'est pas une propriété.**
>
> **Conséquence à écrire et non à contourner :** on cesse d'annoncer que l'échec crie inconditionnellement. La fonction SQL est le **filet**, jamais le garde unique.
>
> **Pourquoi `CreateExecutionStrategy` est une obligation, pas une précaution.** Microsoft Learn, _Connection Resiliency_, vérifié : « if your code initiates a transaction using `BeginTransactionAsync()` … You will receive an exception … does not support user-initiated transactions. Use the execution strategy returned by `DbContext.Database.CreateExecutionStrategy()`. » Le jour où quelqu'un activera `EnableRetryOnFailure` sur une base managée qui clignote, **toutes** les requêtes lèveraient d'un coup.

- [ ] **Étape 3 : le test de réflexion — « rien d'autre ne peut le faire »**

Un test qui parcourt les assemblages `Palier.Application` et `Palier.Api` et **interdit qu'un type hors des handlers prenne `PalierDbContext`** en dépendance — constructeur, propriété ou champ.

**Une trentaine de lignes, aucune dépendance nouvelle.** `System.Reflection` suffit. `CLAUDE.md` § 1 déconseille d'inventer un outillage quand un mécanisme simple suffit, et `chercher avant d'écrire` s'applique aussi aux paquets : NetArchTest ferait la même chose au prix d'une dépendance, d'une licence à vérifier et d'un Dependabot de plus.

> **C'est la différence entre « le pipeline le fait » et « rien d'autre ne peut le faire ».** Un `IHostedService`, une tâche de fond, un contrôle de santé ou une file de rejeu n'ont **pas de transaction**, donc pas d'identité — bruyant sur table peuplée, **silencieux sur table vide**.

Le message d'échec **nomme le type fautif**. Deux assertions : le refus **et** le nom.

- [ ] **Étape 4 : franchissement — trois violations, dont une inversée**

| #     | Violation à provoquer                                                                             | Refus attendu                                                                             |
| ----- | ------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------- |
| **1** | Appeler un cas d'usage sans identité, **sur une table VIDE**                                      | un **refus applicatif nommant le cas d'usage** — jamais un code SQL nu, jamais zéro ligne |
| **2** | Écrire un `IHostedService`, une tâche de fond ou un contrôle de santé qui prend `PalierDbContext` | le test de réflexion refuse **en nommant le type**                                        |
| **3** | Activer `EnableRetryOnFailure` dans la fixture                                                    | **tout reste VERT**                                                                       |

> **La violation 1 ferme le trou de la table vide.** Sans cette garde, l'identité manquante est indiscernable de « cet utilisateur n'a pas de données » **les premiers jours de production — précisément quand toutes les tables sont vides**.
>
> **La violation 3 est une épreuve inversée, et c'est voulu.** Le vert **prouve** que le passage par `CreateExecutionStrategy` est réel. Sans lui, l'exception documentée par Microsoft ferait lever toutes les requêtes d'un coup, le soir où quelqu'un activera la résilience. Une épreuve inversée doit être **annotée comme telle** dans le code, sinon quelqu'un la « corrigera » en la rendant rouge.
>
> **Et il faut vérifier que l'étape 3 a bien PRIS EFFET** : que `EnableRetryOnFailure` est réellement actif pendant l'exécution, pas seulement écrit. Une expérience dont une étape n'a pas eu lieu n'est pas une expérience.

- [ ] **Étape 5 : commit**

```bash
git commit -m "pose l identite dans le pipeline et refuse le contexte hors handler" -- back docs/superpowers/specs
```

---

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

## Tâche 11 : L'écran d'état, ses quatre états, et le parcours au clavier

**Docker : REQUIS** — l'état d'erreur se provoque en arrêtant le conteneur, et le contenu vient de l'API réelle.

**Fichiers :**

- Créer : `front/src/features/etat/`, ses tests de rendu, `front/tests/e2e/etat-clavier.spec.ts`, `front/tests/harness/etats-ecran.test.ts`
- Modifier : `front/src/app/App.tsx`, `front/src/locales/{fr,en}.json`

**Interfaces :**

- Consomme : `jetons` et `t()` (tâche 3), `GET /api/v1/sante` (tâche 9)
- Produit : le premier écran du projet. **C'est ici que D31 est honorée, ou qu'elle a échoué.**

> **D41 — l'écran d'état est un instrument, et sa fin de vie est datée.** Il n'affiche aucune donnée de santé et aucun compte, et **il n'entre pas au périmètre V1** de `docs/00-produit.md`. Au lot 4 il passe derrière l'authentification et un rôle d'administration ; **au lot 6, la question « le garde-t-on ou le supprime-t-on » est reprise explicitement et tranchée**, pas laissée à l'inertie. Sans cette échéance écrite, l'écran resterait par inertie sur un domaine public.
>
> **Pourquoi celui-là et pas un écran produit.** C'est le plus petit écran qui ait honnêtement quatre états, et **le seul dont l'état d'erreur se provoque** — `npm run db:down`, recharger — au lieu de se simuler. Un écran de séance suppose l'authentification et le domaine ; le construire contre une donnée simulée produirait une épreuve **qui garde un simulacre**, une marche au-dessus du défaut que D31 voulait éviter.

- [ ] **Étape 1 : écrire l'épreuve des états, et la voir échouer — D31 livrable 1**

`front/tests/harness/etats-ecran.test.ts` : une épreuve qui **refuse un écran dépourvu d'état vide ou d'état d'erreur**, et qui **nomme lequel manque**.

```typescript
// Deux branches, éprouvées SÉPARÉMENT. Une épreuve qui dit « il manque un
// état » sans dire lequel oblige à relire le composant ; et surtout, une
// épreuve qui ne teste que l'un des deux passe au vert quand l'autre est retiré.
```

Lancer : `npm --prefix front run test:harness -- etats-ecran`
Attendu : **ÉCHEC** — l'écran n'existe pas.

- [ ] **Étape 2 : l'écran et ses quatre états**

`front/src/features/etat/` :

| État       | Déclencheur                                                             |
| ---------- | ----------------------------------------------------------------------- |
| chargement | l'appel est en cours                                                    |
| vide       | schéma migré, **référentiel non chargé** — `nutrient_refs` à zéro ligne |
| erreur     | conteneur arrêté, API injoignable                                       |
| contenu    | la réponse de `GET /api/v1/sante`                                       |

**Toutes chaînes en i18next.** **Tous jetons issus de `front/src/ui/jetons.ts`.** Aucune couleur en dur, aucune ombre, aucun dégradé, aucune flèche Unicode — `npm run regles` le vérifie.

- [ ] **Étape 3 : le parcours Playwright au clavier seul — D31 livrable 2**

`front/tests/e2e/etat-clavier.spec.ts` : atteindre et actionner chaque élément interactif **au clavier seul**, sans souris. Et axe-core vert sur cet écran.

- [ ] **Étape 4 : franchissement — quatre violations, cinq branches**

| #     | Violation à provoquer                                 | Refus attendu                                   |
| ----- | ----------------------------------------------------- | ----------------------------------------------- |
| 1a    | Retirer **l'état vide** du composant                  | l'épreuve refuse **en nommant l'état vide**     |
| 1b    | Retirer **l'état d'erreur**                           | l'épreuve refuse **en nommant l'état d'erreur** |
| **2** | `npm run db:down`, puis **recharger l'écran**         | **l'état d'erreur s'affiche POUR DE VRAI**      |
| 3     | Rendre un élément interactif inatteignable au clavier | le parcours Playwright refuse                   |
| 4     | Supprimer un `aria-label`                             | axe-core refuse                                 |

> **La violation 2 est la seule qui compte vraiment.** Pas une donnée simulée, pas un `mock` : l'état réel, **provoqué**. C'est la règle du projet — « provoquer l'absence, pas seulement constater la présence » — et l'écran d'état existe précisément parce qu'il rend cette provocation possible à coût dérisoire. Une capture ou la sortie exacte au journal du lot ; « je l'ai vu » n'est pas une mesure.
>
> **Les violations 1a et 1b sont deux branches, pas une.** Retirer l'un des deux états et voir l'épreuve rougir ne dit rien de l'autre.

- [ ] **Étape 5 : Knip retrouve ses deux dépendances**

Les tests de rendu de cette tâche rendent `@testing-library/react` et `@testing-library/jest-dom` **réellement utilisés**. Vérifier que `npm run front knip` est vert **sans** les exceptions retirées à la tâche 3. Si ce n'est pas le cas, **le dire au journal** plutôt que de remettre l'exception.

- [ ] **Étape 6 : D31 — le verdict, écrit**

D31 dit d'elle-même : « **Si le lot 2 se termine sans ces trois livrables, cette décision a échoué et il faut le dire au lieu de la reconduire.** »

Écrire au journal du lot, en toutes lettres : les trois livrables sont-ils là, oui ou non, chacun avec sa preuve de franchissement. **Un « partiellement » n'existe pas** — c'est une reconduction déguisée.

- [ ] **Étape 7 : commit**

```bash
git commit -m "livre l ecran d etat et ses quatre etats" -- front
```

---

---

## Tâche 12 : Remesurer `verify` et arbitrer ce que le porteur autorise

**Docker : REQUIS** — la mesure n'a de sens qu'avec `back:test` complet.

**Fichiers :**

- Créer : aucun
- Modifier : `scripts/verify.mjs` (seulement si le porteur autorise un levier), `tests-harness/verify.test.mjs`, le rapport de lot

**Interfaces :**

- Consomme : `npm run verify`
- Produit : la mesure étape par étape, publiée au rapport de lot.

- [ ] **Étape 1 : remesurer, avant d'invoquer quoi que ce soit**

> **La seule mesure consignée à ce jour est 129,1 s pour 15 étapes** (`progress.md`), et `scripts/verify.mjs` en porte **16** aujourd'hui. **L'écart doit être remesuré avant d'être invoqué** — un chiffre sans l'instrument qui l'a produit est suspect, et sept instruments se sont trompés en une semaine sur ce projet.

```bash
npm run verify
```

Publier la table complète, étape par étape, au rapport de lot. Le seuil d'avertissement est à 90 s et **il n'a pas été relevé** : `08-workflow.md` § 5 fait de la lenteur un défaut à traiter, pas un fait à enregistrer.

- [ ] **Étape 2 : n'appliquer que les leviers que le porteur autorise**

Trois leviers sont chiffrés par D26, **aucun n'est appliqué d'office** :

| Levier                                                                          | Gain mesuré au lot 1 | État                     |
| ------------------------------------------------------------------------------- | -------------------- | ------------------------ |
| Retirer `--no-incremental` de `back:build`                                      | 14,0 s               | à arbitrer               |
| Mettre en cache les réponses des registres npm et NuGet du contrôle de licences | 7,6 s + 12,7 s       | à arbitrer               |
| Sortir les épreuves de `front:test`                                             | −39 s                | **redevient discutable** |

Le troisième **redevient discutable** puisque `front/src/` porte enfin des tests — l'argument du lot 1 (« l'étape deviendrait vide et verte sans rien contrôler ») tombe. Mais **D5 et D25 interdisent de retirer une épreuve de `verify`** : elles peuvent **changer d'étape, jamais quitter la suite**.

- [ ] **Étape 3 : franchissement — deux violations**

| #   | Violation à provoquer                                                                    | Refus attendu                                                 |
| --- | ---------------------------------------------------------------------------------------- | ------------------------------------------------------------- |
| 1   | Retirer une étape de `scripts/verify.mjs`                                                | `tests-harness/verify.test.mjs` refuse **en nommant l'étape** |
| 2   | Faire énumérer des contrôles au hook de pré-envoi au lieu de déléguer à `npm run verify` | l'épreuve refuse                                              |

> **La violation 2 est le garde-fou de D25, et il doit continuer de mordre APRÈS l'ajout de Docker à la boucle.** C'est exactement le moment où la tentation d'un `--filter` — donc d'une seconde liste — apparaît.

- [ ] **Étape 4 : commit, si quelque chose a changé**

```bash
git commit -m "remesure la boucle de verification" -- scripts/verify.mjs tests-harness/verify.test.mjs
```

---

---

## Tâche 13 : La reprise documentaire et le journal des décisions

**Docker : non requis.** Dernière tâche.

**Fichiers :**

- Modifier : `docs/decisions.md`, `docs/07-roadmap.md`, `docs/16-projet.md`, `docs/03-donnees.md`, `docs/securite/asvs-l2.md`, `DEMARRAGE.md`, `docs/superpowers/specs/2026-08-19-architecture-backend-csharp-design.md`

**Interfaces :**

- Consomme : tout ce que les tâches 1 à 12 ont mesuré
- Produit : le dossier à jour.

> **Cette tâche est de nature documentaire et son contrôle est de nature différente. Il faut le dire au lieu de faire semblant** qu'une épreuve de franchissement garde de la prose.

- [ ] **Étape 1 : `docs/decisions.md` — D32 à D43, écrites AVANT le lot, à confronter à ce qu'il a mesuré**

> **Fait au 20/08/2026, commit `26773be` :** les douze entrées **sont déjà écrites**, ainsi que la reprise de `docs/07-roadmap.md`. Cette étape ne les crée donc pas : elle les **confronte aux mesures du lot**. Une décision écrite avant l'exécution est une intention ; ce qui la valide ou la rouvre, ce sont les tâches 1 à 12.

Chaque entrée porte, comme D1 à D31 : **ce qui est tranché**, **le motif** (avec les mesures et leurs instruments), et **ce qui la rouvrirait**. Reprendre chacune avec ce que le lot a réellement mesuré, et **corriger toute affirmation que l'exécution a contredite** — notamment D34 (le tag réellement tiré), D37 (le résultat des deux branches de `pg_dump`, tâche 10) et D36 (le coût des trois allers-retours, tâche 7 étape 6).

| #   | Titre                                                                                                                               |
| --- | ----------------------------------------------------------------------------------------------------------------------------------- |
| D32 | Arborescence à trois racines : `front/`, `back/`, `db/`. Les migrations restent dans `back/`                                        |
| D33 | Docker : oui, au lot 2, et pour PostgreSQL seul                                                                                     |
| D34 | PostgreSQL 18.6, image Debian, un seul endroit où le tag est écrit                                                                  |
| D35 | La clé d'`AspNetUsers` est un `uuid`, et les tables d'identité naissent au lot 2                                                    |
| D36 | L'identité parvient au moteur par `set_config('app.utilisateur', $1, true)`, en portée transaction, doublée d'une garde applicative |
| D37 | Trois rôles PostgreSQL, trois chaînes de connexion, `FORCE ROW LEVEL SECURITY`, et une assertion contre la base réelle              |
| D38 | Les tables d'identité naissent en refus par défaut ; leur chemin d'accès est conçu au lot 4                                         |
| D39 | Le schéma arrive par tranches, avec le cas d'usage qui l'exige                                                                      |
| D40 | Aucune donnée de production sur un poste de développement                                                                           |
| D41 | L'écran d'état est un instrument, et sa fin de vie est datée                                                                        |
| D42 | Pas de Tailwind au lot 2 ; les jetons vivent dans `front/src/ui/jetons.ts` et en variables CSS                                      |
| D43 | La feuille de route porte les durées constatées, lot par lot                                                                        |

**Trois de ces entrées ne sont PAS prises tant que le porteur n'a pas répondu :** D37 (le `FORCE`), D39 (l'écart au schéma complet), D42 (Tailwind). Les écrire **avec leur statut**, jamais comme des faits acquis.

- [ ] **Étape 2 : `docs/07-roadmap.md` — une PROPOSITION, jamais un fait accompli**

> **`CLAUDE.md` § 6 interdit de modifier le contenu métier sans validation.** Cette étape produit une proposition de réécriture, soumise avant application.
>
> **Fait au 20/08/2026, commit `26773be` :** la reprise est **déjà écrite** — bandeau daté, étape 1 scindée en 1a/1b, durée révisée à 8-12 mois avec sa base, table étape ↔ lot ↔ durée constatée de D43, jugements produit conservés mot pour mot. **Elle reste une proposition tant que le porteur ne l'a pas validée**, et D39 en particulier n'est pas prise. Cette étape vérifie que chaque point ci-dessous y figure, et **soumet l'ensemble** ; elle ne réécrit pas ce qui est déjà juste.

Points principaux, tous à soumettre :

- **Un bandeau daté en tête**, sur le modèle déjà appliqué le 20/08/2026 à six autres documents. Motif : `07-roadmap.md` est le **dernier document du dossier** à décrire encore Supabase et Vercel sans en-tête de reprise, **alors qu'il est de rang 3** dans la hiérarchie de `CLAUDE.md` § 5 — un agent qui applique cette hiérarchie lit le rang 3 et **réintroduit Supabase**.
- **Trois lignes seulement sont factuellement fausses** : L32 (ESLint → Oxlint, D18), L34 (Supabase → PostgreSQL managé chez OVHcloud), L39 (Vercel → image conteneur, **mais le plafond de dépense survit : sa cible se déplace sur les appels aux modèles**). Le reste est soit vrai tel quel, soit vrai mais devenu plus cher.
- **Les jugements du document sont conservés MOT POUR MOT** : la durée, la condition de sortie de l'étape 2 (L75, L77), la dépendance au diététicien, le paragraphe sur l'avatar 3D (L116, L172), L24, L26, L98, L150-152, L162. Ils ne dépendaient d'aucune pile.
- **Scinder l'étape 1** en « 1a — Harnais (livré) » et « 1b — Socle ». Les confondre fait croire l'étape close.
- **Quatre points d'étape 0 à ajouter**, créés par D15 et D17 : DPA OVHcloud (**couplé au point 3 — le statut — donc leur ordre n'est pas libre**), quel produit OVHcloud (VPS ou Public Cloud, **bloquant pour l'AIPD**), la certification HDS est-elle pertinente, et un **fournisseur d'email transactionnel européen avec DPA**.
- **La durée** : conserver « Toute estimation plus courte est une estimation fausse » mot pour mot, et remplacer le chiffre unique par trois lignes de natures différentes — semaines de développement, **onze semaines de fenêtres calendaires irréductibles** écrites dans le document même (L75 et L150), et délais externes de l'étape 0.
- **Une table étape ↔ lot ↔ date de début ↔ date de fin ↔ durée constatée (D43), dans CE fichier et jamais dans un second** : deux documents portant l'ordre de construction divergeraient, et **aucune épreuve de franchissement ne peut vérifier que deux textes en prose disent la même chose**.
- **Un point manquant, décidé nulle part : le stockage des photos** (étape 4). Supabase Storage était implicite ; il n'y a plus rien. Trois voies, dont le coût de conformité diffère — et la plus économe est de **ne rien stocker**.

- [ ] **Étape 3 : `docs/03-donnees.md` — fermer les deux points ouverts**

- Les deux points ouverts **fermés** par D35 (type de clé) et D36 (mécanisme d'identité) ;
- les trois `auth.uid()` substitués **une fois et en connaissance de cause** ;
- **la liste NOMMÉE des tables hors du modèle `owner_id`, avec la forme de politique de chacune** — identité, catalogues publics, possédées par jointure. Sans elle, **la même épreuve prouve deux choses contradictoires selon la table qu'on lui donne**, et `08-workflow.md` § 6 (« test RLS vert pour chaque table ») reste en contradiction ouverte ;
- **à soumettre :** la phrase qui range « une injection SQL ou une requête brute » parmi ce que RLS protège est **trop généreuse**, et elle le reste avec ce mécanisme — un SQL injecté peut **reposer** `app.utilisateur`. RLS y protège du **filtre oublié**, pas de l'attaquant délibéré. Soit la ligne est corrigée, soit elle est assumée comme décrivant une intention et non une garantie. **Les deux se disent, aucune ne se suppose.**

- [ ] **Étape 4 : les quatre autres documents**

| Document                          | Ce qui change                                                                               |
| --------------------------------- | ------------------------------------------------------------------------------------------- |
| `docs/16-projet.md` § 1           | l'arborescence gagne `db/`                                                                  |
| `docs/16-projet.md` § 4           | la distinction **référentiel** / **démonstration**, que le mot « seed » confond aujourd'hui |
| `DEMARRAGE.md` § 0                | Docker en prérequis, avec `docker info` et non `docker --version`                           |
| `docs/securite/asvs-l2.md`        | la ligne **V8 déplace RLS du lot 4 au lot 2**                                               |
| spec d'architecture § 5 ligne 108 | **déjà réécrite à la tâche 8** — vérifier qu'elle l'est                                     |

- [ ] **Étape 5 : le contrôle, de nature documentaire — trois choses**

1. **`prettier --check` sur les fichiers touchés.**

   > **Fait mesuré, hors périmètre de cette tâche mais à signaler :** **20 fichiers Markdown du dépôt échouent à `prettier --check`** aujourd'hui, dont `CLAUDE.md`, `README.md`, `docs/16-projet.md`, `docs/08-workflow.md` et les deux specs — et **aucune étape de `verify` ni aucun job de la CI ne les regarde** : `format:scripts` ne cible que `scripts/`, `tests-harness/` et `.lintstagedrc.mjs`, et le `format:check` du front tourne depuis `front/`. Pire, `.lintstagedrc.mjs` les **reformaterait au commit** puisque son motif porte `md`, ce qui rend la non-conformité **invisible et intermittente**. Les fichiers repris le 20/08 sont propres ; les autres non. **Question 8 en fin de plan.**

2. **Une relecture qui vérifie qu'aucune décision D1 à D31 n'est contredite en silence.** Une contradiction se signale avec ses références exactes ; elle ne se tranche pas.

3. **`CLAUDE.md` § 6 :** cette tâche produit une **proposition** de réécriture de `07-roadmap.md`, jamais un fait accompli.

- [ ] **Étape 6 : commit**

```bash
git commit -m "reprend le dossier et ecrit les decisions du lot 2" -- docs DEMARRAGE.md
```

---

---

## Ce qui reste ouvert et n'appartient pas aux exécutants

**Ces douze points ne se tranchent pas dans une tâche.** Ils sont portés au rapport de fin de lot et remontés au porteur du projet. Un implémenteur qui rencontre l'un d'eux **s'arrête et signale**.

1. **Activer WSL2 ou Hyper-V — bloquant, et personne d'autre ne peut le faire.** Droits administrateur et redémarrage. Tâche 0.
2. **Accepter que `npm run verify` — donc `git push` — exige désormais un moteur de conteneurs.** C'est un changement du contrat de la boucle, cohérent avec « un contrôle qui n'a plus sa cible doit crier », mais il appartient au porteur. Le hook de pré-**commit** n'est pas touché.
3. **`FORCE ROW LEVEL SECURITY` : oui ou non, en connaissance du prix.** La tâche 10 le mesure sur Testcontainers ; si la branche « dump réussi » n'est pas obtenue, la décision revient au porteur.
4. **Docker Desktop ou Podman Desktop.** Docker Desktop est gratuit sous les deux seuils — moins de 250 salariés **et** moins de 10 M$ de chiffre d'affaires. Podman et Rancher Desktop suppriment la question de licence pour toujours, au prix d'une configuration de Testcontainers. **Si l'idée d'un seuil contractuel à surveiller déplaît, c'est maintenant qu'il faut choisir, pas dans deux ans.**
5. **L'écart au « schéma complet » de la feuille de route (D39).** `CLAUDE.md` § 5 range `07-roadmap.md` au-dessus de `CLAUDE.md`. **Sans validation explicite, D39 n'est pas prise.**
6. **Tailwind, oui ou non (D42).** Dépendance lourde au sens de `CLAUDE.md` § 6, et elle rendrait `couleur-hors-jetons` **aveugle sans rien signaler**.
7. **D27 — Lighthouse CI, toujours sans réponse depuis le lot 1.** Sept vulnérabilités hautes, dont une **sans version corrigée** (`extract-zip`, plage `*`), exécutées dans le job `performance` avec accès au dépôt en lecture. Le risque est **déplacé hors de la vue de `npm audit`**, pas éliminé.
8. **Le formatage des documents.** Deux voies : étendre le périmètre à `docs/` et à la racine — ce qui reformate 20 fichiers d'un coup, **y compris `CLAUDE.md`, qui est le document du porteur** — ou déclarer la documentation hors périmètre et **l'écrire**, plutôt que de laisser une zone aveugle non nommée.
9. **La phrase de `docs/03-donnees.md` § RLS sur l'injection SQL** — contenu métier, donc non modifiable sans validation.
10. **La contradiction entre `08-workflow.md` § 6 et les trois familles de tables** hors du modèle `owner_id`.
11. **Deux points d'étape 0 devenus des prérequis contractuels du lot 4, à engager maintenant à cause de leur délai :** le **DPA OVHcloud** — couplé au statut, donc l'ordre n'est pas libre — et un **fournisseur d'email transactionnel européen avec DPA**, sans lequel la vérification d'email de `09-comptes.md` § 1 n'a **aucun moyen d'envoi**.
12. **Trois questions de schéma que le lot 2 n'a pas le droit de trancher seul :**
    - (a) `programs.owner_id` est `not null references auth.users`, mais `docs/16-projet.md` § 4 prévoit **9 programmes modèles** livrés : le schéma n'a **aucune place pour un programme sans propriétaire** ;
    - (b) `03-foods.sql` (300 aliments CIQUAL) est-il du **référentiel de production** ou un jeu de développement ? La réponse change le fichier qui l'applique et le moment ;
    - (c) `docs/16-projet.md` § 2 interdit le préfixe `I` sur les types **sans distinguer le front du backend** — or les analyseurs Roslyn du dépôt, avec `AnalysisLevel: latest-all` et `TreatWarningsAsErrors`, **exigent** ce préfixe sur les interfaces C#. La règle `type-prefixe-i` de `scripts/regles-projet.mjs` ne porte aujourd'hui que sur `.ts` et `.tsx`, donc rien ne casse — **mais la contradiction est écrite dans le document, et elle mordra au premier port C# vers le front.**

---

## Auto-revue du plan

**Ce que ce plan reprend d'une conception, et ce qu'il en écarte.** Il transforme en tâches exécutables une conception à cinq études et trois verdicts adversariaux. Les quatorze tâches pressenties sont conservées ; **seul leur ORDRE change**, pour que la voie sèche (tâches 1 à 4) avance pendant que le blocage matériel dure. La correspondance : plan 1↔conception 1 · 2↔2 · **3↔9** · **4↔11** · **5↔3** · **6↔4** · **7↔5** · **8↔6** · **9↔7** · **10↔8** · **11↔10** · **12↔12** · 13↔13.

**Les trois amendements bloquants du jury, et où ils atterrissent.**

| Amendement                                                              | Tâche                                                                              | Forme                                                                                                                      |
| ----------------------------------------------------------------------- | ---------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------- |
| Les épreuves ne distinguaient pas `set_config(..., true)` de `false`    | **7, épreuve 2**                                                                   | connexion unique, `Pooling=false`, `current_setting` après `COMMIT`, et le franchissement en passant le littéral à `false` |
| « L'échec crie » est faux sur une table vide — RLS évalue **par ligne** | **7 (épreuve 1, nommée « sur table peuplée ») et 8 (violation 1, sur table VIDE)** | la promesse de loudness inconditionnelle est **abandonnée**, pas contournée ; la garde applicative devient obligatoire     |
| Les tables d'identité seraient les seules sans barrière                 | **6 (RLS forcée sur `AspNet*`) et 7 (aucune politique, épreuve 8)**                | refus par défaut, et le chemin de connexion **conçu au lot 4**, pas découvert                                              |

**Les cinq exigences des points non bloquants du jury**, toutes inscrites : le seed sous `FORCE` par politique d'écriture explicite (tâche 6, étape 4) · le test de réflexion contre le `DbContext` hors pipeline (tâche 8) · l'assertion de démarrage contre la base réelle (tâche 9) · la troisième chaîne de connexion et la restauration promue en épreuve (tâche 10) · les deux accesseurs et les deux politiques permissives séparées (tâche 7).

**Les épreuves, une par garde-fou.** Attribution des sources (T1) · format `db/` (T1) · panne du moteur Docker (T2) · divergence du nom de service (T2) · les deux branches gitleaks (T2) · couleurs hors jetons **avec sa cible** (T3) · parité des locales dans les deux sens (T3) · chaîne en constante exportée (T3) · Knip sans ses trois exceptions, **et** Knip qui mord sur `core/` (T3) · `franchissement` rouge et non gris (T4) · état du service Dependabot, éteint **et** allumé (T4) · rôle `palier_app` non superutilisateur et sans `BYPASSRLS` (T5) · tag lu et non redéclaré (T5) · RLS activée **et** forcée, en forme de catalogue (T6) · dérive du modèle C# (T6) · vue avec son `Down` (T6) · borne `CHECK` (T6) · **les huit épreuves d'isolation** (T7) · garde applicative sur table vide (T8) · `DbContext` hors handler (T8) · `CreateExecutionStrategy` réel (T8) · les trois assertions de démarrage (T9) · données de santé au journal (T9) · `EnableSensitiveDataLogging` en production (T9) · `pg_dump` qui échoue sous `FORCE` (T10) · dump et restauration à nombre de lignes égal (T10) · état vide et état d'erreur, séparément (T11) · **l'erreur provoquée pour de vrai** (T11) · clavier seul (T11) · axe-core (T11) · étape retirée de `verify` (T12) · hook qui énumère (T12). **Trente-trois épreuves, dont trois inversées** — celle de `pg_dump`, celle d'`EnableRetryOnFailure` et celle de Knip vert sans exceptions ; les trois sont annotées comme telles dans le code, sans quoi quelqu'un les « corrigera ».

**Trois épreuves de la spec d'architecture entrent enfin en service** : « une donnée de santé dans un journal » (T9) et « un utilisateur A lit une ligne de B » (T7) — le lot 1 les avait inscrites au lot 4 et au lot 5 faute de journal et de base. « Un nom franchit la frontière du modèle » reste au lot 8 : aucun adaptateur LLM n'existe.

**Ce que ce plan ne garde pas, et le dit.** La conformité du Markdown du dossier (question 8) · l'état de `log_statement` sur l'instance managée (fait d'instance) · la mesure du plafond de 100 ms sur une base managée réelle · le comportement du mécanisme sous un pooler PgBouncer réel · tout ce qui touche à OVHcloud, dont aucun compte n'est ouvert.

**Trois points à vérifier à l'exécution, signalés plutôt que découverts.**

1. **Le seuil de couverture ne doit pas s'étendre à `Palier.Database.Tests`.** `back/coverage.runsettings` filtre `[Palier.Domain]*` et le seuil de 100 % vit dans `Palier.Domain.Tests.csproj`. Vérifier après la tâche 5 que le nouveau projet n'y est pas entré par inadvertance — un seuil global pousse à tester ce qui est facile.
2. **`Testcontainers.PostgreSql` sous Windows sans WSL** est précisément ce qui est bloqué. Après la tâche 0, si Testcontainers ne trouve pas le démon, vérifier `DOCKER_HOST` avant de modifier le code : le symptôme ressemble à un défaut de configuration du test.
3. **La question de D20 se pose à chaque outil nouveau** — `docker compose`, `dotnet ef`, `pg_dump`. _Un fichier ignoré reste-t-il ignoré quand on le nomme explicitement en argument ?_ La réponse a été « oui » quatre fois sur quatre au lot 1 ; **elle se mesure ici, elle ne se suppose pas.**
