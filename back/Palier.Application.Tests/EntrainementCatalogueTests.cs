using Palier.Application.Entrainement;

namespace Palier.Application.Tests;

/// <summary>
/// La validation d'un exercice personnalisé et la liste fermée des contraintes.
/// </summary>
public sealed class EntrainementCatalogueTests
{
    private static CreationDExercice Exercice(
        string nom = "Rowing haltère",
        IReadOnlyList<string>? primaires = null,
        IReadOnlyList<string>? secondaires = null,
        decimal increment = 2.5m,
        IReadOnlyList<string>? contraintes = null
    ) =>
        new(
            nom,
            "haltère",
            primaires ?? ["dos"],
            secondaires ?? ["biceps"],
            Unilateral: true,
            increment,
            contraintes ?? []
        );

    // ================================================================
    // Ce qui passe
    // ================================================================

    [Fact]
    public void Un_exercice_ordinaire_ne_porte_aucune_faute() => Assert.Null(Exercice().Faute);

    [Fact]
    public void Un_exercice_SANS_muscle_secondaire_passe() =>
        Assert.Null(Exercice(secondaires: []).Faute);

    [Fact]
    public void Un_exercice_SANS_contre_indication_passe() =>
        Assert.Null(Exercice(contraintes: []).Faute);

    [Fact]
    public void Les_QUATRE_contraintes_du_document_passent() =>
        // Elles ne sont pas inventées : `docs/05-entrainement.md` § 4 les nomme
        // toutes les quatre. Cette épreuve est ce qui garde le lien entre le
        // document et le code.
        Assert.Null(
            Exercice(contraintes: ["cervicale", "lombaire", "epaule", "genou"]).Faute
        );

    [Fact]
    public void La_CASSE_d_une_contrainte_est_tolérée() =>
        // Confort de saisie. La comparaison est ordinale et insensible à la
        // casse — jamais culturelle : une comparaison de culture ferait
        // dépendre le résultat de la locale du serveur, le fameux « i » turc où
        // `"I".ToLower()` ne rend pas `"i"`.
        Assert.Null(Exercice(contraintes: ["Cervicale", "EPAULE"]).Faute);

    [Fact]
    public void Les_BORNES_passent()
    {
        Assert.Null(Exercice(nom: new string('a', CreationDExercice.LongueurMaximaleDuNom)).Faute);
        Assert.Null(Exercice(increment: CreationDExercice.IncrementMaximal).Faute);
        Assert.Null(Exercice(increment: 0.25m).Faute);
        Assert.Null(
            Exercice(
                primaires: [.. Enumerable.Repeat("dos", CreationDExercice.MusclesMaximum)],
                secondaires: []
            ).Faute
        );
    }

    // ================================================================
    // Ce qui est refusé, et par QUEL code
    // ================================================================

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Un_nom_VIDE_est_refuse(string nom) =>
        Assert.Equal("NomRequis", Exercice(nom: nom).Faute);

    [Fact]
    public void Un_nom_TROP_LONG_est_refuse() =>
        // La colonne est `text`, donc PostgreSQL ne borne rien : sans ce
        // contrôle, un nom d'un mégaoctet entrerait en base et casserait chaque
        // écran qui l'affiche.
        Assert.Equal(
            "NomTropLong",
            Exercice(nom: new string('a', CreationDExercice.LongueurMaximaleDuNom + 1)).Faute
        );

    [Fact]
    public void Un_exercice_SANS_muscle_primaire_est_refuse() =>
        // Il ne compterait dans AUCUN volume : la vue `weekly_volume` déplie
        // `primary_muscles`, et un tableau vide ne produit aucune ligne.
        // L'exercice existerait sans jamais apparaître nulle part.
        Assert.Equal("MusclePrimaireRequis", Exercice(primaires: []).Faute);

    [Fact]
    public void TROP_de_muscles_est_refuse() =>
        // Les tableaux `text[]` n'ont pas de taille maximale en PostgreSQL. Un
        // tableau d'un million d'entrées passerait, et la vue `weekly_volume`
        // le DÉPLIE par `unnest` : chaque série de cet exercice produirait un
        // million de lignes. C'est un déni de service qu'un compte authentifié
        // déclenche en deux requêtes.
        Assert.Equal(
            "TropDeMuscles",
            Exercice(
                primaires: [.. Enumerable.Repeat("dos", CreationDExercice.MusclesMaximum)],
                secondaires: ["biceps"]
            ).Faute
        );

    [Fact]
    public void Un_nom_de_muscle_VIDE_est_refuse() =>
        Assert.Equal("MuscleInvalide", Exercice(primaires: ["dos", "  "]).Faute);

    [Fact]
    public void Un_nom_de_muscle_TROP_LONG_est_refuse() =>
        Assert.Equal(
            "MuscleInvalide",
            Exercice(
                primaires: [new string('m', CreationDExercice.LongueurMaximaleDUnMuscle + 1)]
            ).Faute
        );

    [Theory]
    [InlineData(0)]
    [InlineData(-2.5)]
    [InlineData(50.1)]
    public void Un_increment_HORS_BORNES_est_refuse(decimal increment) =>
        Assert.Equal("IncrementInvalide", Exercice(increment: increment).Faute);

    [Theory]
    [InlineData("poignet")]
    [InlineData("cheville")]
    [InlineData("")]
    [InlineData("cervicale lombaire")]
    public void Une_contrainte_HORS_LISTE_est_refusee(string contrainte) =>
        // La liste est fermée par le document métier. Une cinquième région
        // s'ajoute à l'énumération, en base et dans les libellés — elle ne
        // s'infiltre pas par le corps d'une requête.
        Assert.Equal("ContrainteInvalide", Exercice(contraintes: [contrainte]).Faute);

    [Fact]
    public void Le_PREMIER_refus_rencontre_est_celui_qui_sort()
    {
        // Cinq fautes à la fois : c'est le nom qui sort. L'épreuve fige
        // l'ordre — sans elle, une réorganisation changerait le code rendu à
        // l'utilisateur, et le front traduirait autre chose que la cause.
        var fautif = new CreationDExercice("", null, [], [], false, -1m, ["poignet"]);
        Assert.Equal("NomRequis", fautif.Faute);
    }

    [Fact]
    public void Un_MATERIEL_trop_long_est_refuse() =>
        // L'asymétrie que la revue a relevée : tous les champs texte étaient
        // bornés SAUF celui-ci, alors que le motif écrit sur
        // `LongueurMaximaleDuNom` vaut mot pour mot — la colonne est `text`,
        // PostgreSQL ne borne rien, et un mégaoctet de matériel serait renvoyé
        // à chaque lecture du catalogue.
        Assert.Equal(
            "MaterielTropLong",
            new CreationDExercice(
                "Curl",
                new string('m', CreationDExercice.LongueurMaximaleDuMateriel + 1),
                ["biceps"],
                [],
                false,
                2.5m,
                []
            ).Faute
        );

    [Fact]
    public void Un_MATERIEL_a_la_borne_passe() =>
        Assert.Null(
            new CreationDExercice(
                "Curl",
                new string('m', CreationDExercice.LongueurMaximaleDuMateriel),
                ["biceps"],
                [],
                false,
                2.5m,
                []
            ).Faute
        );

    [Fact]
    public void Le_materiel_tient_une_COMPOSITION_realiste() =>
        // Soixante et non quarante : le matériel se décrit par une composition
        // — « poids de corps + élastique lourd » — là où un muscle porte un nom.
        Assert.Null(
            new CreationDExercice(
                "Traction",
                "poids de corps + élastique lourd + ceinture lestée",
                ["dos"],
                [],
                false,
                2.5m,
                []
            ).Faute
        );

    // ================================================================
    // Les listes ABSENTES du corps JSON
    // ================================================================

    [Fact]
    public void Une_liste_de_muscles_secondaires_ABSENTE_vaut_une_liste_vide()
    {
        // LE TYPE DIT NON-NULLABLE, ET IL MENT. Ces listes viennent de la
        // désérialisation d'un corps JSON : un client qui omet
        // `musclesSecondaires` — ce qui est légitime, tous les exercices n'en
        // ont pas — produit `null`, que l'annotation de nullabilité n'empêche
        // en rien. Lire `.Count` dessus lèverait, donc rendrait 500 sur une
        // requête parfaitement valide.
        var sans = new CreationDExercice("Curl", null, ["biceps"], null!, false, 2.5m, []);

        Assert.Null(sans.Faute);
        Assert.Empty(sans.SecondairesOuVide);
    }

    [Fact]
    public void Une_liste_de_contre_indications_ABSENTE_vaut_une_liste_vide()
    {
        var sans = new CreationDExercice("Curl", null, ["biceps"], [], false, 2.5m, null!);

        Assert.Null(sans.Faute);
        Assert.Empty(sans.ContraintesOuVide);
    }

    [Fact]
    public void Une_liste_de_muscles_primaires_ABSENTE_est_refusee_SANS_lever()
    {
        // Refusée, oui — mais par un 400 nommé, pas par une exception. La
        // différence se voit chez l'utilisateur : « il manque un muscle » ou
        // « le serveur a un problème ».
        var sans = new CreationDExercice("Curl", null, null!, [], false, 2.5m, []);

        Assert.Equal("MusclePrimaireRequis", sans.Faute);
        Assert.Empty(sans.PrimairesOuVide);
    }

    [Fact]
    public void Un_nom_de_muscle_NUL_dans_la_liste_est_refuse_SANS_lever() =>
        // Un tableau JSON peut porter `null` parmi ses éléments :
        // `["dos", null]`. Sans le paramètre nullable de `MuscleValide`, le
        // `.Length` aurait levé.
        Assert.Equal(
            "MuscleInvalide",
            new CreationDExercice("Curl", null, ["dos", null!], [], false, 2.5m, []).Faute
        );

    [Fact]
    public void Le_materiel_et_l_unilateralite_traversent_le_contrat()
    {
        // Ces deux champs ne participent à aucune validation — c'est
        // précisément pourquoi il faut les éprouver. Le seuil de 100 % l'a
        // signalé : leurs accesseurs n'étaient lus nulle part, donc rien
        // n'aurait rougi si le gestionnaire les avait ignorés en écrivant.
        var exercice = Exercice();

        Assert.Equal("haltère", exercice.Materiel);
        Assert.True(exercice.Unilateral);
    }

    // ================================================================
    // La liste fermée elle-même
    // ================================================================

    [Theory]
    [InlineData(Contrainte.Cervicale, "cervicale")]
    [InlineData(Contrainte.Lombaire, "lombaire")]
    [InlineData(Contrainte.Epaule, "epaule")]
    [InlineData(Contrainte.Genou, "genou")]
    public void La_forme_stockee_est_en_minuscules_SANS_ACCENT(Contrainte contrainte, string attendu)
    {
        // `epaule` et non `épaule`. Le tableau `text[]` se compare octet pour
        // octet, et une collation qui traiterait « e » et « é » différemment
        // selon l'environnement rendrait l'intersection dépendante de la
        // configuration du serveur. Le libellé accentué vit dans i18next.
        Assert.Equal(attendu, Contraintes.EnBase(contrainte));
        Assert.DoesNotContain('é', Contraintes.EnBase(contrainte));
    }

    [Fact]
    public void Une_valeur_d_enumeration_FORCEE_leve() =>
        // Un `(Contrainte)42` ne peut venir que d'un forçage, ou d'un membre
        // ajouté à l'énumération sans être ajouté au dictionnaire. Lever plutôt
        // que rendre une chaîne vide : une chaîne vide entrerait en base et y
        // resterait, invisible.
        Assert.Throws<ArgumentOutOfRangeException>(() => Contraintes.EnBase((Contrainte)42));

    [Fact]
    public void La_lecture_rend_la_valeur_ET_le_succes()
    {
        Assert.True(Contraintes.Lire("genou", out var lue));
        Assert.Equal(Contrainte.Genou, lue);
    }

    [Fact]
    public void La_lecture_d_une_valeur_INCONNUE_echoue_sans_lever()
    {
        Assert.False(Contraintes.Lire("poignet", out var lue));
        Assert.Null(lue);

        Assert.False(Contraintes.Lire(null, out var nulle));
        Assert.Null(nulle);
    }

    [Fact]
    public void Les_QUATRE_valeurs_sont_exposees() =>
        Assert.Equal(
            ["cervicale", "lombaire", "epaule", "genou"],
            Contraintes.Toutes
        );

    // ================================================================
    // La forme rendue
    // ================================================================

    [Fact]
    public void Un_exercice_rendu_conserve_ses_dix_champs()
    {
        var identifiant = Guid.NewGuid();
        var rendu = new ExerciceRendu(
            identifiant,
            "Rowing haltère",
            "haltère",
            ["dos"],
            ["biceps"],
            true,
            2.5m,
            ["lombaire"],
            EstPersonnalise: true,
            [new MarquageDeContrainte("lombaire", "strict")]
        );

        Assert.Equal(identifiant, rendu.Id);
        Assert.Equal("Rowing haltère", rendu.Nom);
        Assert.Equal("haltère", rendu.Materiel);
        Assert.Equal(["dos"], rendu.MusclesPrimaires);
        Assert.Equal(["biceps"], rendu.MusclesSecondaires);
        Assert.True(rendu.Unilateral);
        Assert.Equal(2.5m, rendu.IncrementParDefaut);
        Assert.Equal(["lombaire"], rendu.ContreIndicationsPour);
        Assert.True(rendu.EstPersonnalise);
        Assert.Single(rendu.Marquages);
    }

    [Fact]
    public void Un_marquage_porte_la_region_ET_la_severite_declaree()
    {
        // Les DEUX, toujours. La région seule ne dirait pas à l'écran comment
        // présenter — et c'est la sévérité, réglée par l'utilisateur, qui
        // pilote cette présentation.
        var marquage = new MarquageDeContrainte("genou", "leger");

        Assert.Equal("genou", marquage.Region);
        Assert.Equal("leger", marquage.Severite);
    }

    [Fact]
    public void Un_exercice_SANS_marquage_est_l_etat_ordinaire() =>
        // La plupart des exercices ne recoupent aucune contrainte déclarée, et
        // la plupart des utilisateurs n'en déclarent aucune.
        Assert.Empty(
            new ExerciceRendu(
                Guid.NewGuid(),
                "Développé couché",
                null,
                ["pectoraux"],
                [],
                false,
                2.5m,
                [],
                EstPersonnalise: false,
                []
            ).Marquages
        );
}
