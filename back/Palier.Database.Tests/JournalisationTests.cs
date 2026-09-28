using System.Data.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Palier.Api;
using Palier.Infrastructure;

namespace Palier.Database.Tests;

/// <summary>
/// « Aucune donnée de santé dans les logs applicatifs » — `01-conformite.md`
/// § 4. La règle existait ; RIEN NE L'OBSERVAIT. Mesuré le 20/08/2026 :
/// <c>grep -rn SensitiveDataLogging back/ --include=*.cs</c> rendait zéro.
///
/// Ces épreuves appartiennent à CE lot et non au lot 9, parce que c'est CE lot
/// qui fabrique le chemin d'erreur : le mécanisme d'identité de D36 fait de tout
/// défaut d'identité une <c>PostgresException</c>, portée par l'événement
/// <c>CommandError</c> d'EF Core — qui journalise le <c>CommandText</c>, sur des
/// tables nommées <c>body_weight</c>, <c>intake_entries</c>,
/// <c>exercise_feedback</c>.
///
/// L'épreuve n'inspecte AUCUN fichier : elle provoque un vrai échec SQL sur le
/// moteur, capture ce qu'EF Core a réellement écrit, et cherche les valeurs
/// dedans.
/// </summary>
[Collection(BaseFixture.Collection)]
public sealed class JournalisationTests(BaseFixture baseDeDonnees)
{
    /// <summary>La valeur de poids employée. Distinctive, pour qu'aucune autre ligne ne la produise par hasard.</summary>
    private const decimal _poids = 87.6543m;

    [Fact]
    public async Task Aucune_donnee_de_sante_ne_part_au_journal_quand_une_commande_SQL_echoue()
    {
        var (capture, proprietaire) = await ProvoquerUnEchecSqlAsync(isSensible: false);
        var journal = string.Join("\n", capture);

        // (a) La cible existe : la ligne qui PORTERAIT les valeurs a bien été
        // capturée. Sans cette assertion, un capteur qui n'attrape rien rendrait
        // les deux suivantes vertes en n'ayant rien lu — la forme la plus banale
        // du faux vert.
        Assert.Contains("insert into public.body_weight", journal, StringComparison.Ordinal);

        // (b) et (c) Ni le poids ni l'identifiant de l'utilisateur n'y sont.
        Assert.DoesNotContain(
            _poids.ToString(System.Globalization.CultureInfo.InvariantCulture),
            journal,
            StringComparison.Ordinal
        );
        Assert.DoesNotContain(
            proprietaire.ToString(),
            journal,
            StringComparison.OrdinalIgnoreCase
        );
    }

    [Fact]
    public async Task ÉPREUVE_INVERSÉE_avec_EnableSensitiveDataLogging_le_poids_et_l_identifiant_FUITENT()
    {
        // ÉPREUVE INVERSÉE, ET C'EST VOULU — annotée pour que personne ne la
        // « corrige ». Elle mesure la SENSIBILITÉ de l'instrument : sans elle,
        // l'épreuve ci-dessus pourrait être verte parce qu'EF Core ne journalise
        // jamais les paramètres, et non parce que la configuration l'en empêche.
        // Une garde dont on n'a jamais vu l'objet passer ne garde rien.
        var (capture, proprietaire) = await ProvoquerUnEchecSqlAsync(isSensible: true);
        var journal = string.Join("\n", capture);

        Assert.Contains(
            _poids.ToString(System.Globalization.CultureInfo.InvariantCulture),
            journal,
            StringComparison.Ordinal
        );
        Assert.Contains(proprietaire.ToString(), journal, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void La_configuration_de_PRODUCTION_n_active_pas_la_journalisation_sensible()
    {
        // La configuration RÉELLE de production est CONSTRUITE, pas relue. Un
        // appel conditionné à `IsDevelopment()` qui fuit en production est
        // exactement ce qu'une lecture de fichier ne peut pas attraper : le
        // fichier est juste, c'est la condition qui est fausse.
        var constructeur = WebApplication.CreateBuilder(
            new WebApplicationOptions
            {
                EnvironmentName = Environments.Production,
                ContentRootPath = AppContext.BaseDirectory,
                Args = [],
            }
        );
        constructeur.Configuration.AddInMemoryCollection(
            new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["ConnectionStrings:Palier"] = baseDeDonnees.ChaineApp,
                // Lot 4 : la composition exige aussi le chemin d'authentification.
                ["ConnectionStrings:PalierAuth"] = baseDeDonnees.ChaineAuth,
                // ... et une clé de signature d'au moins 32 octets.
                ["JWT_SIGNING_KEY"] = "cle-de-signature-des-epreuves-du-lot-quatre",
                // D60 : la composition refuse sans le courrier.
                ["SMTP_HOST"] = "relais.invalid",
                ["SMTP_PORT"] = "587",
                ["SMTP_FROM"] = "palier@exemple.test",
                ["APP_URL"] = "https://palier.test",
            }
        );

        Composition.Composer(constructeur);
        using var hote = constructeur.Build();

        // (a) On construit bien la PRODUCTION. Sans cette assertion, l'épreuve
        // serait verte sur la configuration de développement.
        Assert.Equal(Environments.Production, constructeur.Environment.EnvironmentName);

        using var portee = hote.Services.CreateScope();
        var options = portee.ServiceProvider.GetRequiredService<DbContextOptions<PalierDbContext>>();
        var noyau = options.FindExtension<CoreOptionsExtension>();

        // (b) L'extension est bien là — sinon `IsSensitiveDataLoggingEnabled`
        // serait lu sur une option qui n'existe pas.
        Assert.NotNull(noyau);

        Assert.False(
            noyau.IsSensitiveDataLoggingEnabled,
            "La configuration de PRODUCTION active `EnableSensitiveDataLogging`. EF Core "
                + "journalise alors la VALEUR de chaque paramètre à chaque erreur SQL — donc un "
                + "poids, un apport, un identifiant d'utilisateur. `01-conformite.md` § 4 "
                + "l'interdit. Un appel conditionné à `IsDevelopment()` ne suffit pas : c'est la "
                + "condition qui casse, pas l'appel."
        );
    }

    /// <summary>
    /// Provoque un ÉCHEC SQL RÉEL sous le rôle applicatif : l'insertion dans
    /// <c>body_weight</c> sans identité déclenche la politique, qui appelle
    /// l'accesseur qui lève. C'est le chemin exact que D36 fabrique.
    /// </summary>
    private async Task<(IReadOnlyList<string> Capture, Guid Proprietaire)> ProvoquerUnEchecSqlAsync(
        bool isSensible
    )
    {
        using var capture = new JournalDeCapture();
        using var fabrique = LoggerFactory.Create(b =>
        {
            b.AddProvider(capture);
            b.SetMinimumLevel(LogLevel.Debug);
        });

        var options = new DbContextOptionsBuilder<PalierDbContext>().UseNpgsql(
            baseDeDonnees.ChaineApp
        );
        options = options.UseLoggerFactory(fabrique);
        if (isSensible)
        {
            options = options.EnableSensitiveDataLogging();
        }

        var proprietaire = Guid.NewGuid();
        var identifiant = Guid.NewGuid();
        var mesure = new DateOnly(2026, 8, 20);

        await using (var contexte = new PalierDbContext(options.Options))
        {
            // Le SQL est écrit en minuscules et sans mise en forme : l'assertion
            // (a) de l'épreuve cherche cette chaîne exacte dans le journal.
            await Assert.ThrowsAnyAsync<DbException>(() =>
                contexte.Database.ExecuteSqlAsync(
                    $"insert into public.body_weight (id, owner_id, measured_on, weight_kg) values ({identifiant}, {proprietaire}, {mesure}, {_poids})"
                )
            );
        }

        return (capture.Lignes, proprietaire);
    }
}

/// <summary>
/// Un <c>ILoggerProvider</c> qui garde TOUT ce qui passe, message rendu et
/// exception comprise. Rien n'est filtré ici : filtrer au capteur reviendrait à
/// décider d'avance ce que l'épreuve a le droit de trouver.
/// </summary>
internal sealed class JournalDeCapture : ILoggerProvider
{
    private readonly List<string> _lignes = [];

    public IReadOnlyList<string> Lignes
    {
        get
        {
            lock (_lignes)
            {
                return [.. _lignes];
            }
        }
    }

    public ILogger CreateLogger(string categoryName) => new Enregistreur(this);

    public void Dispose()
    {
        // Rien à libérer : la capture est une liste en mémoire.
    }

    private void Ajouter(string ligne)
    {
        lock (_lignes)
        {
            _lignes.Add(ligne);
        }
    }

    private sealed class Enregistreur(JournalDeCapture parent) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter
        )
        {
            ArgumentNullException.ThrowIfNull(formatter);
            parent.Ajouter(formatter(state, exception) + "\n" + exception);
        }
    }
}
