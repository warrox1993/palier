#!/usr/bin/env node
// Résout le chemin de l'exécutable gitleaks, où qu'il soit installé.
//
// gitleaks est un binaire Go, pas un paquet npm — celui du registre est un
// squat vide (décision D23). Il ne descend donc pas avec `npm ci`, et le
// harnais dépend d'une installation faite hors du dépôt. Mesuré le 20/08/2026 :
// `winget install Gitleaks.Gitleaks` réussit, place le binaire sous
// `%LOCALAPPDATA%\Microsoft\WinGet\Packages\…` et **ne crée aucun lien dans le
// PATH**. `gitleaks` n'était appelable par son nom depuis aucun terminal, et
// `npm run verify` échouait sur trois épreuves.
//
// Ce n'est pas la faute de l'épreuve : elle a fait exactement ce qu'on lui
// demande — refuser de dire « vert » quand l'outil n'a pas tourné. C'est la
// seconde assertion du ruling P8 qui a attrapé le cas, parce que sous Windows
// `cmd.exe` absorbe l'erreur et rend un code 1 ordinaire, indistinguable d'un
// refus (décision D22).
//
// Ce module rend la résolution reproductible : il cherche aux emplacements
// connus, et à défaut donne les commandes d'installation plutôt que d'échouer
// en silence.
import { existsSync, readdirSync } from 'node:fs'
import { join } from 'node:path'
import { pathToFileURL } from 'node:url'
import { spawnSync } from 'node:child_process'

const SUR_WINDOWS = process.platform === 'win32'
const NOM = SUR_WINDOWS ? 'gitleaks.exe' : 'gitleaks'

export const COMMANDES_INSTALLATION = [
  '  Windows : winget install Gitleaks.Gitleaks',
  '  macOS   : brew install gitleaks',
  '  Linux   : https://github.com/gitleaks/gitleaks/releases',
].join('\n')

function surLePath() {
  const r = spawnSync(SUR_WINDOWS ? 'where' : 'which', ['gitleaks'], {
    encoding: 'utf8',
    shell: false,
  })
  if (r.status !== 0) return null
  const premier = (r.stdout ?? '').split(/\r?\n/).find((l) => l.trim())
  return premier?.trim() || null
}

// winget installe sous un dossier dont le nom porte un identifiant de source
// variable : on énumère plutôt que de le deviner.
function sousWinget() {
  if (!SUR_WINDOWS) return null
  const racine = join(process.env.LOCALAPPDATA ?? '', 'Microsoft', 'WinGet', 'Packages')
  if (!existsSync(racine)) return null
  for (const dossier of readdirSync(racine)) {
    if (!dossier.startsWith('Gitleaks.')) continue
    const candidat = join(racine, dossier, NOM)
    if (existsSync(candidat)) return candidat
  }
  return null
}

/**
 * Retourne le chemin absolu de gitleaks, ou `null` s'il est introuvable.
 * L'ordre reflète la préférence : ce que l'utilisateur a mis sur son PATH
 * l'emporte sur ce qu'un gestionnaire a posé ailleurs.
 */
export function cheminGitleaks() {
  return surLePath() ?? sousWinget()
}

/**
 * Comme `cheminGitleaks`, mais **lève** avec les commandes d'installation.
 * À utiliser partout où l'absence de l'outil doit refuser, jamais passer :
 * un contrôle de secrets qui se laisse sauter ne protège personne.
 */
export function exigerGitleaks() {
  const chemin = cheminGitleaks()
  if (chemin) return chemin
  throw new Error(
    `gitleaks est introuvable — ni sur le PATH, ni aux emplacements connus.\n${COMMANDES_INSTALLATION}\n` +
      "Sous Windows, winget n'ajoute pas toujours le binaire au PATH : ce module " +
      'le cherche aussi sous %LOCALAPPDATA%\\Microsoft\\WinGet\\Packages.',
  )
}

// Appelé directement : imprime le chemin, ou explique et sort en 1.
// Sert au hook de pré-commit, qui est un script shell et ne peut pas importer.
//
// `pathToFileURL` plutôt qu'une concaténation : sous Windows, `argv[1]` est un
// chemin `C:\…` que `file://${…}` transforme en `file://C:/…` — deux barres au
// lieu de trois, et la comparaison échoue toujours en silence. Le bloc ne
// s'exécutait jamais, et le script sortait en 0 sans rien imprimer.
if (import.meta.url === pathToFileURL(process.argv[1] ?? '').href) {
  const chemin = cheminGitleaks()
  if (!chemin) {
    console.error('gitleaks est introuvable — ni sur le PATH, ni aux emplacements connus.')
    console.error(COMMANDES_INSTALLATION)
    console.error("Le contrôle de secrets est refusé tant qu'aucun binaire ne peut tourner.")
    process.exit(1)
  }
  console.log(chemin)
}
