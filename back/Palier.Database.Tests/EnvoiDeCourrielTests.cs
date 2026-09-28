using Microsoft.Extensions.Configuration;
using Palier.Api.Auth;
using Palier.Infrastructure.Courrier;
using Palier.Infrastructure.Identite;

namespace Palier.Database.Tests;

/// <summary>
/// Le pont entre le contrat d'Identity et l'envoyeur — D60.
///
/// Trois lignes de délégation, et elles méritent une épreuve pour une raison
/// précise : c'est ce type qui remplace <c>NoOpEmailSender</c>. S'il déléguait
/// au mauvais courriel, l'utilisateur recevrait un message de réinitialisation
/// pour confirmer son adresse, et rien ne le signalerait.
/// </summary>
public sealed class EnvoiDeCourrielTests
{
    [Fact]
    public async Task Le_lien_de_CONFIRMATION_part_en_courriel_de_verification()
    {
        using var transport = new TransportFactice();
        var envoi = Envoi(transport);

        await envoi.SendConfirmationLinkAsync(new Utilisateur(), "a@exemple.test", "https://x/1");

        Assert.Contains(
            "confirmez votre adresse",
            Assert.Single(transport.Messages).Sujet,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public async Task Le_lien_de_REINITIALISATION_part_en_courriel_de_mot_de_passe()
    {
        using var transport = new TransportFactice();
        var envoi = Envoi(transport);

        await envoi.SendPasswordResetLinkAsync(new Utilisateur(), "a@exemple.test", "https://x/2");

        Assert.Contains(
            "mot de passe",
            Assert.Single(transport.Messages).Sujet,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public async Task Le_CODE_de_réinitialisation_part_aussi()
    {
        using var transport = new TransportFactice();
        var envoi = Envoi(transport);

        await envoi.SendPasswordResetCodeAsync(new Utilisateur(), "a@exemple.test", "123456");

        Assert.Contains("123456", Assert.Single(transport.Messages).Texte, StringComparison.Ordinal);
    }

    private static EnvoiDeCourriel Envoi(TransportFactice transport) =>
        new EnvoiDeCourriel(
            new EnvoyeurSmtp(
                ReglagesDuCourrier.Depuis(
                    new ConfigurationBuilder()
                        .AddInMemoryCollection(
                            new Dictionary<string, string?>
                            {
                                ["SMTP_HOST"] = "relais.invalid",
                                ["SMTP_PORT"] = "587",
                                ["SMTP_FROM"] = "palier@exemple.test",
                                ["APP_URL"] = "https://palier.test",
                            }
                        )
                        .Build()
                ),
                new JournalFactice(),
                () => transport
            )
        );
}
