namespace Palier.Domain.Grandeurs;

/// <summary>
/// Un poids corporel, en kilogrammes.
/// </summary>
/// <remarks>
/// La borne haute est celle du corps humain, pas celle d'une barre : le type
/// <c>Charge</c> monte à 1 000 kg. C'est la raison pour laquelle le domaine
/// porte un type par grandeur plutôt qu'un type générique paramétré — un
/// plafond commun aurait été faux pour les deux.
/// </remarks>
public readonly record struct Masse
{
    private Masse(decimal kilogrammes) => Kilogrammes = kilogrammes;

    public decimal Kilogrammes { get; }

    public static Masse DepuisKilogrammes(decimal kilogrammes) =>
        kilogrammes is > 0m and <= 500m
            ? new Masse(kilogrammes)
            : throw new ArgumentOutOfRangeException(nameof(kilogrammes), kilogrammes, null);
}
