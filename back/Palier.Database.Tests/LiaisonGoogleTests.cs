using System.Globalization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Palier.Api.Auth;
using Palier.Infrastructure.Coffre;
using Palier.Infrastructure.Identite;

namespace Palier.Database.Tests;

/// <summary>
/// La fusion des comptes — exigence 7 de <c>docs/09-comptes.md</c> § 1.
///
/// « Lier sur la seule égalité des adresses est une prise de contrôle de compte
/// dès lors que l'adresse rendue par le fournisseur n'est pas vérifiée. » Ces
/// épreuves gardent la preuve de possession, et tout ce qui peut la contourner.
/// </summary>
[Collection(BaseFixture.Collection)]
public sealed class LiaisonGoogleTests(BaseFixture baseDeDonnees)
{
    private const string _motDePasse = "brouette-hivernale-38-oscille";
    /// <summary>
    /// UNIQUE par épreuve. Une constante partagée faisait échouer la seconde
    /// liaison réussie : deux comptes ne peuvent pas porter la même connexion
    /// Google, et `AddLoginAsync` refusait — un échec qui ressemblait à un
    /// défaut de la preuve de possession.
    /// </summary>
    private static string SujetUnique() =>
        string.Create(CultureInfo.InvariantCulture, $"sujet-{Guid.NewGuid():N}");

    // ================================================================
    // Épreuve 1 — la preuve de possession
    // ================================================================

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("mauvais-mot-de-passe-mais-solide-42")]
    public async Task Sans_le_BON_mot_de_passe_la_liaison_est_refusee(string? motDePasse)
    {
        // Sans elle, ce point d'entrée lierait un compte Google à n'importe
        // quel compte dont on connaît l'adresse.
        await using var hote = HarnaisHttp.Hote(baseDeDonnees);
        var compte = await CompteAsync(hote.Services);

        var (code, _) = await LierAsync(hote, compte, motDePasse, SceauValide(hote, compte.Id));

        Assert.Equal(400, code);
        Assert.Empty(await ConnexionsAsync(hote.Services, compte));
    }

    // ================================================================
    // Épreuve 2 — sans sceau, rien
    // ================================================================

    [Fact]
    public async Task Sans_SCEAU_la_liaison_est_refusee()
    {
        await using var hote = HarnaisHttp.Hote(baseDeDonnees);
        var compte = await CompteAsync(hote.Services);

        var (code, _) = await LierAsync(hote, compte, _motDePasse, sceau: null);

        Assert.Equal(400, code);
        Assert.Empty(await ConnexionsAsync(hote.Services, compte));
    }

    // ================================================================
    // Épreuve 3 — celle qui porte la sécurité du sceau
    // ================================================================

    [Fact]
    public async Task Un_sceau_produit_pour_un_AUTRE_compte_est_refuse()
    {
        // Les données associées lient le sceau au compte visé. Sans elles, un
        // sceau intercepté lierait n'importe quel compte Google à n'importe
        // quel compte email dont on connaît le mot de passe.
        await using var hote = HarnaisHttp.Hote(baseDeDonnees);
        var victime = await CompteAsync(hote.Services);
        var autre = await CompteAsync(hote.Services);

        var (code, _) = await LierAsync(hote, victime, _motDePasse, SceauValide(hote, autre.Id));

        Assert.Equal(400, code);
        Assert.Empty(await ConnexionsAsync(hote.Services, victime));
    }

    // ================================================================
    // Épreuve 4 — un sceau périmé ne vaut rien
    // ================================================================

    [Fact]
    public async Task Un_sceau_EXPIRE_est_refuse()
    {
        await using var hote = HarnaisHttp.Hote(baseDeDonnees);
        var compte = await CompteAsync(hote.Services);

        var perime = LiaisonGoogle.Sceller(
            hote.Services.GetRequiredService<PorteurDeTrousseau>().Trousseau,
            compte.Id,
            SujetUnique(),
            DateTimeOffset.UtcNow.AddMinutes(-1)
        );

        var (code, _) = await LierAsync(hote, compte, _motDePasse, perime);

        Assert.Equal(400, code);
        Assert.Empty(await ConnexionsAsync(hote.Services, compte));
    }

    // ================================================================
    // Épreuve 5 — le second facteur garde le compte
    // ================================================================

    [Fact]
    public async Task Avec_la_2FA_active_le_mot_de_passe_seul_ne_SUFFIT_PAS()
    {
        // Le lot 4 a établi que le second facteur garde le compte, pas
        // seulement la connexion. Lier une seconde voie d'accès est exactement
        // le geste qu'il doit couvrir.
        await using var hote = HarnaisHttp.Hote(baseDeDonnees);
        var compte = await CompteAsync(hote.Services, deuxFacteurs: true);

        var (code, _) = await LierAsync(hote, compte, _motDePasse, SceauValide(hote, compte.Id));

        Assert.Equal(400, code);
        Assert.Empty(await ConnexionsAsync(hote.Services, compte));
    }

    // ================================================================
    // Épreuve 6 — la borne : avec tout ce qu'il faut, ça lie
    // ================================================================

    [Fact]
    public async Task Avec_le_sceau_ET_le_mot_de_passe_la_liaison_reussit()
    {
        // Sans cette borne, les cinq épreuves ci-dessus seraient vertes en
        // refusant tout, y compris ce qui est légitime.
        await using var hote = HarnaisHttp.Hote(baseDeDonnees);
        var compte = await CompteAsync(hote.Services);
        var sujet = SujetUnique();

        var (code, _) = await LierAsync(
            hote,
            compte,
            _motDePasse,
            SceauValide(hote, compte.Id, sujet)
        );

        Assert.Equal(204, code);

        var connexions = await ConnexionsAsync(hote.Services, compte);
        var connexion = Assert.Single(connexions);
        Assert.Equal(Google.Schema, connexion.LoginProvider);
        Assert.Equal(sujet, connexion.ProviderKey);
    }

    // ================================================================
    // Épreuve 7 — le sceau est à usage unique
    // ================================================================

    [Fact]
    public async Task Le_cookie_du_sceau_est_EFFACE_apres_usage()
    {
        await using var hote = HarnaisHttp.Hote(baseDeDonnees);
        var compte = await CompteAsync(hote.Services);

        var contexte = await LierEtRendreLeContexteAsync(
            hote,
            compte,
            _motDePasse,
            SceauValide(hote, compte.Id)
        );

        // Un cookie s'efface en le réécrivant vide et périmé : la présence de
        // son nom dans `Set-Cookie` avec une valeur vide est la trace du geste.
        var cookies = contexte.Response.Headers.SetCookie.ToString();
        Assert.Contains(LiaisonGoogle.NomDuCookie, cookies, StringComparison.Ordinal);
        Assert.Contains("expires=", cookies, StringComparison.OrdinalIgnoreCase);
    }

    // ================================================================

    private static string SceauValide(WebApplication hote, Guid compte, string? sujet = null) =>
        LiaisonGoogle.Sceller(
            hote.Services.GetRequiredService<PorteurDeTrousseau>().Trousseau,
            compte,
            sujet ?? SujetUnique(),
            DateTimeOffset.UtcNow.AddMinutes(5)
        );

    private static async Task<(int Code, string Corps)> LierAsync(
        WebApplication hote,
        Utilisateur compte,
        string? motDePasse,
        string? sceau
    )
    {
        var contexte = await LierEtRendreLeContexteAsync(hote, compte, motDePasse, sceau);
        return (contexte.Response.StatusCode, string.Empty);
    }

    private static async Task<DefaultHttpContext> LierEtRendreLeContexteAsync(
        WebApplication hote,
        Utilisateur compte,
        string? motDePasse,
        string? sceau
    )
    {
        using var portee = hote.Services.GetRequiredService<IServiceScopeFactory>().CreateScope();
        var contexte = HarnaisHttp.Contexte(portee.ServiceProvider);

        if (sceau is not null)
        {
            contexte.Request.Headers.Cookie = $"{LiaisonGoogle.NomDuCookie}={sceau}";
        }

        using var corps = new MemoryStream();
        contexte.Response.Body = corps;

        var resultat = await LiaisonGoogle.LierAsync(
            new DemandeDeLiaison(compte.Email, motDePasse, null),
            contexte,
            portee.ServiceProvider.GetRequiredService<UserManager<Utilisateur>>(),
            hote.Services.GetRequiredService<PorteurDeTrousseau>(),
            hote.Services.GetRequiredService<TimeProvider>(),
            CancellationToken.None
        );

        await resultat.ExecuteAsync(contexte);
        return contexte;
    }

    private static async Task<IList<UserLoginInfo>> ConnexionsAsync(
        IServiceProvider services,
        Utilisateur compte
    )
    {
        using var portee = services.GetRequiredService<IServiceScopeFactory>().CreateScope();
        var utilisateurs = portee.ServiceProvider.GetRequiredService<UserManager<Utilisateur>>();
        var suivi = await utilisateurs.FindByIdAsync(
            compte.Id.ToString("D", CultureInfo.InvariantCulture)
        );

        return await utilisateurs.GetLoginsAsync(suivi!);
    }

    private static async Task<Utilisateur> CompteAsync(
        IServiceProvider services,
        bool deuxFacteurs = false
    )
    {
        using var portee = services.GetRequiredService<IServiceScopeFactory>().CreateScope();
        var utilisateurs = portee.ServiceProvider.GetRequiredService<UserManager<Utilisateur>>();

        var adresse = string.Create(
            CultureInfo.InvariantCulture,
            $"l-{Guid.NewGuid():N}@exemple.test"
        );

        var compte = new Utilisateur
        {
            UserName = adresse,
            Email = adresse,
            EmailConfirmed = true,
            TwoFactorEnabled = deuxFacteurs,
        };

        var resultat = await utilisateurs.CreateAsync(compte, _motDePasse);
        Assert.True(resultat.Succeeded, string.Join(", ", resultat.Errors.Select(e => e.Code)));

        return compte;
    }
}
