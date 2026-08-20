// Le nom de la marque n'est pas de la copie traduisible : aucune clé i18next ne
// lui correspondra jamais. Il est donc porté par une constante et non par un
// nœud de texte JSX, que la règle `chaine-en-dur` de `scripts/regles-projet.mjs`
// réserve à la copie destinée à l'utilisateur.
const NOM_APPLICATION = 'palier'

export function App() {
  return <main id="contenu">{NOM_APPLICATION}</main>
}
