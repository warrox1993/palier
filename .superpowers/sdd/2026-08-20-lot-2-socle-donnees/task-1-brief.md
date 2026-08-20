# Brief — Tâche 1

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
