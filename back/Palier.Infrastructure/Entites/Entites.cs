namespace Palier.Infrastructure.Entites;

// Les cinq tables non-identité de la tranche `SocleInitial` — D39. Elles ne
// sont pas choisies au hasard : elles portent UNE représentante de chacune des
// cinq formes de table du schéma, pour que chaque forme de politique soit
// éprouvée dès ce lot et que le suivant ajoute des tables sans inventer de
// mécanisme.
//
//   nutrient_refs  référence publique       — aucun owner_id
//   exercises      catalogue mixte          — owner_id NULLABLE, is_custom
//   workouts       possédée directe         — owner_id not null
//   sets           possédée par jointure    — aucun owner_id, passe par workouts
//   body_weight    possédée directe         — owner_id not null, contrainte unique
//
// Les noms de types et de propriétés suivent `docs/03-donnees.md` et la spec
// d'architecture § 4, qui nomme déjà `Workout`, `Set`, `Exercise`,
// `NutrientRef`. Traduire ici ouvrirait une occasion de diverger du seul
// document qui fait autorité sur le schéma.
//
// CA1819 « les propriétés ne doivent pas retourner de tableaux » est éteinte
// pour ce fichier et pour lui seul. Trois colonnes du schéma sont des `text[]`
// PostgreSQL — `primary_muscles`, `secondary_muscles`, `contraindicated_for` —
// et le tableau est le seul type que Npgsql y fasse correspondre sans
// convertisseur. Le motif de la règle, « un appelant peut modifier le tableau
// qu'on lui rend », vise une API publiée ; ces types sont des lignes de table,
// et EF Core doit pouvoir écrire dedans. La borne est ici, dans le fichier
// concerné, plutôt que dans `.editorconfig` où elle s'appliquerait à des
// fichiers que personne n'a relus (D21).
#pragma warning disable CA1819

/// <summary>Référence publique : les valeurs EFSA. En base, jamais en dur.</summary>
public sealed class NutrientRef
{
    /// <summary>La clé, en <c>text</c> : « zinc_mg », « protein_g »…</summary>
    public required string Nutrient { get; set; }

    public required string LabelFr { get; set; }

    public required string LabelEn { get; set; }

    public required string Unit { get; set; }

    /// <summary>Apport adéquat, homme.</summary>
    public decimal? AiMale { get; set; }

    /// <summary>Apport adéquat, femme.</summary>
    public decimal? AiFemale { get; set; }

    /// <summary>Limite haute, NULL si inexistante.</summary>
    public decimal? Ul { get; set; }

    public bool PerKg { get; set; }

    public required string Source { get; set; }

    public int? SourceYear { get; set; }
}

/// <summary>
/// Catalogue mixte : les exercices du catalogue public (<c>is_custom = false</c>,
/// <c>owner_id</c> nul) et ceux que l'utilisateur a créés.
/// </summary>
public sealed class Exercise
{
    public Guid Id { get; set; }

    public required string Name { get; set; }

    public string? Equipment { get; set; }

    public required string[] PrimaryMuscles { get; set; }

    public string[] SecondaryMuscles { get; set; } = [];

    public bool IsUnilateral { get; set; }

    public decimal DefaultIncrement { get; set; }

    /// <summary>Régions contre-indiquées : filtrage automatique.</summary>
    public string[] ContraindicatedFor { get; set; } = [];

    public bool IsCustom { get; set; }

    /// <summary>Nul pour le catalogue public. C'est ce qui fait la « branche publique ».</summary>
    public Guid? OwnerId { get; set; }
}

/// <summary>Possédée directe : une séance appartient à un utilisateur et à un seul.</summary>
public sealed class Workout
{
    public Guid Id { get; set; }

    public Guid OwnerId { get; set; }

    public DateTimeOffset StartedAt { get; set; }

    public DateTimeOffset? EndedAt { get; set; }

    public decimal? SleepHours { get; set; }

    /// <summary>Borné par un <c>CHECK</c> entre 1 et 5, écrit dans la migration.</summary>
    public int? Energy15 { get; set; }

    public string? Note { get; set; }
}

/// <summary>
/// Possédée PAR JOINTURE : aucune colonne <c>owner_id</c>. Sa politique doit
/// remonter jusqu'à <c>workouts</c>, et c'est toute la difficulté de cette forme.
///
/// Le type ne s'appelle pas <c>Set</c> comme la table : CA1716 refuse un
/// identifiant qui est un mot réservé d'un langage .NET — <c>Set</c> l'est en
/// Visual Basic — et `TreatWarningsAsErrors` en fait une erreur de compilation.
/// Mesuré le 20/08/2026. La table, elle, reste <c>sets</c>.
/// </summary>
public sealed class WorkoutSet
{
    public Guid Id { get; set; }

    public Guid WorkoutId { get; set; }

    public Guid ExerciseId { get; set; }

    public int SetIndex { get; set; }

    public decimal? WeightKg { get; set; }

    public int? Reps { get; set; }

    public int? Rir { get; set; }

    public bool IsWarmup { get; set; }

    public DateTimeOffset LoggedAt { get; set; }
}

/// <summary>Possédée directe, avec une contrainte d'unicité par jour.</summary>
public sealed class BodyWeight
{
    public Guid Id { get; set; }

    public Guid OwnerId { get; set; }

    public DateOnly MeasuredOn { get; set; }

    public decimal WeightKg { get; set; }
}

/// <summary>
/// Une ligne de la vue <c>weekly_volume</c> : séries dures par muscle et par
/// semaine.
/// </summary>
/// <remarks>
/// <para>
/// <b>C'est une VUE, pas une table.</b> Elle est déclarée <c>HasNoKey</c> et
/// <c>ToView</c>, ce qui la rend strictement en lecture et empêche EF Core de
/// vouloir la créer : elle est posée par <c>migrationBuilder.Sql</c> au lot 2,
/// avec la logique de dépliage qu'aucun modèle objet n'exprimerait — un muscle
/// primaire compte 1, un secondaire compte 0,5.
/// </para>
///
/// <para>
/// <b>Elle porte <c>security_invoker = true</c></b>, donc elle s'exécute sous
/// les politiques de l'APPELANT. Sans cela, elle tournerait avec les droits de
/// son propriétaire — <c>palier_migrations</c>, qui possède toutes les tables —
/// et serait le seul chemin du schéma où l'isolation change de règle sans que
/// personne le voie. Une épreuve d'isolation le prouve sur la vue elle-même.
/// </para>
/// </remarks>
public sealed class WeeklyVolume
{
    public Guid OwnerId { get; set; }

    /// <summary>Le LUNDI de la semaine — <c>date_trunc('week', ...)</c>.</summary>
    public DateTimeOffset Week { get; set; }

    public required string Muscle { get; set; }

    /// <summary>Décimal, parce qu'un muscle secondaire compte pour une demi-série.</summary>
    public decimal HardSets { get; set; }
}

/// <summary>
/// Le ressenti d'un exercice au sein d'une séance —
/// <c>docs/05-entrainement.md</c> § 5.
/// </summary>
/// <remarks>
/// <para>
/// <b>Possédée PAR JOINTURE, comme <see cref="WorkoutSet" /></b> : aucune
/// colonne <c>owner_id</c>. Sa politique remonte jusqu'à <c>workouts</c>, et
/// c'est toute la difficulté de cette forme — une politique qui oublierait la
/// remontée laisserait lire les ressentis de tout le monde, sans qu'une seule
/// ligne de code applicatif ne le montre.
/// </para>
///
/// <para>
/// <b>Un ressenti par exercice et par séance</b>, garanti par
/// <c>unique (workout_id, exercise_id)</c> : changer d'avis en cours de séance
/// est un remplacement, pas un second avis. Les seuils du § 5 — « 2 pain
/// consécutifs », « 3 meh consécutifs » — compteraient faux sur des doublons.
/// </para>
/// </remarks>
public sealed class ExerciseFeedback
{
    public Guid Id { get; set; }

    public Guid WorkoutId { get; set; }

    public Guid ExerciseId { get; set; }

    /// <summary>
    /// <c>good</c>, <c>meh</c> ou <c>pain</c>. Borné par un <c>CHECK</c> écrit
    /// dans la migration : la contrainte applicative refuse déjà, mais elle ne
    /// protège pas d'une écriture faite hors de l'API.
    /// </summary>
    public required string Feeling { get; set; }

    public DateTimeOffset NotedAt { get; set; }
}

/// <summary>
/// Une contrainte déclarée par l'utilisateur —
/// <c>docs/05-entrainement.md</c> § 4, « le second différenciateur ».
/// </summary>
/// <remarks>
/// <para>
/// <b>Possédée DIRECTE</b>, contrairement à <see cref="ExerciseFeedback" /> :
/// elle porte son <c>owner_id</c>, et sa politique est la forme la plus simple
/// du schéma. C'est aussi la forme la plus facile à écrire juste, donc celle où
/// une erreur se verrait le moins.
/// </para>
///
/// <para>
/// <b>Une ligne par région</b>, garantie par
/// <c>unique (owner_id, region)</c> : déclarer deux fois la même contrainte est
/// un remplacement, pas un doublon. Sans cette contrainte, le filtrage du
/// catalogue — lot 6 — dédupliquerait à la lecture, ou ne dédupliquerait pas.
/// </para>
/// </remarks>
public sealed class UserConstraint
{
    public Guid Id { get; set; }

    public Guid OwnerId { get; set; }

    /// <summary>
    /// <c>cervicale</c>, <c>lombaire</c>, <c>epaule</c> ou <c>genou</c>. Borné
    /// par un <c>CHECK</c> écrit dans la migration, et par l'énumération
    /// <c>Contrainte</c> côté applicatif — les deux, parce que le CHECK
    /// protège aussi d'une écriture faite hors de l'API.
    /// </summary>
    public required string Region { get; set; }

    public DateTimeOffset DeclaredAt { get; set; }
}

#pragma warning restore CA1819
