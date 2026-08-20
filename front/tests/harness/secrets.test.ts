// @vitest-environment node
import { existsSync, readFileSync, rmSync, writeFileSync } from 'node:fs'
import { describe, expect, it } from 'vitest'
import { lancerOutil } from './run-outil'
import { exigerGitleaks } from '../../../scripts/outil-gitleaks.mjs'

// gitleaks est un binaire Go installe hors npm : il ne descend pas avec
// `npm ci`. Mesure du 20/08/2026 : `winget install Gitleaks.Gitleaks` reussit
// et ne cree AUCUN lien dans le PATH — l'outil n'etait appelable par son nom
// depuis aucun terminal, et ces trois epreuves echouaient. Le resolveur cherche
// aux emplacements connus, et LEVE avec les commandes d'installation quand il
// ne trouve rien : l'absence de l'outil doit refuser, jamais passer.
const GITLEAKS = exigerGitleaks()

const FIXTURE = 'tests/harness/fixtures/faux-secret.txt'

// gitleaks est lancé depuis la RACINE, comme le hook et la CI le font :
// `[extend] path` se résout par rapport au répertoire courant et non par
// rapport au fichier de configuration — mesuré le 20/08/2026, une exécution
// depuis `front/` rend « failed to load extended config ». Les chemins de cette
// épreuve sont donc ceux de la racine.
const DEPUIS_LA_RACINE = { cwd: '..' }
const FIXTURE_DEPUIS_RACINE = 'front/tests/harness/fixtures'

// Le faux secret est VERSIONNÉ : sans exception, gitleaks le trouverait à
// chaque exécution sur le dépôt réel et le contrôle crierait au loup tous les
// jours. L'exception vit dans `.gitleaks.toml` — et c'est précisément pourquoi
// l'épreuve n'utilise PAS ce fichier : une cible allowlistée reste invisible
// même quand son dossier est nommé en argument. Mesuré : la configuration
// réelle scanne 7 459 octets là où les règles seules en scannent 7 756, soit le
// fichier entièrement sauté. gitleaks est le QUATRIÈME outil à porter ce piège,
// après Oxlint, Prettier et Playwright (rulings P6 et P9). L'épreuve a donc sa
// propre configuration, sans exception, comme `knip.fixtures.json` et
// `playwright.fixtures.config.ts`.
const CONFIG_REELLE = '.gitleaks.toml'
const CONFIG_REGLES = '.gitleaks.regles.toml'

describe('garde-fou : secrets', () => {
  it('la fixture de violation existe', () => {
    expect(existsSync(FIXTURE), `Cible manquante : ${FIXTURE}`).toBe(true)
  })

  it('gitleaks détecte une clé au format reconnu', () => {
    const r = lancerOutil(
      [
        GITLEAKS,
        'dir',
        FIXTURE_DEPUIS_RACINE,
        '--config',
        CONFIG_REGLES,
        '--redact',
        '--no-banner',
        '-v',
      ],
      DEPUIS_LA_RACINE,
    )
    expect(r.code, `gitleaks n'a rien vu :\n${r.sortie}`).not.toBe(0)
    // Seconde assertion obligatoire (P8) : un code non nul prouve seulement que
    // quelque chose a échoué, pas que l'outil a refusé. Mesuré le 20/08/2026 :
    // gitleaks absent du PATH sort en code 1 sous cmd.exe avec « n'est pas
    // reconnu en tant que commande interne », SANS que `lancerOutil` puisse
    // lever — cmd.exe absorbe l'erreur et rend un code, pas un `spawn ENOENT`.
    // Sans la ligne suivante, l'épreuve serait verte sur une machine où
    // gitleaks n'est pas installé.
    expect(r.sortie, 'gitleaks a échoué sans avoir signalé de fuite').toMatch(/leaks found/i)
    expect(r.sortie, "la règle maison n'est pas celle qui a mordu").toMatch(/cle-anthropic/)
  })

  it("l'exception ne couvre que la fixture, pas le reste du dépôt", () => {
    // Une exception trop large est un faux vert permanent : elle rassure au
    // lieu de protéger (ruling P16). On provoque donc un secret AILLEURS, avec
    // la configuration RÉELLE, et on exige qu'il soit refusé.
    const localement = 'tests/harness/secret-provoque.tmp.txt'
    const depuisRacine = 'front/tests/harness/secret-provoque.tmp.txt'
    // Le faux secret est ASSEMBLÉ, jamais écrit en entier : le hook de
    // pré-commit a refusé ce fichier tant que le littéral y figurait — la
    // preuve, au passage, que gitleaks lit bien le contenu indexé et pas
    // seulement les noms de fichiers. Un littéral ici resterait aussi une
    // fausse alerte permanente pour tout autre scanner qui lit le dépôt.
    const faux = `sk-ant-${'api03-PROVOQUE-PAR-EPREUVE-000000000000'}`
    writeFileSync(localement, `ANTHROPIC_API_KEY=${faux}\n`)
    try {
      const r = lancerOutil(
        [GITLEAKS, 'dir', depuisRacine, '--config', CONFIG_REELLE, '--redact', '--no-banner', '-v'],
        DEPUIS_LA_RACINE,
      )
      expect(r.code, `L'exception de .gitleaks.toml est trop large :\n${r.sortie}`).not.toBe(0)
      expect(r.sortie).toMatch(/cle-anthropic/)
    } finally {
      rmSync(localement, { force: true })
    }
  })

  it('la configuration réelle laisse passer la fixture, et elle seule', () => {
    const r = lancerOutil(
      [
        GITLEAKS,
        'dir',
        FIXTURE_DEPUIS_RACINE,
        '--config',
        CONFIG_REELLE,
        '--redact',
        '--no-banner',
        '-v',
      ],
      DEPUIS_LA_RACINE,
    )
    expect(r.code, `Le dépôt réel crierait au loup à chaque exécution :\n${r.sortie}`).toBe(0)
    // Le code 0 seul serait vert aussi si gitleaks n'avait rien scanné.
    expect(r.sortie, "gitleaks n'a pas scanné la fixture").toMatch(/no leaks found/i)
  })

  it('les deux hooks existent', () => {
    expect(existsSync('../.husky/pre-commit'), 'Cible manquante : .husky/pre-commit').toBe(true)
    expect(existsSync('../.husky/pre-push'), 'Cible manquante : .husky/pre-push').toBe(true)
  })

  it('le hook pre-commit appelle gitleaks sur ce qui est indexé', () => {
    const hook = readFileSync('../.husky/pre-commit', 'utf8')
    expect(hook).toMatch(/gitleaks/)
    // `--staged` est ce qui distingue « ce que je m'apprête à commiter » de
    // « ce qui est sur le disque » : sans lui, le hook scanne l'arbre de travail
    // et laisse passer un secret indexé puis retiré du fichier.
    expect(hook, "le hook scanne l'arbre et non l'index").toMatch(/--staged/)
  })

  it("le hook pre-push n'appelle que verify", () => {
    const hook = readFileSync('../.husky/pre-push', 'utf8')
    expect(hook).toContain('npm run verify')
    // Le hook délègue, il n'énumère pas : sinon il diverge de la CI.
    expect(hook).not.toMatch(/npm run (lint|typecheck|build|knip)/)
  })
})
