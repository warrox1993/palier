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
    // La réduction en fenêtres glissantes — D64
    // ================================================================

    private static readonly DateOnly _fin = new(2026, 8, 30);

    [Fact]
    public void Sept_pesees_dans_une_fenetre_donnent_UNE_valeur()
    {
        // Sans cette réduction, `EstDetectee` mesurerait des JOURS en croyant
        // mesurer des semaines, et conclurait sur trois semaines qui n'ont pas
        // eu lieu.
        var semaine = Enumerable
            .Range(0, 7)
            .Select(j => new Pesee(_fin.AddDays(-j), Masse.DepuisKilogrammes(80m)))
            .ToArray();

        var reduites = PerteDePoidsRapide.MoyennesHebdomadaires(semaine, _fin);

        Assert.Single(reduites);
        Assert.Equal(80m, reduites[0].Poids.Kilogrammes);
        Assert.Equal(_fin, reduites[0].Jour);
    }

    [Fact]
    public void La_valeur_est_la_MOYENNE_et_non_la_derniere_pesee()
    {
        // LE POINT QUI COMPTE. Le poids varie de 1 à 2 % d'un jour à l'autre —
        // plus que le seuil de 1 % lui-même. Retenir la dernière pesée ferait
        // du seuil un générateur de faux positifs.
        var fenetre = new[]
        {
            new Pesee(_fin.AddDays(-6), Masse.DepuisKilogrammes(80m)),
            new Pesee(_fin.AddDays(-3), Masse.DepuisKilogrammes(82m)),
            new Pesee(_fin, Masse.DepuisKilogrammes(78m)),
        };

        var reduites = PerteDePoidsRapide.MoyennesHebdomadaires(fenetre, _fin);

        Assert.Single(reduites);
        Assert.Equal(80m, reduites[0].Poids.Kilogrammes);
    }

    [Fact]
    public void Les_fenetres_sortent_de_la_PLUS_ANCIENNE_a_la_plus_recente()
    {
        // `EstDetectee` compare des valeurs consécutives dans cet ordre : la
        // rendre à l'envers inverserait le sens de chaque variation, et une
        // prise de poids passerait pour une perte.
        var pesees = new[]
        {
            new Pesee(_fin, Masse.DepuisKilogrammes(78m)),
            new Pesee(_fin.AddDays(-7), Masse.DepuisKilogrammes(80m)),
        };

        var reduites = PerteDePoidsRapide.MoyennesHebdomadaires(pesees, _fin);

        Assert.Equal(2, reduites.Count);
        Assert.Equal(80m, reduites[0].Poids.Kilogrammes);
        Assert.Equal(78m, reduites[1].Poids.Kilogrammes);
    }

    [Fact]
    public void UNE_SEULE_pesee_dans_une_fenetre_suffit()
    {
        // Exiger deux pesées par fenêtre retarderait la détection chez qui pèse
        // une fois par semaine — la plupart des gens. La règle des trois
        // baisses consécutives fait déjà le travail anti-bruit.
        var une = new[] { new Pesee(_fin.AddDays(-2), Masse.DepuisKilogrammes(75m)) };

        var reduites = PerteDePoidsRapide.MoyennesHebdomadaires(une, _fin);

        Assert.Single(reduites);
        Assert.Equal(75m, reduites[0].Poids.Kilogrammes);
    }

    [Fact]
    public void Une_fenetre_VIDE_interrompt_la_serie()
    {
        // Elle ne s'interpole PAS : inventer une valeur pour une semaine sans
        // pesée reviendrait à conclure sur une mesure qui n'existe pas. Ici,
        // trois semaines de données puis un trou puis une semaine : seule la
        // dernière survit.
        var pesees = new[]
        {
            new Pesee(_fin.AddDays(-21), Masse.DepuisKilogrammes(85m)),
            new Pesee(_fin.AddDays(-14), Masse.DepuisKilogrammes(83m)),
            // rien entre -13 et -7 : la fenêtre -7 est vide
            new Pesee(_fin, Masse.DepuisKilogrammes(80m)),
        };

        var reduites = PerteDePoidsRapide.MoyennesHebdomadaires(pesees, _fin);

        Assert.Single(reduites);
        Assert.Equal(80m, reduites[0].Poids.Kilogrammes);
    }

    [Fact]
    public void Une_serie_VIDE_donne_une_serie_vide() =>
        Assert.Empty(PerteDePoidsRapide.MoyennesHebdomadaires([], _fin));

    [Fact]
    public void Une_serie_commencee_un_JEUDI_n_invente_aucune_semaine_courte()
    {
        // LE DÉFAUT QUE D64 CORRIGE. Avec un découpage par semaine calendaire,
        // quelqu'un qui commence à peser un jeudi a une première « semaine » de
        // quatre jours, et l'écart avec la suivante porte sur environ cinq
        // jours — le seuil de 1 % y devient plus strict qu'il ne devrait, donc
        // un faux positif.
        //
        // Ici, un poids PARFAITEMENT STABLE, pesé chaque jour à partir d'un
        // jeudi : aucune fenêtre ne doit montrer de variation.
        var jeudi = new DateOnly(2026, 8, 6);
        var pesees = Enumerable
            .Range(0, 25)
            .Select(j => new Pesee(jeudi.AddDays(j), Masse.DepuisKilogrammes(80m)))
            .ToArray();

        var fin = jeudi.AddDays(24);
        var reduites = PerteDePoidsRapide.MoyennesHebdomadaires(pesees, fin);

        Assert.Equal(4, reduites.Count);
        Assert.All(reduites, p => Assert.Equal(80m, p.Poids.Kilogrammes));
        Assert.False(PerteDePoidsRapide.EstDetectee(reduites));
    }

    [Fact]
    public void La_reduction_rend_la_detection_JUSTE_sur_des_pesees_quotidiennes()
    {
        // Le bout à bout : quatre semaines à −1,5 % par semaine, pesées tous
        // les jours. Sans réduction, `EstDetectee` verrait quatre jours
        // consécutifs à variation quasi nulle et conclurait « non ».
        var pesees = new List<Pesee>();
        var poids = 80m;
        var debut = new DateOnly(2026, 8, 3);

        for (var semaine = 0; semaine < 4; semaine++)
        {
            for (var jour = 0; jour < 7; jour++)
            {
                pesees.Add(
                    new Pesee(debut.AddDays((semaine * 7) + jour), Masse.DepuisKilogrammes(poids))
                );
            }

            poids *= 0.985m;
        }

        var fin = debut.AddDays(27);

        Assert.False(PerteDePoidsRapide.EstDetectee(pesees));
        Assert.True(
            PerteDePoidsRapide.EstDetectee(
                PerteDePoidsRapide.MoyennesHebdomadaires(pesees, fin)
            )
        );
    }

    [Fact]
    public void Le_domaine_DIT_de_combien_de_jours_il_a_besoin()
    {
        // `JoursNecessaires` existe pour que l'appelant ne puisse pas
        // raccourcir l'analyse sans le savoir. La valeur SUIT
        // `SemainesConsecutives` : si le seuil passe un jour à quatre semaines,
        // la fenêtre de lecture suit toute seule.
        Assert.Equal(
            (PerteDePoidsRapide.SemainesConsecutives + 1) * 7,
            PerteDePoidsRapide.JoursNecessaires
        );

        // Et il vaut au moins ce que `EstDetectee` exige : quatre valeurs
        // hebdomadaires, donc vingt-huit jours.
        Assert.True(PerteDePoidsRapide.JoursNecessaires >= 28);
    }

    [Fact]
    public void Une_serie_TRONQUEE_sous_le_seuil_ne_peut_RIEN_detecter()
    {
        // LA PREUVE DU SILENCE, faite au domaine. Une perte de 2 %/semaine
        // depuis un mois, mais dont on ne garde que les sept derniers jours :
        // deux fenêtres survivent, `EstDetectee` en exige quatre, et le retour
        // est `false` — indiscernable de « rien à signaler ».
        //
        // C'est cette épreuve qui justifie que la fenêtre d'analyse ne se
        // négocie pas avec l'appelant.
        var fin = new DateOnly(2026, 8, 30);
        var toutes = new List<Pesee>();
        var poids = 80m;

        for (var jour = 27; jour >= 0; jour--)
        {
            toutes.Add(new Pesee(fin.AddDays(-jour), Masse.DepuisKilogrammes(poids)));
            poids *= 0.997m;
        }

        // La série COMPLÈTE détecte.
        Assert.True(
            PerteDePoidsRapide.EstDetectee(
                PerteDePoidsRapide.MoyennesHebdomadaires(toutes, fin)
            )
        );

        // La série TRONQUÉE à sept jours ne détecte plus rien.
        var tronquee = toutes.Where(p => p.Jour >= fin.AddDays(-7)).ToArray();
        Assert.False(
            PerteDePoidsRapide.EstDetectee(
                PerteDePoidsRapide.MoyennesHebdomadaires(tronquee, fin)
            )
        );
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
