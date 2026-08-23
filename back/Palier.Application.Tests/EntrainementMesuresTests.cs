using Palier.Application.Entrainement;

namespace Palier.Application.Tests;

/// <summary>
/// Les contrats de la mesure — progression et volume.
/// </summary>
public sealed class EntrainementMesuresTests
{
    // ================================================================
    // La fenêtre du volume
    // ================================================================

    [Fact]
    public void Un_nombre_de_semaines_ABSENT_prend_la_valeur_par_defaut() =>
        Assert.Equal(BilanDeVolume.SemainesParDefaut, BilanDeVolume.BornerLesSemaines(null));

    [Fact]
    public void Un_nombre_de_semaines_DEMESURE_est_ramene_au_plafond() =>
        // Sans plafond, `?semaines=100000` remonterait toute la vue en mémoire.
        // La vue agrège, donc elle rend moins de lignes qu'une table — mais
        // « moins » n'est pas « peu » sur plusieurs années et vingt muscles.
        Assert.Equal(BilanDeVolume.SemainesMaximales, BilanDeVolume.BornerLesSemaines(100_000));

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void Un_nombre_de_semaines_NUL_OU_NEGATIF_est_ramene_a_un(int semaines) =>
        Assert.Equal(1, BilanDeVolume.BornerLesSemaines(semaines));

    [Fact]
    public void Un_nombre_de_semaines_RAISONNABLE_passe_tel_quel() =>
        Assert.Equal(12, BilanDeVolume.BornerLesSemaines(12));

    // ================================================================
    // La progression
    // ================================================================

    [Fact]
    public void Une_progression_SANS_donnee_est_un_etat_legitime()
    {
        // Zéro série retenue, aucune estimation, aucun plateau. C'est l'état
        // vide de `11-qualite.md`, et il se distingue de « l'exercice n'existe
        // pas », qui est un 404.
        var vide = new ProgressionRendue(Guid.NewGuid(), null, null, EnPlateau: false, 0);

        Assert.Null(vide.UnRepetitionMaximumKg);
        Assert.Null(vide.Fiabilite);
        Assert.False(vide.EnPlateau);
        Assert.Equal(0, vide.SeriesRetenues);
    }

    [Fact]
    public void La_fiabilite_voyage_AVEC_l_estimation()
    {
        // Les deux ensemble, toujours. Une force estimée sans sa fiabilité
        // invite à la lire comme une mesure — et `05-entrainement.md` § 3
        // rappelle qu'« aucun test de charge maximale n'est jamais proposé par
        // le produit » : l'estimation existe pour ÉVITER le test.
        var exercice = Guid.NewGuid();
        var progression = new ProgressionRendue(exercice, 102.5m, "Bonne", EnPlateau: true, 12);

        Assert.Equal(exercice, progression.ExerciceId);
        Assert.Equal(102.5m, progression.UnRepetitionMaximumKg);
        Assert.Equal("Bonne", progression.Fiabilite);
        Assert.True(progression.EnPlateau);
        Assert.Equal(12, progression.SeriesRetenues);
    }

    // ================================================================
    // Le volume
    // ================================================================

    [Fact]
    public void Un_volume_de_muscle_accepte_les_DEMI_series() =>
        // Un muscle secondaire compte pour 0,5 — c'est la vue `weekly_volume`
        // qui l'applique. Un entier aurait tronqué, donc sous-compté tout
        // travail indirect.
        Assert.Equal(7.5m, new VolumeDUnMuscle("pectoraux", 7.5m).SeriesDures);

    [Fact]
    public void Une_semaine_de_volume_porte_son_lundi_et_ses_muscles()
    {
        var lundi = new DateOnly(2026, 8, 17);
        var semaine = new SemaineDeVolume(
            lundi,
            [new VolumeDUnMuscle("dos", 12m), new VolumeDUnMuscle("pectoraux", 9m)]
        );

        Assert.Equal(lundi, semaine.Semaine);
        Assert.Equal(2, semaine.Muscles.Count);
        Assert.Equal("dos", semaine.Muscles[0].Muscle);
    }

    [Fact]
    public void Un_bilan_VIDE_est_un_etat_legitime() =>
        // Quelqu'un qui n'a pas encore d'entraînement n'est pas une erreur.
        Assert.Empty(new BilanDeVolume([]).Semaines);
}
