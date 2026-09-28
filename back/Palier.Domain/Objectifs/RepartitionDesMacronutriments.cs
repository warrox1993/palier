namespace Palier.Domain.Objectifs;

/// <summary>
/// Ce que la répartition de l'énergie entre les trois macronutriments a pu
/// honorer.
/// </summary>
/// <remarks>
/// <para>
/// <b>L'impossibilité est un ÉTAT, jamais une exception.</b> Avant le
/// 25/08/2026, le domaine levait un
/// <see cref="ArgumentOutOfRangeException"/> quand l'énergie ne suffisait pas
/// à couvrir les protéines et les lipides demandés — sur un profil parfaitement
/// ordinaire : une femme de 100 kg, sédentaire, en déficit de 20 %.
/// </para>
///
/// <para>
/// Le cas juste au-dessus du seuil était pire encore : le calcul rendait
/// <b>12,6 g de glucides pour la journée</b>, soit 3 % de l'énergie, sans rien
/// signaler. Une exception se voit ; une cible absurde s'affiche.
/// </para>
/// </remarks>
public enum IssueDeRepartition
{
    /// <summary>La demande tient dans le budget énergétique, telle quelle.</summary>
    Honoree,

    /// <summary>
    /// La demande ne tenait pas : les lipides ont cédé, puis les protéines si
    /// nécessaire. La cible rendue n'est PAS celle qui a été demandée, et
    /// l'écran doit le dire.
    /// </summary>
    DemandeReduite,

    /// <summary>
    /// Même aux planchers, aucune répartition n'existe à cet apport
    /// énergétique.
    /// </summary>
    /// <remarks>
    /// <b>Ce n'est pas un refus de sécurité, c'est une impossibilité de
    /// calcul</b>, et le libellé doit le dire ainsi. La question à poser à
    /// l'utilisateur porte sur l'ÉNERGIE — un apport si bas qu'aucune
    /// répartition ne tient — et non sur ses macronutriments.
    /// </remarks>
    Impossible,
}

/// <summary>
/// Le résultat d'une répartition : son issue, et la cible quand elle existe.
/// </summary>
/// <remarks>
/// <para>
/// Un tuple plutôt qu'une cible portant un drapeau : quand l'issue vaut
/// <see cref="IssueDeRepartition.Impossible"/>, il n'y a AUCUNE cible à rendre.
/// Renvoyer des zéros aurait produit exactement le genre de nombre absurde que
/// ce type existe pour empêcher.
/// </para>
///
/// <para>
/// <c>ProteinesDemandeesParKg</c> et <c>LipidesDemandesParKg</c> sont conservés
/// même quand la demande a été réduite : l'écran a besoin des deux valeurs pour
/// dire ce qui n'a pas été honoré, et il ne peut pas les recalculer.
/// </para>
/// </remarks>
public sealed record RepartitionDesMacronutriments(
    IssueDeRepartition Issue,
    CibleMacronutriments? Cible,
    decimal ProteinesDemandeesParKg,
    decimal LipidesDemandesParKg
);
