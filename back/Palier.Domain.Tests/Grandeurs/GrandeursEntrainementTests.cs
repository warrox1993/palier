using Palier.Domain.Grandeurs;

namespace Palier.Domain.Tests.Grandeurs;

public sealed class GrandeursEntrainementTests
{
    [Fact]
    public void Charge_conserve_la_valeur_donnee()
    {
        Assert.Equal(62.5m, Charge.DepuisKilogrammes(62.5m).Kilogrammes);
    }

    // Le poids de corps est une charge légitime : une traction ou une pompe se
    // note à 0 kg ajouté. Masse, elle, refuse zéro — un corps qui ne pèse rien
    // n'existe pas. C'est la démonstration que les deux grandeurs n'ont pas le
    // même domaine de validité, et qu'un type générique les aurait confondues.
    [Fact]
    public void Charge_accepte_zero()
    {
        Assert.Equal(0m, Charge.DepuisKilogrammes(0m).Kilogrammes);
    }

    [Theory]
    [InlineData(-0.5)]
    [InlineData(1000.1)]
    public void Charge_refuse_hors_bornes(decimal kilogrammes)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Charge.DepuisKilogrammes(kilogrammes));
    }

    [Fact]
    public void Charge_accepte_la_borne_haute()
    {
        Assert.Equal(1000m, Charge.DepuisKilogrammes(1000m).Kilogrammes);
    }

    [Fact]
    public void Repetitions_conserve_la_valeur_donnee()
    {
        Assert.Equal(8, Repetitions.De(8).Nombre);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1001)]
    public void Repetitions_refuse_hors_bornes(int nombre)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Repetitions.De(nombre));
    }

    [Fact]
    public void Rir_conserve_la_valeur_donnee()
    {
        Assert.Equal(3, Rir.De(3).Nombre);
    }

    [Fact]
    public void Rir_accepte_zero_qui_est_l_echec_musculaire()
    {
        Assert.Equal(0, Rir.De(0).Nombre);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(11)]
    public void Rir_refuse_hors_bornes(int nombre)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Rir.De(nombre));
    }

    // docs/05-entrainement.md § 3 : « si le RIR n'est pas renseigné, supposer 2
    // — hypothèse prudente ». L'ACSM 2026 en fait la recommandation de
    // référence : 2 à 3 répétitions en réserve produisent les mêmes gains que
    // l'échec absolu, avec moins de fatigue et moins de risque de blessure.
    [Fact]
    public void Rir_par_defaut_vaut_deux()
    {
        Assert.Equal(2, Rir.ParDefaut.Nombre);
    }
}
