using Palier.Domain.Grandeurs;

namespace Palier.Domain.Entrainement;

/// <summary>
/// Une séance pour un exercice donné, réduite à ce que la détection consomme.
/// </summary>
public sealed record SeanceDExercice(
    DateOnly Jour,
    Charge ChargeMaximale,
    int RepetitionsALaChargeMaximale);

/// <summary>
/// La détection de plateau de <c>docs/05-entrainement.md</c> § 2.
/// </summary>
/// <remarks>
/// « Même charge maximale sur trois séances consécutives sans progression du
/// nombre de répétitions. Le système le signale, <b>sans prescrire de
/// solution</b> — il rappelle les leviers disponibles (tempo, série
/// supplémentaire, allègement) et laisse l'utilisateur décider. »
///
/// D'où le retour booléen : le domaine constate un fait mesurable, il ne rédige
/// ni ne recommande.
/// </remarks>
public static class DetectionPlateau
{
    /// <summary>Le nombre de séances sur lequel la stagnation se juge.</summary>
    public static int SeancesConsecutives => 3;

    public static bool EstEnPlateau(IReadOnlyList<SeanceDExercice> seances)
    {
        ArgumentNullException.ThrowIfNull(seances);

        if (seances.Count < SeancesConsecutives)
        {
            return false;
        }

        // Le tri par date n'est pas une précaution de style. Sans lui, « les
        // trois dernières » désigne les trois derniers éléments de la liste, et
        // non les trois séances les plus récentes : une collection remontée
        // d'une requête sans ORDER BY conclurait sur les mauvaises séances,
        // silencieusement.
        var dernieres = seances
            .OrderBy(s => s.Jour)
            .Skip(seances.Count - SeancesConsecutives)
            .ToArray();

        var reference = dernieres[0];

        for (var i = 1; i < dernieres.Length; i++)
        {
            var chargeIdentique =
                dernieres[i].ChargeMaximale.Kilogrammes == reference.ChargeMaximale.Kilogrammes;

            var repetitionsQuiMontent =
                dernieres[i].RepetitionsALaChargeMaximale > reference.RepetitionsALaChargeMaximale;

            if (!chargeIdentique || repetitionsQuiMontent)
            {
                return false;
            }
        }

        return true;
    }
}
