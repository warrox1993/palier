using System.Globalization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Palier.Api.Auth;
using Palier.Application.Pipeline;
using Palier.Infrastructure.Courrier;
using Palier.Infrastructure.Identite;

namespace Palier.Database.Tests;

/// <summary>
/// La suppression de compte — article 17, et ce que le lot 4 n'avait livré
/// qu'à moitié : la cascade des sessions existait, le parcours non.
///
/// <b>Aucun délai de grâce</b>, et c'est une décision : l'article 17 demande
/// l'effacement, pas l'archivage. Un compte « supprimé dans 30 jours »
/// laisserait une donnée de santé survivre à la demande de son propriétaire.
/// </summary>
[Collection(BaseFixture.Collection)]
public sealed class SuppressionTests(BaseFixture baseDeDonnees)
{
    private const string _motDePasse = "brouette-hivernale-38-oscille";

    // ================================================================
    // Épreuve 1 — la demande envoie le courriel
    // ================================================================

    [Fact]
    public async Task La_demande_envoie_un_courriel_de_CONFIRMATION()
    {
        using var transport = new TransportFactice();
        var demandeur = new DemandeurMutable();
        await using var hote = Hote(transport, demandeur);
        var compte = await CompteAsync(hote.Services);
        demandeur.Identifiant = compte.Id;
        transport.Messages.Clear();

        var (code, _) = await DemanderAsync(hote);

        Assert.Equal(202, code);
        Assert.Contains(
            "suppression",
            Assert.Single(transport.Messages).Sujet,
            StringComparison.OrdinalIgnoreCase
        );
    }

    // ================================================================
    // Épreuve 2 — sans session, rien
    // ================================================================

    [Fact]
    public async Task Sans_SESSION_la_demande_est_refusee()
    {
        // On ne supprime pas un compte dont on ne prouve pas qu'on y est
        // connecté : l'adresse seule ouvrirait un chemin de nuisance contre
        // n'importe qui.
        using var transport = new TransportFactice();
        var demandeur = new DemandeurMutable();
        await using var hote = Hote(transport, demandeur);
        demandeur.Identifiant = null;

        var (code, _) = await DemanderAsync(hote);

        Assert.Equal(401, code);
        Assert.Empty(transport.Messages);
    }

    // ================================================================
    // Épreuve 3 — sans confirmation, le compte survit
    // ================================================================

    [Fact]
    public async Task Sans_CONFIRMATION_le_compte_survit()
    {
        using var transport = new TransportFactice();
        var demandeur = new DemandeurMutable();
        await using var hote = Hote(transport, demandeur);
        var compte = await CompteAsync(hote.Services);
        demandeur.Identifiant = compte.Id;

        await DemanderAsync(hote);

        Assert.NotNull(await TrouverAsync(hote.Services, compte.Id));
    }

    // ================================================================
    // Épreuve 4 — un code invalide ne supprime rien
    // ================================================================

    [Fact]
    public async Task Un_code_INVALIDE_ne_supprime_rien()
    {
        using var transport = new TransportFactice();
        var demandeur = new DemandeurMutable();
        await using var hote = Hote(transport, demandeur);
        var compte = await CompteAsync(hote.Services);
        demandeur.Identifiant = compte.Id;

        var (code, _) = await ConfirmerAsync(hote, "code-invente");

        Assert.Equal(400, code);
        Assert.NotNull(await TrouverAsync(hote.Services, compte.Id));
    }

    // ================================================================
    // Épreuve 5 — celle qui compte : la cascade, vérifiée EN BASE
    // ================================================================

    [Fact]
    public async Task La_confirmation_efface_le_compte_ET_ses_donnees_de_sante()
    {
        // La cascade appartient au moteur, posée au lot 2. L'éprouver au
        // travers de l'API ne prouverait rien : c'est la base qu'on interroge.
        using var transport = new TransportFactice();
        var demandeur = new DemandeurMutable();
        await using var hote = Hote(transport, demandeur);
        var compte = await CompteAsync(hote.Services);
        demandeur.Identifiant = compte.Id;
        await PoserUnPoidsAsync(compte.Id);

        Assert.Equal(1, await CompterLesPoidsAsync(compte.Id));

        var (code, _) = await ConfirmerAsync(hote, await CodeAsync(hote.Services, compte));

        Assert.Equal(204, code);
        Assert.Null(await TrouverAsync(hote.Services, compte.Id));
        Assert.Equal(0, await CompterLesPoidsAsync(compte.Id));
    }

    // ================================================================
    // Épreuve 6 — les sessions ne survivent pas non plus
    // ================================================================

    [Fact]
    public async Task Apres_suppression_les_SESSIONS_ne_valent_plus_rien()
    {
        using var transport = new TransportFactice();
        var demandeur = new DemandeurMutable();
        await using var hote = Hote(transport, demandeur);
        var compte = await CompteAsync(hote.Services);
        demandeur.Identifiant = compte.Id;
        await OuvrirUneSessionAsync(hote.Services, compte.Id);

        await ConfirmerAsync(hote, await CodeAsync(hote.Services, compte));

        Assert.Equal(0, await CompterSessionsAsync(compte.Id));
    }

    // ================================================================

    private WebApplication Hote(TransportFactice transport, DemandeurMutable demandeur) =>
        HarnaisHttp.Hote(
            baseDeDonnees,
            services =>
            {
                services.AddSingleton<Func<ITransportDeCourrier>>(() => transport);
                services.AddSingleton<IIdentiteDemandeur>(demandeur);
            }
        );

    private static async Task<(int Code, string Corps)> DemanderAsync(WebApplication hote)
    {
        using var portee = hote.Services.GetRequiredService<IServiceScopeFactory>().CreateScope();

        return await HarnaisHttp.ExecuterAsync(
            portee.ServiceProvider,
            Suppression.DemanderAsync(
                hote.Services.GetRequiredService<IIdentiteDemandeur>(),
                portee.ServiceProvider.GetRequiredService<UserManager<Utilisateur>>(),
                portee.ServiceProvider.GetRequiredService<EnvoyeurSmtp>(),
                portee.ServiceProvider.GetRequiredService<ReglagesDuCourrier>(),
                CancellationToken.None
            )
        );
    }

    private static async Task<(int Code, string Corps)> ConfirmerAsync(
        WebApplication hote,
        string code
    )
    {
        using var portee = hote.Services.GetRequiredService<IServiceScopeFactory>().CreateScope();

        return await HarnaisHttp.ExecuterAsync(
            portee.ServiceProvider,
            Suppression.ConfirmerAsync(
                new DemandeDeSuppression(code),
                hote.Services.GetRequiredService<IIdentiteDemandeur>(),
                portee.ServiceProvider.GetRequiredService<UserManager<Utilisateur>>(),
                CancellationToken.None
            )
        );
    }

    private static async Task<string> CodeAsync(IServiceProvider services, Utilisateur compte)
    {
        using var portee = services.GetRequiredService<IServiceScopeFactory>().CreateScope();
        var utilisateurs = portee.ServiceProvider.GetRequiredService<UserManager<Utilisateur>>();
        var suivi = await utilisateurs.FindByIdAsync(
            compte.Id.ToString("D", CultureInfo.InvariantCulture)
        );

        return Verification.Encoder(
            await utilisateurs.GenerateChangeEmailTokenAsync(suivi!, suivi!.Email!)
        );
    }

    private static async Task<Utilisateur> CompteAsync(IServiceProvider services)
    {
        using var portee = services.GetRequiredService<IServiceScopeFactory>().CreateScope();
        var utilisateurs = portee.ServiceProvider.GetRequiredService<UserManager<Utilisateur>>();

        var adresse = string.Create(
            CultureInfo.InvariantCulture,
            $"s-{Guid.NewGuid():N}@exemple.test"
        );

        var compte = new Utilisateur
        {
            UserName = adresse,
            Email = adresse,
            EmailConfirmed = true,
        };

        var resultat = await utilisateurs.CreateAsync(compte, _motDePasse);
        Assert.True(resultat.Succeeded, string.Join(", ", resultat.Errors.Select(e => e.Code)));

        return compte;
    }

    private static async Task<Utilisateur?> TrouverAsync(IServiceProvider services, Guid compte)
    {
        using var portee = services.GetRequiredService<IServiceScopeFactory>().CreateScope();
        return await portee
            .ServiceProvider.GetRequiredService<UserManager<Utilisateur>>()
            .FindByIdAsync(compte.ToString("D", CultureInfo.InvariantCulture));
    }

    private static async Task OuvrirUneSessionAsync(IServiceProvider services, Guid compte)
    {
        using var portee = services.GetRequiredService<IServiceScopeFactory>().CreateScope();
        await portee
            .ServiceProvider.GetRequiredService<MagasinDeSessions>()
            .OuvrirAsync(compte, "epreuve", DateTimeOffset.UtcNow);
    }

    private async Task PoserUnPoidsAsync(Guid compte)
    {
        await using var connexion = new NpgsqlConnection(baseDeDonnees.ChaineMigrations);
        await connexion.OpenAsync();
        await using var commande = new NpgsqlCommand(
            "insert into public.body_weight (owner_id, measured_on, weight_kg) "
                + "values ($1, current_date, 72.5)",
            connexion
        );
        commande.Parameters.AddWithValue(compte);
        await commande.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// La requête est FIXE, et non composée avec un nom de table : le dépôt
    /// interdit toute requête construite par concaténation, y compris dans une
    /// épreuve — c'est CA2100, et la règle a raison même ici.
    /// </summary>
    private async Task<long> CompterLesPoidsAsync(Guid compte)
    {
        await using var connexion = new NpgsqlConnection(baseDeDonnees.ChaineMigrations);
        await connexion.OpenAsync();
        await using var commande = new NpgsqlCommand(
            "select count(*) from public.body_weight where owner_id = $1",
            connexion
        );
        commande.Parameters.AddWithValue(compte);

        return (long)(await commande.ExecuteScalarAsync())!;
    }

    private async Task<long> CompterSessionsAsync(Guid compte)
    {
        await using var connexion = new NpgsqlConnection(baseDeDonnees.ChaineAuth);
        await connexion.OpenAsync();
        await using var commande = new NpgsqlCommand(
            "select count(*) from public.sessions_refresh where owner_id = $1",
            connexion
        );
        commande.Parameters.AddWithValue(compte);

        return (long)(await commande.ExecuteScalarAsync())!;
    }
}
