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
}
