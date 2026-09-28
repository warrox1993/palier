using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Palier.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CoffreDesSecrets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "cles_de_donnees",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    enveloppe = table.Column<string>(type: "text", nullable: false),
                    creee_le = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cles_de_donnees", x => x.id);
                });

            // D14 : les politiques et les privilèges passent par `Sql`, EF ne
            // les modélise pas. D37 : toute table de `public` porte RLS activée
            // ET forcée, sans quoi `LecteurDeSocle` refuse le démarrage.
            migrationBuilder.Sql(
                """
                alter table public.cles_de_donnees enable row level security;
                alter table public.cles_de_donnees force row level security;

                -- Forme « référence publique », celle de `nutrient_refs`.
                -- `using (true)` ne concède rien : une enveloppe est un JWE
                -- que seul le coffre sait déballer. C'est précisément ce qui
                -- donne au design ses DEUX compromissions indépendantes — la
                -- base seule ne suffit pas, le compte de service seul non plus.
                create policy lecture_publique on public.cles_de_donnees
                  for select to palier_app
                  using (true);

                -- AUCUNE politique d'écriture POUR palier_app, et c'est la
                -- décision : « If no policy exists for the table, a
                -- default-deny policy is used » (ddl-rowsecurity.html). Le
                -- privilège manquant refuse déjà ; l'absence de politique
                -- refuse une seconde fois, sur un chemin différent.
                grant select on public.cles_de_donnees to palier_app;

                -- Le rôle propriétaire, lui, doit pouvoir poser une clé : c'est
                -- la commande d'exploitation qui le fait. Sous FORCE,
                -- `palier_migrations` est LUI AUSSI soumis aux politiques —
                -- mesuré : sans cette ligne, son INSERT échoue en 42501, « new
                -- row violates row-level security policy ».
                --
                -- Les deux corrections réflexes — BYPASSRLS sur le rôle, ou
                -- retirer FORCE — sont exactement les deux façons d'éteindre
                -- RLS sans que rien ne le signale. Le chemin passe par une
                -- politique explicite et nominative, comme les six tables du
                -- socle initial. Elle n'ouvre rien à `palier_app` : une
                -- politique permissive ne vaut que pour les rôles qu'elle nomme.
                create policy migrations_referentiel on public.cles_de_donnees
                  for all to palier_migrations
                  using (true) with check (true);

                -- Et la sauvegarde. Le socle initial avait accordé `select on
                -- ALL TABLES in schema public`, mais cette forme ne vaut que
                -- pour les tables existantes AU MOMENT du grant : toute table
                -- née après lui échappe. Trois épreuves de `SauvegardeTests`
                -- l'ont refusé — un `pg_dump` incomplet rend un fichier qui
                -- restaure sans erreur en ayant perdu une table.
                grant select on public.cles_de_donnees to palier_sauvegarde;
                """
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cles_de_donnees");
        }
    }
}
