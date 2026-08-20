#!/usr/bin/env node
// Garde des commandes `db:*`. Il existe pour UNE raison, mesurée le 20/08/2026
// sur un poste où WSL2 n'est pas installé : quand le démon ne répond pas,
// `docker compose up` rend
//
//   unable to get image '<image>': failed to connect to the docker API at
//   npipe:////./pipe/dockerDesktopLinuxEngine; check if the path is correct and
//   if the daemon is running: open //./pipe/dockerDesktopLinuxEngine: Le
//   fichier spécifié est introuvable.
//
// — un tuyau nommé Windows, aucun remède, et le mot « Docker Desktop »
// n'apparaît nulle part. C'est illisible pour qui découvre le blocage à 23 h,
// et c'est la branche qu'on ne provoque jamais parce qu'elle ne survient qu'un
// jour de panne. Elle a été franchie pendant qu'elle était gratuite.
import { spawnSync } from 'node:child_process'
import { dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

// Comme `regles-projet.mjs` : toutes les cibles se résolvent depuis la racine
// du dépôt, jamais depuis le répertoire courant. `npm run db:up` lancé depuis
// `front/` doit désigner le même compose.
const RACINE = dirname(dirname(fileURLToPath(import.meta.url)))
const COMPOSE = resolve(RACINE, 'db', 'compose.yaml')

const CODE_COMPOSE = 1
const CODE_USAGE = 2
// Distinct du 1 : « le moteur est éteint » et « compose a échoué » n'appellent
// pas le même geste, et un code unique force à relire la sortie pour les
// séparer.
const CODE_MOTEUR = 3

const ACTIONS = {
  up: (service) => ['compose', '-f', COMPOSE, 'up', '-d', ...(service ? [service] : [])],
  down: () => ['compose', '-f', COMPOSE, 'down'],
  // La SEULE commande du projet qui détruit des données. Le `-v` supprime le
  // volume nommé `palier-db-data` avec le conteneur.
  reset: () => ['compose', '-f', COMPOSE, 'down', '-v'],
}

function lancerDocker(args) {
  // `shell: false` : `docker` est un vrai `.exe` que CreateProcess résout sur
  // le PATH. Rester hors du shell évite DEP0190 et le recollage d'arguments,
  // et rend `r.error` exploitable — sous `shell: true`, cmd.exe absorbe
  // l'absence du binaire en un code 1 ordinaire (ruling P18).
  return spawnSync('docker', args, { stdio: 'inherit', shell: false, encoding: 'utf8' })
}

function moteurInjoignable() {
  const r = spawnSync('docker', ['info', '--format', '{{.ServerVersion}}'], {
    encoding: 'utf8',
    shell: false,
  })
  if (r.error) {
    console.error(
      "db : le client Docker est introuvable. Ce n'est pas le démon qui manque, c'est " +
        "l'exécutable.\n" +
        '  Installer Docker Desktop : winget install Docker.DockerDesktop\n' +
        '  Puis rouvrir le terminal, pour que le PATH soit rechargé.',
    )
    return true
  }
  if (r.status !== 0) {
    console.error(
      'db : le moteur Docker ne répond pas. Le CLIENT répond parfaitement — ' +
        "`docker --version` réussit — mais le DÉMON est injoignable, et c'est lui qui " +
        'lève les conteneurs.\n' +
        '  1. Démarrer Docker Desktop et attendre que sa baleine cesse de clignoter.\n' +
        "  2. Vérifier : docker info --format '{{.ServerVersion}}' doit rendre une version.\n" +
        "  3. Si la commande échoue encore, WSL2 ou Hyper-V n'est pas activé : " +
        'wsl --install (droits administrateur et redémarrage requis).\n' +
        `  Message rendu par Docker :\n  ${(r.stderr ?? '').trim()}`,
    )
    return true
  }
  return false
}

const action = process.argv[2]
const service = process.argv[3]

if (action === undefined || !(action in ACTIONS)) {
  console.error(
    `db : action inconnue « ${action ?? ''} ». Attendu : ${Object.keys(ACTIONS).join(', ')}.\n` +
      '  npm run db:up · npm run db:down · npm run db:reset',
  )
  process.exit(CODE_USAGE)
}

if (moteurInjoignable()) process.exit(CODE_MOTEUR)

const r = lancerDocker(ACTIONS[action](service))
process.exit(r.status === 0 ? 0 : CODE_COMPOSE)
