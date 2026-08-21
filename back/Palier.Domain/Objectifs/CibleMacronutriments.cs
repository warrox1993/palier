using Palier.Domain.Grandeurs;

namespace Palier.Domain.Objectifs;

/// <summary>
/// Les cibles quotidiennes de macronutriments, en grammes, plus l'hydratation.
/// </summary>
public sealed record CibleMacronutriments(
    decimal ProteinesG,
    decimal LipidesG,
    decimal GlucidesG,
    decimal FibresG,
    VolumeEau Eau);

/// <summary>
/// Les références de macronutriments de <c>docs/04-nutrition.md</c> § 2 :
/// pré-remplies, toujours modifiables, et affichées avec leur fourchette.
/// </summary>
public static class Macronutriments
{
    /// <summary>
    /// Borne basse des protéines, en g/kg.
    /// </summary>
    /// <remarks>
    /// La méta-analyse de Morton 2018 — 49 études, 1 863 participants — place
    /// le point d'inflexion à 1,62 g/kg : au-delà, la supplémentation ne
    /// produit plus de gain de masse maigre. La borne haute de 2,2 est le
    /// sommet de l'intervalle de confiance à 95 % autour de ce point, et non
    /// un plafond physiologique.
    /// </remarks>
    public static decimal ProteinesParKgMin => 1.6m;

    public static decimal ProteinesParKgDefaut => 1.8m;

    public static decimal ProteinesParKgMax => 2.2m;

    public static decimal LipidesParKgMin => 0.8m;

    public static decimal LipidesParKgDefaut => 1.0m;

    public static decimal LipidesParKgMax => 1.2m;

    public static decimal FibresGMin => 25m;

    public static decimal FibresGDefaut => 30m;

    public static decimal FibresGMax => 35m;

    /// <summary>
    /// Millilitres d'eau par kilogramme de masse corporelle.
    /// </summary>
    /// <remarks>
    /// Cohérent avec les apports adéquats de l'EFSA — 2,0 L pour la femme et
    /// 2,5 L pour l'homme — qui correspondent respectivement à 57 et 71 kg. La
    /// forme par kilogramme individualise ce que la valeur de population ne
    /// fait pas.
    /// </remarks>
    public static decimal EauMlParKg => 35m;

    private static decimal KcalParGrammeDeProteine => 4m;

    private static decimal KcalParGrammeDeLipide => 9m;

    private static decimal KcalParGrammeDeGlucide => 4m;

    /// <summary>
    /// Les cibles, à partir de la masse et de l'apport visé.
    /// </summary>
    /// <remarks>
    /// Les glucides sont <b>le reste</b> des calories, jamais une cible
    /// propre : ce sont les protéines et les lipides qui portent une
    /// exigence, et le reste équilibre l'apport.
    /// </remarks>
    public static CibleMacronutriments Calculer(
        Masse masse,
        Energie apportCible,
        decimal? proteinesParKg = null,
        decimal? lipidesParKg = null,
        decimal? fibresG = null)
    {
        var proteines = DansLaFourchette(
            proteinesParKg ?? ProteinesParKgDefaut,
            ProteinesParKgMin,
            ProteinesParKgMax,
            nameof(proteinesParKg));

        var lipides = DansLaFourchette(
            lipidesParKg ?? LipidesParKgDefaut,
            LipidesParKgMin,
            LipidesParKgMax,
            nameof(lipidesParKg));

        var fibres = DansLaFourchette(
            fibresG ?? FibresGDefaut,
            FibresGMin,
            FibresGMax,
            nameof(fibresG));

        var proteinesG = proteines * masse.Kilogrammes;
        var lipidesG = lipides * masse.Kilogrammes;

        var calorieRestante =
            apportCible.Kilocalories
            - (proteinesG * KcalParGrammeDeProteine)
            - (lipidesG * KcalParGrammeDeLipide);

        if (calorieRestante < 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(apportCible),
                apportCible.Kilocalories,
                null);
        }

        return new CibleMacronutriments(
            proteinesG,
            lipidesG,
            calorieRestante / KcalParGrammeDeGlucide,
            fibres,
            VolumeEau.DepuisMillilitres(EauMlParKg * masse.Kilogrammes));
    }

    private static decimal DansLaFourchette(decimal valeur, decimal min, decimal max, string nom) =>
        valeur >= min && valeur <= max
            ? valeur
            : throw new ArgumentOutOfRangeException(nom, valeur, null);
}
