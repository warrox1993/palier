using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Palier.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AjouteLeRessenti : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "exercise_feedback",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    workout_id = table.Column<Guid>(type: "uuid", nullable: false),
                    exercise_id = table.Column<Guid>(type: "uuid", nullable: false),
                    feeling = table.Column<string>(type: "text", nullable: false),
                    noted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_exercise_feedback", x => x.id);
                    table.ForeignKey(
                        name: "FK_exercise_feedback_exercises_exercise_id",
                        column: x => x.exercise_id,
                        principalTable: "exercises",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_exercise_feedback_workouts_workout_id",
                        column: x => x.workout_id,
                        principalTable: "workouts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_exercise_feedback_exercise_id",
                table: "exercise_feedback",
                column: "exercise_id");

            migrationBuilder.CreateIndex(
                name: "ux_exercise_feedback_workout_exercise",
                table: "exercise_feedback",
                columns: new[] { "workout_id", "exercise_id" },
                unique: true);

            // ================================================================
            // Ce qu'EF Core ne pilote pas, et qui s'écrit donc à la main — D14.
            // ================================================================
            migrationBuilder.Sql(
                """
                -- La borne des trois états, `docs/05-entrainement.md` § 5. La
                -- validation applicative refuse déjà, mais elle ne protège pas
                -- d'une écriture faite hors de l'API — une commande
                -- d'exploitation, une reprise de données, un script. Et les
                -- seuils du § 5 comptent des occurrences : une quatrième valeur
                -- en base les ferait compter faux, sans erreur.
                alter table public.exercise_feedback
                  add constraint ck_exercise_feedback_feeling
                  check (feeling in ('good', 'meh', 'pain'));

                -- ACTIVÉE **ET** FORCÉE. `relrowsecurity` seule laisse le
                -- PROPRIÉTAIRE de la table contourner toutes les politiques —
                -- et le propriétaire, ici, est `palier_migrations`, qui applique
                -- le seed. Deux épreuves du dépôt interrogent les deux colonnes
                -- sur TOUTE table de `public` ; une table qui n'aurait que la
                -- première les ferait rougir.
                alter table public.exercise_feedback enable row level security;
                alter table public.exercise_feedback force row level security;

                -- POSSÉDÉE PAR JOINTURE : aucune colonne `owner_id`. La
                -- politique remonte jusqu'à `workouts`, comme celle de `sets`.
                --
                -- `using` ET `with check`, les DEUX. Sans `with check`, un
                -- utilisateur insérerait une ligne rattachée à la séance d'un
                -- autre : il ne pourrait pas la relire, mais elle serait là, et
                -- elle compterait dans les seuils de sa victime — deux `pain`
                -- consécutifs déclencheraient chez elle une orientation vers un
                -- professionnel qu'elle n'a jamais motivée.
                create policy proprietaire on public.exercise_feedback
                  for all to palier_app
                  using (exists (select 1
                                   from public.workouts w
                                  where w.id = exercise_feedback.workout_id
                                    and w.owner_id = (select app.utilisateur())))
                  with check (exists (select 1
                                        from public.workouts w
                                       where w.id = exercise_feedback.workout_id
                                         and w.owner_id = (select app.utilisateur())));

                -- Le rôle des migrations applique le seed et les reprises. Sans
                -- cette politique, `force row level security` le soumettrait à
                -- celle ci-dessus — qui exige une identité qu'une migration n'a
                -- pas — et l'écriture échouerait en 42501. Mesuré au lot 4b sur
                -- `cles_de_donnees`.
                create policy migrations_referentiel on public.exercise_feedback
                  for all to palier_migrations
                  using (true) with check (true);

                grant select, insert, update, delete
                   on public.exercise_feedback to palier_app;

                -- LE TROISIÈME RÔLE N'EST PAS FACULTATIF. `grant select on ALL
                -- TABLES` posé au lot 2 ne couvre PAS les tables nées après
                -- lui : `SauvegardeTests` interroge le catalogue et refuse un
                -- `pg_dump` incomplet. Trois épreuves l'ont payé au lot 4b.
                grant select on public.exercise_feedback to palier_sauvegarde;
                """
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // `drop table` emporte la contrainte, les politiques et les
            // privilèges avec elle : ils n'existent que pour cette table. Rien
            // à défaire à la main.
            migrationBuilder.DropTable(
                name: "exercise_feedback");
        }
    }
}
