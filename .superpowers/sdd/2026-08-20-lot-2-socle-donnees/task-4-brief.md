# Brief — Tâche 4

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
