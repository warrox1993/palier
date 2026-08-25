using Palier.Domain.Grandeurs;
using Palier.Domain.Objectifs;

namespace Palier.Domain.Tests.Objectifs;

/// <summary>
/// La répartition de l'énergie entre les trois macronutriments — § 2 ter de
/// <c>docs/04-nutrition.md</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>DEUX NIVEAUX DE SOURCE, et ils répondent à deux questions.</b> Le socle
/// réglementaire européen — ANSES 2016, glucides 40 à 55 % de l'énergie — dit
/// quelle répartition convient à une population. La littérature d'entraînement
/// — Morton 2018, point d'inflexion protéique à 1,62 g/kg — dit combien de
/// protéines construisent du muscle. Le socle gouverne la contrainte, la
/// littérature gouverne la demande.
/// </para>
///
/// <para>
/// Ces épreuves franchissent les trois issues et les deux ordres de cession.
/// Une branche jamais franchie est une branche qui ment.
/// </para>
/// </remarks>
public sealed class CibleMacronutrimentsTests
{
    // ================================================================
    // Ce que la demande obtient quand elle tient
    // ================================================================

    [Fact]
    public void Une_demande_qui_TIENT_est_honoree_telle_quelle()
    {
        // L'exemple du document : 73 kg, 2 400 kcal.
        //   budget non glucidique = 60 % × 2 400 = 1 440 kcal
        //   protéines 1,8 × 73 = 131,4 g →   525,6 kcal
        //   lipides   1,0 × 73 =  73,0 g →   657,0 kcal
        //                                  ------------
        //                                    1 182,6 kcal  ≤ 1 440 ✓
        var r = Macronutriments.Repartir(
            Masse.DepuisKilogrammes(73m),
            Energie.DepuisKilocalories(2400m)
        );

        Assert.Equal(IssueDeRepartition.Honoree, r.Issue);
        Assert.NotNull(r.Cible);
        Assert.Equal(131.4m, r.Cible.ProteinesG);
        Assert.Equal(73m, r.Cible.LipidesG);
        Assert.Equal(304.35m, r.Cible.GlucidesG);
        Assert.Equal(30m, r.Cible.FibresG);
    }

    [Fact]
    public void Les_TROIS_postes_somment_TOUJOURS_a_l_energie()
    {
        // L'invariant se teste, il ne s'espère pas. C'est lui qui interdit
        // `Math.Max(0, reste)` : une garde qui ramènerait les glucides à zéro
        // livrerait un plan dont les macros ne somment plus aux calories.
        foreach (var (kg, kcal) in new[] { (73m, 2400m), (60m, 2000m), (95m, 3000m), (50m, 1600m) })
        {
            var r = Macronutriments.Repartir(
                Masse.DepuisKilogrammes(kg),
                Energie.DepuisKilocalories(kcal)
            );

            Assert.NotNull(r.Cible);
            var somme = (r.Cible.ProteinesG * 4m) + (r.Cible.LipidesG * 9m) + (r.Cible.GlucidesG * 4m);
            Assert.Equal(kcal, decimal.Round(somme, 6));
        }
    }

    [Fact]
    public void Les_glucides_ne_descendent_JAMAIS_sous_la_part_minimale()
    {
        // La non-négativité est un THÉORÈME, pas une garde : la part non
        // glucidique ne peut pas dépasser 1 − PartGlucidiqueMinimale par
        // construction.
        foreach (var kcal in new[] { 1500m, 1800m, 2200m, 2600m, 3200m })
        {
            var r = Macronutriments.Repartir(
                Masse.DepuisKilogrammes(85m),
                Energie.DepuisKilocalories(kcal)
            );

            if (r.Cible is null)
            {
                continue;
            }

            var partGlucidique = r.Cible.GlucidesG * 4m / kcal;
            Assert.True(
                partGlucidique >= Macronutriments.PartGlucidiqueMinimale - 0.000001m,
                $"À {kcal} kcal, les glucides ne pèsent que {partGlucidique:P1} de l'énergie."
            );
        }
    }

    // ================================================================
    // Ce que la contrainte fait céder, et dans quel ordre
    // ================================================================

    [Fact]
    public void Quand_la_demande_deborde_les_LIPIDES_cedent_en_premier()
    {
        // 73 kg, 1 800 kcal. budget = 1 080 kcal.
        //   demandé : 525,6 (prot) + 657,0 (lip) = 1 182,6 > 1 080
        //   lipides ramenés à (1 080 − 525,6) / 9 = 61,6 g → 554,4 kcal
        //   525,6 + 554,4 = 1 080 ✓ — les protéines ne bougent pas
        var r = Macronutriments.Repartir(
            Masse.DepuisKilogrammes(73m),
            Energie.DepuisKilocalories(1800m)
        );

        Assert.Equal(IssueDeRepartition.DemandeReduite, r.Issue);
        Assert.NotNull(r.Cible);

        // Les protéines sont INTACTES : sous restriction, on les préserve pour
        // protéger la masse maigre.
        Assert.Equal(131.4m, r.Cible.ProteinesG);
        Assert.Equal(61.6m, r.Cible.LipidesG);
        Assert.Equal(180m, r.Cible.GlucidesG);
    }

    [Fact]
    public void Si_le_plancher_lipidique_ne_suffit_pas_les_PROTEINES_cedent_ensuite()
    {
        // 73 kg, 1 700 kcal. budget = 1 020 kcal.
        //   lipides au plancher : 0,8 × 73 = 58,4 g → 525,6 kcal
        //   525,6 + 525,6 = 1 051,2 > 1 020 — il faut encore céder
        //   protéines ramenées à (1 020 − 525,6) / 4 = 123,6 g → 494,4 kcal
        var r = Macronutriments.Repartir(
            Masse.DepuisKilogrammes(73m),
            Energie.DepuisKilocalories(1700m)
        );

        Assert.Equal(IssueDeRepartition.DemandeReduite, r.Issue);
        Assert.NotNull(r.Cible);
        Assert.Equal(58.4m, r.Cible.LipidesG);
        Assert.Equal(123.6m, r.Cible.ProteinesG);
        Assert.Equal(170m, r.Cible.GlucidesG);
    }

    [Fact]
    public void La_demande_D_ORIGINE_est_conservee_pour_que_l_ecran_le_dise()
    {
        // Rendre silencieusement d'autres chiffres que ceux demandés serait la
        // défaillance silencieuse déguisée en correction. L'écran doit pouvoir
        // dire « vous demandiez 1,8 g/kg, la répartition en a retenu 1,69 ».
        var r = Macronutriments.Repartir(
            Masse.DepuisKilogrammes(73m),
            Energie.DepuisKilocalories(1700m),
            proteinesParKg: 1.8m,
            lipidesParKg: 1.0m
        );

        Assert.Equal(1.8m, r.ProteinesDemandeesParKg);
        Assert.Equal(1.0m, r.LipidesDemandesParKg);
        Assert.NotNull(r.Cible);
        Assert.NotEqual(1.8m * 73m, r.Cible.ProteinesG);
    }

    // ================================================================
    // L'impossibilité — un état, jamais une exception
    // ================================================================

    [Fact]
    public void Une_utilisatrice_de_CENT_KILOS_en_deficit_ne_fait_plus_PLANTER_le_calcul()
    {
        // LE DÉFAUT QUE CE TYPE EXISTE POUR FERMER. Avant le 25/08/2026, ce
        // profil — femme 100 kg, 155 cm, 55 ans, sédentaire, déficit de 20 % —
        // levait `ArgumentOutOfRangeException`.
        //
        // Ce n'est pas un cas limite : ce sont les valeurs PAR DÉFAUT du
        // document appliquées à une personne de cent kilos.
        var r = Macronutriments.Repartir(
            Masse.DepuisKilogrammes(100m),
            Energie.DepuisKilocalories(1532.75m)
        );

        Assert.Equal(IssueDeRepartition.Impossible, r.Issue);

        // AUCUNE cible n'est rendue. Renvoyer des zéros aurait produit
        // exactement le nombre absurde qu'on cherche à empêcher.
        Assert.Null(r.Cible);

        // Et la demande d'origine reste lisible, pour que l'écran sache quoi
        // dire.
        Assert.Equal(1.8m, r.ProteinesDemandeesParKg);
        Assert.Equal(1.0m, r.LipidesDemandesParKg);
    }

    [Fact]
    public void Juste_AU_DESSUS_de_l_ancien_seuil_le_calcul_ne_rend_plus_de_cible_absurde()
    {
        // C'ÉTAIT LE PIRE DES DEUX CAS. Le même profil à 165 cm et 40 ans
        // passait l'ancien test de négativité et rendait tranquillement
        // 12,6 g de glucides pour la journée — 3 % de l'énergie, quand
        // l'intervalle de référence commence à 40 %.
        //
        // Une exception se voit ; une cible absurde s'affiche.
        var r = Macronutriments.Repartir(
            Masse.DepuisKilogrammes(100m),
            Energie.DepuisKilocalories(1670.25m)
        );

        Assert.Equal(IssueDeRepartition.Impossible, r.Issue);
        Assert.Null(r.Cible);
    }

    [Fact]
    public void L_impossibilite_a_une_BORNE_calculable_et_elle_se_franchit_des_deux_cotes()
    {
        // Aux deux planchers — 1,6 g/kg de protéines et 0,8 g/kg de lipides —
        // il faut (4×1,6 + 9×0,8) / 0,60 = 22,67 kcal par kilogramme.
        // Pour 100 kg : 2 266,67 kcal.
        var justeEnDessous = Macronutriments.Repartir(
            Masse.DepuisKilogrammes(100m),
            Energie.DepuisKilocalories(2266m)
        );
        Assert.Equal(IssueDeRepartition.Impossible, justeEnDessous.Issue);

        var justeAuDessus = Macronutriments.Repartir(
            Masse.DepuisKilogrammes(100m),
            Energie.DepuisKilocalories(2267m)
        );
        Assert.NotEqual(IssueDeRepartition.Impossible, justeAuDessus.Issue);
        Assert.NotNull(justeAuDessus.Cible);
    }

    // ================================================================
    // Les fourchettes de saisie
    // ================================================================

    [Theory]
    [InlineData(1.5)]
    [InlineData(2.3)]
    public void Des_PROTEINES_hors_fourchette_sont_refusees(double parKg)
    {
        var faute = Assert.Throws<ArgumentOutOfRangeException>(() =>
            Macronutriments.Repartir(
                Masse.DepuisKilogrammes(73m),
                Energie.DepuisKilocalories(2400m),
                proteinesParKg: (decimal)parKg
            )
        );

        Assert.Equal("proteinesParKg", faute.ParamName);
    }

    [Theory]
    [InlineData(0.7)]
    [InlineData(1.3)]
    public void Des_LIPIDES_hors_fourchette_sont_refuses(double parKg)
    {
        var faute = Assert.Throws<ArgumentOutOfRangeException>(() =>
            Macronutriments.Repartir(
                Masse.DepuisKilogrammes(73m),
                Energie.DepuisKilocalories(2400m),
                lipidesParKg: (decimal)parKg
            )
        );

        Assert.Equal("lipidesParKg", faute.ParamName);
    }

    [Theory]
    [InlineData(24)]
    [InlineData(36)]
    public void Des_FIBRES_hors_fourchette_sont_refusees(int grammes)
    {
        var faute = Assert.Throws<ArgumentOutOfRangeException>(() =>
            Macronutriments.Repartir(
                Masse.DepuisKilogrammes(73m),
                Energie.DepuisKilocalories(2400m),
                fibresG: grammes
            )
        );

        Assert.Equal("fibresG", faute.ParamName);
    }

    [Fact]
    public void Les_BORNES_des_fourchettes_sont_inclusives()
    {
        var r = Macronutriments.Repartir(
            Masse.DepuisKilogrammes(73m),
            Energie.DepuisKilocalories(3000m),
            proteinesParKg: Macronutriments.ProteinesParKgMin,
            lipidesParKg: Macronutriments.LipidesParKgMax,
            fibresG: Macronutriments.FibresGMin
        );

        Assert.NotNull(r.Cible);
        Assert.Equal(25m, r.Cible.FibresG);
    }

    [Fact]
    public void La_part_glucidique_minimale_est_celle_de_l_ANSES()
    {
        // ANSES 2016 : glucides 40 à 55 % de l'énergie. L'épreuve lit la
        // constante plutôt que de recopier 0,40 : une valeur recopiée ne verrait
        // jamais un changement de référentiel.
        Assert.Equal(0.40m, Macronutriments.PartGlucidiqueMinimale);
        Assert.True(Macronutriments.PartGlucidiqueMinimale is > 0m and < 1m);
    }
}
