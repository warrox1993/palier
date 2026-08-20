// Violations délibérées : la même dépendance applicative que `core-impur.ts`,
// atteinte à toutes les profondeurs et sous toutes les formes.
//
// Les motifs d'origine étaient `../lib/*` et `../ui/*` : un seul cran. Mesuré,
// `import { bouton } from '../../ui/bouton'` depuis `src/core/sous/` sortait en
// code 0. Le lot 2 crée `core/nutrition/` et `core/entrainement/` — la barrière
// de pureté serait tombée dès le premier sous-répertoire, sans un signal.
import { a } from '../ui/bouton'
import { b } from '../../ui/bouton'
import { c } from '../../../ui/bouton'
import { d } from '@/ui/bouton'
import { e } from '@/ui/sous/bouton'
import { f } from '../../lib/format'
import { g } from '../../../features/seance'
import { h } from '../../ui'
import { i } from '../../lib/sous/format'

export const tout = [a, b, c, d, e, f, g, h, i]
