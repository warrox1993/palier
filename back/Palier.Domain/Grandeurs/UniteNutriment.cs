namespace Palier.Domain.Grandeurs;

/// <summary>
/// L'unité de masse d'un nutriment.
/// </summary>
/// <remarks>
/// Les trois cohabitent dans les références de l'EFSA : le zinc a une limite
/// haute en milligrammes, le sélénium en microgrammes, les protéines en
/// grammes. Confondre deux d'entre elles se trompe d'un facteur mille — sur
/// une comparaison à une limite de sécurité, l'erreur se voit chez
/// l'utilisateur.
/// </remarks>
public enum UniteNutriment
{
    Gramme,
    Milligramme,
    Microgramme,
}
