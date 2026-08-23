using System.Globalization;
using MailKit.Net.Smtp;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace Palier.Infrastructure.Courrier;

/// <summary>
/// Ce qu'il faut savoir faire pour qu'un message parte. Une abstraction de
/// trois lignes, dont la seule raison d'être est que les épreuves puissent
/// juger le MESSAGE composé sans ouvrir de connexion.
/// </summary>
public interface ITransportDeCourrier : IDisposable
{
    public Task EnvoyerAsync(MimeMessage message, CancellationToken jeton);
}

/// <summary>
/// L'envoi réel — D60, et ce que l'exigence 2 de <c>docs/09-comptes.md</c> § 1
/// attendait depuis le lot 4.
/// </summary>
/// <remarks>
/// <para>
/// <b>Il n'implémente PAS <c>IEmailSender&lt;TUser&gt;</c>.</b> Cette interface
/// vit dans l'assembly ASP.NET Core, et cette couche n'a pas à connaître le
/// framework web pour composer un message. L'adaptateur qui fait le pont vit
/// dans <c>Palier.Api</c> — trois lignes de délégation, à leur place.
/// </para>
///
/// <para>
/// <b>Rien de personnel ne part au journal</b> — <c>01-conformite.md</c> § 4.
/// Ni l'adresse, qui est une donnée personnelle ; ni le lien, qui porte un
/// secret à usage unique. Un journal qui les recopie transforme une fuite de
/// journal en prise de comptes.
/// </para>
///
/// <para>
/// <b>Un envoi qui échoue LÈVE.</b> Le mode de défaillance le plus coûteux
/// serait le silence : un utilisateur qui n'a jamais reçu son courriel et une
/// API qui croit l'avoir envoyé.
/// </para>
/// </remarks>
public sealed partial class EnvoyeurSmtp(
    ReglagesDuCourrier reglages,
    ILogger<EnvoyeurSmtp> journal,
    Func<ITransportDeCourrier>? fabrique = null
)
{
    /// <summary>Envoie un des quatre courriels du lot.</summary>
    public async Task EnvoyerAsync(
        Courriel courriel,
        string destinataire,
        string lienOuCode,
        string? langue = null,
        CancellationToken jeton = default
    )
    {
        var gabarit = Gabarits.Pour(courriel, langue);

        using var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(reglages.Expediteur));
        message.To.Add(MailboxAddress.Parse(destinataire));
        message.Subject = gabarit.Sujet;

        // Texte ET HTML. Un courriel qui n'a que du HTML est illisible pour un
        // lecteur en mode texte, et les filtres anti-indésirables le notent.
        message.Body = new BodyBuilder
        {
            TextBody = string.Format(CultureInfo.InvariantCulture, gabarit.Texte, lienOuCode),
            HtmlBody = string.Format(CultureInfo.InvariantCulture, gabarit.Html, lienOuCode),
        }.ToMessageBody();

        using var transport = (fabrique ?? Reel)();
        await transport.EnvoyerAsync(message, jeton).ConfigureAwait(false);

        // Le TYPE de courriel, et rien d'autre. Pas le destinataire, pas le
        // lien, pas même une adresse tronquée : les huit premiers signes d'une
        // adresse en disent déjà trop.
        JournaliserEnvoi(journal, courriel);
    }

    /// <summary>
    /// Un délégué engendré plutôt qu'un appel direct : CA1848. L'intérêt n'est
    /// pas la performance ici — quelques courriels par jour — mais que le
    /// gabarit du message soit figé à la compilation, donc impossible à
    /// composer par erreur avec une valeur personnelle.
    /// </summary>
    [LoggerMessage(Level = LogLevel.Information, Message = "Courriel {Courriel} envoyé.")]
    private static partial void JournaliserEnvoi(ILogger journal, Courriel courriel);

    private TransportMailKit Reel() => new(reglages);
}

/// <summary>
/// Une connexion par envoi. C'est un choix : garder une connexion ouverte
/// obligerait à la surveiller, à la rouvrir et à sérialiser les envois, pour un
/// produit qui en fait quelques-uns par jour.
/// </summary>
/// <remarks>
/// <b>EXCLU DE LA COUVERTURE, et le motif doit tenir.</b> Ce type n'est que la
/// traduction de <see cref="ITransportDeCourrier" /> en appels MailKit : il
/// ouvre une connexion TCP, négocie STARTTLS et parle SMTP. Le couvrir
/// demanderait un serveur SMTP dans la suite d'épreuves — donc un conteneur de
/// plus, une dépendance de plus, et une épreuve qui mesurerait la conformité de
/// MailKit au protocole plutôt que notre code.
///
/// Ce qui SE juge — le message composé, le refus qui ne se tait pas, le journal
/// qui ne porte rien de personnel — est éprouvé sur l'envoyeur, contre un
/// transport factice. Ce qui reste ici tient en huit lignes sans branche
/// métier.
/// </remarks>
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage(
    Justification = "Traduction directe en appels MailKit : la couvrir exigerait un serveur SMTP."
)]
internal sealed class TransportMailKit(ReglagesDuCourrier reglages) : ITransportDeCourrier
{
    private readonly SmtpClient _client = new();

    public async Task EnvoyerAsync(MimeMessage message, CancellationToken jeton)
    {
        await _client
            .ConnectAsync(reglages.Hote, reglages.Port, reglages.Chiffrement, jeton)
            .ConfigureAwait(false);

        if (reglages.Utilisateur is { } utilisateur)
        {
            await _client
                .AuthenticateAsync(utilisateur, reglages.MotDePasse ?? string.Empty, jeton)
                .ConfigureAwait(false);
        }

        await _client.SendAsync(message, jeton).ConfigureAwait(false);
        await _client.DisconnectAsync(quit: true, jeton).ConfigureAwait(false);
    }

    public void Dispose() => _client.Dispose();
}
