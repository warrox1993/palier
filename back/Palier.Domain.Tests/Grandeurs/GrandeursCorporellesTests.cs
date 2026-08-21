using Palier.Domain.Grandeurs;

namespace Palier.Domain.Tests.Grandeurs;

/// <summary>
/// Les bornes diffèrent d'une grandeur à l'autre, et c'est le motif du choix
/// « un type par grandeur » de la spec du lot 3 § 5 : un poids corporel plafonne
/// là où aucun humain n'a jamais été pesé, une charge de barre monte bien plus
/// haut. Un type générique unique aurait dû trancher pour un plafond commun,
/// donc faux pour les deux.
/// </summary>
public sealed class GrandeursCorporellesTests
{
    [Fact]
    public void Masse_conserve_la_valeur_donnee()
    {
        Assert.Equal(73.5m, Masse.DepuisKilogrammes(73.5m).Kilogrammes);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(500.1)]
    public void Masse_refuse_hors_bornes(decimal kilogrammes)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Masse.DepuisKilogrammes(kilogrammes));
    }

    [Fact]
    public void Masse_accepte_la_borne_haute()
    {
        Assert.Equal(500m, Masse.DepuisKilogrammes(500m).Kilogrammes);
    }

    [Fact]
    public void Taille_expose_les_centimetres_et_les_metres()
    {
        var t = Taille.DepuisCentimetres(178m);
        Assert.Equal(178m, t.Centimetres);
        Assert.Equal(1.78m, t.Metres);
    }

    [Theory]
    [InlineData(29.9)]
    [InlineData(300.1)]
    public void Taille_refuse_hors_bornes(decimal centimetres)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Taille.DepuisCentimetres(centimetres));
    }

    [Fact]
    public void Taille_accepte_les_bornes()
    {
        Assert.Equal(30m, Taille.DepuisCentimetres(30m).Centimetres);
        Assert.Equal(300m, Taille.DepuisCentimetres(300m).Centimetres);
    }

    [Fact]
    public void Age_accepte_zero()
    {
        Assert.Equal(0m, Age.DepuisAnnees(0m).Annees);
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(130.1)]
    public void Age_refuse_hors_bornes(decimal annees)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Age.DepuisAnnees(annees));
    }

    [Fact]
    public void Age_accepte_la_borne_haute()
    {
        Assert.Equal(130m, Age.DepuisAnnees(130m).Annees);
    }
}
