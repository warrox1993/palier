// Déclarations pour `outil-gitleaks.mjs`, importé depuis les épreuves
// TypeScript du harnais. Le module reste en JavaScript parce que le hook de
// pré-commit — un script shell — l'appelle directement par `node`, sans étape
// de compilation.
//
// Sans ce fichier, `npm run typecheck` refuse l'import avec TS7016 : le module
// aurait un type `any` implicite, ce que `noImplicitAny` interdit. Le garde-fou
// a mordu à l'ajout de l'import, comme prévu.

/** Commandes d'installation, une par système, prêtes à afficher. */
export const COMMANDES_INSTALLATION: string

/** Chemin absolu de l'exécutable gitleaks, ou `null` s'il est introuvable. */
export function cheminGitleaks(): string | null

/**
 * Chemin absolu de l'exécutable gitleaks. **Lève** s'il est introuvable, avec
 * les commandes d'installation. À utiliser partout où l'absence de l'outil doit
 * refuser plutôt que passer.
 */
export function exigerGitleaks(): string
