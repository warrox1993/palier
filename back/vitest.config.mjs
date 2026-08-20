// Configuration Vitest des épreuves du harnais backend.
//
// La racine est celle du dépôt, et non `back/` : toutes les épreuves adressent
// leurs cibles depuis la racine (`back/Palier.sln`, `scripts/verifier-licences.mjs`),
// exactement comme les commandes que `verify`, les hooks et la CI lancent.
//
// L'objet est exporté brut, sans `defineConfig` : `vitest` n'est installé que
// dans `front/node_modules`, hors de la chaîne de résolution d'un fichier de
// `back/`. Un import de `vitest/config` ici échouerait.
//
// L'environnement est `node` : ces épreuves lancent des outils en
// sous-processus et ne touchent jamais au DOM. Le délai est porté à cinq
// minutes parce qu'une restauration NuGet à froid dépasse largement le défaut.
import { fileURLToPath, URL } from 'node:url'

const racine = fileURLToPath(new URL('..', import.meta.url))

// `import { describe } from 'vitest'` depuis `back/tests-harness/` se résout,
// vérifié empiriquement, bien qu'aucun `node_modules` ne remonte de `back/` :
// Vitest fournit lui-même la résolution de son propre paquet. Si un jour elle
// cesse, l'échec est bruyant — une épreuve introuvable, jamais une épreuve
// verte qui ne contrôle rien.
export default {
  root: racine,
  test: {
    environment: 'node',
    globals: false,
    include: ['back/tests-harness/**/*.test.mjs'],
    exclude: ['**/node_modules/**'],
    testTimeout: 300_000,
    hookTimeout: 300_000,
  },
}
