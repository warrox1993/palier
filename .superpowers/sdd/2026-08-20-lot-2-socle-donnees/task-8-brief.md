# Brief — Tâche 8

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
