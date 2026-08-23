using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;
using Palier.Infrastructure.Courrier;

namespace Palier.Database.Tests;

/// <summary>
/// L'envoi d'emails — D60. Le fournisseur est OVHcloud, et le code ne le sait
/// pas : hôte, port, identifiants et expéditeur sont de la configuration.
///
/// Aucun serveur SMTP n'est joint ici. Le transport est remplacé par un objet
/// qui retient ce qu'on lui donne : ce qui se juge est le MESSAGE composé, pas
/// la capacité de MailKit à ouvrir une connexion.
/// </summary>
public sealed class CourrierTests
{
    // ================================================================
    // Épreuves 1 à 4 — chaque réglage absent refuse en le NOMMANT
    // ================================================================

    [Theory]
    [InlineData("SMTP_HOST")]
    [InlineData("SMTP_PORT")]
    [InlineData("SMTP_FROM")]
    [InlineData("APP_URL")]
    public void Chaque_reglage_ABSENT_refuse_le_demarrage_en_le_NOMMANT(string absent)
    {
        var faute = Assert.Throws<InvalidOperationException>(
            () => ReglagesDuCourrier.Depuis(ConfigurationSans(absent))
        );

        Assert.Contains(absent, faute.Message, StringComparison.Ordinal);
        Assert.Contains(".env.example", faute.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Un_port_NON_NUMERIQUE_refuse_en_le_disant()
    {
        var faute = Assert.Throws<InvalidOperationException>(
            () => ReglagesDuCourrier.Depuis(ConfigurationAvec("SMTP_PORT", "cinq-cent-quatre-vingt-sept"))
        );

        Assert.Contains("SMTP_PORT", faute.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Les_reglages_COMPLETS_construisent_l_objet()
    {
        // La borne. Sans elle, un refus systématique rendrait les cinq épreuves
        // ci-dessus vertes sans rien démontrer.
        var reglages = ReglagesDuCourrier.Depuis(ConfigurationComplete());

        Assert.Equal("ssl0.ovh.net", reglages.Hote);
        Assert.Equal(587, reglages.Port);
    }

    [Fact]
    public void Un_identifiant_ABSENT_est_permis_le_relais_peut_etre_ouvert()
    {
        // SMTP_USER et SMTP_PASSWORD sont facultatifs : un relais interne ne
        // demande pas toujours d'authentification. Les exiger empêcherait un
        // déploiement parfaitement légitime.
        var sansIdentifiants = ConfigurationComplete();
        sansIdentifiants["SMTP_USER"] = null;
        sansIdentifiants["SMTP_PASSWORD"] = null;

        var reglages = ReglagesDuCourrier.Depuis(sansIdentifiants);

        Assert.Null(reglages.Utilisateur);
    }

    // ================================================================
    // Épreuves 6 bis — le chiffrement est décidé par l'HÔTE
    // ================================================================

    [Theory]
    [InlineData("localhost")]
    [InlineData("127.0.0.1")]
    [InlineData("::1")]
    public void Sur_la_BOUCLE_LOCALE_le_chiffrement_n_est_pas_exige(string hote)
    {
        // Ce n'est pas une commodité : le chiffrement protège d'un
        // intermédiaire sur le réseau, et il n'y en a pas entre un processus et
        // un conteneur qui n'écoute que sur 127.0.0.1.
        var reglages = ReglagesDuCourrier.Depuis(ConfigurationAvec("SMTP_HOST", hote));

        Assert.Equal(SecureSocketOptions.None, reglages.Chiffrement);
    }

    [Theory]
    [InlineData("ssl0.ovh.net")]
    [InlineData("smtp.exemple.test")]
    [InlineData("192.0.2.44")]
    public void AILLEURS_le_chiffrement_est_EXIGE_et_non_opportuniste(string hote)
    {
        // `StartTlsWhenAvailable` négocierait en clair si le serveur ne
        // proposait pas l'extension — un attaquant en position d'intermédiaire
        // n'aurait qu'à retirer l'annonce pour lire les liens de vérification
        // de tous les comptes créés. La valeur exigeante est la seule sûre.
        var reglages = ReglagesDuCourrier.Depuis(ConfigurationAvec("SMTP_HOST", hote));

        Assert.Equal(SecureSocketOptions.StartTls, reglages.Chiffrement);
        Assert.NotEqual(SecureSocketOptions.StartTlsWhenAvailable, reglages.Chiffrement);
    }

    // ================================================================
    // Épreuve 7 — le message part en TEXTE et en HTML
    // ================================================================

    [Fact]
    public async Task Le_message_part_en_TEXTE_et_en_HTML()
    {
        // Un courriel qui n'a que du HTML est illisible pour un lecteur en mode
        // texte, et les filtres anti-indésirables le notent.
        using var transport = new TransportFactice();

        await Envoyeur(transport)
            .EnvoyerAsync(Courriel.Verification, "destinataire@exemple.test", "https://x/verifier");

        var parti = Assert.Single(transport.Messages);
        Assert.False(string.IsNullOrWhiteSpace(parti.Texte), "le corps TEXTE manque");
        Assert.False(string.IsNullOrWhiteSpace(parti.Html), "le corps HTML manque");
        Assert.Contains("<p>", parti.Html, StringComparison.Ordinal);
    }

    // ================================================================
    // Épreuve 8 — le lien est dans le corps, et il est bien celui-là
    // ================================================================

    [Fact]
    public async Task Le_LIEN_recu_est_celui_qui_part()
    {
        const string lien = "https://palier.test/verifier?code=abc";
        using var transport = new TransportFactice();

        await Envoyeur(transport)
            .EnvoyerAsync(Courriel.Verification, "destinataire@exemple.test", lien);

        var parti = Assert.Single(transport.Messages);
        Assert.Contains(lien, parti.Texte, StringComparison.Ordinal);
        Assert.Contains(lien, parti.Html, StringComparison.Ordinal);
    }

    // ================================================================
    // Épreuve 9 — celle qui protège : rien de personnel au journal
    // ================================================================

    [Fact]
    public async Task Le_journal_ne_porte_NI_adresse_NI_lien()
    {
        // `01-conformite.md` § 4. Une adresse est une donnée personnelle ; un
        // lien de vérification est un secret à usage unique. Un journal qui les
        // recopie transforme une fuite de journal en prise de comptes.
        const string adresse = "victime@exemple.test";
        const string lien = "https://palier.test/verifier?code=secret-a-usage-unique";
        var journal = new JournalFactice();
        using var transport = new TransportFactice();

        await Envoyeur(transport, journal).EnvoyerAsync(Courriel.Verification, adresse, lien);

        Assert.DoesNotContain(adresse, journal.Tout, StringComparison.Ordinal);
        Assert.DoesNotContain(lien, journal.Tout, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-a-usage-unique", journal.Tout, StringComparison.Ordinal);
    }

    // ================================================================
    // Épreuve 10 — plusieurs envois de suite ne s'écrasent pas
    // ================================================================

    [Fact]
    public async Task Trois_courriels_de_suite_partent_tous()
    {
        using var transport = new TransportFactice();
        var envoyeur = Envoyeur(transport);

        await envoyeur.EnvoyerAsync(Courriel.Verification, "a@exemple.test", "https://x/1");
        await envoyeur.EnvoyerAsync(Courriel.Bienvenue, "a@exemple.test", "https://x/2");
        await envoyeur.EnvoyerAsync(Courriel.Suppression, "a@exemple.test", "https://x/3");

        Assert.Equal(3, transport.Messages.Count);
    }

    // ================================================================
    // Épreuve 11 — un envoi qui échoue ne ressemble PAS à un envoi
    // ================================================================

    [Fact]
    public async Task Un_envoi_qui_ECHOUE_leve_plutot_que_de_se_taire()
    {
        // Le mode de défaillance le plus coûteux serait le silence : un
        // utilisateur qui n'a jamais reçu son courriel et une API qui croit
        // l'avoir envoyé.
        using var transport = new TransportFactice { Echoue = true };

        await Assert.ThrowsAnyAsync<Exception>(
            () => Envoyeur(transport)
                .EnvoyerAsync(Courriel.Verification, "a@exemple.test", "https://x/1")
        );
    }

    // ================================================================
    // Épreuve 12 — chaque gabarit existe dans les DEUX langues
    // ================================================================

    [Theory]
    [InlineData("fr")]
    [InlineData("en")]
    public void Chaque_gabarit_existe_dans_les_DEUX_langues(string langue)
    {
        foreach (var courriel in Enum.GetValues<Courriel>())
        {
            var gabarit = Gabarits.Pour(courriel, langue);

            Assert.False(
                string.IsNullOrWhiteSpace(gabarit.Sujet),
                $"le sujet de {courriel} manque en « {langue} »"
            );
            Assert.False(
                string.IsNullOrWhiteSpace(gabarit.Texte),
                $"le corps texte de {courriel} manque en « {langue} »"
            );
            Assert.False(
                string.IsNullOrWhiteSpace(gabarit.Html),
                $"le corps HTML de {courriel} manque en « {langue} »"
            );
        }
    }

    [Fact]
    public void Une_langue_INCONNUE_retombe_sur_le_francais()
    {
        // Le produit est français d'abord. Lever sur une langue inconnue
        // empêcherait un courriel de partir pour une préférence mal renseignée.
        Assert.Equal(
            Gabarits.Pour(Courriel.Verification, "fr").Sujet,
            Gabarits.Pour(Courriel.Verification, "br").Sujet
        );
    }

    // ================================================================

    private static EnvoyeurSmtp Envoyeur(
        TransportFactice transport,
        JournalFactice? journal = null
    ) =>
        new(
            ReglagesDuCourrier.Depuis(ConfigurationComplete()),
            journal ?? new JournalFactice(),
            () => transport
        );

    private static IConfigurationRoot ConfigurationComplete() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["SMTP_HOST"] = "ssl0.ovh.net",
                    ["SMTP_PORT"] = "587",
                    ["SMTP_FROM"] = "palier@exemple.test",
                    ["SMTP_USER"] = "palier@exemple.test",
                    ["SMTP_PASSWORD"] = "secret",
                    ["APP_URL"] = "https://palier.test",
                }
            )
            .Build();

    private static IConfigurationRoot ConfigurationSans(string absent)
    {
        var configuration = ConfigurationComplete();
        configuration[absent] = null;
        return configuration;
    }

    private static IConfigurationRoot ConfigurationAvec(string cle, string valeur)
    {
        var configuration = ConfigurationComplete();
        configuration[cle] = valeur;
        return configuration;
    }
}

/// <summary>Ce qu'une épreuve a besoin de savoir d'un message parti.</summary>
internal sealed record MessageParti(string? Sujet, string? Texte, string? Html);

/// <summary>
/// Il CAPTURE ce qu'il faut juger, il ne garde pas le message.
///
/// L'envoyeur dispose le <c>MimeMessage</c> dès la sortie — c'est correct, il
/// est parti. Retenir la référence donnait une <c>ObjectDisposedException</c> à
/// la lecture : l'épreuve jugeait un objet déjà libéré.
/// </summary>
internal sealed class TransportFactice : ITransportDeCourrier
{
    public List<MessageParti> Messages { get; } = [];

    public bool Echoue { get; init; }

    public Task EnvoyerAsync(MimeMessage message, CancellationToken jeton)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (Echoue)
        {
            return Task.FromException(new InvalidOperationException("relais injoignable"));
        }

        Messages.Add(new MessageParti(message.Subject, message.TextBody, message.HtmlBody));
        return Task.CompletedTask;
    }

    public void Dispose() { }
}

/// <summary>Il concatène tout ce qu'on lui écrit, pour qu'on puisse le fouiller.</summary>
internal sealed class JournalFactice : ILogger<EnvoyeurSmtp>
{
    private readonly System.Text.StringBuilder _tout = new();

    public string Tout => _tout.ToString();

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
        _tout.AppendLine(formatter(state, exception));
    }
}
