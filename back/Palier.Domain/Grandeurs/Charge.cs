namespace Palier.Domain.Grandeurs;

/// <summary>
/// Une charge soulevée, en kilogrammes.
/// </summary>
/// <remarks>
/// Zéro est accepté, contrairement à <see cref="Masse" /> : le poids de corps
/// est une charge légitime, et une traction se note à 0 kg ajouté. La borne
/// haute dépasse les records de force, parce qu'un domaine refuse
/// l'impossible, pas l'improbable.
/// </remarks>
public readonly record struct Charge
{
    private Charge(decimal kilogrammes) => Kilogrammes = kilogrammes;

    public decimal Kilogrammes { get; }

    public static Charge DepuisKilogrammes(decimal kilogrammes) =>
        kilogrammes is >= 0m and <= 1000m
            ? new Charge(kilogrammes)
            : throw new ArgumentOutOfRangeException(nameof(kilogrammes), kilogrammes, null);
}
