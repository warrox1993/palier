using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Palier.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ConsentementSante : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ConsentementSanteLe",
                table: "AspNetUsers",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConsentementSanteLe",
                table: "AspNetUsers");
        }
    }
}
