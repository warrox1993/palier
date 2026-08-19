// Violation délibérée. Ce fichier est exclu de tsconfig.json et du build.
// Il sert uniquement à prouver que le typecheck refuse un any implicite.
export function additionne(a, b) {
  return a + b
}
