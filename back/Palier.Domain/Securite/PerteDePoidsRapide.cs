using Palier.Domain.Grandeurs;

namespace Palier.Domain.Securite;

/// <summary>Une pesée, réduite à ce que la détection consomme.</summary>
public sealed record Pesee(DateOnly Jour, Masse Poids);

/// <summary>
/// La détection de perte de poids rapide de <c>docs/01-conformite.md</c> § 5 :
/// « plus de 1 % du poids par semaine sur trois semaines → message
/// d'orientation ».
/// </summary>
/// <remarks>
/// Le retour est un booléen. Le document impose « un message d'orientation vers
/// un professionnel, <b>jamais</b> de renforcement de la restriction » — c'est
/// une affaire de formulation, donc de libellé versionné en base et d'i18next.
/// Le domaine constate un fait mesurable et s'arrête là.
/// </remarks>
public static class PerteDePoidsRapide
{
    /// <summary>Fraction du poids perdue en une semaine, au-delà de laquelle on alerte.</summary>
    public static decimal SeuilHebdomadaire => 0.01m;

    /// <summary>Nombre de semaines consécutives requises.</summary>
    public static int SemainesConsecutives => 3;

    /// <summary>
    /// Ramène une série de pesées quotidiennes à UNE valeur par semaine : la
    /// MOYENNE des pesées de la semaine, datée de sa dernière pesée.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Sans cette réduction, la détection est fausse.</b>
    /// <see cref="EstDetectee" /> compare des valeurs CONSÉCUTIVES et suppose
    /// qu'une semaine les sépare — c'est ce que <c>docs/01-conformite.md</c>
    /// § 5 décrit : « plus de 1 % du poids par semaine sur trois semaines ».
    /// Lui passer quatre pesées quotidiennes lui ferait mesurer quatre JOURS et
    /// conclure sur trois semaines qui n'ont pas eu lieu.
    /// </para>
    ///
    /// <para>
    /// <b>La MOYENNE, et non la dernière pesée de la semaine.</b> Le poids
    /// corporel varie de 1 à 2 % d'un jour à l'autre — eau, glycogène, contenu
    /// digestif — soit PLUS que le seuil de 1 % lui-même. Un échantillon
    /// hebdomadaire unique ferait donc du seuil un générateur de faux
    /// positifs : deux jours de rétention d'eau suffiraient à déclencher, puis
    /// à taire, l'alerte. Et un garde-fou qui crie tout le temps est un
    /// garde-fou qu'on finit par ignorer — ou par désactiver.
    /// </para>
    ///
    /// <para>
    /// <b>Une semaine incomplète compte quand même.</b> La moyenne d'une seule
    /// pesée vaut cette pesée : refuser les semaines partielles retarderait la
    /// détection de sept jours au pire moment, celui où quelqu'un vient de
    /// commencer à perdre vite.
    /// </para>
    ///
    /// <para>
    /// La semaine est celle du lundi, par
    /// <see cref="System.Globalization.ISOWeek" /> : elle ne dépend ni de la
    /// culture du serveur ni du fuseau de l'utilisateur, contrairement au
    /// premier jour de semaine du calendrier, qui change de pays en pays.
    /// </para>
    /// </remarks>
    public static IReadOnlyList<Pesee> MoyennesHebdomadaires(IReadOnlyList<Pesee> pesees)
    {
        ArgumentNullException.ThrowIfNull(pesees);

        return
        [
            .. pesees
                .GroupBy(p => System.Globalization.ISOWeek.GetYear(p.Jour.ToDateTime(TimeOnly.MinValue)) * 100
                    + System.Globalization.ISOWeek.GetWeekOfYear(p.Jour.ToDateTime(TimeOnly.MinValue)))
                .Select(semaine => new Pesee(
                    semaine.Max(p => p.Jour),
                    Masse.DepuisKilogrammes(semaine.Average(p => p.Poids.Kilogrammes))
                ))
                .OrderBy(p => p.Jour),
        ];
    }

    public static bool EstDetectee(IReadOnlyList<Pesee> pesees)
    {
        ArgumentNullException.ThrowIfNull(pesees);

        // Une pesée sur quatre bornes trois semaines : il en faut une de plus
        // que de semaines observées.
        if (pesees.Count < SemainesConsecutives + 1)
        {
            return false;
        }

        // Trié pour la même raison que la détection de plateau : une collection
        // remontée sans ORDER BY conclurait sur des écarts inventés.
        var triees = pesees.OrderBy(p => p.Jour).ToArray();
        var dernieres = triees.Skip(triees.Length - (SemainesConsecutives + 1)).ToArray();

        for (var i = 1; i < dernieres.Length; i++)
        {
            var depart = dernieres[i - 1].Poids.Kilogrammes;
            var perte = depart - dernieres[i].Poids.Kilogrammes;

            if (perte <= depart * SeuilHebdomadaire)
            {
                return false;
            }
        }

        return true;
    }
}
