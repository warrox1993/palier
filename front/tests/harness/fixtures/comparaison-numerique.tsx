// NON-RÉGRESSION — ce fichier ne doit déclencher aucune règle.
// L'ancienne règle `chaine-en-dur` le refusait : « > », puis des lettres et des
// espaces, puis « < ». Aucune chaîne n'est pourtant affichée ici. Une règle qui
// crie où il ne faut pas est une règle qu'on cesse de lire.
export const estBas = (valeur: number) => valeur < 10
export const estHaut = (valeur: number) => valeur > 90
export const compare = (a: number, b: number) => a > b && b < a
