namespace Palier.Application.Entrainement;

// Les contrats des programmes.
//
// UN PROGRAMME S'ÉCRIT EN ENTIER, jamais par morceaux. `PUT /programmes/{id}`
// remplace les séances et leurs exercices d'un seul geste, et il n'existe
// aucune route pour déplacer un exercice ou renommer une séance.
//
// Ce choix vient des POSITIONS. Six routes fines — ajouter, retirer, monter,
// descendre — auraient chacune eu à renuméroter ce qu'elles touchent, et six
// implémentations de la même renumérotation auraient divergé. Ici les rangs
// sont attribués par le serveur à partir de l'ordre reçu, à un seul endroit.
//
// Le coût est réel et assumé : deux écrans ouverts sur le même programme, le
// dernier qui enregistre écrase l'autre. C'est le comportement d'un document,
// et un programme d'entraînement en est un.

/// <summary>Un programme dans la liste, sans son contenu.</summary>
/// <remarks>
/// La liste ne porte PAS les séances. Neuf modèles avec leurs trente-six
/// séances et leurs cent cinquante-sept exercices feraient une réponse que
/// personne ne lit en entier, à chaque ouverture de l'écran.
/// </remarks>
public sealed record ProgrammeEnListe(
    Guid Id,
    string? Slug,
    string Nom,
    string? Description,
    bool EstUnModele,
    int? FrequenceMin,
    int? FrequenceMax,
    string? ContrainteMenagee,
    bool Actif,
    int NombreDeSeances
);

/// <summary>Un programme et tout son contenu.</summary>
public sealed record ProgrammeRendu(
    Guid Id,
    string? Slug,
    string Nom,
    string? Description,
    string? Notes,
    bool EstUnModele,
    int? FrequenceMin,
    int? FrequenceMax,
    string? ContrainteMenagee,
    bool Actif,
    IReadOnlyList<SeanceDeProgrammeRendue> Seances
);

/// <summary>Une séance type et ses exercices.</summary>
public sealed record SeanceDeProgrammeRendue(
    Guid Id,
    string Libelle,
    int Position,
    IReadOnlyList<ExerciceDeSeanceRendu> Exercices
);

/// <summary>Un exercice posé dans une séance, avec ses cibles.</summary>
/// <remarks>
/// <c>Marquages</c> porte les contraintes DÉCLARÉES par l'appelant que ce
/// mouvement touche — vide le plus souvent.
///
/// <para>
/// <b>Le marquage informe, il ne retire rien</b> — D63 et D75. Un exercice
/// contre-indiqué pour une contrainte déclarée reste dans le programme et
/// reste ajoutable : `docs/01-conformite.md` sépare informer de prescrire, et
/// un logiciel qui interdirait un mouvement à quelqu'un dont il ignore le
/// dossier prescrirait.
/// </para>
/// </remarks>
public sealed record ExerciceDeSeanceRendu(
    Guid Id,
    Guid ExerciceId,
    string NomDeLExercice,
    int Position,
    int Series,
    int RepetitionsMin,
    int? RepetitionsMax,
    int RirCible,
    int ReposSecondes,
    string? Note,
    IReadOnlyList<MarquageDeContrainte> Marquages
);

/// <summary>Ce qu'on envoie pour créer ou remplacer un programme.</summary>
public sealed record EcritureDeProgramme(
    string Nom,
    string? Description,
    bool Actif,
    IReadOnlyList<EcritureDeSeance>? Seances
)
{
    /// <summary>La longueur maximale du nom.</summary>
    /// <remarks>
    /// La colonne est <c>text</c> : PostgreSQL ne borne rien. Sans ce
    /// contrôle, un nom d'un mégaoctet entrerait en base et casserait chaque
    /// écran qui l'affiche — le même raisonnement que pour un exercice, et la
    /// même valeur.
    /// </remarks>
    public const int LongueurMaximaleDuNom = 120;

    /// <summary>La longueur maximale de la description.</summary>
    public const int LongueurMaximaleDeLaDescription = 500;

    /// <summary>Le nombre maximal de séances dans un programme.</summary>
    /// <remarks>
    /// Quatorze, soit deux par jour. Au-delà, ce n'est plus un programme
    /// hebdomadaire, et la borne existe pour que le coût d'une écriture reste
    /// prévisible — pas pour juger de la pratique de quiconque.
    /// </remarks>
    public const int SeancesMaximum = 14;

    public IReadOnlyList<EcritureDeSeance> SeancesOuVide => Seances ?? [];

    public string NomNettoye => Nom?.Trim() ?? string.Empty;

    public string? DescriptionNettoyee =>
        string.IsNullOrWhiteSpace(Description) ? null : Description.Trim();
}

/// <summary>Une séance à écrire.</summary>
public sealed record EcritureDeSeance(
    string Libelle,
    IReadOnlyList<EcritureDExerciceDeSeance>? Exercices
)
{
    /// <summary>La longueur maximale du libellé.</summary>
    public const int LongueurMaximaleDuLibelle = 60;

    /// <summary>Le nombre maximal d'exercices dans une séance.</summary>
    public const int ExercicesMaximum = 30;

    public IReadOnlyList<EcritureDExerciceDeSeance> ExercicesOuVide => Exercices ?? [];

    public string LibelleNettoye => Libelle?.Trim() ?? string.Empty;
}

/// <summary>Un exercice à poser dans une séance.</summary>
/// <remarks>
/// <b>Les bornes ci-dessous DOUBLENT celles du moteur</b>, elles ne les
/// remplacent pas. Les contraintes <c>ck_program_exercises_*</c> disent la même
/// chose en SQL, et c'est délibéré : une restauration, un chargement de
/// référentiel ou une écriture directe ne passent pas par ce code.
///
/// Une épreuve vérifie que les deux refusent exactement la même chose. Sans
/// elle, les deux dériveraient — et c'est toujours la validation applicative
/// qui aurait raison à l'écran pendant que le moteur laisserait passer.
/// </remarks>
public sealed record EcritureDExerciceDeSeance(
    Guid ExerciceId,
    int Series,
    int RepetitionsMin,
    int? RepetitionsMax,
    int RirCible,
    int ReposSecondes,
    string? Note
)
{
    public const int SeriesMinimum = 1;
    public const int SeriesMaximum = 20;
    public const int RepetitionsMinimum = 1;
    public const int RepetitionsMaximum = 100;
    public const int RirMinimum = 0;
    public const int RirMaximum = 10;
    public const int ReposMinimum = 0;
    public const int ReposMaximum = 900;

    /// <summary>La longueur maximale de la note.</summary>
    public const int LongueurMaximaleDeLaNote = 300;

    public string? NoteNettoyee => string.IsNullOrWhiteSpace(Note) ? null : Note.Trim();
}

/// <summary>Un programme dont la forme a été vérifiée.</summary>
/// <remarks>
/// <para>
/// <b>C'est CE type que les gestionnaires reçoivent</b>, jamais
/// <see cref="EcritureDeProgramme"/>. Il ne peut pas être construit invalide :
/// la route valide à la frontière, une seule fois, et le reste du code n'a plus
/// à se défendre.
/// </para>
///
/// <para>
/// Le gain n'est pas cosmétique. Un gestionnaire qui aurait pris l'écriture
/// brute aurait dû porter une branche « et si c'était invalide ? » que la route
/// rend inatteignable — une branche morte, que le seuil de couverture aurait
/// signalée à juste titre.
/// </para>
/// </remarks>
public sealed record ProgrammeValide(
    string Nom,
    string? Description,
    bool Actif,
    IReadOnlyList<SeanceValidee> Seances
)
{
    /// <summary>Valide une écriture. Le seul chemin vers ce type.</summary>
    public static (ProgrammeValide? Valide, string? Faute) Lire(EcritureDeProgramme? ecrite)
    {
        if (ecrite is null)
        {
            return (null, "ProgrammeInvalide");
        }

        var nom = ecrite.NomNettoye;
        if (nom.Length == 0)
        {
            return (null, "NomRequis");
        }

        if (nom.Length > EcritureDeProgramme.LongueurMaximaleDuNom)
        {
            return (null, "NomTropLong");
        }

        var description = ecrite.DescriptionNettoyee;
        if (description is { Length: > EcritureDeProgramme.LongueurMaximaleDeLaDescription })
        {
            return (null, "DescriptionTropLongue");
        }

        var seancesEcrites = ecrite.SeancesOuVide;
        if (seancesEcrites.Count > EcritureDeProgramme.SeancesMaximum)
        {
            return (null, "TropDeSeances");
        }

        var seances = new List<SeanceValidee>(seancesEcrites.Count);
        foreach (var seanceEcrite in seancesEcrites)
        {
            var (seance, faute) = SeanceValidee.Lire(seanceEcrite);
            if (faute is not null)
            {
                return (null, faute);
            }

            seances.Add(seance!);
        }

        return (new ProgrammeValide(nom, description, ecrite.Actif, seances), null);
    }
}

/// <summary>Une séance dont la forme a été vérifiée.</summary>
public sealed record SeanceValidee(string Libelle, IReadOnlyList<ExerciceDeSeanceValide> Exercices)
{
    internal static (SeanceValidee? Valide, string? Faute) Lire(EcritureDeSeance? ecrite)
    {
        if (ecrite is null)
        {
            return (null, "SeanceInvalide");
        }

        var libelle = ecrite.LibelleNettoye;
        if (libelle.Length == 0)
        {
            return (null, "LibelleRequis");
        }

        if (libelle.Length > EcritureDeSeance.LongueurMaximaleDuLibelle)
        {
            return (null, "LibelleTropLong");
        }

        var exercicesEcrits = ecrite.ExercicesOuVide;
        if (exercicesEcrits.Count > EcritureDeSeance.ExercicesMaximum)
        {
            return (null, "TropDExercices");
        }

        var exercices = new List<ExerciceDeSeanceValide>(exercicesEcrits.Count);
        foreach (var exerciceEcrit in exercicesEcrits)
        {
            var (exercice, faute) = ExerciceDeSeanceValide.Lire(exerciceEcrit);
            if (faute is not null)
            {
                return (null, faute);
            }

            exercices.Add(exercice!);
        }

        return (new SeanceValidee(libelle, exercices), null);
    }
}

/// <summary>Un exercice de séance dont les cibles ont été vérifiées.</summary>
public sealed record ExerciceDeSeanceValide(
    Guid ExerciceId,
    int Series,
    int RepetitionsMin,
    int? RepetitionsMax,
    int RirCible,
    int ReposSecondes,
    string? Note
)
{
    internal static (ExerciceDeSeanceValide? Valide, string? Faute) Lire(
        EcritureDExerciceDeSeance? ecrit
    )
    {
        if (ecrit is null)
        {
            return (null, "ExerciceDeSeanceInvalide");
        }

        if (ecrit.ExerciceId == Guid.Empty)
        {
            return (null, "ExerciceRequis");
        }

        if (
            ecrit.Series
            is < EcritureDExerciceDeSeance.SeriesMinimum
                or > EcritureDExerciceDeSeance.SeriesMaximum
        )
        {
            return (null, "SeriesHorsBornes");
        }

        if (
            ecrit.RepetitionsMin
            is < EcritureDExerciceDeSeance.RepetitionsMinimum
                or > EcritureDExerciceDeSeance.RepetitionsMaximum
        )
        {
            return (null, "RepetitionsHorsBornes");
        }

        // La borne HAUTE se compare à la basse, pas seulement au maximum. Une
        // cible « 12 à 8 » passerait tous les contrôles individuels et
        // s'afficherait à l'envers.
        if (
            ecrit.RepetitionsMax is { } maximum
            && (maximum < ecrit.RepetitionsMin
                || maximum > EcritureDExerciceDeSeance.RepetitionsMaximum)
        )
        {
            return (null, "RepetitionsHorsBornes");
        }

        if (
            ecrit.RirCible
            is < EcritureDExerciceDeSeance.RirMinimum or > EcritureDExerciceDeSeance.RirMaximum
        )
        {
            return (null, "RirHorsBornes");
        }

        if (
            ecrit.ReposSecondes
            is < EcritureDExerciceDeSeance.ReposMinimum or > EcritureDExerciceDeSeance.ReposMaximum
        )
        {
            return (null, "ReposHorsBornes");
        }

        var note = ecrit.NoteNettoyee;
        if (note is { Length: > EcritureDExerciceDeSeance.LongueurMaximaleDeLaNote })
        {
            return (null, "NoteTropLongue");
        }

        return (
            new ExerciceDeSeanceValide(
                ecrit.ExerciceId,
                ecrit.Series,
                ecrit.RepetitionsMin,
                ecrit.RepetitionsMax,
                ecrit.RirCible,
                ecrit.ReposSecondes,
                note
            ),
            null
        );
    }
}
