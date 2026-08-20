# Brief — Tâche 13

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
