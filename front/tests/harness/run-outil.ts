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
