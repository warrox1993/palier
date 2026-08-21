using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Palier.Infrastructure.Identite;

/// <summary>
/// Le contexte du rôle d'authentification. Il ne voit que les tables d'identité
/// et les sessions — jamais une donnée de santé.
/// </summary>
/// <remarks>
/// <para>
/// <b>Pourquoi un second contexte plutôt qu'une seconde chaîne sur le premier.</b>
/// <c>PalierDbContext</c> porte les <c>DbSet</c> des séances, des séries et des
/// poids. Le rôle <c>palier_auth</c> n'a aucun privilège dessus — PostgreSQL
/// refuserait par un 42501 — mais un type qui les expose invite à les écrire.
/// Ici, ce qui n'est pas déclaré n'est pas atteignable, et la barrière du moteur
/// n'est plus la seule.
/// </para>
///
/// <para>
/// <b>Il ne génère aucune migration.</b> Le schéma appartient à
/// <c>PalierDbContext</c> : deux contextes qui migreraient les mêmes tables
/// produiraient deux historiques concurrents. <c>FabriqueDeConception</c> ne le
/// référence pas, et c'est délibéré.
/// </para>
///
/// <para>
/// <b>Il est surveillé comme l'autre.</b> <c>ArchitectureTests</c> refuse que
/// tout type hors exemption nommée le prenne en dépendance. Sans cette
/// extension, un contexte neuf aurait échappé au contrôle par construction —
/// c'est-à-dire qu'on aurait ouvert une seconde porte en croyant n'en surveiller
/// qu'une.
/// </para>
/// </remarks>
public sealed class PalierAuthDbContext(DbContextOptions<PalierAuthDbContext> options)
    : IdentityDbContext<Utilisateur, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<SessionRafraichissement> Sessions => Set<SessionRafraichissement>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        base.OnModelCreating(builder);

        // La même correspondance que dans PalierDbContext, sans les relations
        // vers les tables que ce contexte ne voit pas. Les deux déclarations
        // doivent rester d'accord sur les NOMS DE COLONNES ; le schéma, lui,
        // n'est créé que par l'autre.
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
            t.Property(x => x.Device).HasColumnName("device");
            t.Property(x => x.LastSeenAt).HasColumnName("last_seen_at");

            t.HasIndex(x => x.TokenHash).IsUnique();
            t.HasIndex(x => x.OwnerId);
            t.HasIndex(x => x.FamilyId);
        });
    }
}
