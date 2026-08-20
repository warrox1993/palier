// Violation délibérée : la copie est glissée dans une accolade JSX pour
// ressembler à un appel de traduction. C'est une chaîne littérale, pas une clé.
export function Etiquette() {
  return <span className="etiquette">{'Aucune séance enregistrée'}</span>
}
