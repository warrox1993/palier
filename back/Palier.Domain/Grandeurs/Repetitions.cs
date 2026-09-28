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

    /// <summary>
    /// Les bornes, DEMANDÉES plutôt que franchies.
    /// </summary>
    /// <remarks>
    /// Une valeur hors bornes tapée par un utilisateur n'est pas un défaut du
    /// programme : c'est une saisie, et une saisie se refuse par un 400. Sans
    /// ce prédicat, l'adaptateur HTTP n'aurait que deux choix, tous deux
    /// mauvais — attraper l'exception pour en faire un flux normal, ou
    /// recopier les bornes chez lui, ce qui dupliquerait la connaissance à
    /// l'endroit exact où elle doit être unique.
    ///
    /// La fabrique s'appuie dessus : les bornes ne sont écrites QU'UNE FOIS.
    /// </remarks>
    public static bool EstValide(int nombre) => nombre is >= 1 and <= 1000;

    public static Repetitions De(int nombre) =>
        EstValide(nombre)
            ? new Repetitions(nombre)
            : throw new ArgumentOutOfRangeException(nameof(nombre), nombre, null);
}
