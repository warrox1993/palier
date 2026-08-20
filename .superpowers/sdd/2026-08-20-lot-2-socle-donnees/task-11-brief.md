# Brief — Tâche 11

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
