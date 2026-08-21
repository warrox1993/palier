using Palier.Domain.Grandeurs;

namespace Palier.Domain.Tests.Grandeurs;

/// <summary>
/// La provenance de la mesure voyage avec la valeur, et ce n'est pas un
/// ornement : la spec du lot 3 § 3 réserve Katch-McArdle aux mesures fiables,
/// parce qu'une balance à impédance dévie en moyenne de 4,4 points sous la DXA
/// et que l'erreur se propage à environ 16 kcal par point. Si la provenance
/// vivait à côté de la valeur, un appelant pourrait l'oublier ; ici il ne peut
/// pas construire l'une sans l'autre.
/// </summary>
public sealed class EnergieEtCompositionTests
{
    [Fact]
    public void Energie_accepte_zero()
    {
        Assert.Equal(0m, Energie.DepuisKilocalories(0m).Kilocalories);
    }

    [Fact]
    public void Energie_refuse_une_valeur_negative()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Energie.DepuisKilocalories(-1m));
    }

    [Fact]
    public void MasseGrasse_calcule_la_masse_maigre()
    {
        var masseGrasse = PourcentageMasseGrasse.De(20m, ProvenanceMesure.MesureFiable);
        var maigre = masseGrasse.MasseMaigreDe(Masse.DepuisKilogrammes(75m));
        Assert.Equal(60m, maigre.Kilogrammes);
    }

    [Fact]
    public void MasseGrasse_conserve_la_provenance()
    {
        var masseGrasse = PourcentageMasseGrasse.De(22m, ProvenanceMesure.Impedancemetrie);
        Assert.Equal(ProvenanceMesure.Impedancemetrie, masseGrasse.Provenance);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    [InlineData(-1)]
    public void MasseGrasse_refuse_hors_bornes(decimal pourcentage)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => PourcentageMasseGrasse.De(pourcentage, ProvenanceMesure.MesureFiable));
    }

    [Fact]
    public void MasseGrasse_refuse_une_provenance_hors_enumeration()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => PourcentageMasseGrasse.De(20m, (ProvenanceMesure)99));
    }
}
