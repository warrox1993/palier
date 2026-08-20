// Configuration lint-staged. Le format `.mjs` est choisi pour deux raisons :
// il porte ses motifs — un fichier JSON ne peut pas les porter sans risquer le
// ruling P14, où une clé inconnue désarme la règle entière en silence — et il
// permet de FILTRER les fixtures plutôt que de les exclure par un motif nié
// fragile.
//
// Ce qui est écarté, et pourquoi :
//
// 1. Les fixtures du harnais. `front/tests/harness/fixtures/format-casse.ts`
//    est délibérément mal formaté ; le passer à `prettier --write` le
//    réparerait, et l'épreuve qui s'appuie dessus passerait au vert sans plus
//    rien contrôler. C'est le ruling P9 appliqué à l'entrée : l'exclusion
//    appartient à la commande, ici à ce filtre.
//
// 2. Oxlint. Le plan prévoyait `oxlint --fix` ici. Mesuré : lancé depuis la
//    RACINE sur des fichiers de `front/`, Oxlint résout `ignorePatterns` et
//    `overrides` par rapport à la racine — la barrière de pureté de
//    `src/core/**` ne s'appliquerait plus, et personne ne le verrait. Un
//    linter à demi configuré rassure plus qu'il ne protège. Le lint complet
//    reste au pré-envoi, dans `npm run verify`, avec le bon répertoire courant.
const FIXTURES = /(front\/tests\/harness|back\/tests-harness)\/fixtures\//

const PRETTIER = 'node front/node_modules/prettier/bin/prettier.cjs'

/** @param {string[]} fichiers */
function formater(fichiers) {
  const cibles = fichiers.filter((f) => !FIXTURES.test(f.replaceAll('\\', '/')))
  if (cibles.length === 0) return []
  return [`${PRETTIER} --write ${cibles.map((f) => JSON.stringify(f)).join(' ')}`]
}

export default {
  '*.{ts,tsx,mjs,cjs,js,jsx,json,md,css,html,yml,yaml}': formater,
}
