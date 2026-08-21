using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Palier.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class VerrouillageProgressif : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DernierEchecLe",
                table: "AspNetUsers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "VerrouillagesSubis",
                table: "AspNetUsers",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DernierEchecLe",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "VerrouillagesSubis",
                table: "AspNetUsers");
        }
    }
}
