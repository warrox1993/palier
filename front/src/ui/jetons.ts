/**
 * Les jetons de design, source unique - `docs/02-design.md` section 4 et D8.
 *
 * Ce fichier est la CIBLE de la regle `couleur-hors-jetons` de
 * `scripts/regles-projet.mjs`. Jusqu'au 20/08/2026, cette regle portait une
 * exclusion `/src[\/]ui[\/]jetons\./` qui pointait un fichier INEXISTANT : elle
 * s'appliquait donc partout et n'attrapait rien, faute de code a lire. Elle a
 * enfin quelque chose a proteger.
 *
 * Pas de Tailwind (D42) : `couleur-hors-jetons` cherche un motif `#RRGGBB`, et
 * des classes utilitaires n'en portent aucun - la regle deviendrait aveugle
 * sans rien signaler. Les valeurs vivent ici, et se propagent aux feuilles de
 * style par les variables CSS que `variablesCss()` produit.
 *
 * `tests/harness/jetons.test.ts` compare chacune de ces valeurs au document qui
 * fait autorite : une valeur recopiee a la main dans deux fichiers derive, et
 * la derive se ferme par une lecture plutot que par une discipline.
 */

export const couleurs = {
  'surface-0': '#14181D',
  'surface-1': '#1D232A',
  'surface-2': '#262E37',
  line: '#333D48',
  ink: '#E9E7E2',
  'ink-muted': '#8A96A3',
  'signal-under': '#3D82C4',
  'signal-ok': '#4FA37A',
  'signal-over': '#F2C230',
  'signal-alert': '#D64541',
} as const

/** Echelle typographique, ratio 1,25. En pixels. */
export const tailles = [11, 13, 15, 19, 24, 30, 38] as const

/** Base d'espacement, en pixels. Tout ecart vertical ou horizontal en est un
 * multiple. */
export const espacement = 4

/** 2 px partout - un produit d'instrumentation, pas une application grand
 * public arrondie. */
export const rayon = 2

/** Zone tactile minimale, et l'ecart vertical minimal entre deux cibles. */
export const tactile = { minimum: 48, ecart: 8 } as const

/** Trois durees, en millisecondes : retour au doigt, validation, repere de la
 * regle - D8. */
export const durees = { retour: 100, validation: 150, repere: 400 } as const

/** Une seule courbe, en sortie douce - D8. */
export const courbe = 'cubic-bezier(0.25, 0.46, 0.45, 0.94)'

export const jetons = {
  couleurs,
  tailles,
  espacement,
  rayon,
  tactile,
  durees,
  courbe,
} as const

/**
 * Les jetons sous forme de paires `--nom: valeur`, dans l'ordre ou ils sont
 * declares. Les feuilles de style consomment ces variables et jamais les
 * valeurs : c'est ce qui garde `couleur-hors-jetons` capable de voir une
 * couleur ecrite ailleurs.
 */
export function variablesCss(): Record<string, string> {
  const sortie: Record<string, string> = {}
  for (const [nom, valeur] of Object.entries(couleurs)) sortie[`--couleur-${nom}`] = valeur
  tailles.forEach((taille, rang) => {
    sortie[`--taille-${rang}`] = `${taille}px`
  })
  sortie['--espacement'] = `${espacement}px`
  sortie['--rayon'] = `${rayon}px`
  sortie['--tactile-minimum'] = `${tactile.minimum}px`
  sortie['--tactile-ecart'] = `${tactile.ecart}px`
  for (const [nom, valeur] of Object.entries(durees)) sortie[`--duree-${nom}`] = `${valeur}ms`
  sortie['--courbe'] = courbe
  return sortie
}

/**
 * Pose les variables sur l'element racine. Appelee une fois au demarrage.
 * Ecrire les jetons ici plutot que dans une feuille de style statique evite la
 * seconde declaration : les valeurs n'existent qu'a un endroit, et une
 * divergence est impossible plutot qu'interdite.
 */
export function appliquerJetons(racine: HTMLElement = document.documentElement): void {
  for (const [nom, valeur] of Object.entries(variablesCss())) racine.style.setProperty(nom, valeur)
}
