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

#pragma warning restore CA1819
