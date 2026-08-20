using Palier.Domain.Energie;

namespace Palier.Domain.Tests.Energie;

public sealed class MetabolismeDeBaseTests
{
    // docs/04-nutrition.md § 1 :
    // Homme : 10 × poids + 6,25 × taille − 5 × âge + 5
    // Femme : 10 × poids + 6,25 × taille − 5 × âge − 161
    [Fact]
    public void MifflinStJeor_calcule_la_valeur_pour_un_homme()
    {
        var r = MetabolismeDeBase.MifflinStJeor(Sexe.Homme, poidsKg: 73m, tailleCm: 178m, age: 35m);
        // 730 + 1112,5 − 175 + 5 = 1672,5
        Assert.Equal(1672.5m, r);
    }

    [Fact]
    public void MifflinStJeor_calcule_la_valeur_pour_une_femme()
    {
        var r = MetabolismeDeBase.MifflinStJeor(Sexe.Femme, poidsKg: 60m, tailleCm: 165m, age: 30m);
        // 600 + 1031,25 − 150 − 161 = 1320,25
        Assert.Equal(1320.25m, r);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void MifflinStJeor_refuse_un_poids_non_positif(int poids)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => MetabolismeDeBase.MifflinStJeor(Sexe.Homme, poids, 178m, 35m));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void MifflinStJeor_refuse_une_taille_non_positive(int taille)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => MetabolismeDeBase.MifflinStJeor(Sexe.Homme, 73m, taille, 35m));
    }

    [Fact]
    public void MifflinStJeor_refuse_un_age_negatif()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => MetabolismeDeBase.MifflinStJeor(Sexe.Homme, 73m, 178m, -1m));
    }

    // Le bras par défaut de la sélection n'est atteignable que par une valeur
    // d'énumération forgée. Sans ce test, il reste une ligne et une branche non
    // couvertes : le domaine plafonnait à 91,66 % de lignes et 75 % de branches,
    // et le seuil de 100 % exigé par docs/08-workflow.md § 6 refusait le domaine
    // réel. Ce n'est pas un artifice de couverture — c'est la garde qui répond
    // quand une valeur hors énumération traverse une frontière de sérialisation.
    [Fact]
    public void MifflinStJeor_refuse_un_sexe_hors_enumeration()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => MetabolismeDeBase.MifflinStJeor((Sexe)99, 73m, 178m, 35m));
    }
}
