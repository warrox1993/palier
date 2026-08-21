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

    public static Rir De(int nombre) =>
        nombre is >= 0 and <= 10
            ? new Rir(nombre)
            : throw new ArgumentOutOfRangeException(nameof(nombre), nombre, null);
}
