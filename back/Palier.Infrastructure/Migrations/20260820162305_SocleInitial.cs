using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Palier.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SocleInitial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: true),
                    SecurityStamp = table.Column<string>(type: "text", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true),
                    PhoneNumber = table.Column<string>(type: "text", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "nutrient_refs",
                columns: table => new
                {
                    nutrient = table.Column<string>(type: "text", nullable: false),
                    label_fr = table.Column<string>(type: "text", nullable: false),
                    label_en = table.Column<string>(type: "text", nullable: false),
                    unit = table.Column<string>(type: "text", nullable: false),
                    ai_male = table.Column<decimal>(type: "numeric", nullable: true),
                    ai_female = table.Column<decimal>(type: "numeric", nullable: true),
                    ul = table.Column<decimal>(type: "numeric", nullable: true),
                    per_kg = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    source = table.Column<string>(type: "text", nullable: false),
                    source_year = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_nutrient_refs", x => x.nutrient);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: true),
                    ClaimValue = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: true),
                    ClaimValue = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "text", nullable: false),
                    ProviderKey = table.Column<string>(type: "text", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "text", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    LoginProvider = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "body_weight",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    measured_on = table.Column<DateOnly>(type: "date", nullable: false),
                    weight_kg = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_body_weight", x => x.id);
                    table.ForeignKey(
                        name: "FK_body_weight_AspNetUsers_owner_id",
                        column: x => x.owner_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "exercises",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    name = table.Column<string>(type: "text", nullable: false),
                    equipment = table.Column<string>(type: "text", nullable: true),
                    primary_muscles = table.Column<string[]>(type: "text[]", nullable: false),
                    secondary_muscles = table.Column<string[]>(type: "text[]", nullable: false, defaultValueSql: "'{}'"),
                    is_unilateral = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    default_increment = table.Column<decimal>(type: "numeric", nullable: false, defaultValue: 2.5m),
                    contraindicated_for = table.Column<string[]>(type: "text[]", nullable: false, defaultValueSql: "'{}'"),
                    is_custom = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_exercises", x => x.id);
                    table.ForeignKey(
                        name: "FK_exercises_AspNetUsers_owner_id",
                        column: x => x.owner_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "workouts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ended_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    sleep_hours = table.Column<decimal>(type: "numeric", nullable: true),
                    energy_1_5 = table.Column<int>(type: "integer", nullable: true),
                    note = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workouts", x => x.id);
                    table.CheckConstraint("ck_workouts_energy_1_5", "energy_1_5 between 1 and 5");
                    table.ForeignKey(
                        name: "FK_workouts_AspNetUsers_owner_id",
                        column: x => x.owner_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    workout_id = table.Column<Guid>(type: "uuid", nullable: false),
                    exercise_id = table.Column<Guid>(type: "uuid", nullable: false),
                    set_index = table.Column<int>(type: "integer", nullable: false),
                    weight_kg = table.Column<decimal>(type: "numeric", nullable: true),
                    reps = table.Column<int>(type: "integer", nullable: true),
                    rir = table.Column<int>(type: "integer", nullable: true),
                    is_warmup = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    logged_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sets", x => x.id);
                    table.ForeignKey(
                        name: "FK_sets_exercises_exercise_id",
                        column: x => x.exercise_id,
                        principalTable: "exercises",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sets_workouts_workout_id",
                        column: x => x.workout_id,
                        principalTable: "workouts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AspNetRoleClaims_RoleId",
                table: "AspNetRoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "AspNetRoles",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserClaims_UserId",
                table: "AspNetUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                table: "AspNetUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_RoleId",
                table: "AspNetUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "AspNetUsers",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "AspNetUsers",
                column: "NormalizedUserName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_body_weight_owner_id_measured_on",
                table: "body_weight",
                columns: new[] { "owner_id", "measured_on" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_exercises_owner_id",
                table: "exercises",
                column: "owner_id");

            migrationBuilder.CreateIndex(
                name: "IX_sets_exercise_id",
                table: "sets",
                column: "exercise_id");

            migrationBuilder.CreateIndex(
                name: "ix_sets_workout_id",
                table: "sets",
                column: "workout_id");

            migrationBuilder.CreateIndex(
                name: "ix_workouts_owner_id_started_at",
                table: "workouts",
                columns: new[] { "owner_id", "started_at" },
                descending: new[] { false, true });

            // ================================================================
            // Ce qu'EF Core ne pilote pas, et qui s'écrit donc à la main — D14.
            // ================================================================

            // ---- La vue `weekly_volume` -------------------------------------
            //
            // Séries DURES (hors échauffement) par muscle et par semaine, en
            // dépliant `primary_muscles` au coefficient 1 et `secondary_muscles`
            // au coefficient 0,5 — `docs/03-donnees.md` § Vues utiles.
            //
            // `security_invoker = true` n'est pas décoratif. Sans lui, une vue
            // s'exécute avec les droits de son PROPRIÉTAIRE — ici
            // `palier_migrations`, qui possède toutes les tables — et ce sont
            // SES politiques qui s'appliquent, pas celles de l'appelant. La vue
            // deviendrait le seul chemin du schéma où l'isolation change de
            // règle sans que personne le voie. Disponible depuis PostgreSQL 15.
            migrationBuilder.Sql(
                """
                create view public.weekly_volume with (security_invoker = true) as
                select w.owner_id                                as owner_id,
                       date_trunc('week', s.logged_at)           as week,
                       m.muscle                                  as muscle,
                       sum(m.coefficient)                        as hard_sets
                  from public.sets s
                  join public.workouts w  on w.id = s.workout_id
                  join public.exercises e on e.id = s.exercise_id
                  cross join lateral (
                        select unnest(e.primary_muscles)   as muscle, 1.0::numeric as coefficient
                         union all
                        select unnest(e.secondary_muscles) as muscle, 0.5::numeric as coefficient
                  ) m
                 where s.is_warmup = false
                 group by w.owner_id, date_trunc('week', s.logged_at), m.muscle;
                """
            );

            // ---- Le schéma `app` et ses DEUX accesseurs — D36 ----------------
            //
            // DEUX, et non un. Une politique de catalogue public qui appellerait
            // une fonction qui LÈVE transformerait toute lecture anonyme en
            // erreur 500. Et le contournement naïf —
            // `using (is_custom = false or owner_id = app.utilisateur())` — ne
            // marche pas : la documentation ne garantit AUCUN court-circuit,
            // elle dit que les expressions « will be evaluated for each row »,
            // et les politiques permissives multiples sont combinées par `OR`
            // sans ordre garanti. La levée peut donc partir sur une ligne
            // publique.
            //
            // `current_setting(…, true)` — le second argument est `missing_ok` :
            // « If there is no such setting, current_setting throws an error
            // UNLESS missing_ok is supplied and is true (in which case NULL is
            // returned) ». Sans lui, la fonction lèverait une erreur DIFFÉRENTE
            // de celle qu'on veut, et le message ne dirait pas ce qu'on croit.
            //
            // LE MESSAGE NE PORTE AUCUNE VALEUR. Ni identifiant, ni donnée.
            // `01-conformite.md` § 4 : « Aucune donnée de santé dans les logs
            // applicatifs ». La tentation d'y ajouter l'identifiant « pour
            // déboguer » sera forte ; elle est refusée ici une fois pour toutes.
            //
            // `stable` et non `volatile` : c'est ce qui autorise le
            // planificateur à hisser l'appel en InitPlan quand il est enveloppé
            // dans un sous-select, donc une évaluation par INSTRUCTION au lieu
            // d'une par ligne.
            migrationBuilder.Sql(
                """
                create schema app;

                create function app.utilisateur() returns uuid
                  language plpgsql stable
                as $corps$
                declare pose text;
                begin
                  pose := current_setting('app.utilisateur', true);
                  if pose is null or pose = '' then
                    raise exception 'identite absente de la transaction courante'
                      using errcode = '28000',
                            hint = 'Le cas d usage doit ouvrir une transaction et y poser '
                                   'app.utilisateur avant toute lecture.';
                  end if;
                  return pose::uuid;
                end
                $corps$;

                create function app.utilisateur_ou_null() returns uuid
                  language sql stable
                as $corps$
                  select nullif(current_setting('app.utilisateur', true), '')::uuid
                $corps$;

                grant usage on schema app to palier_app;
                """
            );

            // ---- RLS activée ET forcée sur TOUTE table de `public` -----------
            //
            // `enable` seul ne protège pas du PROPRIÉTAIRE : « Table owners
            // normally bypass row security » (ddl-rowsecurity.html). `force`
            // ajoute la seule chose que la séparation des rôles ne donne pas —
            // la protection contre un accès direct sous le rôle propriétaire :
            // outil d'administration, restauration, identifiants fuités, tâche
            // lancée à la main sur le serveur. C'est la PREMIÈRE des trois
            // familles que `docs/03-donnees.md` § RLS nomme.
            //
            // Les treize tables sont écrites NOMMÉMENT plutôt que parcourues :
            // un diff doit montrer ce qui a été protégé. Le filet contre l'oubli
            // est `SchemaTests`, qui interroge `pg_class` en forme de catalogue
            // et refuse toute table de `public` sans les DEUX colonnes.
            //
            // `__EFMigrationsHistory` en fait partie, et ce n'est pas un excès
            // de zèle : sans elle, la règle « toute table de public » porterait
            // une exception, et une exception est un endroit où l'on ne regarde
            // plus. Elle reçoit sa politique pour `palier_migrations` juste
            // après — sans quoi EF ne pourrait plus y inscrire ses migrations.
            foreach (
                var table in new[]
                {
                    "AspNetRoles",
                    "AspNetRoleClaims",
                    "AspNetUsers",
                    "AspNetUserClaims",
                    "AspNetUserLogins",
                    "AspNetUserRoles",
                    "AspNetUserTokens",
                    "__EFMigrationsHistory",
                    "nutrient_refs",
                    "exercises",
                    "workouts",
                    "sets",
                    "body_weight",
                }
            )
            {
                migrationBuilder.Sql(
                    $"""
                    alter table public."{table}" enable row level security;
                    alter table public."{table}" force row level security;
                    """
                );
            }

            // ---- La politique d'écriture du rôle propriétaire ----------------
            //
            // Sous FORCE, `palier_migrations` est lui aussi soumis aux
            // politiques : les INSERT du référentiel passent par le WITH CHECK,
            // et la ligne qu'EF inscrit dans `__EFMigrationsHistory` aussi.
            //
            // Les deux corrections réflexes — accorder BYPASSRLS au rôle de
            // migration, ou retirer FORCE le temps du chargement — sont
            // EXACTEMENT les deux façons d'éteindre RLS sans que rien ne le
            // signale. Le chemin passe donc par une politique explicite,
            // nominative, jamais par un contournement.
            //
            // Elle est réservée `to palier_migrations` : une politique
            // permissive ne s'applique qu'aux rôles qu'elle nomme, et celle-ci
            // n'ouvre donc rien à `palier_app`.
            //
            // Les tables `AspNet*` n'en reçoivent AUCUNE — D38. Elles naissent
            // en refus par défaut, pour tout le monde, y compris leur
            // propriétaire.
            foreach (
                var table in new[]
                {
                    "__EFMigrationsHistory",
                    "nutrient_refs",
                    "exercises",
                    "workouts",
                    "sets",
                    "body_weight",
                }
            )
            {
                migrationBuilder.Sql(
                    $"""
                    create policy migrations_referentiel on public."{table}"
                      for all to palier_migrations
                      using (true) with check (true);
                    """
                );
            }

            // ---- Les politiques des cinq formes — D36 ------------------------
            //
            // Chacune est réservée `to palier_app`. Une politique permissive ne
            // s'applique qu'aux rôles qu'elle nomme : un rôle futur qu'on
            // aurait oublié n'hérite donc de RIEN, et tombe sur le refus par
            // défaut. C'est le bon sens de l'erreur.
            //
            // L'ACCESSEUR EST ENVELOPPÉ DANS UN SOUS-SELECT, et ce n'est pas
            // une optimisation : c'est ce qui supprime un faux arbitrage. La
            // documentation tranche la question — l'expression d'une politique
            // est évaluée POUR CHAQUE LIGNE. `(select …)` force un InitPlan
            // évalué une fois par instruction. On garde donc le `plpgsql` qui
            // lève ET la vitesse ; on n'échange pas la conformité contre la
            // latence.
            //
            // Réserve à connaître, écrite plutôt que tue : l'InitPlan étant
            // évalué paresseusement, il AGGRAVE le trou de la table vide — zéro
            // ligne parcourue, zéro évaluation, aucune exception. La garde
            // applicative du pipeline est ce qui ferme ce trou, pas ces
            // politiques.
            migrationBuilder.Sql(
                """
                -- Forme « possédée directe ». Le WITH CHECK est identique au
                -- USING : sans lui, A pourrait INSÉRER une ligne au nom de B
                -- tout en étant incapable de la relire.
                create policy proprietaire on public.workouts
                  for all to palier_app
                  using (owner_id = (select app.utilisateur()))
                  with check (owner_id = (select app.utilisateur()));

                create policy proprietaire on public.body_weight
                  for all to palier_app
                  using (owner_id = (select app.utilisateur()))
                  with check (owner_id = (select app.utilisateur()));

                -- Forme « possédée PAR JOINTURE ». `sets` n'a pas d'owner_id :
                -- la politique doit remonter jusqu'à `workouts`, dont la
                -- politique s'applique à son tour dans la sous-requête.
                create policy proprietaire on public.sets
                  for all to palier_app
                  using (exists (select 1 from public.workouts w
                                  where w.id = sets.workout_id
                                    and w.owner_id = (select app.utilisateur())))
                  with check (exists (select 1 from public.workouts w
                                       where w.id = sets.workout_id
                                         and w.owner_id = (select app.utilisateur())));

                -- Forme « catalogue mixte ». DEUX POLITIQUES PERMISSIVES
                -- SÉPARÉES, jamais un `OR` dans une seule : la documentation ne
                -- garantit aucun ordre d'évaluation entre les branches d'un
                -- `OR`, et se fier au court-circuit serait une supposition
                -- déguisée en protection. Séparées, la branche publique ne peut
                -- PAS déclencher l'accesseur qui lève.
                create policy catalogue_public on public.exercises
                  for select to palier_app
                  using (is_custom = false);

                create policy proprietaire on public.exercises
                  for all to palier_app
                  using (owner_id = (select app.utilisateur_ou_null()))
                  with check (owner_id = (select app.utilisateur_ou_null()));

                -- Forme « référence publique ». Lecture pour tous, et AUCUNE
                -- politique d'écriture : « If no policy exists for the table, a
                -- default-deny policy is used ». Le privilège manquant refuse
                -- déjà l'écriture ; l'absence de politique la refuse une
                -- seconde fois, sur un chemin différent.
                create policy lecture_publique on public.nutrient_refs
                  for select to palier_app
                  using (true);

                """
            );

            // Les tables `AspNet*` ne reçoivent AUCUNE politique — D38, et c'est
            // la décision, pas un oubli. Elles porteront les empreintes de mots
            // de passe, les secrets TOTP, les jetons de rafraîchissement et les
            // sessions : y poser `using (true)` en ferait le SEUL endroit du
            // schéma sans barrière de ligne, c'est-à-dire l'endroit où un filtre
            // oublié coûterait le plus cher.
            //
            // Conséquence assumée : le chemin de connexion, qui lit
            // `AspNetUsers` PAR EMAIL avant que la moindre identité existe,
            // casse ici — fermé et bruyant, donc jamais en fuite. LE LOT 4 DOIT
            // LE CONCEVOIR : rôle dédié avec sa politique, ou fonction
            // `security definer` au périmètre minimal, jamais une pose de
            // l'identité d'autrui. Ne pas ouvrir cette porte pour faire passer
            // quelque chose.

            // ---- Les privilèges de `palier_app`, objet par objet -------------
            //
            // D37 : le rôle applicatif ne possède aucun objet et reçoit ses
            // privilèges un par un, dans la migration qui crée l'objet. Aucun
            // `alter default privileges` : un privilège par défaut accorderait
            // d'avance sur des tables que personne n'a encore écrites.
            //
            // `nutrient_refs` ne reçoit que SELECT — « lecture publique,
            // écriture interdite via l'API » (`docs/03-donnees.md`). Le refus
            // d'écriture est ainsi porté par le PRIVILÈGE et non par l'absence
            // de politique : une barrière de plus, et une qui ne dépend pas du
            // nombre de lignes parcourues.
            //
            // Les tables `AspNet*` ne reçoivent RIEN. Le chemin de connexion du
            // lot 4 devra être conçu, pas découvert.
            migrationBuilder.Sql(
                """
                grant select, insert, update, delete
                   on public.exercises, public.workouts, public.sets, public.body_weight
                   to palier_app;
                grant select on public.nutrient_refs to palier_app;
                grant select on public.weekly_volume to palier_app;
                """
            );

            // ---- La version du schéma, en LECTURE SEULE pour l'API -----------
            //
            // AJOUTÉ À LA TÂCHE 9, ET C'EST UN DÉFAUT DU PLAN CORRIGÉ, PAS UNE
            // AMÉLIORATION. `GET /api/v1/sante` doit rendre « la version de
            // migration APPLIQUÉE » — c'est le seul champ qui distingue « le
            // schéma est en retard sur le code » de « tout va bien ». Mesuré le
            // 20/08/2026 sous `palier_app` :
            //
            //     ERROR:  permission denied for table __EFMigrationsHistory
            //
            // Le rôle n'avait ni le privilège ni de politique. Les deux
            // manquaient : sous FORCE, le privilège seul ne suffit pas.
            //
            // SELECT SEULEMENT, et une politique de LECTURE seulement. Rien
            // n'ouvre l'écriture : c'est EF, sous `palier_migrations` et par la
            // politique `migrations_referentiel`, qui inscrit ses lignes.
            //
            // Ce que cela expose : le nom de la dernière migration et la version
            // d'EF Core. C'est une empreinte de version, pas une donnée
            // personnelle — et elle est déjà rendue par la route de santé, qui
            // est publique jusqu'au lot 4 (D41). Le choix d'exposer se fait donc
            // à la ROUTE, pas ici.
            migrationBuilder.Sql(
                """
                grant select on public."__EFMigrationsHistory" to palier_app;

                create policy lecture_version on public."__EFMigrationsHistory"
                  for select to palier_app
                  using (true);
                """
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // La vue tombe AVANT les tables, et ce n'est pas une politesse :
            // `drop table` sans `cascade` refuse tant qu'une vue en dépend. Un
            // `Down` qui oublie la vue échoue donc bruyamment — ce qui est le
            // bon comportement, mais l'épreuve `Down puis Up` de SchemaTests
            // existe pour que personne n'ait à le découvrir en production.
            //
            // Les politiques et les privilèges tombent avec leurs tables :
            // PostgreSQL les supprime en même temps que l'objet qui les porte.
            // `__EFMigrationsHistory` n'est pas supprimée par EF — sa politique
            // et sa RLS sont donc défaites explicitement, sans quoi un `Down`
            // suivi d'un `Up` échouerait sur « la politique existe déjà ».
            migrationBuilder.Sql("drop view if exists public.weekly_volume;");
            migrationBuilder.Sql("drop schema if exists app cascade;");
            migrationBuilder.Sql(
                """
                drop policy if exists migrations_referentiel on public."__EFMigrationsHistory";
                drop policy if exists lecture_version on public."__EFMigrationsHistory";
                revoke select on public."__EFMigrationsHistory" from palier_app;
                alter table public."__EFMigrationsHistory" no force row level security;
                alter table public."__EFMigrationsHistory" disable row level security;
                """
            );

            migrationBuilder.DropTable(
                name: "AspNetRoleClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens");

            migrationBuilder.DropTable(
                name: "body_weight");

            migrationBuilder.DropTable(
                name: "nutrient_refs");

            migrationBuilder.DropTable(
                name: "sets");

            migrationBuilder.DropTable(
                name: "AspNetRoles");

            migrationBuilder.DropTable(
                name: "exercises");

            migrationBuilder.DropTable(
                name: "workouts");

            migrationBuilder.DropTable(
                name: "AspNetUsers");
        }
    }
}
