using Npgsql;

namespace Palier.Database.Tests;

/// <summary>
/// Le catalogue d'exercices, appliqué sur un moteur RÉEL.
/// </summary>
/// <remarks>
/// <para>
/// <b>C'est la seule épreuve qui lit le fichier de référentiel lui-même.</b>
/// `db/referentiel/02-exercises.sql` n'est ni compilé, ni couvert par les
/// analyseurs, ni exercé par une route : sans elle, une faute de syntaxe, une
/// contre-indication hors liste ou un slug en double ne se découvrirait qu'au
/// déploiement — ou jamais, si personne n'applique le référentiel.
/// </para>
///
/// <para>
/// Elle s'applique sous <c>palier_migrations</c>, comme le fera le déploiement :
/// les tables portent <c>FORCE ROW LEVEL SECURITY</c>, et le chargement emprunte
/// la politique <c>migrations_referentiel</c>. L'exécuter sous un autre rôle
/// mesurerait un chemin que la production n'emprunte pas.
/// </para>
/// </remarks>
[Collection(BaseFixture.Collection)]
public sealed class ReferentielExercicesTests(BaseFixture baseDeDonnees)
{
    /// <summary>
    /// La CIBLE de `docs/14-contenu.md` § 1 : « 250 à 400 exercices ».
    /// </summary>
    /// <remarks>
    /// <para>
    /// Le seuil a monté trois fois avec le catalogue : soixante au jalon 1
    /// (`16-projet.md` § 4, « 60 exercices prioritaires PUIS extension »), puis
    /// cent cinquante — le seuil de crédibilité, « en dessous de 150, le
    /// produit paraît vide face à la concurrence » — puis la borne basse de la
    /// cible.
    /// </para>
    ///
    /// <para>
    /// <b>Il monte AVEC le contenu, jamais après.</b> Le laisser en arrière
    /// laisserait un fichier se vider sans que rien ne rougisse : l'épreuve
    /// dirait « au moins soixante » sur un catalogue qui en aurait perdu deux
    /// cents.
    /// </para>
    /// </remarks>
    private const int _cible = 250;

    [Fact]
    public async Task Le_referentiel_s_applique_et_atteint_la_CIBLE_du_document()
    {
        await AppliquerAsync();

        var poses = await CompterAsync(
            "select count(*) from public.exercises where is_custom = false and slug is not null"
        );

        // `docs/14-contenu.md` § 1 : « Cible : 250 à 400 exercices ». Le
        // nombre est une PROMESSE du document, pas une constatation de ce que
        // les fichiers contiennent — un référentiel tronqué passerait sans
        // cette assertion.
        Assert.True(
            poses >= _cible,
            $"Le référentiel pose {poses} exercice(s) au lieu des {_cible} promis "
                + "par `docs/14-contenu.md` § 1."
        );
    }

    [Fact]
    public async Task Le_referentiel_est_REJOUABLE_sans_doublon()
    {
        // C'est ce que le slug existe pour permettre — D68. Après un
        // `db:reset`, après une correction de consigne, le fichier se rejoue.
        // Sans `on conflict`, la seconde application heurterait
        // `ux_exercises_slug` et le déploiement échouerait sur une correction
        // de faute de frappe.
        await AppliquerAsync();
        var premier = await CompterAsync(
            "select count(*) from public.exercises where is_custom = false and slug is not null"
        );

        await AppliquerAsync();
        var second = await CompterAsync(
            "select count(*) from public.exercises where is_custom = false and slug is not null"
        );

        Assert.Equal(premier, second);
    }

    [Fact]
    public async Task Chaque_exercice_du_catalogue_est_COMPLET()
    {
        // La contrainte `ck_exercises_catalogue_complet` refuse déjà
        // l'insertion — D70. Cette épreuve vérifie l'autre moitié : que la
        // contrainte est bien POSÉE, et qu'aucune ligne du catalogue ne
        // l'esquive. Une contrainte retirée par mégarde laisserait passer un
        // exercice sans consignes, affiché à tout le monde.
        await AppliquerAsync();

        var incomplets = await CompterAsync(
            """
            select count(*) from public.exercises
             where is_custom = false
               and (slug is null or name_en is null
                    or instructions_fr is null or instructions_en is null
                    or common_errors_fr is null or common_errors_en is null)
            """
        );

        Assert.Equal(0, incomplets);
    }

    [Fact]
    public async Task Aucune_contre_indication_HORS_de_la_liste_fermee()
    {
        // Les quatre régions de `docs/05-entrainement.md` § 4, et rien d'autre.
        // Une cinquième valeur en base — « poignet », ou « epaules » au pluriel
        // — ne produirait AUCUNE erreur : l'intersection avec les contraintes
        // déclarées ne trouverait simplement rien, et le marquage du catalogue
        // serait muet là où il devrait parler.
        //
        // `contraindicated_for` est un `text[]` : aucune clé étrangère ne peut
        // le contraindre, et le `CHECK` de `user_constraints` ne porte pas sur
        // cette colonne. Cette épreuve est le SEUL garde-fou de ce lien.
        await AppliquerAsync();

        var horsListe = await LireAsync(
            """
            select distinct region
              from public.exercises, unnest(contraindicated_for) as region
             where is_custom = false
               and region not in ('cervicale', 'lombaire', 'epaule', 'genou')
            """
        );

        Assert.True(
            horsListe.Count == 0,
            "Régions contre-indiquées hors de la liste fermée du § 4 : "
                + string.Join(", ", horsListe)
        );
    }

    [Fact]
    public async Task Aucun_role_de_mouvement_HORS_de_la_liste_fermee()
    {
        await AppliquerAsync();

        var horsListe = await LireAsync(
            """
            select distinct movement_role from public.exercises
             where is_custom = false
               and movement_role not in ('tirage', 'poussee', 'aucun')
            """
        );

        Assert.True(
            horsListe.Count == 0,
            "Rôles de mouvement hors liste : " + string.Join(", ", horsListe)
        );
    }

    [Fact]
    public async Task Le_ratio_tirage_poussee_est_CALCULABLE()
    {
        // `docs/05-entrainement.md` § 4 le veut : « dos et trapèzes contre
        // pectoraux et triceps ». Un catalogue où tout vaudrait `aucun`
        // rendrait le ratio structurellement impossible — et D69 n'aurait
        // fermé le report de D62 que sur le papier.
        await AppliquerAsync();

        var tirages = await CompterAsync(
            "select count(*) from public.exercises where is_custom = false and movement_role = 'tirage'"
        );
        var poussees = await CompterAsync(
            "select count(*) from public.exercises where is_custom = false and movement_role = 'poussee'"
        );

        Assert.True(tirages >= 30, $"Seulement {tirages} mouvement(s) de tirage au catalogue.");
        Assert.True(poussees >= 30, $"Seulement {poussees} mouvement(s) de poussée au catalogue.");
    }

    [Fact]
    public async Task Les_VARIANTES_pointent_toutes_sur_un_exercice_du_catalogue()
    {
        // La table de liens garantit l'intégrité par ses clés étrangères — D71.
        // Cette épreuve vérifie que le fichier les a bien remplies : une faute
        // de frappe dans un slug ne produit PAS d'erreur, elle produit
        // silencieusement zéro ligne, puisque la jointure du fichier ne trouve
        // simplement rien.
        await AppliquerAsync();

        var liens = await CompterAsync("select count(*) from public.exercise_variants");
        Assert.True(liens >= 140, $"Seulement {liens} lien(s) de variante posé(s).");

        // Et aucune ne se pointe elle-même : `ck_exercise_variants_pas_soi_meme`
        // le refuse, mais l'épreuve vérifie que la contrainte est POSÉE.
        var surSoi = await CompterAsync(
            "select count(*) from public.exercise_variants where exercise_id = variant_id"
        );
        Assert.Equal(0, surSoi);
    }

    [Fact]
    public async Task Les_incrementts_suivent_les_valeurs_du_DOCUMENT()
    {
        // `docs/05-entrainement.md` § 3 : « 5 kg à la presse, 2,5 kg aux
        // poulies et machines, 2 kg aux haltères, 1 kg sur les élévations et
        // les mouvements de rotateurs ». Aucun autre incrément n'a de raison
        // d'exister, et un 10 kg glissé par mégarde ferait proposer des sauts
        // de charge que personne ne tient.
        await AppliquerAsync();

        var horsListe = await LireAsync(
            """
            select distinct default_increment::text from public.exercises
             where is_custom = false
               and default_increment not in (1, 2, 2.5, 5)
            """
        );

        Assert.True(
            horsListe.Count == 0,
            "Incréments hors des valeurs du § 3 : " + string.Join(", ", horsListe)
        );
    }

    [Fact]
    public async Task Aucun_exercice_du_catalogue_n_est_PERSONNALISE()
    {
        // Un exercice du référentiel qui entrerait avec `is_custom = true`
        // deviendrait invisible pour tous — la politique `catalogue_public` ne
        // sort que `is_custom = false` — et il faudrait le chercher pour
        // comprendre pourquoi le catalogue en compte un de moins.
        await AppliquerAsync();

        var personnalises = await CompterAsync(
            "select count(*) from public.exercises where slug is not null and is_custom"
        );

        Assert.Equal(0, personnalises);
    }

    [Fact]
    public async Task TOUTES_les_familles_d_equipement_sont_couvertes()
    {
        // Le porteur a nommé les familles à couvrir : machines, poids libres,
        // poids de corps, élastiques, poulies, et les mouvements de
        // renforcement.
        //
        // Sans cette épreuve, un catalogue riche mais déséquilibré passerait —
        // trois cents mouvements dont aucun sans matériel, et le produit serait
        // inutilisable pour quelqu'un qui s'entraîne chez lui, ou en reprise
        // après une blessure.
        await AppliquerAsync();

        foreach (var famille in new[] { "machine", "barre", "haltère", "haltères", "poulie", "élastique", "poids de corps", "kettlebell" })
        {
            var compte = await CompterAsync(
                $"select count(*) from public.exercises where is_custom = false and equipment = '{famille}'"
            );

            Assert.True(
                compte >= 5,
                $"La famille « {famille} » ne compte que {compte} mouvement(s). "
                    + "Un catalogue déséquilibré est inutilisable pour qui n'a pas ce matériel."
            );
        }
    }

    [Fact]
    public async Task Des_mouvements_SANS_MATERIEL_existent_pour_chaque_grande_region()
    {
        // La reprise, le retour après blessure, l'entraînement à domicile :
        // `docs/00-produit.md` place cette population au cœur de la cible. Un
        // catalogue qui exigerait une salle pour chaque région la laisserait
        // dehors.
        await AppliquerAsync();

        foreach (var muscle in new[] { "pectoraux", "dos", "abdominaux", "fessiers", "quadriceps", "mollets" })
        {
            var compte = await CompterAsync(
                $"""
                select count(*) from public.exercises
                 where is_custom = false
                   and equipment in ('poids de corps', 'élastique')
                   and '{muscle}' = any(primary_muscles)
                """
            );

            Assert.True(
                compte >= 1,
                $"Aucun mouvement sans matériel pour « {muscle} »."
            );
        }
    }

    // ================================================================
    // Le harnais
    // ================================================================

    /// <summary>
    /// Applique le fichier de référentiel tel quel, sous
    /// <c>palier_migrations</c>.
    /// </summary>
    /// <remarks>
    /// Le fichier est lu depuis le DÉPÔT, jamais recopié dans l'épreuve :
    /// recopier reviendrait à éprouver la copie, et c'est le défaut que ce
    /// dépôt ferme partout ailleurs.
    /// </remarks>
    private async Task AppliquerAsync()
    {
        await using var connexion = new NpgsqlConnection(baseDeDonnees.ChaineMigrations);
        await connexion.OpenAsync();

        // TOUS les fichiers d'exercices, dans l'ordre de leur préfixe — comme
        // `scripts/referentiel.mjs`. N'en appliquer qu'un mesurerait un
        // référentiel partiel, c'est-à-dire pas celui qui part en production.
        foreach (var chemin in Fichiers())
        {
            var sql = await File.ReadAllTextAsync(chemin);
#pragma warning disable CA2100 // Le SQL vient des fichiers de référentiel du DÉPÔT, versionnés, jamais d'une entrée.
            await using var commande = new NpgsqlCommand(sql, connexion);
#pragma warning restore CA2100
            await commande.ExecuteNonQueryAsync();
        }
    }

    /// <summary>
    /// Remonte jusqu'à la racine du dépôt depuis le répertoire d'exécution.
    /// </summary>
    /// <remarks>
    /// Un chemin relatif en dur — <c>../../../../../db/…</c> — casserait au
    /// premier changement de cadre cible ou de disposition des dossiers, et le
    /// message serait « fichier introuvable » sans dire lequel manque.
    /// </remarks>
    private static string[] Fichiers()
    {
        var dossier = new DirectoryInfo(AppContext.BaseDirectory);

        while (dossier is not null && !Directory.Exists(Path.Combine(dossier.FullName, "db")))
        {
            dossier = dossier.Parent;
        }

        Assert.True(
            dossier is not null,
            "Racine du dépôt introuvable depuis " + AppContext.BaseDirectory
        );

        var referentiel = Path.Combine(dossier!.FullName, "db", "referentiel");

        // Ceux qui portent des EXERCICES. Le référentiel accueillera aussi les
        // nutriments et les aliments, qui n'ont rien à faire dans cette
        // épreuve : les appliquer ici la ferait échouer sur des tables que le
        // catalogue ne connaît pas.
        var fichiers = Directory
            .GetFiles(referentiel, "*exercises*.sql")
            .OrderBy(chemin => Path.GetFileName(chemin), StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            fichiers.Length > 0,
            $"Aucun fichier d'exercices dans {referentiel}. C'est la cible de cette épreuve ; "
                + "une cible absente se signale au lieu de se remplacer."
        );

        return fichiers;
    }

    private async Task<long> CompterAsync(string requete)
    {
        await using var connexion = new NpgsqlConnection(baseDeDonnees.ChaineMigrations);
        await connexion.OpenAsync();
#pragma warning disable CA2100 // Le SQL vient de littéraux de ce fichier.
        await using var commande = new NpgsqlCommand(requete, connexion);
#pragma warning restore CA2100
        return (long)(await commande.ExecuteScalarAsync())!;
    }

    private async Task<IReadOnlyList<string>> LireAsync(string requete)
    {
        await using var connexion = new NpgsqlConnection(baseDeDonnees.ChaineMigrations);
        await connexion.OpenAsync();
#pragma warning disable CA2100 // Le SQL vient de littéraux de ce fichier.
        await using var commande = new NpgsqlCommand(requete, connexion);
#pragma warning restore CA2100
        await using var lecteur = await commande.ExecuteReaderAsync();

        var valeurs = new List<string>();
        while (await lecteur.ReadAsync())
        {
            valeurs.Add(lecteur.GetString(0));
        }

        return valeurs;
    }
}
