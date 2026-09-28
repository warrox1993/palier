using Palier.Application.Entrainement;

namespace Palier.Application.Tests;

/// <summary>
/// La validation d'une pesée et le bornage de la fenêtre — sans base.
/// </summary>
public sealed class EntrainementPoidsTests
{
    private static readonly DateOnly _aujourdHui = new(2026, 8, 23);

    // ================================================================
    // Le poids
    // ================================================================

    [Theory]
    [InlineData(0.1)]
    [InlineData(82.4)]
    [InlineData(500)]
    public void Un_poids_DANS_les_bornes_passe(decimal kilogrammes) =>
        Assert.Null(new MesureDePoids(null, kilogrammes).Faute(_aujourdHui));

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(500.1)]
    public void Un_poids_HORS_des_bornes_est_refuse(decimal kilogrammes) =>
        // Zéro est refusé, contrairement à une charge : un corps qui ne pèse
        // rien n'existe pas. Les bornes viennent de `Masse`, elles ne sont pas
        // recopiées ici.
        Assert.Equal("PoidsInvalide", new MesureDePoids(null, kilogrammes).Faute(_aujourdHui));

    // ================================================================
    // La date
    // ================================================================

    [Fact]
    public void Une_pesee_SANS_date_passe() =>
        // Absent, c'est aujourd'hui. Exiger la date obligerait chaque client à
        // décider d'un fuseau, et deux clients en décideraient différemment.
        Assert.Null(new MesureDePoids(null, 82.4m).Faute(_aujourdHui));

    [Fact]
    public void Une_pesee_D_AUJOURD_HUI_passe() =>
        Assert.Null(new MesureDePoids(_aujourdHui, 82.4m).Faute(_aujourdHui));

    [Fact]
    public void Une_pesee_PASSEE_passe() =>
        // Saisir après coup est légitime : on rattrape une semaine oubliée.
        Assert.Null(new MesureDePoids(_aujourdHui.AddDays(-30), 82.4m).Faute(_aujourdHui));

    [Fact]
    public void Une_pesee_DANS_LE_FUTUR_est_refusee() =>
        // Ce n'est pas du zèle. La détection de perte rapide compare des
        // valeurs consécutives dans le temps : une date future les réordonne,
        // et une faute de frappe sur l'année suffirait à faire passer la
        // dernière pesée en tête de série, donc à inverser le sens de la
        // variation.
        Assert.Equal(
            "JourDansLeFutur",
            new MesureDePoids(_aujourdHui.AddDays(1), 82.4m).Faute(_aujourdHui)
        );

    [Fact]
    public void Le_POIDS_est_contrôlé_AVANT_la_date()
    {
        // Les deux fautes à la fois : c'est le poids qui sort. L'épreuve fige
        // l'ordre — sans elle, une réorganisation changerait le code rendu à
        // l'utilisateur sans que rien ne rougisse, et le front traduirait
        // autre chose que la cause réelle.
        var fautive = new MesureDePoids(_aujourdHui.AddDays(1), -5m);
        Assert.Equal("PoidsInvalide", fautive.Faute(_aujourdHui));
    }

    // ================================================================
    // La fenêtre de lecture
    // ================================================================

    [Fact]
    public void Une_fenetre_ABSENTE_prend_la_valeur_par_defaut() =>
        Assert.Equal(SerieDePoids.FenetreParDefaut, SerieDePoids.BornerLaFenetre(null));

    [Fact]
    public void Une_fenetre_DEMESUREE_est_ramenee_au_plafond() =>
        Assert.Equal(SerieDePoids.FenetreMaximale, SerieDePoids.BornerLaFenetre(100_000));

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Une_fenetre_NULLE_OU_NEGATIVE_est_ramenee_a_un(int jours) =>
        // Zéro rendrait une série vide en permanence, et l'écran de courbe
        // afficherait « aucune donnée » à quelqu'un qui pèse tous les jours.
        Assert.Equal(1, SerieDePoids.BornerLaFenetre(jours));

    [Fact]
    public void La_fenetre_par_defaut_couvre_les_quatre_semaines_de_la_DETECTION() =>
        // Le lien qui n'est écrit nulle part ailleurs : la détection a besoin
        // de quatre valeurs hebdomadaires, donc de 28 jours au moins. Une
        // fenêtre par défaut plus courte rendrait le constat structurellement
        // impossible à produire, sans qu'aucune épreuve ne rougisse.
        Assert.True(SerieDePoids.FenetreParDefaut >= 28);

    // ================================================================
    // La forme rendue
    // ================================================================

    [Fact]
    public void Une_serie_SANS_constat_est_l_etat_ordinaire()
    {
        var serie = new SerieDePoids([new PoidsRendu(_aujourdHui, 82.4m)], null);

        Assert.Single(serie.Mesures);
        Assert.Equal(_aujourdHui, serie.Mesures[0].Jour);
        Assert.Equal(82.4m, serie.Mesures[0].PoidsKg);
        Assert.Null(serie.Constat);
    }

    [Fact]
    public void Un_constat_est_un_CODE_et_non_une_phrase()
    {
        // `01-conformite.md` sépare informer de prescrire, et le libellé vit en
        // base, versionné et validé. Une phrase écrite dans l'API échapperait à
        // cette validation ET à i18next — sur le sujet exact où le document
        // interdit « tout renforcement de la restriction ».
        var serie = new SerieDePoids([], SerieDePoids.PerteRapide);

        Assert.Equal("PerteRapide", serie.Constat);
        Assert.DoesNotContain(" ", serie.Constat, StringComparison.Ordinal);
    }

    [Fact]
    public void Une_serie_VIDE_est_un_etat_legitime() =>
        // « Je n'ai pas encore pesé » est une réponse, pas une erreur.
        Assert.Empty(new SerieDePoids([], null).Mesures);
}
