using System.Globalization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Palier.Api.Auth;
using Palier.Infrastructure.Courrier;
using Palier.Infrastructure.Identite;

namespace Palier.Database.Tests;

/// <summary>
/// La vérification d'adresse — exigence 2 de <c>docs/09-comptes.md</c> § 1.
///
/// Le lot 4 avait livré la RÈGLE : <c>PorteDesDomaines.NutritionOuverte</c> lit
/// <c>EmailConfirmed</c>, et trois épreuves gardent les quatre combinaisons. Ce
/// qui manquait était le MOYEN de faire passer ce drapeau à vrai — donc aucun
/// compte ne pouvait atteindre la nutrition.
/// </summary>
[Collection(BaseFixture.Collection)]
public sealed class VerificationTests(BaseFixture baseDeDonnees)
{
    private const string _motDePasse = "brouette-hivernale-38-oscille";

    // ================================================================
    // Épreuve 1 — l'inscription envoie le courriel de vérification
    // ================================================================

    [Fact]
    public async Task L_inscription_envoie_le_courriel_de_VERIFICATION()
    {
        using var transport = new TransportFactice();
        await using var hote = Hote(transport);

        await Inscrire(hote.Services, AdresseUnique());

        Assert.Contains(
            "confirmez votre adresse",
            Assert.Single(transport.Messages).Sujet,
            StringComparison.Ordinal
        );
    }

    // ================================================================
    // Épreuve 2 — celle qui protège l'annuaire
    // ================================================================

    [Fact]
    public async Task Une_inscription_sur_une_adresse_DEJA_PRISE_envoie_AUSSI_un_courriel()
    {
        // Sans cela, le seul fait qu'un courriel parte — ou non — trahirait
        // l'existence du compte : le temps de réponse suffirait à interroger
        // l'annuaire. Le lot 4 a fermé ce canal sur la connexion ; l'ouvrir ici
        // par la porte de derrière l'aurait rouvert.
        //
        // Le second courriel n'est PAS le même : il avertit le propriétaire
        // qu'on a tenté de s'inscrire avec son adresse.
        using var transport = new TransportFactice();
        await using var hote = Hote(transport);
        var adresse = AdresseUnique();

        await Inscrire(hote.Services, adresse);
        await Inscrire(hote.Services, adresse);

        Assert.Equal(2, transport.Messages.Count);
        Assert.Contains("confirmez", transport.Messages[0].Sujet, StringComparison.Ordinal);
        Assert.DoesNotContain("confirmez", transport.Messages[1].Sujet, StringComparison.Ordinal);
    }

    // ================================================================
    // Épreuve 3 — un code valide pose le drapeau
    // ================================================================

    [Fact]
    public async Task Un_code_VALIDE_pose_EmailConfirmed()
    {
        using var transport = new TransportFactice();
        await using var hote = Hote(transport);
        var compte = await CompteAsync(hote.Services);

        var (code, _) = await HarnaisHttp.ExecuterAsync(
            hote.Services,
            Verification.VerifierAsync(
                new DemandeDeVerification(
                    compte.Id.ToString("D", CultureInfo.InvariantCulture),
                    await CodeAsync(hote.Services, compte)
                ),
                Utilisateurs(hote.Services),
                CancellationToken.None
            )
        );

        // 204 : la vérification ne rend aucun corps. Rien à dire à l'appelant
        // qu'un code de succès ne dise déjà.
        Assert.Equal(204, code);
        Assert.True(await EstVerifieAsync(hote.Services, compte.Id));
    }

    // ================================================================
    // Épreuve 4 — un code d'un autre compte ne vaut rien
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
            Verification.VerifierAsync(
                new DemandeDeVerification(
                    victime.Id.ToString("D", CultureInfo.InvariantCulture),
                    await CodeAsync(hote.Services, attaquant)
                ),
                Utilisateurs(hote.Services),
                CancellationToken.None
            )
        );

        Assert.Equal(400, code);
        Assert.False(await EstVerifieAsync(hote.Services, victime.Id));
    }

    // ================================================================
    // Épreuve 5 — le renvoi ne dit jamais si l'adresse existe
    // ================================================================

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Le_renvoi_rend_202_que_le_compte_existe_ou_NON(bool existe)
    {
        using var transport = new TransportFactice();
        await using var hote = Hote(transport);

        var adresse = existe
            ? (await CompteAsync(hote.Services)).Email!
            : AdresseUnique();

        var (code, corps) = await HarnaisHttp.ExecuterAsync(
            hote.Services,
            Verification.RenvoyerAsync(
                new DemandeDeRenvoi(adresse),
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
    // Épreuve 6 — et il n'envoie rien à une adresse inconnue
    // ================================================================

    [Fact]
    public async Task Le_renvoi_n_ENVOIE_RIEN_pour_une_adresse_inconnue()
    {
        // Le 202 seul ne prouve rien : si le courriel partait quand même, il
        // arriverait à une adresse que le produit ne connaît pas — et le
        // destinataire apprendrait qu'on s'intéresse à lui.
        using var transport = new TransportFactice();
        await using var hote = Hote(transport);

        await HarnaisHttp.ExecuterAsync(
            hote.Services,
            Verification.RenvoyerAsync(
                new DemandeDeRenvoi(AdresseUnique()),
                Utilisateurs(hote.Services),
                hote.Services.GetRequiredService<EnvoyeurSmtp>(),
                hote.Services.GetRequiredService<ReglagesDuCourrier>(),
                CancellationToken.None
            )
        );

        Assert.Empty(transport.Messages);
    }

    // ================================================================
    // Épreuve 7 — un compte déjà vérifié ne reçoit pas un second courriel
    // ================================================================

    [Fact]
    public async Task Un_compte_DEJA_verifie_ne_recoit_pas_un_second_courriel()
    {
        using var transport = new TransportFactice();
        await using var hote = Hote(transport);
        var compte = await CompteAsync(hote.Services, verifie: true);
        transport.Messages.Clear();

        await HarnaisHttp.ExecuterAsync(
            hote.Services,
            Verification.RenvoyerAsync(
                new DemandeDeRenvoi(compte.Email!),
                Utilisateurs(hote.Services),
                hote.Services.GetRequiredService<EnvoyeurSmtp>(),
                hote.Services.GetRequiredService<ReglagesDuCourrier>(),
                CancellationToken.None
            )
        );

        Assert.Empty(transport.Messages);
    }

    // ================================================================
    // Épreuve 8 — le lien porte la base configurée
    // ================================================================

    [Fact]
    public async Task Le_lien_envoye_part_de_APP_URL()
    {
        using var transport = new TransportFactice();
        await using var hote = Hote(transport);
        var compte = await CompteAsync(hote.Services);
        transport.Messages.Clear();

        await HarnaisHttp.ExecuterAsync(
            hote.Services,
            Verification.RenvoyerAsync(
                new DemandeDeRenvoi(compte.Email!),
                Utilisateurs(hote.Services),
                hote.Services.GetRequiredService<EnvoyeurSmtp>(),
                hote.Services.GetRequiredService<ReglagesDuCourrier>(),
                CancellationToken.None
            )
        );

        Assert.Contains(
            "https://palier.test/",
            Assert.Single(transport.Messages).Texte,
            StringComparison.Ordinal
        );
    }

    // ================================================================

    private WebApplication Hote(TransportFactice transport) =>
        HarnaisHttp.Hote(
            baseDeDonnees,
            services =>
            {
                services.AddSingleton<Func<ITransportDeCourrier>>(() => transport);
            }
        );

    private static UserManager<Utilisateur> Utilisateurs(IServiceProvider services) =>
        services
            .GetRequiredService<IServiceScopeFactory>()
            .CreateScope()
            .ServiceProvider.GetRequiredService<UserManager<Utilisateur>>();

    private static async Task Inscrire(IServiceProvider services, string adresse)
    {
        using var portee = services.GetRequiredService<IServiceScopeFactory>().CreateScope();

        await HarnaisHttp.ExecuterAsync(
            services,
            PointsDEntree.InscrireAsync(
                new DemandeDIdentifiants(adresse, _motDePasse, null),
                portee.ServiceProvider.GetRequiredService<UserManager<Utilisateur>>(),
                portee.ServiceProvider.GetRequiredService<EnvoyeurSmtp>(),
                portee.ServiceProvider.GetRequiredService<ReglagesDuCourrier>(),
                CancellationToken.None
            )
        );
    }

    private static async Task<Utilisateur> CompteAsync(
        IServiceProvider services,
        bool verifie = false
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

        var resultat = await utilisateurs.CreateAsync(compte, _motDePasse);
        Assert.True(resultat.Succeeded, string.Join(", ", resultat.Errors.Select(e => e.Code)));

        return compte;
    }

    private static async Task<string> CodeAsync(IServiceProvider services, Utilisateur compte)
    {
        using var portee = services.GetRequiredService<IServiceScopeFactory>().CreateScope();
        var utilisateurs = portee.ServiceProvider.GetRequiredService<UserManager<Utilisateur>>();
        var suivi = await utilisateurs.FindByIdAsync(
            compte.Id.ToString("D", CultureInfo.InvariantCulture)
        );

        // ENCODÉ comme il l'est dans le lien : le point d'entrée reçoit ce que
        // le courriel a transporté, pas le jeton brut d'Identity.
        return Verification.Encoder(
            await utilisateurs.GenerateEmailConfirmationTokenAsync(suivi!)
        );
    }

    private static async Task<bool> EstVerifieAsync(IServiceProvider services, Guid compte)
    {
        using var portee = services.GetRequiredService<IServiceScopeFactory>().CreateScope();
        var utilisateurs = portee.ServiceProvider.GetRequiredService<UserManager<Utilisateur>>();
        var suivi = await utilisateurs.FindByIdAsync(
            compte.ToString("D", CultureInfo.InvariantCulture)
        );

        return suivi!.EmailConfirmed;
    }

    private static string AdresseUnique() =>
        string.Create(CultureInfo.InvariantCulture, $"v-{Guid.NewGuid():N}@exemple.test");
}
