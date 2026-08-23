using Palier.Domain.Grandeurs;
using Palier.Domain.Securite;

namespace Palier.Domain.Tests.Securite;

/// <summary>
/// <c>docs/01-conformite.md</c> § 5 : « une application de comptage calorique
/// attire mécaniquement des personnes en TCA. C'est une obligation éthique
/// autant qu'une protection juridique. » Ces détections rendent un booléen et
/// jamais un message : le document impose « une orientation vers un
/// professionnel » et interdit « tout renforcement de la restriction », ce qui
/// est une affaire de libellé — donc de base et d'i18next, pas de domaine.
/// </summary>
public sealed class DetectionsDeSecuriteTests
{
    private static Pesee P(int jour, decimal kg) =>
        new(new DateOnly(2026, 8, jour), Masse.DepuisKilogrammes(kg));

    private static JourneeDApport J(int jour, decimal kcal) =>
        new(new DateOnly(2026, 8, jour), Energie.DepuisKilocalories(kcal));

    // 80 → 78,4 → 76,8 → 75,2 : environ 2 % par semaine, trois semaines.
    [Fact]
    public void Une_perte_de_plus_d_un_pour_cent_par_semaine_sur_trois_semaines_est_detectee()
    {
        Assert.True(
            PerteDePoidsRapide.EstDetectee([P(1, 80m), P(8, 78.4m), P(15, 76.8m), P(22, 75.2m)]));
    }

    // 80 → 79,6 → 79,2 → 78,8 : 0,5 % par semaine.
    [Fact]
    public void Une_perte_lente_n_est_pas_detectee()
    {
        Assert.False(
            PerteDePoidsRapide.EstDetectee([P(1, 80m), P(8, 79.6m), P(15, 79.2m), P(22, 78.8m)]));
    }

    [Fact]
    public void Une_seule_semaine_rapide_ne_declenche_pas()
    {
        Assert.False(
            PerteDePoidsRapide.EstDetectee([P(1, 80m), P(8, 78m), P(15, 77.8m), P(22, 77.6m)]));
    }

    [Fact]
    public void Une_prise_de_poids_n_est_jamais_detectee()
    {
        Assert.False(
            PerteDePoidsRapide.EstDetectee([P(1, 75m), P(8, 76m), P(15, 77m), P(22, 78m)]));
    }

    [Fact]
    public void Les_pesees_sont_triees_avant_d_etre_jugees()
    {
        Assert.True(
            PerteDePoidsRapide.EstDetectee([P(22, 75.2m), P(1, 80m), P(15, 76.8m), P(8, 78.4m)]));
    }

    [Fact]
    public void Moins_de_trois_semaines_ne_conclut_pas()
    {
        Assert.False(PerteDePoidsRapide.EstDetectee([P(1, 80m), P(8, 78m)]));
    }

    [Fact]
    public void Une_liste_de_pesees_vide_ne_conclut_pas()
    {
        Assert.False(PerteDePoidsRapide.EstDetectee([]));
    }

    [Fact]
    public void Le_seuil_hebdomadaire_est_expose()
    {
        Assert.Equal(0.01m, PerteDePoidsRapide.SeuilHebdomadaire);
        Assert.Equal(3, PerteDePoidsRapide.SemainesConsecutives);
    }

    // 1600 × 0,8 = 1280. Cinq journées à 1200 sont sous le seuil.
    [Fact]
    public void Cinq_jours_sous_quatre_vingts_pour_cent_du_metabolisme_sont_detectes()
    {
        var journees = Enumerable.Range(1, 5).Select(j => J(j, 1200m)).ToArray();

        Assert.True(RestrictionSevere.EstDetectee(journees, Energie.DepuisKilocalories(1600m)));
    }

    [Fact]
    public void Un_apport_juste_au_dessus_du_seuil_n_est_pas_detecte()
    {
        var journees = Enumerable.Range(1, 5).Select(j => J(j, 1281m)).ToArray();

        Assert.False(RestrictionSevere.EstDetectee(journees, Energie.DepuisKilocalories(1600m)));
    }

    [Fact]
    public void Une_journee_normale_interrompt_la_serie()
    {
        var journees = new[]
        {
            J(1, 1200m),
            J(2, 1200m),
            J(3, 2000m),
            J(4, 1200m),
            J(5, 1200m),
            J(6, 1200m),
        };

        Assert.False(RestrictionSevere.EstDetectee(journees, Energie.DepuisKilocalories(1600m)));
    }

    [Fact]
    public void Une_serie_de_cinq_jours_apres_une_interruption_est_detectee()
    {
        var journees = new[]
        {
            J(1, 1200m),
            J(2, 2000m),
            J(3, 1200m),
            J(4, 1200m),
            J(5, 1200m),
            J(6, 1200m),
            J(7, 1200m),
        };

        Assert.True(RestrictionSevere.EstDetectee(journees, Energie.DepuisKilocalories(1600m)));
    }

    [Fact]
    public void Les_journees_sont_triees_avant_d_etre_jugees()
    {
        var journees = new[] { J(5, 1200m), J(1, 1200m), J(4, 1200m), J(2, 1200m), J(3, 1200m) };

        Assert.True(RestrictionSevere.EstDetectee(journees, Energie.DepuisKilocalories(1600m)));
    }

    [Fact]
    public void Moins_de_cinq_journees_ne_conclut_pas()
    {
        Assert.False(RestrictionSevere.EstDetectee([], Energie.DepuisKilocalories(1600m)));
    }

    [Fact]
    public void Un_metabolisme_de_base_nul_est_refuse()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => RestrictionSevere.EstDetectee([], Energie.DepuisKilocalories(0m)));
    }

    [Fact]
    public void Les_seuils_de_restriction_sont_exposes()
    {
        Assert.Equal(0.8m, RestrictionSevere.FractionDuMetabolismeDeBase);
        Assert.Equal(5, RestrictionSevere.JoursConsecutifs);
    }

    // ================================================================
    // La réduction hebdomadaire
    // ================================================================

    [Fact]
    public void Sept_pesees_dans_une_semaine_donnent_UNE_valeur()
    {
        // Sans cette réduction, `EstDetectee` mesurerait des JOURS en croyant
        // mesurer des semaines, et conclurait sur trois semaines qui n'ont pas
        // eu lieu.
        var semaine = Enumerable
            .Range(0, 7)
            .Select(j => new Pesee(new DateOnly(2026, 8, 3).AddDays(j), Masse.DepuisKilogrammes(80m)))
            .ToArray();

        var reduites = PerteDePoidsRapide.MoyennesHebdomadaires(semaine);

        Assert.Single(reduites);
        Assert.Equal(80m, reduites[0].Poids.Kilogrammes);

        // Datée de la DERNIÈRE pesée de la semaine : c'est celle qui situe la
        // valeur dans le temps pour la comparaison suivante.
        Assert.Equal(new DateOnly(2026, 8, 9), reduites[0].Jour);
    }

    [Fact]
    public void La_valeur_hebdomadaire_est_la_MOYENNE_et_non_la_derniere_pesee()
    {
        // LE POINT QUI COMPTE. Le poids varie de 1 à 2 % d'un jour à l'autre —
        // plus que le seuil de 1 % lui-même. Retenir la dernière pesée ferait
        // du seuil un générateur de faux positifs : ici, 78 kg le dimanche
        // après 80 kg toute la semaine.
        var semaine = new[]
        {
            new Pesee(new DateOnly(2026, 8, 3), Masse.DepuisKilogrammes(80m)),
            new Pesee(new DateOnly(2026, 8, 4), Masse.DepuisKilogrammes(82m)),
            new Pesee(new DateOnly(2026, 8, 9), Masse.DepuisKilogrammes(78m)),
        };

        var reduites = PerteDePoidsRapide.MoyennesHebdomadaires(semaine);

        Assert.Single(reduites);
        Assert.Equal(80m, reduites[0].Poids.Kilogrammes);
    }

    [Fact]
    public void Deux_semaines_donnent_DEUX_valeurs_dans_l_ordre()
    {
        var pesees = new[]
        {
            // Semaine du 10 août, donnée EN PREMIER pour que l'épreuve juge
            // aussi le tri : une série remontée sans ORDER BY conclurait sur
            // des écarts inventés.
            new Pesee(new DateOnly(2026, 8, 10), Masse.DepuisKilogrammes(79m)),
            new Pesee(new DateOnly(2026, 8, 3), Masse.DepuisKilogrammes(80m)),
        };

        var reduites = PerteDePoidsRapide.MoyennesHebdomadaires(pesees);

        Assert.Equal(2, reduites.Count);
        Assert.Equal(80m, reduites[0].Poids.Kilogrammes);
        Assert.Equal(79m, reduites[1].Poids.Kilogrammes);
    }

    [Fact]
    public void Une_semaine_INCOMPLETE_compte_quand_meme()
    {
        // Refuser les semaines partielles retarderait la détection de sept
        // jours au pire moment : celui où quelqu'un vient de commencer à
        // perdre vite.
        var une = new[] { new Pesee(new DateOnly(2026, 8, 5), Masse.DepuisKilogrammes(75m)) };

        var reduites = PerteDePoidsRapide.MoyennesHebdomadaires(une);

        Assert.Single(reduites);
        Assert.Equal(75m, reduites[0].Poids.Kilogrammes);
    }

    [Fact]
    public void Une_serie_VIDE_donne_une_serie_vide() =>
        Assert.Empty(PerteDePoidsRapide.MoyennesHebdomadaires([]));

    [Fact]
    public void Le_31_decembre_et_le_1er_janvier_de_la_MEME_semaine_ISO_ne_se_separent_pas()
    {
        // Le piège que le regroupement par (année, numéro de semaine) attrape
        // mal si l'année vient du calendrier plutôt que d'ISOWeek : le 31
        // décembre 2026 est un jeudi, et il appartient à la semaine ISO 53 de
        // 2026, comme le 1er janvier 2027 qui la suit. Regrouper sur
        // `Jour.Year` les aurait séparés en deux semaines, donc inventé une
        // comparaison hebdomadaire de UN jour.
        var pesees = new[]
        {
            new Pesee(new DateOnly(2026, 12, 31), Masse.DepuisKilogrammes(80m)),
            new Pesee(new DateOnly(2027, 1, 1), Masse.DepuisKilogrammes(82m)),
        };

        var reduites = PerteDePoidsRapide.MoyennesHebdomadaires(pesees);

        Assert.Single(reduites);
        Assert.Equal(81m, reduites[0].Poids.Kilogrammes);
    }

    [Fact]
    public void La_reduction_rend_la_detection_JUSTE_sur_des_pesees_quotidiennes()
    {
        // Le bout à bout : quatre semaines à −1,5 % par semaine, pesées tous
        // les jours. Sans réduction, `EstDetectee` verrait quatre jours
        // consécutifs à variation quasi nulle et conclurait « non ».
        var pesees = new List<Pesee>();
        var poids = 80m;
        for (var semaine = 0; semaine < 4; semaine++)
        {
            for (var jour = 0; jour < 7; jour++)
            {
                pesees.Add(
                    new Pesee(
                        new DateOnly(2026, 8, 3).AddDays((semaine * 7) + jour),
                        Masse.DepuisKilogrammes(poids)
                    )
                );
            }

            poids *= 0.985m;
        }

        Assert.False(PerteDePoidsRapide.EstDetectee(pesees));
        Assert.True(PerteDePoidsRapide.EstDetectee(PerteDePoidsRapide.MoyennesHebdomadaires(pesees)));
    }

    // ================================================================
    // Masse : demander sans lever
    // ================================================================

    [Theory]
    [InlineData(0.1)]
    [InlineData(80)]
    [InlineData(500)]
    public void Masse_valide_ce_que_la_fabrique_accepte(decimal kilogrammes)
    {
        Assert.True(Masse.EstValide(kilogrammes));
        Assert.Equal(kilogrammes, Masse.DepuisKilogrammes(kilogrammes).Kilogrammes);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(500.1)]
    public void Masse_refuse_ce_que_la_fabrique_refuse(decimal kilogrammes)
    {
        // Zéro est refusé, contrairement à `Charge` : un corps qui ne pèse rien
        // n'existe pas, alors qu'une traction se note à 0 kg ajouté.
        Assert.False(Masse.EstValide(kilogrammes));
        Assert.Throws<ArgumentOutOfRangeException>(() => Masse.DepuisKilogrammes(kilogrammes));
    }
}
