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
    public static bool EstValide(decimal kilogrammes) => kilogrammes is >= 0m and <= 1000m;

    public static Charge DepuisKilogrammes(decimal kilogrammes) =>
        EstValide(kilogrammes)
            ? new Charge(kilogrammes)
            : throw new ArgumentOutOfRangeException(nameof(kilogrammes), kilogrammes, null);
}
