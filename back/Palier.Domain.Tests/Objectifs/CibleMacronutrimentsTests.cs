using Palier.Domain.Grandeurs;
using Palier.Domain.Objectifs;

namespace Palier.Domain.Tests.Objectifs;

/// <summary>
/// Les fourchettes sont exposées parce que <c>docs/04-nutrition.md</c> § 2
/// impose d'afficher chaque objectif avec sa source et sa fourchette de
/// référence. Elles sont fondées : la méta-analyse de Morton 2018 place le
/// point d'inflexion des protéines à 1,62 g/kg, l'intervalle de confiance à
/// 95 % montant jusqu'à 2,2.
/// </summary>
public sealed class CibleMacronutrimentsTests
{
    [Fact]
    public void Calculer_applique_les_valeurs_par_defaut()
    {
        var c = Macronutriments.Calculer(
            Masse.DepuisKilogrammes(73m),
            Energie.DepuisKilocalories(2400m));

        Assert.Equal(131.4m, c.ProteinesG); // 1,8 × 73 — l'exemple du document
        Assert.Equal(73m, c.LipidesG); // 1,0 × 73
        Assert.Equal(30m, c.FibresG);
        Assert.Equal(2555m, c.Eau.Millilitres); // 35 × 73
    }

    // Les glucides sont le reste des calories, jamais une cible propre :
    // 2400 − (131,4 × 4) − (73 × 9) = 2400 − 525,6 − 657 = 1217,4 → /4
    [Fact]
    public void Les_glucides_sont_le_reste_des_calories()
    {
        var c = Macronutriments.Calculer(
            Masse.DepuisKilogrammes(73m),
            Energie.DepuisKilocalories(2400m));

        Assert.Equal(304.35m, c.GlucidesG);
    }

    [Fact]
    public void Calculer_accepte_une_valeur_choisie_dans_la_fourchette()
    {
        var c = Macronutriments.Calculer(
            Masse.DepuisKilogrammes(73m),
            Energie.DepuisKilocalories(2400m),
            proteinesParKg: 2.2m);

        Assert.Equal(160.6m, c.ProteinesG);
    }

    [Fact]
    public void Calculer_accepte_des_lipides_et_des_fibres_choisis()
    {
        var c = Macronutriments.Calculer(
            Masse.DepuisKilogrammes(73m),
            Energie.DepuisKilocalories(2400m),
            lipidesParKg: 0.8m,
            fibresG: 35m);

        Assert.Equal(58.4m, c.LipidesG);
        Assert.Equal(35m, c.FibresG);
    }

    [Theory]
    [InlineData(1.5)]
    [InlineData(2.3)]
    public void Calculer_refuse_des_proteines_hors_fourchette(decimal parKg)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                Macronutriments.Calculer(
                    Masse.DepuisKilogrammes(73m),
                    Energie.DepuisKilocalories(2400m),
                    proteinesParKg: parKg));
    }

    [Theory]
    [InlineData(0.7)]
    [InlineData(1.3)]
    public void Calculer_refuse_des_lipides_hors_fourchette(decimal parKg)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                Macronutriments.Calculer(
                    Masse.DepuisKilogrammes(73m),
                    Energie.DepuisKilocalories(2400m),
                    lipidesParKg: parKg));
    }

    [Theory]
    [InlineData(24)]
    [InlineData(36)]
    public void Calculer_refuse_des_fibres_hors_fourchette(decimal grammes)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                Macronutriments.Calculer(
                    Masse.DepuisKilogrammes(73m),
                    Energie.DepuisKilocalories(2400m),
                    fibresG: grammes));
    }

    // 2,2 g/kg de protéines et 1,2 g/kg de lipides sur 120 kg pèsent
    // 1056 + 1296 = 2352 kcal, au-delà des 1200 disponibles : les glucides
    // seraient négatifs, ce qui n'a aucun sens physiologique.
    [Fact]
    public void Calculer_refuse_un_apport_que_les_macros_depassent()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                Macronutriments.Calculer(
                    Masse.DepuisKilogrammes(120m),
                    Energie.DepuisKilocalories(1200m),
                    proteinesParKg: 2.2m,
                    lipidesParKg: 1.2m));
    }

    [Fact]
    public void Les_fourchettes_de_reference_sont_exposees()
    {
        Assert.Equal(1.6m, Macronutriments.ProteinesParKgMin);
        Assert.Equal(1.8m, Macronutriments.ProteinesParKgDefaut);
        Assert.Equal(2.2m, Macronutriments.ProteinesParKgMax);
        Assert.Equal(0.8m, Macronutriments.LipidesParKgMin);
        Assert.Equal(1.0m, Macronutriments.LipidesParKgDefaut);
        Assert.Equal(1.2m, Macronutriments.LipidesParKgMax);
        Assert.Equal(25m, Macronutriments.FibresGMin);
        Assert.Equal(30m, Macronutriments.FibresGDefaut);
        Assert.Equal(35m, Macronutriments.FibresGMax);
        Assert.Equal(35m, Macronutriments.EauMlParKg);
    }
}
