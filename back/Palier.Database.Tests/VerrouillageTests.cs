using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Palier.Api.Auth;
using Palier.Application.Sessions;
using Palier.Infrastructure.Identite;

namespace Palier.Database.Tests;

/// <summary>
/// Le verrouillage progressif, sur le compte réel et par le point d'entrée
/// réel.
/// </summary>
/// <remarks>
/// <para>
/// La décision est éprouvée ailleurs, pure et à 100 %. Ce fichier vérifie ce
/// que la décision seule ne peut pas dire : que l'état est bien <b>écrit</b>,
/// bien <b>relu</b>, et que le point d'entrée s'en sert dans le bon
/// <b>ordre</b>.
/// </para>
///
/// <para>
/// <b>L'ordre est le sujet.</b> Contrôler le verrou après la vérification du
/// mot de passe transformerait le verrouillage en oracle : l'attaquant
/// continuerait de tester pendant le verrouillage et saurait, au changement de
/// réponse, lequel est le bon.
/// </para>
/// </remarks>
[Collection(BaseFixture.Collection)]
public sealed class VerrouillageTests(BaseFixture baseDeDonnees)
{
    private static readonly DateTimeOffset _maintenant = new(2026, 8, 21, 12, 0, 0, TimeSpan.Zero);
    private const string _bon = "brouette-hivernale-38-oscille";
    private const string _mauvais = "un-tout-autre-mot-de-passe-42";

    [Fact]
    public async Task Le_CINQUIEME_echec_verrouille_le_compte()
    {
        await using var hote = HarnaisHttp.Hote(baseDeDonnees);
        using var portee = hote.Services.CreateScope();
        var email = await InscritAsync(portee.ServiceProvider);

        for (var i = 0; i < DecisionDeVerrouillage.EchecsAvantVerrouillage; i++)
        {
            await ConnecterAsync(portee.ServiceProvider, email, _mauvais, _maintenant);
        }

        var utilisateur = await CompteAsync(portee.ServiceProvider, email);

        Assert.NotNull(utilisateur.LockoutEnd);
        Assert.Equal(_maintenant + DecisionDeVerrouillage.Paliers[0], utilisateur.LockoutEnd);
        Assert.Equal(1, utilisateur.VerrouillagesSubis);
    }

    [Fact]
    public async Task Un_compte_VERROUILLE_refuse_le_BON_mot_de_passe()
    {
        // Le verrouillage n'a de sens que s'il tient contre le bon mot de passe.
        await using var hote = HarnaisHttp.Hote(baseDeDonnees);
        using var portee = hote.Services.CreateScope();
        var email = await InscritAsync(portee.ServiceProvider);

        for (var i = 0; i < DecisionDeVerrouillage.EchecsAvantVerrouillage; i++)
        {
            await ConnecterAsync(portee.ServiceProvider, email, _mauvais, _maintenant);
        }

        var (code, corps) = await ConnecterAsync(portee.ServiceProvider, email, _bon, _maintenant);

        Assert.Equal(StatusCodes.Status401Unauthorized, code);
        Assert.Equal("IdentifiantsInvalides", HarnaisHttp.Code(corps));
    }

    [Fact]
    public async Task Le_verrou_ne_dit_PAS_qu_il_est_un_verrou()
    {
        // Le cœur de l'épreuve. Un code distinct — « CompteVerrouille » —
        // rendu à qui présente le bon mot de passe ferait du verrouillage un
        // ORACLE : l'attaquant continuerait de tester pendant le verrouillage
        // et saurait, au changement de réponse, lequel est le bon. Le verrou
        // n'empêcherait plus la découverte, seulement l'ouverture de session.
        await using var hote = HarnaisHttp.Hote(baseDeDonnees);
        using var portee = hote.Services.CreateScope();
        var email = await InscritAsync(portee.ServiceProvider);

        for (var i = 0; i < DecisionDeVerrouillage.EchecsAvantVerrouillage; i++)
        {
            await ConnecterAsync(portee.ServiceProvider, email, _mauvais, _maintenant);
        }

        var avecLeBon = await ConnecterAsync(portee.ServiceProvider, email, _bon, _maintenant);
        var avecLeMauvais = await ConnecterAsync(
            portee.ServiceProvider,
            email,
            _mauvais,
            _maintenant
        );

        Assert.Equal(avecLeBon.Code, avecLeMauvais.Code);
        Assert.Equal(
            avecLeBon.Corps,
            avecLeMauvais.Corps
        );
    }

    [Fact]
    public async Task Le_verrou_TOMBE_a_l_echeance()
    {
        // Sans cette épreuve, un verrouillage définitif satisferait toutes les
        // précédentes — et enfermerait les utilisateurs pour de bon.
        await using var hote = HarnaisHttp.Hote(baseDeDonnees);
        using var portee = hote.Services.CreateScope();
        var email = await InscritAsync(portee.ServiceProvider);

        for (var i = 0; i < DecisionDeVerrouillage.EchecsAvantVerrouillage; i++)
        {
            await ConnecterAsync(portee.ServiceProvider, email, _mauvais, _maintenant);
        }

        var apres = _maintenant + DecisionDeVerrouillage.Paliers[0] + TimeSpan.FromSeconds(1);
        var (code, _) = await ConnecterAsync(portee.ServiceProvider, email, _bon, apres);

        Assert.Equal(StatusCodes.Status200OK, code);
    }

    [Fact]
    public async Task La_SECONDE_recidive_verrouille_PLUS_LONGTEMPS()
    {
        await using var hote = HarnaisHttp.Hote(baseDeDonnees);
        using var portee = hote.Services.CreateScope();
        var email = await InscritAsync(portee.ServiceProvider);

        for (var i = 0; i < DecisionDeVerrouillage.EchecsAvantVerrouillage; i++)
        {
            await ConnecterAsync(portee.ServiceProvider, email, _mauvais, _maintenant);
        }

        // Passé le premier verrou, on recommence.
        var apres = _maintenant + DecisionDeVerrouillage.Paliers[0] + TimeSpan.FromSeconds(1);
        for (var i = 0; i < DecisionDeVerrouillage.EchecsAvantVerrouillage; i++)
        {
            await ConnecterAsync(portee.ServiceProvider, email, _mauvais, apres);
        }

        var utilisateur = await CompteAsync(portee.ServiceProvider, email);

        Assert.Equal(2, utilisateur.VerrouillagesSubis);
        Assert.Equal(apres + DecisionDeVerrouillage.Paliers[1], utilisateur.LockoutEnd);
    }

    [Fact]
    public async Task Des_echecs_ESPACES_ne_verrouillent_jamais()
    {
        // La fenêtre est ce qui manque à Identity. Sans elle, quatre fautes de
        // frappe étalées sur trois mois plus une aujourd'hui verrouillent le
        // compte — et l'utilisateur ne peut pas comprendre pourquoi.
        await using var hote = HarnaisHttp.Hote(baseDeDonnees);
        using var portee = hote.Services.CreateScope();
        var email = await InscritAsync(portee.ServiceProvider);

        // L'ESPACEMENT EST LITTÉRAL, et l'assertion qui suit dit pourquoi.
        //
        // Le dériver de `DecisionDeVerrouillage.Fenetre` rendrait cette épreuve
        // vraie par construction : porter la fenêtre à dix ans espacerait aussi
        // les tentatives de dix ans, et elle resterait verte. Mesuré le
        // 21/08/2026 — le franchissement n'a pas mordu, et c'est l'épreuve qui
        // était en cause, pas le code.
        var espacement = TimeSpan.FromMinutes(16);

        Assert.True(
            espacement > DecisionDeVerrouillage.Fenetre,
            $"la fenêtre vaut {DecisionDeVerrouillage.Fenetre} et l'espacement {espacement} : "
                + "cette épreuve ne teste plus des échecs HORS fenêtre. Ajuster l'espacement."
        );

        var instant = _maintenant;
        for (var i = 0; i < DecisionDeVerrouillage.EchecsAvantVerrouillage * 2; i++)
        {
            await ConnecterAsync(portee.ServiceProvider, email, _mauvais, instant);
            instant += espacement;
        }

        var utilisateur = await CompteAsync(portee.ServiceProvider, email);

        Assert.Null(utilisateur.LockoutEnd);
        Assert.Equal(0, utilisateur.VerrouillagesSubis);

        // Et le compte s'ouvre toujours.
        var (code, _) = await ConnecterAsync(portee.ServiceProvider, email, _bon, instant);
        Assert.Equal(StatusCodes.Status200OK, code);
    }

    [Fact]
    public async Task Une_REUSSITE_efface_le_compteur()
    {
        await using var hote = HarnaisHttp.Hote(baseDeDonnees);
        using var portee = hote.Services.CreateScope();
        var email = await InscritAsync(portee.ServiceProvider);

        for (var i = 0; i < DecisionDeVerrouillage.EchecsAvantVerrouillage - 1; i++)
        {
            await ConnecterAsync(portee.ServiceProvider, email, _mauvais, _maintenant);
        }

        var (reussite, _) = await ConnecterAsync(portee.ServiceProvider, email, _bon, _maintenant);
        Assert.Equal(StatusCodes.Status200OK, reussite);

        var utilisateur = await CompteAsync(portee.ServiceProvider, email);
        Assert.Equal(0, utilisateur.AccessFailedCount);
        Assert.Null(utilisateur.DernierEchecLe);
    }

    [Fact]
    public async Task Le_verrouillage_d_UN_compte_n_atteint_pas_les_AUTRES()
    {
        // Sans cette épreuve, un verrouillage global satisferait toutes les
        // précédentes — et cinq erreurs de n'importe qui fermeraient le produit.
        await using var hote = HarnaisHttp.Hote(baseDeDonnees);
        using var portee = hote.Services.CreateScope();
        var vise = await InscritAsync(portee.ServiceProvider);
        var voisin = await InscritAsync(portee.ServiceProvider);

        for (var i = 0; i < DecisionDeVerrouillage.EchecsAvantVerrouillage; i++)
        {
            await ConnecterAsync(portee.ServiceProvider, vise, _mauvais, _maintenant);
        }

        var (code, _) = await ConnecterAsync(portee.ServiceProvider, voisin, _bon, _maintenant);

        Assert.Equal(StatusCodes.Status200OK, code);
    }

    // ================================================================
    // Le harnais
    // ================================================================

    private static async Task<(int Code, string Corps)> ConnecterAsync(
        IServiceProvider services,
        string email,
        string motDePasse,
        DateTimeOffset maintenant
    )
    {
        // Une portée NEUVE par tentative : sans elle, le suivi d'EF rendrait
        // l'utilisateur déjà chargé et l'épreuve lirait son propre cache au
        // lieu de la base — le défaut exact que le magasin de sessions a déjà
        // payé.
        using var portee = services.GetRequiredService<IServiceScopeFactory>().CreateScope();
        var contexte = HarnaisHttp.Contexte(portee.ServiceProvider);

        return await HarnaisHttp.ExecuterAsync(
            portee.ServiceProvider,
            PointsDEntree.ConnecterAsync(
                new DemandeDIdentifiants(email, motDePasse),
                portee.ServiceProvider.GetRequiredService<UserManager<Utilisateur>>(),
                portee.ServiceProvider.GetRequiredService<MagasinDeSessions>(),
                portee.ServiceProvider.GetRequiredService<SignataireDeJetons>(),
                portee.ServiceProvider.GetRequiredService<GardienDeVerrouillage>(),
                portee.ServiceProvider.GetRequiredService<IPasswordHasher<Utilisateur>>(),
                new HorlogeFixe(maintenant),
                contexte,
                CancellationToken.None
            ),
            contexte
        );
    }

    private static async Task<Utilisateur> CompteAsync(IServiceProvider services, string email)
    {
        using var portee = services.GetRequiredService<IServiceScopeFactory>().CreateScope();
        var utilisateur = await portee
            .ServiceProvider.GetRequiredService<UserManager<Utilisateur>>()
            .FindByEmailAsync(email);

        Assert.True(utilisateur is not null, $"le compte {email} a disparu");
        return utilisateur;
    }

    private static async Task<string> InscritAsync(IServiceProvider services)
    {
        var email =
            "verrou-"
            + Guid.NewGuid().ToString("N", System.Globalization.CultureInfo.InvariantCulture)
            + "@exemple.test";

        using var portee = services.GetRequiredService<IServiceScopeFactory>().CreateScope();
        var resultat = await portee
            .ServiceProvider.GetRequiredService<UserManager<Utilisateur>>()
            .CreateAsync(new Utilisateur { UserName = email, Email = email }, _bon);

        Assert.True(
            resultat.Succeeded,
            "le harnais n'a pas pu créer l'utilisateur : "
                + string.Join(", ", resultat.Errors.Select(e => e.Code))
        );
        return email;
    }
}
