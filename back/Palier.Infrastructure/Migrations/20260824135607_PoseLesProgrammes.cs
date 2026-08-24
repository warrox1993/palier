using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Palier.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PoseLesProgrammes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "search_key",
                table: "exercises",
                type: "text",
                nullable: true,
                computedColumnSql: "translate(upper(coalesce(name_fr, '') || ' ' || coalesce(name_en, '')), 'ÁÀÂÄÃÅÉÈÊËÍÌÎÏÓÒÔÖÕÚÙÛÜŸÇÑ', 'AAAAAAEEEEIIIIOOOOOUUUUYCN')",
                stored: true);

            migrationBuilder.CreateTable(
                name: "programs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    slug = table.Column<string>(type: "text", nullable: true),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_template = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    name_fr = table.Column<string>(type: "text", nullable: false),
                    name_en = table.Column<string>(type: "text", nullable: true),
                    description_fr = table.Column<string>(type: "text", nullable: true),
                    description_en = table.Column<string>(type: "text", nullable: true),
                    notes_fr = table.Column<string>(type: "text", nullable: true),
                    notes_en = table.Column<string>(type: "text", nullable: true),
                    frequency_min = table.Column<int>(type: "integer", nullable: true),
                    frequency_max = table.Column<int>(type: "integer", nullable: true),
                    targets_constraint = table.Column<string>(type: "text", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_programs", x => x.id);
                    table.CheckConstraint("ck_programs_frequence", "frequency_min is null or (frequency_min between 1 and 7 and frequency_max between frequency_min and 7)");
                    table.CheckConstraint("ck_programs_modele_complet", "not is_template or (slug is not null and name_en is not null and description_fr is not null and description_en is not null and notes_fr is not null and notes_en is not null and frequency_min is not null and frequency_max is not null)");
                    table.CheckConstraint("ck_programs_proprietaire", "(is_template and owner_id is null) or (not is_template and owner_id is not null)");
                    table.CheckConstraint("ck_programs_targets_constraint", "targets_constraint is null or targets_constraint in ('cervicale', 'lombaire', 'epaule', 'genou', 'hanche', 'poignet', 'cheville')");
                    table.ForeignKey(
                        name: "FK_programs_AspNetUsers_owner_id",
                        column: x => x.owner_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "program_days",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    program_id = table.Column<Guid>(type: "uuid", nullable: false),
                    label_fr = table.Column<string>(type: "text", nullable: false),
                    label_en = table.Column<string>(type: "text", nullable: true),
                    position = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_program_days", x => x.id);
                    table.CheckConstraint("ck_program_days_position", "position >= 1");
                    table.ForeignKey(
                        name: "FK_program_days_programs_program_id",
                        column: x => x.program_id,
                        principalTable: "programs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "program_exercises",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    program_day_id = table.Column<Guid>(type: "uuid", nullable: false),
                    exercise_id = table.Column<Guid>(type: "uuid", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    target_sets = table.Column<int>(type: "integer", nullable: false),
                    target_reps_min = table.Column<int>(type: "integer", nullable: false),
                    target_reps_max = table.Column<int>(type: "integer", nullable: true),
                    target_rir = table.Column<int>(type: "integer", nullable: false),
                    rest_seconds = table.Column<int>(type: "integer", nullable: false),
                    note = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_program_exercises", x => x.id);
                    table.CheckConstraint("ck_program_exercises_position", "position >= 1");
                    table.CheckConstraint("ck_program_exercises_repetitions", "target_reps_min between 1 and 100 and (target_reps_max is null or target_reps_max between target_reps_min and 100)");
                    table.CheckConstraint("ck_program_exercises_repos", "rest_seconds between 0 and 900");
                    table.CheckConstraint("ck_program_exercises_rir", "target_rir between 0 and 10");
                    table.CheckConstraint("ck_program_exercises_series", "target_sets between 1 and 20");
                    table.ForeignKey(
                        name: "FK_program_exercises_exercises_exercise_id",
                        column: x => x.exercise_id,
                        principalTable: "exercises",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_program_exercises_program_days_program_day_id",
                        column: x => x.program_day_id,
                        principalTable: "program_days",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ux_program_days_program_id_position",
                table: "program_days",
                columns: new[] { "program_id", "position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_program_exercises_exercise_id",
                table: "program_exercises",
                column: "exercise_id");

            migrationBuilder.CreateIndex(
                name: "ux_program_exercises_day_id_position",
                table: "program_exercises",
                columns: new[] { "program_day_id", "position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_programs_owner_id",
                table: "programs",
                column: "owner_id");

            migrationBuilder.CreateIndex(
                name: "ux_programs_slug",
                table: "programs",
                column: "slug",
                unique: true,
                filter: "is_template = true");

            // ================================================================
            // RLS — le reste ne s'engendre pas, et D14 veut qu'il passe par là.
            // ================================================================
            migrationBuilder.Sql(
                """
                -- ACTIVÉE **ET** FORCÉE sur les trois tables. Sans `force`, le
                -- propriétaire — `palier_migrations` — contournerait toutes les
                -- politiques, et c'est lui qui applique le référentiel.
                alter table public.programs           enable row level security;
                alter table public.programs           force  row level security;
                alter table public.program_days       enable row level security;
                alter table public.program_days       force  row level security;
                alter table public.program_exercises  enable row level security;
                alter table public.program_exercises  force  row level security;

                -- Forme « catalogue mixte » — D72, transposée d'`exercises`.
                --
                -- DEUX POLITIQUES PERMISSIVES SÉPARÉES, jamais un `OR` dans une
                -- seule : aucun ordre d'évaluation n'est garanti entre les
                -- branches d'un `OR`, et se fier au court-circuit serait une
                -- supposition déguisée en protection. Séparées, la branche des
                -- modèles ne peut PAS déclencher l'accesseur qui lève.
                create policy modeles_publics on public.programs
                  for select to palier_app
                  using (is_template = true);

                create policy proprietaire on public.programs
                  for all to palier_app
                  using (owner_id = (select app.utilisateur_ou_null()))
                  with check (owner_id = (select app.utilisateur_ou_null()));

                -- Forme « possédée INDIRECTE » : ces deux tables ne portent pas
                -- de `owner_id`, et c'est délibéré — le dupliquer créerait deux
                -- vérités sur la même appartenance, dont l'une finirait fausse.
                -- L'appartenance se lit donc en remontant au programme.
                --
                -- Là encore les deux populations sont SÉPARÉES : la lecture
                -- d'un jour de modèle n'a pas à réclamer une identité.
                create policy modeles_publics on public.program_days
                  for select to palier_app
                  using (exists (select 1 from public.programs p
                                  where p.id = program_days.program_id
                                    and p.is_template = true));

                create policy proprietaire on public.program_days
                  for all to palier_app
                  using (exists (select 1 from public.programs p
                                  where p.id = program_days.program_id
                                    and p.owner_id = (select app.utilisateur_ou_null())))
                  with check (exists (select 1 from public.programs p
                                       where p.id = program_days.program_id
                                         and p.owner_id = (select app.utilisateur_ou_null())));

                -- Deux niveaux de remontée. La jointure est le prix de
                -- l'absence de duplication, et l'index `ux_program_days_*` la
                -- sert.
                create policy modeles_publics on public.program_exercises
                  for select to palier_app
                  using (exists (select 1 from public.program_days d
                                  join public.programs p on p.id = d.program_id
                                  where d.id = program_exercises.program_day_id
                                    and p.is_template = true));

                create policy proprietaire on public.program_exercises
                  for all to palier_app
                  using (exists (select 1 from public.program_days d
                                  join public.programs p on p.id = d.program_id
                                  where d.id = program_exercises.program_day_id
                                    and p.owner_id = (select app.utilisateur_ou_null())))
                  with check (exists (select 1 from public.program_days d
                                       join public.programs p on p.id = d.program_id
                                       where d.id = program_exercises.program_day_id
                                         and p.owner_id = (select app.utilisateur_ou_null())));

                -- Le rôle qui applique `04-programs.sql`. Sans lui, le
                -- référentiel devrait passer par `BYPASSRLS` ou par un `NO
                -- FORCE` temporaire — les deux façons d'éteindre RLS sans que
                -- rien ne le signale.
                create policy migrations_referentiel on public.programs
                  for all to palier_migrations using (true) with check (true);
                create policy migrations_referentiel on public.program_days
                  for all to palier_migrations using (true) with check (true);
                create policy migrations_referentiel on public.program_exercises
                  for all to palier_migrations using (true) with check (true);

                grant select, insert, update, delete on public.programs          to palier_app;
                grant select, insert, update, delete on public.program_days      to palier_app;
                grant select, insert, update, delete on public.program_exercises to palier_app;

                -- Le troisième rôle, sans lequel `pg_dump` rend un fichier
                -- incomplet et `SauvegardeTests` refuse.
                grant select on public.programs          to palier_sauvegarde;
                grant select on public.program_days      to palier_sauvegarde;
                grant select on public.program_exercises to palier_sauvegarde;
                """
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "program_exercises");

            migrationBuilder.DropTable(
                name: "program_days");

            migrationBuilder.DropTable(
                name: "programs");

            migrationBuilder.DropColumn(
                name: "search_key",
                table: "exercises");
        }
    }
}
