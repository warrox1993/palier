// @vitest-environment node
import { readFileSync, existsSync } from 'node:fs'
import { describe, expect, it } from 'vitest'

const CSPROJ = 'back/Palier.Domain/Palier.Domain.csproj'

describe('garde-fou : pureté du domaine', () => {
  it('le projet Domain existe', () => {
    expect(existsSync(CSPROJ), `Cible manquante : ${CSPROJ}`).toBe(true)
  })

  it('Palier.Domain ne référence aucun projet', () => {
    const x = readFileSync(CSPROJ, 'utf8')
    expect(x, 'Domain doit rester sans référence de projet').not.toMatch(/<ProjectReference/)
  })

  it("Palier.Domain ne référence aucun paquet d'accès aux données, réseau ou UI", () => {
    const x = readFileSync(CSPROJ, 'utf8')
    for (const interdit of [
      'EntityFrameworkCore',
      'Npgsql',
      'Microsoft.AspNetCore',
      'System.Net.Http',
      'Dapper',
    ]) {
      expect(x, `Domain ne doit pas référencer ${interdit}`).not.toContain(interdit)
    }
  })
})
