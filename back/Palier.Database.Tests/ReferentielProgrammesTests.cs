using Npgsql;

namespace Palier.Database.Tests;

/// <summary>
/// Les neuf programmes modèles, appliqués sur un moteur RÉEL.
/// </summary>
/// <remarks>
/// <para>
/// <b>Le fichier de référentiel n'est ni compilé, ni analysé, ni exercé par une
/// route.</b> Sans cette épreuve, une faute de syntaxe, un slug d'exercice
/// introuvable ou un mouvement contre-indiqué glissé dans un programme de
/// reprise ne se découvrirait qu'au déploiement — ou jamais.
/// </para>
///
/// <para>
/// Elle applique les exercices AVANT les programmes, comme
/// <c>scripts/referentiel.mjs</c> le fera : un programme référence ses
/// exercices par slug, et sur un catalogue vide il ne poserait rien.
/// </para>
/// </remarks>
[Collection(BaseFixture.Collection)]
public sealed class ReferentielProgrammesTests(BaseFixture baseDeDonnees)
{
    /// <summary>
    /// Les onze programmes FONDAMENTAUX, nommément — les neuf de
    /// <c>docs/14-contenu.md</c> § 2, plus les deux de fréquence basse.
    /// </summary>
    /// <remarks>
    /// La liste est écrite ICI plutôt que comptée : un tableau de neuf lignes
    /// dont deux auraient fusionné donnerait encore le bon compte si l'on
    /// s'était contenté de compter.
    /// </remarks>
    private static readonly string[] _attendus =
    [
        "deux-seances",
        "epaule-menagee",
        "full-body",
        "genou-menage",
        "ppl",
        "reprise",
        "reprise-cervicale",
        "reprise-lombaire",
        "split",
        "une-seance",
        "upper-lower",
    ];

    [Fact]
    public async Task Les_ONZE_programmes_FONDAMENTAUX_sont_tous_la()
    {
        // Les neuf de `docs/14-contenu.md` § 2, plus les deux de fréquence
        // basse — D77. Ils sont nommés un par un parce qu'ils portent la
        // couverture par contrainte et par fréquence : un seuil global ne
        // dirait pas lequel a disparu.
        await AppliquerAsync();

        var slugs = await ListerAsync(
            "select slug from public.programs where is_template = true order by slug"
        );

        foreach (var attendu in _attendus)
        {
            Assert.Contains(attendu, slugs);
        }
    }

    [Fact]
    public async Task Le_referentiel_atteint_la_CIBLE_de_programmes()
    {
        // Les onze fondamentaux plus les programmes issus des méthodes. Le
        // seuil monte AVEC le contenu : le laisser à onze laisserait
        // `04b-programs-methodes.sql` se vider sans que rien ne rougisse.
        await AppliquerAsync();

        var poses = await CompterAsync(
            "select count(*) from public.programs where is_template = true"
        );

        Assert.True(poses >= _cibleDeProgrammes, $"Seulement {poses} programme(s) modèle(s).");
    }

    /// <summary>La cible, qui monte avec le contenu.</summary>
    /// <remarks>
    /// Onze fondamentaux au 24/08/2026, puis quarante-neuf programmes issus des
    /// méthodes — trois tours de composition, vérification et correction, sur
    /// cinquante-neuf composés.
    /// </remarks>
    private const int _cibleDeProgrammes = 55;

    [Fact]
    public async Task AUCUN_programme_de_contrainte_ne_contient_un_mouvement_CONTRE_INDIQUE()
    {
        // L'ÉPREUVE CENTRALE DE CE LOT.
        //
        // Un programme nommé « Genou ménagé » qui proposerait une fente
        // profonde serait le pire défaut possible de ce produit : il ne
        // planterait pas, il ne lèverait rien, et il blesserait quelqu'un qui
        // avait justement choisi ce programme pour éviter cela.
        //
        // Le générateur du fichier fait déjà ce contrôle. Il est REFAIT ici,
        // sur la base, parce que le générateur vit dans un scratchpad et que
        // rien n'oblige le prochain à s'en servir — le fichier `.sql` peut
        // être édité à la main.
        await AppliquerAsync();

        var fautes = await ListerAsync(
            """
            select p.slug || ' → ' || e.slug || ' (contre-indiqué : ' ||
                   array_to_string(e.contraindicated_for, ', ') || ')'
              from public.programs p
              join public.program_days d      on d.program_id = p.id
              join public.program_exercises x on x.program_day_id = d.id
              join public.exercises e         on e.id = x.exercise_id
             where p.is_template = true
               and p.targets_constraint is not null
               and p.targets_constraint = any(e.contraindicated_for)
             order by 1
            """
        );

        Assert.True(
            fautes.Length == 0,
            "Un programme ménage une contrainte tout en contenant un mouvement "
                + "contre-indiqué pour elle :\n  " + string.Join("\n  ", fautes)
        );
    }

    [Fact]
    public async Task Les_QUATRE_contraintes_du_document_ont_chacune_leur_programme()
    {
        // `docs/14-contenu.md` § 2 en nomme quatre. Compter les programmes ne
        // dirait pas laquelle manque le jour où l'un d'eux disparaît.
        await AppliquerAsync();

        // DISTINCT, et non la liste brute : plusieurs programmes peuvent
        // ménager la même contrainte, et c'est une richesse, pas un défaut. Ce
        // que l'épreuve garde, c'est qu'aucune des quatre ne reste sans
        // réponse.
        var couvertes = await ListerAsync(
            """
            select distinct targets_constraint from public.programs
             where is_template = true and targets_constraint is not null
             order by 1
            """
        );

        foreach (var region in new[] { "cervicale", "epaule", "genou", "lombaire" })
        {
            Assert.Contains(region, couvertes);
        }
    }

    [Fact]
    public async Task Chaque_programme_porte_ses_seances_ET_ses_exercices()
    {
        // Le fichier lève lui-même si le compte total ne tombe pas juste. Cette
        // épreuve ferme l'autre trou : un programme ENTIER sans séance passerait
        // le compte total si un autre en avait reçu davantage.
        await AppliquerAsync();

        var vides = await ListerAsync(
            """
            select p.slug
              from public.programs p
             where p.is_template = true
               and (
                 not exists (select 1 from public.program_days d where d.program_id = p.id)
                 or exists (
                   select 1 from public.program_days d
                    where d.program_id = p.id
                      and not exists (
                        select 1 from public.program_exercises x
                         where x.program_day_id = d.id)))
             order by 1
            """
        );

        Assert.True(
            vides.Length == 0,
            "Ces programmes ont une séance vide, ou aucune séance : " + string.Join(", ", vides)
        );
    }

    [Fact]
    public async Task Les_positions_sont_CONTIGUES_a_partir_de_1()
    {
        // L'unicité est posée au schéma ; la CONTIGUÏTÉ ne l'est pas, et ne
        // peut pas l'être par une contrainte. Une séance 1, 2 puis 4 laisserait
        // un trou dans l'écran sans que rien ne refuse.
        await AppliquerAsync();

        var trous = await ListerAsync(
            """
            select p.slug || ' — séances ' || array_to_string(array_agg(d.position order by d.position), ', ')
              from public.programs p
              join public.program_days d on d.program_id = p.id
             where p.is_template = true
             group by p.id, p.slug
            having max(d.position) <> count(*) or min(d.position) <> 1
             order by 1
            """
        );

        Assert.True(trous.Length == 0, "Positions non contiguës :\n  " + string.Join("\n  ", trous));
    }

    [Fact]
    public async Task Le_moteur_REFUSE_un_modele_INCOMPLET()
    {
        // `ck_programs_modele_complet` — D70 transposé. Provoquée, pas
        // supposée : une contrainte dont on n'a jamais vu le rouge ne protège
        // rien.
        await AppliquerAsync();

        var faute = await RefuseAsync(
            """
            insert into public.programs (slug, is_template, owner_id, name_fr)
            values ('modele-sans-note', true, null, 'Modèle incomplet')
            """
        );

        Assert.Contains("ck_programs_modele_complet", faute, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Le_moteur_REFUSE_un_modele_QUI_A_UN_PROPRIETAIRE()
    {
        // La première branche de `ck_programs_proprietaire` — D72. Un modèle
        // possédé serait invisible aux autres utilisateurs : la politique
        // `modeles_publics` les montre, celle du propriétaire les cache.
        await AppliquerAsync();

        var faute = await RefuseAsync(
            """
            insert into public.programs
              (slug, is_template, owner_id, name_fr, name_en, description_fr, description_en,
               notes_fr, notes_en, frequency_min, frequency_max)
            values ('modele-possede', true, gen_random_uuid(), 'x', 'x', 'x', 'x', 'x', 'x', 3, 3)
            """
        );

        Assert.Contains("ck_programs_proprietaire", faute, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Le_moteur_REFUSE_un_programme_personnel_SANS_proprietaire()
    {
        // L'AUTRE branche de la même contrainte. Sans cette épreuve, une
        // contrainte qui n'en vérifierait qu'une passerait pour complète —
        // « une branche jamais franchie est une branche qui ment ».
        //
        // Un programme personnel sans propriétaire serait pire qu'orphelin : la
        // politique `proprietaire` compare `owner_id` à l'identité de
        // l'appelant, et `null` ne vaut jamais rien. Il deviendrait invisible à
        // tous, y compris à celui qui l'a écrit.
        await AppliquerAsync();

        var faute = await RefuseAsync(
            "insert into public.programs (is_template, owner_id, name_fr) "
                + "values (false, null, 'Programme orphelin')"
        );

        Assert.Contains("ck_programs_proprietaire", faute, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Le_moteur_REFUSE_une_contrainte_visee_HORS_LISTE()
    {
        await AppliquerAsync();

        var faute = await RefuseAsync(
            """
            insert into public.programs
              (slug, is_template, owner_id, name_fr, name_en, description_fr, description_en,
               notes_fr, notes_en, frequency_min, frequency_max, targets_constraint)
            values ('modele-region-inventee', true, null, 'x', 'x', 'x', 'x', 'x', 'x', 3, 3, 'coude')
            """
        );

        Assert.Contains("ck_programs_targets_constraint", faute, StringComparison.Ordinal);
    }

    [Fact]
    public async Task La_cle_de_recherche_IGNORE_LES_ACCENTS()
    {
        // D73. Le clavier d'un téléphone ne propose pas spontanément les
        // accents : sans cette normalisation, `developpe couche` ne trouverait
        // rien, et la construction d'un programme par saisie de noms serait
        // inutilisable là où elle sert le plus.
        await AppliquerAsync();

        var trouves = await ListerAsync(
            "select name_fr from public.exercises "
                + "where search_key like '%DEVELOPPE COUCHE%' and is_custom = false "
                + "order by name_fr limit 3"
        );

        Assert.NotEmpty(trouves);
        Assert.Contains(trouves, n => n.Contains('é', StringComparison.Ordinal));
    }

    [Fact]
    public async Task La_cle_de_recherche_COUVRE_LES_DEUX_LANGUES()
    {
        // Le produit est bilingue dès la première ligne (`11-qualite.md`). Une
        // recherche qui ne verrait que le français laisserait un utilisateur
        // anglophone devant un catalogue muet.
        await AppliquerAsync();

        var trouves = await ListerAsync(
            "select name_fr from public.exercises "
                + "where search_key like '%BACK SQUAT%' and is_custom = false"
        );

        Assert.NotEmpty(trouves);
    }

    [Fact]
    public async Task Les_STRUCTURES_du_document_sont_toutes_representees()
    {
        // `docs/05-entrainement.md` § 1 associe une structure à chaque
        // fréquence, de 3 à 6 séances. Un catalogue de programmes qui n'en
        // couvrirait que deux laisserait sans réponse celui qui déclare les
        // autres.
        await AppliquerAsync();

        // D'UNE À SEPT, et les deux bornes ont été payées.
        //
        // L'épreuve s'arrêtait à six : mesuré le 24/08/2026, aucun programme ne
        // convenait à sept séances alors que l'onboarding propose cette
        // fréquence — un cul-de-sac que rien ne signalait. Un seuil qui
        // s'arrête avant la promesse ne garde rien.
        //
        // Et elle commençait à trois, parce que les documents commençaient là.
        // Quelqu'un qui ne tient qu'une ou deux séances existe, et le renvoyer
        // dehors contredit la promesse du produit — D77.
        foreach (var seances in new[] { 1, 2, 3, 4, 5, 6, 7 })
        {
            var compte = await CompterAsync(
                "select count(*) from public.programs where is_template = true "
                    + $"and {seances} between frequency_min and frequency_max"
            );

            Assert.True(
                compte >= 1,
                $"Aucun programme modèle ne convient à {seances} séances par semaine."
            );
        }
    }

    // ================================================================
    // Le harnais
    // ================================================================

    /// <summary>
    /// Applique les exercices PUIS les programmes, sous
    /// <c>palier_migrations</c>.
    /// </summary>
    private async Task AppliquerAsync()
    {
        await using var connexion = new NpgsqlConnection(baseDeDonnees.ChaineMigrations);
        await connexion.OpenAsync();

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
    /// Les exercices d'abord, les programmes ensuite — l'ordre des préfixes,
    /// qui est aussi celui de la dépendance.
    /// </summary>
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

        var fichiers = Directory
            .GetFiles(referentiel, "*.sql")
            .Where(chemin =>
                Path.GetFileName(chemin).Contains("exercises", StringComparison.Ordinal)
                || Path.GetFileName(chemin).Contains("programs", StringComparison.Ordinal)
            )
            .OrderBy(chemin => Path.GetFileName(chemin), StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            fichiers.Any(c => Path.GetFileName(c).Contains("programs", StringComparison.Ordinal)),
            $"Aucun fichier de programmes dans {referentiel}. C'est la cible de cette épreuve ; "
                + "une cible absente se signale au lieu de se remplacer."
        );

        return fichiers;
    }

    private async Task<long> CompterAsync(string requete)
    {
        await using var connexion = new NpgsqlConnection(baseDeDonnees.ChaineMigrations);
        await connexion.OpenAsync();
#pragma warning disable CA2100 // Requêtes littérales de l'épreuve, sans entrée extérieure.
        await using var commande = new NpgsqlCommand(requete, connexion);
#pragma warning restore CA2100
        return (long)(await commande.ExecuteScalarAsync())!;
    }

    private async Task<string[]> ListerAsync(string requete)
    {
        await using var connexion = new NpgsqlConnection(baseDeDonnees.ChaineMigrations);
        await connexion.OpenAsync();
#pragma warning disable CA2100 // Requêtes littérales de l'épreuve, sans entrée extérieure.
        await using var commande = new NpgsqlCommand(requete, connexion);
#pragma warning restore CA2100
        await using var lecteur = await commande.ExecuteReaderAsync();

        var valeurs = new List<string>();
        while (await lecteur.ReadAsync())
        {
            valeurs.Add(lecteur.GetString(0));
        }

        return [.. valeurs];
    }

    /// <summary>
    /// Exécute une écriture qui DOIT être refusée, et rend le message du
    /// moteur.
    /// </summary>
    /// <remarks>
    /// Deux assertions valent mieux qu'une : l'épreuve appelante vérifie le nom
    /// de la contrainte, pas seulement qu'une exception est survenue. Une
    /// colonne mal orthographiée lèverait aussi.
    /// </remarks>
    private async Task<string> RefuseAsync(string ecriture)
    {
        await using var connexion = new NpgsqlConnection(baseDeDonnees.ChaineMigrations);
        await connexion.OpenAsync();
#pragma warning disable CA2100 // Écriture littérale de l'épreuve, sans entrée extérieure.
        await using var commande = new NpgsqlCommand(ecriture, connexion);
#pragma warning restore CA2100

        var faute = await Assert.ThrowsAsync<PostgresException>(
            () => commande.ExecuteNonQueryAsync()
        );

        return faute.ConstraintName ?? faute.Message;
    }
}
