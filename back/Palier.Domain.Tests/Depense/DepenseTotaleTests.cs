using Palier.Domain.Depense;
using Palier.Domain.Grandeurs;

namespace Palier.Domain.Tests.Depense;

public sealed class DepenseTotaleTests
{
    // docs/04-nutrition.md § 1, tableau des facteurs d'activité.
    [Theory]
    [InlineData(NiveauActivite.BureauPeuDeMarche, 1.25)]
    [InlineData(NiveauActivite.BureauMarcheModeree, 1.35)]
    [InlineData(NiveauActivite.MixteMarcheReguliere, 1.45)]
    [InlineData(NiveauActivite.MetierDebout, 1.60)]
    public void Facteur_suit_le_tableau_de_04_nutrition(NiveauActivite niveau, decimal attendu)
    {
        Assert.Equal(attendu, DepenseTotale.Facteur(niveau));
    }

    [Fact]
    public void Facteur_refuse_un_niveau_hors_enumeration()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => DepenseTotale.Facteur((NiveauActivite)99));
    }

    // 1 MET = 1 kcal/kg/h. La revue systématique 2024 sur la dépense en
    // musculation ne recense AUCUNE formule fondée sur charge × répétitions :
    // le MET est la seule voie praticable.
    [Fact]
    public void CoutDeSeance_multiplie_le_MET_par_la_masse_et_la_duree()
    {
        var r = DepenseTotale.CoutDeSeance(
            5.0m,
            Masse.DepuisKilogrammes(75m),
            TimeSpan.FromHours(1));

        Assert.Equal(375m, r.Kilocalories);
    }

    // Le forfait de 5 kcal/min qu'on remplace aurait facturé 300 kcal à cette
    // femme de 55 kg, au-dessus de la fourchette mesurée chez la femme
    // (2,3 à 5,2 kcal/min). Le MET tient compte de la masse.
    [Fact]
    public void CoutDeSeance_tient_compte_de_la_masse()
    {
        var r = DepenseTotale.CoutDeSeance(
            5.0m,
            Masse.DepuisKilogrammes(55m),
            TimeSpan.FromHours(1));

        Assert.Equal(275m, r.Kilocalories);
    }

    [Fact]
    public void CoutDeSeance_accepte_une_duree_nulle()
    {
        var r = DepenseTotale.CoutDeSeance(5m, Masse.DepuisKilogrammes(75m), TimeSpan.Zero);
        Assert.Equal(0m, r.Kilocalories);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void CoutDeSeance_refuse_un_MET_non_positif(decimal met)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => DepenseTotale.CoutDeSeance(met, Masse.DepuisKilogrammes(75m), TimeSpan.FromHours(1)));
    }

    [Fact]
    public void CoutDeSeance_refuse_une_duree_negative()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => DepenseTotale.CoutDeSeance(5m, Masse.DepuisKilogrammes(75m), TimeSpan.FromHours(-1)));
    }

    [Fact]
    public void Calculer_ajoute_le_cout_des_seances_au_metabolisme_module()
    {
        var r = DepenseTotale.Calculer(
            Energie.DepuisKilocalories(1600m),
            NiveauActivite.BureauMarcheModeree,
            Energie.DepuisKilocalories(240m));

        // 1600 × 1,35 + 240 = 2400
        Assert.Equal(2400m, r.Estimation.Kilocalories);
    }

    // docs/04-nutrition.md § 1 : « les formules prédictives ont une erreur
    // réelle de ±10 à 15 %. Cette marge est affichée avec le chiffre,
    // systématiquement. » Le milieu de cette fourchette, ±12,5 %, reproduit
    // exactement la borne basse de l'exemple du document : 2400 → 2100.
    [Fact]
    public void La_fourchette_encadre_l_estimation_a_douze_et_demi_pour_cent()
    {
        var r = DepenseEstimee.Autour(Energie.DepuisKilocalories(2400m));

        Assert.Equal(2100m, r.BorneBasse.Kilocalories);
        Assert.Equal(2700m, r.BorneHaute.Kilocalories);
    }

    [Fact]
    public void La_fourchette_accompagne_toujours_le_calcul_complet()
    {
        var r = DepenseTotale.Calculer(
            Energie.DepuisKilocalories(1600m),
            NiveauActivite.BureauMarcheModeree,
            Energie.DepuisKilocalories(240m));

        Assert.Equal(2100m, r.BorneBasse.Kilocalories);
        Assert.Equal(2700m, r.BorneHaute.Kilocalories);
    }

    // La marge est publique parce qu'elle est un arbitrage documenté, pas un
    // détail : ±12,5 %, milieu de la fourchette « ±10 à 15 % » du document.
    // Ce test la fige, et il a un second effet — en C# une `const decimal`
    // n'est pas une constante IL mais un `static readonly` initialisé par un
    // constructeur statique, que rien n'exécute tant que personne ne lit la
    // valeur. Sans cette lecture, le seuil de couverture refuse le domaine.
    [Fact]
    public void La_marge_documentee_vaut_douze_et_demi_pour_cent()
    {
        Assert.Equal(0.125m, DepenseEstimee.Marge);
    }

    [Fact]
    public void Une_estimation_nulle_donne_une_fourchette_nulle()
    {
        var r = DepenseEstimee.Autour(Energie.DepuisKilocalories(0m));

        Assert.Equal(0m, r.Estimation.Kilocalories);
        Assert.Equal(0m, r.BorneBasse.Kilocalories);
        Assert.Equal(0m, r.BorneHaute.Kilocalories);
    }
}
