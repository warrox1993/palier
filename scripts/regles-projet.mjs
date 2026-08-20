#!/usr/bin/env node
// Règles propres à ce projet, qu'aucun linter généraliste ne connaît.
// Remplace no-restricted-syntax, eslint-plugin-i18next et naming-convention,
// absents d'Oxlint. Aucune dépendance : on lit ce qu'on exécute.
import { existsSync, readFileSync, readdirSync, statSync } from 'node:fs'
import { join, extname, basename, dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

// Toutes les cibles se résolvent depuis la racine du dépôt, jamais depuis le
// répertoire courant. L'épreuve du harnais vit dans `front/` et appelle
// `node ../scripts/regles-projet.mjs` ; `verify` et le hook de pré-envoi
// l'appellent depuis la racine. Un chemin relatif au cwd aurait donné deux
// comportements pour la même commande — et l'épreuve aurait mesuré un fichier
// introuvable au lieu d'une violation refusée.
const RACINE = dirname(dirname(fileURLToPath(import.meta.url)))

// Codes de sortie : 0 conforme, 1 violations trouvées, 2 le contrôle n'a pas pu
// tourner. Le 2 doit rester distinct du 1 : sans lui, une arborescence déplacée
// ou un argument oublié sortait en 1 avec une pile Node, et `verify` annonçait
// « violation des règles de projet » alors qu'aucun fichier n'avait été lu.
const CODE_VIOLATIONS = 1
const CODE_USAGE = 2

function abandonner(message) {
  console.error(`regles-projet : ${message}`)
  console.error("Le contrôle n'a rien pu vérifier. Ce n'est PAS une violation de règle.")
  process.exit(CODE_USAGE)
}

// ---------------------------------------------------------------------------
// Masquage des commentaires
// ---------------------------------------------------------------------------

const AVANT_UNE_REGEX = new Set([
  '',
  '(',
  ',',
  '=',
  ':',
  '[',
  '!',
  '&',
  '|',
  '?',
  '{',
  '}',
  ';',
  '+',
  '-',
  '*',
  '%',
  '~',
  '^',
  '<',
  '>',
])
const MOTS_AVANT_UNE_REGEX = new Set([
  'return',
  'typeof',
  'instanceof',
  'in',
  'of',
  'new',
  'delete',
  'void',
  'do',
  'else',
  'yield',
  'await',
  'case',
])

/** Fin d'une chaîne ouverte en `debut`, ou -1 si elle ne se ferme pas avant la
 * fin de la ligne. Une apostrophe française — « L'utilisateur » dans un nœud de
 * texte JSX — n'ouvre pas une chaîne : sans ce garde-fou, le masqueur avalait
 * tout le reste du fichier et les commentaires qui suivaient n'étaient plus
 * neutralisés. */
function finDeChaine(src, debut, guillemet) {
  for (let i = debut + 1; i < src.length; i++) {
    if (src[i] === '\\') {
      i++
      continue
    }
    if (src[i] === '\n') return -1
    if (src[i] === guillemet) return i
  }
  return -1
}

/**
 * Remplace le contenu des commentaires par des espaces, en conservant la
 * longueur exacte du fichier et ses sauts de ligne : les décalages, donc les
 * numéros de ligne rapportés, restent justes.
 *
 * Les chaînes, les gabarits et les littéraux d'expression régulière sont
 * recopiés tels quels — sans quoi le `//` d'une URL ferait disparaître la fin
 * de sa ligne et la règle se tairait sur du vrai code.
 */
function masquerCommentaires(src) {
  let out = ''
  let i = 0
  let precedent = ''
  let mot = ''
  const n = src.length

  const avancer = (c) => {
    out += c
    if (/[\w$]/.test(c)) mot += c
    else mot = ''
    if (!/\s/.test(c)) precedent = c
  }

  while (i < n) {
    const c = src[i]
    const d = src[i + 1]

    if (c === '/' && d === '/') {
      while (i < n && src[i] !== '\n') {
        out += ' '
        i++
      }
      precedent = ''
      mot = ''
      continue
    }

    if (c === '/' && d === '*') {
      while (i < n && !(src[i] === '*' && src[i + 1] === '/')) {
        out += src[i] === '\n' ? '\n' : ' '
        i++
      }
      if (i < n) {
        out += '  '
        i += 2
      }
      precedent = ''
      mot = ''
      continue
    }

    if (c === '`') {
      out += c
      i++
      while (i < n) {
        if (src[i] === '\\') {
          out += src[i] + (src[i + 1] ?? '')
          i += 2
          continue
        }
        out += src[i]
        if (src[i] === '`') {
          i++
          break
        }
        i++
      }
      precedent = '`'
      mot = ''
      continue
    }

    if (c === '"' || c === "'") {
      const fin = finDeChaine(src, i, c)
      if (fin !== -1) {
        out += src.slice(i, fin + 1)
        i = fin + 1
        precedent = c
        mot = ''
        continue
      }
      avancer(c)
      i++
      continue
    }

    if (c === '/' && (AVANT_UNE_REGEX.has(precedent) || MOTS_AVANT_UNE_REGEX.has(mot))) {
      const debut = i
      let j = i + 1
      let dansClasse = false
      let ferme = false
      while (j < n) {
        if (src[j] === '\\') {
          j += 2
          continue
        }
        if (src[j] === '\n') break
        if (src[j] === '[') dansClasse = true
        else if (src[j] === ']') dansClasse = false
        else if (src[j] === '/' && !dansClasse) {
          ferme = true
          j++
          break
        }
        j++
      }
      if (ferme) {
        out += src.slice(debut, j)
        i = j
        precedent = '/'
        mot = ''
        continue
      }
    }

    avancer(c)
    i++
  }

  return out
}

// ---------------------------------------------------------------------------
// Règle « chaîne en dur » — analysée sur le fichier entier
// ---------------------------------------------------------------------------

/** Un mot d'au moins trois lettres : ce qui distingue de la copie d'un opérateur
 * ou d'une unité. */
const MOT_DE_COPIE = /[A-Za-zÀ-ÖØ-öø-ÿ]{3,}/

/** Caractères qui n'apparaissent pas dans de la copie destinée à l'écran, mais
 * bien dans du code. Filet de sécurité derrière l'ancrage sur `</`. */
const CODE_DANS_LE_TEXTE = /[=`"\\]/

/**
 * Nœud de texte JSX. L'ancrage tient en trois points :
 *  - le `<` de fermeture doit être suivi d'un `/` : dans une balise fermante,
 *    jamais dans une comparaison. C'est ce qui élimine `valeur < 10` ;
 *  - la classe `[^<>{}]` interdit toute autre balise ou accolade entre les
 *    deux, donc elle se raccroche au `>` de la balise ouvrante la plus proche
 *    et ignore `{t('cle')}`, seule forme correcte ;
 *  - elle traverse les sauts de ligne, donc le découpage de Prettier.
 */
const NOEUD_TEXTE_JSX = />([^<>{}]*)<\//g

/** Balise ouvrante, valeurs entre guillemets comprises, sur une ou plusieurs
 * lignes. Le groupe 1 est la zone des attributs ; le drapeau `d` en donne la
 * position, d'où se calcule le numéro de ligne. */
const BALISE_OUVRANTE = /<[A-Za-z][\w.:-]*((?:"[^"]*"|'[^']*'|[^<>'"])*)\/?>/dg

/** Attributs qui portent de la copie. Le `(?<![-.\w$])` écarte `data-title=`,
 * `document.title =` et tout suffixe d'un identifiant plus long. Une valeur
 * entre accolades — `title={t('cle')}` — n'est pas une chaîne littérale et ne
 * correspond donc pas. */
const ATTRIBUT_DE_COPIE = /(?<![-.\w$])(title|alt|placeholder|aria-label)\s*=\s*(['"])([^'"]*)\2/g

/** Chaîne littérale glissée dans une accolade JSX : `<span>{'Texte'}</span>`. */
const LITTERAL_DANS_JSX = />\s*\{\s*(['"])([^'"\n]*)\1\s*\}\s*<\//g

/**
 * Constante exportée dont la valeur est un littéral de texte :
 * `export const MESSAGE_VIDE = 'Aucune séance enregistrée'`.
 *
 * C'est le trou mesuré au lot 1 — D31 livrable 3. La règle ne regardait que le
 * JSX ; déplacer un libellé dans une constante exportée, ce qu'on fait dès
 * qu'on veut le réutiliser à deux endroits, le faisait disparaître du contrôle.
 * Le `d` donne la position du littéral, d'où se calcule le numéro de ligne.
 *
 * Seules les constantes EXPORTÉES sont vues : une constante privée peut porter
 * un identifiant technique — `const NOM_APPLICATION = 'palier'` dans App.tsx —
 * et le nom de la marque n'a aucune clé de traduction.
 */
const CONSTANTE_EXPORTEE =
  /export\s+const\s+([A-Za-z_$][\w$]*)\s*(?::[^=]+)?=\s*(['"`])((?:[^\\]|\\.)*?)\2/dg

function resume(texte) {
  const plat = texte.replace(/\s+/g, ' ').trim()
  return plat.length > 60 ? `${plat.slice(0, 57)}…` : plat
}

function chainesEnDur(contenu) {
  const trouvees = []

  for (const m of contenu.matchAll(NOEUD_TEXTE_JSX)) {
    const texte = m[1].trim()
    if (texte === '' || CODE_DANS_LE_TEXTE.test(texte) || !MOT_DE_COPIE.test(texte)) continue
    trouvees.push({ index: m.index, detail: `nœud de texte JSX « ${resume(texte)} »` })
  }

  for (const balise of contenu.matchAll(BALISE_OUVRANTE)) {
    const attributs = balise[1]
    if (attributs === undefined || attributs === '') continue
    const debut = balise.indices[1][0]
    for (const a of attributs.matchAll(ATTRIBUT_DE_COPIE)) {
      if (!MOT_DE_COPIE.test(a[3])) continue
      trouvees.push({ index: debut + a.index, detail: `attribut ${a[1]}="${resume(a[3])}"` })
    }
  }

  for (const m of contenu.matchAll(LITTERAL_DANS_JSX)) {
    if (!MOT_DE_COPIE.test(m[2])) continue
    trouvees.push({ index: m.index, detail: `littéral dans une accolade JSX « ${resume(m[2])} »` })
  }

  for (const m of contenu.matchAll(CONSTANTE_EXPORTEE)) {
    const texte = m[3]
    if (texte === '' || CODE_DANS_LE_TEXTE.test(texte) || !MOT_DE_COPIE.test(texte)) continue
    trouvees.push({
      index: m.indices[3][0],
      detail: `constante exportée ${m[1]} = « ${resume(texte)} »`,
    })
  }

  return trouvees.sort((a, b) => a.index - b.index)
}

// ---------------------------------------------------------------------------
// Les règles
// ---------------------------------------------------------------------------

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
    message: 'Aucune flèche Unicode dans un libellé : utiliser une icône, ou rien.',
  },
  {
    id: 'chaine-en-dur',
    // Analysée sur le fichier entier, jamais ligne à ligne : Prettier coupe tout
    // composant réel en plusieurs lignes (printWidth 100), et un motif appliqué
    // à chaque ligne prise isolément ne voit plus rien du tout.
    portee: 'fichier',
    chercher: chainesEnDur,
    // `.ts` autant que `.tsx` depuis le lot 2 : le détecteur des constantes
    // exportées vise précisément les fichiers sans JSX, où le libellé se range
    // quand on le sort d'un composant.
    extensions: ['.ts', '.tsx'],
    // Faux positif MESURÉ le 20/08/2026, et un seul : `export const courbe =
    // 'cubic-bezier(0.25, 0.46, 0.45, 0.94)'`. C'est une valeur technique, pas
    // de la copie, et aucune clé i18next ne lui correspondra jamais. La borne
    // est posée ICI — dans le script, comme celle de `couleur-hors-jetons` —
    // et jamais par une clé ajoutée à un fichier de configuration d'outil
    // (D21). Le fichier ne porte aucun JSX : l'exclusion ne coûte rien aux
    // trois autres détecteurs.
    exclure: [/src[\\/]ui[\\/]jetons\./],
    message:
      'Aucune chaîne de texte en dur : tout passe par i18next, y compris les erreurs et les états vides — CLAUDE.md § 4.',
  },
  {
    id: 'booleen-mal-nomme',
    motif: /\b(?:const|let)\s+(?!is|has|can|should)[a-z]\w*\s*:\s*boolean\b/,
    extensions: ['.ts', '.tsx'],
    message: 'Un booléen se préfixe par is, has, can ou should — docs/16-projet.md § 2.',
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
  const depart = resolve(RACINE, racine)
  if (!existsSync(depart)) {
    abandonner(
      `répertoire introuvable : ${depart}. L'arborescence attendue par ce contrôle a bougé — ` +
        'corriger les cibles du script, ou le lancer depuis un dépôt complet.',
    )
  }
  const out = []
  const parcourir = (d) => {
    for (const e of readdirSync(d)) {
      if (IGNORES.includes(e)) continue
      const p = join(d, e)
      if (statSync(p).isDirectory()) parcourir(p)
      else out.push(p)
    }
  }
  parcourir(depart)
  return out
}

// ---------------------------------------------------------------------------
// Contrôle transverse : toute source de données est attribuée
// ---------------------------------------------------------------------------

/** Le catalogue d'attribution. Sa disparition est une PANNE du contrôle, pas
 * une absence de violation — d'où le CODE_USAGE et non le silence. */
const CATALOGUE = 'db/SOURCES.md'
const REFERENTIEL = 'db/referentiel'

/**
 * Cette règle ne rentre pas dans `REGLES` : les autres cherchent un motif dans
 * un fichier, celle-ci COMPARE DEUX FICHIERS. Elle s'exécute après la boucle
 * par fichier et alimente le même tableau `violations` — un seul point de
 * sortie, un seul format de message.
 *
 * `--source <chemin>` la lance sur une cible unique, ce dont l'épreuve du
 * harnais a besoin : sa fixture vit dans `front/tests/harness/fixtures/`, hors
 * du parcours, parce qu'une fixture posée dans `db/referentiel/` ferait échouer
 * `npm run regles` sur le dépôt réel. **L'argument explicite l'emporte donc sur
 * le périmètre du parcours** — c'est la réponse mesurée à la question de D20
 * pour cet outil, et c'est la forme voulue : une exclusion qui survit à
 * l'argument rend la fixture invisible pour sa propre épreuve.
 */
function sourcesNonAttribuees(sourceUnique) {
  const catalogue = resolve(RACINE, CATALOGUE)
  if (!existsSync(catalogue)) {
    abandonner(
      `catalogue introuvable : ${catalogue}. Toute source de ${REFERENTIEL}/ doit y porter ` +
        'une ligne — nom, source, licence, millésime, date, URL. Sans lui, ce contrôle ' +
        "n'a plus de cible et ne peut donc rien conclure.",
    )
  }
  const attribution = readFileSync(catalogue, 'utf8')
  // Le contrôle ne lit pas la source : il compare son NOM au catalogue. Sans
  // cette garde, `--source db/referentiel/absent.sql` rendait une violation
  // pour un fichier qui n'existe pas — un refus sincère et faux, la forme
  // inverse du faux vert (ruling P22). Mesuré le 20/08/2026 en lançant la
  // commande de l'étape 5 du brief telle qu'elle y est écrite.
  if (sourceUnique !== null && !existsSync(sourceUnique)) {
    abandonner(
      `source introuvable : ${sourceUnique}. --source nomme un fichier existant ; ` +
        "un chemin mort n'est pas une violation d'attribution.",
    )
  }
  const sources =
    sourceUnique !== null
      ? [sourceUnique]
      : fichiers(REFERENTIEL).filter((f) => extname(f).toLowerCase() === '.sql')

  const out = []
  for (const f of sources) {
    const nom = basename(f)
    if (attribution.includes(nom)) continue
    out.push({
      fichier: f,
      ligne: 1,
      regle: 'source-non-attribuee',
      detail: `« ${nom} » n'a aucune ligne dans ${CATALOGUE}`,
      message:
        `Toute source de données porte son attribution : ajouter une ligne pour ${nom} dans ` +
        `${CATALOGUE} — source, licence, version, date de relevé, URL. ` +
        'docs/17-donnees-sources.md interdit de fusionner des sources aux licences ' +
        "différentes ; une source non tracée ne peut plus s'en séparer.",
    })
  }
  return { violations: out, nombre: sources.length }
}

const iFichier = process.argv.indexOf('--fichier')
if (iFichier !== -1 && process.argv[iFichier + 1] === undefined) {
  abandonner('option --fichier employée sans valeur. Usage : --fichier <chemin depuis la racine>.')
}
const iSource = process.argv.indexOf('--source')
if (iSource !== -1 && process.argv[iSource + 1] === undefined) {
  abandonner('option --source employée sans valeur. Usage : --source <chemin depuis la racine>.')
}
const sourceUnique = iSource !== -1 ? resolve(RACINE, process.argv[iSource + 1]) : null

// `--source` vise le contrôle transverse seul : la boucle par fichier n'a rien
// à dire d'un `.sql`, et lui faire parcourir tout le front rendrait l'épreuve
// dépendante de l'état du front.
const cibles =
  sourceUnique !== null
    ? []
    : iFichier !== -1
      ? [resolve(RACINE, process.argv[iFichier + 1])]
      : fichiers('front/src').concat(fichiers('front/tests').filter((f) => !f.includes('fixtures')))

function indexDesLignes(contenu) {
  const debuts = [0]
  for (let i = 0; i < contenu.length; i++) if (contenu[i] === '\n') debuts.push(i + 1)
  return debuts
}

function ligneDe(debuts, index) {
  let bas = 0
  let haut = debuts.length - 1
  while (bas < haut) {
    const milieu = Math.ceil((bas + haut) / 2)
    if (debuts[milieu] <= index) bas = milieu
    else haut = milieu - 1
  }
  return bas + 1
}

const violations = []
for (const f of cibles) {
  const ext = extname(f)
  let brut
  try {
    brut = readFileSync(f, 'utf8')
  } catch {
    abandonner(`fichier illisible : ${f}`)
  }
  // Les commentaires ne s'affichent jamais : ils sont neutralisés avant toute
  // recherche, sur les règles de ligne comme sur celles du fichier entier.
  const contenu = masquerCommentaires(brut)
  const debuts = indexDesLignes(contenu)
  const lignes = contenu.split('\n')

  for (const regle of REGLES) {
    if (!regle.extensions.includes(ext)) continue
    if (regle.exclure?.some((r) => r.test(f))) continue

    if (regle.portee === 'fichier') {
      for (const t of regle.chercher(contenu)) {
        violations.push({
          fichier: f,
          ligne: ligneDe(debuts, t.index),
          regle: regle.id,
          detail: t.detail,
          message: regle.message,
        })
      }
      continue
    }

    lignes.forEach((ligne, i) => {
      if (regle.motif.test(ligne)) {
        violations.push({ fichier: f, ligne: i + 1, regle: regle.id, message: regle.message })
      }
    })
  }
}

// Le contrôle transverse tourne APRÈS la boucle par fichier, et jamais sous
// `--fichier`, qui vise une règle de contenu sur une cible unique.
let nbSources = 0
if (iFichier === -1) {
  const attribution = sourcesNonAttribuees(sourceUnique)
  nbSources = attribution.nombre
  violations.push(...attribution.violations)
}

if (violations.length > 0) {
  console.error(`${violations.length} violation(s) des règles de projet :\n`)
  for (const v of violations) {
    console.error(`  ${v.fichier}:${v.ligne}  [${v.regle}]${v.detail ? ` ${v.detail}` : ''}`)
    console.error(`    ${v.message}\n`)
  }
  process.exit(CODE_VIOLATIONS)
}

console.log(
  `${cibles.length + nbSources} fichiers vérifiés, aucune violation des règles de projet.`,
)
