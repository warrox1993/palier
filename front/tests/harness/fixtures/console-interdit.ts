// Violation délibérée : appel à la console.
// Ce garde-fou est le seul mécanisme du lot face à « aucune donnée de santé
// dans les logs » (docs/01-conformite.md, docs/08-workflow.md § 9).
export function trace(poids: number): void {
  console.log('poids du jour', poids)
}
