using Palier.Application.Entrainement;

namespace Palier.Application.Tests;

/// <summary>
/// Les trois états du ressenti, et leur liste fermée.
/// </summary>
public sealed class EntrainementRessentiTests
{
    // ================================================================
    // La liste fermée
    // ================================================================

    [Theory]
    [InlineData(Ressenti.Bon, "good")]
    [InlineData(Ressenti.Moyen, "meh")]
    [InlineData(Ressenti.Douleur, "pain")]
    public void La_forme_stockee_est_celle_du_DOCUMENT(Ressenti ressenti, string attendu) =>
        // `good`, `meh`, `pain` — pas une traduction française. Le document
        // métier, la contrainte CHECK de la migration et cette table s'accordent
        // donc mot pour mot, ce qui rend la vérification possible à l'œil nu.
        Assert.Equal(attendu, Ressentis.EnBase(ressenti));

    [Fact]
    public void Les_TROIS_valeurs_sont_exposees() =>
        Assert.Equal(["good", "meh", "pain"], Ressentis.Tous);

    [Fact]
    public void Une_valeur_d_enumeration_FORCEE_leve() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Ressentis.EnBase((Ressenti)42));

    [Fact]
    public void La_lecture_rend_la_valeur_ET_le_succes()
    {
        Assert.True(Ressentis.Lire("pain", out var lu));
        Assert.Equal(Ressenti.Douleur, lu);
    }

    [Fact]
    public void La_CASSE_est_toleree_a_la_lecture()
    {
        Assert.True(Ressentis.Lire("PAIN", out var lu));
        Assert.Equal(Ressenti.Douleur, lu);
    }

    [Theory]
    [InlineData("excellent")]
    [InlineData("bien")]
    [InlineData("")]
    [InlineData(null)]
    public void Une_valeur_INCONNUE_echoue_sans_lever(string? valeur)
    {
        Assert.False(Ressentis.Lire(valeur, out var lu));
        Assert.Null(lu);
    }

    // ================================================================
    // La note
    // ================================================================

    [Theory]
    [InlineData("good")]
    [InlineData("meh")]
    [InlineData("pain")]
    public void Une_note_VALIDE_ne_porte_aucune_faute(string valeur) =>
        Assert.Null(new NoteDeRessenti(Guid.NewGuid(), valeur).Faute);

    [Theory]
    [InlineData("excellent")]
    [InlineData("")]
    [InlineData(null)]
    public void Une_note_INVALIDE_est_refusee(string? valeur) =>
        // Sans ce refus, une valeur inconnue entrerait en base — refusée par le
        // CHECK, donc en 500 — ou pire, y entrerait si le CHECK venait à
        // manquer, et les seuils du § 5 compteraient faux : « deux pain
        // consécutifs » ne verrait pas un « PAIN » ni un « douleur ».
        Assert.Equal("RessentiInvalide", new NoteDeRessenti(Guid.NewGuid(), valeur).Faute);

    [Fact]
    public void Une_note_conserve_l_exercice_vise()
    {
        var exercice = Guid.NewGuid();
        Assert.Equal(exercice, new NoteDeRessenti(exercice, "good").ExerciceId);
    }

    // ================================================================
    // L'historique
    // ================================================================

    [Fact]
    public void Un_historique_ABSENT_prend_la_valeur_par_defaut() =>
        Assert.Equal(
            RessentiRendu.HistoriqueParDefaut,
            RessentiRendu.BornerLHistorique(null)
        );

    [Fact]
    public void Un_historique_DEMESURE_est_ramene_au_plafond() =>
        Assert.Equal(RessentiRendu.HistoriqueMaximal, RessentiRendu.BornerLHistorique(100_000));

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Un_historique_NUL_OU_NEGATIF_est_ramene_a_un(int combien) =>
        Assert.Equal(1, RessentiRendu.BornerLHistorique(combien));

    [Fact]
    public void L_historique_par_defaut_couvre_les_fenetres_du_document() =>
        // Le § 5 parle de « 3 dernières séances », « 2 consécutifs »,
        // « 3 consécutifs ». Un défaut inférieur à trois rendrait ces règles
        // structurellement inapplicables, sans qu'aucune épreuve ne rougisse.
        Assert.True(RessentiRendu.HistoriqueParDefaut >= 3);

    [Fact]
    public void Un_ressenti_rendu_conserve_ses_quatre_champs()
    {
        var instant = new DateTimeOffset(2026, 8, 23, 9, 0, 0, TimeSpan.Zero);
        var seance = Guid.NewGuid();
        var exercice = Guid.NewGuid();
        var rendu = new RessentiRendu(seance, exercice, "pain", instant);

        Assert.Equal(seance, rendu.SeanceId);
        Assert.Equal(exercice, rendu.ExerciceId);
        Assert.Equal("pain", rendu.Ressenti);
        Assert.Equal(instant, rendu.Instant);
    }
}
