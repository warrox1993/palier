import { useTranslation } from 'react-i18next'
import '../lib/i18n'
import { appliquerJetons } from '../ui/jetons'

// Le nom de la marque n'est pas de la copie traduisible : aucune clé i18next ne
// lui correspondra jamais. Il est donc porté par une constante et non par un
// nœud de texte JSX, que la règle `chaine-en-dur` de `scripts/regles-projet.mjs`
// réserve à la copie destinée à l'utilisateur.
const NOM_APPLICATION = 'palier'

// Les jetons sont posés une fois, au chargement du module, plutôt qu'à chaque
// rendu : les variables CSS vivent sur l'élément racine, hors de React.
appliquerJetons()

export function App() {
  const { t } = useTranslation()

  return (
    <>
      <a href="#contenu">{t('navigation.allerAuContenu')}</a>
      <main id="contenu">{NOM_APPLICATION}</main>
    </>
  )
}
