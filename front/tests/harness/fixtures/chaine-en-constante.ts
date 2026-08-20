// Le trou mesuré au lot 1 : une chaîne d'interface déplacée dans une constante
// exportée échappait entièrement à `chaine-en-dur`, qui ne regardait que le JSX.
// Le fichier est un `.ts`, sans une balise — la règle ne voyait donc rien.
export const MESSAGE_VIDE = 'Aucune séance enregistrée'
