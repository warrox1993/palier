import { describe, expect, it } from 'vitest'
import { lancerOutil } from './run-outil'

describe('lancerOutil', () => {
  it('retourne le code 0 et la sortie quand la commande réussit', () => {
    const r = lancerOutil(['node', '-e', 'console.log("bonjour")'])
    expect(r.code).toBe(0)
    expect(r.sortie).toContain('bonjour')
  })

  it('retourne un code non nul quand la commande échoue', () => {
    const r = lancerOutil(['node', '-e', 'process.exit(3)'])
    expect(r.code).toBe(3)
  })

  it('lève une erreur si la commande est vide', () => {
    expect(() => lancerOutil([])).toThrow('Commande vide')
  })
})
