import { useCallback, useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { lireEtatDuSocle, type EtatDuSocle } from './etat'
import './etat.css'

/**
 * LE PREMIER ÉCRAN DU PROJET — D41, et c'est ici que D31 est honorée.
 *
 * Quatre états, et chacun porte un `data-etat` : c'est le CONTRAT que
 * `tests/harness/etats-ecran.test.ts` fait respecter à tout écran du dossier
 * `features/`. Un écran dépourvu d'état vide ou d'état d'erreur est refusé, et
 * l'épreuve nomme lequel manque.
 *
 * | État       | Déclencheur                                              |
 * | ---------- | -------------------------------------------------------- |
 * | chargement | l'appel est en cours                                     |
 * | vide       | schéma migré, `nutrient_refs` à zéro ligne               |
 * | erreur     | conteneur arrêté, API injoignable                        |
 * | contenu    | la réponse de `GET /api/v1/sante`                        |
 *
 * L'état d'erreur SE PROVOQUE — `npm run db:down`, recharger — au lieu de se
 * simuler. C'est ce qui distingue cet écran d'un simulacre, et c'est la seule
 * raison pour laquelle celui-ci a été choisi comme premier écran.
 *
 * Il n'affiche AUCUNE donnée de santé et AUCUN compte. Il n'entre pas au
 * périmètre V1 : au lot 4 il passe derrière l'authentification, et au lot 6 la
 * question « le garde-t-on ou le supprime-t-on » est reprise et tranchée.
 */
type Phase =
  | { readonly nom: 'chargement' }
  | { readonly nom: 'erreur' }
  | { readonly nom: 'vide'; readonly etat: EtatDuSocle }
  | { readonly nom: 'contenu'; readonly etat: EtatDuSocle }

export function EcranEtat() {
  const { t } = useTranslation()
  const [phase, setPhase] = useState<Phase>({ nom: 'chargement' })
  const [rang, setRang] = useState(0)

  // Le retour au chargement est déclenché par l'ÉVÉNEMENT, pas par l'effet. Un
  // `setState` synchrone dans un effet relance un rendu pour rien, et Oxlint le
  // refuse (`react/set-state-in-effect`) — à raison : l'état initial est déjà
  // « chargement », l'effet n'a donc à le poser que lorsqu'on le rejoue.
  const relancer = useCallback(() => {
    setPhase({ nom: 'chargement' })
    setRang((precedent) => precedent + 1)
  }, [])

  useEffect(() => {
    const abandon = new AbortController()

    lireEtatDuSocle(abandon.signal)
      .then((etat) => {
        if (abandon.signal.aborted) return
        // Le référentiel vide n'est PAS une erreur : le schéma est migré, les
        // valeurs EFSA ne sont pas encore chargées. Les confondre ferait crier
        // l'écran sur un état parfaitement normal du lot 2.
        setPhase(
          etat.referentielNutriments === 0 ? { nom: 'vide', etat } : { nom: 'contenu', etat },
        )
      })
      .catch(() => {
        // Le motif technique n'est NI affiché NI journalisé : `01-conformite.md`
        // § 4, et un message d'erreur d'API n'a aucune clé de traduction.
        if (!abandon.signal.aborted) setPhase({ nom: 'erreur' })
      })

    return () => {
      abandon.abort()
    }
  }, [rang])

  return (
    <section className="etat" aria-labelledby="etat-titre">
      <h1 className="etat__titre" id="etat-titre">
        {t('etat.titre')}
      </h1>

      {/* `<output>` et non `<div role="status">` : le rôle est implicite, et
          Oxlint refuse la seconde forme (`jsx-a11y/prefer-tag-over-role`). Son
          contenu se limite à du texte — `<output>` n'accepte pas de paragraphe. */}
      {phase.nom === 'chargement' && (
        <output className="etat__bloc" data-etat="chargement">
          {t('commun.chargement')}
        </output>
      )}

      {phase.nom === 'erreur' && (
        <div className="etat__bloc" data-etat="erreur" role="alert">
          <p>{t('etat.erreur.titre')}</p>
          <p className="etat__aide">{t('etat.erreur.aide')}</p>
        </div>
      )}

      {phase.nom === 'vide' && (
        <div className="etat__bloc" data-etat="vide">
          <p>{t('etat.vide.titre')}</p>
          <p className="etat__aide">{t('etat.vide.aide')}</p>
          <dl className="etat__releve">
            <Releve libelle={t('etat.schema')} valeur={phase.etat.schema} />
          </dl>
        </div>
      )}

      {phase.nom === 'contenu' && (
        <div className="etat__bloc" data-etat="contenu">
          <dl className="etat__releve">
            <Releve libelle={t('etat.schema')} valeur={phase.etat.schema} />
            <Releve
              libelle={t('etat.referentiel')}
              valeur={phase.etat.referentielNutriments.toString()}
            />
          </dl>
        </div>
      )}

      <button className="etat__action" onClick={relancer} type="button">
        {t('etat.actualiser')}
      </button>
    </section>
  )
}

/** Une ligne de relevé. Extraite pour que le couple libellé/valeur ne soit
 * écrit qu'une fois : deux copies divergent, et la divergence se ferme par une
 * lecture. */
function Releve({ libelle, valeur }: { readonly libelle: string; readonly valeur: string }) {
  return (
    <>
      <dt className="etat__libelle">{libelle}</dt>
      <dd className="etat__valeur">{valeur}</dd>
    </>
  )
}
