using Palier.Domain.Grandeurs;

namespace Palier.Domain.Tests.Grandeurs;

/// <summary>
/// L'unité est portée, jamais supposée. Une limite haute s'exprime en mg pour
/// le zinc et en µg pour le sélénium ; additionner sans convertir se trompe
/// d'un facteur mille, et sur une comparaison à une limite de sécurité cette
/// erreur se voit chez l'utilisateur.
/// </summary>
public sealed class NutrimentsTests
{
    [Fact]
    public void Nutriment_conserve_sa_valeur_et_son_unite()
    {
        var n = MasseNutriment.De(17.2m, UniteNutriment.Milligramme);
        Assert.Equal(17.2m, n.Valeur);
        Assert.Equal(UniteNutriment.Milligramme, n.Unite);
    }

    [Fact]
    public void Nutriment_convertit_les_grammes_en_milligrammes()
    {
        var n = MasseNutriment.De(1m, UniteNutriment.Gramme);
        Assert.Equal(1000m, n.ConvertieEn(UniteNutriment.Milligramme).Valeur);
    }

    [Fact]
    public void Nutriment_convertit_les_microgrammes_en_milligrammes()
    {
        var n = MasseNutriment.De(500m, UniteNutriment.Microgramme);
        Assert.Equal(0.5m, n.ConvertieEn(UniteNutriment.Milligramme).Valeur);
    }

    [Fact]
    public void Nutriment_convertit_vers_sa_propre_unite_sans_rien_changer()
    {
        var n = MasseNutriment.De(42m, UniteNutriment.Milligramme);
        Assert.Equal(42m, n.ConvertieEn(UniteNutriment.Milligramme).Valeur);
    }

    [Fact]
    public void Nutriment_refuse_une_unite_cible_hors_enumeration()
    {
        var n = MasseNutriment.De(1m, UniteNutriment.Gramme);
        Assert.Throws<ArgumentOutOfRangeException>(() => n.ConvertieEn((UniteNutriment)99));
    }

    // L'exemple du zinc de docs/04-nutrition.md § 4 : 17,2 mg d'alimentation et
    // 25 mg de compléments font 42,2 mg. Ici les compléments arrivent en µg,
    // ce qui est le cas réel d'une étiquette.
    [Fact]
    public void Nutriment_additionne_dans_l_unite_la_plus_fine()
    {
        var somme = MasseNutriment.Somme(
            MasseNutriment.De(17.2m, UniteNutriment.Milligramme),
            MasseNutriment.De(25000m, UniteNutriment.Microgramme));
        Assert.Equal(UniteNutriment.Microgramme, somme.Unite);
        Assert.Equal(42200m, somme.Valeur);
    }

    [Fact]
    public void Nutriment_additionne_deux_valeurs_de_meme_unite()
    {
        var somme = MasseNutriment.Somme(
            MasseNutriment.De(10m, UniteNutriment.Milligramme),
            MasseNutriment.De(5m, UniteNutriment.Milligramme));
        Assert.Equal(UniteNutriment.Milligramme, somme.Unite);
        Assert.Equal(15m, somme.Valeur);
    }

    [Fact]
    public void Nutriment_additionne_en_partant_de_la_plus_fine()
    {
        var somme = MasseNutriment.Somme(
            MasseNutriment.De(500m, UniteNutriment.Microgramme),
            MasseNutriment.De(1m, UniteNutriment.Gramme));
        Assert.Equal(UniteNutriment.Microgramme, somme.Unite);
        Assert.Equal(1000500m, somme.Valeur);
    }

    [Fact]
    public void Nutriment_accepte_zero()
    {
        Assert.Equal(0m, MasseNutriment.De(0m, UniteNutriment.Gramme).Valeur);
    }

    [Fact]
    public void Nutriment_refuse_une_valeur_negative()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => MasseNutriment.De(-1m, UniteNutriment.Gramme));
    }

    [Fact]
    public void Nutriment_refuse_une_unite_hors_enumeration()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => MasseNutriment.De(1m, (UniteNutriment)99));
    }

    [Fact]
    public void VolumeEau_conserve_la_valeur_donnee()
    {
        Assert.Equal(2555m, VolumeEau.DepuisMillilitres(2555m).Millilitres);
    }

    [Fact]
    public void VolumeEau_accepte_zero()
    {
        Assert.Equal(0m, VolumeEau.DepuisMillilitres(0m).Millilitres);
    }

    [Fact]
    public void VolumeEau_refuse_une_valeur_negative()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => VolumeEau.DepuisMillilitres(-1m));
    }
}
