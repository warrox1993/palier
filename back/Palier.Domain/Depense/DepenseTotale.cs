using Palier.Domain.Grandeurs;

namespace Palier.Domain.Depense;

/// <summary>
/// La dépense énergétique totale : métabolisme de base modulé par l'activité,
/// plus le coût des séances effectivement enregistrées.
/// </summary>
public static class DepenseTotale
{
    /// <summary>Le facteur d'activité de <c>docs/04-nutrition.md</c> § 1.</summary>
    public static decimal Facteur(NiveauActivite niveau) =>
        niveau switch
        {
            NiveauActivite.BureauPeuDeMarche => 1.25m,
            NiveauActivite.BureauMarcheModeree => 1.35m,
            NiveauActivite.MixteMarcheReguliere => 1.45m,
            NiveauActivite.MetierDebout => 1.60m,
            _ => throw new ArgumentOutOfRangeException(nameof(niveau), niveau, null),
        };

    /// <summary>
    /// Le coût énergétique d'une séance, par la voie des équivalents
    /// métaboliques : 1 MET vaut 1 kcal par kilogramme et par heure.
    /// </summary>
    /// <remarks>
    /// La revue systématique de 2024 sur les méthodes d'estimation de la
    /// dépense en musculation ne recense <b>aucune formule</b> fondée sur
    /// charge × déplacement × répétitions ni sur le volume de charge. Le MET
    /// est la seule voie praticable, et il a l'avantage de tenir compte de la
    /// masse : le forfait de 5 kcal/min qu'il remplace facturait autant à une
    /// femme de 55 kg qu'à un homme de 95.
    ///
    /// La valeur du MET n'est <b>pas</b> une constante de ce domaine. Elle
    /// arrive en paramètre et vit en base avec son code d'activité, sa version
    /// de Compendium et sa date — les sources publiées donnent de 3,5 à 9,0
    /// selon la version et le code retenus, et une valeur de référence qui
    /// bouge n'a rien à faire dans du code compilé.
    /// </remarks>
    public static Energie CoutDeSeance(decimal met, Masse masse, TimeSpan duree)
    {
        if (met <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(met), met, null);
        }

        if (duree < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(duree), duree, null);
        }

        return Energie.DepuisKilocalories(met * masse.Kilogrammes * (decimal)duree.TotalHours);
    }

    /// <summary>
    /// La dépense totale, rendue <b>avec sa fourchette</b> : voir
    /// <see cref="DepenseEstimee" />.
    /// </summary>
    public static DepenseEstimee Calculer(
        Energie metabolismeDeBase,
        NiveauActivite niveau,
        Energie coutDesSeances) =>
        DepenseEstimee.Autour(
            Energie.DepuisKilocalories(
                (metabolismeDeBase.Kilocalories * Facteur(niveau)) + coutDesSeances.Kilocalories));
}
