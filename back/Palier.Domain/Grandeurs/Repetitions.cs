namespace Palier.Domain.Grandeurs;

/// <summary>
/// Un nombre de répétitions effectuées dans une série.
/// </summary>
/// <remarks>
/// Une série de zéro répétition n'est pas une série : elle ne se note pas, elle
/// ne se produit pas. La borne haute n'a pas de sens physiologique, elle borne
/// une saisie erronée.
/// </remarks>
public readonly record struct Repetitions
{
    private Repetitions(int nombre) => Nombre = nombre;

    public int Nombre { get; }

    public static Repetitions De(int nombre) =>
        nombre is >= 1 and <= 1000
            ? new Repetitions(nombre)
            : throw new ArgumentOutOfRangeException(nameof(nombre), nombre, null);
}
