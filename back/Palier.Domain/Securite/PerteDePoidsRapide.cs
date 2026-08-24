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
    /// Ramène une série de pesées à UNE valeur par semaine, par MOYENNE MOBILE
    /// sur sept jours glissants, ancrée sur la dernière pesée.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Sans cette réduction, la détection est fausse.</b>
    /// <see cref="EstDetectee" /> compare des valeurs CONSÉCUTIVES et suppose
    /// qu'une semaine les sépare — c'est ce que <c>docs/01-conformite.md</c>
    /// § 5 décrit : « plus de 1 % du poids par semaine sur trois semaines ».
    /// Lui passer des pesées quotidiennes lui ferait mesurer des JOURS en
    /// croyant mesurer des semaines, et la variation d'un jour à l'autre reste
    /// sous le seuil : la détection serait restée MUETTE chez les utilisateurs
    /// qui pèsent tous les jours.
    /// </para>
    ///
    /// <para>
    /// <b>Une moyenne MOBILE, et non un découpage par semaine calendaire.</b>
    /// C'est la correction de D64 sur D61. Le découpage ISO livré au lot 5
    /// avait un défaut mesurable : quelqu'un qui commence à peser un jeudi a
    /// une première « semaine » de quatre jours, et l'écart entre sa moyenne et
    /// celle de la semaine suivante porte sur environ cinq jours, pas sept. Le
    /// seuil de 1 % appliqué à cinq jours est plus strict qu'il ne devrait —
    /// donc un faux positif, sur une alerte qui oriente vers un professionnel.
    /// </para>
    ///
    /// <para>
    /// Les fenêtres glissantes n'ont pas ce défaut : chaque comparaison porte
    /// sur exactement sept jours, quel que soit le jour où l'utilisateur a
    /// commencé. C'est aussi plus fidèle au texte, où « par semaine » désigne
    /// un intervalle et non une case du calendrier.
    /// </para>
    ///
    /// <para>
    /// <b>La MOYENNE, et non la dernière pesée de la fenêtre.</b> Le poids
    /// corporel varie de 1 à 2 % d'un jour à l'autre — eau, glycogène, contenu
    /// digestif — soit PLUS que le seuil lui-même. Un échantillon unique ferait
    /// du seuil un générateur de faux positifs.
    /// </para>
    ///
    /// <para>
    /// <b>Une fenêtre VIDE interrompt la série</b>, elle ne s'interpole pas :
    /// inventer une valeur pour une semaine sans pesée reviendrait à conclure
    /// sur une mesure qui n'existe pas. Une seule pesée dans une fenêtre suffit
    /// en revanche — la règle des trois baisses consécutives fait déjà le
    /// travail anti-bruit, et exiger plus retarderait la détection chez qui
    /// pèse une fois par semaine, c'est-à-dire la plupart des gens.
    /// </para>
    /// </remarks>
    /// <param name="pesees">Les pesées, dans n'importe quel ordre.</param>
    /// <param name="finDeFenetre">
    /// Le dernier jour observé. Les fenêtres remontent à partir de lui.
    /// </param>
    public static IReadOnlyList<Pesee> MoyennesHebdomadaires(
        IReadOnlyList<Pesee> pesees,
        DateOnly finDeFenetre
    )
    {
        ArgumentNullException.ThrowIfNull(pesees);

        var fenetres = new List<Pesee>();

        // De la plus ancienne à la plus récente, pour que `EstDetectee` reçoive
        // la série dans l'ordre où elle compare.
        for (var rang = SemainesConsecutives; rang >= 0; rang--)
        {
            var fin = finDeFenetre.AddDays(-7 * rang);
            var debut = fin.AddDays(-6);

            var dedans = pesees.Where(p => p.Jour >= debut && p.Jour <= fin).ToArray();

            if (dedans.Length == 0)
            {
                // La fenêtre est vide : la série est interrompue, et ce qui
                // précède ne peut plus être comparé à ce qui suit. On repart de
                // zéro plutôt que d'accoler deux morceaux séparés par un trou.
                fenetres.Clear();
                continue;
            }

            fenetres.Add(
                new Pesee(fin, Masse.DepuisKilogrammes(dedans.Average(p => p.Poids.Kilogrammes)))
            );
        }

        return fenetres;
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
