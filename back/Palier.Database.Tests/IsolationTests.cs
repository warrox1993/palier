using System.Diagnostics;
using System.Globalization;
using Npgsql;

namespace Palier.Database.Tests;

/// <summary>
/// Les huit épreuves d'isolation, toutes sur un moteur réel et toutes sous
/// <c>palier_app</c>. <see cref="RolesTests" /> garantit que ce rôle n'est ni
/// superutilisateur ni porteur de <c>BYPASSRLS</c> ; sans cette garantie, tout
/// ce fichier serait vert sans rien démontrer.
///
/// CHAQUE ÉPREUVE NOMME SA TABLE. La même assertion — « sans identité, le
/// moteur lève » — est VRAIE sur `workouts` et FAUSSE sur `exercises`, dont la
/// branche publique sort ses lignes sans erreur. Une épreuve qui ne nomme pas
/// sa table prouve deux choses contradictoires selon ce qu'on lui donne.
///
/// Le SQL est écrit à la main, sans EF : aucune couche d'abstraction ne peut
/// alors être soupçonnée d'avoir filtré à la place des politiques.
/// </summary>
[Collection(BaseFixture.Collection)]
public sealed class IsolationTests(BaseFixture baseDeDonnees)
{
    /// <summary>
    /// Le troisième argument de <c>set_config</c>. À <c>true</c>, la valeur ne
    /// vit que dans la transaction courante : « If is_local is true, the new
    /// value will only apply during the current transaction ».
    ///
    /// À <c>false</c>, elle passe en portée SESSION, survit à la connexion
    /// rendue au pool, et l'utilisateur suivant hérite de l'identité du
    /// précédent — sans erreur, sans journal, sans test rouge. Toute la sûreté
    /// du dispositif tient à ce littéral, et
    /// <see cref="La_portee_transaction_ne_survit_pas_au_commit_sur_la_meme_connexion" />
    /// est la SEULE épreuve du lot qui le distingue.
    /// </summary>
    private const bool _porteeTransaction = true;

    // ================================================================
    // Épreuve 1
    // ================================================================

    [Fact]
    public async Task Sur_workouts_PEUPLEE_et_sans_identite_le_moteur_leve_28000()
    {
        // SUR TABLE PEUPLÉE, et le nom du test le dit.
        // `ddl-rowsecurity.html` : l'expression d'une politique « will be
        // evaluated FOR EACH ROW ». Sur une table VIDE, la politique n'est
        // JAMAIS évaluée : aucune exception, zéro ligne. Cette épreuve y
        // passerait pour la mauvaise raison. Le trou de la table vide est fermé
        // par la garde applicative de la tâche 8, pas ici.
        var a = await Utilisateur();
        await Seance(a);

        var lignes = await Compter("select count(*) from public.workouts", Migrations);
        Assert.True(lignes > 0, "La table `workouts` est vide : l'épreuve ne prouverait rien.");

        var refus = await Assert.ThrowsAsync<PostgresException>(async () =>
        {
            await using var connexion = await Ouvrir(baseDeDonnees.ChaineApp);
            await using var commande = new NpgsqlCommand(
                "select id from public.workouts",
                connexion
            );
            await using var lecteur = await commande.ExecuteReaderAsync();
            while (await lecteur.ReadAsync()) { }
        });

        Assert.True(
            refus.SqlState == "28000",
            $"Sur `workouts` peuplée et sans identité, attendu 28000, reçu {refus.SqlState} : "
                + refus.MessageText
        );
        Assert.DoesNotContain(a.ToString(), refus.MessageText, StringComparison.OrdinalIgnoreCase);
    }

    // ================================================================
    // Épreuve 2 — LA PLUS DÉCISIVE DU LOT
    // ================================================================

    [Fact]
    public async Task La_portee_transaction_ne_survit_pas_au_commit_sur_la_meme_connexion()
    {
        // POURQUOI ELLE EST DÉCISIVE. Le moteur ne garantit RIEN si le troisième
        // argument de `set_config` vaut `false` : la valeur passe en portée
        // session, et l'on retombe exactement sur le mécanisme écarté par D36 —
        // nettoyage délégué au pilote, annulé par `No Reset On Close`, par le
        // multiplexing d'Npgsql, ou par un PgBouncer en mode transaction, où la
        // matrice des fonctionnalités marque `SET/RESET` comme « Never ».
        //
        // Et l'épreuve 6 ci-dessous PASSE AU VERT avec `false` : la seconde
        // requête pose sa propre identité, écrase la précédente, et ne voit que
        // ses lignes. Test vert, mécanisme cassé. Sans cette épreuve-ci, toute
        // la sûreté du dispositif tiendrait à un littéral booléen que rien ne
        // regarde.
        //
        // `Pooling=false` : UNE connexion physique, la même avant et après le
        // COMMIT. Sans cela, le pool en rendrait une autre et l'épreuve
        // observerait une connexion neuve — verte pour la mauvaise raison.
        var a = await Utilisateur();
        await Seance(a);

        await using var connexion = await Ouvrir(baseDeDonnees.Chaine("palier_app", pooling: false));

        await using (var transaction = await connexion.BeginTransactionAsync())
        {
            await Poser(connexion, transaction, a);
            await using var dedans = new NpgsqlCommand(
                "select count(*) from public.workouts",
                connexion,
                transaction
            );
            Assert.True(
                (long)(await dedans.ExecuteScalarAsync())! >= 1,
                "L'identité posée DANS la transaction ne rend aucune ligne : l'épreuve ne "
                    + "mesurerait pas ce qu'elle croit."
            );
            await transaction.CommitAsync();
        }

        // Sur la MÊME connexion, sans rien reposer.
        await using (var apres = new NpgsqlCommand(
            "select current_setting('app.utilisateur', true)",
            connexion
        ))
        {
            var reste = await apres.ExecuteScalarAsync();
            var vu = reste is null or DBNull ? null : (string?)reste;
            Assert.True(
                string.IsNullOrEmpty(vu),
                $"`app.utilisateur` a SURVÉCU au COMMIT : « {vu} ». Le troisième argument de "
                    + "set_config vaut `false` — la valeur est en portée SESSION, elle sera "
                    + "rendue au pool avec la connexion, et l'utilisateur suivant héritera de "
                    + "l'identité du précédent."
            );
        }

        var refus = await Assert.ThrowsAsync<PostgresException>(async () =>
        {
            await using var commande = new NpgsqlCommand(
                "select id from public.workouts",
                connexion
            );
            await using var lecteur = await commande.ExecuteReaderAsync();
            while (await lecteur.ReadAsync()) { }
        });
        Assert.True(
            refus.SqlState == "28000",
            $"Après COMMIT, une lecture de `workouts` (peuplée) devrait lever 28000 ; "
                + $"reçu {refus.SqlState} : {refus.MessageText}"
        );
    }

    // ================================================================
    // Épreuves 3 et 4 — table `workouts`
    // ================================================================

    [Fact]
    public async Task Sur_workouts_A_ne_lit_aucune_ligne_de_B()
    {
        var a = await Utilisateur();
        var b = await Utilisateur();
        var seanceDeB = await Seance(b);

        var vues = await SousIdentite(
            a,
            "select count(*) from public.workouts where id = $1",
            seanceDeB
        );
        Assert.True(
            vues == 0,
            $"A voit {vues} ligne(s) de B dans `workouts`. La politique `proprietaire` ne "
                + "filtre pas."
        );

        // Et A voit bien SES lignes : sans cette seconde moitié, une politique
        // qui refuserait tout passerait l'épreuve en ne prouvant rien.
        var seanceDeA = await Seance(a);
        var siennes = await SousIdentite(
            a,
            "select count(*) from public.workouts where id = $1",
            seanceDeA
        );
        Assert.True(siennes == 1, "A ne voit pas sa propre séance : la politique refuse tout.");
    }

    [Fact]
    public async Task Sur_workouts_A_ne_peut_pas_inserer_au_nom_de_B()
    {
        var a = await Utilisateur();
        var b = await Utilisateur();

        var refus = await Assert.ThrowsAsync<PostgresException>(async () =>
        {
            await using var connexion = await Ouvrir(baseDeDonnees.ChaineApp);
            await using var transaction = await connexion.BeginTransactionAsync();
            await Poser(connexion, transaction, a);
            await using var commande = new NpgsqlCommand(
                "insert into public.workouts (owner_id) values ($1)",
                connexion,
                transaction
            );
            commande.Parameters.AddWithValue(b);
            await commande.ExecuteNonQueryAsync();
        });

        Assert.True(
            refus.SqlState == "42501",
            $"Attendu 42501 (violation de politique) sur `workouts`, reçu {refus.SqlState}"
        );
        Assert.Contains("row-level security", refus.MessageText, StringComparison.OrdinalIgnoreCase);
    }

    // ================================================================
    // Épreuve 5 — table `sets`, possédée PAR JOINTURE
    // ================================================================

    [Fact]
    public async Task Sur_sets_A_ne_voit_aucune_serie_d_une_seance_de_B()
    {
        var a = await Utilisateur();
        var b = await Utilisateur();
        var seanceDeB = await Seance(b);
        var exercice = await Exercice(estPersonnalise: false, proprietaire: null);
        var serieDeB = await Serie(seanceDeB, exercice);

        var vues = await SousIdentite(a, "select count(*) from public.sets where id = $1", serieDeB);
        Assert.True(
            vues == 0,
            $"A voit {vues} série(s) de B dans `sets`. Cette table n'a AUCUN owner_id : sa "
                + "politique doit remonter jusqu'à `workouts`, et c'est ce chemin-là qui casse."
        );

        var seanceDeA = await Seance(a);
        var serieDeA = await Serie(seanceDeA, exercice);
        var siennes = await SousIdentite(
            a,
            "select count(*) from public.sets where id = $1",
            serieDeA
        );
        Assert.True(siennes == 1, "A ne voit pas sa propre série : la politique de `sets` refuse tout.");
    }

    // ================================================================
    // Épreuve 6 — deux identités sur la même connexion physique
    // ================================================================

    [Fact]
    public async Task Deux_identites_successives_sur_la_meme_connexion_ne_se_melangent_pas()
    {
        // ATTENTION : cette épreuve PASSE AU VERT même avec `set_config(..., false)`.
        // Elle est conservée parce qu'elle couvre le cas nominal du pool, mais
        // elle ne prouve RIEN sur la portée — c'est l'épreuve 2 qui le fait.
        var a = await Utilisateur();
        var b = await Utilisateur();
        await Seance(a);
        var seanceDeB = await Seance(b);

        await using var connexion = await Ouvrir(baseDeDonnees.Chaine("palier_app", pooling: false));

        long vuesParA;
        await using (var transaction = await connexion.BeginTransactionAsync())
        {
            await Poser(connexion, transaction, a);
            await using var commande = new NpgsqlCommand(
                "select count(*) from public.workouts where id = $1",
                connexion,
                transaction
            );
            commande.Parameters.AddWithValue(seanceDeB);
            vuesParA = (long)(await commande.ExecuteScalarAsync())!;
            await transaction.CommitAsync();
        }

        long vuesParB;
        await using (var transaction = await connexion.BeginTransactionAsync())
        {
            await Poser(connexion, transaction, b);
            await using var commande = new NpgsqlCommand(
                "select count(*) from public.workouts where id = $1",
                connexion,
                transaction
            );
            commande.Parameters.AddWithValue(seanceDeB);
            vuesParB = (long)(await commande.ExecuteScalarAsync())!;
            await transaction.CommitAsync();
        }

        Assert.True(vuesParA == 0, $"A voit {vuesParA} séance(s) de B sur la connexion partagée.");
        Assert.True(vuesParB == 1, $"B ne voit plus sa propre séance après le passage de A.");
    }

    // ================================================================
    // Épreuve 7 — table `exercises`, branche publique
    // ================================================================

    [Fact]
    public async Task Sur_exercises_le_catalogue_public_sort_SANS_identite_et_le_reste_non()
    {
        // L'assertion inverse de l'épreuve 1, sur une AUTRE table — et les deux
        // sont justes. C'est pourquoi chaque nom de test porte sa table.
        var b = await Utilisateur();
        var publie = await Exercice(estPersonnalise: false, proprietaire: null);
        var aB = await Exercice(estPersonnalise: true, proprietaire: b);

        await using var connexion = await Ouvrir(baseDeDonnees.ChaineApp);

        await using (var commande = new NpgsqlCommand(
            "select count(*) from public.exercises where id = $1",
            connexion
        ))
        {
            commande.Parameters.AddWithValue(publie);
            var vues = (long)(await commande.ExecuteScalarAsync())!;
            Assert.True(
                vues == 1,
                "Sans identité, une ligne `is_custom = false` de `exercises` doit SORTIR, et "
                    + $"sans erreur ; {vues} vue(s). Une politique qui appellerait l'accesseur "
                    + "qui lève transformerait toute lecture anonyme en erreur 500."
            );
        }

        await using (var commande = new NpgsqlCommand(
            "select count(*) from public.exercises where id = $1",
            connexion
        ))
        {
            commande.Parameters.AddWithValue(aB);
            var vues = (long)(await commande.ExecuteScalarAsync())!;
            Assert.True(
                vues == 0,
                $"Sans identité, {vues} exercice(s) POSSÉDÉ(S) de `exercises` sort(ent). La "
                    + "branche publique ne doit rien découvrir des lignes d'un propriétaire."
            );
        }
    }

    // ================================================================
    // Épreuve 8 — tables `AspNet*`, refus par défaut
    // ================================================================

    [Fact]
    public async Task Sur_AspNetUsers_ni_palier_app_ni_le_proprietaire_ne_lisent_ou_n_ecrivent()
    {
        var existant = await Utilisateur();

        // Sous `palier_app` : aucun privilège n'a été accordé sur ces tables.
        // Le refus vient de la couche des PRIVILÈGES, code 42501, et c'est déjà
        // la bonne réponse.
        var refusApp = await Assert.ThrowsAsync<PostgresException>(async () =>
        {
            await using var connexion = await Ouvrir(baseDeDonnees.ChaineApp);
            await using var commande = new NpgsqlCommand(
                """select count(*) from public."AspNetUsers" """,
                connexion
            );
            await commande.ExecuteScalarAsync();
        });
        Assert.True(
            refusApp.SqlState == "42501",
            $"palier_app lit `AspNetUsers` : attendu 42501, reçu {refusApp.SqlState}"
        );

        // Sous `palier_migrations`, QUI POSSÈDE CES TABLES et a tous les
        // privilèges dessus. Ici il ne reste que RLS — et sous `force` sans
        // politique, « a default-deny policy is used, meaning that no rows are
        // visible or can be modified ». C'est la moitié de l'épreuve qui prouve
        // vraiment D38.
        var vues = await Compter("""select count(*) from public."AspNetUsers" """, Migrations);
        Assert.True(
            vues == 0,
            $"Le PROPRIÉTAIRE voit {vues} ligne(s) de `AspNetUsers` alors qu'il n'existe "
                + "aucune politique. `force row level security` ne mord pas."
        );

        var refusEcriture = await Assert.ThrowsAsync<PostgresException>(async () =>
        {
            await using var connexion = await Ouvrir(baseDeDonnees.ChaineMigrations);
            await using var commande = new NpgsqlCommand(
                """
                insert into public."AspNetUsers" ("Id", "EmailConfirmed", "PhoneNumberConfirmed",
                       "TwoFactorEnabled", "LockoutEnabled", "AccessFailedCount")
                values (gen_random_uuid(), false, false, false, false, 0)
                """,
                connexion
            );
            await commande.ExecuteNonQueryAsync();
        });
        Assert.True(
            refusEcriture.SqlState == "42501",
            $"Le propriétaire écrit dans `AspNetUsers` : attendu 42501, reçu "
                + refusEcriture.SqlState
        );

        // Et la ligne existe bel et bien : sans cette assertion, « zéro ligne
        // visible » serait vrai sur une table vide et ne prouverait rien.
        var reelles = await Compter(
            """select count(*) from public."AspNetUsers" """,
            baseDeDonnees.ChaineAdministrateur
        );
        Assert.True(
            reelles > 0,
            $"`AspNetUsers` ne contient aucune ligne (identifiant posé : {existant:N} absent). "
                + "Le refus par défaut serait indiscernable d'une table vide."
        );
    }

    // ================================================================
    // La borne de transaction oisive — vérifiée SUR LA SESSION, pas relue
    // ================================================================

    [Fact]
    public async Task La_borne_de_transaction_oisive_est_APPLIQUEE_a_la_session_de_palier_app()
    {
        // Un réglage accepté sans erreur n'est pas un réglage appliqué (P11).
        // `alter role … set …` réussit silencieusement même quand rien ne le
        // reprend : on interroge donc `pg_settings` SUR UNE CONNEXION
        // `palier_app`, ce qui est la seule chose qui compte.
        await using var connexion = await Ouvrir(baseDeDonnees.ChaineApp);
        await using var commande = new NpgsqlCommand(
            "select setting from pg_settings where name = 'idle_in_transaction_session_timeout'",
            connexion
        );
        var valeur = (string?)await commande.ExecuteScalarAsync();

        Assert.True(
            valeur is not null && valeur != "0",
            $"`idle_in_transaction_session_timeout` vaut « {valeur ?? "(absent)"} » sur une "
                + "session palier_app. À 0 la borne est désactivée : une transaction laissée "
                + "ouverte pendant un appel réseau bloquerait VACUUM et épuiserait le pool."
        );
    }

    // ================================================================
    // Le coût, mesuré plutôt que supposé
    // ================================================================

    [Fact]
    public async Task Le_cout_des_trois_allers_retours_est_mesure_et_non_suppose()
    {
        // Le mécanisme ajoute TROIS allers-retours par cas d'usage — BEGIN,
        // set_config, COMMIT — pas un. La requête mesurée est la plus chère du
        // schéma : `sets` avec un `exists` sur `workouts`, dont la politique
        // s'applique à son tour dans la sous-requête.
        var a = await Utilisateur();
        var seance = await Seance(a);
        var exercice = await Exercice(estPersonnalise: false, proprietaire: null);
        for (var i = 0; i < 50; i++)
        {
            await Serie(seance, exercice);
        }

        await using var connexion = await Ouvrir(baseDeDonnees.Chaine("palier_app", pooling: false));
        var chrono = Stopwatch.StartNew();
        const int tours = 20;
        for (var i = 0; i < tours; i++)
        {
            await using var transaction = await connexion.BeginTransactionAsync();
            await Poser(connexion, transaction, a);
            await using var commande = new NpgsqlCommand(
                "select count(*) from public.sets",
                connexion,
                transaction
            );
            await commande.ExecuteScalarAsync();
            await transaction.CommitAsync();
        }

        chrono.Stop();
        var parCas = chrono.Elapsed.TotalMilliseconds / tours;

        // CE QUE CE CHIFFRE NE PROUVE PAS, et il faut le dire : il porte sur un
        // conteneur local, en boucle locale, sur une base de quelques dizaines
        // de lignes. L'instance managée n'existe pas encore. Le plafond de
        // 100 ms de `docs/08-workflow.md` § 6 n'est donc PAS acquis par cette
        // mesure — elle établit seulement que le mécanisme lui-même ne le
        // consomme pas.
        Assert.True(
            parCas < 100,
            $"Un cas d'usage complet (BEGIN · set_config · select · COMMIT) sur `sets` prend "
                + $"{parCas:F1} ms, au-delà du plafond de 100 ms. Mesure : {tours} tours, "
                + "conteneur local, 50 séries."
        );
    }

    // ================================================================
    // Outillage
    // ================================================================

    private string Migrations => baseDeDonnees.ChaineMigrations;

    /// <summary>
    /// La pose de l'identité, telle que le pipeline de la tâche 8 la fera.
    /// <c>set_config</c> attend un <c>text</c> en deuxième argument : un
    /// <see cref="Guid" /> passé en paramètre part en <c>uuid</c> et ne trouve
    /// pas la fonction. Le <c>ToString()</c> est OBLIGATOIRE.
    /// </summary>
    private static async Task Poser(
        NpgsqlConnection connexion,
        NpgsqlTransaction transaction,
        Guid identifiant
    )
    {
        // La transaction est passée EXPLICITEMENT, bien qu'Npgsql enlise déjà la
        // commande dans celle qui est ouverte sur la connexion — mesuré le
        // 20/08/2026 en la retirant : rien ne changeait. Ce n'est donc PAS cet
        // argument qui donne sa localité au `set_config`, c'est le `BEGIN`. Le
        // franchissement l'a montré en posant l'identité AVANT le `BEGIN` :
        // 28000 immédiat, la valeur ayant vécu le temps d'une transaction
        // implicite d'une seule instruction.
        await using var commande = new NpgsqlCommand(
            "select set_config('app.utilisateur', $1, $2)",
            connexion,
            transaction
        );
        commande.Parameters.AddWithValue(identifiant.ToString());
        commande.Parameters.AddWithValue(_porteeTransaction);
        await commande.ExecuteScalarAsync();
    }

    private async Task<long> SousIdentite(Guid identifiant, string sql, Guid parametre)
    {
        await using var connexion = await Ouvrir(baseDeDonnees.ChaineApp);
        await using var transaction = await connexion.BeginTransactionAsync();
        await Poser(connexion, transaction, identifiant);
#pragma warning disable CA2100 // Le SQL vient de constantes littérales de ce fichier.
        await using var commande = new NpgsqlCommand(sql, connexion, transaction);
#pragma warning restore CA2100
        commande.Parameters.AddWithValue(parametre);
        var compte = (long)(await commande.ExecuteScalarAsync())!;
        await transaction.CommitAsync();
        return compte;
    }

    private static async Task<NpgsqlConnection> Ouvrir(string chaine)
    {
        var connexion = new NpgsqlConnection(chaine);
        await connexion.OpenAsync();
        return connexion;
    }

    private static async Task<long> Compter(string sql, string chaine)
    {
        await using var connexion = await Ouvrir(chaine);
#pragma warning disable CA2100 // Le SQL vient de constantes littérales de ce fichier.
        await using var commande = new NpgsqlCommand(sql, connexion);
#pragma warning restore CA2100
        return (long)(await commande.ExecuteScalarAsync())!;
    }

    /// <summary>
    /// Crée un utilisateur. Sous le compte d'ADMINISTRATION : `AspNetUsers`
    /// naît en refus par défaut (D38) et ni `palier_app` ni le propriétaire ne
    /// peuvent y écrire — c'est justement ce que l'épreuve 8 démontre. Le lot 4
    /// concevra le vrai chemin ; ici, seule la fixture triche, et elle le dit.
    /// </summary>
    private async Task<Guid> Utilisateur()
    {
        var identifiant = Guid.NewGuid();
        await using var connexion = await Ouvrir(baseDeDonnees.ChaineAdministrateur);
        await using var commande = new NpgsqlCommand(
            """
            insert into public."AspNetUsers" ("Id", "EmailConfirmed", "PhoneNumberConfirmed",
                   "TwoFactorEnabled", "LockoutEnabled", "AccessFailedCount")
            values ($1, false, false, false, false, 0)
            """,
            connexion
        );
        commande.Parameters.AddWithValue(identifiant);
        await commande.ExecuteNonQueryAsync();
        return identifiant;
    }

    private async Task<Guid> Seance(Guid proprietaire)
    {
        await using var connexion = await Ouvrir(Migrations);
        await using var commande = new NpgsqlCommand(
            "insert into public.workouts (owner_id) values ($1) returning id",
            connexion
        );
        commande.Parameters.AddWithValue(proprietaire);
        return (Guid)(await commande.ExecuteScalarAsync())!;
    }

    private async Task<Guid> Exercice(bool estPersonnalise, Guid? proprietaire)
    {
        await using var connexion = await Ouvrir(Migrations);
        await using var commande = new NpgsqlCommand(
            """
            insert into public.exercises (name, primary_muscles, is_custom, owner_id)
            values ($1, array['pectoraux'], $2, $3) returning id
            """,
            connexion
        );
        commande.Parameters.AddWithValue(
            $"exercice-{Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)}"
        );
        commande.Parameters.AddWithValue(estPersonnalise);
        commande.Parameters.AddWithValue(
            proprietaire.HasValue ? proprietaire.Value : (object)DBNull.Value
        );
        return (Guid)(await commande.ExecuteScalarAsync())!;
    }

    private async Task<Guid> Serie(Guid seance, Guid exercice)
    {
        await using var connexion = await Ouvrir(Migrations);
        await using var commande = new NpgsqlCommand(
            """
            insert into public.sets (workout_id, exercise_id, set_index)
            values ($1, $2, 1) returning id
            """,
            connexion
        );
        commande.Parameters.AddWithValue(seance);
        commande.Parameters.AddWithValue(exercice);
        return (Guid)(await commande.ExecuteScalarAsync())!;
    }

    // ================================================================
    // Épreuve 9 — `sessions_refresh` : palier_auth seul, et la cascade
    // ================================================================

    [Fact]
    public async Task Sur_sessions_refresh_palier_app_n_a_aucun_privilege()
    {
        // Une session porte l'empreinte d'un jeton et l'appareil de son porteur.
        // Elle n'a rien à faire sur le chemin des données de santé, et le rôle
        // qui les sert n'a rien à y faire non plus.
        var refus = await Assert.ThrowsAsync<PostgresException>(async () =>
        {
            await using var connexion = await Ouvrir(baseDeDonnees.ChaineApp);
            await using var commande = new NpgsqlCommand(
                "select count(*) from public.sessions_refresh",
                connexion
            );
            await commande.ExecuteScalarAsync();
        });

        Assert.True(
            refus.SqlState == "42501",
            $"palier_app lit `sessions_refresh` : attendu 42501, reçu {refus.SqlState}"
        );
    }

    [Fact]
    public async Task Sur_sessions_refresh_palier_auth_lit_et_ecrit()
    {
        var utilisateur = await Utilisateur();

        await using var connexion = await Ouvrir(baseDeDonnees.ChaineAuth);
        await using var insertion = new NpgsqlCommand(
            """
            insert into public.sessions_refresh
              (id, owner_id, token_hash, family_id, created_at, expires_at, last_seen_at)
            values (gen_random_uuid(), $1, $2, gen_random_uuid(), now(),
                    now() + interval '14 days', now())
            """,
            connexion
        );
        insertion.Parameters.AddWithValue(utilisateur);
        insertion.Parameters.AddWithValue(new byte[32]);

        Assert.True(
            await insertion.ExecuteNonQueryAsync() == 1,
            "palier_auth n'écrit pas dans `sessions_refresh` : le magasin de sessions "
                + "n'aurait aucun chemin."
        );
    }

    [Fact]
    public async Task Sur_AspNetUsers_palier_auth_lit_desormais()
    {
        await Utilisateur();

        // D38 fermait ces tables à TOUT LE MONDE en attendant ce lot. Si cette
        // épreuve rougit, le chemin de connexion n'existe pas.
        var vues = await Compter(
            """select count(*) from public."AspNetUsers" """,
            baseDeDonnees.ChaineAuth
        );

        Assert.True(
            vues > 0,
            "palier_auth ne lit pas `AspNetUsers` : la connexion par email est impossible."
        );
    }

    [Fact]
    public async Task Sur_AspNetUsers_palier_app_reste_refuse()
    {
        // L'ouverture faite pour palier_auth ne doit RIEN ouvrir à palier_app.
        // Une politique permissive ne s'applique qu'aux rôles qu'elle nomme, et
        // cette épreuve garde la promesse.
        var refus = await Assert.ThrowsAsync<PostgresException>(async () =>
        {
            await using var connexion = await Ouvrir(baseDeDonnees.ChaineApp);
            await using var commande = new NpgsqlCommand(
                """select count(*) from public."AspNetUsers" """,
                connexion
            );
            await commande.ExecuteScalarAsync();
        });

        Assert.True(
            refus.SqlState == "42501",
            $"palier_app lit `AspNetUsers` après l'ouverture faite à palier_auth : "
                + $"attendu 42501, reçu {refus.SqlState}"
        );
    }

    [Fact]
    public async Task La_suppression_d_un_utilisateur_emporte_ses_sessions()
    {
        // RGPD article 17. La cascade est portée par la contrainte ; cette
        // épreuve vérifie qu'elle mord vraiment, sur une session RÉELLE.
        var utilisateur = await Utilisateur();

        await using (var connexion = await Ouvrir(baseDeDonnees.ChaineAuth))
        {
            await using var insertion = new NpgsqlCommand(
                """
                insert into public.sessions_refresh
                  (id, owner_id, token_hash, family_id, created_at, expires_at, last_seen_at)
                values (gen_random_uuid(), $1, $2, gen_random_uuid(), now(),
                        now() + interval '14 days', now())
                """,
                connexion
            );
            insertion.Parameters.AddWithValue(utilisateur);
            insertion.Parameters.AddWithValue(new byte[32]);
            await insertion.ExecuteNonQueryAsync();
        }

        // Sans cette assertion, « zéro session après suppression » serait vrai
        // sur une table vide et ne prouverait rien.
        var avant = await CompterSessionsDe(utilisateur);
        Assert.True(avant == 1, $"la session n'a pas été créée : {avant} trouvée(s)");

        await using (var connexion = await Ouvrir(baseDeDonnees.ChaineAdministrateur))
        {
            await using var suppression = new NpgsqlCommand(
                """delete from public."AspNetUsers" where "Id" = $1""",
                connexion
            );
            suppression.Parameters.AddWithValue(utilisateur);
            await suppression.ExecuteNonQueryAsync();
        }

        var apres = await CompterSessionsDe(utilisateur);
        Assert.True(
            apres == 0,
            $"{apres} session(s) survivent à leur utilisateur supprimé — article 17."
        );
    }

    [Fact]
    public async Task La_cascade_NE_DEBORDE_PAS_sur_les_autres_comptes()
    {
        // L'autre moitié de la règle, et celle qu'on oublie. Une contrainte
        // trop large — ou un `delete` sans clause — emporterait les sessions de
        // tout le monde, et l'épreuve ci-dessus resterait parfaitement verte.
        var vise = await Utilisateur();
        var voisin = await Utilisateur();

        await using (var connexion = await Ouvrir(baseDeDonnees.ChaineAuth))
        {
            foreach (var proprietaire in new[] { vise, voisin })
            {
                await using var insertion = new NpgsqlCommand(
                    """
                    insert into public.sessions_refresh
                      (id, owner_id, token_hash, family_id, created_at, expires_at, last_seen_at)
                    values (gen_random_uuid(), $1, $2, gen_random_uuid(), now(),
                            now() + interval '14 days', now())
                    """,
                    connexion
                );
                insertion.Parameters.AddWithValue(proprietaire);
                insertion.Parameters.AddWithValue(Guid.NewGuid().ToByteArray());
                await insertion.ExecuteNonQueryAsync();
            }
        }

        Assert.True(
            await CompterSessionsDe(voisin) == 1,
            "le harnais n'a pas créé la session du voisin : l'épreuve ne prouverait rien."
        );

        await using (var connexion = await Ouvrir(baseDeDonnees.ChaineAdministrateur))
        {
            await using var suppression = new NpgsqlCommand(
                """delete from public."AspNetUsers" where "Id" = $1""",
                connexion
            );
            suppression.Parameters.AddWithValue(vise);
            await suppression.ExecuteNonQueryAsync();
        }

        Assert.True(
            await CompterSessionsDe(vise) == 0,
            "la cascade n'a pas emporté les sessions du compte supprimé."
        );
        Assert.True(
            await CompterSessionsDe(voisin) == 1,
            "la cascade a DÉBORDÉ : les sessions d'un autre compte ont disparu."
        );
    }

    private async Task<long> CompterSessionsDe(Guid utilisateur)
    {
        await using var connexion = await Ouvrir(baseDeDonnees.ChaineAdministrateur);
        await using var commande = new NpgsqlCommand(
            "select count(*) from public.sessions_refresh where owner_id = $1",
            connexion
        );
        commande.Parameters.AddWithValue(utilisateur);
        return (long)(await commande.ExecuteScalarAsync())!;
    }
}
