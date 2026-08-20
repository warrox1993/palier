/**
 * L'accès à `GET /api/v1/sante`. Aucun rendu ici : ce module est pur au sens du
 * projet — il ne connaît ni React ni i18next, et il ne fabrique AUCUN libellé.
 *
 * Il ne porte non plus AUCUN repli, et c'est délibéré. Une valeur par défaut
 * rendue quand la base ne répond pas transformerait l'état d'erreur en état de
 * contenu — l'écran afficherait « zéro référence » là où il faut lire « la base
 * ne répond pas ». C'est la classe de faux vert que ce dépôt traque, transposée
 * à l'écran.
 */

/** Ce que la route rend. Les noms viennent du contrat de l'API, en camelCase. */
export interface EtatDuSocle {
  /** Le dernier `MigrationId` appliqué, lu dans l'historique des migrations. */
  readonly schema: string
  /** Faux quand l'API répond mais que la base ne répond pas — 503. */
  readonly isBaseJoignable: boolean
  /** Le nombre de lignes de `nutrient_refs`. Zéro = référentiel non chargé. */
  readonly referentielNutriments: number
}

/** `VITE_API_URL` existe dans `front/.env.example` ; le repli est le chemin
 * relatif de D16, front et API sur le même domaine. Constante NON exportée :
 * la règle `chaine-en-dur` ne vise que les constantes exportées, et un chemin
 * technique n'a aucune clé de traduction. */
const RACINE_API = import.meta.env.VITE_API_URL ?? '/api'

/** Le chemin versionné dès la première route — `14-contenu.md` § 8. */
const CHEMIN_SANTE = `${RACINE_API}/v1/sante`

/** L'échec est TYPÉ : l'écran distingue « la route a refusé » de « la réponse
 * n'est pas celle attendue », sans jamais afficher le message technique. */
export class SocleInjoignableError extends Error {
  constructor(cause: string) {
    super(cause)
    this.name = 'SocleInjoignableError'
  }
}

/**
 * Lit l'état du socle. Lève {@link SocleInjoignableError} dès que la réponse
 * n'est pas exploitable — code non 200, corps qui n'est pas du JSON, champ
 * manquant. Le serveur de prévisualisation rend `index.html` sur une route
 * inconnue, avec un code 200 : sans le contrôle du type de contenu, l'écran
 * croirait avoir reçu un état.
 */
export async function lireEtatDuSocle(signal?: AbortSignal): Promise<EtatDuSocle> {
  const reponse = await fetch(CHEMIN_SANTE, {
    headers: { Accept: 'application/json' },
    ...(signal === undefined ? {} : { signal }),
  })

  if (!reponse.ok) {
    throw new SocleInjoignableError(`code ${String(reponse.status)}`)
  }

  const type = reponse.headers.get('content-type') ?? ''
  if (!type.includes('application/json')) {
    throw new SocleInjoignableError(`type de contenu ${type || 'absent'}`)
  }

  const brut: unknown = await reponse.json()
  if (!estUnEtat(brut)) {
    throw new SocleInjoignableError('corps inattendu')
  }

  return brut
}

function estUnEtat(valeur: unknown): valeur is EtatDuSocle {
  if (typeof valeur !== 'object' || valeur === null) return false
  const objet = valeur as Record<string, unknown>
  return (
    typeof objet.schema === 'string' &&
    typeof objet.isBaseJoignable === 'boolean' &&
    typeof objet.referentielNutriments === 'number'
  )
}
