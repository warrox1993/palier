using Palier.Domain.Grandeurs;
using Palier.Domain.Nutriments;

namespace Palier.Domain.Tests.Nutriments;

/// <summary>
/// Quatre issues, jamais deux. L'EFSA ne produit pas une seule sorte de valeur :
/// une limite haute autorise à parler de dépassement, un « safe level of intake »
/// ne l'autorise pas — l'avis sur le fer précise que « le niveau où le risque
/// commence à augmenter n'est pas défini ». Les confondre annoncerait un danger
/// là où la science n'en définit aucun.
/// </summary>
public sealed class ComparaisonReferenceTests
{
    private static ApportAgrege Zinc(decimal alimentationMg, decimal complementsMg) =>
        new(
            "zinc_mg",
            MasseNutriment.De(alimentationMg, UniteNutriment.Milligramme),
            MasseNutriment.De(complementsMg, UniteNutriment.Milligramme));

    private static ReferenceNutriment LimiteZinc(decimal mg) =>
        new(
            "zinc_mg",
            StatutReference.LimiteHauteEtablie,
            MasseNutriment.De(mg, UniteNutriment.Milligramme),
            "EFSA",
            2006);

    // L'exemple de docs/04-nutrition.md § 4 : 17,2 mg + 25,0 mg = 42,2 mg.
    [Fact]
    public void Le_total_additionne_alimentation_et_complements()
    {
        Assert.Equal(42.2m, Zinc(17.2m, 25m).Total.Valeur);
    }

    [Fact]
    public void Un_depassement_de_limite_haute_est_signale()
    {
        var r = ComparaisonReference.Comparer(Zinc(17.2m, 25m), LimiteZinc(25m));

        Assert.Equal(IssueComparaison.AuDessusDeLaReference, r.Issue);
        Assert.Equal(42.2m, r.Total.Valeur);
    }

    [Fact]
    public void Un_apport_sous_la_limite_haute_ne_signale_rien()
    {
        var r = ComparaisonReference.Comparer(Zinc(8m, 5m), LimiteZinc(25m));
        Assert.Equal(IssueComparaison.SousLaReference, r.Issue);
    }

    // Une limite haute est un plafond tolérable, pas un interdit : l'atteindre
    // exactement n'est pas la dépasser.
    [Fact]
    public void L_egalite_stricte_n_est_pas_un_depassement()
    {
        var r = ComparaisonReference.Comparer(Zinc(20m, 5m), LimiteZinc(25m));
        Assert.Equal(IssueComparaison.SousLaReference, r.Issue);
    }

    // Le fer n'a AUCUNE limite haute : l'avis EFSA de 2024 établit un « safe
    // level of intake » de 40 mg/j, dont il précise que « l'application est plus
    // limitée qu'une UL parce que le niveau où le risque commence à augmenter
    // n'est pas défini ». 55 mg dépassent ce repère sans constituer un
    // dépassement au sens où le produit peut l'annoncer.
    [Fact]
    public void Un_niveau_sur_d_apport_ne_produit_jamais_un_depassement()
    {
        var apport = new ApportAgrege(
            "iron_mg",
            MasseNutriment.De(30m, UniteNutriment.Milligramme),
            MasseNutriment.De(25m, UniteNutriment.Milligramme));

        var r = ComparaisonReference.Comparer(
            apport,
            new ReferenceNutriment(
                "iron_mg",
                StatutReference.NiveauSurDApport,
                MasseNutriment.De(40m, UniteNutriment.Milligramme),
                "EFSA",
                2024));

        Assert.Equal(IssueComparaison.ReferenceIndicative, r.Issue);
    }

    // La vitamine C n'a aucune valeur dérivable — données insuffisantes,
    // confirmé en 2024. Un decimal valant zéro aurait inventé une limite.
    [Theory]
    [InlineData(StatutReference.NonDerivable)]
    [InlineData(StatutReference.JamaisEvalue)]
    public void Sans_reference_le_domaine_n_invente_rien(StatutReference statut)
    {
        var r = ComparaisonReference.Comparer(
            Zinc(1m, 1m),
            new ReferenceNutriment("zinc_mg", statut, null, "EFSA", 2004));

        Assert.Equal(IssueComparaison.AucuneReference, r.Issue);
        Assert.Null(r.Reference);
    }

    [Fact]
    public void Une_limite_haute_sans_valeur_est_une_reference_incoherente()
    {
        Assert.Throws<ArgumentException>(
            () =>
                new ReferenceNutriment(
                    "zinc_mg",
                    StatutReference.LimiteHauteEtablie,
                    null,
                    "EFSA",
                    2006));
    }

    [Fact]
    public void Un_niveau_sur_sans_valeur_est_aussi_incoherent()
    {
        Assert.Throws<ArgumentException>(
            () =>
                new ReferenceNutriment(
                    "iron_mg",
                    StatutReference.NiveauSurDApport,
                    null,
                    "EFSA",
                    2024));
    }

    // Le sélénium s'exprime en microgrammes, l'alimentation arrive parfois en
    // milligrammes. Sans conversion, l'écart est d'un facteur mille.
    [Fact]
    public void La_comparaison_convertit_les_unites()
    {
        var apport = new ApportAgrege(
            "selenium_ug",
            MasseNutriment.De(0.1m, UniteNutriment.Milligramme),
            MasseNutriment.De(200m, UniteNutriment.Microgramme));

        var r = ComparaisonReference.Comparer(
            apport,
            new ReferenceNutriment(
                "selenium_ug",
                StatutReference.LimiteHauteEtablie,
                MasseNutriment.De(255m, UniteNutriment.Microgramme),
                "EFSA",
                2023));

        Assert.Equal(300m, r.Total.Valeur);
        Assert.Equal(UniteNutriment.Microgramme, r.Total.Unite);
        Assert.Equal(IssueComparaison.AuDessusDeLaReference, r.Issue);
    }

    [Fact]
    public void La_comparaison_refuse_un_statut_hors_enumeration()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                ComparaisonReference.Comparer(
                    Zinc(1m, 1m),
                    new ReferenceNutriment(
                        "zinc_mg",
                        (StatutReference)99,
                        MasseNutriment.De(25m, UniteNutriment.Milligramme),
                        "EFSA",
                        2006)));
    }

    [Fact]
    public void La_comparaison_refuse_une_reference_d_un_autre_nutriment()
    {
        Assert.Throws<ArgumentException>(
            () =>
                ComparaisonReference.Comparer(
                    Zinc(1m, 1m),
                    new ReferenceNutriment(
                        "magnesium_mg",
                        StatutReference.LimiteHauteEtablie,
                        MasseNutriment.De(250m, UniteNutriment.Milligramme),
                        "SCF",
                        2001)));
    }

    // docs/04-nutrition.md § 2 : « chaque objectif s'affiche avec sa source ».
    // La provenance voyage donc avec la valeur, et une révision — la B6 passée
    // de 25 à 12 mg en 2023 — se lit à l'écran plutôt que de se deviner.
    [Fact]
    public void La_reference_transporte_sa_source_et_son_annee()
    {
        var r = LimiteZinc(25m);

        Assert.Equal("EFSA", r.Source);
        Assert.Equal(2006, r.Annee);
    }

    [Fact]
    public void Une_reference_jamais_evaluee_n_a_pas_d_annee()
    {
        var r = new ReferenceNutriment("bore_mg", StatutReference.JamaisEvalue, null, "—", null);

        Assert.Null(r.Annee);
        Assert.Equal("—", r.Source);
    }

    [Fact]
    public void Une_cle_vide_est_refusee()
    {
        Assert.Throws<ArgumentException>(
            () =>
                new ReferenceNutriment(
                    " ",
                    StatutReference.NonDerivable,
                    null,
                    "EFSA",
                    2004));
    }
}
