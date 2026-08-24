using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Palier.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RelieLaSeanceAuProgramme : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "program_day_id",
                table: "workouts",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_workouts_program_day_id",
                table: "workouts",
                column: "program_day_id");

            migrationBuilder.AddForeignKey(
                name: "FK_workouts_program_days_program_day_id",
                table: "workouts",
                column: "program_day_id",
                principalTable: "program_days",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_workouts_program_days_program_day_id",
                table: "workouts");

            migrationBuilder.DropIndex(
                name: "ix_workouts_program_day_id",
                table: "workouts");

            migrationBuilder.DropColumn(
                name: "program_day_id",
                table: "workouts");
        }
    }
}
