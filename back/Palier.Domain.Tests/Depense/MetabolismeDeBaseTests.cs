using Palier.Domain.Depense;
using Palier.Domain.Grandeurs;

namespace Palier.Domain.Tests.Depense;

/// <summary>
/// Les tests de bornes ont disparu d'ici : Masse, Taille et Age ne peuvent plus
/// être construits invalides, et ils portent désormais leurs propres épreuves.
/// C'est le gain du typage, et il se constate plutôt qu'il ne se suppose.
/// </summary>
public sealed class MetabolismeDeBaseTests
{
    // docs/04-nutrition.md § 1 :
    // Homme : 10 × poids + 6,25 × taille − 5 × âge + 5
    // Femme : 10 × poids + 6,25 × taille − 5 × âge − 161
    [Fact]
    public void MifflinStJeor_calcule_la_valeur_pour_un_homme()
    {
        var r = MetabolismeDeBase.MifflinStJeor(
            Sexe.Homme,
            Masse.DepuisKilogrammes(73m),
            Taille.DepuisCentimetres(178m),
            Age.DepuisAnnees(35m));

        // 730 + 1112,5 − 175 + 5 = 1672,5
        Assert.Equal(1672.5m, r.Kilocalories);
    }

    [Fact]
    public void MifflinStJeor_calcule_la_valeur_pour_une_femme()
    {
        var r = MetabolismeDeBase.MifflinStJeor(
            Sexe.Femme,
            Masse.DepuisKilogrammes(60m),
            Taille.DepuisCentimetres(165m),
            Age.DepuisAnnees(30m));

        // 600 + 1031,25 − 150 − 161 = 1320,25
        Assert.Equal(1320.25m, r.Kilocalories);
    }

    // Le bras par défaut de la sélection n'est atteignable que par une valeur
    // d'énumération forgée. Sans ce test, il reste une ligne et une branche non
    // couvertes, et le seuil de 100 % refuse le domaine. Ce n'est pas un
    // artifice : c'est la garde qui répond quand une valeur hors énumération
    // traverse une frontière de sérialisation.
    [Fact]
    public void MifflinStJeor_refuse_un_sexe_hors_enumeration()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                MetabolismeDeBase.MifflinStJeor(
                    (Sexe)99,
                    Masse.DepuisKilogrammes(73m),
                    Taille.DepuisCentimetres(178m),
                    Age.DepuisAnnees(35m)));
    }

    // docs/04-nutrition.md § 1 : 370 + 21,6 × masse maigre.
    // C'est la révision 1991 de Cunningham ; sa version de 1980, 500 + 22 × MM,
    // est une autre équation qu'il ne faut pas confondre avec celle-ci.
    [Fact]
    public void KatchMcArdle_applique_la_formule()
    {
        var r = MetabolismeDeBase.KatchMcArdle(Masse.DepuisKilogrammes(60m));
        Assert.Equal(1666m, r.Kilocalories);
    }

    [Fact]
    public void Estimer_prend_Katch_McArdle_sur_une_mesure_fiable()
    {
        var r = MetabolismeDeBase.Estimer(
            Sexe.Homme,
            Masse.DepuisKilogrammes(75m),
            Taille.DepuisCentimetres(178m),
            Age.DepuisAnnees(35m),
            PourcentageMasseGrasse.De(20m, ProvenanceMesure.MesureFiable));

        // masse maigre 60 kg → 370 + 21,6 × 60
        Assert.Equal(1666m, r.Kilocalories);
    }

    // La spec du lot 3 § 3 : Katch-McArdle n'est plus exacte que Mifflin-St
    // Jeor si le pourcentage de masse grasse ne l'est pas. Une balance à
    // impédance dévie de 4,4 points sous la DXA, soit environ 70 kcal d'erreur
    // propagée — davantage que le biais qu'on cherchait à corriger.
    [Theory]
    [InlineData(ProvenanceMesure.Impedancemetrie)]
    [InlineData(ProvenanceMesure.Declaratif)]
    public void Estimer_refuse_Katch_McArdle_hors_mesure_fiable(ProvenanceMesure provenance)
    {
        var r = MetabolismeDeBase.Estimer(
            Sexe.Homme,
            Masse.DepuisKilogrammes(73m),
            Taille.DepuisCentimetres(178m),
            Age.DepuisAnnees(35m),
            PourcentageMasseGrasse.De(20m, provenance));

        Assert.Equal(1672.5m, r.Kilocalories);
    }

    [Fact]
    public void Estimer_prend_Mifflin_sans_masse_grasse()
    {
        var r = MetabolismeDeBase.Estimer(
            Sexe.Homme,
            Masse.DepuisKilogrammes(73m),
            Taille.DepuisCentimetres(178m),
            Age.DepuisAnnees(35m),
            masseGrasse: null);

        Assert.Equal(1672.5m, r.Kilocalories);
    }
}
