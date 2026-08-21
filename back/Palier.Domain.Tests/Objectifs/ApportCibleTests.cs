using Palier.Domain.Grandeurs;
using Palier.Domain.Objectifs;

namespace Palier.Domain.Tests.Objectifs;

/// <summary>
/// <c>docs/01-conformite.md</c> § 5 : « impossible de fixer un objectif sous
/// 1200 kcal (femme) ou 1500 kcal (homme). Non contournable », et « ces règles
/// sont testées unitairement et ne peuvent pas être désactivées par
/// configuration ». Il n'existe donc aucun paramètre permettant de lever le
/// plancher — c'est ce que ces épreuves protègent.
/// </summary>
public sealed class ApportCibleTests
{
    [Theory]
    [InlineData(Sexe.Femme, 1200)]
    [InlineData(Sexe.Homme, 1500)]
    public void Le_plancher_suit_01_conformite(Sexe sexe, decimal attendu)
    {
        Assert.Equal(attendu, PlancherCalorique.Pour(sexe).Kilocalories);
    }

    [Fact]
    public void Le_plancher_refuse_un_sexe_hors_enumeration()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PlancherCalorique.Pour((Sexe)99));
    }

    [Fact]
    public void Sans_ajustement_l_apport_vaut_la_maintenance()
    {
        var r = CalculApportCible.Calculer(Energie.DepuisKilocalories(2400m), Sexe.Homme);

        Assert.Equal(2400m, r.Valeur.Kilocalories);
        Assert.False(r.PlancherAtteint);
    }

    // docs/04-nutrition.md § 2 : « si l'utilisateur veut un déficit ou un
    // surplus, il le choisit dans une fourchette encadrée : −20 % à +15 % de la
    // maintenance, jamais au-delà ».
    [Fact]
    public void Un_deficit_de_vingt_pour_cent_est_applique()
    {
        var r = CalculApportCible.Calculer(Energie.DepuisKilocalories(2400m), Sexe.Homme, -0.20m);
        Assert.Equal(1920m, r.Valeur.Kilocalories);
    }

    [Fact]
    public void Un_surplus_de_quinze_pour_cent_est_applique()
    {
        var r = CalculApportCible.Calculer(Energie.DepuisKilocalories(2400m), Sexe.Homme, 0.15m);
        Assert.Equal(2760m, r.Valeur.Kilocalories);
    }

    [Theory]
    [InlineData(-0.21)]
    [InlineData(0.16)]
    public void Un_ajustement_hors_bornes_est_refuse(decimal ajustement)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                CalculApportCible.Calculer(
                    Energie.DepuisKilocalories(2400m),
                    Sexe.Homme,
                    ajustement));
    }

    // 1700 × 0,80 = 1360, sous le plancher homme de 1500.
    [Fact]
    public void Le_plancher_mord_sur_une_maintenance_basse()
    {
        var r = CalculApportCible.Calculer(Energie.DepuisKilocalories(1700m), Sexe.Homme, -0.20m);

        Assert.Equal(1500m, r.Valeur.Kilocalories);
        Assert.True(r.PlancherAtteint);
    }

    // Le plancher ne dépend pas du déficit demandé : une maintenance mesurée
    // sous le plancher ne descend pas l'objectif non plus.
    [Fact]
    public void Le_plancher_mord_aussi_sans_aucun_deficit()
    {
        var r = CalculApportCible.Calculer(Energie.DepuisKilocalories(1100m), Sexe.Femme);

        Assert.Equal(1200m, r.Valeur.Kilocalories);
        Assert.True(r.PlancherAtteint);
    }

    [Fact]
    public void Un_apport_exactement_au_plancher_ne_le_declenche_pas()
    {
        var r = CalculApportCible.Calculer(Energie.DepuisKilocalories(1500m), Sexe.Homme);

        Assert.Equal(1500m, r.Valeur.Kilocalories);
        Assert.False(r.PlancherAtteint);
    }

    [Fact]
    public void Les_bornes_d_ajustement_sont_exposees()
    {
        Assert.Equal(-0.20m, CalculApportCible.DeficitMaximal);
        Assert.Equal(0.15m, CalculApportCible.SurplusMaximal);
    }

    [Fact]
    public void Les_planchers_sont_exposes()
    {
        Assert.Equal(1200m, PlancherCalorique.FemmeKcal);
        Assert.Equal(1500m, PlancherCalorique.HommeKcal);
    }
}
