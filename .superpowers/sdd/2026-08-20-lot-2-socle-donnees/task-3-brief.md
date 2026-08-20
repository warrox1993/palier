# Brief — Tâche 3

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
