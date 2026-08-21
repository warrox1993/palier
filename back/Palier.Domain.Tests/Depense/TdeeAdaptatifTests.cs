using Palier.Domain.Depense;
using Palier.Domain.Grandeurs;

namespace Palier.Domain.Tests.Depense;

/// <summary>
/// Le TDEE adaptatif mesure au lieu d'estimer, et c'est ce que
/// <c>docs/04-nutrition.md</c> § 1 appelle « le vrai différenciateur ». Il rend
/// <c>null</c> plutôt qu'un chiffre faible quand la fenêtre ne suffit pas : une
/// valeur approximative serait indiscernable d'une valeur mesurée une fois
/// sortie de la fonction.
/// </summary>
public sealed class TdeeAdaptatifTests
{
    private static FenetreDeMesure Fenetre(
        int jours = 21,
        int pesees = 15,
        int couverts = 21,
        decimal apport = 2400m,
        decimal variation = -0.5m) =>
        new(jours, pesees, couverts, Energie.DepuisKilocalories(apport), variation);

    [Fact]
    public void Calculer_infere_la_depense_sur_une_perte_de_poids()
    {
        var r = TdeeAdaptatif.Calculer(Fenetre());

        // 2400 − (−0,5 × 7700 / 21) = 2400 + 183,333…
        Assert.Equal(2583.33m, Math.Round(r!.Value.Kilocalories, 2));
    }

    [Fact]
    public void Calculer_infere_la_depense_sur_une_prise_de_poids()
    {
        var r = TdeeAdaptatif.Calculer(Fenetre(apport: 3000m, variation: 0.7m));

        // 3000 − (0,7 × 7700 / 21) = 3000 − 256,666…
        Assert.Equal(2743.33m, Math.Round(r!.Value.Kilocalories, 2));
    }

    [Fact]
    public void Calculer_rend_l_apport_moyen_quand_le_poids_ne_bouge_pas()
    {
        var r = TdeeAdaptatif.Calculer(Fenetre(variation: 0m));
        Assert.Equal(2400m, r!.Value.Kilocalories);
    }

    [Fact]
    public void Calculer_refuse_de_conclure_sous_quatorze_jours_de_saisie()
    {
        Assert.Null(TdeeAdaptatif.Calculer(Fenetre(jours: 13)));
    }

    [Fact]
    public void Calculer_accepte_exactement_quatorze_jours()
    {
        Assert.NotNull(TdeeAdaptatif.Calculer(Fenetre(jours: 14)));
    }

    [Fact]
    public void Calculer_refuse_de_conclure_sous_dix_pesees()
    {
        Assert.Null(TdeeAdaptatif.Calculer(Fenetre(pesees: 9)));
    }

    [Fact]
    public void Calculer_accepte_exactement_dix_pesees()
    {
        Assert.NotNull(TdeeAdaptatif.Calculer(Fenetre(pesees: 10)));
    }

    [Fact]
    public void Calculer_refuse_une_fenetre_sans_jour_couvert()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => TdeeAdaptatif.Calculer(Fenetre(couverts: 0)));
    }

    // Une prise de poids massive sur une courte fenêtre pousserait le calcul
    // sous zéro. Energie refuse le négatif, et le domaine doit le dire au lieu
    // de laisser une exception remonter d'une couche de construction.
    [Fact]
    public void Calculer_ne_rend_jamais_une_depense_negative()
    {
        Assert.Null(
            TdeeAdaptatif.Calculer(
                Fenetre(jours: 14, pesees: 10, couverts: 14, apport: 1000m, variation: 5m)));
    }

    // Le coefficient est exposé parce qu'il est contesté : la règle de
    // Wishnofsky ignore l'adaptation métabolique, et Hall a montré qu'en
    // PRÉDICTION elle surestime largement la perte. L'usage est ici
    // rétrospectif, ce qui est plus défendable — mais le rendre visible permet
    // de le corriger sans fouiller le code.
    [Fact]
    public void Le_coefficient_de_conversion_vaut_sept_mille_sept_cents()
    {
        Assert.Equal(7700m, TdeeAdaptatif.KilocaloriesParKilogramme);
    }

    [Fact]
    public void Les_seuils_de_declenchement_suivent_04_nutrition()
    {
        Assert.Equal(14, TdeeAdaptatif.JoursDeSaisieMinimum);
        Assert.Equal(10, TdeeAdaptatif.PeseesMinimum);
    }
}
