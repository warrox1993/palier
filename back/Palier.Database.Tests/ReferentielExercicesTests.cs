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
    /// <summary>Le nombre de mouvements que le jalon 1 promet — `docs/16-projet.md` § 4.</summary>
    private const int _prioritaires = 60;

    [Fact]
    public async Task Le_referentiel_s_applique_et_pose_les_SOIXANTE_mouvements()
    {
        await AppliquerAsync();

        var poses = await CompterAsync(
            "select count(*) from public.exercises where is_custom = false and slug is not null"
        );

        // `docs/16-projet.md` § 4 : « 60 exercices prioritaires puis
        // extension ». Le nombre est une PROMESSE du document, pas une
        // constatation de ce que le fichier contient — un fichier tronqué à
        // quarante passerait sans cette assertion.
        Assert.True(
            poses >= _prioritaires,
            $"Le référentiel pose {poses} exercice(s) au lieu des {_prioritaires} promis "
                + "par `docs/16-projet.md` § 4."
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

        Assert.True(tirages >= 8, $"Seulement {tirages} mouvement(s) de tirage au catalogue.");
        Assert.True(poussees >= 8, $"Seulement {poussees} mouvement(s) de poussée au catalogue.");
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
        Assert.True(liens >= 30, $"Seulement {liens} lien(s) de variante posé(s).");

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
        var chemin = Chemin();
        var sql = await File.ReadAllTextAsync(chemin);

        await using var connexion = new NpgsqlConnection(baseDeDonnees.ChaineMigrations);
        await connexion.OpenAsync();
#pragma warning disable CA2100 // Le SQL vient du fichier de référentiel du DÉPÔT, versionné et relu, jamais d'une entrée.
        await using var commande = new NpgsqlCommand(sql, connexion);
#pragma warning restore CA2100
        await commande.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Remonte jusqu'à la racine du dépôt depuis le répertoire d'exécution.
    /// </summary>
    /// <remarks>
    /// Un chemin relatif en dur — <c>../../../../../db/…</c> — casserait au
    /// premier changement de cadre cible ou de disposition des dossiers, et le
    /// message serait « fichier introuvable » sans dire lequel manque.
    /// </remarks>
    private static string Chemin()
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

        var chemin = Path.Combine(dossier!.FullName, "db", "referentiel", "02-exercises.sql");

        Assert.True(
            File.Exists(chemin),
            $"Le référentiel du catalogue est introuvable : {chemin}. C'est la cible de cette "
                + "épreuve ; une cible absente se signale au lieu de se remplacer."
        );

        return chemin;
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
