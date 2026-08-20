// Violation délibérée : image sans texte alternatif.
// Le tableau `plugins` de `.oxlintrc.json` active `jsx-a11y` et `react` via
// `categories.correctness`. Ces règles ne sont déclarées nulle part dans le
// fichier `rules` : retirer un mot du tableau `plugins` les supprimerait toutes
// en silence. Cette fixture est ce qui rend cette suppression détectable.
export function Vignette({ source }: { source: string }) {
  return <img src={source} />
}
