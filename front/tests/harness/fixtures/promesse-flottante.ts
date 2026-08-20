// Violation délibérée : la promesse n'est ni attendue, ni chaînée, ni confiée à
// un gestionnaire d'erreur. C'est la forme exacte de la perte silencieuse que
// `no-floating-promises` existe pour arrêter dans la file de retry hors ligne.
async function enregistrerLaSeance(): Promise<void> {
  await Promise.resolve()
}

export function declencher(): void {
  enregistrerLaSeance()
}
