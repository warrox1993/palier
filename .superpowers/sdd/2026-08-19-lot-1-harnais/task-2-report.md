# Rapport — Tâche 2 : Vitest et l'utilitaire de franchissement

Statut : **DONE_WITH_CONCERNS** (implémentation complète et vérifiée, mais deux déviations
par rapport au texte verbatim du brief, documentées ci-dessous, et une ambiguïté non résolue
unilatéralement).

---

## 1. Ce que j'ai implémenté

- **`vitest.config.ts`** — copié verbatim depuis le brief (Étape 2). jsdom, globals, includes
  `src/**/*.test.{ts,tsx}` et `tests/harness/**/*.test.ts`, exclusion de
  `tests/harness/accessibilite.test.ts`, timeout 60 s, seuils de couverture 100 % sur
  `src/core/**`.
- **`tests/harness/run-outil.ts`** — la fonction `lancerOutil(commande, options)`, signature
  strictement identique à celle exigée par le brief et par mon consigne (« sa signature ne
  devra plus bouger »). **Le corps diffère du bloc verbatim du brief** — voir § 3.
- **`tests/harness/run-outil.test.ts`** — copié verbatim depuis le brief (Étape 4), les 3
  `it()` sont mot pour mot ceux fournis. Je n'y ai rien changé.
- **`package.json`** — ajout des scripts `test`, `test:harness`, `test:watch` tels que donnés
  par le brief, sans toucher à `dev`, `build`, `preview`, `typecheck`.
- **`tsconfig.json`** — un ajout hors périmètre déclaré du brief, nécessaire et documenté en
  § 4.
- npm install (Étape 1) : `vitest`, `@vitest/coverage-v8`, `jsdom`, `@testing-library/react`,
  `@testing-library/jest-dom`, sans version figée — le lockfile les verrouille.

**Non fait délibérément** : `src/core/exemple.test.ts`, listé dans la ligne « Fichiers : Créer »
du brief mais absent des 7 étapes concrètes. Voir § 5 — je n'ai pas tranché seul, je le
signale.

---

## 2. Preuve TDD — rouge avant implémentation, puis vert

Ordre respecté à la lettre : le test de l'Étape 4 a été écrit et lancé **avant** que
`tests/harness/run-outil.ts` (Étape 3) n'existe.

### ROUGE

Avant d'écrire le test, confirmation que `run-outil.ts` n'existait pas :
```
$ test -f tests/harness/run-outil.ts && echo "EXISTE" || echo "absent, comme attendu"
absent, comme attendu
```

Commande lancée :
```
npx vitest run tests/harness/run-outil.test.ts
```

Sortie obtenue (extrait) :
```
 ❯ tests/harness/run-outil.test.ts (0 test)

⎯⎯⎯⎯⎯⎯ Failed Suites 1 ⎯⎯⎯⎯⎯⎯⎯

 FAIL  tests/harness/run-outil.test.ts [ tests/harness/run-outil.test.ts ]
Error: Failed to resolve import "./run-outil" from "tests/harness/run-outil.test.ts". Does the file exist?
...
 Test Files  1 failed (1)
      Tests  no tests
```

Échec attendu : `run-outil.ts` n'existe pas encore, Vite ne peut pas résoudre l'import. Exit
code 1. C'est exactement l'échec prévu par l'Étape 5 du brief.

### VERT (première tentative — a révélé un vrai bug, voir § 3)

Après avoir écrit l'implémentation verbatim du brief, le même lancement a produit un échec
**réel** (pas l'échec attendu) :
```
FAIL  tests/harness/run-outil.test.ts > lancerOutil > retourne le code 0 et la sortie quand la commande réussit
AssertionError: expected 1 to be +0
  Tests  1 failed | 2 passed (3)
```
J'ai traité ceci comme un vrai bug (skill `systematic-debugging`, détails en § 3), corrigé
l'implémentation, puis relancé.

### VERT (après correction)

Commande :
```
npx vitest run tests/harness/run-outil.test.ts
```
Sortie :
```
 Test Files  1 passed (1)
      Tests  3 passed (3)
   Duration  4.28s
```
Relancé 3 fois de suite pour écarter un flake lié aux sous-processus (durées 4.27s / 4.28s /
4.33s) — stable à chaque fois, 3/3 tests passent, aucune sortie parasite.

---

## 3. Déviation n° 1 — `run-outil.ts` corrigé, pas recopié verbatim

**Le code de l'Étape 3, recopié tel quel, échoue réellement sur cet environnement** (Windows
11, Node 24.16.0 — l'environnement mandaté par le brief pour ce lot). Ce n'est pas une
simulation d'échec : `lancerOutil(['node', '-e', 'console.log("bonjour")'])` retournait
`code: 1` au lieu de `0`.

**Root cause (skill `systematic-debugging`, phases 1 à 4) :**

1. Reproduction isolée hors Vitest, capture de stderr brut :
   ```
   stderr: "[eval]:1\r\nconsole.log(bonjour)\r\n            ^\r\n\r\nReferenceError: bonjour is not defined..."
   ```
   Le processus enfant recevait `console.log(bonjour)` — **les guillemets avaient disparu**.
2. Un `DeprecationWarning [DEP0190]` accompagnait l'appel : *"Passing args to a child process
   with shell option true can lead to security vulnerabilities, as the arguments are not
   escaped, only concatenated."* Sur Node 24 (confirmé `tsc`/`node --version` → v24.16.0),
   avec `shell: true` (déclenché sur win32 par le code du brief) et des arguments passés en
   tableau, Node ne fait plus lui-même l'échappement pour `cmd.exe` — il concatène brut.
   `cmd.exe` a donc dépouillé les guillemets de `"bonjour"` avant de les transmettre à `node`.
3. Hypothèse testée isolément (script jetable, hors dépôt) : échapper chaque argument pour
   `cmd.exe` (encadrer de guillemets, échapper les `"` internes) et fournir la commande comme
   **une seule chaîne** plutôt qu'un tableau. Résultat : `status: 0`, `stdout: "bonjour\n"`.
   Testé aussi avec un argument à espace simple, un flag nu (`node --version`), et un argument
   combinant espace + guillemets internes (`un "chemin" bizarre`) — les 3 cas restituent
   exactement l'argument d'origine côté enfant.
4. Bénéfice additionnel constaté : passer la commande en chaîne unique (au lieu d'un tableau
   non vide) **supprime aussi le `DeprecationWarning`**, qui ne se déclenche que pour un
   tableau d'arguments non vide combiné à `shell:true`. Vérifié : `npm run test` et
   `npm run test:harness` ne produisent plus aucun avertissement.

**Ce que j'ai changé** dans `tests/harness/run-outil.ts` par rapport au bloc du brief :
ajout d'une fonction privée `echapperArgumentWindows`, et sur Windows uniquement, assemblage
de `binaire + args échappés` en une seule chaîne passée à `spawnSync` (au lieu du tableau brut).
Sur les autres plateformes (`shell` reste `false`), le comportement est strictement identique
au code du brief — binaire et arguments passés tels quels, sans transformation.

**Ce qui n'a pas changé** : la signature publique `lancerOutil(commande: string[], options?: {
cwd?: string }): { code: number; sortie: string }`, le comportement observable pour l'appelant,
le fichier de test (non modifié, verbatim).

**Pourquoi je n'ai pas simplement rapporté ceci comme un blocage** : réversible (fonction
courte, isolée, facile à revenir au code du brief), détectable (le test exact du brief
prouve la correction, aucune triche sur le test), et laisser cette fonction cassée aurait
compromis les 8 tâches suivantes qui en dépendent — sur cette même machine Windows.

Contenu final de `tests/harness/run-outil.ts` :
```typescript
import { spawnSync } from 'node:child_process'

export interface ResultatOutil {
  code: number
  sortie: string
}

/**
 * Échappe un argument pour cmd.exe. Depuis Node 24 (DEP0190), passer un
 * tableau d'arguments avec shell:true ne fait plus quoter les éléments par
 * Node : ils sont concaténés bruts, ce qui casse tout argument contenant un
 * guillemet ou une espace. On échappe donc nous-mêmes avant de fournir la
 * commande comme une chaîne unique.
 */
function echapperArgumentWindows(arg: string): string {
  if (arg === '') return '""'
  if (!/[\s"]/.test(arg)) return arg
  return `"${arg.replace(/"/g, '\\"')}"`
}

/**
 * Lance un outil en sous-processus et retourne son code de sortie et sa sortie
 * complète. Utilisé par les épreuves du harnais pour vérifier qu'un garde-fou
 * refuse effectivement une violation.
 */
export function lancerOutil(commande: string[], options: { cwd?: string } = {}): ResultatOutil {
  const [binaire, ...args] = commande
  if (!binaire) throw new Error('Commande vide')

  // shell:true reste nécessaire sur Windows pour résoudre les outils
  // installés par npm (.cmd) ; voir echapperArgumentWindows pour la raison
  // du passage en chaîne unique plutôt qu'en tableau d'arguments.
  const surWindows = process.platform === 'win32'
  const binaireSpawn = surWindows ? [binaire, ...args.map(echapperArgumentWindows)].join(' ') : binaire
  const argsSpawn = surWindows ? [] : args

  const r = spawnSync(binaireSpawn, argsSpawn, {
    cwd: options.cwd ?? process.cwd(),
    encoding: 'utf8',
    shell: surWindows,
  })

  return {
    code: r.status ?? -1,
    sortie: `${r.stdout ?? ''}${r.stderr ?? ''}`,
  }
}
```

---

## 4. Déviation n° 2 — `tsconfig.json` modifié, hors périmètre déclaré du brief

Le brief liste `package.json` comme seul fichier « Modifier » pour cette tâche. Après avoir
écrit `run-outil.ts`, `npm run typecheck` échouait :
```
tests/harness/run-outil.ts(1,27): error TS2591: Cannot find name 'node:child_process'. Do you
need to install type definitions for node? Try `npm i --save-dev @types/node` and then add
'node' to the types field in your tsconfig.
tests/harness/run-outil.ts(33,22): error TS2591: Cannot find name 'process'. ...
tests/harness/run-outil.ts(38,25): error TS2591: Cannot find name 'process'. ...
```
`@types/node` est pourtant installé (`24.13.3`). Root cause vérifiée par test isolé (tsconfig
dupliqué, `types: ["node"]` ajouté, testé seul) : `tsconfig.json` racine n'a jamais eu de champ
`types` explicite, et sans lui TypeScript 7.0.2 (version installée) ne charge pas les types
ambient de `@types/node` pour cette configuration. `tsconfig.node.json` avait déjà
`"types": ["node"]` explicite (posé par la tâche 1 pour `vite.config.ts`) — le même trou
existait donc déjà côté `tsconfig.json` racine, simplement jamais détecté puisqu'aucun fichier
sous `src/` ou `tests/` n'utilisait encore d'API Node avant `run-outil.ts`.

**Correctif appliqué** : ajout de `"types": ["node"]` dans `compilerOptions` de
`tsconfig.json`, à la ligne juste après `noEmit`, aligné sur le pattern déjà en place dans
`tsconfig.node.json`. Vérifié que cela ne casse rien côté React : `npm run typecheck` (les
deux tsconfig) et `npm run build` sont verts après le changement, y compris `src/app/App.tsx`
et `src/main.tsx`.

**Pourquoi je l'ai fait plutôt que de bloquer** : mon instruction explicite était « vérifier
que `npm run typecheck` reste vert » — un typecheck rouge à la fin de cette tâche aurait
laissé le dépôt dans un état cassé pour la suite du lot. Changement d'une ligne, réversible,
vérifié.

---

## 5. Ambiguïté non tranchée seul — `src/core/exemple.test.ts`

La ligne « Fichiers : Créer » du brief liste `src/core/exemple.test.ts`, mais **aucune des 7
étapes numérotées ne le mentionne**, et aucun contenu verbatim n'est donné pour lui — à
la différence de chaque autre fichier de cette tâche, dont le contenu exact est fourni en
bloc de code. De plus, l'Étape 6 dit explicitement : « Attendu : 3 tests passent » — un
compte qui correspond exactement aux 3 `it()` de `run-outil.test.ts` et à rien d'autre.

Si j'avais créé ce fichier avec un contenu inventé, soit il contient au moins un test et le
total dépasse 3 (contredit l'Étape 6 verbatim), soit il est vide et Vitest le fait échouer
(« No test suite found in file » — un fichier correspondant à un pattern `include` sans aucun
test est traité comme un échec par Vitest, pas comme un no-op).

**Décision prise** : ne pas créer ce fichier, suivre les 7 étapes numérotées comme la
spécification concrète et faisant autorité, en traitant la ligne « Fichiers » comme
probablement obsolète. Je ne l'ai pas tranché en silence — c'est documenté ici et repris dans
ma réponse courte. Si c'est une erreur de ma part, le fichier est trivial à ajouter a
posteriori.

---

## 6. Fichiers créés / modifiés

| Fichier | Statut | Notes |
|---|---|---|
| `vitest.config.ts` | Créé | Verbatim brief |
| `tests/harness/run-outil.ts` | Créé | Corrigé par rapport au verbatim — § 3 |
| `tests/harness/run-outil.test.ts` | Créé | Verbatim brief, non modifié |
| `package.json` | Modifié | + `test`, `test:harness`, `test:watch` ; `dev/build/preview/typecheck` intacts ; devDependencies mises à jour par `npm install` |
| `package-lock.json` | Modifié | Généré par `npm install` |
| `tsconfig.json` | Modifié | + `"types": ["node"]` — hors périmètre déclaré, § 4 |
| `src/core/exemple.test.ts` | **Non créé** | § 5 |

Commit : `fe33880` — `installe Vitest et l'utilitaire de franchissement`, sur
`feat/lot-1-harnais`, local uniquement (rien poussé). `git add -A` tel que prescrit par le
brief — sûr à ce stade car un commit externe (`f505cbd ajoute vite-env.d.ts et .gitattributes
(relecture externe)`), apparu pendant mon travail, avait déjà absorbé les deux seuls autres
fichiers non suivis du dépôt ; `git status` juste avant mon commit ne contenait plus que mes
propres fichiers.

---

## 7. Vérifications finales (fraîches, ce tour)

```
npm run typecheck   → exit 0
npm run test        → 3 tests passent, exit 0
npm run test:harness→ 3 tests passent, exit 0
npm run build       → exit 0, dist/ généré
git status --short  → vide (working tree propre)
```

---

## 8. Constats de relecture

- **Complétude** — les 7 étapes numérotées sont faites. `src/core/exemple.test.ts` (listé
  seulement dans le résumé « Fichiers ») ne l'est pas — § 5.
- **Discipline (YAGNI)** — je n'ai rien ajouté au-delà de ce qui corrige un défaut constaté
  (pas de nouvelle dépendance, pas de fonctionnalité anticipée pour les tâches 3-11, pas de
  test supplémentaire committé). Le helper `echapperArgumentWindows` est la seule addition
  substantielle, et elle est défensive, pas décorative.
- **Tests** — les 3 `it()` exercent un comportement réel : vrais sous-processus Node, vrai
  code de sortie, vraie erreur sur commande vide. Aucun mock, aucun simulacre.
- **Sortie propre** — vérifié explicitement : `npm run test` et `npm run test:harness` ne
  produisent aucun avertissement (le `DeprecationWarning` DEP0190 initialement observé a
  disparu avec la correction du § 3).

---

## 9. Skills Superpowers invoqués

- **`systematic-debugging`** — invoqué face à l'échec réel (non attendu) de
  `run-outil.test.ts` après l'implémentation verbatim, et à nouveau face à l'échec de
  `npm run typecheck`. Les 4 phases ont été suivies : reproduction isolée hors Vitest,
  lecture complète de l'erreur et du `DeprecationWarning`, hypothèse unique testée
  minimalement avant modification du fichier réel, vérification après correction.
- **`verification-before-completion`** — invoqué avant la rédaction de ce rapport ; la
  section 7 ci-dessus est la sortie fraîche de ce passage, dans ce même tour.
- **`test-driven-development`** — non invoqué séparément via l'outil Skill : le brief
  prescrivait déjà, étape par étape, la séquence rouge-vert exacte (Étapes 4-5-3-6). Je l'ai
  appliquée directement plutôt que de repasser par le skill générique.
- **`requesting-code-review`** — non invoqué : la consigne de la tâche l'exclut
  explicitement (« la revue est mon travail, elle est déjà prévue »).
- **`brainstorming` / `writing-plans`** — non pertinents : le brief est déjà un plan approuvé,
  rien à concevoir.

---

## 10. Doutes à faire trancher par Jean-Baptiste

1. **`src/core/exemple.test.ts`** (§ 5) — confirmer que ce fichier n'était pas attendu, ou
   préciser son contenu voulu si je me trompe.
2. **Le correctif de `run-outil.ts`** (§ 3) — je m'écarte du bloc de code verbatim du brief. Le
   comportement observable et la signature sont préservés, mais si le lot exige une fidélité
   littérale au code fourni indépendamment de son fonctionnement réel sur Windows, ceci doit
   être rediscuté avant que les tâches 3 à 9 et 11 ne s'appuient dessus.
3. **`tsconfig.json`** (§ 4) — un fichier hors périmètre déclaré de cette tâche a été modifié.
   Changement minimal et vérifié, mais je le signale explicitement puisqu'il n'était pas dans
   la liste « Fichiers » du brief.
4. **Portée de `echapperArgumentWindows`** — ma correction ne gère que les espaces et les
   guillemets doubles (les cas concrets rencontrés et ceux testés manuellement : espace seul,
   flag nu, espace+guillemets combinés). Je n'ai pas géré l'éventail complet des métacaractères
   `cmd.exe` (`&`, `|`, `<`, `>`, `^`, `%`) faute de cas d'usage à tester ici — si une tâche
   future (3-9, 11) passe un argument contenant l'un de ces caractères, il faudra l'étendre.
   Je ne l'ai pas fait par anticipation pour rester au plus près du besoin réel (YAGNI).
5. **Ce qui n'a pas pu être vérifié** : le comportement de `lancerOutil` sur macOS/Linux n'a
   pas été testé (pas d'accès à un tel environnement dans cette session) — le code y est
   inchangé par rapport au verbatim du brief (branche `surWindows` fausse), donc le risque est
   faible, mais je ne l'ai pas exécuté pour le confirmer.
