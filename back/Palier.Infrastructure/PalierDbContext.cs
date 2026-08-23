using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
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

    public DbSet<Workout> Workouts => Set<Workout>();

    public DbSet<WorkoutSet> WorkoutSets => Set<WorkoutSet>();

    public DbSet<BodyWeight> BodyWeights => Set<BodyWeight>();

    /// <summary>Le ressenti par exercice — lot 5, `05-entrainement.md` § 5.</summary>
    public DbSet<ExerciseFeedback> ExerciseFeedbacks => Set<ExerciseFeedback>();

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

        builder.Entity<ExerciseFeedback>(t =>
        {
            t.ToTable("exercise_feedback");
            t.HasKey(x => x.Id);
            t.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            t.Property(x => x.WorkoutId).HasColumnName("workout_id");
            t.Property(x => x.ExerciseId).HasColumnName("exercise_id");
            t.Property(x => x.Feeling).HasColumnName("feeling").IsRequired();
            t.Property(x => x.NotedAt).HasColumnName("noted_at").HasDefaultValueSql("now()");

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
            t.Property(x => x.Name).HasColumnName("name").IsRequired();
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
    }
}
