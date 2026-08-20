// Extrait le brief d'une tâche depuis le plan.
// Remplace scripts/task-brief, qui ne reconnaît que les titres « Task N » en anglais.
// Usage : node extraire-brief.mjs <N>
import fs from 'node:fs'

const PLAN = 'docs/superpowers/plans/2026-08-19-lot-1-harnais.md'
const W = '.superpowers/sdd/2026-08-19-lot-1-harnais'

const n = Number(process.argv[2])
if (!Number.isInteger(n) || n < 1) {
  console.error('Usage : node extraire-brief.mjs <numéro de tâche>')
  process.exit(1)
}

const plan = fs.readFileSync(PLAN, 'utf8')

const contraintes = plan.match(/## Contraintes globales\n([\s\S]*?)\n---\n/)
if (!contraintes) {
  console.error('Section « Contraintes globales » introuvable')
  process.exit(1)
}

const debut = plan.indexOf(`## Tâche ${n} :`)
if (debut === -1) {
  console.error(`Tâche ${n} introuvable dans le plan`)
  process.exit(1)
}
const suivante = plan.indexOf(`## Tâche ${n + 1} :`, debut)
const fin = suivante === -1 ? plan.indexOf('\n## Auto-revue du plan', debut) : suivante
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
  console.error(`ATTENTION : aucune étape extraite pour la tâche ${n}`)
  process.exit(1)
}
console.log(`${chemin} — ${brief.split('\n').length} lignes, ${etapes} étapes`)
