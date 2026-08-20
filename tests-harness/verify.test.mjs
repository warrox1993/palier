import { existsSync, readFileSync } from 'node:fs'
import { describe, expect, it } from 'vitest'

describe("garde-fou : point d'entrée unique", () => {
  it('le script verify existe', () => {
    expect(existsSync('scripts/verify.mjs'), 'Cible manquante : scripts/verify.mjs').toBe(true)
  })

  it('verify couvre les deux écosystèmes', () => {
    const s = readFileSync('scripts/verify.mjs', 'utf8')
    for (const etape of [
      'front:format',
      'front:lint',
      'front:lint:types',
      'front:typecheck',
      'front:test',
      'front:knip',
      'regles',
      'back:format',
      'back:build',
      'back:test',
      'licences',
      'back:audit',
      'front:build',
    ]) {
      expect(s, `Contrôle manquant dans verify : ${etape}`).toContain(etape)
    }
  })

  it("le hook de pré-envoi n'appelle que verify", () => {
    expect(existsSync('.husky/pre-push'), 'Cible manquante : .husky/pre-push').toBe(true)
    const h = readFileSync('.husky/pre-push', 'utf8')
    expect(h).toContain('npm run verify')
    expect(h, "le hook délègue, il n'énumère pas").not.toMatch(
      /dotnet (build|test)|npm run (lint|typecheck)/,
    )
  })
})
