using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Palier.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EnrichitLeCatalogue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "name",
                table: "exercises",
                newName: "name_fr");

            migrationBuilder.AddColumn<string>(
                name: "common_errors_en",
                table: "exercises",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "common_errors_fr",
                table: "exercises",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "instructions_en",
                table: "exercises",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "instructions_fr",
                table: "exercises",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "movement_role",
                table: "exercises",
                type: "text",
                nullable: false,
                defaultValue: "aucun");

            migrationBuilder.AddColumn<string>(
                name: "name_en",
                table: "exercises",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "slug",
                table: "exercises",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "exercise_variants",
                columns: table => new
                {
                    exercise_id = table.Column<Guid>(type: "uuid", nullable: false),
                    variant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_exercise_variants", x => new { x.exercise_id, x.variant_id });
                    table.ForeignKey(
                        name: "FK_exercise_variants_exercises_exercise_id",
                        column: x => x.exercise_id,
                        principalTable: "exercises",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_exercise_variants_exercises_variant_id",
                        column: x => x.variant_id,
                        principalTable: "exercises",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ux_exercises_slug",
                table: "exercises",
                column: "slug",
                unique: true,
                filter: "is_custom = false");

            migrationBuilder.CreateIndex(
                name: "IX_exercise_variants_variant_id",
                table: "exercise_variants",
                column: "variant_id");

            // ================================================================
            // Ce qu'EF Core ne pilote pas, et qui s'écrit donc à la main — D14.
            // ================================================================
            migrationBuilder.Sql(
                """
                -- LE RÔLE DU MOUVEMENT, en liste fermée — D69, qui ferme le
                -- report de D62. `aucun` n'est pas une valeur de repli par
                -- paresse : `docs/05-entrainement.md` § 4 exclut explicitement
                -- le deltoïde latéral du ratio tirage/poussée, « il ne tire ni
                -- ne pousse ».
                alter table public.exercises
                  add constraint ck_exercises_movement_role
                  check (movement_role in ('tirage', 'poussee', 'aucun'));

                -- LE GARDE-FOU QUI COMPTE — D70.
                --
                -- Les champs du catalogue sont NULLABLES, parce qu'un exercice
                -- personnalisé n'a ni traduction, ni consignes, ni slug :
                -- l'utilisateur donne un nom, point. Mais un exercice du
                -- catalogue PUBLIC sans consignes serait affiché à tout le
                -- monde, sans rien pour l'exécuter correctement — sur un
                -- produit dont la promesse est d'éviter les blessures.
                --
                -- La contrainte les exige donc là où ils comptent, et nulle
                -- part ailleurs. Une épreuve provoque le refus.
                alter table public.exercises
                  add constraint ck_exercises_catalogue_complet
                  check (
                    is_custom
                    or (slug             is not null
                    and name_en          is not null
                    and instructions_fr  is not null
                    and instructions_en  is not null
                    and common_errors_fr is not null
                    and common_errors_en is not null)
                  );

                -- UN EXERCICE N'EST PAS SA PROPRE VARIANTE. Sans cette borne,
                -- une ligne (x, x) passerait, et la proposition de
                -- remplacement du § 6 offrirait l'exercice qu'on cherche
                -- justement à remplacer.
                alter table public.exercise_variants
                  add constraint ck_exercise_variants_pas_soi_meme
                  check (exercise_id <> variant_id);

                -- ACTIVÉE **ET** FORCÉE, comme toute table de `public`.
                alter table public.exercise_variants enable row level security;
                alter table public.exercise_variants force row level security;

                -- FORME « RÉFÉRENCE PUBLIQUE », comme `nutrient_refs` : lecture
                -- pour tous, écriture pour personne. Les variantes ne lient que
                -- des exercices du catalogue ; un utilisateur n'en déclare pas.
                --
                -- L'ABSENCE de politique d'écriture n'est pas un oubli : « If no
                -- policy exists for the table, a default-deny policy is used ».
                -- Le privilège manquant refuse déjà, et l'absence de politique
                -- refuse une seconde fois, sur un chemin différent.
                create policy lecture_publique on public.exercise_variants
                  for select to palier_app
                  using (true);

                create policy migrations_referentiel on public.exercise_variants
                  for all to palier_migrations
                  using (true) with check (true);

                -- `select` SEUL pour `palier_app` : le privilège dit la même
                -- chose que la politique, et les deux doivent tomber pour
                -- qu'une écriture passe.
                grant select on public.exercise_variants to palier_app;
                grant select on public.exercise_variants to palier_sauvegarde;
                """
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Les contraintes AVANT les colonnes qu'elles portent : une
            // contrainte retirée après sa colonne échouerait.
            migrationBuilder.Sql(
                """
                alter table public.exercises
                  drop constraint if exists ck_exercises_catalogue_complet;
                alter table public.exercises
                  drop constraint if exists ck_exercises_movement_role;
                """
            );

            migrationBuilder.DropTable(
                name: "exercise_variants");

            migrationBuilder.DropIndex(
                name: "ux_exercises_slug",
                table: "exercises");

            migrationBuilder.DropColumn(
                name: "common_errors_en",
                table: "exercises");

            migrationBuilder.DropColumn(
                name: "common_errors_fr",
                table: "exercises");

            migrationBuilder.DropColumn(
                name: "instructions_en",
                table: "exercises");

            migrationBuilder.DropColumn(
                name: "instructions_fr",
                table: "exercises");

            migrationBuilder.DropColumn(
                name: "movement_role",
                table: "exercises");

            migrationBuilder.DropColumn(
                name: "name_en",
                table: "exercises");

            migrationBuilder.DropColumn(
                name: "slug",
                table: "exercises");

            migrationBuilder.RenameColumn(
                name: "name_fr",
                table: "exercises",
                newName: "name");
        }
    }
}
