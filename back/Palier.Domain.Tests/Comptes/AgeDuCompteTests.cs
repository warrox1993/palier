using Palier.Domain.Comptes;

namespace Palier.Domain.Tests.Comptes;

/// <summary>
/// La règle d'âge de <c>docs/13-juridique.md</c> § 1.
/// </summary>
/// <remarks>
/// Chaque palier est provoqué, et les BORNES le sont des deux côtés : une règle
/// d'âge se trompe toujours d'un an, et c'est l'année d'écart qui laisse entrer
/// quelqu'un que le document refuse.
/// </remarks>
public sealed class AgeDuCompteTests
{
    private static readonly DateOnly _aujourdHui = new(2026, 8, 24);

    // ================================================================
    // L'âge révolu — l'anniversaire compte
    // ================================================================

    [Fact]
    public void L_anniversaire_NON_ECHU_retire_une_annee()
    {
        // LA FAUTE QUE CETTE MÉTHODE EXISTE POUR ÉVITER. Né le 31 décembre
        // 2010, on n'a pas seize ans le 24 août 2026 : on les aura le
        // 31 décembre. Soustraire les années donnerait seize et ouvrirait un
        // compte à quelqu'un de quinze ans.
        Assert.Equal(15, AgeDuCompte.AgeRevolu(new DateOnly(2010, 12, 31), _aujourdHui));
    }

    [Fact]
    public void L_anniversaire_ECHU_compte_l_annee()
    {
        Assert.Equal(16, AgeDuCompte.AgeRevolu(new DateOnly(2010, 8, 23), _aujourdHui));
    }

    [Fact]
    public void LE_JOUR_de_l_anniversaire_compte_l_annee()
    {
        // La borne exacte. Le jour de ses seize ans, on a seize ans.
        Assert.Equal(16, AgeDuCompte.AgeRevolu(new DateOnly(2010, 8, 24), _aujourdHui));
    }

    [Fact]
    public void Le_29_fevrier_ne_casse_pas_le_calcul()
    {
        // Né un 29 février, l'anniversaire tombe le 28 les années communes —
        // c'est ce que fait `AddYears`. 2026 n'est pas bissextile.
        Assert.Equal(18, AgeDuCompte.AgeRevolu(new DateOnly(2008, 2, 29), _aujourdHui));
    }

    // ================================================================
    // Les trois paliers du document
    // ================================================================

    [Fact]
    public void Moins_de_SEIZE_ans_est_REFUSE()
    {
        // « < 16 ans | Refus d'inscription »
        var naissance = _aujourdHui.AddYears(-16).AddDays(1);

        Assert.Equal(AccesDuCompte.Refuse, AgeDuCompte.Accorder(naissance, _aujourdHui));
    }

    [Fact]
    public void SEIZE_ans_JOUR_POUR_JOUR_ouvre_l_entrainement()
    {
        // La borne basse, exactement. Un jour de moins et c'est un refus.
        var naissance = _aujourdHui.AddYears(-16);

        Assert.Equal(AccesDuCompte.EntrainementSeul, AgeDuCompte.Accorder(naissance, _aujourdHui));
    }

    [Fact]
    public void DIX_SEPT_ans_reste_a_l_entrainement_SEUL()
    {
        // « 16-17 ans | Entraînement uniquement. Nutrition, poids et
        // progression corporelle désactivés »
        var naissance = _aujourdHui.AddYears(-17);

        Assert.Equal(AccesDuCompte.EntrainementSeul, AgeDuCompte.Accorder(naissance, _aujourdHui));
    }

    [Fact]
    public void La_VEILLE_des_dix_huit_ans_reste_a_l_entrainement_seul()
    {
        // La borne haute par en dessous. C'est ici qu'une règle d'âge se trompe.
        var naissance = _aujourdHui.AddYears(-18).AddDays(1);

        Assert.Equal(AccesDuCompte.EntrainementSeul, AgeDuCompte.Accorder(naissance, _aujourdHui));
    }

    [Fact]
    public void DIX_HUIT_ans_JOUR_POUR_JOUR_ouvre_TOUT()
    {
        // « ≥ 18 ans | Complet »
        var naissance = _aujourdHui.AddYears(-18);

        Assert.Equal(AccesDuCompte.Complet, AgeDuCompte.Accorder(naissance, _aujourdHui));
    }

    [Fact]
    public void Un_adulte_ordinaire_a_TOUT()
    {
        Assert.Equal(
            AccesDuCompte.Complet,
            AgeDuCompte.Accorder(new DateOnly(1990, 6, 15), _aujourdHui)
        );
    }

    // ================================================================
    // Ce qui n'est pas une date de naissance
    // ================================================================

    [Fact]
    public void Une_date_dans_le_FUTUR_est_nommee_INVALIDE()
    {
        // Elle produirait un âge négatif, donc un refus — le bon résultat pour
        // la mauvaise raison. Le message dirait « trop jeune » à quelqu'un qui
        // s'est trompé d'année, et il chercherait longtemps.
        Assert.Equal(
            AccesDuCompte.DateInvalide,
            AgeDuCompte.Accorder(_aujourdHui.AddDays(1), _aujourdHui)
        );
    }

    [Fact]
    public void AUJOURD_HUI_n_est_pas_dans_le_futur()
    {
        // La borne du futur, par en dessous : né aujourd'hui, on a zéro an —
        // refusé pour l'âge, pas pour la date.
        Assert.Equal(AccesDuCompte.Refuse, AgeDuCompte.Accorder(_aujourdHui, _aujourdHui));
    }

    [Fact]
    public void Une_date_ABSURDEMENT_ancienne_est_nommee_INVALIDE()
    {
        // Cent trente ans dépasse le record humain documenté : c'est une faute
        // de saisie, pas une longévité.
        Assert.Equal(
            AccesDuCompte.DateInvalide,
            AgeDuCompte.Accorder(new DateOnly(1850, 1, 1), _aujourdHui)
        );
    }

    [Fact]
    public void CENT_TRENTE_ans_passe_encore()
    {
        // La borne de l'absurde, par en dessous. Sans cette épreuve, la branche
        // pourrait refuser à cent vingt ans sans que rien ne le dise.
        var naissance = _aujourdHui.AddYears(-130);

        Assert.Equal(AccesDuCompte.Complet, AgeDuCompte.Accorder(naissance, _aujourdHui));
    }

    // ================================================================
    // Les constantes, lues plutôt que recopiées
    // ================================================================

    [Fact]
    public void Les_DEUX_bornes_sont_celles_du_document()
    {
        // `docs/13-juridique.md` § 1 : « âge minimum 16 ans, partie nutrition
        // verrouillée en dessous de 18 ans ». Une épreuve qui recopierait 16 et
        // 18 en dur ne verrait jamais un changement de constante.
        Assert.Equal(16, AgeDuCompte.AgeMinimal);
        Assert.Equal(18, AgeDuCompte.AgeDeLaMajorite);
        Assert.True(AgeDuCompte.AgeMinimal < AgeDuCompte.AgeDeLaMajorite);
    }
}
