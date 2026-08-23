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

    /// <summary>
    /// Les bornes, DEMANDÉES plutôt que franchies.
    /// </summary>
    /// <remarks>
    /// Un poids hors bornes tapé par un utilisateur est une saisie, pas un
    /// défaut du programme : il se refuse par un 400. La fabrique s'appuie
    /// dessus, donc les bornes ne sont écrites QU'UNE FOIS.
    /// </remarks>
    public static bool EstValide(decimal kilogrammes) => kilogrammes is > 0m and <= 500m;

    public static Masse DepuisKilogrammes(decimal kilogrammes) =>
        EstValide(kilogrammes)
            ? new Masse(kilogrammes)
            : throw new ArgumentOutOfRangeException(nameof(kilogrammes), kilogrammes, null);
}
