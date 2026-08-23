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

    // ================================================================
    // Demander sans lever
    // ================================================================
    //
    // POURQUOI CES TROIS PRÉDICATS EXISTENT. Une valeur hors bornes tapée par
    // un utilisateur n'est pas un défaut du programme : c'est une saisie, et
    // une saisie se refuse par un 400, pas par une exception qui remonte la
    // pile. Sans eux, l'adaptateur HTTP n'aurait que deux choix, tous deux
    // mauvais — attraper une `ArgumentOutOfRangeException` pour en faire un
    // flux normal, ou RECOPIER les bornes dans le point d'entrée, ce qui
    // dupliquerait la connaissance à l'endroit exact où elle doit être unique.
    //
    // La fabrique s'appuie SUR le prédicat : les bornes ne sont écrites qu'une
    // fois, et les épreuves ci-dessous le vérifient en croisant les deux.

    [Theory]
    [InlineData(0)]
    [InlineData(62.5)]
    [InlineData(1000)]
    public void Charge_valide_ce_que_la_fabrique_accepte(decimal kilogrammes)
    {
        Assert.True(Charge.EstValide(kilogrammes));
        Assert.Equal(kilogrammes, Charge.DepuisKilogrammes(kilogrammes).Kilogrammes);
    }

    [Theory]
    [InlineData(-0.5)]
    [InlineData(1000.1)]
    public void Charge_refuse_ce_que_la_fabrique_refuse(decimal kilogrammes)
    {
        // Les DEUX assertions, et c'est le point : un prédicat qui dirait
        // « valide » là où la fabrique lève, ou l'inverse, ferait rendre 400 à
        // une valeur acceptable ou 500 à une valeur refusée.
        Assert.False(Charge.EstValide(kilogrammes));
        Assert.Throws<ArgumentOutOfRangeException>(() => Charge.DepuisKilogrammes(kilogrammes));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(8)]
    [InlineData(1000)]
    public void Repetitions_valide_ce_que_la_fabrique_accepte(int nombre)
    {
        Assert.True(Repetitions.EstValide(nombre));
        Assert.Equal(nombre, Repetitions.De(nombre).Nombre);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1001)]
    public void Repetitions_refuse_ce_que_la_fabrique_refuse(int nombre)
    {
        Assert.False(Repetitions.EstValide(nombre));
        Assert.Throws<ArgumentOutOfRangeException>(() => Repetitions.De(nombre));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(10)]
    public void Rir_valide_ce_que_la_fabrique_accepte(int nombre)
    {
        Assert.True(Rir.EstValide(nombre));
        Assert.Equal(nombre, Rir.De(nombre).Nombre);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(11)]
    public void Rir_refuse_ce_que_la_fabrique_refuse(int nombre)
    {
        Assert.False(Rir.EstValide(nombre));
        Assert.Throws<ArgumentOutOfRangeException>(() => Rir.De(nombre));
    }
}
