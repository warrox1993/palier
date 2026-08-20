// Violation délibérée : un module pur qui importe React.
import { useState } from 'react'

export function agregeQuelqueChose(): number {
  const [valeur] = useState(0)
  return valeur
}
