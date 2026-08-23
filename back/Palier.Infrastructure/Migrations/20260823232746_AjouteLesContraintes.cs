using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Palier.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AjouteLesContraintes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "user_constraints",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    region = table.Column<string>(type: "text", nullable: false),
                    declared_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_constraints", x => x.id);
                    table.ForeignKey(
                        name: "FK_user_constraints_AspNetUsers_owner_id",
                        column: x => x.owner_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ux_user_constraints_owner_region",
                table: "user_constraints",
                columns: new[] { "owner_id", "region" },
                unique: true);

            // ================================================================
            // Ce qu'EF Core ne pilote pas, et qui s'écrit donc à la main — D14.
            // ================================================================
            migrationBuilder.Sql(
                """
                -- Les QUATRE régions de `docs/05-entrainement.md` § 4, et rien
                -- d'autre. La validation applicative refuse déjà, mais elle ne
                -- protège pas d'une écriture faite hors de l'API — et une
                -- cinquième valeur en base ferait manquer le filtrage du
                -- catalogue sans erreur visible : l'intersection avec
                -- `exercises.contraindicated_for` ne trouverait simplement
                -- rien.
                --
                -- Les valeurs sont EN MINUSCULES SANS ACCENT, comme dans
                -- `contraindicated_for` : les deux colonnes se comparent, et
                -- une collation qui traiterait « e » et « é » différemment
                -- selon l'environnement rendrait le filtrage dépendant de la
                -- configuration du serveur.
                alter table public.user_constraints
                  add constraint ck_user_constraints_region
                  check (region in ('cervicale', 'lombaire', 'epaule', 'genou'));

                -- ACTIVÉE **ET** FORCÉE. Sans `force`, le propriétaire de la
                -- table — `palier_migrations` — contournerait toutes les
                -- politiques. Deux épreuves du dépôt interrogent les deux
                -- colonnes sur TOUTE table de `public`.
                alter table public.user_constraints enable row level security;
                alter table public.user_constraints force row level security;

                -- POSSÉDÉE DIRECTE : la forme la plus simple du schéma, et
                -- celle où une erreur se verrait le moins.
                --
                -- `app.utilisateur()` et non `app.utilisateur_ou_null()` : il
                -- n'y a AUCUNE branche publique ici. Une contrainte cervicale
                -- ou lombaire est une donnée de santé au sens de l'article 9 ;
                -- l'accesseur qui LÈVE est le bon, et le silence sur table vide
                -- est fermé par la garde applicative du pipeline.
                create policy proprietaire on public.user_constraints
                  for all to palier_app
                  using (owner_id = (select app.utilisateur()))
                  with check (owner_id = (select app.utilisateur()));

                create policy migrations_referentiel on public.user_constraints
                  for all to palier_migrations
                  using (true) with check (true);

                grant select, insert, update, delete
                   on public.user_constraints to palier_app;

                -- Le troisième rôle, sans lequel `pg_dump` rend un fichier
                -- incomplet et `SauvegardeTests` refuse.
                grant select on public.user_constraints to palier_sauvegarde;
                """
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // `drop table` emporte la contrainte, les politiques et les
            // privilèges : ils n'existent que pour cette table.
            migrationBuilder.DropTable(
                name: "user_constraints");
        }
    }
}
