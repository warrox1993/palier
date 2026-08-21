using Palier.Domain.Entrainement;
using Palier.Domain.Grandeurs;

namespace Palier.Domain.Tests.Entrainement;

/// <summary>
/// <c>docs/05-entrainement.md</c> § 2, mot pour mot : « même charge maximale sur
/// trois séances consécutives sans progression du nombre de répétitions ». Les
/// deux conditions sont nécessaires — une charge identique dont les répétitions
/// montent est une progression, pas un plateau.
///
/// Le domaine constate, il ne prescrit pas : le document précise que le système
/// « rappelle les leviers disponibles et laisse l'utilisateur décider ».
/// </summary>
public sealed class DetectionPlateauTests
{
    private static SeanceDExercice Seance(int jour, decimal charge, int repetitions) =>
        new(new DateOnly(2026, 8, jour), Charge.DepuisKilogrammes(charge), repetitions);

    [Fact]
    public void Trois_seances_identiques_sont_un_plateau()
    {
        Assert.True(
            DetectionPlateau.EstEnPlateau(
                [Seance(10, 60m, 8), Seance(14, 60m, 8), Seance(18, 60m, 8)]));
    }

    [Fact]
    public void Une_progression_des_repetitions_n_est_pas_un_plateau()
    {
        Assert.False(
            DetectionPlateau.EstEnPlateau(
                [Seance(10, 60m, 8), Seance(14, 60m, 9), Seance(18, 60m, 10)]));
    }

    [Fact]
    public void Une_progression_de_charge_n_est_pas_un_plateau()
    {
        Assert.False(
            DetectionPlateau.EstEnPlateau(
                [Seance(10, 60m, 8), Seance(14, 62.5m, 8), Seance(18, 65m, 8)]));
    }

    [Fact]
    public void Seules_les_trois_dernieres_seances_comptent()
    {
        Assert.True(
            DetectionPlateau.EstEnPlateau(
                [Seance(2, 50m, 6), Seance(10, 60m, 8), Seance(14, 60m, 8), Seance(18, 60m, 8)]));
    }

    // « Sans progression » couvre l'égalité ET la baisse : la charge stagne et
    // les répétitions ne montent pas.
    [Fact]
    public void Un_recul_des_repetitions_est_aussi_un_plateau()
    {
        Assert.True(
            DetectionPlateau.EstEnPlateau(
                [Seance(10, 60m, 9), Seance(14, 60m, 8), Seance(18, 60m, 8)]));
    }

    [Fact]
    public void Une_charge_qui_baisse_n_est_pas_un_plateau()
    {
        Assert.False(
            DetectionPlateau.EstEnPlateau(
                [Seance(10, 60m, 8), Seance(14, 60m, 8), Seance(18, 55m, 8)]));
    }

    [Fact]
    public void Moins_de_trois_seances_ne_conclut_pas()
    {
        Assert.False(DetectionPlateau.EstEnPlateau([Seance(14, 60m, 8), Seance(18, 60m, 8)]));
    }

    [Fact]
    public void Une_liste_vide_ne_conclut_pas()
    {
        Assert.False(DetectionPlateau.EstEnPlateau([]));
    }

    // Une collection remontée sans ORDER BY arrive dans un ordre quelconque.
    // Sans tri, « les trois dernières » désignerait les trois derniers éléments
    // de la liste et non les trois séances les plus récentes : ici, la séance
    // du 2 août, où la charge était plus basse, ferait conclure à l'absence de
    // plateau alors que les trois vraies dernières stagnent.
    [Fact]
    public void Les_seances_sont_triees_avant_d_etre_jugees()
    {
        Assert.True(
            DetectionPlateau.EstEnPlateau(
                [Seance(18, 60m, 8), Seance(2, 50m, 6), Seance(10, 60m, 8), Seance(14, 60m, 8)]));
    }

    [Fact]
    public void Le_nombre_de_seances_consecutives_est_expose()
    {
        Assert.Equal(3, DetectionPlateau.SeancesConsecutives);
    }
}
