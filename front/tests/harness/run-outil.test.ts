// @vitest-environment node
import { describe, expect, it } from 'vitest'
import { existsSync, rmSync } from 'node:fs'
import { join } from 'node:path'
import { tmpdir } from 'node:os'
import { mkdtempSync } from 'node:fs'
import { echapperArgumentWindows, lancerOutil } from './run-outil'

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
    // `process.exitCode = 1` et NON `process.exit(1)`. Sur un tuyau, `exit()`
    // termine le processus sans attendre que stdout soit vidé : la CI Linux a
    // rendu 146 176 octets sur les 2 Mio écrits, et l'épreuve a échoué en
    // accusant `maxBuffer` alors que la fixture perdait sa propre sortie.
    // Sous Windows le tuyau se vidait avant la sortie, ce qui masquait le
    // défaut. `exitCode` laisse le processus se terminer naturellement.
    const r = lancerOutil([
      'node',
      '-e',
      'process.stdout.write("x".repeat(2*1024*1024)); process.stdout.write("MOTIF_FINAL"); process.exitCode = 1',
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

/**
 * Découpage d'une ligne de commande par le runtime C de Microsoft, selon les
 * règles documentées (« Parsing C command-line arguments ») : c'est ce que
 * font Node, .NET et la plupart des outils sous Windows. Référence écrite ici
 * pour que l'épreuve tourne aussi sous Linux, où la CI s'exécute.
 */
function decouperCommeLeRuntimeC(ligne: string): string[] {
  const args: string[] = []
  let courant = ''
  let dansUnArgument = false
  let entreGuillemets = false
  let i = 0
  while (i < ligne.length) {
    const c = ligne.charAt(i)
    if (c === '\\') {
      let barres = 0
      while (ligne.charAt(i) === '\\') {
        barres++
        i++
      }
      if (ligne.charAt(i) === '"') {
        courant += '\\'.repeat(Math.floor(barres / 2))
        if (barres % 2 === 1) {
          courant += '"'
          i++
        }
      } else {
        courant += '\\'.repeat(barres)
      }
      dansUnArgument = true
      continue
    }
    if (c === '"') {
      if (entreGuillemets && ligne.charAt(i + 1) === '"') {
        courant += '"'
        i += 2
      } else {
        entreGuillemets = !entreGuillemets
        i++
      }
      dansUnArgument = true
      continue
    }
    if ((c === ' ' || c === '\t') && !entreGuillemets) {
      if (dansUnArgument) args.push(courant)
      courant = ''
      dansUnArgument = false
      i++
      continue
    }
    courant += c
    dansUnArgument = true
    i++
  }
  if (dansUnArgument) args.push(courant)
  return args
}

/**
 * Ce que cmd.exe laisserait HORS guillemets : il ignore les barres inverses et
 * bascule à chaque `"`. Un métacaractère hors guillemets serait interprété
 * (séparateur de commandes, redirection…).
 */
function metacaracteresHorsGuillemetsPourCmd(ligne: string): string[] {
  const exposes: string[] = []
  let entreGuillemets = false
  for (const c of ligne) {
    if (c === '"') entreGuillemets = !entreGuillemets
    else if (!entreGuillemets && '&|<>^()'.includes(c)) exposes.push(c)
  }
  return exposes
}

// CodeQL js/incomplete-sanitization (28/09/2026) : l'ancien échappement
// remplaçait `"` par `\"` sans traiter les barres obliques inverses. Ces cas
// sont précisément ceux qu'il cassait.
describe('echapperArgumentWindows — relu à l’identique par le runtime C et par cmd.exe', () => {
  const CAS = [
    'simple',
    'avec espace',
    'a^b',
    'a>b',
    'C:\\dossier x\\',
    'C:\\dossier x\\\\',
    'C:\\a b\\c',
    'a"b c',
    'a\\"b c',
    'a\\\\"b c',
    '"',
    '\\',
    ' ',
    'fin par barre \\',
    'a"b & calc',
    'x" & echo pwned & "',
    '(groupe) | tube',
    '\\\\serveur\\partage\\fichier avec espace',
  ]

  it.each(CAS)('%j ressort intact du découpage du runtime C', (arg) => {
    const ligne = `outil ${echapperArgumentWindows(arg)} suivant`
    expect(decouperCommeLeRuntimeC(ligne)).toEqual(['outil', arg, 'suivant'])
  })

  it.each(CAS)('%j ne laisse aucun métacaractère hors guillemets pour cmd.exe', (arg) => {
    expect(metacaracteresHorsGuillemetsPourCmd(echapperArgumentWindows(arg))).toEqual([])
  })

  it('laisse tel quel un argument sans caractère spécial, et quote la chaîne vide', () => {
    expect(echapperArgumentWindows('C:\\sans\\espace')).toBe('C:\\sans\\espace')
    expect(echapperArgumentWindows('')).toBe('""')
    expect(decouperCommeLeRuntimeC(`a ${echapperArgumentWindows('')} b`)).toEqual(['a', '', 'b'])
  })

  it('double les barres inverses devant un guillemet et en fin d’argument, pas ailleurs', () => {
    expect(echapperArgumentWindows('C:\\dossier x\\')).toBe('"C:\\dossier x\\\\"')
    expect(echapperArgumentWindows('a\\"b c')).toBe('"a\\\\""b c"')
    expect(echapperArgumentWindows('C:\\a b\\c')).toBe('"C:\\a b\\c"')
  })
})
