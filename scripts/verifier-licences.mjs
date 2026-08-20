#!/usr/bin/env node
// Vérifie que chaque dépendance porte une licence permissive.
// Aucune dépendance : interroge directement les registres npm et NuGet.
// Décision D13 du journal — motivée par le passage de MediatR sous RPL-1.5.
import { existsSync, readFileSync, readdirSync, statSync } from 'node:fs'
import { join } from 'node:path'

const PERMISES = [
  'MIT', 'Apache-2.0', 'BSD-2-Clause', 'BSD-3-Clause', 'ISC',
  'PostgreSQL', '0BSD', 'Unlicense', 'CC0-1.0', 'MIT-0',
]

// Exceptions nominatives, chacune avec son motif. Une licence non standard
// n'est pas forcément interdite : elle doit être LUE, puis inscrite ici avec
// la raison de son admission — jamais ajoutée à la liste blanche, qui ne
// porte que des expressions SPDX permissives.
const EXCEPTIONS = {
  '@axe-core/playwright':
    'MPL-2.0, lue le 20/08/2026. Copyleft PAR FICHIER, sans clause réseau : ' +
    'la section 3.3 autorise explicitement la combinaison avec du code propriétaire ' +
    'sous nos propres termes, et la 3.2 n\'oblige à publier que les fichiers MPL ' +
    'que l\'on MODIFIE. Nous ne modifions pas axe-core. C\'est la différence de fond ' +
    'avec RPL-1.5, qui a motivé D13 : celle-ci ferme la faille SaaS, MPL-2.0 ne la ' +
    'connaît pas. De plus axe-core est un outil de développement, jamais distribué ' +
    'dans le produit, et CLAUDE.md § 3 l\'impose nommément. ' +
    'Ce qui rouvrirait cette exception : un besoin de PATCHER axe-core.',
}

// Pour mémoire, non détecté par ce contrôle qui ne lit que les dépendances
// DIRECTES : `lightningcss` est aussi en MPL-2.0, en transitif de Vite 8.
// Le même raisonnement s'applique, et il n'y a de toute façon aucun moyen de
// l'éviter sans changer d'empaqueteur. Si le contrôle est un jour étendu aux
// dépendances transitives, il devra entrer ici.

// Comparaison par jetons, et non par sous-chaîne. `includes` acceptait
// « MITNFA » parce qu'il contient « MIT » : sur un contrôle juridique, une
// correspondance approximative est un faux vert. Une expression composée
// (« MIT OR Apache-2.0 ») n'est permise que si CHACUN de ses termes l'est.
const estPermise = (l) => {
  if (!l) return false
  const jetons = l
    .split(/\s+(?:OR|AND)\s+|[()]/i)
    .map((t) => t.trim())
    .filter(Boolean)
  return (
    jetons.length > 0 &&
    jetons.every((t) => PERMISES.some((p) => p.toLowerCase() === t.toLowerCase()))
  )
}

// Deux raisons de refus que le libellé unique « licence non standard »
// confondait, alors qu'elles n'ont rien à voir :
//
//   SANS_EXPRESSION — le paquet a été LU. Il publie un fichier de licence et
//     aucune expression SPDX. C'est un verdict sur le paquet : une lecture
//     humaine tranche, puis inscrit le résultat dans EXCEPTIONS.
//   NON_LUE — on n'a rien pu lire du tout : nom erroné, registre en 404,
//     réseau coupé. Ce n'est pas un verdict, c'est une panne du contrôle.
//     Elle échoue fermée, mais elle doit se DISTINGUER : une épreuve qui
//     accepte les deux passe au vert sur une faute de frappe.
//
// Mesuré le 20/08/2026 : `--tester MediatR` et `--tester MediatRxyz-inexistant`
// rendaient tous deux code 1 en réimprimant le nom passé en argument. Une
// épreuve qui n'assertait que le nom ne distinguait donc jamais « refusé pour
// sa licence » de « refusé parce qu'on n'a rien pu lire ».
const SANS_EXPRESSION = "pas d'expression SPDX"
const NON_LUE = 'licence NON LUE'

async function licenceNpm(nom) {
  const r = await fetch(`https://registry.npmjs.org/${encodeURIComponent(nom)}/latest`)
  if (!r.ok) return { licence: null, refus: `${NON_LUE} — registre npm injoignable (HTTP ${r.status})` }
  const j = await r.json()
  const licence = typeof j.license === 'string' ? j.license : (j.license?.type ?? null)
  if (licence) return { licence, refus: null }
  return { licence: null, refus: `${SANS_EXPRESSION} — le paquet ne déclare aucun champ « license »` }
}

// L'API de recherche NuGet ne renvoie PAS `licenseExpression` — mesuré le
// 20/08/2026 : elle rend `licenseUrl`, une URL d'indirection sans valeur
// juridique lisible. Le plan s'appuyait sur ce champ absent, et TOUS les
// paquets NuGet du dépôt ressortaient « licence non standard ». Un contrôle
// qui refuse tout ne refuse rien : on l'aurait désarmé au premier passage.
// L'expression SPDX vit dans l'entrée de catalogue, atteinte en trois sauts :
// recherche (version courante) → feuille d'enregistrement → catalogue.
async function licenceNuget(nom) {
  const recherche = await fetch(
    `https://azuresearch-usnc.nuget.org/query?q=packageid:${encodeURIComponent(nom)}&prerelease=false`,
  )
  if (!recherche.ok)
    return { licence: null, refus: `${NON_LUE} — registre NuGet injoignable (HTTP ${recherche.status})` }
  const d = ((await recherche.json()).data ?? [])[0]
  if (!d?.version) return { licence: null, refus: `${NON_LUE} — paquet introuvable sur nuget.org` }

  const feuille = await fetch(
    `https://api.nuget.org/v3/registration5-semver1/${encodeURIComponent(nom.toLowerCase())}/${encodeURIComponent(d.version)}.json`,
  )
  if (!feuille.ok)
    return { licence: null, refus: `${NON_LUE} — enregistrement illisible (HTTP ${feuille.status})` }
  const { catalogEntry } = await feuille.json()
  if (typeof catalogEntry !== 'string')
    return { licence: null, refus: `${NON_LUE} — entrée de catalogue absente` }

  const catalogue = await fetch(catalogEntry)
  if (!catalogue.ok)
    return { licence: null, refus: `${NON_LUE} — catalogue illisible (HTTP ${catalogue.status})` }
  const c = await catalogue.json()

  // Une expression SPDX absente signale une licence à lire à la main. C'est le
  // cas de MediatR (RPL-1.5) — mais AUSSI de Mediator.SourceGenerator, son
  // alternative MIT retenue par la décision D12 : les deux publient un fichier
  // de licence et aucune expression. Ce contrôle ne les distingue donc pas ; il
  // les arrête tous les deux et exige une lecture humaine. C'est le
  // comportement voulu, pas une limite tue.
  //
  // Le message doit le dire à qui le lit : MediatR est arrêté ici parce qu'il
  // ne publie AUCUNE expression, pas parce que le contrôle aurait reconnu
  // « RPL-1.5 ». Personne ne doit croire que la liste blanche a comparé quoi
  // que ce soit.
  if (c.licenseExpression) return { licence: c.licenseExpression, refus: null }
  return {
    licence: null,
    refus: c.licenseFile
      ? `${SANS_EXPRESSION} — le paquet publie « ${c.licenseFile} », à lire à la main`
      : `${SANS_EXPRESSION} — et aucun fichier de licence publié`,
  }
}

function paquetsNpm() {
  const p = JSON.parse(readFileSync('front/package.json', 'utf8'))
  return Object.keys({ ...p.dependencies, ...p.devDependencies })
}

// L'ordre des attributs d'un élément XML est LIBRE. MSBuild traite
// `<PackageReference Version="5.7.0" Include="NHibernate" />` exactement comme
// l'ordre canonique. Un motif qui exige `Include` juste après le nom d'élément
// ne voit donc qu'une écriture sur deux — mesuré le 20/08/2026 : NHibernate,
// LGPL-2.1-only, déclaré dans l'ordre inverse, sortait « 24 dépendances, toutes
// sous licence permissive » en code 0. Sur un contrôle juridique, un faux vert
// est le pire mode de défaillance possible.
// On isole donc l'élément d'abord, on cherche l'attribut dedans ensuite.
const MOTIF_ELEMENT = /<PackageReference\b([^>]*?)\/?>/g
const MOTIF_INCLUDE = /\bInclude\s*=\s*"([^"]*)"/

// Un `PackageReference` ne vit pas que dans un `.csproj`. `Directory.Build.props`
// à la racine s'applique à TOUS les projets de l'arborescence : un paquet qui y
// est déclaré n'apparaît dans aucun projet et échappait entièrement au contrôle,
// qui ne parcourait que `back/`.
const FICHIERS_RACINE = [
  'Directory.Build.props',
  'Directory.Build.targets',
  'Directory.Packages.props',
]

function fichiersDeProjet() {
  const fichiers = []
  const parcourir = (d) => {
    for (const e of readdirSync(d)) {
      const chemin = join(d, e)
      if (statSync(chemin).isDirectory()) {
        if (e === 'bin' || e === 'obj' || e === 'node_modules') continue
        parcourir(chemin)
      } else if (/\.(csproj|props|targets)$/i.test(e)) {
        fichiers.push(chemin)
      }
    }
  }
  parcourir('back')
  for (const f of FICHIERS_RACINE) if (existsSync(f)) fichiers.push(f)
  return fichiers
}

function paquetsNuget() {
  const noms = new Set()
  for (const chemin of fichiersDeProjet()) {
    const x = readFileSync(chemin, 'utf8')
    for (const [, attributs] of x.matchAll(MOTIF_ELEMENT)) {
      const inclus = MOTIF_INCLUDE.exec(attributs)?.[1]?.trim()
      if (inclus) noms.add(inclus)
    }
  }
  return [...noms]
}

const aTester = process.argv.indexOf('--tester')
const cibles =
  aTester !== -1
    ? [{ nom: process.argv[aTester + 1], source: 'nuget' }]
    : [
        ...paquetsNpm().map((nom) => ({ nom, source: 'npm' })),
        ...paquetsNuget().map((nom) => ({ nom, source: 'nuget' })),
      ]

const refuses = []
for (const { nom, source } of cibles) {
  if (Object.hasOwn(EXCEPTIONS, nom)) continue
  const { licence, refus } = source === 'npm' ? await licenceNpm(nom) : await licenceNuget(nom)
  if (!estPermise(licence)) {
    const dit = licence ?? `(${refus})`
    refuses.push(`${source.padEnd(6)} ${nom.padEnd(50)} ${dit}`)
  }
}

if (refuses.length > 0) {
  console.error('Dépendances hors liste blanche :\n')
  for (const l of refuses) console.error('  ' + l)
  console.error(
    `\nListe blanche : ${PERMISES.join(', ')}.\n` +
      `« ${SANS_EXPRESSION} » = le paquet a été LU, il ne publie pas d'expression SPDX. Le contrôle\n` +
      "  n'a comparé aucune licence : il exige une lecture humaine, puis une inscription dans\n" +
      '  EXCEPTIONS avec son motif. Jamais un ajout à la liste blanche, qui ne porte que des\n' +
      '  expressions SPDX permissives.\n' +
      `« ${NON_LUE} » = le contrôle lui-même a échoué (nom erroné, registre indisponible, réseau\n` +
      "  coupé). Ce n'est pas un verdict sur le paquet : il faut relancer, pas arbitrer.",
  )
  process.exit(1)
}

console.log(`${cibles.length} dépendances vérifiées, toutes sous licence permissive.`)
