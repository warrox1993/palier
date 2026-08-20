namespace Palier.Domain.Energie;

/// <summary>
/// Métabolisme de base. Voir docs/04-nutrition.md § 1.
/// Module pur : aucun accès réseau, base ou interface.
/// </summary>
public static class MetabolismeDeBase
{
    public static decimal MifflinStJeor(Sexe sexe, decimal poidsKg, decimal tailleCm, decimal age)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(poidsKg);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(tailleCm);
        ArgumentOutOfRangeException.ThrowIfNegative(age);

        var socle = (10m * poidsKg) + (6.25m * tailleCm) - (5m * age);
        return sexe switch
        {
            Sexe.Homme => socle + 5m,
            Sexe.Femme => socle - 161m,
            _ => throw new ArgumentOutOfRangeException(nameof(sexe)),
        };
    }
}
