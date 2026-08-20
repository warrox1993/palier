import { spawnSync } from 'node:child_process'
import { existsSync, readFileSync, rmSync, writeFileSync } from 'node:fs'
import { extname } from 'node:path'
import { describe, expect, it } from 'vitest'
import { parse } from 'yaml'

const COMPOSE = 'db/compose.yaml'
const PAQUET = 'package.json'

const brut = existsSync(COMPOSE) ? readFileSync(COMPOSE, 'utf8') : ''
const compose = brut === '' ? null : parse(brut)

/** Le tag majeur, écrit ici et nulle part ailleurs — D34. Le chercher sur la
 * version majeure seule, et non sur `18.6`, attrape aussi une seconde
 * déclaration qui aurait dérivé d'un correctif. */
const TAG_MAJEUR = 'postgres:18'

/**
 * Les dossiers où une occurrence du tag est du TEXTE et non une déclaration :
 * `docs/` porte D34, qui cite le tag pour le justifier, et `.superpowers/`
 * porte le plan et les briefs, qui décrivent cette épreuve même. Même famille
 * que le ruling P21 — l'exception est restreinte aux documents, jamais aux
 * répertoires de code.
 */
const HORS_CODE = new Set(['docs', '.superpowers'])

const EXTENSIONS_LUES = new Set([
  '.yaml',
  '.yml',
  '.json',
  '.mjs',
  '.cjs',
  '.js',
  '.ts',
  '.tsx',
  '.cs',
  '.sql',
  '.props',
  '.csproj',
  '.md',
])

function fichiersDeCode() {
  const git = spawnSync('git', ['ls-files', '-z'], { encoding: 'utf8' })
  if (git.status !== 0) {
    throw new Error(
      `git ls-files a rendu ${git.status} : ${(git.stderr ?? '').trim() || '(rien sur stderr)'}. ` +
        "Ce contrôle lit l'index git ; privé de dépôt il n'a plus de cible, et il le dit.",
    )
  }
  return git.stdout
    .split('\0')
    .filter(Boolean)
    .filter((f) => EXTENSIONS_LUES.has(extname(f)) && !HORS_CODE.has(f.split('/')[0]))
}

/** Le nom de service que `db:up` passe à `docker compose`. C'est le dernier
 * mot de la commande, et il est cherché ici plutôt que recopié : sans cette
 * lecture, la divergence entre les deux déclarations ne serait vue par
 * personne. */
function serviceDesScripts() {
  const paquet = JSON.parse(readFileSync(PAQUET, 'utf8'))
  const commande = paquet.scripts?.['db:up'] ?? ''
  const mots = commande.trim().split(/\s+/)
  return mots[mots.length - 1] ?? ''
}

describe('garde-fou : la base locale et son compose', () => {
  it('la cible existe', () => {
    expect(existsSync(COMPOSE), `Cible manquante : ${COMPOSE}`).toBe(true)
    expect(compose, `${COMPOSE} ne se parse pas comme du YAML`).not.toBeNull()
  })

  it("le tag PostgreSQL n'est écrit qu'à un seul endroit du dépôt", () => {
    // D34 : le builder Testcontainers de la tâche 5 LIT ce tag au lieu de le
    // redéclarer. Deux déclarations du même tag divergent — et la divergence
    // se ferme par une lecture, jamais par une discipline.
    const occurrences = brut.match(/postgres:/g) ?? []
    expect(occurrences.length, `le tag n'apparaît pas exactement une fois dans ${COMPOSE}`).toBe(1)

    const fichiers = fichiersDeCode()
    // Sans cette assertion, un parcours cassé rendrait une liste vide et le
    // test suivant serait vert en ne lisant rien — ruling P12.
    expect(fichiers.length, "le parcours du dépôt n'a lu aucun fichier").toBeGreaterThan(20)

    // Ce fichier-ci porte le tag parce qu'il le CHERCHE : c'est le détecteur,
    // pas une déclaration. L'exclusion est portée par la commande — ici, par
    // ce filtre — et jamais par un fichier de configuration (D20).
    const soi = 'tests-harness/db.test.mjs'
    const ailleurs = fichiers
      .map((f) => f.replaceAll('\\', '/').replace(/^\.\//, ''))
      .filter((f) => f !== COMPOSE && f !== soi && readFileSync(f, 'utf8').includes(TAG_MAJEUR))
    expect(ailleurs, `Le tag ${TAG_MAJEUR} est redéclaré hors de ${COMPOSE}`).toEqual([])
  })

  it('le parcours ne voit que ce que git suit, jamais ce que le disque porte', () => {
    // Épreuve de franchissement : un parcours du disque compte comme faisant
    // partie du dépôt toute copie locale — worktree, sauvegarde, clone. Le
    // témoin est ÉCRIT, pas décrit : la branche qui se déclenche le jour venu
    // est la seule qu'on n'a pas éprouvée.
    const temoin = 'temoin-hors-index.yaml'
    writeFileSync(temoin, `image: ${TAG_MAJEUR}\n`)
    try {
      expect(
        fichiersDeCode(),
        `${temoin} n'est pas versionné : le parcours ne doit pas le voir`,
      ).not.toContain(temoin)
    } finally {
      rmSync(temoin)
    }
  })

  it("db/compose.yaml ne déclare qu'un seul service — D33", () => {
    const services = Object.keys(compose.services ?? {})
    expect(services.length, `services déclarés : ${services.join(', ')}`).toBe(1)
  })

  it('le service porte le nom que les scripts db:* passent à docker compose', () => {
    const duCompose = Object.keys(compose.services ?? {})[0]
    const desScripts = serviceDesScripts()
    // Les DEUX valeurs sont nommées : « les noms diffèrent » oblige à rouvrir
    // deux fichiers pour savoir lequel corriger.
    expect(
      desScripts,
      `Divergence de nom de service — ${COMPOSE} déclare « ${duCompose} », ` +
        `${PAQUET} passe « ${desScripts} » à docker compose.`,
    ).toBe(duCompose)
  })

  it('db:reset est la seule commande qui détruit des données, et elle le dit', () => {
    // Le `-v` est lu là où il VIT — dans l'action `reset` de `scripts/db.mjs`
    // — et non dans le script npm, qui ne fait que nommer l'action. Un `-v`
    // décoratif dans `package.json`, que le garde ignorerait, passerait cette
    // épreuve en ne prouvant rien.
    const garde = readFileSync('scripts/db.mjs', 'utf8')
    const ligneReset = /reset:\s*\(\)\s*=>\s*\[([^\]]*)\]/.exec(garde)?.[1] ?? ''
    const ligneDown = /down:\s*\(\)\s*=>\s*\[([^\]]*)\]/.exec(garde)?.[1] ?? ''
    expect(ligneReset, "l'action reset est introuvable dans scripts/db.mjs").not.toBe('')
    expect(ligneDown, "l'action down est introuvable dans scripts/db.mjs").not.toBe('')
    expect(ligneReset, 'db:reset ne supprime pas les volumes').toMatch(/'-v'/)
    expect(ligneDown, 'db:down supprime les volumes').not.toMatch(/'-v'/)

    // Une commande destructrice qui n'est que listée n'est pas signalée.
    const lisezMoi = readFileSync('db/README.md', 'utf8')
    expect(lisezMoi, 'db/README.md ne signale pas que db:reset détruit les données').toMatch(
      /db:reset[\s\S]{0,400}détruit/,
    )
  })

  it('les scripts db:* passent par le garde qui teste le moteur', () => {
    // `docker compose` rend une erreur de socket brute quand le démon ne
    // répond pas — mesuré le 20/08/2026 sur ce poste, où WSL2 n'est pas
    // installé. Illisible pour quelqu'un qui découvre le blocage à 23 h.
    const paquet = JSON.parse(readFileSync(PAQUET, 'utf8'))
    for (const nom of ['db:up', 'db:down', 'db:reset']) {
      expect(paquet.scripts?.[nom], `${nom} n'appelle pas scripts/db.mjs`).toMatch(
        /node scripts\/db\.mjs/,
      )
    }
    expect(existsSync('scripts/db.mjs'), 'Cible manquante : scripts/db.mjs').toBe(true)
  })

  it('refuse une action inconnue avec un code distinct', () => {
    const r = spawnSync(process.execPath, ['scripts/db.mjs', 'zoup'], { encoding: 'utf8' })
    expect(r.status, `Une action inconnue a été acceptée :\n${r.stdout}${r.stderr}`).toBe(2)
    expect(`${r.stdout}${r.stderr}`).toMatch(/action inconnue « zoup »/)
  })

  it("crie quand le moteur n'est pas là, au lieu de rendre un nom de tuyau Windows", () => {
    // Le garde est LANCÉ, pas relu. Une épreuve qui cherche « docker info »
    // dans le source de `db.mjs` lit un fichier au lieu d'interroger le
    // système — c'est exactement le défaut de D30, où un `dependabot.yml`
    // juste gardait un service éteint.
    //
    // L'état est PROVOQUÉ en retirant Docker du PATH, et non attendu d'une
    // panne : cette branche reste donc franchissable après l'activation de
    // WSL2, quand le démon répondra. Sans cela elle redeviendrait une branche
    // qu'on ne provoque jamais.
    // Un PATH VIDE, et non un PATH filtré sur le nom des répertoires. Le filtre
    // par nom marchait sous Windows, où Docker vit dans `…\DockerDesktop\…`,
    // et ne retirait rien sous Linux, où il vit dans `/usr/bin` — un répertoire
    // dont le nom ne dit pas ce qu'il contient. En intégration continue, le
    // moteur répondait donc réellement, `db:up` démarrait le conteneur, et
    // l'épreuve rougissait faute du message qu'elle attendait.
    //
    // Quatrième divergence entre le poste de développement et l'intégration
    // continue sur ce projet, et la plus retorse : l'épreuve de l'ABSENCE
    // échouait parce que la chose était PRÉSENTE.
    //
    // `process.execPath` est absolu : Node démarre sans PATH.
    const chemin = ''
    const r = spawnSync(process.execPath, ['scripts/db.mjs', 'up', 'palier-db'], {
      encoding: 'utf8',
      env: { ...process.env, PATH: chemin, Path: chemin },
    })
    const sortie = `${r.stdout}${r.stderr}`
    // Code 3 : distinct du 1 que rend un échec de compose, et du 2 d'un
    // mauvais usage. Un code unique forcerait à relire la sortie pour les
    // séparer.
    expect(r.status, `Le moteur absent n'a pas été signalé :\n${sortie}`).toBe(3)
    // Seconde assertion : le message doit nommer le remède. Un code 3 seul ne
    // prouve pas qu'il est lisible — D19.
    expect(sortie, 'le message ne nomme pas Docker Desktop').toMatch(/Docker Desktop/)
    expect(sortie, 'le message ne rend aucune marche à suivre').toMatch(/winget install|Démarrer/)
    // Et il ne se contente PAS de recracher le tuyau nommé de Docker.
    expect(sortie).not.toMatch(/^npipe/)
  })
})
