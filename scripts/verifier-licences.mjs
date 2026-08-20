#!/usr/bin/env node
// Vérifie que chaque dépendance porte une licence permissive.
// Aucune dépendance : interroge directement les registres npm et NuGet.
// Décision D13 du journal — motivée par le passage de MediatR sous RPL-1.5.
import { readFileSync, readdirSync, statSync } from 'node:fs'
import { join } from 'node:path'

const PERMISES = [
  'MIT', 'Apache-2.0', 'BSD-2-Clause', 'BSD-3-Clause', 'ISC',
  'PostgreSQL', '0BSD', 'Unlicense', 'CC0-1.0', 'MIT-0',
]

// Exceptions nominatives, chacune avec son motif. Une licence non standard
// n'est pas forcément interdite : elle doit être LUE, puis inscrite ici avec
// la raison de son admission — jamais ajoutée à la liste blanche, qui ne
// porte que des expressions SPDX permissives.
// La liste est vide : aucune exception n'a été accordée à ce jour.
const EXCEPTIONS = {
  // 'Exemple.Paquet': 'MIT lue dans LICENSE le 20/08/2026, sans expression SPDX publiée.',
}

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

async function licenceNpm(nom) {
  const r = await fetch(`https://registry.npmjs.org/${encodeURIComponent(nom)}/latest`)
  if (!r.ok) return { licence: null, detail: `registre injoignable (HTTP ${r.status})` }
  const j = await r.json()
  const licence = typeof j.license === 'string' ? j.license : (j.license?.type ?? null)
  return { licence, detail: null }
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
  if (!recherche.ok) return { licence: null, detail: `registre injoignable (HTTP ${recherche.status})` }
  const d = ((await recherche.json()).data ?? [])[0]
  if (!d?.version) return { licence: null, detail: 'paquet introuvable sur nuget.org' }

  const feuille = await fetch(
    `https://api.nuget.org/v3/registration5-semver1/${encodeURIComponent(nom.toLowerCase())}/${encodeURIComponent(d.version)}.json`,
  )
  if (!feuille.ok) return { licence: null, detail: `enregistrement illisible (HTTP ${feuille.status})` }
  const { catalogEntry } = await feuille.json()
  if (typeof catalogEntry !== 'string') return { licence: null, detail: 'entrée de catalogue absente' }

  const catalogue = await fetch(catalogEntry)
  if (!catalogue.ok) return { licence: null, detail: `catalogue illisible (HTTP ${catalogue.status})` }
  const c = await catalogue.json()

  // Une expression SPDX absente signale une licence à lire à la main. C'est le
  // cas de MediatR (RPL-1.5) — mais AUSSI de Mediator.SourceGenerator, son
  // alternative MIT retenue par la décision D12 : les deux publient un fichier
  // de licence et aucune expression. Ce contrôle ne les distingue donc pas ; il
  // les arrête tous les deux et exige une lecture humaine. C'est le
  // comportement voulu, pas une limite tue.
  return {
    licence: c.licenseExpression ?? null,
    detail: c.licenseFile ? `fichier « ${c.licenseFile} » à lire` : null,
  }
}

function paquetsNpm() {
  const p = JSON.parse(readFileSync('front/package.json', 'utf8'))
  return Object.keys({ ...p.dependencies, ...p.devDependencies })
}

function paquetsNuget() {
  const noms = new Set()
  const parcourir = (d) => {
    for (const e of readdirSync(d)) {
      const chemin = join(d, e)
      if (statSync(chemin).isDirectory()) {
        if (e === 'bin' || e === 'obj' || e === 'node_modules') continue
        parcourir(chemin)
      } else if (e.endsWith('.csproj')) {
        const x = readFileSync(chemin, 'utf8')
        for (const m of x.matchAll(/PackageReference\s+Include="([^"]+)"/g)) noms.add(m[1])
      }
    }
  }
  parcourir('back')
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
  const { licence, detail } = source === 'npm' ? await licenceNpm(nom) : await licenceNuget(nom)
  if (!estPermise(licence)) {
    const dit = licence ?? `(licence non standard${detail ? ` — ${detail}` : ''})`
    refuses.push(`${source.padEnd(6)} ${nom.padEnd(50)} ${dit}`)
  }
}

if (refuses.length > 0) {
  console.error('Dépendances hors liste blanche :\n')
  for (const l of refuses) console.error('  ' + l)
  console.error(
    `\nListe blanche : ${PERMISES.join(', ')}.\n` +
      "Une licence non standard n'est pas forcément interdite — elle doit être lue avant d'être admise,\n" +
      "puis inscrite dans EXCEPTIONS avec son motif. Jamais ajoutée à la liste blanche.",
  )
  process.exit(1)
}

console.log(`${cibles.length} dépendances vérifiées, toutes sous licence permissive.`)
