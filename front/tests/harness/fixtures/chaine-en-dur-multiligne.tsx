// Violation délibérée : la même chaîne que `chaine-en-dur.tsx`, mais telle que
// Prettier la rend une fois la ligne trop longue (printWidth 100). C'est la
// forme qu'aura tout composant réel — l'ancienne règle, appliquée ligne à ligne,
// ne voyait plus rien ici.
export function Bouton({ onEnvoyer }: { onEnvoyer: () => void }) {
  return (
    <button type="button" className="bouton-principal" onClick={onEnvoyer}>
      Enregistrer la séance
    </button>
  )
}
