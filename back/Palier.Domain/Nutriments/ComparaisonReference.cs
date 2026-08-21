using Palier.Domain.Grandeurs;

namespace Palier.Domain.Nutriments;

/// <summary>
/// L'issue d'une comparaison entre un apport et sa référence.
/// </summary>
public enum IssueComparaison
{
    /// <summary>L'apport reste sous la limite haute établie.</summary>
    SousLaReference,

    /// <summary>L'apport dépasse la limite haute établie.</summary>
    AuDessusDeLaReference,

    /// <summary>
    /// Une valeur existe, mais elle ne permet pas de parler de dépassement :
    /// c'est un niveau sûr d'apport, pas une limite haute.
    /// </summary>
    ReferenceIndicative,

    /// <summary>Aucune référence : rien ne peut être affiché.</summary>
    AucuneReference,
}

/// <summary>
/// Le résultat d'une comparaison. La référence n'est rendue que lorsqu'il en
/// existe une.
/// </summary>
public sealed record ResultatComparaison(
    IssueComparaison Issue,
    MasseNutriment Total,
    MasseNutriment? Reference);

/// <summary>
/// Compare un apport agrégé à sa référence sanitaire.
/// </summary>
public static class ComparaisonReference
{
    /// <summary>
    /// Compare, et rend <b>quatre</b> issues possibles et non deux.
    /// </summary>
    /// <remarks>
    /// <c>docs/04-nutrition.md</c> § 3 est catégorique : « les UL n'existent
    /// que pour une quinzaine de nutriments. Pour tous les autres, aucun seuil
    /// haut ne doit être affiché, et il est formellement interdit d'en inventer
    /// un. » Un booléen, ou un nombre valant zéro en l'absence de limite,
    /// aurait fabriqué un dépassement pour chaque nutriment non évalué.
    /// </remarks>
    public static ResultatComparaison Comparer(ApportAgrege apport, ReferenceNutriment reference)
    {
        ArgumentNullException.ThrowIfNull(apport);
        ArgumentNullException.ThrowIfNull(reference);

        if (!string.Equals(apport.Cle, reference.Cle, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"La référence porte « {reference.Cle} » et l'apport « {apport.Cle} ».",
                nameof(reference));
        }

        if (!Enum.IsDefined(reference.Statut))
        {
            throw new ArgumentOutOfRangeException(
                nameof(reference),
                reference.Statut,
                null);
        }

        var total = apport.Total;

        switch (reference.Statut)
        {
            case StatutReference.NonDerivable:
            case StatutReference.JamaisEvalue:
                return new ResultatComparaison(IssueComparaison.AucuneReference, total, null);

            case StatutReference.NiveauSurDApport:
                return new ResultatComparaison(
                    IssueComparaison.ReferenceIndicative,
                    total,
                    reference.Valeur);

            default:
                // La construction de ReferenceNutriment garantit qu'une limite
                // haute établie porte une valeur.
                var limite = reference.Valeur!.Value.ConvertieEn(total.Unite);
                return new ResultatComparaison(
                    total.Valeur > limite.Valeur
                        ? IssueComparaison.AuDessusDeLaReference
                        : IssueComparaison.SousLaReference,
                    total,
                    reference.Valeur);
        }
    }
}
