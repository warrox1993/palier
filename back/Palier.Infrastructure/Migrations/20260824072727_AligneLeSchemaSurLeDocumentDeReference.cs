using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Palier.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AligneLeSchemaSurLeDocumentDeReference : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "noted_at",
                table: "exercise_feedback");

            migrationBuilder.RenameColumn(
                name: "feeling",
                table: "exercise_feedback",
                newName: "rating");

            migrationBuilder.AddColumn<string>(
                name: "note",
                table: "user_constraints",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "severity",
                table: "user_constraints",
                type: "text",
                nullable: false,
                defaultValue: "modere");

            // ================================================================
            // Ce qu'EF Core ne pilote pas, et qui s'écrit donc à la main — D14.
            // ================================================================
            migrationBuilder.Sql(
                """
                -- Le renommage de la colonne a suivi la contrainte, PAS son
                -- nom : PostgreSQL met à jour l'expression d'un `CHECK` quand
                -- la colonne change de nom, mais laisse la contrainte
                -- s'appeler `ck_exercise_feedback_feeling`. L'épreuve de schéma
                -- cherche un nom, et un nom qui ne dit plus ce qu'il contraint
                -- est un piège pour la prochaine lecture.
                alter table public.exercise_feedback
                  rename constraint ck_exercise_feedback_feeling
                                 to ck_exercise_feedback_rating;

                -- Les TROIS sévérités de `docs/03-donnees.md`. La validation
                -- applicative refuse déjà, mais elle ne protège pas d'une
                -- écriture faite hors de l'API — et une quatrième valeur en
                -- base ne produirait aucune erreur : l'écran ne saurait pas
                -- quoi en faire et retomberait sur un comportement par défaut,
                -- silencieusement.
                alter table public.user_constraints
                  add constraint ck_user_constraints_severity
                  check (severity in ('leger', 'modere', 'strict'));
                """
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Défaire le SQL écrit à la main AVANT que les colonnes bougent :
            // une contrainte porte sur une colonne, et la retirer après sa
            // colonne échouerait.
            migrationBuilder.Sql(
                """
                alter table public.user_constraints
                  drop constraint ck_user_constraints_severity;

                alter table public.exercise_feedback
                  rename constraint ck_exercise_feedback_rating
                                 to ck_exercise_feedback_feeling;
                """
            );

            migrationBuilder.DropColumn(
                name: "note",
                table: "user_constraints");

            migrationBuilder.DropColumn(
                name: "severity",
                table: "user_constraints");

            migrationBuilder.RenameColumn(
                name: "rating",
                table: "exercise_feedback",
                newName: "feeling");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "noted_at",
                table: "exercise_feedback",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()");
        }
    }
}
