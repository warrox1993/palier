// Violation délibérée : cycle d'import, moitié A.
// `import/no-cycle` est la seule des règles syntaxiques à protéger
// l'architecture : sans elle, la frontière front / domaine se contourne.
import { versB } from './cycle-b'

export function versA(): string {
  return `A${versB()}`
}
