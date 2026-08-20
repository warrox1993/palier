// @vitest-environment node
import { describe, expect, it } from 'vitest'
import { lancerOutil } from '../../front/tests/harness/run-outil.js'

describe('garde-fou : licences des dépendances', () => {
  it('accepte les dépendances actuelles', () => {
    const r = lancerOutil(['node', 'scripts/verifier-licences.mjs'])
    expect(r.code, `Une dépendance actuelle sort de la liste blanche :\n${r.sortie}`).toBe(0)
  })

  it('refuse un paquet sous licence réciproque', () => {
    const r = lancerOutil(['node', 'scripts/verifier-licences.mjs', '--tester', 'MediatR'])
    expect(r.code, `MediatR (RPL-1.5) a été accepté :\n${r.sortie}`).not.toBe(0)
    expect(r.sortie).toMatch(/MediatR/)
  })
})
