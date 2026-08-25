using Palier.Domain.Grandeurs;
using Palier.Domain.Objectifs;

namespace Palier.Domain.Tests.Objectifs;

/// <summary>
/// L'hydratation — § 2 bis de <c>docs/04-nutrition.md</c>, corrigé le
/// 25/08/2026.
/// </summary>
/// <remarks>
/// Le domaine posait <c>35 ml × poids</c> en se réclamant des apports adéquats
/// de l'EFSA. Le raisonnement était faux à sa racine : ces valeurs portent sur
/// l'eau <b>totale</b>, boissons et aliments confondus, alors que le produit ne
/// compte que les boissons.
/// </remarks>
public sealed class CibleDHydratationTests
{
    [Fact]
    public void La_cible_de_l_HOMME_porte_sur_les_boissons()
    {
        // Apport adéquat EFSA : 2 500 ml d'eau TOTALE. Converti en boissons :
        //   70 % → 1 750 ml (borne basse, lecture prudente)
        //   80 % → 2 000 ml (borne haute, plancher ESPEN R61)
        var c = Hydratation.Calculer(Sexe.Homme);

        Assert.Equal(1750m, c.Basse.Millilitres);
        Assert.Equal(2000m, c.Haute.Millilitres);
    }

    [Fact]
    public void La_cible_de_la_FEMME_porte_sur_les_boissons()
    {
        // Apport adéquat EFSA : 2 000 ml d'eau totale → 1 400 à 1 600 ml.
        var c = Hydratation.Calculer(Sexe.Femme);

        Assert.Equal(1400m, c.Basse.Millilitres);
        Assert.Equal(1600m, c.Haute.Millilitres);
    }

    [Fact]
    public void L_ANCIENNE_cible_depassait_l_apport_adequat_TOUT_COMPRIS()
    {
        // LA MESURE QUI JUSTIFIE LA CORRECTION. Pour 73 kg, l'ancien calcul
        // demandait 35 × 73 = 2 555 ml de BOISSONS, quand l'apport adéquat
        // masculin est de 2 500 ml d'eau TOTALE, aliments compris.
        //
        // La cible dépassait donc de plus d'un tiers ce que l'EFSA pose une
        // fois ramenée au même périmètre.
        const decimal ancienneCible = 35m * 73m;

        Assert.True(ancienneCible > Hydratation.EauTotaleHommeMl);

        var c = Hydratation.Calculer(Sexe.Homme);
        Assert.True(
            ancienneCible > c.Haute.Millilitres * 1.25m,
            $"L'ancienne cible ({ancienneCible} ml) devrait dépasser d'au moins un quart "
                + $"la nouvelle borne haute ({c.Haute.Millilitres} ml)."
        );
    }

    [Fact]
    public void L_eau_des_ALIMENTS_est_rendue_pour_etre_dite_jamais_ajoutee()
    {
        // Un utilisateur qui a lu « 2,5 litres par jour » ailleurs se croirait
        // en déficit permanent si l'écran ne disait pas ce que le compteur ne
        // compte pas.
        var c = Hydratation.Calculer(Sexe.Femme);

        Assert.Equal(875m, c.EauDesAlimentsMl.Millilitres);

        // Et la somme retombe bien sur l'apport adéquat : la conversion est
        // cohérente, pas approximative.
        var totalReconstitue = c.Haute.Millilitres + c.EauDesAlimentsMl.Millilitres;
        Assert.True(
            totalReconstitue > Hydratation.EauTotaleFemmeMl,
            "Boissons hautes plus eau des aliments devrait couvrir l'apport adéquat."
        );
    }

    [Fact]
    public void La_cible_ne_depend_QUE_du_sexe()
    {
        // Le sexe est la SEULE variable que l'EFSA et l'ANSES retiennent chez
        // l'adulte. Ni le poids, ni la taille, ni l'âge — et c'est pourquoi la
        // signature n'en prend aucun autre.
        var premier = Hydratation.Calculer(Sexe.Homme);
        var second = Hydratation.Calculer(Sexe.Homme);

        Assert.Equal(premier, second);
        Assert.NotEqual(premier.Basse.Millilitres, Hydratation.Calculer(Sexe.Femme).Basse.Millilitres);
    }

    [Fact]
    public void Les_DEUX_parts_de_boissons_encadrent_la_fourchette_de_l_ESPEN()
    {
        // « drinks or beverages account for 70 to 80 % of fluid consumed ». La
        // fourchette est reportée plutôt que refermée sur son sommet.
        Assert.Equal(0.70m, Hydratation.PartDesBoissonsBasse);
        Assert.Equal(0.80m, Hydratation.PartDesBoissonsHaute);
        Assert.True(Hydratation.PartDesBoissonsBasse < Hydratation.PartDesBoissonsHaute);
    }

    [Fact]
    public void Le_garde_fou_haut_est_un_DEBIT_et_non_un_total_journalier()
    {
        // Il n'existe AUCUNE limite haute journalière pour l'eau : « No maximum
        // daily amount of water that can be tolerated by a population group can
        // be defined » (EFSA), « a Tolerable Upper Intake Level was not set for
        // water » (IOM, 2005).
        //
        // Le rein excrète au plus 0,7 à 1,0 L/h ; la borne basse est retenue.
        Assert.Equal(700m, Hydratation.DebitRenalMaximalMlParHeure);

        // Et ce débit horaire dépasse largement la cible JOURNALIÈRE : le
        // confondre avec un plafond quotidien n'alerterait jamais.
        var c = Hydratation.Calculer(Sexe.Homme);
        Assert.True(Hydratation.DebitRenalMaximalMlParHeure * 24m > c.Haute.Millilitres * 5m);
    }

    [Fact]
    public void La_majoration_d_effort_reste_une_ESTIMATION_modeste()
    {
        // La sudation va de 0,3 à 2,4 L/h. Pour une séance de musculation en
        // salle, le bas de fourchette est l'ordre de grandeur plausible — et la
        // consigne de référence reste « boire à la soif ».
        Assert.Equal(500m, Hydratation.MajorationParHeureDEffortMl);
        Assert.True(Hydratation.MajorationParHeureDEffortMl < Hydratation.DebitRenalMaximalMlParHeure);
    }
}
