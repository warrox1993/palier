using Palier.Domain.Entrainement;

namespace Palier.Domain.Tests.Entrainement;

/// <summary>
/// Les deux références arrivent en paramètre, jamais en constante. La cible de
/// séries vient du Position Stand 2026 de l'ACSM — environ dix séries
/// hebdomadaires par groupe, chaque groupe travaillé au moins deux fois par
/// semaine. Le ratio tirage/poussée de 1,3, lui, est un repère d'équilibre
/// délibérément déclassé : la littérature va de 1:1 à 3:1 et débat de la
/// pertinence même d'un ratio fixe, si bien que le produit ne dira jamais qu'un
/// ratio inférieur expose à une blessure.
/// </summary>
public sealed class VolumeParGroupeTests
{
    private static readonly DateOnly _fin = new(2026, 8, 21);

    private static SerieEffectuee S(string groupe, RoleMouvement role, int joursAvant) =>
        new(groupe, role, _fin.AddDays(-joursAvant));

    [Fact]
    public void Les_series_sont_comptees_par_groupe()
    {
        var b = VolumeParGroupe.SurSeptJours(
            [
                S("dos", RoleMouvement.Tirage, 0),
                S("dos", RoleMouvement.Tirage, 2),
                S("pectoraux", RoleMouvement.Poussee, 1),
            ],
            _fin,
            10,
            1.3m);

        Assert.Equal(2, b.SeriesParGroupe["dos"]);
        Assert.Equal(1, b.SeriesParGroupe["pectoraux"]);
    }

    [Fact]
    public void Une_serie_hors_fenetre_de_sept_jours_est_ignoree()
    {
        var b = VolumeParGroupe.SurSeptJours([S("dos", RoleMouvement.Tirage, 7)], _fin, 10, 1.3m);
        Assert.Empty(b.SeriesParGroupe);
    }

    [Fact]
    public void La_borne_de_six_jours_reste_dans_la_fenetre()
    {
        var b = VolumeParGroupe.SurSeptJours([S("dos", RoleMouvement.Tirage, 6)], _fin, 10, 1.3m);
        Assert.Equal(1, b.SeriesParGroupe["dos"]);
    }

    [Fact]
    public void Une_serie_posterieure_a_la_fenetre_est_ignoree()
    {
        var b = VolumeParGroupe.SurSeptJours([S("dos", RoleMouvement.Tirage, -1)], _fin, 10, 1.3m);
        Assert.Empty(b.SeriesParGroupe);
    }

    [Fact]
    public void Le_ratio_tirage_poussee_est_calcule()
    {
        var series = Enumerable
            .Range(0, 13)
            .Select(_ => S("dos", RoleMouvement.Tirage, 1))
            .Concat(Enumerable.Range(0, 10).Select(_ => S("pectoraux", RoleMouvement.Poussee, 1)))
            .ToArray();

        var b = VolumeParGroupe.SurSeptJours(series, _fin, 10, 1.3m);

        Assert.Equal(1.3m, b.RatioTiragePoussee);
        Assert.False(b.RatioSousLaCible);
    }

    [Fact]
    public void Un_ratio_sous_la_cible_est_signale()
    {
        var series = Enumerable
            .Range(0, 10)
            .Select(_ => S("dos", RoleMouvement.Tirage, 1))
            .Concat(Enumerable.Range(0, 10).Select(_ => S("pectoraux", RoleMouvement.Poussee, 1)))
            .ToArray();

        var b = VolumeParGroupe.SurSeptJours(series, _fin, 10, 1.3m);

        Assert.Equal(1m, b.RatioTiragePoussee);
        Assert.True(b.RatioSousLaCible);
    }

    // Sans ratio, il n'y a rien à comparer : le domaine ne dit pas « sous la
    // cible » d'une mesure qui n'existe pas.
    [Fact]
    public void Sans_ratio_rien_n_est_dit_de_la_cible()
    {
        var b = VolumeParGroupe.SurSeptJours([S("dos", RoleMouvement.Tirage, 1)], _fin, 10, 1.3m);

        Assert.Null(b.RatioTiragePoussee);
        Assert.Null(b.RatioSousLaCible);
    }

    // docs/05-entrainement.md § 4 : « le deltoïde latéral est exclu du calcul —
    // il ne tire ni ne pousse ». Il compte au volume, pas au ratio.
    [Fact]
    public void Le_deltoide_lateral_est_exclu_du_ratio()
    {
        var b = VolumeParGroupe.SurSeptJours(
            [
                S("dos", RoleMouvement.Tirage, 1),
                S("pectoraux", RoleMouvement.Poussee, 1),
                S("deltoide_lateral", RoleMouvement.NiTirageNiPoussee, 1),
            ],
            _fin,
            10,
            1.3m);

        Assert.Equal(1m, b.RatioTiragePoussee);
        Assert.Equal(1, b.SeriesParGroupe["deltoide_lateral"]);
    }

    // Une division par zéro n'est pas un ratio infini, c'est une absence de
    // mesure. Le produit ne peut rien dire d'un ratio qu'il n'a pas.
    [Fact]
    public void Sans_poussee_le_ratio_n_existe_pas()
    {
        var b = VolumeParGroupe.SurSeptJours([S("dos", RoleMouvement.Tirage, 1)], _fin, 10, 1.3m);
        Assert.Null(b.RatioTiragePoussee);
    }

    [Fact]
    public void Les_groupes_sous_la_cible_sont_nommes()
    {
        var series = Enumerable
            .Range(0, 10)
            .Select(_ => S("dos", RoleMouvement.Tirage, 1))
            .Append(S("pectoraux", RoleMouvement.Poussee, 1))
            .ToArray();

        var b = VolumeParGroupe.SurSeptJours(series, _fin, 10, 1.3m);

        Assert.DoesNotContain("dos", b.GroupesSousLaCible);
        Assert.Contains("pectoraux", b.GroupesSousLaCible);
    }

    [Fact]
    public void Une_semaine_vide_ne_fait_pas_echouer_le_bilan()
    {
        var b = VolumeParGroupe.SurSeptJours([], _fin, 10, 1.3m);

        Assert.Empty(b.SeriesParGroupe);
        Assert.Null(b.RatioTiragePoussee);
        Assert.Empty(b.GroupesSousLaCible);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Une_cible_non_positive_est_refusee(int cible)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => VolumeParGroupe.SurSeptJours([], _fin, cible, 1.3m));
    }

    [Fact]
    public void Un_ratio_cible_non_positif_est_refuse()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => VolumeParGroupe.SurSeptJours([], _fin, 10, 0m));
    }

    [Fact]
    public void Un_role_hors_enumeration_est_refuse()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => VolumeParGroupe.SurSeptJours([S("dos", (RoleMouvement)99, 1)], _fin, 10, 1.3m));
    }

    [Fact]
    public void Un_groupe_musculaire_vide_est_refuse()
    {
        Assert.Throws<ArgumentException>(
            () => VolumeParGroupe.SurSeptJours([S(" ", RoleMouvement.Tirage, 1)], _fin, 10, 1.3m));
    }
}
