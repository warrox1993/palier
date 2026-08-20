import { spawnSync } from 'node:child_process'

export interface ResultatOutil {
  code: number
  sortie: string
}

/** 64 Mio. `gitleaks` sur l'historique, `knip` et `dotnet format` sur la solution
 * dépassent largement le défaut de Node (1 Mio) : au-delà, `spawnSync` tronque la
 * sortie ET rend `status: null`, ce qui transformerait un refus authentique en
 * échec illisible — le motif attendu ayant disparu de la sortie tronquée. */
const TAILLE_MAX_SORTIE = 64 * 1024 * 1024

/** 5 minutes. `spawnSync` bloque le fil de Vitest : son `testTimeout` ne peut pas
 * se déclencher pendant qu'un sous-processus tourne. Sans cette borne, un outil
 * qui pend fait pendre la suite indéfiniment — or `08-workflow.md` § 5 exige
 * « des outils qui ne pendent pas ». */
const DELAI_MAX_MS = 5 * 60 * 1000

/**
 * Échappe un argument pour cmd.exe. Depuis Node 24 (DEP0190), passer un
 * tableau d'arguments avec shell:true ne fait plus quoter les éléments par
 * Node : ils sont concaténés bruts, ce qui casse tout argument contenant un
 * guillemet ou une espace. On échappe donc nous-mêmes avant de fournir la
 * commande comme une chaîne unique.
 *
 * La classe de caractères couvre aussi les métacaractères de cmd.exe. Sans eux,
 * `a^b` arrive à l'outil comme `ab` et `a>b` crée un fichier `b` dans le
 * répertoire courant — les deux en silence, avec un code de sortie 0.
 * Note : `%VAR%` reste développé même entre guillemets ; cmd.exe n'offre aucun
 * moyen de l'empêcher. Ne pas passer de `%` littéral à un outil sous Windows.
 */
function echapperArgumentWindows(arg: string): string {
  if (arg === '') return '""'
  if (!/[\s"^&|<>()%!]/.test(arg)) return arg
  return `"${arg.replace(/"/g, '\\"')}"`
}

/**
 * Lance un outil en sous-processus et retourne son code de sortie et sa sortie
 * complète. Utilisé par les épreuves du harnais pour vérifier qu'un garde-fou
 * refuse effectivement une violation.
 *
 * **Lève** si le sous-processus n'a pas pu s'exécuter jusqu'au bout — binaire
 * introuvable, délai dépassé, sortie tronquée. Sans cela, « l'outil n'a jamais
 * tourné » deviendrait « l'outil a refusé », et une épreuve passerait au vert
 * en ne contrôlant rien. C'est le mode de défaillance le plus coûteux de cette
 * suite : il est donc converti en échec bruyant, jamais en succès silencieux.
 */
export function lancerOutil(
  commande: string[],
  options: { cwd?: string; delaiMs?: number } = {},
): ResultatOutil {
  const [binaire, ...args] = commande
  if (!binaire) throw new Error('Commande vide')

  // Surchargeable pour que l'épreuve du délai puisse franchir la branche sans
  // attendre cinq minutes. Une branche jamais franchie est une branche qui ment.
  const delaiMs = options.delaiMs ?? DELAI_MAX_MS

  // shell:true reste nécessaire sur Windows pour résoudre les outils
  // installés par npm (.cmd) ; voir echapperArgumentWindows pour la raison
  // du passage en chaîne unique plutôt qu'en tableau d'arguments.
  const surWindows = process.platform === 'win32'
  const binaireSpawn = surWindows
    ? [binaire, ...args.map(echapperArgumentWindows)].join(' ')
    : binaire
  const argsSpawn = surWindows ? [] : args

  const r = spawnSync(binaireSpawn, argsSpawn, {
    cwd: options.cwd ?? process.cwd(),
    encoding: 'utf8',
    shell: surWindows,
    maxBuffer: TAILLE_MAX_SORTIE,
    timeout: delaiMs,
  })

  const sortie = `${r.stdout ?? ''}${r.stderr ?? ''}`

  if (r.error) {
    throw new Error(
      `L'outil « ${commande.join(' ')} » n'a pas pu s'exécuter : ${r.error.message}. ` +
        `Ce n'est PAS un refus de l'outil — l'épreuve ne prouve rien tant que ceci n'est pas réglé.` +
        (sortie ? `\nSortie partielle :\n${sortie}` : ''),
    )
  }

  if (r.signal) {
    throw new Error(
      `L'outil « ${commande.join(' ')} » a été interrompu par le signal ${r.signal} ` +
        `(délai maximal : ${delaiMs / 1000} s). Ce n'est PAS un refus de l'outil.` +
        (sortie ? `\nSortie partielle :\n${sortie}` : ''),
    )
  }

  return { code: r.status ?? -1, sortie }
}
