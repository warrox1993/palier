// Configuration lint-staged. Le format `.mjs` est choisi pour deux raisons :
// il porte ses motifs — un fichier JSON ne peut pas les porter sans risquer le
// ruling P14, où une clé inconnue désarme la règle entière en silence — et il
// permet de FILTRER les fixtures plutôt que de les exclure par un motif nié
// fragile.
//
// CE QUI EST AUTO-CORRIGÉ, ET CE QUI NE L'EST JAMAIS
//
// Seul le MÉCANIQUE se corrige tout seul : formatage, `using` inutiles, ordre
// des imports. Il n'y a qu'une bonne réponse, et aucune information n'est
// perdue.
//
// Le SÉMANTIQUE ne se corrige jamais automatiquement. `knip --fix` supprime les
// exports qu'il juge morts : sur un export écrit ce matin dont le consommateur
// arrive demain, il détruit du travail ET le commit passe au vert. La détection
// est utile, la correction est destructrice. Même chose pour une chaîne en dur,
// qui exige de choisir une clé de traduction.
//
// Ce qui est écarté du formatage, et pourquoi :
//
// 1. Les fixtures du harnais. `front/tests/harness/fixtures/format-casse.ts`
//    est délibérément mal formaté ; le passer à `prettier --write` le
//    réparerait, et l'épreuve qui s'appuie dessus passerait au vert sans plus
//    rien contrôler. C'est le ruling P9 appliqué à l'entrée : l'exclusion
//    appartient à la commande, ici à ce filtre.
//
// 2. Oxlint lancé DEPUIS LA RACINE. Mesuré : sur des fichiers de `front/`, il
//    résout `ignorePatterns` et `overrides` par rapport au répertoire courant —
//    la barrière de pureté de `src/core/**` ne s'appliquerait plus, et personne
//    ne le verrait. La parade n'est pas de renoncer à `--fix`, c'est de lancer
//    Oxlint AVEC LE BON RÉPERTOIRE COURANT : voir `corrigerFront` ci-dessous.
const FIXTURES = /(front\/tests\/harness|back\/tests-harness)\/fixtures\//

const PRETTIER = 'node front/node_modules/prettier/bin/prettier.cjs'

// `npm --prefix front run` et NON `cd front && …` : lint-staged exécute ses
// commandes SANS shell, donc `cd` y est cherché comme un exécutable et la
// commande échoue avec « Le chemin d'accès spécifié est introuvable » — mesuré.
// `npm run` s'exécute depuis le répertoire du `package.json` qu'il lit : c'est
// lui qui donne à Oxlint le bon répertoire courant, et donc la bonne résolution
// de `.oxlintrc.json`, de ses `ignorePatterns` et de ses `overrides`.
const OXLINT_FIX = 'npm --prefix front run lint:fix --'

/** Normalise un chemin Windows pour que les motifs soient comparables. */
const normaliser = (f) => f.replaceAll('\\', '/')

/** @param {string[]} fichiers */
const horsFixtures = (fichiers) => fichiers.filter((f) => !FIXTURES.test(normaliser(f)))

/** @param {string[]} fichiers */
function formater(fichiers) {
  const cibles = horsFixtures(fichiers)
  if (cibles.length === 0) return []
  return [`${PRETTIER} --write ${cibles.map((f) => JSON.stringify(f)).join(' ')}`]
}

/**
 * Oxlint en correction sur les fichiers de `front/`, lancé DEPUIS `front/`.
 *
 * Le `cd front &&` n'est pas cosmétique : c'est lui qui fait résoudre
 * `.oxlintrc.json`, ses `ignorePatterns` et surtout ses `overrides` — dont la
 * barrière de pureté de `src/core/**` — contre le bon répertoire. Sans lui, les
 * règles s'appliquent contre la racine et la barrière disparaît en silence.
 *
 * Les chemins sont donc rendus relatifs à `front/`, puisque c'est de là que la
 * commande s'exécute.
 *
 * `--fix` ne corrige que ce qui a une réponse unique : imports inutiles, ordre,
 * syntaxe équivalente. Il ne touche ni à `no-restricted-imports`, ni à
 * `no-explicit-any`, ni à `no-console` — ceux-là REFUSENT, et c'est voulu.
 *
 * @param {string[]} fichiers
 */
function corrigerFront(fichiers) {
  const cibles = horsFixtures(fichiers)
    .map(normaliser)
    .filter((f) => f.includes('/front/') || f.startsWith('front/'))
    .map((f) => f.replace(/^.*\/front\//, '').replace(/^front\//, ''))
  if (cibles.length === 0) return []
  return [`${OXLINT_FIX} ${cibles.map((f) => JSON.stringify(f)).join(' ')}`]
}

/**
 * `dotnet format` sur les fichiers C# indexés.
 *
 * `whitespace` et `style` seulement — PAS `analyzers`. Les correcteurs
 * d'analyseurs réécrivent du code : ils peuvent changer une signature, extraire
 * une variable, modifier une comparaison. C'est du sémantique, et le sémantique
 * ne s'auto-corrige pas. Le contrôle complet reste dans `verify`, où il REFUSE.
 *
 * `--include` reçoit les chemins : sans lui, `dotnet format` traiterait toute la
 * solution à chaque commit, ce qui coûterait une vingtaine de secondes.
 *
 * @param {string[]} fichiers
 */
function corrigerBackend(fichiers) {
  const cibles = horsFixtures(fichiers)
  if (cibles.length === 0) return []
  const chemins = cibles.map((f) => JSON.stringify(f)).join(' ')
  return [
    `dotnet format back/Palier.sln whitespace --include ${chemins}`,
    `dotnet format back/Palier.sln style --include ${chemins}`,
  ]
}

export default {
  '*.{ts,tsx,mjs,cjs,js,jsx,json,md,css,html,yml,yaml}': formater,
  'front/**/*.{ts,tsx,js,jsx}': corrigerFront,
  '*.cs': corrigerBackend,
}
