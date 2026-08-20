import { existsSync, readFileSync } from 'node:fs'
import { describe, expect, it } from 'vitest'
import { parse } from 'yaml'

const CHEMIN = '.github/workflows/ci.yml'
const ACTION_GITLEAKS = '.github/actions/installer-gitleaks/action.yml'
const DEPENDABOT = '.github/dependabot.yml'

const brut = existsSync(CHEMIN) ? readFileSync(CHEMIN, 'utf8') : ''
const flux = brut === '' ? null : parse(brut)

/** Toutes les valeurs `uses:` du fichier, action locale comprise. */
function toutesLesActions(texte) {
  return [...texte.matchAll(/^\s*-?\s*uses:\s*(\S+)/gm)].map((m) => m[1])
}

describe('garde-fou : intégration continue', () => {
  it('le workflow existe et est du YAML valide', () => {
    expect(existsSync(CHEMIN), `Cible manquante : ${CHEMIN}`).toBe(true)
    expect(flux, 'le workflow ne se parse pas').not.toBeNull()
    expect(Object.keys(flux.jobs ?? {})).toContain('franchissement')
  })

  it('chaque action tierce est épinglée par empreinte, jamais par étiquette', () => {
    // Une étiquette est mutable. `actions/checkout@v7` repointé par un attaquant
    // exécuterait son code avec les droits du job. Une empreinte de commit ne
    // se déplace pas. Ce test est le seul endroit qui empêche un retour à
    // `@v7` lors d'une mise à jour faite à la main.
    const actions = toutesLesActions(brut)
    expect(actions.length, "aucune action trouvée : le motif n'a rien lu").toBeGreaterThan(0)
    const tierces = actions.filter((a) => !a.startsWith('./'))
    expect(
      tierces.length,
      'aucune action tierce : le workflow a-t-il changé de forme ?',
    ).toBeGreaterThan(0)
    for (const action of tierces) {
      expect(action, `Action non épinglée par empreinte : ${action}`).toMatch(
        /^[\w.-]+\/[\w.-]+@[0-9a-f]{40}$/,
      )
    }
  })

  it('le job de franchissement dépend de tous les autres', () => {
    // Sans cette dépendance, `franchissement` passerait au vert pendant qu'un
    // autre job échoue, et c'est lui que la protection de branche exige.
    const jobs = Object.keys(flux.jobs)
    const autres = jobs.filter((j) => j !== 'franchissement')
    const requis = flux.jobs.franchissement.needs ?? []
    for (const job of autres) {
      expect(requis, `Le job « ${job} » n'est pas exigé par franchissement`).toContain(job)
    }
  })

  it('le franchissement installe les dépendances des DEUX package.json', () => {
    // Les épreuves de `tests-harness/` importent `yaml`, déclaré à la racine.
    // Sans `npm ci` racine, le job échoue sur ERR_MODULE_NOT_FOUND — invisible
    // en local, où `node_modules/` existe depuis longtemps. Mesuré en CI le
    // 20/08/2026. Le job installait `front` seulement.
    const etapes = flux.jobs.franchissement.steps.map((e) => String(e.run ?? ''))
    expect(etapes, 'les dépendances du front ne sont pas installées').toContain(
      'npm --prefix front ci',
    )
    expect(etapes, 'les dépendances de la racine ne sont pas installées').toContain('npm ci')
  })

  it('le franchissement lance bien les épreuves du harnais', () => {
    const etapes = JSON.stringify(flux.jobs.franchissement.steps)
    expect(etapes).toContain('npm run test:harness')
    // Et il vérifie que le hook local délègue : c'est ce qui empêche le poste
    // de développement et la CI de contrôler deux choses différentes.
    expect(etapes).toContain('.husky/pre-push')
  })

  it("l'historique complet est récupéré là où gitleaks le parcourt", () => {
    // `gitleaks git` lit l'HISTORIQUE. Avec le clone superficiel par défaut
    // d'actions/checkout, il n'aurait qu'un commit à lire et rendrait « no
    // leaks found » — un vert qui ne prouve rien, exactement le faux vert du
    // ruling P16.
    const securite = flux.jobs.securite
    const scanne = securite.steps.some((e) => String(e.run ?? '').includes('gitleaks git'))
    expect(scanne, 'le job securite ne scanne plus l’historique').toBe(true)
    const checkout = securite.steps.find((e) => String(e.uses ?? '').includes('actions/checkout'))
    expect(
      checkout?.with?.['fetch-depth'],
      'clone superficiel : gitleaks ne verrait qu’un commit',
    ).toBe(0)
  })

  it('gitleaks est installé avec vérification de son empreinte', () => {
    expect(existsSync(ACTION_GITLEAKS), `Cible manquante : ${ACTION_GITLEAKS}`).toBe(true)
    const action = readFileSync(ACTION_GITLEAKS, 'utf8')
    // Télécharger un binaire sans vérifier son empreinte, dans un job qui lit
    // tout le dépôt, revient à exécuter ce que le réseau veut bien rendre.
    expect(action, "l'empreinte du binaire n'est pas vérifiée").toMatch(/sha256sum\s+--check/)
    expect(action).toMatch(/[0-9a-f]{64}/)
  })

  it('Dependabot couvre les cinq écosystèmes, actions et compose compris', () => {
    // `docker-compose:/db` porte le tag PostgreSQL, seul endroit du dépôt où il
    // est écrit (D34). Sans suivi, la base locale dérive de l'instance managée
    // en silence — la divergence qu'une base locale existe pour supprimer.
    expect(existsSync(DEPENDABOT), `Cible manquante : ${DEPENDABOT}`).toBe(true)
    const conf = parse(readFileSync(DEPENDABOT, 'utf8'))
    const cles = conf.updates.map((u) => `${u['package-ecosystem']}:${u.directory}`)
    for (const attendu of [
      'npm:/front',
      'npm:/',
      'nuget:/back',
      'github-actions:/',
      'docker-compose:/db',
    ]) {
      expect(cles, `Écosystème non suivi par Dependabot : ${attendu}`).toContain(attendu)
    }
  })

  it('chaque écosystème porte un délai de refroidissement', () => {
    // Sans `cooldown`, une version compromise publiée sur npm est proposée à la
    // fusion dans l'heure — le vecteur des attaques de chaîne
    // d'approvisionnement. Trouvé par semgrep à sa première exécution en
    // intégration continue le 20/08/2026 ; le plan ne le prévoyait pas.
    //
    // Cette épreuve existe parce que le correctif est supprimable en silence :
    // retirer quatre blocs `cooldown` ne fait échouer aucun autre contrôle, et
    // semgrep ne tourne que dans un job dont l'échec peut être toléré un jour.
    const conf = parse(readFileSync(DEPENDABOT, 'utf8'))
    for (const u of conf.updates) {
      const quoi = `${u['package-ecosystem']}:${u.directory}`
      expect(u.cooldown, `Aucun délai de refroidissement sur ${quoi}`).toBeDefined()
      expect(
        u.cooldown['default-days'],
        `Délai de refroidissement nul ou absent sur ${quoi}`,
      ).toBeGreaterThan(0)
    }
  })

  it('le gardien de main existe et surveille la CI', () => {
    // Il n'y a AUCUNE protection de branche sur ce dépôt : GitHub Free ne
    // l'autorise que sur les dépôts publics, et `palier` est privé. Vérifié le
    // 20/08/2026, les deux API rendent 403 avec des droits d'administration
    // pleins. Ce gardien est ce qui remplace la barrière absente — il n'empêche
    // rien, il rend l'échec impossible à ignorer. Décision D29.
    //
    // Cette épreuve existe parce qu'un fichier de workflow se supprime sans que
    // rien ne bouge : il n'est appelé par aucun script, aucun test, aucune
    // commande. C'est exactement le mode de défaillance du ruling P2.
    const chemin = '.github/workflows/gardien-main.yml'
    expect(existsSync(chemin), `Cible manquante : ${chemin}`).toBe(true)
    const gardien = parse(readFileSync(chemin, 'utf8'))

    // `on:` est lu par YAML comme le booléen `true` — piège classique de
    // YAML 1.1, et la raison pour laquelle on lit les deux clés.
    const declencheur = gardien.on ?? gardien[true]
    expect(declencheur?.workflow_run?.workflows, 'le gardien ne suit pas la CI').toContain('CI')
    expect(
      declencheur?.workflow_run?.branches,
      'le gardien ne surveille pas la branche par défaut',
    ).toContain('main')

    // Sans `issues: write`, le gardien tourne, réussit, et n'ouvre rien : il
    // afficherait vert en ne protégeant rien.
    expect(gardien.permissions?.issues, "le gardien ne peut pas ouvrir d'alerte").toBe('write')

    const script = JSON.stringify(gardien.jobs)
    expect(script, "l'alerte ne se ferme jamais toute seule").toContain('gh issue close')
    expect(script, "l'alerte ne s'ouvre jamais").toContain('gh issue create')
  })

  it('le seuil Lighthouse est déclaré et bloquant', () => {
    const chemin = 'front/.lighthouserc.json'
    expect(existsSync(chemin), `Cible manquante : ${chemin}`).toBe(true)
    const conf = JSON.parse(readFileSync(chemin, 'utf8'))
    const assertions = conf.ci.assert.assertions
    // `08-workflow.md` § 9 fait de « Lighthouse supérieur à 90 » un critère de
    // sortie. Un seuil déclaré en avertissement ne bloque rien.
    for (const categorie of ['categories:performance', 'categories:accessibility']) {
      expect(assertions[categorie]?.[0], `${categorie} n'est pas bloquant`).toBe('error')
      expect(assertions[categorie]?.[1]?.minScore).toBeGreaterThanOrEqual(0.9)
    }
  })
})
