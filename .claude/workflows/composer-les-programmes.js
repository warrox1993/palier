export const meta = {
  name: 'composer-les-programmes',
  description:
    'Compose des programmes exploitables à partir des 153 méthodes retenues, avec les exercices réels du catalogue',
  phases: [
    { title: 'Composer', detail: 'un agent par famille de méthodes' },
    {
      title: 'Verifier',
      detail: 'chaque programme contesté : slugs, contre-indications, sécurité',
    },
    { title: 'Bilan', detail: "ce qui est composable et ce qui ne l'est pas" },
  ],
}

const SCRATCH =
  'C:/Users/jeanb/AppData/Local/Temp/claude/C--Users-jeanb-Desktop-AppMuscu/edb197c5-b929-4192-abc4-cbd28d5ecb1f/scratchpad'

const CADRE = `
TU COMPOSES DES PROGRAMMES POUR UNE APPLICATION DONT LA PROMESSE EST
D'EVITER LES EXCES ET LES BLESSURES — pas d'optimiser la performance. Le public
inclut des gens qui reprennent apres une blessure, une grossesse, une prothese.

=== LE CATALOGUE D'EXERCICES ===
Lis-le AVANT toute composition :
  cat "${SCRATCH}/catalogue.txt"

255 lignes au format : slug | nom | materiel | muscles | ci:contre-indications

**TU N'UTILISES QUE DES SLUGS DE CE FICHIER.** Un slug invente produit une
seance amputee en base, sans la moindre erreur — la jointure ne trouve rien et
n'insere rien. Verifie chaque slug par grep avant de l'ecrire.

=== LES CIBLES NE S'INVENTENT PAS ===
docs/05-entrainement.md § 1 les fixe :
  Intensite    | 0 a 3 RIR en regime normal, 3 a 4 EN REPRISE
  Force        | 3 a 6 repetitions, 180 a 300 s de repos
  Hypertrophie | 6 a 15 repetitions, 90 a 180 s de repos
  Endurance    | 15 a 30 repetitions, repos court, en fin de seance
  Volume       | 10 a 20 series dures par groupe et par semaine
  Frequence    | chaque muscle stimule 2 fois par semaine minimum

Bornes du schema, refusees par le moteur au-dela :
  series 1-20 | repetitions 1-100 | RIR 0-10 | repos 0-900 s
  frequence 1-7 | au plus 14 seances | au plus 30 exercices par seance

=== CE QUE TU NE FAIS JAMAIS ===
  - copier la sequence d'un programme protege : tu appliques ses PRINCIPES
  - nommer une pathologie, annoncer un effet therapeutique, poser une posologie
  - proposer un format chronometre a score, un test maximal, un travail a l'echec
  - inventer un chiffre de litterature dans une note
  - mettre un exercice contre-indique pour la contrainte que le programme menage

=== LES NOTES ===
Elles s'adressent a L'UTILISATEUR, pas au developpeur. Jamais de reference a un
fichier du depot. Elles expliquent POURQUOI ces choix — c'est une obligation :
« un utilisateur qui comprend pourquoi un exercice est absent l'accepte ; sinon
il le rajoute et se blesse ».

Quand une methode porte un nom d'auteur ou d'ecole, tu peux le CITER comme
origine — c'est un fait. Tu ne reprends ni son texte, ni sa mise en forme, ni sa
sequence exacte.

=== TON JUGEMENT COMPTE ===
Beaucoup de ces methodes NE SONT PAS des programmes : ce sont des procedures,
des doctrines, des modeles conceptuels, des protocoles cliniques exigeant un
diagnostic. **Ne compose que ce qui se tient debout comme programme autonome
pour ce public.** Ecarter est une reponse valable et attendue — dis pourquoi.
`

const SCHEMA = {
  type: 'object',
  properties: {
    programmes: {
      type: 'array',
      items: {
        type: 'object',
        properties: {
          slug: { type: 'string', description: 'kebab-case, sans accent, unique' },
          nom_fr: { type: 'string' },
          nom_en: { type: 'string' },
          description_fr: { type: 'string', description: 'a qui il s adresse, 1 a 2 phrases' },
          description_en: { type: 'string' },
          notes_fr: {
            type: 'string',
            description: 'POURQUOI ces choix. Plusieurs paragraphes separes par \\n\\n',
          },
          notes_en: { type: 'string' },
          origine: {
            type: 'string',
            description: 'la methode dont il s inspire, auteur et date si connus',
          },
          frequence_min: { type: 'integer', minimum: 1, maximum: 7 },
          frequence_max: { type: 'integer', minimum: 1, maximum: 7 },
          contrainte_visee: {
            type: ['string', 'null'],
            description: 'cervicale, lombaire, epaule, genou, hanche, poignet, cheville, ou null',
          },
          jours: {
            type: 'array',
            items: {
              type: 'object',
              properties: {
                libelle_fr: { type: 'string' },
                libelle_en: { type: 'string' },
                exercices: {
                  type: 'array',
                  items: {
                    type: 'object',
                    properties: {
                      slug: { type: 'string', description: 'DOIT exister dans catalogue.txt' },
                      series: { type: 'integer', minimum: 1, maximum: 20 },
                      reps_min: { type: 'integer', minimum: 1, maximum: 100 },
                      reps_max: { type: ['integer', 'null'] },
                      rir: { type: 'integer', minimum: 0, maximum: 10 },
                      repos: { type: 'integer', minimum: 0, maximum: 900 },
                      note_fr: { type: ['string', 'null'] },
                      note_en: { type: ['string', 'null'] },
                    },
                    required: ['slug', 'series', 'reps_min', 'reps_max', 'rir', 'repos'],
                  },
                },
              },
              required: ['libelle_fr', 'libelle_en', 'exercices'],
            },
          },
        },
        required: [
          'slug',
          'nom_fr',
          'nom_en',
          'description_fr',
          'description_en',
          'notes_fr',
          'notes_en',
          'origine',
          'frequence_min',
          'frequence_max',
          'contrainte_visee',
          'jours',
        ],
      },
    },
    ecartees: {
      type: 'array',
      items: {
        type: 'object',
        properties: {
          nom: { type: 'string' },
          motif: { type: 'string' },
        },
        required: ['nom', 'motif'],
      },
    },
  },
  required: ['programmes', 'ecartees'],
}

const FAMILLES = [
  'Halterophilie',
  'Renforcement et rehabilitation',
  'Conditionnement metabolique',
  'Bodybuilding et hypertrophie',
  'Force et powerlifting',
  'Traditions nationales et historiques',
  'Preparation physique des sports de combat',
  'Kettlebell',
  'Mobilite et qualite de mouvement',
  'Entrainement militaire et tactique',
  'Endurance et cardio',
  'Minimalisme et faible frequence',
  'Poids de corps',
  'Populations specifiques',
]

phase('Composer')

const lots = await parallel(
  FAMILLES.map(
    (f, rang) => () =>
      agent(
        `${CADRE}

=== TA FAMILLE : ${f} ===

Les methodes de ta famille sont dans un fichier JSON. Lis-les :

  node -e "const l=require('${SCRATCH}/lots.json');const k=Object.keys(l)[${rang}];console.log(k);console.log(JSON.stringify(l[k],null,1))"

Si ce lot te parait vide ou illisible, liste les cles disponibles et prends
celle qui correspond a « ${f} » :

  node -e "console.log(Object.keys(require('${SCRATCH}/lots.json')))"

COMPOSE un programme par methode qui le merite. Vise la QUALITE plutot que le
nombre : un programme bien construit vaut mieux que trois approximatifs.

Pour chacun, la note doit dire :
  - de quelle methode il s'inspire, et ce que cette methode apporte
  - pourquoi CES exercices et pas d'autres
  - ce qui a ete retire de la methode d'origine pour ce public, et pourquoi
  - ce que l'utilisateur doit savoir avant de commencer

Les slugs des programmes doivent etre UNIQUES et ne pas entrer en collision avec
ceux qui existent deja : une-seance, deux-seances, reprise, reprise-cervicale,
reprise-lombaire, epaule-menagee, genou-menage, full-body, upper-lower, ppl,
split.`,
        { label: `compose:${f.slice(0, 22)}`, phase: 'Composer', schema: SCHEMA },
      ),
  ),
)

phase('Verifier')

const SCHEMA_VERDICT = {
  type: 'object',
  properties: {
    verdicts: {
      type: 'array',
      items: {
        type: 'object',
        properties: {
          slug: { type: 'string' },
          verdict: { type: 'string', enum: ['garder', 'corriger', 'rejeter'] },
          slugsInexistants: { type: 'array', items: { type: 'string' } },
          contreIndicationsViolees: { type: 'array', items: { type: 'string' } },
          problemes: { type: 'array', items: { type: 'string' } },
          motif: { type: 'string' },
        },
        required: [
          'slug',
          'verdict',
          'slugsInexistants',
          'contreIndicationsViolees',
          'problemes',
          'motif',
        ],
      },
    },
  },
  required: ['verdicts'],
}

const valides = lots.filter(Boolean)

const verdicts = await parallel(
  valides.map(
    (lot, rang) => () =>
      agent(
        `${CADRE}

TU ES LE VERIFICATEUR. Voici des programmes composes par un autre agent :

${JSON.stringify(lot.programmes || [])}

CONTESTE-LES. Verifie MECANIQUEMENT, par commande, pas au jugement :

1. **CHAQUE SLUG EXISTE-T-IL ?** Pour chaque exercice :
     grep -c "^SLUG |" "${SCRATCH}/catalogue.txt"
   Un slug absent est une faute BLOQUANTE : il produirait une seance amputee en
   base sans la moindre erreur.

2. **CONTRE-INDICATIONS.** Si le programme porte une contrainte_visee, aucun de
   ses exercices ne doit la porter dans sa colonne « ci: ». Verifie par grep.

3. **BORNES.** series 1-20, repetitions 1-100 avec max >= min, RIR 0-10,
   repos 0-900, frequence 1-7, au plus 14 seances, 30 exercices par seance.

4. **COHERENCE avec la frequence annoncee.** Un programme a 3 seances doit avoir
   3 jours, ou dire pourquoi il en a moins.

5. **LA PROMESSE.** Rien qui pousse a l'exces : pas de test maximal, pas de
   format chronometre a score, pas de travail a l'echec systematique, pas de
   charge portee, pas de privation de recuperation.

6. **LES NOTES.** Aucune reference a un fichier du depot. Aucun chiffre de
   litterature invente. Aucune promesse therapeutique.

Verdict « rejeter » si le programme n'est pas rattrapable, « corriger » si un
detail cloche, « garder » sinon. Sois SEVERE : un programme douteux qui passe
finit devant quelqu'un qui reprend apres une blessure.`,
        { label: `verifie:${rang}`, phase: 'Verifier', schema: SCHEMA_VERDICT },
      ),
  ),
)

phase('Bilan')

const tousLesProgrammes = valides.flatMap((l) => l.programmes || [])
const tousLesVerdicts = verdicts.filter(Boolean).flatMap((v) => v.verdicts || [])
const rejetes = new Set(tousLesVerdicts.filter((v) => v.verdict === 'rejeter').map((v) => v.slug))
const aCorriger = tousLesVerdicts.filter((v) => v.verdict === 'corriger')

log(
  `composes: ${tousLesProgrammes.length} | rejetes: ${rejetes.size} | a corriger: ${aCorriger.length}`,
)

return {
  programmes: tousLesProgrammes,
  verdicts: tousLesVerdicts,
  ecartees: valides.flatMap((l) => l.ecartees || []),
  compte: {
    composes: tousLesProgrammes.length,
    rejetes: rejetes.size,
    aCorriger: aCorriger.length,
  },
}
