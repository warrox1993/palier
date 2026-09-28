using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Palier.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SessionsEtRoleAuth : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sessions_refresh",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<byte[]>(type: "bytea", nullable: false),
                    family_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    consumed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    replaced_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    device = table.Column<string>(type: "text", nullable: true),
                    last_seen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sessions_refresh", x => x.id);
                    table.ForeignKey(
                        name: "FK_sessions_refresh_AspNetUsers_owner_id",
                        column: x => x.owner_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_sessions_refresh_family_id",
                table: "sessions_refresh",
                column: "family_id");

            migrationBuilder.CreateIndex(
                name: "IX_sessions_refresh_owner_id",
                table: "sessions_refresh",
                column: "owner_id");

            migrationBuilder.CreateIndex(
                name: "IX_sessions_refresh_token_hash",
                table: "sessions_refresh",
                column: "token_hash",
                unique: true);

            // ---- RLS, politiques et privilèges — D14, D37, D38 --------------
            //
            // EF ne génère rien de tout ceci : il crée la table, pas la barrière.
            // L'assertion de démarrage refuse de servir si une table de `public`
            // n'a pas RLS activée ET forcée, donc les deux lignes sont
            // obligatoires — `enable` seul laisse le propriétaire contourner ses
            // propres politiques ; sans `enable`, il n'y a aucune politique du
            // tout.
            migrationBuilder.Sql(
                """
                alter table public.sessions_refresh enable row level security;
                alter table public.sessions_refresh force row level security;
                """
            );

            // La politique désigne le RÔLE, jamais une variable de session.
            //
            // Le drapeau de contexte — set_config('app.contexte','auth') — a été
            // écarté pour cette raison précise : la barrière dépendrait alors
            // d'une valeur que le code applicatif contrôle, et un FromSqlRaw
            // distrait la ferait tomber sans que rien le signale. Ici c'est le
            // moteur qui décide, sur une identité de connexion.
            //
            // `palier_app` n'est nommé nulle part : une politique permissive ne
            // s'applique qu'aux rôles qu'elle désigne, et il n'a par ailleurs
            // aucun privilège sur cette table.
            migrationBuilder.Sql(
                """
                create policy authentification on public.sessions_refresh
                  for all to palier_auth
                  using (true) with check (true);

                grant select, insert, update, delete
                  on public.sessions_refresh to palier_auth;
                """
            );

            // Le propriétaire écrit ses migrations : sous FORCE, `palier_migrations`
            // est lui aussi soumis aux politiques, y compris pour les DDL qui
            // touchent des lignes.
            migrationBuilder.Sql(
                """
                create policy migrations_sessions on public.sessions_refresh
                  for all to palier_migrations
                  using (true) with check (true);
                """
            );

            // ---- L'ouverture des tables d'identité — ce que D38 attendait ----
            //
            // « Le lot 4 doit concevoir ce chemin explicitement : rôle dédié avec
            // sa politique, ou fonction SECURITY DEFINER au périmètre minimal,
            // jamais une pose de l'identité d'autrui. » C'est le rôle dédié.
            //
            // Les fonctions SECURITY DEFINER ont été écartées pour deux motifs
            // mesurés : ASP.NET Identity interroge le DbContext en LINQ à travers
            // UserStore, donc il faudrait réimplémenter cinq interfaces de magasin
            // sur le chemin le plus sensible du produit ; et chaque fonction
            // devrait porter SET search_path sous peine de rouvrir CVE-2018-1058.
            foreach (
                var table in new[]
                {
                    "AspNetUsers",
                    "AspNetUserTokens",
                    "AspNetUserLogins",
                    "AspNetUserClaims",
                    "AspNetUserRoles",
                    "AspNetRoles",
                }
            )
            {
                migrationBuilder.Sql(
                    $"""
                    create policy authentification on public."{table}"
                      for all to palier_auth
                      using (true) with check (true);

                    grant select, insert, update, delete
                      on public."{table}" to palier_auth;
                    """
                );
            }

            // La séquence de `AspNetUserClaims` et `AspNetRoleClaims` : Identity y
            // écrit des identifiants entiers. Sans USAGE, l'INSERT échoue sur un
            // 42501 qui ne nomme pas la séquence.
            migrationBuilder.Sql(
                """
                grant usage, select on all sequences in schema public to palier_auth;
                """
            );

            // ---- La sauvegarde doit voir la nouvelle table -------------------
            //
            // `grant select on all tables` ne vaut que pour les tables existantes
            // au moment où il est exécuté : une table créée après ne l'hérite pas.
            // Sans cette ligne, `pg_dump` échoue sur « permission denied for table
            // sessions_refresh » — et la tâche 10 du lot 2 a mesuré ce que cela
            // donne : un fichier tronqué de 40 829 octets là où le dump complet en
            // fait 41 287. Un dump raté ressemble à un dump.
            //
            // Trois épreuves de `SauvegardeTests` ont rougi sur cet oubli avant
            // que cette ligne existe.
            migrationBuilder.Sql(
                """
                grant select on public.sessions_refresh to palier_sauvegarde;
                """
            );

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sessions_refresh");
        }
    }
}
