using System.Globalization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Palier.Api.Auth;
using Palier.Infrastructure.Courrier;
using Palier.Infrastructure.Identite;

namespace Palier.Database.Tests;

/// <summary>
/// La réinitialisation de mot de passe.
///
/// Elle porte les mêmes règles que la vérification — pas d'oracle, 202
/// systématique — et une de plus, qui lui est propre : <b>elle révoque toutes
/// les sessions</b>. C'est ce qu'on fait quand on croit son compte compromis ;
/// laisser vivre les jetons existants viderait le geste de son sens.
/// </summary>
[Collection(BaseFixture.Collection)]
public sealed class ReinitialisationTests(BaseFixture baseDeDonnees)
{
    private const string _ancien = "brouette-hivernale-38-oscille";
    private const string _nouveau = "clavecin-orageux-71-titube";

    // ================================================================
    // Épreuve 1 — pas d'oracle sur la demande
    // ================================================================

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task La_demande_rend_202_que_le_compte_existe_ou_NON(bool existe)
    {
        using var transport = new TransportFactice();
        await using var hote = Hote(transport);

        var adresse = existe ? (await CompteAsync(hote.Services)).Email! : AdresseUnique();

        var (code, corps) = await HarnaisHttp.ExecuterAsync(
            hote.Services,
            Reinitialisation.OublierAsync(
                new DemandeDOubli(adresse),
                Utilisateurs(hote.Services),
                hote.Services.GetRequiredService<EnvoyeurSmtp>(),
                hote.Services.GetRequiredService<ReglagesDuCourrier>(),
                CancellationToken.None
            )
        );

        Assert.Equal(202, code);
        Assert.Equal(string.Empty, corps);
    }

    // ================================================================
    // Épreuve 2 — et rien ne part à une adresse inconnue
    // ================================================================

    [Fact]
    public async Task La_demande_n_ENVOIE_RIEN_pour_une_adresse_inconnue()
    {
        using var transport = new TransportFactice();
        await using var hote = Hote(transport);

        await Demander(hote.Services, AdresseUnique());

        Assert.Empty(transport.Messages);
    }

    // ================================================================
    // Épreuve 3 — ni à une adresse NON VÉRIFIÉE
    // ================================================================

    [Fact]
    public async Task La_demande_n_envoie_RIEN_a_une_adresse_NON_VERIFIEE()
    {
        // Envoyer un lien de reprise de compte à une adresse dont personne n'a
        // prouvé la possession, c'est offrir le chemin qu'une prise de contrôle
        // emprunterait.
        using var transport = new TransportFactice();
        await using var hote = Hote(transport);
        var compte = await CompteAsync(hote.Services, verifie: false);
        transport.Messages.Clear();

        await Demander(hote.Services, compte.Email!);

        Assert.Empty(transport.Messages);
    }

    // ================================================================
    // Épreuve 4 — le validateur du lot 4 s'applique aussi ici
    // ================================================================

    [Fact]
    public async Task Un_mot_de_passe_FREQUENT_est_refuse_a_la_reinitialisation()
    {
        // Un mot de passe choisi après réinitialisation n'est pas moins exposé
        // qu'un autre — c'est même l'inverse : on le choisit vite, sous le coup
        // de l'urgence.
        using var transport = new TransportFactice();
        await using var hote = Hote(transport);
        var compte = await CompteAsync(hote.Services);

        var (code, _) = await Reinitialiser(hote.Services, compte, "password");

        Assert.Equal(400, code);
    }

    // ================================================================
    // Épreuve 5 — celle qui porte le sens du geste
    // ================================================================

    [Fact]
    public async Task La_reinitialisation_REVOQUE_toutes_les_sessions()
    {
        using var transport = new TransportFactice();
        await using var hote = Hote(transport);
        var compte = await CompteAsync(hote.Services);
        await OuvrirUneSessionAsync(hote.Services, compte.Id);

        Assert.Equal(1, await SessionsVivantesAsync(compte.Id));

        var (code, _) = await Reinitialiser(hote.Services, compte, _nouveau);

        Assert.Equal(204, code);
        Assert.Equal(0, await SessionsVivantesAsync(compte.Id));
    }

    // ================================================================
    // Épreuve 6 — un code d'un autre compte ne vaut rien
    // ================================================================

    [Fact]
    public async Task Un_code_D_UN_AUTRE_compte_est_refuse()
    {
        using var transport = new TransportFactice();
        await using var hote = Hote(transport);
        var victime = await CompteAsync(hote.Services);
        var attaquant = await CompteAsync(hote.Services);

        var (code, _) = await HarnaisHttp.ExecuterAsync(
            hote.Services,
            Reinitialisation.ReinitialiserAsync(
                new DemandeDeNouveauMotDePasse(
                    victime.Id.ToString("D", CultureInfo.InvariantCulture),
                    await CodeAsync(hote.Services, attaquant),
                    _nouveau
                ),
                Utilisateurs(hote.Services),
                hote.Services.GetRequiredService<IServiceScopeFactory>()
                    .CreateScope()
                    .ServiceProvider.GetRequiredService<MagasinDeSessions>(),
                hote.Services.GetRequiredService<TimeProvider>(),
                CancellationToken.None
            )
        );

        Assert.Equal(400, code);
    }

    // ================================================================
    // Épreuve 7 — le nouveau mot de passe ouvre, l'ancien non
    // ================================================================

    [Fact]
    public async Task Apres_reinitialisation_l_ANCIEN_mot_de_passe_ne_vaut_plus_rien()
    {
        using var transport = new TransportFactice();
        await using var hote = Hote(transport);
        var compte = await CompteAsync(hote.Services);

        await Reinitialiser(hote.Services, compte, _nouveau);

        using var portee = hote.Services.GetRequiredService<IServiceScopeFactory>().CreateScope();
        var utilisateurs = portee.ServiceProvider.GetRequiredService<UserManager<Utilisateur>>();
        var suivi = await utilisateurs.FindByIdAsync(
            compte.Id.ToString("D", CultureInfo.InvariantCulture)
        );

        Assert.False(await utilisateurs.CheckPasswordAsync(suivi!, _ancien));
        Assert.True(await utilisateurs.CheckPasswordAsync(suivi!, _nouveau));
    }

    // ================================================================

    private WebApplication Hote(TransportFactice transport) =>
        HarnaisHttp.Hote(
            baseDeDonnees,
            services => services.AddSingleton<Func<ITransportDeCourrier>>(() => transport)
        );

    private static UserManager<Utilisateur> Utilisateurs(IServiceProvider services) =>
        services
            .GetRequiredService<IServiceScopeFactory>()
            .CreateScope()
            .ServiceProvider.GetRequiredService<UserManager<Utilisateur>>();

    private static Task<(int Code, string Corps)> Demander(IServiceProvider services, string adresse) =>
        HarnaisHttp.ExecuterAsync(
            services,
            Reinitialisation.OublierAsync(
                new DemandeDOubli(adresse),
                Utilisateurs(services),
                services.GetRequiredService<EnvoyeurSmtp>(),
                services.GetRequiredService<ReglagesDuCourrier>(),
                CancellationToken.None
            )
        );

    private static async Task<(int Code, string Corps)> Reinitialiser(
        IServiceProvider services,
        Utilisateur compte,
        string nouveau
    )
    {
        using var portee = services.GetRequiredService<IServiceScopeFactory>().CreateScope();

        return await HarnaisHttp.ExecuterAsync(
            services,
            Reinitialisation.ReinitialiserAsync(
                new DemandeDeNouveauMotDePasse(
                    compte.Id.ToString("D", CultureInfo.InvariantCulture),
                    await CodeAsync(services, compte),
                    nouveau
                ),
                portee.ServiceProvider.GetRequiredService<UserManager<Utilisateur>>(),
                portee.ServiceProvider.GetRequiredService<MagasinDeSessions>(),
                services.GetRequiredService<TimeProvider>(),
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
            await utilisateurs.GeneratePasswordResetTokenAsync(suivi!)
        );
    }

    private static async Task<Utilisateur> CompteAsync(
        IServiceProvider services,
        bool verifie = true
    )
    {
        using var portee = services.GetRequiredService<IServiceScopeFactory>().CreateScope();
        var utilisateurs = portee.ServiceProvider.GetRequiredService<UserManager<Utilisateur>>();

        var adresse = AdresseUnique();
        var compte = new Utilisateur
        {
            UserName = adresse,
            Email = adresse,
            EmailConfirmed = verifie,
        };

        var resultat = await utilisateurs.CreateAsync(compte, _ancien);
        Assert.True(resultat.Succeeded, string.Join(", ", resultat.Errors.Select(e => e.Code)));

        return compte;
    }

    private static async Task OuvrirUneSessionAsync(IServiceProvider services, Guid compte)
    {
        using var portee = services.GetRequiredService<IServiceScopeFactory>().CreateScope();
        var sessions = portee.ServiceProvider.GetRequiredService<MagasinDeSessions>();

        await sessions.OuvrirAsync(compte, "epreuve", DateTimeOffset.UtcNow);
    }

    private async Task<long> SessionsVivantesAsync(Guid compte)
    {
        await using var connexion = new NpgsqlConnection(baseDeDonnees.ChaineAuth);
        await connexion.OpenAsync();
        await using var commande = new NpgsqlCommand(
            "select count(*) from public.sessions_refresh "
                + "where owner_id = $1 and revoked_at is null",
            connexion
        );
        commande.Parameters.AddWithValue(compte);

        return (long)(await commande.ExecuteScalarAsync())!;
    }

    private static string AdresseUnique() =>
        string.Create(CultureInfo.InvariantCulture, $"r-{Guid.NewGuid():N}@exemple.test");
}
