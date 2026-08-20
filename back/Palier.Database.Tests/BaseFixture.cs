using DotNet.Testcontainers.Containers;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;
using Palier.Infrastructure;
using Testcontainers.PostgreSql;

namespace Palier.Database.Tests;

/// <summary>
/// Le moteur réel sur lequel tournent toutes les épreuves de ce projet.
///
/// UN SEUL conteneur pour toute la suite : le démarrage de PostgreSQL coûte
/// plusieurs secondes, et `verify` est déjà la boucle lente du projet.
///
/// RIEN N'EST REDÉCLARÉ ICI. Le tag de l'image, le nom de la base et le compte
/// d'administration sont LUS dans `db/compose.yaml` ; les mots de passe des
/// trois rôles sont LUS dans `db/amorcage/01-roles.sql`. Deux déclarations du
/// même tag divergent — D34 — et la divergence se ferme par une lecture, jamais
/// par une discipline. `tests-harness/db.test.mjs` refuse le dépôt si le tag
/// réapparaît ailleurs que dans le compose.
///
/// Chaque lecture qui échoue LÈVE avec le chemin et le motif : un contrôle qui
/// n'a plus sa cible doit crier, quatrième question du franchissement. Une
/// fixture qui retomberait sur une valeur par défaut rendrait toute la suite
/// verte sur une base qui n'est pas celle du produit.
/// </summary>
public sealed class BaseFixture : IAsyncLifetime
{
    /// <summary>Le nom de la collection xUnit qui partage cette fixture.</summary>
    public const string Collection = "base-postgresql";

    private const string _cheminCompose = "db/compose.yaml";
    private const string _cheminAmorcage = "db/amorcage/01-roles.sql";

    private readonly PostgreSqlContainer _conteneur;

    public BaseFixture()
    {
        Racine = TrouverRacine();
        var compose = LireFichier(_cheminCompose);
        var amorcage = LireFichier(_cheminAmorcage);

        Tag = Extraire(compose, @"^\s*image:\s*([^\s#]+)", _cheminCompose, "le tag de l'image");
        Base = Extraire(compose, @"^\s*POSTGRES_DB:\s*([^\s#]+)", _cheminCompose, "le nom de la base");
        Administrateur = Extraire(
            compose,
            @"^\s*POSTGRES_USER:\s*([^\s#]+)",
            _cheminCompose,
            "le compte d'administration"
        );

        // Les arguments d'initialisation sont LUS eux aussi — D44. Sans cela,
        // les épreuves tourneraient sur la collation par défaut de l'image
        // (`libc`, `en_US.utf8`) pendant que la base locale et l'instance
        // managée seraient en ICU `fr-BE`. Un tri qui diffère entre ce qu'on
        // teste et ce qu'on exploite est exactement la divergence que ce projet
        // a déjà payée quatre fois — et celle-ci ne se manifesterait pas par un
        // test rouge, mais par un ordre de résultats faux en production.
        ArgumentsInitialisation = Extraire(
            compose,
            @"^\s*POSTGRES_INITDB_ARGS:\s*(.+?)\s*$",
            _cheminCompose,
            "les arguments d'initialisation du cluster"
        );

        MotDePasse = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var role in Roles)
        {
            MotDePasse[role] = Extraire(
                amorcage,
                $@"create\s+role\s+{Regex.Escape(role)}\s+login\s+password\s+'([^']+)'",
                _cheminAmorcage,
                $"le mot de passe du rôle {role}"
            );
        }

        // Le tag entre par le CONSTRUCTEUR : `PostgreSqlBuilder()` sans image est
        // obsolète depuis la 4.14 et disparaîtra. Le passer ici garantit qu'aucun
        // tag par défaut du module ne peut se substituer à celui du compose.
        _conteneur = new PostgreSqlBuilder(Tag)
            .WithDatabase(Base)
            .WithUsername(Administrateur)
            .WithEnvironment("POSTGRES_INITDB_ARGS", ArgumentsInitialisation)
            // Le mot de passe du compte d'administration n'est PAS repris du
            // compose : ce conteneur est jetable, il n'écoute sur aucun port
            // fixe, et une valeur tirée au hasard supprime la question de sa
            // présence dans le dépôt.
            .WithPassword(Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture))
            // `db/amorcage/01-roles.sql` est monté dans le répertoire d'amorçage
            // de l'image : l'entrypoint l'exécute sous le compte
            // d'administration, avant que la base accepte la moindre connexion.
            // C'est le SEUL usage de ce compte dans tout le lot.
            .WithResourceMapping(
                new FileInfo(Path.Combine(Racine, _cheminAmorcage)),
                "/docker-entrypoint-initdb.d/"
            )
            .Build();
    }

    /// <summary>Racine du dépôt, trouvée en remontant depuis l'assemblage de test.</summary>
    public string Racine { get; }

    /// <summary>Le tag de l'image PostgreSQL, lu dans <c>db/compose.yaml</c>.</summary>
    public string Tag { get; }

    /// <summary>
    /// Les arguments passés à <c>initdb</c>, lus dans <c>db/compose.yaml</c>.
    /// Ils portent le fournisseur de collation et la locale : les épreuves
    /// doivent trier comme la base locale et comme l'instance managée.
    /// </summary>
    public string ArgumentsInitialisation { get; }

    /// <summary>Le nom de la base, lu dans <c>db/compose.yaml</c>.</summary>
    public string Base { get; }

    /// <summary>Le compte d'administration du conteneur, lu dans <c>db/compose.yaml</c>.</summary>
    public string Administrateur { get; }

    /// <summary>Les trois rôles de D37, dans l'ordre où l'amorçage les crée.</summary>
    public static IReadOnlyList<string> Roles { get; } =
        ["palier_migrations", "palier_app", "palier_sauvegarde"];

    private Dictionary<string, string> MotDePasse { get; }

    /// <summary>
    /// La chaîne de connexion du rôle applicatif. C'est celle que toutes les
    /// épreuves d'isolation utilisent : une suite lancée sous le compte
    /// d'administration passerait intégralement au vert sans rien démontrer.
    /// </summary>
    public string ChaineApp => Chaine("palier_app");

    /// <summary>La chaîne du propriétaire des tables. Migrations et référentiel seulement.</summary>
    public string ChaineMigrations => Chaine("palier_migrations");

    /// <summary>La chaîne du rôle de sauvegarde. Consommée par la tâche 10.</summary>
    public string ChaineSauvegarde => Chaine("palier_sauvegarde");

    /// <summary>La chaîne du compte d'administration du conteneur, pour les épreuves qui doivent le comparer.</summary>
    public string ChaineAdministrateur => _conteneur.GetConnectionString();

    /// <summary>
    /// Construit une chaîne pour un rôle donné. <paramref name="pooling" /> à
    /// faux force UNE connexion physique : c'est ce dont l'épreuve de la portée
    /// transaction a besoin pour observer ce qui survit à un COMMIT.
    /// </summary>
    public string Chaine(string role, bool pooling = true)
    {
        if (!MotDePasse.TryGetValue(role, out var motDePasse))
        {
            throw new InvalidOperationException(
                $"Rôle inconnu « {role} » : {_cheminAmorcage} n'en déclare que "
                    + $"{string.Join(", ", Roles)}."
            );
        }

        var pool = pooling ? "" : ";Pooling=false";
        return $"Host={_conteneur.Hostname};Port={_conteneur.GetMappedPublicPort(5432)};"
            + $"Database={Base};Username={role};Password={motDePasse}{pool}";
    }

    /// <summary>
    /// Une URI de connexion utilisable DEPUIS L'INTÉRIEUR du conteneur : les
    /// outils <c>pg_dump</c> et <c>pg_restore</c> vivent dans l'image, et ils y
    /// voient le serveur sur <c>localhost:5432</c>, jamais sur le port publié.
    ///
    /// La forme URI plutôt que <c>PGPASSWORD</c> : <c>ExecAsync</c> ne transmet
    /// aucune variable d'environnement, et un mot de passe local sans entropie
    /// dans une ligne de commande à l'intérieur d'un conteneur jetable n'ouvre
    /// rien.
    /// </summary>
    public string ConnexionInterne(string role, string? baseDeDonnees = null)
    {
        if (!MotDePasse.TryGetValue(role, out var motDePasse))
        {
            throw new InvalidOperationException(
                $"Rôle inconnu « {role} » : {_cheminAmorcage} n'en déclare que "
                    + $"{string.Join(", ", Roles)}."
            );
        }

        return $"postgresql://{role}:{motDePasse}@localhost:5432/{baseDeDonnees ?? Base}";
    }

    /// <summary>
    /// Exécute une commande DANS le conteneur. C'est le seul moyen d'éprouver
    /// <c>pg_dump</c> et <c>pg_restore</c> sans exiger le client PostgreSQL sur
    /// le poste — or `docs/14-contenu.md` § 7 : « une sauvegarde jamais
    /// restaurée n'est pas une sauvegarde », et une épreuve qu'on ne peut lancer
    /// que sur une machine outillée n'est jamais lancée.
    /// </summary>
    public Task<ExecResult> ExecuterDansLeConteneurAsync(params string[] commande) =>
        _conteneur.ExecAsync(commande);

    public async Task InitializeAsync()
    {
        await _conteneur.StartAsync();

        // La migration est appliquée SOUS `palier_migrations`, jamais sous le
        // compte d'administration : c'est ce qui fait de ce rôle le
        // propriétaire des tables, et donc ce qui rend `force row level
        // security` observable. Une suite qui migrerait sous l'administrateur
        // éprouverait un schéma dont personne n'est propriétaire au sens du
        // produit.
        await using var contexte = Contexte(ChaineMigrations);
        await contexte.Database.MigrateAsync();
    }

    public Task DisposeAsync() => _conteneur.DisposeAsync().AsTask();

    /// <summary>
    /// Un contexte EF sur une chaîne donnée. Sert aux épreuves de schéma et à
    /// la garde applicative ; les épreuves d'isolation, elles, parlent au
    /// moteur en SQL nu, pour qu'aucune couche d'abstraction ne puisse être
    /// soupçonnée d'avoir filtré à la place des politiques.
    /// </summary>
    public static PalierDbContext Contexte(
        string chaine,
        Action<NpgsqlDbContextOptionsBuilder>? npgsql = null
    )
    {
        var options = new DbContextOptionsBuilder<PalierDbContext>()
            .UseNpgsql(chaine, npgsql ?? (_ => { }))
            .Options;
        return new PalierDbContext(options);
    }

    private static string TrouverRacine()
    {
        var dossier = new DirectoryInfo(AppContext.BaseDirectory);
        while (dossier is not null)
        {
            if (File.Exists(Path.Combine(dossier.FullName, _cheminCompose)))
            {
                return dossier.FullName;
            }

            dossier = dossier.Parent;
        }

        throw new InvalidOperationException(
            $"Racine du dépôt introuvable : aucun « {_cheminCompose} » en remontant depuis "
                + $"{AppContext.BaseDirectory}. La fixture ne peut pas deviner le tag de l'image, "
                + "et ne se rabat sur AUCUNE valeur par défaut — une base qui n'est pas celle du "
                + "produit rendrait toute la suite verte pour rien."
        );
    }

    private string LireFichier(string relatif)
    {
        var chemin = Path.Combine(Racine, relatif);
        if (!File.Exists(chemin))
        {
            throw new InvalidOperationException(
                $"Cible manquante : {chemin}. La fixture LIT ses valeurs dans ce fichier au lieu "
                    + "de les redéclarer (D34) ; sans lui elle n'a rien à lire et refuse de partir."
            );
        }

        return File.ReadAllText(chemin);
    }

    private static string Extraire(string contenu, string motif, string fichier, string quoi)
    {
        var trouve = Regex.Match(
            contenu,
            motif,
            RegexOptions.Multiline | RegexOptions.IgnoreCase,
            TimeSpan.FromSeconds(5)
        );
        if (!trouve.Success)
        {
            throw new InvalidOperationException(
                $"{fichier} ne porte pas {quoi} : le motif « {motif} » n'y trouve rien. "
                    + "Le fichier a changé de forme, ou la valeur a été déplacée — dans les deux "
                    + "cas cette fixture doit être corrigée, jamais contournée par une constante."
            );
        }

        return trouve.Groups[1].Value;
    }
}

/// <summary>
/// Rattache toutes les classes d'épreuve au même conteneur. Sans cette
/// collection, xUnit instancierait une fixture — donc un PostgreSQL — par
/// classe de test.
/// </summary>
[CollectionDefinition(BaseFixture.Collection)]
public sealed class CollectionDeBase : ICollectionFixture<BaseFixture>;
