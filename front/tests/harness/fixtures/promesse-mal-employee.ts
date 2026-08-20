// Violations délibérées, toutes invisibles sans information de type. Elles
// couvrent les quatre règles type-aware de `.oxlintrc.json` que le bloc `rules`
// porte seul — celles que `categories.correctness` ne rallume pas.

// `await` sur une valeur qui n'est pas une promesse : await-thenable.
export async function attendreUnNombre(): Promise<number> {
  return await 42
}

// Une promesse en position de condition est toujours vraie :
// no-misused-promises, et strict-boolean-expressions en avertissement.
export function conditionner(promesse: Promise<boolean>): string {
  if (promesse) return 'oui'
  return 'non'
}

// `JSON.parse` rend `any` : l'affecter sans le valider est no-unsafe-assignment.
export function lireLaCharge(brut: string): unknown {
  const charge = JSON.parse(brut)
  return charge
}
