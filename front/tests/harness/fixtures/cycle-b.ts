// Violation délibérée : cycle d'import, moitié B. Voir `cycle-a.ts`.
import { versA } from './cycle-a'

export function versB(): string {
  return versA().slice(0, 1)
}
