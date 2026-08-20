#!/usr/bin/env node
// Règles propres à ce projet, qu'aucun linter généraliste ne connaît.
// Remplace no-restricted-syntax, eslint-plugin-i18next et naming-convention,
// absents d'Oxlint. Aucune dépendance : on lit ce qu'on exécute.
import { readFileSync, readdirSync, statSync } from 'node:fs'
import { join, extname, dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

// Toutes les cibles se résolvent depuis la racine du dépôt, jamais depuis le
// répertoire courant. L'épreuve du harnais vit dans `front/` et appelle
// `node ../scripts/regles-projet.mjs` ; `verify` et le hook de pré-envoi
// l'appellent depuis la racine. Un chemin relatif au cwd aurait donné deux
// comportements pour la même commande — et l'épreuve aurait mesuré un fichier
// introuvable au lieu d'une violation refusée.
const RACINE = dirname(dirname(fileURLToPath(import.meta.url)))

const REGLES = [
  {
    id: 'calcul-de-conformite',
    motif:
      /\b(mifflin|katchMcArdle|tdeeAdaptatif|metabolismeDeBase|limiteHauteEfsa|plancherCalorique|plancherProteique|epleyCorrige)\w*/i,
    extensions: ['.ts', '.tsx'],
    message:
      'Les calculs de conformité vivent dans Palier.Domain, jamais dans le front. ' +
      "Le front affiche ce que l'API a calculé ; hors ligne, la dernière valeur connue avec son horodatage.",
  },
  {
    id: 'couleur-hors-jetons',
    motif: /#[0-9a-fA-F]{3}(?:[0-9a-fA-F]{3})?\b/,
    extensions: ['.ts', '.tsx', '.css'],
    exclure: [/src[\\/]ui[\\/]jetons\./],
    message: 'Aucune couleur hors des jetons de src/ui/jetons.ts — docs/02-design.md § 9.',
  },
  {
    id: 'ombre-ou-degrade',
    motif:
      /\b(box-shadow|drop-shadow|backdrop-blur|backdrop-filter|linear-gradient|radial-gradient)\b/,
    extensions: ['.ts', '.tsx', '.css'],
    message: 'Aucune ombre portée, aucun flou, aucun dégradé — docs/02-design.md § 2 et § 4.',
  },
  {
    id: 'fleche-unicode',
    motif: /["'`][^"'`]*[→←↑↓][^"'`]*["'`]/,
    extensions: ['.ts', '.tsx'],
    message: "Aucune flèche Unicode dans un libellé : utiliser une icône, ou rien.",
  },
  {
    id: 'chaine-en-dur',
    // Texte visible entre balises JSX : > Bonjour <
    motif: />\s*[A-Za-zÀ-ÿ][A-Za-zÀ-ÿ ,.'’!?-]{2,}\s*</,
    extensions: ['.tsx'],
    message:
      'Aucune chaîne de texte en dur : tout passe par i18next, y compris les erreurs et les états vides — CLAUDE.md § 4.',
  },
  {
    id: 'booleen-mal-nomme',
    motif: /\b(?:const|let)\s+(?!is|has|can|should)[a-z]\w*\s*:\s*boolean\b/,
    extensions: ['.ts', '.tsx'],
    message: "Un booléen se préfixe par is, has, can ou should — docs/16-projet.md § 2.",
  },
  {
    id: 'type-prefixe-i',
    motif: /\b(?:interface|type)\s+I[A-Z]\w*/,
    extensions: ['.ts', '.tsx'],
    message: 'Préfixe I interdit sur les types — docs/16-projet.md § 2.',
  },
]

const IGNORES = ['node_modules', 'dist', 'coverage', '.git', 'bin', 'obj', 'playwright-report']

function fichiers(racine) {
  const out = []
  const parcourir = (d) => {
    for (const e of readdirSync(d)) {
      if (IGNORES.includes(e)) continue
      const p = join(d, e)
      if (statSync(p).isDirectory()) parcourir(p)
      else out.push(p)
    }
  }
  parcourir(resolve(RACINE, racine))
  return out
}

const iFichier = process.argv.indexOf('--fichier')
const cibles =
  iFichier !== -1
    ? [resolve(RACINE, process.argv[iFichier + 1])]
    : fichiers('front/src').concat(fichiers('front/tests').filter((f) => !f.includes('fixtures')))

const violations = []
for (const f of cibles) {
  const ext = extname(f)
  let contenu
  try {
    contenu = readFileSync(f, 'utf8')
  } catch {
    console.error(`Fichier illisible : ${f}`)
    process.exit(2)
  }
  const lignes = contenu.split('\n')
  for (const regle of REGLES) {
    if (!regle.extensions.includes(ext)) continue
    if (regle.exclure?.some((r) => r.test(f))) continue
    lignes.forEach((ligne, i) => {
      if (ligne.trimStart().startsWith('//')) return
      if (regle.motif.test(ligne)) {
        violations.push({ fichier: f, ligne: i + 1, regle: regle.id, message: regle.message })
      }
    })
  }
}

if (violations.length > 0) {
  console.error(`${violations.length} violation(s) des règles de projet :\n`)
  for (const v of violations) {
    console.error(`  ${v.fichier}:${v.ligne}  [${v.regle}]`)
    console.error(`    ${v.message}\n`)
  }
  process.exit(1)
}

console.log(`${cibles.length} fichiers vérifiés, aucune violation des règles de projet.`)
