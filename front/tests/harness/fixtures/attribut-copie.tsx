// Violation délibérée : quatre attributs qui portent de la copie destinée à
// l'utilisateur. Aucun n'est un nœud de texte JSX — l'ancienne règle, qui ne
// cherchait que « > texte < », les laissait tous passer.
export function Champ() {
  return (
    <label htmlFor="poids">
      <input
        id="poids"
        type="number"
        placeholder="Poids en kilogrammes"
        aria-label="Poids du jour"
        title="Saisir le poids de la séance"
      />
      <img src="/graphique.png" alt="Graphique de progression" />
    </label>
  )
}
