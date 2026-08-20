// @vitest-environment node
import { readFileSync, rmSync, writeFileSync } from 'node:fs'
import { afterEach, describe, expect, it } from 'vitest'
import { lancerOutil } from '../../front/tests/harness/run-outil.js'

const SCRIPT = ['node', 'scripts/verifier-licences.mjs']

// Sonde jetable : un `.csproj` sous `back/`, hors de `Palier.sln`, donc invisible
// pour `dotnet build` et `dotnet format`. Elle sert à provoquer la violation que
// le contrôle doit voir, sans toucher à un projet réel.
const SONDE = 'back/tests-harness/fixtures/Sonde.csproj'
const RACINE = 'Directory.Build.props'
const RACINE_ORIGINE = readFileSync(RACINE)

// NHibernate publie une expression SPDX, `LGPL-2.1-only` : réciproque, hors
// liste blanche. Le refus porte donc sur la LICENCE elle-même — pas sur une
// lecture qui aurait échoué. C'est ce qui en fait une sonde utilisable pour
// éprouver l'extraction : si le paquet est vu, il est refusé, sans ambiguïté.
const REFUS_ATTENDU = /NHibernate\s+LGPL-2\.1-only/

const projetSonde = (attributs) =>
  `<Project Sdk="Microsoft.NET.Sdk">\n  <ItemGroup>\n    <PackageReference ${attributs} />\n  </ItemGroup>\n</Project>\n`

// Nettoyage après CHAQUE épreuve, et non après toutes : une épreuve qui échoue
// en cours de route laisserait sinon sa violation en place, et les suivantes
// rougiraient pour la mauvaise cause.
afterEach(() => {
  rmSync(SONDE, { force: true })
  writeFileSync(RACINE, RACINE_ORIGINE)
})

describe('garde-fou : licences des dépendances', () => {
  it('accepte les dépendances actuelles', () => {
    const r = lancerOutil(SCRIPT)
    expect(r.code, `Une dépendance actuelle sort de la liste blanche :\n${r.sortie}`).toBe(0)
    // Seconde assertion : un code 0 prouve seulement que rien n'a planté. Sans
    // ce motif, l'épreuve resterait verte si le script sortait 0 sans avoir
    // examiné une seule dépendance.
    expect(r.sortie, `Aucun décompte imprimé :\n${r.sortie}`).toMatch(/\d+ dépendances vérifiées/)
  })

  it('refuse un paquet qui ne publie aucune expression SPDX', () => {
    const r = lancerOutil([...SCRIPT, '--tester', 'MediatR'])
    expect(r.code, `MediatR a été accepté :\n${r.sortie}`).not.toBe(0)
    // Le motif porte sur le NOM SUIVI DU VERDICT, pas sur le nom seul.
    // Mesuré le 20/08/2026 : le script réimprime le nom passé en argument dans
    // TOUS les cas de refus. Une assertion sur `/MediatR/` passait donc au vert
    // avec une faute de frappe, un registre en 404 ou une coupure réseau — elle
    // ne distinguait jamais « refusé pour sa licence » de « rien n'a pu être lu ».
    //
    // Et le nom doit être dans le motif : la légende du message d'erreur cite
    // les DEUX verdicts pour l'expliquer au lecteur. Un motif sur le seul
    // libellé matcherait cette légende, pas la ligne de refus.
    expect(r.sortie, `Ce n'est pas le verdict attendu :\n${r.sortie}`).toMatch(
      /MediatR\s+\(pas d'expression SPDX/,
    )
  })

  it("distingue « licence non lue » d'un verdict sur la licence", () => {
    const r = lancerOutil([...SCRIPT, '--tester', 'MediatRxyz-inexistant'])
    expect(r.code, `Un paquet introuvable a été accepté :\n${r.sortie}`).not.toBe(0)
    expect(r.sortie, `La panne du contrôle n'est pas nommée :\n${r.sortie}`).toMatch(
      /MediatRxyz-inexistant\s+\(licence NON LUE/,
    )
    expect(
      r.sortie,
      `Une panne du contrôle est présentée comme un verdict juridique :\n${r.sortie}`,
    ).not.toMatch(/MediatRxyz-inexistant\s+\(pas d'expression SPDX/)
  })

  // Même paquet, même fichier, seul l'ordre des attributs change. L'ordre des
  // attributs d'un élément XML est libre et MSBuild traite les deux à
  // l'identique : les deux doivent être refusés. Mesuré avant correctif :
  // l'ordre inversé sortait « 24 dépendances, toutes sous licence permissive »
  // en code 0, avec NHibernate dans l'arborescence.
  for (const [libelle, attributs] of [
    ['ordre canonique', 'Include="NHibernate" Version="5.7.0"'],
    ['ordre inversé', 'Version="5.7.0" Include="NHibernate"'],
  ]) {
    it(`refuse un paquet réciproque déclaré en ${libelle}`, () => {
      writeFileSync(SONDE, projetSonde(attributs))
      const r = lancerOutil(SCRIPT)
      expect(r.code, `NHibernate (${libelle}) a traversé le contrôle :\n${r.sortie}`).not.toBe(0)
      expect(r.sortie, `Ce n'est pas NHibernate qui a été refusé :\n${r.sortie}`).toMatch(
        REFUS_ATTENDU,
      )
    })
  }

  it('examine aussi les paquets déclarés hors de back/, à la racine', () => {
    // `Directory.Build.props` s'applique à TOUS les projets. Un paquet qui y
    // est déclaré n'apparaît dans aucun `.csproj` : le contrôle, qui ne
    // parcourait que `back/`, ne le voyait jamais.
    writeFileSync(
      RACINE,
      RACINE_ORIGINE.toString('utf8').replace(
        '</Project>',
        '  <ItemGroup>\n    <PackageReference Include="NHibernate" Version="5.7.0" />\n  </ItemGroup>\n</Project>',
      ),
    )
    const r = lancerOutil(SCRIPT)
    expect(r.code, `Un paquet déclaré à la racine a échappé au contrôle :\n${r.sortie}`).not.toBe(0)
    expect(r.sortie, `Ce n'est pas NHibernate qui a été refusé :\n${r.sortie}`).toMatch(
      REFUS_ATTENDU,
    )
  })
})
