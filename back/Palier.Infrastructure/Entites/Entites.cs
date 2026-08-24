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

    /// <summary>
    /// La clé naturelle du catalogue — <c>developpe-couche-barre</c>. NULLE pour
    /// un exercice personnalisé.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Elle résout deux problèmes d'un coup — D68. D'abord l'IDEMPOTENCE du
    /// référentiel : <c>02-exercises.sql</c> doit pouvoir être rejoué après un
    /// <c>db:reset</c> ou une correction, et un <c>on conflict (slug)</c> le
    /// permet là où il faudrait sinon figer des UUID dans le fichier — que
    /// personne ne pourrait relire.
    /// </para>
    ///
    /// <para>
    /// Ensuite les VARIANTES : un exercice se lie à un autre par son slug, pas
    /// par un identifiant qu'aucun humain ne peut vérifier à la lecture.
    /// </para>
    ///
    /// <para>
    /// L'unicité est PARTIELLE — <c>where is_custom = false</c>. Un utilisateur
    /// qui nomme son exercice comme un exercice du catalogue ne doit pas être
    /// refusé, et il n'écrit pas de slug.
    /// </para>
    /// </remarks>
    public string? Slug { get; set; }

    /// <summary>Le nom français.</summary>
    public required string NameFr { get; set; }

    /// <summary>
    /// Le nom anglais. NUL pour un exercice personnalisé, EXIGÉ au catalogue.
    /// </summary>
    /// <remarks>
    /// <b>Des colonnes suffixées plutôt qu'une table de traductions</b> — D67.
    /// Deux langues, figées par <c>11-qualite.md</c> : « français et anglais dès
    /// la V1 ». Et surtout, la contrainte <c>CHECK</c> rend l'oubli impossible :
    /// une table de traductions aurait permis un exercice sans sa ligne
    /// anglaise, silencieusement, et l'écran aurait affiché un trou.
    /// </remarks>
    public string? NameEn { get; set; }

    /// <summary>Les consignes d'exécution, en français. Exigées au catalogue.</summary>
    /// <remarks>
    /// <b>Elles décrivent le mouvement, elles ne prescrivent rien.</b>
    /// « Genoux dans l'axe des pieds » dit comment faire ; « vous devriez
    /// muscler vos quadriceps » serait un conseil, que
    /// <c>01-conformite.md</c> § 2 interdit.
    /// </remarks>
    public string? InstructionsFr { get; set; }

    /// <inheritdoc cref="InstructionsFr" />
    public string? InstructionsEn { get; set; }

    /// <summary>Les erreurs fréquentes, en français. Exigées au catalogue.</summary>
    public string? CommonErrorsFr { get; set; }

    /// <inheritdoc cref="CommonErrorsFr" />
    public string? CommonErrorsEn { get; set; }

    /// <summary>
    /// <c>tirage</c>, <c>poussee</c> ou <c>aucun</c> — <c>05-entrainement.md</c> § 4.
    /// </summary>
    /// <remarks>
    /// <b>Elle ferme le report de D62.</b> Le ratio tirage/poussée était
    /// calculable par le domaine depuis le lot 3, mais rien en base ne disait
    /// si un mouvement tire ou pousse — et le déduire des muscles aurait été
    /// faux : un pull-over travaille les pectoraux ET le grand dorsal.
    ///
    /// <c>aucun</c> n'est pas un défaut par paresse : le document exclut
    /// explicitement le deltoïde latéral du ratio, « il ne tire ni ne pousse ».
    /// </remarks>
    public required string MovementRole { get; set; }

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
///
/// <para>
/// <b>AUCUNE date de saisie</b>, et c'est une correction. La table en portait
/// une à la livraison du lot 5, et l'ordre du § 5 s'en servait — à tort : la
/// chronologie qui compte est celle des SÉANCES, pas celle du moment où l'on a
/// tapé la note. Noter après coup le ressenti d'une séance ancienne l'aurait
/// placée en tête, et « deux <c>pain</c> consécutifs » aurait désigné deux
/// séances qui ne se suivent pas.
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
    /// <remarks>
    /// Le nom vient de <c>docs/03-donnees.md</c>, qui fait autorité sur le
    /// schéma — <c>CLAUDE.md</c> § 5. Il s'appelait <c>feeling</c> à la
    /// livraison du lot 5, par inattention à ce document.
    /// </remarks>
    public required string Rating { get; set; }
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

    /// <summary>
    /// <c>leger</c>, <c>modere</c> ou <c>strict</c> — <c>docs/03-donnees.md</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>C'est le réglage de l'utilisateur, et il est PAR CONTRAINTE.</b> Une
    /// épaule strictement contre-indiquée et un genou légèrement sensible
    /// n'appellent pas le même traitement, et un réglage global d'affichage
    /// aurait forcé à choisir un seul comportement pour les deux.
    /// </para>
    ///
    /// <para>
    /// Elle ne fait RIEN filtrer. Elle voyage avec le marquage du catalogue, et
    /// c'est l'écran qui décide de la présentation — un exercice contre-indiqué
    /// pour une contrainte <c>stricte</c> se replie par défaut, un
    /// <c>leger</c> s'affiche marqué. Voir le doc de <c>ExerciceRendu</c>.
    /// </para>
    /// </remarks>
    public required string Severity { get; set; }

    /// <summary>
    /// Un aide-mémoire libre — « douleur à la flexion complète ».
    /// </summary>
    /// <remarks>
    /// <b>Il n'atteint AUCUN calcul et AUCUN modèle.</b> C'est une donnée de
    /// santé au sens de l'article 9, en texte libre : la faire voyager vers un
    /// fournisseur de modèle demanderait la base légale que
    /// <c>docs/13-juridique.md</c> § 2 encadre, pour un bénéfice nul — le
    /// raisonnement se fait sur la région et la sévérité, qui sont des valeurs
    /// fermées.
    /// </remarks>
    public string? Note { get; set; }

    public DateTimeOffset DeclaredAt { get; set; }
}

/// <summary>
/// Un lien de variante entre deux exercices du catalogue —
/// <c>docs/05-entrainement.md</c> § 6.
/// </summary>
/// <remarks>
/// <para>
/// <b>Une table de liens, jamais un tableau d'identifiants</b> — D71. Un
/// <c>uuid[]</c> aurait coûté une colonne au lieu d'une table, mais PostgreSQL
/// ne contraint pas les références dans un tableau : une variante pointant sur
/// un exercice supprimé y resterait, et le produit proposerait un remplacement
/// qui n'existe plus.
/// </para>
///
/// <para>
/// Le lien n'est PAS symétrique en base : « développé haltères est une variante
/// de développé barre » se déclare dans un sens, et le référentiel déclare les
/// deux quand la réciproque a du sens. Forcer la symétrie par un déclencheur
/// coûterait plus que de l'écrire.
/// </para>
/// </remarks>
public sealed class ExerciseVariant
{
    public Guid ExerciseId { get; set; }

    public Guid VariantId { get; set; }
}

#pragma warning restore CA1819
