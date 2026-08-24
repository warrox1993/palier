#!/usr/bin/env node
// Applique les fichiers de `db/referentiel/` — les DONNÉES DE PRODUCTION que
// `docs/16-projet.md` § 4 distingue de la démonstration : valeurs EFSA,
// catalogue d'exercices, programmes modèles.
//
// POURQUOI CE SCRIPT N'EST PAS UNE MIGRATION. Le référentiel n'est pas du
// schéma. Corriger une faute dans une consigne d'exécution ne doit pas produire
// une migration versionnée que tout déploiement rejouera pour l'éternité ;
// `16-projet.md` sépare d'ailleurs les deux physiquement, dans deux dossiers.
// Le mécanisme suit cette séparation.
//
// POURQUOI IL PASSE PAR `palier_migrations`. Les tables portent
// `FORCE ROW LEVEL SECURITY` : sous `FORCE`, le propriétaire est LUI AUSSI
// soumis aux politiques — c'est le point que `db/README.md` souligne. Le
// chargement emprunte donc la politique `migrations_referentiel`, nominative et
// explicite, jamais un `BYPASSRLS` ni un `NO FORCE` temporaire, qui sont les
// deux façons d'éteindre RLS sans que rien ne le signale.
//
// CE SCRIPT NE CONTRÔLE PAS L'ATTRIBUTION. C'est le travail de la règle
// `source-non-attribuee` de `regles-projet.mjs`, qui refuse le dépôt si un
// `*.sql` de `referentiel/` n'a pas sa ligne dans `db/SOURCES.md`. Deux
// contrôles au même endroit se recopieraient l'un l'autre et divergeraient.
import { spawnSync } from 'node:child_process'
import { readdirSync, readFileSync, existsSync } from 'node:fs'
import { dirname, resolve, join } from 'node:path'
import { fileURLToPath } from 'node:url'

const RACINE = dirname(dirname(fileURLToPath(import.meta.url)))
const REFERENTIEL = resolve(RACINE, 'db', 'referentiel')

const CODE_USAGE = 2
const CODE_MOTEUR = 3
const CODE_SQL = 4

const CONTENEUR = process.env.PALIER_DB_CONTENEUR ?? 'palier-db'
const BASE = process.env.PALIER_DB_BASE ?? 'palier'
const ROLE = 'palier_migrations'

function abandonner(code, message) {
  console.error(`\n${message}\n`)
  process.exit(code)
}

if (!existsSync(REFERENTIEL)) {
  abandonner(
    CODE_USAGE,
    `Dossier introuvable : ${REFERENTIEL}\n` +
      'Le référentiel est la cible de ce script ; une cible absente se signale au lieu de se remplacer.',
  )
}

// Triés par NOM, donc par le préfixe numérique des fichiers. L'ordre compte :
// `04-programs.sql` référencera un jour des exercices que `02-exercises.sql`
// pose. Se fier à l'ordre de `readdir` reviendrait à dépendre du système de
// fichiers.
const fichiers = readdirSync(REFERENTIEL)
  .filter((nom) => nom.endsWith('.sql'))
  .sort()

if (fichiers.length === 0) {
  console.log(
    '\nAucun fichier de référentiel à appliquer.\n' +
      'Ce n’est pas une erreur : les données arrivent avec le cas d’usage qui les exige (D39).\n',
  )
  process.exit(0)
}

// `docker exec` plutôt qu'un client PostgreSQL local : `psql` n'est pas garanti
// installé sur un poste de développement, alors qu'il est toujours dans
// l'image. Le déploiement du lot 9 appliquera ces mêmes fichiers autrement,
// avec la chaîne de connexion de production — le CONTENU est le contrat, pas le
// transport.
for (const nom of fichiers) {
  const chemin = join(REFERENTIEL, nom)
  process.stdout.write(`  ${nom} … `)

  const resultat = spawnSync(
    'docker',
    [
      'exec',
      '-i',
      CONTENEUR,
      'psql',
      // SANS LUI, `psql` continue après une erreur et sort en SUCCÈS : le
      // référentiel serait partiellement appliqué, et le script dirait que
      // tout va bien. C'est le faux vert que ce dépôt refuse partout.
      '-v',
      'ON_ERROR_STOP=1',
      '--quiet',
      '-U',
      ROLE,
      '-d',
      BASE,
    ],
    { input: readFileSync(chemin), encoding: 'buffer' },
  )

  if (resultat.error?.code === 'ENOENT') {
    process.stdout.write('\n')
    abandonner(
      CODE_MOTEUR,
      'La commande `docker` est introuvable.\n' +
        'Docker Desktop doit être installé ET démarré : ce script parle au conteneur, pas à un ' +
        'PostgreSQL local.',
    )
  }

  if (resultat.status !== 0) {
    process.stdout.write('ÉCHEC\n')
    abandonner(
      CODE_SQL,
      `Le fichier ${nom} a été refusé par le moteur.\n\n` +
        `${resultat.stderr?.toString('utf8').trim() ?? ''}\n\n` +
        `Le conteneur « ${CONTENEUR} » doit tourner (\`npm run db:up\`) et le schéma être à jour ` +
        '(`dotnet ef database update`). Rien n’a été appliqué de ce fichier : `ON_ERROR_STOP` ' +
        'annule la transaction au premier refus.',
    )
  }

  process.stdout.write('appliqué\n')
}

console.log(`\n${fichiers.length} fichier(s) de référentiel appliqué(s) sous ${ROLE}.\n`)
