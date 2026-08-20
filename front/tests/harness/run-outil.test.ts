// @vitest-environment node
import { describe, expect, it } from 'vitest'
import { existsSync, rmSync } from 'node:fs'
import { join } from 'node:path'
import { tmpdir } from 'node:os'
import { mkdtempSync } from 'node:fs'
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

// Les trois modes de défaillance qui transformeraient « l'outil n'a jamais
// tourné » en « l'outil a refusé ». Chacun a été observé sur une sonde avant
// d'être corrigé ; chacun reste ici pour détecter une régression.
describe('lancerOutil — les défaillances ne se déguisent pas en refus', () => {
  it('rend la sortie entière au-delà du défaut de 1 Mio de Node', () => {
    // 2 Mio puis sortie en 1 : c'est un refus authentique d'un outil bavard.
    // Avec le maxBuffer par défaut, spawnSync rendrait status:null et une
    // sortie tronquée — le motif attendu disparaîtrait, et l'assertion sur le
    // motif rendrait un rouge illisible sur une violation réelle.
    const r = lancerOutil([
      'node',
      '-e',
      'process.stdout.write("x".repeat(2*1024*1024)); process.stdout.write("MOTIF_FINAL"); process.exit(1)',
    ])
    expect(r.code).toBe(1)
    expect(r.sortie.length).toBeGreaterThan(2 * 1024 * 1024)
    expect(r.sortie).toContain('MOTIF_FINAL')
  })

  it('lève quand le délai est dépassé, au lieu de rendre un code non nul', () => {
    // spawnSync bloque le fil : le testTimeout de Vitest ne peut pas se
    // déclencher pendant qu'un sous-processus tourne. Sans cette borne, un
    // outil qui pend fait pendre la suite entière.
    expect(() =>
      lancerOutil(['node', '-e', 'setTimeout(() => {}, 30000)'], { delaiMs: 300 }),
    ).toThrow(/PAS un refus/)
  })

  it('transmet un argument contenant un métacaractère de cmd.exe sans l’altérer', () => {
    // Sous Windows, `a^b` non échappé arrive à l'outil comme `ab` : une ancre
    // de motif serait silencieusement avalée, et l'outil chercherait autre
    // chose que ce que l'épreuve croit lui demander.
    const r = lancerOutil(['node', '-e', 'process.stdout.write(process.argv[1])', 'a^b'])
    expect(r.code).toBe(0)
    expect(r.sortie).toBe('a^b')
  })

  it('n’exécute pas une redirection cachée dans un argument', () => {
    // `a>b` non échappé fait écrire cmd.exe dans un fichier `b` du répertoire
    // courant : un effet de bord destructeur, en silence, avec un code 0.
    const bac = mkdtempSync(join(tmpdir(), 'palier-epreuve-'))
    try {
      const r = lancerOutil(['node', '-e', 'process.stdout.write(process.argv[1])', 'a>b'], {
        cwd: bac,
      })
      expect(r.sortie).toBe('a>b')
      expect(existsSync(join(bac, 'b')), 'une redirection a été exécutée').toBe(false)
    } finally {
      rmSync(bac, { recursive: true, force: true })
    }
  })
})
