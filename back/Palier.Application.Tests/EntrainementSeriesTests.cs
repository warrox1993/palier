using Palier.Application.Entrainement;

namespace Palier.Application.Tests;

/// <summary>
/// La validation d'une série — ce qui se refuse SANS base.
/// </summary>
/// <remarks>
/// <b>Le point que ces épreuves gardent :</b> les bornes ne sont pas recopiées
/// ici. <c>AjoutDeSerie.Faute</c> appelle <c>Charge.EstValide</c>,
/// <c>Repetitions.EstValide</c> et <c>Rir.EstValide</c> — les mêmes prédicats
/// dont les fabriques du domaine se servent. Il ne peut donc pas exister une
/// valeur que ce contrôle accepte et que la construction refuserait, ce qui
/// serait un 500 sur une saisie parfaitement ordinaire.
/// </remarks>
public sealed class EntrainementSeriesTests
{
    private static AjoutDeSerie Serie(
        int index = 1,
        decimal? charge = 60m,
        int? repetitions = 8,
        int? rir = 2
    ) => new(Guid.NewGuid(), index, charge, repetitions, rir, Echauffement: false);

    // ================================================================
    // Ce qui passe
    // ================================================================

    [Fact]
    public void Une_serie_ordinaire_ne_porte_aucune_faute() => Assert.Null(Serie().Faute);

    [Fact]
    public void Une_serie_au_POIDS_DE_CORPS_passe() =>
        // Charge nulle, et non zéro : une traction se note sans charge du tout.
        // Zéro kilogramme ajouté est une AUTRE information, et les deux sont
        // valides — `Charge` accepte zéro délibérément.
        Assert.Null(Serie(charge: null).Faute);

    [Fact]
    public void Une_serie_a_ZERO_kilogramme_passe() => Assert.Null(Serie(charge: 0m).Faute);

    [Fact]
    public void Un_GAINAGE_sans_repetitions_passe() =>
        // Un gainage se compte en secondes. Exiger des répétitions rendrait
        // impossible de noter un exercice isométrique — et le pratiquant
        // saisirait un nombre inventé, qui fausserait le volume.
        Assert.Null(Serie(repetitions: null).Faute);

    [Fact]
    public void Un_RIR_non_renseigne_passe() =>
        // `docs/05-entrainement.md` § 3 : le domaine suppose 2 quand il manque.
        // Le refuser ici obligerait à une saisie que le document déclare
        // facultative.
        Assert.Null(Serie(rir: null).Faute);

    [Fact]
    public void Les_BORNES_du_domaine_passent()
    {
        Assert.Null(Serie(charge: 0m).Faute);
        Assert.Null(Serie(charge: 1000m).Faute);
        Assert.Null(Serie(repetitions: 1).Faute);
        Assert.Null(Serie(repetitions: 1000).Faute);
        Assert.Null(Serie(rir: 0).Faute);
        Assert.Null(Serie(rir: 10).Faute);
    }

    // ================================================================
    // Ce qui est refusé, et par QUEL code
    // ================================================================

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Un_index_INFERIEUR_A_UN_est_refuse(int index) =>
        // Les séries se numérotent à partir de 1, comme elles s'annoncent à
        // l'utilisateur. Un index à zéro ou négatif décalerait tout affichage
        // qui compte à partir de un.
        Assert.Equal("IndexInvalide", Serie(index: index).Faute);

    [Theory]
    [InlineData(-0.5)]
    [InlineData(1000.1)]
    public void Une_charge_HORS_BORNES_est_refusee(decimal charge) =>
        Assert.Equal("ChargeInvalide", Serie(charge: charge).Faute);

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1001)]
    public void Des_repetitions_HORS_BORNES_sont_refusees(int repetitions) =>
        Assert.Equal("RepetitionsInvalides", Serie(repetitions: repetitions).Faute);

    [Theory]
    [InlineData(-1)]
    [InlineData(11)]
    public void Un_RIR_HORS_BORNES_est_refuse(int rir) =>
        Assert.Equal("RirInvalide", Serie(rir: rir).Faute);

    [Fact]
    public void Le_PREMIER_refus_rencontre_est_celui_qui_sort()
    {
        // Trois fautes à la fois : c'est l'index qui sort, parce qu'il est
        // contrôlé en premier. L'épreuve fige cet ordre — sans elle, une
        // réorganisation du contrôle changerait le code rendu à l'utilisateur
        // sans que rien ne rougisse, et le front traduirait autre chose.
        var fautive = Serie(index: 0, charge: -5m, repetitions: 0, rir: 99);
        Assert.Equal("IndexInvalide", fautive.Faute);
    }

    [Fact]
    public void Un_ajout_conserve_l_exercice_vise_et_le_drapeau_d_echauffement()
    {
        // Ces deux champs ne participent à AUCUNE validation — c'est
        // précisément pourquoi il faut les éprouver. Le seuil de 100 % l'a
        // signalé : leurs accesseurs n'étaient lus nulle part, donc rien
        // n'aurait rougi si le gestionnaire les avait ignorés en écrivant en
        // base. Une série d'échauffement enregistrée comme série DURE fausse
        // le volume hebdomadaire et gonfle l'estimation de force.
        var exercice = Guid.NewGuid();
        var ajout = new AjoutDeSerie(exercice, 1, 60m, 8, 2, Echauffement: true);

        Assert.Equal(exercice, ajout.ExerciceId);
        Assert.True(ajout.Echauffement);
        Assert.Null(ajout.Faute);
    }

    // ================================================================
    // Les formes rendues
    // ================================================================

    [Fact]
    public void Une_serie_rendue_conserve_ses_huit_champs()
    {
        var instant = new DateTimeOffset(2026, 8, 23, 9, 0, 0, TimeSpan.Zero);
        var identifiant = Guid.NewGuid();
        var exercice = Guid.NewGuid();
        var rendue = new SerieRendue(identifiant, exercice, 2, 62.5m, 8, 1, true, instant);

        Assert.Equal(identifiant, rendue.Id);
        Assert.Equal(exercice, rendue.ExerciceId);
        Assert.Equal(2, rendue.Index);
        Assert.Equal(62.5m, rendue.ChargeKg);
        Assert.Equal(8, rendue.Repetitions);
        Assert.Equal(1, rendue.Rir);
        Assert.True(rendue.Echauffement);
        Assert.Equal(instant, rendue.Instant);
    }

    [Fact]
    public void Une_seance_detaillee_porte_la_seance_ET_ses_series()
    {
        var instant = new DateTimeOffset(2026, 8, 23, 9, 0, 0, TimeSpan.Zero);
        var seance = new SeanceRendue(Guid.NewGuid(), instant, null, null, null, null);
        var serie = new SerieRendue(Guid.NewGuid(), Guid.NewGuid(), 1, 60m, 8, 2, false, instant);
        var detaillee = new SeanceDetaillee(seance, [serie]);

        Assert.Equal(seance.Id, detaillee.Seance.Id);
        Assert.Single(detaillee.Series);
        Assert.Equal(serie.Id, detaillee.Series[0].Id);
    }

    [Fact]
    public void Une_seance_detaillee_SANS_serie_est_un_etat_legitime() =>
        // Une séance qu'on vient d'ouvrir n'a pas encore de série. C'est l'état
        // vide que `11-qualite.md` exige de traiter — pas une erreur.
        Assert.Empty(
            new SeanceDetaillee(
                new SeanceRendue(Guid.NewGuid(), DateTimeOffset.UnixEpoch, null, null, null, null),
                []
            ).Series
        );
}
