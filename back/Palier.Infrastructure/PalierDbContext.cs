using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
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

    protected override void OnModelCreating(ModelBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        base.OnModelCreating(builder);

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
