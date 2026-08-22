using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Palier.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SceauDuSuccesseur : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Aucun SQL à la main ici, et c'est un choix vérifié, pas un oubli.
            // `SessionsEtRoleAuth` a posé RLS, ses deux politiques et les
            // privilèges AU NIVEAU DE LA TABLE : un `grant select, insert,
            // update, delete on public.sessions_refresh` couvre les colonnes
            // ajoutées ensuite, les politiques portent sur des LIGNES, et le
            // `grant select ... to palier_sauvegarde` suit la table lui aussi.
            // Ce sont les épreuves qui le démontrent, pas cette phrase :
            // `MagasinDeSessionsTests` écrit et relit cette colonne sous
            // `palier_auth`, et `SauvegardeTests` redump la table.
            migrationBuilder.AddColumn<byte[]>(
                name: "successor_sealed",
                table: "sessions_refresh",
                type: "bytea",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "successor_sealed",
                table: "sessions_refresh");
        }
    }
}
