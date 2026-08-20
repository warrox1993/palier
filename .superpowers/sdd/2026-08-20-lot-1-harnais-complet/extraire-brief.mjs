// Extrait le brief d'une tâche depuis le plan unifié.
// Remplace scripts/task-brief, qui ne reconnaît que les titres « Task N » en anglais.
// Usage : node .superpowers/sdd/<plan>/extraire-brief.mjs <N>
import fs from 'node:fs'

const PLAN = 'docs/superpowers/plans/2026-08-20-lot-1-harnais-complet.md'
const W = '.superpowers/sdd/2026-08-20-lot-1-harnais-complet'

const plan = fs.readFileSync(PLAN, 'utf8')

// Sans argument : extrait toutes les tâches du plan.
const numeros = process.argv[2]
  ? [Number(process.argv[2])]
  : [...plan.matchAll(/^## Tâche (\d+) :/gm)].map((m) => Number(m[1]))

if (numeros.some((x) => !Number.isInteger(x) || x < 1)) {
  console.error('Usage : node extraire-brief.mjs [numéro de tâche]')
  process.exit(1)
}

for (const n of numeros) extraire(n)

function extraire(n) {

const contraintes = plan.match(/## Contraintes globales\n([\s\S]*?)\n---\n/)
if (!contraintes) {
  console.error('Section « Contraintes globales » introuvable')
  process.exit(1)
}

const debut = plan.indexOf(`## Tâche ${n} :`)
if (debut === -1) {
  console.error(`Tâche ${n} introuvable`)
  process.exit(1)
}
const suivante = plan.indexOf(`## Tâche ${n + 1} :`, debut)
const fin = suivante === -1 ? plan.indexOf('\n## Auto-revue', debut) : suivante
const corps = plan.slice(debut, fin === -1 ? plan.length : fin).trimEnd()

const brief = [
  `# Brief — Tâche ${n}`,
  '',
  `> Extrait de \`${PLAN}\`. C'est la source unique de tes exigences.`,
  '> Les valeurs exactes — code, chemins, chaînes de caractères — se reprennent **verbatim**.',
  '',
  '## Contraintes globales',
  '',
  contraintes[1].trim(),
  '',
  '---',
  '',
  corps,
  '',
].join('\n')

const chemin = `${W}/task-${n}-brief.md`
fs.writeFileSync(chemin, brief)

const etapes = (corps.match(/^- \[ \]/gm) ?? []).length
  if (etapes === 0) {
    console.log(`task-${n} — 0 étape (tâche déjà livrée, pas de brief à produire)`)
    return
  }
  console.log(`task-${n} — ${etapes} étapes`)
}

// ── Régénération ────────────────────────────────────────────────────────────
// Sans argument, ce script réécrit TOUS les briefs. C'est le mode à privilégier
// après toute correction du plan : le ruling P7 s'est produit deux fois, parce
// qu'un brief déjà extrait garde la valeur périmée que le plan vient de perdre.
// Un implémenteur lit son brief, pas le plan — la correction qui ne l'atteint
// pas n'existe pas.
