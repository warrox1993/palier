using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Palier.Domain.Entrainement;
using Palier.Infrastructure.Coffre;
using Palier.Infrastructure.Entites;
using Palier.Infrastructure.Identite;

namespace Palier.Infrastructure;

/// <summary>
/// Le contexte du produit. Il hérite d'<see cref="IdentityDbContext{TUser, TRole, TKey}" />
/// en <see cref="Guid" /> — D35 — et ajoute les cinq tables de la tranche D39.
///
/// Les noms de tables et de colonnes sont posés EXPLICITEMENT en
/// <c>snake_case</c>, sans convertisseur automatique : `docs/16-projet.md` § 2
/// impose `snake_case` en base, et EF Core ne le fait pas nativement. Le paquet
/// qui l'automatiserait serait une dépendance de plus à vérifier, à suivre et à
/// mettre à jour pour une transformation que ce fichier écrit une fois. Les
/// tables d'identité gardent leur nom `AspNet*` : D35 y renvoie nommément, et
/// les renommer casserait les quatorze clés étrangères du schéma documenté.
/// </summary>
public class PalierDbContext(DbContextOptions<PalierDbContext> options)
    : IdentityDbContext<Utilisateur, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<NutrientRef> NutrientRefs => Set<NutrientRef>();

    public DbSet<Exercise> Exercises => Set<Exercise>();

    /// <summary>Les liens de variante entre exercices — lot catalogue, D71.</summary>
    public DbSet<ExerciseVariant> ExerciseVariants => Set<ExerciseVariant>();

    public DbSet<Workout> Workouts => Set<Workout>();

    public DbSet<WorkoutSet> WorkoutSets => Set<WorkoutSet>();

    public DbSet<BodyWeight> BodyWeights => Set<BodyWeight>();

    /// <summary>Le ressenti par exercice — lot 5, `05-entrainement.md` § 5.</summary>
    public DbSet<ExerciseFeedback> ExerciseFeedbacks => Set<ExerciseFeedback>();

    /// <summary>Les contraintes déclarées — lot 5, `05-entrainement.md` § 4.</summary>
    public DbSet<UserConstraint> UserConstraints => Set<UserConstraint>();

    /// <summary>
    /// La vue <c>weekly_volume</c>, en LECTURE seule — lot 5. Elle n'est pas
    /// pilotée par les migrations : <c>ToView</c> l'en empêche, et le SQL de la
    /// vue vit dans la migration du socle, écrit à la main comme D14 l'impose.
    /// </summary>
    public DbSet<WeeklyVolume> WeeklyVolumes => Set<WeeklyVolume>();

    /// <summary>
    /// Les sessions de rafraîchissement — lot 4. Déclarées ici parce que le
    /// schéma appartient à ce contexte et à lui seul ; c'est
    /// <c>PalierAuthDbContext</c> qui les LIT, sous le rôle qui en a le droit.
    /// </summary>
    public DbSet<SessionRafraichissement> Sessions => Set<SessionRafraichissement>();

    /// <summary>
    /// Les enveloppes des clés de données — D59. L'API les LIT au démarrage et
    /// n'en écrit jamais : <c>palier_app</c> n'a que <c>select</c> sur cette
    /// table, et c'est une commande d'exploitation qui pose la première.
    /// </summary>
    /// <summary>
    /// Les programmes — les neuf modèles du catalogue ET ceux des utilisateurs,
    /// dans la même table. D72.
    /// </summary>
    public DbSet<TrainingProgram> Programs => Set<TrainingProgram>();

    /// <summary>Les séances types d'un programme.</summary>
    public DbSet<ProgramDay> ProgramDays => Set<ProgramDay>();

    /// <summary>Les exercices posés dans une séance type, avec leurs cibles.</summary>
    public DbSet<ProgramExercise> ProgramExercises => Set<ProgramExercise>();

    public DbSet<CleDeDonnees> ClesDeDonnees => Set<CleDeDonnees>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        base.OnModelCreating(builder);

        // Les deux colonnes que le verrouillage progressif ajoute à
        // `AspNetUsers`. Identity n'en a l'équivalent d'aucune : son compteur
        // d'échecs n'a pas de fenêtre, et sa durée de verrouillage n'a pas
        // d'escalade. Les noms restent en PascalCase, comme le reste des
        // colonnes d'Identity — mélanger deux conventions dans une même table
        // coûte plus qu'il ne rapporte.
        builder.Entity<Utilisateur>(t =>
        {
            t.Property(x => x.DernierEchecLe).HasColumnName("DernierEchecLe");
            t.Property(x => x.VerrouillagesSubis).HasColumnName("VerrouillagesSubis").HasDefaultValue(0);
            t.Property(x => x.ConsentementSanteLe).HasColumnName("ConsentementSanteLe");
        });

        builder.Entity<ExerciseVariant>(t =>
        {
            t.ToTable("exercise_variants");
            t.HasKey(x => new { x.ExerciseId, x.VariantId });
            t.Property(x => x.ExerciseId).HasColumnName("exercise_id");
            t.Property(x => x.VariantId).HasColumnName("variant_id");

            // CASCADE des deux côtés : un lien de variante n'a aucun sens sans
            // ses deux exercices. C'est le seul endroit du schéma où la
            // suppression en cascade est évidente — la ligne ne porte aucune
            // donnée propre, elle n'est QUE la relation.
            t.HasOne<Exercise>()
                .WithMany()
                .HasForeignKey(x => x.ExerciseId)
                .OnDelete(DeleteBehavior.Cascade);

            t.HasOne<Exercise>()
                .WithMany()
                .HasForeignKey(x => x.VariantId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ExerciseFeedback>(t =>
        {
            t.ToTable("exercise_feedback");
            t.HasKey(x => x.Id);
            t.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            t.Property(x => x.WorkoutId).HasColumnName("workout_id");
            t.Property(x => x.ExerciseId).HasColumnName("exercise_id");
            t.Property(x => x.Rating).HasColumnName("rating").IsRequired();

            // UN ressenti par exercice et par séance. Changer d'avis en cours
            // de séance est un remplacement, pas un second avis : les seuils du
            // § 5 — deux `pain` consécutifs, trois `meh` consécutifs —
            // compteraient faux sur des doublons.
            t.HasIndex(x => new { x.WorkoutId, x.ExerciseId })
                .IsUnique()
                .HasDatabaseName("ux_exercise_feedback_workout_exercise");

            // La séance emporte ses ressentis, comme elle emporte ses séries.
            t.HasOne<Workout>()
                .WithMany()
                .HasForeignKey(x => x.WorkoutId)
                .OnDelete(DeleteBehavior.Cascade);

            // RESTRICT sur l'exercice, comme `sets` : supprimer un exercice ne
            // doit pas effacer silencieusement l'historique qui le référence.
            t.HasOne<Exercise>()
                .WithMany()
                .HasForeignKey(x => x.ExerciseId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<UserConstraint>(t =>
        {
            t.ToTable("user_constraints");
            t.HasKey(x => x.Id);
            t.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            t.Property(x => x.OwnerId).HasColumnName("owner_id");
            t.Property(x => x.Region).HasColumnName("region").IsRequired();
            t.Property(x => x.Severity).HasColumnName("severity").IsRequired().HasDefaultValue("modere");
            t.Property(x => x.Note).HasColumnName("note");
            t.Property(x => x.DeclaredAt).HasColumnName("declared_at").HasDefaultValueSql("now()");

            // UNE ligne par région. Déclarer deux fois la même contrainte est
            // un remplacement, pas un doublon.
            t.HasIndex(x => new { x.OwnerId, x.Region })
                .IsUnique()
                .HasDatabaseName("ux_user_constraints_owner_region");

            // Article 17 : les contraintes ne survivent pas au compte. Elles
            // relèvent de l'article 9 — une contrainte cervicale ou lombaire
            // est une donnée de santé.
            t.HasOne<Utilisateur>()
                .WithMany()
                .HasForeignKey(x => x.OwnerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // La vue du volume hebdomadaire. `HasNoKey` parce qu'une vue agrégée
        // n'a pas d'identité de ligne, et `ToView` pour qu'aucune migration ne
        // tente de la créer ou de la supprimer — elle appartient au SQL écrit
        // à la main de la migration du socle.
        builder.Entity<WeeklyVolume>(t =>
        {
            t.HasNoKey();
            t.ToView("weekly_volume");
            t.Property(x => x.OwnerId).HasColumnName("owner_id");
            t.Property(x => x.Week).HasColumnName("week");
            t.Property(x => x.Muscle).HasColumnName("muscle");
            t.Property(x => x.HardSets).HasColumnName("hard_sets");
        });

        builder.Entity<SessionRafraichissement>(t =>
        {
            t.ToTable("sessions_refresh");
            t.HasKey(x => x.Id);
            t.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            t.Property(x => x.OwnerId).HasColumnName("owner_id");
            t.Property(x => x.TokenHash).HasColumnName("token_hash").IsRequired();
            t.Property(x => x.FamilyId).HasColumnName("family_id");
            t.Property(x => x.CreatedAt).HasColumnName("created_at");
            t.Property(x => x.ExpiresAt).HasColumnName("expires_at");
            t.Property(x => x.ConsumedAt).HasColumnName("consumed_at");
            t.Property(x => x.RevokedAt).HasColumnName("revoked_at");
            t.Property(x => x.ReplacedById).HasColumnName("replaced_by_id");
            t.Property(x => x.SuccessorSealed).HasColumnName("successor_sealed");
            t.Property(x => x.Device).HasColumnName("device");
            t.Property(x => x.LastSeenAt).HasColumnName("last_seen_at");

            // L'empreinte est unique : deux sessions ne peuvent pas porter le
            // même jeton, et la recherche au rafraîchissement passe par cet index.
            t.HasIndex(x => x.TokenHash).IsUnique();
            t.HasIndex(x => x.OwnerId);
            t.HasIndex(x => x.FamilyId);

            // Article 17 : les sessions ne survivent pas au compte.
            t.HasOne<Utilisateur>()
                .WithMany()
                .HasForeignKey(x => x.OwnerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<CleDeDonnees>(t =>
        {
            t.ToTable("cles_de_donnees");
            t.HasKey(x => x.Id);
            t.Property(x => x.Id).HasColumnName("id");
            t.Property(x => x.Enveloppe).HasColumnName("enveloppe").IsRequired();
            t.Property(x => x.CreeeLe).HasColumnName("creee_le").IsRequired();
        });

        builder.Entity<NutrientRef>(t =>
        {
            t.ToTable("nutrient_refs");
            t.HasKey(x => x.Nutrient);
            t.Property(x => x.Nutrient).HasColumnName("nutrient");
            t.Property(x => x.LabelFr).HasColumnName("label_fr").IsRequired();
            t.Property(x => x.LabelEn).HasColumnName("label_en").IsRequired();
            t.Property(x => x.Unit).HasColumnName("unit").IsRequired();
            t.Property(x => x.AiMale).HasColumnName("ai_male");
            t.Property(x => x.AiFemale).HasColumnName("ai_female");
            t.Property(x => x.Ul).HasColumnName("ul");
            t.Property(x => x.PerKg).HasColumnName("per_kg").HasDefaultValue(false);
            t.Property(x => x.Source).HasColumnName("source").IsRequired();
            t.Property(x => x.SourceYear).HasColumnName("source_year");
        });

        builder.Entity<Exercise>(t =>
        {
            t.ToTable("exercises");
            t.HasKey(x => x.Id);
            t.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            t.Property(x => x.Slug).HasColumnName("slug");
            t.Property(x => x.NameFr).HasColumnName("name_fr").IsRequired();
            t.Property(x => x.NameEn).HasColumnName("name_en");
            t.Property(x => x.InstructionsFr).HasColumnName("instructions_fr");
            t.Property(x => x.InstructionsEn).HasColumnName("instructions_en");
            t.Property(x => x.CommonErrorsFr).HasColumnName("common_errors_fr");
            t.Property(x => x.CommonErrorsEn).HasColumnName("common_errors_en");
            t.Property(x => x.MovementRole)
                .HasColumnName("movement_role")
                .IsRequired()
                .HasDefaultValue("aucun");

            // L'unicité est PARTIELLE : elle ne porte que sur le catalogue
            // public. Un utilisateur n'écrit pas de slug, et deux exercices
            // personnalisés sans slug ne doivent pas se gêner.
            t.HasIndex(x => x.Slug)
                .IsUnique()
                .HasFilter("is_custom = false")
                .HasDatabaseName("ux_exercises_slug");

            // La clé de recherche — D73. Une colonne GÉNÉRÉE et stockée : le
            // moteur la tient à jour, et aucun chemin d'écriture ne peut
            // l'oublier. Un déclencheur aurait fait le même travail en laissant
            // la possibilité de le contourner.
            //
            // L'EXPRESSION VIENT DU DOMAINE, elle n'est pas écrite ici. La même
            // règle sert à normaliser le terme tapé par l'utilisateur, et les
            // deux doivent changer ensemble : deux listes de caractères à tenir
            // identiques à la main auraient divergé.
            t.Property(x => x.SearchKey)
                .HasColumnName("search_key")
                .HasComputedColumnSql(CleDeRecherche.ExpressionSql, stored: true);

            t.Property(x => x.Equipment).HasColumnName("equipment");
            t.Property(x => x.PrimaryMuscles).HasColumnName("primary_muscles").IsRequired();
            t.Property(x => x.SecondaryMuscles)
                .HasColumnName("secondary_muscles")
                .HasDefaultValueSql("'{}'");
            t.Property(x => x.IsUnilateral).HasColumnName("is_unilateral").HasDefaultValue(false);
            t.Property(x => x.DefaultIncrement)
                .HasColumnName("default_increment")
                .HasDefaultValue(2.5m);
            t.Property(x => x.ContraindicatedFor)
                .HasColumnName("contraindicated_for")
                .HasDefaultValueSql("'{}'");
            t.Property(x => x.IsCustom).HasColumnName("is_custom").HasDefaultValue(false);
            t.Property(x => x.OwnerId).HasColumnName("owner_id");
            t.HasOne<Utilisateur>()
                .WithMany()
                .HasForeignKey(x => x.OwnerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Workout>(t =>
        {
            t.ToTable(
                "workouts",
                b =>
                    // La borne du schéma, écrite comme contrainte du moteur et non
                    // comme validation applicative : une écriture directe, une
                    // restauration ou un `FromSqlRaw` la rencontrent aussi.
                    b.HasCheckConstraint("ck_workouts_energy_1_5", "energy_1_5 between 1 and 5")
            );
            t.HasKey(x => x.Id);
            t.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            t.Property(x => x.OwnerId).HasColumnName("owner_id");
            t.Property(x => x.ProgramDayId).HasColumnName("program_day_id");
            t.Property(x => x.StartedAt).HasColumnName("started_at").HasDefaultValueSql("now()");
            t.Property(x => x.EndedAt).HasColumnName("ended_at");
            t.Property(x => x.SleepHours).HasColumnName("sleep_hours");
            t.Property(x => x.Energy15).HasColumnName("energy_1_5");
            t.Property(x => x.Note).HasColumnName("note");
            t.HasOne<Utilisateur>()
                .WithMany()
                .HasForeignKey(x => x.OwnerId)
                .OnDelete(DeleteBehavior.Cascade);
            t.HasIndex(x => new { x.OwnerId, x.StartedAt })
                .HasDatabaseName("ix_workouts_owner_id_started_at")
                .IsDescending(false, true);

            // `SetNull` et non `Cascade` : supprimer un programme ne doit pas
            // effacer les séances réellement faites. L'historique appartient à
            // l'utilisateur, pas au plan qu'il suivait.
            t.HasOne<ProgramDay>()
                .WithMany()
                .HasForeignKey(x => x.ProgramDayId)
                .OnDelete(DeleteBehavior.SetNull);

            // Le suivi d'un programme interroge « quelles séances viennent de
            // ce jour type ». Sans index, la question coûte un parcours complet
            // de l'historique à chaque ouverture de l'écran.
            t.HasIndex(x => x.ProgramDayId).HasDatabaseName("ix_workouts_program_day_id");
        });

        builder.Entity<WorkoutSet>(t =>
        {
            t.ToTable("sets");
            t.HasKey(x => x.Id);
            t.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            t.Property(x => x.WorkoutId).HasColumnName("workout_id");
            t.Property(x => x.ExerciseId).HasColumnName("exercise_id");
            t.Property(x => x.SetIndex).HasColumnName("set_index");
            t.Property(x => x.WeightKg).HasColumnName("weight_kg");
            t.Property(x => x.Reps).HasColumnName("reps");
            t.Property(x => x.Rir).HasColumnName("rir");
            t.Property(x => x.IsWarmup).HasColumnName("is_warmup").HasDefaultValue(false);
            t.Property(x => x.LoggedAt).HasColumnName("logged_at").HasDefaultValueSql("now()");
            t.HasOne<Workout>()
                .WithMany()
                .HasForeignKey(x => x.WorkoutId)
                .OnDelete(DeleteBehavior.Cascade);
            t.HasOne<Exercise>()
                .WithMany()
                .HasForeignKey(x => x.ExerciseId)
                .OnDelete(DeleteBehavior.Restrict);
            t.HasIndex(x => x.WorkoutId).HasDatabaseName("ix_sets_workout_id");
        });

        builder.Entity<BodyWeight>(t =>
        {
            t.ToTable("body_weight");
            t.HasKey(x => x.Id);
            t.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            t.Property(x => x.OwnerId).HasColumnName("owner_id");
            t.Property(x => x.MeasuredOn).HasColumnName("measured_on");
            t.Property(x => x.WeightKg).HasColumnName("weight_kg");
            t.HasOne<Utilisateur>()
                .WithMany()
                .HasForeignKey(x => x.OwnerId)
                .OnDelete(DeleteBehavior.Cascade);
            t.HasIndex(x => new { x.OwnerId, x.MeasuredOn })
                .HasDatabaseName("ux_body_weight_owner_id_measured_on")
                .IsUnique();
        });

        builder.Entity<TrainingProgram>(t =>
        {
            t.ToTable(
                "programs",
                b =>
                {
                    // Un MODÈLE doit être complet. Les mêmes colonnes restent
                    // libres pour le programme d'un utilisateur, qui donne un
                    // nom et rien d'autre — c'est D70, transposé mot pour mot
                    // du catalogue d'exercices.
                    //
                    // Sans cette contrainte, un modèle sans note entrerait au
                    // catalogue public, et `14-contenu.md` § 2 dit précisément
                    // ce que ça coûte : « Un utilisateur qui comprend pourquoi
                    // un exercice est absent l'accepte ; sinon il le rajoute et
                    // se blesse. »
                    b.HasCheckConstraint(
                        "ck_programs_modele_complet",
                        "not is_template or (slug is not null and name_en is not null "
                            + "and description_fr is not null and description_en is not null "
                            + "and notes_fr is not null and notes_en is not null "
                            + "and frequency_min is not null and frequency_max is not null)"
                    );

                    // Les deux populations de D72, dites au moteur : un modèle
                    // n'a PAS de propriétaire, un programme personnel en a un.
                    // La règle vit ici et non dans le code applicatif, parce
                    // qu'une restauration ou une écriture directe la rencontre
                    // aussi.
                    b.HasCheckConstraint(
                        "ck_programs_proprietaire",
                        "(is_template and owner_id is null) or (not is_template and owner_id is not null)"
                    );

                    // La liste fermée des régions de `user_constraints`. NUL
                    // est permis : la plupart des modèles ne visent aucune
                    // contrainte.
                    b.HasCheckConstraint(
                        "ck_programs_targets_constraint",
                        "targets_constraint is null or targets_constraint in "
                            + "('cervicale', 'lombaire', 'epaule', 'genou', 'hanche', 'poignet', 'cheville')"
                    );

                    b.HasCheckConstraint(
                        "ck_programs_frequence",
                        "frequency_min is null or (frequency_min between 1 and 7 "
                            + "and frequency_max between frequency_min and 7)"
                    );
                }
            );
            t.HasKey(x => x.Id);
            t.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            t.Property(x => x.Slug).HasColumnName("slug");
            t.Property(x => x.OwnerId).HasColumnName("owner_id");
            t.Property(x => x.IsTemplate).HasColumnName("is_template").HasDefaultValue(false);
            t.Property(x => x.NameFr).HasColumnName("name_fr").IsRequired();
            t.Property(x => x.NameEn).HasColumnName("name_en");
            t.Property(x => x.DescriptionFr).HasColumnName("description_fr");
            t.Property(x => x.DescriptionEn).HasColumnName("description_en");
            t.Property(x => x.NotesFr).HasColumnName("notes_fr");
            t.Property(x => x.NotesEn).HasColumnName("notes_en");
            t.Property(x => x.FrequencyMin).HasColumnName("frequency_min");
            t.Property(x => x.FrequencyMax).HasColumnName("frequency_max");
            t.Property(x => x.TargetsConstraint).HasColumnName("targets_constraint");
            t.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true);
            t.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");

            // Unicité PARTIELLE, comme sur les exercices : elle ne porte que
            // sur les modèles. Un utilisateur n'écrit pas de slug, et deux
            // programmes personnels sans slug ne doivent pas se gêner.
            t.HasIndex(x => x.Slug)
                .IsUnique()
                .HasFilter("is_template = true")
                .HasDatabaseName("ux_programs_slug");

            t.HasIndex(x => x.OwnerId).HasDatabaseName("ix_programs_owner_id");

            t.HasOne<Utilisateur>()
                .WithMany()
                .HasForeignKey(x => x.OwnerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ProgramDay>(t =>
        {
            t.ToTable(
                "program_days",
                b => b.HasCheckConstraint("ck_program_days_position", "position >= 1")
            );
            t.HasKey(x => x.Id);
            t.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            t.Property(x => x.ProgramId).HasColumnName("program_id");
            t.Property(x => x.LabelFr).HasColumnName("label_fr").IsRequired();
            t.Property(x => x.LabelEn).HasColumnName("label_en");
            t.Property(x => x.Position).HasColumnName("position");

            // Deux séances ne partagent pas un rang. La contrainte tient la
            // promesse que l'écran fait en les numérotant.
            t.HasIndex(x => new { x.ProgramId, x.Position })
                .IsUnique()
                .HasDatabaseName("ux_program_days_program_id_position");

            t.HasOne<TrainingProgram>()
                .WithMany()
                .HasForeignKey(x => x.ProgramId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ProgramExercise>(t =>
        {
            t.ToTable(
                "program_exercises",
                b =>
                {
                    b.HasCheckConstraint("ck_program_exercises_position", "position >= 1");

                    // Les bornes du raisonnable, posées au moteur. Elles ne
                    // remplacent pas la validation applicative — elles la
                    // doublent sur le chemin qu'une écriture directe emprunte.
                    b.HasCheckConstraint(
                        "ck_program_exercises_series",
                        "target_sets between 1 and 20"
                    );
                    b.HasCheckConstraint(
                        "ck_program_exercises_repetitions",
                        "target_reps_min between 1 and 100 "
                            + "and (target_reps_max is null or target_reps_max between target_reps_min and 100)"
                    );
                    b.HasCheckConstraint("ck_program_exercises_rir", "target_rir between 0 and 10");
                    b.HasCheckConstraint(
                        "ck_program_exercises_repos",
                        "rest_seconds between 0 and 900"
                    );
                }
            );
            t.HasKey(x => x.Id);
            t.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            t.Property(x => x.ProgramDayId).HasColumnName("program_day_id");
            t.Property(x => x.ExerciseId).HasColumnName("exercise_id");
            t.Property(x => x.Position).HasColumnName("position");
            t.Property(x => x.TargetSets).HasColumnName("target_sets");
            t.Property(x => x.TargetRepsMin).HasColumnName("target_reps_min");
            t.Property(x => x.TargetRepsMax).HasColumnName("target_reps_max");
            t.Property(x => x.TargetRir).HasColumnName("target_rir");
            t.Property(x => x.RestSeconds).HasColumnName("rest_seconds");
            t.Property(x => x.Note).HasColumnName("note");

            t.HasIndex(x => new { x.ProgramDayId, x.Position })
                .IsUnique()
                .HasDatabaseName("ux_program_exercises_day_id_position");

            t.HasOne<ProgramDay>()
                .WithMany()
                .HasForeignKey(x => x.ProgramDayId)
                .OnDelete(DeleteBehavior.Cascade);

            // `Restrict`, comme partout où le catalogue est référencé : un
            // exercice cité par un programme ne disparaît pas sous lui. La
            // route de suppression traduit ce refus en 409 nommé.
            t.HasOne<Exercise>()
                .WithMany()
                .HasForeignKey(x => x.ExerciseId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
