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
}
