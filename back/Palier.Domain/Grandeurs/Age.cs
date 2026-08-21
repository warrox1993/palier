namespace Palier.Domain.Grandeurs;

/// <summary>
/// Un âge, en années, décimal pour accepter les fractions d'année.
/// </summary>
/// <remarks>
/// Zéro est accepté : c'est un âge, pas une durée d'entraînement. La borne
/// haute dépasse le record de longévité documenté, parce qu'un domaine refuse
/// l'impossible, pas l'improbable.
/// </remarks>
public readonly record struct Age
{
    private Age(decimal annees) => Annees = annees;

    public decimal Annees { get; }

    public static Age DepuisAnnees(decimal annees) =>
        annees is >= 0m and <= 130m
            ? new Age(annees)
            : throw new ArgumentOutOfRangeException(nameof(annees), annees, null);
}
