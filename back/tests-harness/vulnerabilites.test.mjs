// @vitest-environment node
import { describe, expect, it } from 'vitest'
import { lancerOutil } from '../../front/tests/harness/run-outil.js'

const PROJETS = [
  'Palier.Domain',
  'Palier.Application',
  'Palier.Infrastructure',
  'Palier.Api',
  'Palier.Domain.Tests',
]

/** Toutes les gravités déclarées, quel que soit le niveau d'imbrication. */
function gravites(rapport) {
  const trouvees = []
  for (const projet of rapport.projects ?? []) {
    for (const cadre of projet.frameworks ?? []) {
      for (const liste of [cadre.topLevelPackages ?? [], cadre.transitivePackages ?? []]) {
        for (const paquet of liste) {
          for (const v of paquet.vulnerabilities ?? []) {
            trouvees.push(`${paquet.id} — ${v.severity} — ${v.advisoryurl ?? ''}`)
          }
        }
      }
    }
  }
  return trouvees
}

describe('garde-fou : vulnérabilités des dépendances', () => {
  it('aucune vulnérabilité connue dans les paquets du backend', () => {
    // `--format json` plutôt que la sortie de texte prévue au plan.
    //
    // Ce n'est PAS pour une raison de langue : mesuré sur une CLI en français,
    // la valeur de gravité s'imprime « High » en clair, seul l'en-tête de
    // colonne est traduit. Le motif du plan aurait donc matché.
    //
    // La raison est que l'assertion du plan est NÉGATIVE — « la sortie ne
    // contient ni High ni Critical » — et qu'une assertion négative sur du
    // texte libre passe aussi quand l'outil n'a rien imprimé. Le rapport
    // structuré permet au contraire d'affirmer POSITIVEMENT que les cinq
    // projets ont été inspectés, ce qu'aucune lecture du texte ne donne.
    const r = lancerOutil([
      'dotnet', 'list', 'back/Palier.sln', 'package', '--vulnerable', '--include-transitive',
      '--format', 'json',
    ])
    expect(r.code, `La commande a échoué :\n${r.sortie}`).toBe(0)

    // `dotnet list package --vulnerable` sort en code 0 même quand il trouve
    // quelque chose : c'est la sortie qu'il faut examiner, pas le code de
    // retour. C'est exactement le genre de contrôle qui approuve en silence si
    // on ne le vérifie pas.
    const debut = r.sortie.indexOf('{')
    const fin = r.sortie.lastIndexOf('}')
    expect(debut, `Aucun rapport JSON dans la sortie :\n${r.sortie}`).toBeGreaterThanOrEqual(0)
    const rapport = JSON.parse(r.sortie.slice(debut, fin + 1))

    // Preuve que la commande a bien parcouru la solution. Sans elle, un
    // rapport vide — solution introuvable, restauration muette — passerait
    // pour une absence de vulnérabilité.
    const inspectes = (rapport.projects ?? []).map((p) => p.path).join('\n')
    for (const projet of PROJETS) {
      expect(inspectes, `Projet non inspecté : ${projet}`).toContain(projet)
    }

    const graves = gravites(rapport).filter((g) => /—\s*(high|critical)\s*—/i.test(g))
    expect(graves, `Vulnérabilité grave détectée :\n${graves.join('\n')}`).toEqual([])
  })
})
