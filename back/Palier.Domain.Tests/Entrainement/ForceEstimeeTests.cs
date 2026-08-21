using Palier.Domain.Entrainement;
using Palier.Domain.Grandeurs;

namespace Palier.Domain.Tests.Entrainement;

/// <summary>
/// <c>docs/05-entrainement.md</c> § 3 : « au-delà de 15 [répétitions
/// effectives], ne rien afficher ». La fonction rend donc <c>null</c> plutôt
/// qu'une charge : un nombre rendu quand même serait indiscernable d'une
/// estimation valide.
///
/// La littérature confirme les seuils du document : erreur de prédiction sous
/// 0,03 entre 3 et 8 répétitions, exactitude maximale au 5RM, dégradation
/// au-delà de 10, et « beyond 12 reps, prediction error increases
/// significantly ».
/// </summary>
public sealed class ForceEstimeeTests
{
    [Fact]
    public void Epley_applique_la_formule_avec_le_RIR()
    {
        var r = ForceEstimee.Epley(Charge.DepuisKilogrammes(100m), Repetitions.De(8), Rir.De(2));

        // répétitions effectives 10 → 100 × (1 + 10/30) = 133,33…
        Assert.Equal(133.33m, Math.Round(r!.UnRepetitionMaximum.Kilogrammes, 2));
        Assert.Equal(Fiabilite.Moyenne, r.Fiabilite);
    }

    [Fact]
    public void Epley_suppose_un_RIR_de_deux_quand_il_manque()
    {
        var avec = ForceEstimee.Epley(Charge.DepuisKilogrammes(100m), Repetitions.De(8), Rir.De(2));
        var sans = ForceEstimee.Epley(Charge.DepuisKilogrammes(100m), Repetitions.De(8), null);

        Assert.Equal(avec!.UnRepetitionMaximum, sans!.UnRepetitionMaximum);
    }

    [Fact]
    public void Une_serie_courte_donne_une_bonne_fiabilite()
    {
        // 5 + 1 = 6 répétitions effectives
        var r = ForceEstimee.Epley(Charge.DepuisKilogrammes(120m), Repetitions.De(5), Rir.De(1));
        Assert.Equal(Fiabilite.Bonne, r!.Fiabilite);
    }

    [Fact]
    public void La_borne_de_huit_effectives_reste_bonne()
    {
        var r = ForceEstimee.Epley(Charge.DepuisKilogrammes(100m), Repetitions.De(6), Rir.De(2));
        Assert.Equal(Fiabilite.Bonne, r!.Fiabilite);
    }

    [Fact]
    public void Au_dela_de_douze_effectives_la_fiabilite_devient_faible()
    {
        // 12 + 2 = 14 répétitions effectives
        var r = ForceEstimee.Epley(Charge.DepuisKilogrammes(60m), Repetitions.De(12), Rir.De(2));
        Assert.Equal(Fiabilite.Faible, r!.Fiabilite);
    }

    [Fact]
    public void La_borne_de_douze_effectives_reste_moyenne()
    {
        var r = ForceEstimee.Epley(Charge.DepuisKilogrammes(60m), Repetitions.De(10), Rir.De(2));
        Assert.Equal(Fiabilite.Moyenne, r!.Fiabilite);
    }

    [Fact]
    public void Au_dela_de_quinze_effectives_rien_n_est_rendu()
    {
        // 14 + 2 = 16 répétitions effectives
        Assert.Null(ForceEstimee.Epley(Charge.DepuisKilogrammes(40m), Repetitions.De(14), Rir.De(2)));
    }

    [Fact]
    public void La_borne_de_quinze_est_incluse()
    {
        // 13 + 2 = 15 répétitions effectives : encore rendu
        var r = ForceEstimee.Epley(Charge.DepuisKilogrammes(40m), Repetitions.De(13), Rir.De(2));

        Assert.NotNull(r);
        Assert.Equal(Fiabilite.Faible, r.Fiabilite);
    }

    // Le poids de corps est une charge légitime, mais 0 kg ne permet aucune
    // estimation de force : zéro multiplié par quoi que ce soit reste zéro, et
    // rendre 0 kg de 1RM serait un chiffre faux plutôt qu'une absence.
    [Fact]
    public void Epley_refuse_une_charge_nulle()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => ForceEstimee.Epley(Charge.DepuisKilogrammes(0m), Repetitions.De(5), Rir.De(2)));
    }

    [Fact]
    public void Les_seuils_de_fiabilite_sont_exposes()
    {
        Assert.Equal(8, ForceEstimee.RepetitionsEffectivesFiables);
        Assert.Equal(12, ForceEstimee.RepetitionsEffectivesMoyennes);
        Assert.Equal(15, ForceEstimee.RepetitionsEffectivesMaximum);
    }
}
