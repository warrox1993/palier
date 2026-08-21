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
