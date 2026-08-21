namespace Palier.Domain.Grandeurs;

/// <summary>
/// Un volume d'eau, en millilitres.
/// </summary>
/// <remarks>
/// Le millilitre plutôt que le litre : la saisie réelle porte sur des
/// contenants — un verre, une gourde — dont la contenance s'exprime en
/// millilitres, et arrondir à la décimale de litre perdrait la moitié d'un
/// verre.
/// </remarks>
public readonly record struct VolumeEau
{
    private VolumeEau(decimal millilitres) => Millilitres = millilitres;

    public decimal Millilitres { get; }

    public static VolumeEau DepuisMillilitres(decimal millilitres) =>
        millilitres >= 0m
            ? new VolumeEau(millilitres)
            : throw new ArgumentOutOfRangeException(nameof(millilitres), millilitres, null);
}
