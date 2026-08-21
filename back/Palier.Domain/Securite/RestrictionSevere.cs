using Palier.Domain.Grandeurs;

namespace Palier.Domain.Securite;

/// <summary>Un apport quotidien, réduit à ce que la détection consomme.</summary>
public sealed record JourneeDApport(DateOnly Jour, Energie Apport);

/// <summary>
/// La détection de restriction sévère de <c>docs/01-conformite.md</c> § 5 :
/// « apports très en dessous du métabolisme de base répétés ».
/// </summary>
/// <remarks>
/// Le document ne chiffre ni « très en dessous » ni « répétés ». Les deux
/// valeurs retenues — 80 % du métabolisme de base, cinq jours consécutifs —
/// sont exposées publiquement pour qu'un diététicien puisse les corriger sans
/// lire le code. Elles font partie de ce que la validation externe encore due
/// devra examiner.
/// </remarks>
public static class RestrictionSevere
{
    /// <summary>En dessous de cette fraction du métabolisme de base, l'apport alerte.</summary>
    public static decimal FractionDuMetabolismeDeBase => 0.8m;

    /// <summary>Nombre de journées consécutives requises.</summary>
    public static int JoursConsecutifs => 5;

    public static bool EstDetectee(
        IReadOnlyList<JourneeDApport> journees,
        Energie metabolismeDeBase)
    {
        ArgumentNullException.ThrowIfNull(journees);

        if (metabolismeDeBase.Kilocalories <= 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(metabolismeDeBase),
                metabolismeDeBase.Kilocalories,
                null);
        }

        var seuil = metabolismeDeBase.Kilocalories * FractionDuMetabolismeDeBase;
        var consecutives = 0;

        foreach (var journee in journees.OrderBy(j => j.Jour))
        {
            consecutives = journee.Apport.Kilocalories < seuil ? consecutives + 1 : 0;

            if (consecutives >= JoursConsecutifs)
            {
                return true;
            }
        }

        return false;
    }
}
