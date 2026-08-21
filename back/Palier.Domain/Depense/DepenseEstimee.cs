using Palier.Domain.Grandeurs;

namespace Palier.Domain.Depense;

/// <summary>
/// Une dépense estimée, indissociable de sa fourchette d'incertitude.
/// </summary>
/// <remarks>
/// <c>docs/04-nutrition.md</c> § 1 : « les formules prédictives ont une erreur
/// réelle de ±10 à 15 %. <b>Cette marge est affichée avec le chiffre,
/// systématiquement.</b> » Le motif y est écrit : « un utilisateur qui prend le
/// chiffre pour une vérité et ne bouge pas en trois semaines conclut que son
/// corps est cassé, puis se désabonne. »
///
/// Rendre un simple nombre aurait rendu la fourchette facultative, donc
/// oubliée un jour. Ici elle voyage avec l'estimation : on ne peut pas
/// afficher l'une sans avoir l'autre sous la main.
///
/// La marge retenue, ±12,5 %, est le milieu de la fourchette annoncée. Elle
/// reproduit exactement la borne basse de l'exemple du document — 2 400 × 0,875
/// = 2 100.
/// </remarks>
public readonly record struct DepenseEstimee
{
    /// <summary>La marge d'incertitude, milieu de la fourchette « ±10 à 15 % ».</summary>
    /// <remarks>
    /// Une propriété et non une <c>const decimal</c>. En C# une constante
    /// décimale n'est pas une constante IL : le compilateur produit un champ
    /// <c>static readonly</c> initialisé par un constructeur statique, que
    /// personne n'exécute jamais puisque les lectures sont remplacées par la
    /// valeur littérale à la compilation. Ce constructeur mort faisait chuter
    /// la couverture du domaine sous son seuil — mesuré à 98,03 % de méthodes.
    /// </remarks>
    public static decimal Marge => 0.125m;

    private DepenseEstimee(Energie estimation, Energie borneBasse, Energie borneHaute)
    {
        Estimation = estimation;
        BorneBasse = borneBasse;
        BorneHaute = borneHaute;
    }

    public Energie Estimation { get; }

    public Energie BorneBasse { get; }

    public Energie BorneHaute { get; }

    public static DepenseEstimee Autour(Energie estimation) =>
        new(
            estimation,
            Energie.DepuisKilocalories(estimation.Kilocalories * (1m - Marge)),
            Energie.DepuisKilocalories(estimation.Kilocalories * (1m + Marge)));
}
