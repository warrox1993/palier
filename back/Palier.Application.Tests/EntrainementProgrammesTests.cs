using Palier.Application.Entrainement;

namespace Palier.Application.Tests;

/// <summary>
/// La validation d'un programme — le seul chemin vers <see cref="ProgrammeValide"/>.
/// </summary>
/// <remarks>
/// Chaque refus est provoqué. Une validation dont on n'a jamais vu le rouge ne
/// protège rien : c'est la règle du dépôt, et elle vaut ici comme pour un
/// garde-fou d'outillage.
/// </remarks>
public sealed class EntrainementProgrammesTests
{
    private static EcritureDExerciceDeSeance UnExercice(
        int series = 3,
        int repetitionsMin = 8,
        int? repetitionsMax = 12,
        int rir = 2,
        int repos = 90,
        string? note = null
    ) => new(Guid.NewGuid(), series, repetitionsMin, repetitionsMax, rir, repos, note);

    private static EcritureDeProgramme UnProgramme(
        string nom = "Mon programme",
        string? description = null,
        bool actif = true,
        IReadOnlyList<EcritureDeSeance>? seances = null
    ) => new(nom, description, actif, seances);

    // ================================================================
    // Ce qui passe
    // ================================================================

    [Fact]
    public void Un_programme_MINIMAL_est_accepte()
    {
        // Un nom, et rien d'autre. Créer le programme puis le remplir est le
        // geste naturel : exiger une séance dès la création obligerait l'écran
        // à en inventer une.
        var (valide, faute) = ProgrammeValide.Lire(UnProgramme());

        Assert.Null(faute);
        Assert.NotNull(valide);
        Assert.Equal("Mon programme", valide.Nom);
        Assert.Empty(valide.Seances);
    }

    [Fact]
    public void Le_nom_et_la_description_sont_ELAGUES()
    {
        var (valide, _) = ProgrammeValide.Lire(UnProgramme("  Haut du corps  ", "  Le lundi  "));

        Assert.Equal("Haut du corps", valide!.Nom);
        Assert.Equal("Le lundi", valide.Description);
    }

    [Fact]
    public void Une_description_BLANCHE_devient_nulle()
    {
        // Distinguer « pas de description » de « une description faite
        // d'espaces » n'a aucun sens pour l'écran, et laisserait deux états
        // pour la même absence.
        var (valide, _) = ProgrammeValide.Lire(UnProgramme(description: "   "));

        Assert.Null(valide!.Description);
    }

    [Fact]
    public void Un_programme_COMPLET_conserve_l_ordre_recu()
    {
        // L'ordre est la seule chose que le client transmet sur les rangs : le
        // serveur les attribue ensuite. Le perdre ici les mélangerait sans que
        // rien ne refuse.
        var ecriture = UnProgramme(
            seances:
            [
                new EcritureDeSeance("Haut", [UnExercice(), UnExercice()]),
                new EcritureDeSeance("Bas", [UnExercice()]),
            ]
        );

        var (valide, faute) = ProgrammeValide.Lire(ecriture);

        Assert.Null(faute);
        Assert.Equal(["Haut", "Bas"], valide!.Seances.Select(s => s.Libelle));
        Assert.Equal([2, 1], valide.Seances.Select(s => s.Exercices.Count));
    }

    [Fact]
    public void Une_seance_VIDE_est_acceptee()
    {
        // On crée une séance avant de la remplir. La refuser obligerait l'écran
        // à garder un brouillon hors de la base.
        var (valide, faute) = ProgrammeValide.Lire(
            UnProgramme(seances: [new EcritureDeSeance("Jour A", null)])
        );

        Assert.Null(faute);
        Assert.Empty(valide!.Seances[0].Exercices);
    }

    [Fact]
    public void Une_borne_haute_de_repetitions_NULLE_est_acceptee()
    {
        // « 5 répétitions » et non « 5 à 8 » : une cible exacte est légitime,
        // et c'est même la forme des séries de force.
        var (_, faute) = ProgrammeValide.Lire(
            UnProgramme(
                seances: [new EcritureDeSeance("A", [UnExercice(repetitionsMin: 5, repetitionsMax: null)])]
            )
        );

        Assert.Null(faute);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(20)]
    public void Les_bornes_des_series_sont_INCLUSIVES(int series)
    {
        var (_, faute) = ProgrammeValide.Lire(
            UnProgramme(seances: [new EcritureDeSeance("A", [UnExercice(series: series)])])
        );

        Assert.Null(faute);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10)]
    public void Les_bornes_du_RIR_sont_INCLUSIVES(int rir)
    {
        // Zéro RIR est l'échec musculaire, dix est une série très facile.
        // `docs/05-entrainement.md` § 1 travaille entre 0 et 4 ; les bornes du
        // schéma sont plus larges, parce qu'elles bornent l'absurde, pas la
        // pratique.
        var (_, faute) = ProgrammeValide.Lire(
            UnProgramme(seances: [new EcritureDeSeance("A", [UnExercice(rir: rir)])])
        );

        Assert.Null(faute);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(900)]
    public void Les_bornes_du_REPOS_sont_INCLUSIVES(int repos)
    {
        var (_, faute) = ProgrammeValide.Lire(
            UnProgramme(seances: [new EcritureDeSeance("A", [UnExercice(repos: repos)])])
        );

        Assert.Null(faute);
    }

    // ================================================================
    // Ce qui est refusé — chaque branche provoquée
    // ================================================================

    [Fact]
    public void Une_ecriture_NULLE_est_refusee()
    {
        var (valide, faute) = ProgrammeValide.Lire(null);

        Assert.Null(valide);
        Assert.Equal("ProgrammeInvalide", faute);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Un_nom_ABSENT_est_refuse(string? nom)
    {
        var (_, faute) = ProgrammeValide.Lire(UnProgramme(nom!));

        Assert.Equal("NomRequis", faute);
    }

    [Fact]
    public void Un_nom_TROP_LONG_est_refuse()
    {
        var (_, faute) = ProgrammeValide.Lire(
            UnProgramme(new string('x', EcritureDeProgramme.LongueurMaximaleDuNom + 1))
        );

        Assert.Equal("NomTropLong", faute);
    }

    [Fact]
    public void Une_description_TROP_LONGUE_est_refusee()
    {
        var (_, faute) = ProgrammeValide.Lire(
            UnProgramme(
                description: new string('x', EcritureDeProgramme.LongueurMaximaleDeLaDescription + 1)
            )
        );

        Assert.Equal("DescriptionTropLongue", faute);
    }

    [Fact]
    public void TROP_DE_SEANCES_est_refuse()
    {
        var seances = Enumerable
            .Range(0, EcritureDeProgramme.SeancesMaximum + 1)
            .Select(rang => new EcritureDeSeance($"Jour {rang}", null))
            .ToArray();

        var (_, faute) = ProgrammeValide.Lire(UnProgramme(seances: seances));

        Assert.Equal("TropDeSeances", faute);
    }

    [Fact]
    public void Une_seance_NULLE_dans_la_liste_est_refusee()
    {
        // Le client contrôle le contenu du tableau, y compris ses trous. Sans
        // cette branche, la validation lèverait une NullReferenceException que
        // le pipeline traduirait en 500 — sur une requête simplement mal
        // formée.
        var (_, faute) = ProgrammeValide.Lire(UnProgramme(seances: [null!]));

        Assert.Equal("SeanceInvalide", faute);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Un_libelle_de_seance_ABSENT_est_refuse(string? libelle)
    {
        var (_, faute) = ProgrammeValide.Lire(
            UnProgramme(seances: [new EcritureDeSeance(libelle!, null)])
        );

        Assert.Equal("LibelleRequis", faute);
    }

    [Fact]
    public void Un_libelle_de_seance_TROP_LONG_est_refuse()
    {
        var (_, faute) = ProgrammeValide.Lire(
            UnProgramme(
                seances:
                [
                    new EcritureDeSeance(
                        new string('x', EcritureDeSeance.LongueurMaximaleDuLibelle + 1),
                        null
                    ),
                ]
            )
        );

        Assert.Equal("LibelleTropLong", faute);
    }

    [Fact]
    public void TROP_D_EXERCICES_dans_une_seance_est_refuse()
    {
        var exercices = Enumerable
            .Range(0, EcritureDeSeance.ExercicesMaximum + 1)
            .Select(_ => UnExercice())
            .ToArray();

        var (_, faute) = ProgrammeValide.Lire(
            UnProgramme(seances: [new EcritureDeSeance("A", exercices)])
        );

        Assert.Equal("TropDExercices", faute);
    }

    [Fact]
    public void Un_exercice_NUL_dans_la_liste_est_refuse()
    {
        var (_, faute) = ProgrammeValide.Lire(
            UnProgramme(seances: [new EcritureDeSeance("A", [null!])])
        );

        Assert.Equal("ExerciceDeSeanceInvalide", faute);
    }

    [Fact]
    public void Un_identifiant_d_exercice_VIDE_est_refuse()
    {
        var exercice = new EcritureDExerciceDeSeance(Guid.Empty, 3, 8, 12, 2, 90, null);

        var (_, faute) = ProgrammeValide.Lire(
            UnProgramme(seances: [new EcritureDeSeance("A", [exercice])])
        );

        Assert.Equal("ExerciceRequis", faute);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(21)]
    public void Des_SERIES_hors_bornes_sont_refusees(int series)
    {
        var (_, faute) = ProgrammeValide.Lire(
            UnProgramme(seances: [new EcritureDeSeance("A", [UnExercice(series: series)])])
        );

        Assert.Equal("SeriesHorsBornes", faute);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void Des_REPETITIONS_minimales_hors_bornes_sont_refusees(int minimum)
    {
        var (_, faute) = ProgrammeValide.Lire(
            UnProgramme(
                seances:
                [
                    new EcritureDeSeance(
                        "A",
                        [UnExercice(repetitionsMin: minimum, repetitionsMax: null)]
                    ),
                ]
            )
        );

        Assert.Equal("RepetitionsHorsBornes", faute);
    }

    [Fact]
    public void Une_borne_haute_INFERIEURE_a_la_basse_est_refusee()
    {
        // « 12 à 8 » passerait chaque contrôle individuel et s'afficherait à
        // l'envers. La comparaison des deux bornes entre elles est la seule qui
        // l'attrape.
        var (_, faute) = ProgrammeValide.Lire(
            UnProgramme(
                seances:
                [
                    new EcritureDeSeance("A", [UnExercice(repetitionsMin: 12, repetitionsMax: 8)]),
                ]
            )
        );

        Assert.Equal("RepetitionsHorsBornes", faute);
    }

    [Fact]
    public void Une_borne_haute_AU_DESSUS_DU_MAXIMUM_est_refusee()
    {
        var (_, faute) = ProgrammeValide.Lire(
            UnProgramme(
                seances:
                [
                    new EcritureDeSeance(
                        "A",
                        [
                            UnExercice(
                                repetitionsMin: 1,
                                repetitionsMax: EcritureDExerciceDeSeance.RepetitionsMaximum + 1
                            ),
                        ]
                    ),
                ]
            )
        );

        Assert.Equal("RepetitionsHorsBornes", faute);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(11)]
    public void Un_RIR_hors_bornes_est_refuse(int rir)
    {
        var (_, faute) = ProgrammeValide.Lire(
            UnProgramme(seances: [new EcritureDeSeance("A", [UnExercice(rir: rir)])])
        );

        Assert.Equal("RirHorsBornes", faute);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(901)]
    public void Un_REPOS_hors_bornes_est_refuse(int repos)
    {
        var (_, faute) = ProgrammeValide.Lire(
            UnProgramme(seances: [new EcritureDeSeance("A", [UnExercice(repos: repos)])])
        );

        Assert.Equal("ReposHorsBornes", faute);
    }

    [Fact]
    public void Une_note_TROP_LONGUE_est_refusee()
    {
        var (_, faute) = ProgrammeValide.Lire(
            UnProgramme(
                seances:
                [
                    new EcritureDeSeance(
                        "A",
                        [
                            UnExercice(
                                note: new string(
                                    'x',
                                    EcritureDExerciceDeSeance.LongueurMaximaleDeLaNote + 1
                                )
                            ),
                        ]
                    ),
                ]
            )
        );

        Assert.Equal("NoteTropLongue", faute);
    }

    [Fact]
    public void Une_note_BLANCHE_devient_nulle()
    {
        var (valide, _) = ProgrammeValide.Lire(
            UnProgramme(seances: [new EcritureDeSeance("A", [UnExercice(note: "   ")])])
        );

        Assert.Null(valide!.Seances[0].Exercices[0].Note);
    }

    [Fact]
    public void Une_note_est_ELAGUEE()
    {
        var (valide, _) = ProgrammeValide.Lire(
            UnProgramme(seances: [new EcritureDeSeance("A", [UnExercice(note: "  Par jambe.  ")])])
        );

        Assert.Equal("Par jambe.", valide!.Seances[0].Exercices[0].Note);
    }

    [Fact]
    public void TOUTES_les_cibles_traversent_la_validation_INTACTES()
    {
        // Une épreuve d'ORDRE DES PARAMÈTRES. `ExerciceDeSeanceValide` en porte
        // sept, tous du même type ou presque : intervertir `RirCible` et
        // `ReposSecondes` dans le constructeur compilerait, passerait toutes
        // les épreuves de refus ci-dessus, et donnerait des séances de deux
        // secondes de repos avec quatre-vingt-dix répétitions en réserve.
        var exercice = Guid.NewGuid();

        var (valide, _) = ProgrammeValide.Lire(
            new EcritureDeProgramme(
                "Haut du corps",
                "Le lundi et le jeudi",
                Actif: false,
                [
                    new EcritureDeSeance(
                        "Jour A",
                        [new EcritureDExerciceDeSeance(exercice, 4, 6, 10, 3, 150, "Par bras.")]
                    ),
                ]
            )
        );

        Assert.Equal("Haut du corps", valide!.Nom);
        Assert.Equal("Le lundi et le jeudi", valide.Description);
        Assert.False(valide.Actif);

        var seance = Assert.Single(valide.Seances);
        Assert.Equal("Jour A", seance.Libelle);

        var pose = Assert.Single(seance.Exercices);
        Assert.Equal(exercice, pose.ExerciceId);
        Assert.Equal(4, pose.Series);
        Assert.Equal(6, pose.RepetitionsMin);
        Assert.Equal(10, pose.RepetitionsMax);
        Assert.Equal(3, pose.RirCible);
        Assert.Equal(150, pose.ReposSecondes);
        Assert.Equal("Par bras.", pose.Note);
    }

    // ================================================================
    // La forme rendue
    // ================================================================

    [Fact]
    public void Un_programme_en_liste_conserve_ses_dix_champs()
    {
        var identifiant = Guid.NewGuid();
        var rendu = new ProgrammeEnListe(
            identifiant,
            "reprise-lombaire",
            "Reprise lombaire",
            "Trois séances par semaine.",
            EstUnModele: true,
            FrequenceMin: 3,
            FrequenceMax: 3,
            ContrainteMenagee: "lombaire",
            Actif: true,
            NombreDeSeances: 3
        );

        Assert.Equal(identifiant, rendu.Id);
        Assert.Equal("reprise-lombaire", rendu.Slug);
        Assert.Equal("Reprise lombaire", rendu.Nom);
        Assert.Equal("Trois séances par semaine.", rendu.Description);
        Assert.True(rendu.EstUnModele);
        Assert.Equal(3, rendu.FrequenceMin);
        Assert.Equal(3, rendu.FrequenceMax);
        Assert.Equal("lombaire", rendu.ContrainteMenagee);
        Assert.True(rendu.Actif);
        Assert.Equal(3, rendu.NombreDeSeances);
    }

    [Fact]
    public void Un_programme_rendu_conserve_ses_onze_champs()
    {
        var identifiant = Guid.NewGuid();
        var jour = Guid.NewGuid();
        var pose = Guid.NewGuid();
        var exercice = Guid.NewGuid();

        var rendu = new ProgrammeRendu(
            identifiant,
            "genou-menage",
            "Genou ménagé",
            "Sans fente profonde.",
            "La chaîne postérieure porte le travail.",
            EstUnModele: true,
            FrequenceMin: 3,
            FrequenceMax: 4,
            ContrainteMenagee: "genou",
            Actif: true,
            Seances:
            [
                new SeanceDeProgrammeRendue(
                    jour,
                    "Séance A — bas",
                    1,
                    [
                        new ExerciceDeSeanceRendu(
                            pose,
                            exercice,
                            "Pont fessier",
                            1,
                            3,
                            12,
                            15,
                            2,
                            60,
                            "Sans creuser le bas du dos.",
                            [new MarquageDeContrainte("genou", "souple")]
                        ),
                    ]
                ),
            ]
        );

        Assert.Equal(identifiant, rendu.Id);
        Assert.Equal("genou-menage", rendu.Slug);
        Assert.Equal("Genou ménagé", rendu.Nom);
        Assert.Equal("Sans fente profonde.", rendu.Description);
        Assert.Equal("La chaîne postérieure porte le travail.", rendu.Notes);
        Assert.True(rendu.EstUnModele);
        Assert.Equal(3, rendu.FrequenceMin);
        Assert.Equal(4, rendu.FrequenceMax);
        Assert.Equal("genou", rendu.ContrainteMenagee);
        Assert.True(rendu.Actif);

        var seance = Assert.Single(rendu.Seances);
        Assert.Equal(jour, seance.Id);
        Assert.Equal("Séance A — bas", seance.Libelle);
        Assert.Equal(1, seance.Position);

        var pose_ = Assert.Single(seance.Exercices);
        Assert.Equal(pose, pose_.Id);
        Assert.Equal(exercice, pose_.ExerciceId);
        Assert.Equal("Pont fessier", pose_.NomDeLExercice);
        Assert.Equal(1, pose_.Position);
        Assert.Equal(3, pose_.Series);
        Assert.Equal(12, pose_.RepetitionsMin);
        Assert.Equal(15, pose_.RepetitionsMax);
        Assert.Equal(2, pose_.RirCible);
        Assert.Equal(60, pose_.ReposSecondes);
        Assert.Equal("Sans creuser le bas du dos.", pose_.Note);

        // Le marquage voyage AVEC l'exercice — D75. Un rendu qui le perdrait
        // afficherait un programme de contrainte sans dire lequel de ses
        // mouvements touche une contrainte déclarée.
        var marquage = Assert.Single(pose_.Marquages);
        Assert.Equal("genou", marquage.Region);
    }

    // ================================================================
    // Les accesseurs du contrat
    // ================================================================

    [Fact]
    public void Les_listes_ABSENTES_se_lisent_comme_vides()
    {
        // Un client qui n'envoie pas le champ et un client qui envoie une liste
        // vide veulent dire la même chose. Deux formes pour la même absence
        // obligeraient chaque lecture à les distinguer.
        Assert.Empty(UnProgramme().SeancesOuVide);
        Assert.Empty(new EcritureDeSeance("A", null).ExercicesOuVide);
    }

    [Fact]
    public void Un_nom_NUL_se_lit_comme_vide()
    {
        // Le client contrôle le corps de la requête : `null` y arrive, même sur
        // une propriété non annulable. Sans ce filet, l'élagage lèverait avant
        // que la validation ait pu refuser proprement.
        Assert.Equal(string.Empty, UnProgramme(null!).NomNettoye);
        Assert.Equal(string.Empty, new EcritureDeSeance(null!, null).LibelleNettoye);
    }
}
