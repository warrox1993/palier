namespace Palier.Domain.Grandeurs;

/// <summary>
/// Les répétitions gardées en réserve à la fin d'une série — <i>repetitions in
/// reserve</i>.
/// </summary>
/// <remarks>
/// Zéro est l'échec musculaire : la série ne pouvait pas aller plus loin.
///
/// <see cref="ParDefaut" /> vaut 2, ce qu'impose <c>docs/05-entrainement.md</c>
/// § 3 quand le RIR n'est pas renseigné — « hypothèse prudente ». Le Position
/// Stand 2026 de l'ACSM, bâti sur 137 revues systématiques, en fait la
/// recommandation de référence : deux à trois répétitions en réserve produisent
/// les mêmes gains de masse et de force que l'échec absolu, avec moins de
/// fatigue accumulée et un risque de blessure moindre.
/// </remarks>
public readonly record struct Rir
{
    private Rir(int nombre) => Nombre = nombre;

    public int Nombre { get; }

    /// <summary>L'hypothèse retenue quand le RIR n'a pas été renseigné.</summary>
    public static Rir ParDefaut => new(2);

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
    public static bool EstValide(int nombre) => nombre is >= 0 and <= 10;

    public static Rir De(int nombre) =>
        EstValide(nombre)
            ? new Rir(nombre)
            : throw new ArgumentOutOfRangeException(nameof(nombre), nombre, null);
}
