using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Palier.Api.Auth;
using Palier.Infrastructure.Identite;

namespace Palier.Database.Tests;

/// <summary>
/// La rotation, la déconnexion et la liste des sessions, par les points
/// d'entrée réels et sur le moteur réel.
/// </summary>
/// <remarks>
/// <para>
/// <b>Ce que la rotation achète.</b> Un jeton de rafraîchissement vit deux
/// semaines : volé, il ouvre deux semaines d'accès. La rotation le rend inerte
/// dès son premier usage, si bien qu'un vol ne vaut que jusqu'au prochain
/// rafraîchissement de la victime — et qu'à ce moment-là, DEUX porteurs
/// présentent la même chaîne. C'est ce croisement qui se détecte, et RFC 9700
/// (janvier 2025) en tire la seule conclusion tenable : lequel des deux est le
/// voleur étant indécidable, toute la famille tombe.
/// </para>
///
/// <para>
/// <b>Et ce qu'elle coûte, si on l'applique sans nuance.</b> Deux onglets qui
/// se rafraîchissent à la même seconde présentent aussi la même chaîne, sans
/// aucun vol. D'où la fenêtre de grâce : le jeton tout juste consommé reste
/// acceptable trente secondes, et l'utilisateur n'est pas déconnecté parce que
/// deux de ses requêtes se sont croisées.
/// </para>
/// </remarks>
[Collection(BaseFixture.Collection)]
public sealed class RotationDesSessionsTests(BaseFixture baseDeDonnees)
{
    private static readonly DateTimeOffset _maintenant = new(2026, 8, 21, 12, 0, 0, TimeSpan.Zero);
    private const string _motDePasse = "brouette-hivernale-38-oscille";

    // ================================================================
    // Rafraîchir
    // ================================================================

    [Fact]
    public async Task Rafraichir_SANS_COOKIE_est_refuse()
    {
        await using var hote = HarnaisHttp.Hote(baseDeDonnees);
        using var portee = hote.Services.CreateScope();
        var contexte = HarnaisHttp.Contexte(portee.ServiceProvider);

        var (code, corps) = await HarnaisHttp.ExecuterAsync(
            portee.ServiceProvider,
            Rafraichir(portee.ServiceProvider, contexte, _maintenant),
            contexte
        );

        Assert.Equal(StatusCodes.Status401Unauthorized, code);
        Assert.Equal("SessionInvalide", HarnaisHttp.Code(corps));
    }

    [Fact]
    public async Task Rafraichir_rend_un_JETON_NEUF_et_un_COOKIE_NEUF()
    {
        await using var hote = HarnaisHttp.Hote(baseDeDonnees);
        using var portee = hote.Services.CreateScope();
        var ouverture = await OuvrirAsync(portee.ServiceProvider);
        var contexte = HarnaisHttp.ContexteAvecCookie(portee.ServiceProvider, ouverture);

        var (code, _) = await HarnaisHttp.ExecuterAsync(
            portee.ServiceProvider,
            Rafraichir(portee.ServiceProvider, contexte, _maintenant),
            contexte
        );

        Assert.Equal(StatusCodes.Status200OK, code);

        var neuf = HarnaisHttp.CookieRendu(contexte);
        Assert.NotNull(neuf);
        Assert.NotEqual(ouverture, neuf);
    }

    [Fact]
    public async Task L_ancien_jeton_ne_marche_PLUS_passee_la_grace()
    {
        // Le cœur de la rotation. Sans elle, un jeton volé resterait utilisable
        // pendant les deux semaines de sa durée de vie.
        await using var hote = HarnaisHttp.Hote(baseDeDonnees);
        using var portee = hote.Services.CreateScope();
        var ancien = await OuvrirAsync(portee.ServiceProvider);

        var premier = HarnaisHttp.ContexteAvecCookie(portee.ServiceProvider, ancien);
        await HarnaisHttp.ExecuterAsync(
            portee.ServiceProvider,
            Rafraichir(portee.ServiceProvider, premier, _maintenant),
            premier
        );

        // Une minute plus tard : bien au-delà des trente secondes de grâce.
        var tardif = HarnaisHttp.ContexteAvecCookie(portee.ServiceProvider, ancien);
        var (code, corps) = await HarnaisHttp.ExecuterAsync(
            portee.ServiceProvider,
            Rafraichir(portee.ServiceProvider, tardif, _maintenant.AddMinutes(1)),
            tardif
        );

        Assert.Equal(StatusCodes.Status401Unauthorized, code);
        Assert.Equal("SessionInvalide", HarnaisHttp.Code(corps));
    }

    [Fact]
    public async Task Un_REJEU_TARDIF_fait_tomber_TOUTE_la_famille()
    {
        // Le franchissement qui compte le plus de ce fichier. Deux porteurs
        // détiennent la même chaîne ; lequel est le voleur est indécidable.
        // RFC 9700 § 4.14.2 : les deux perdent l'accès.
        //
        // Sans cela, la rotation seule ne protégerait de rien — le voleur
        // continuerait sa propre chaîne, et la victime la sienne, chacun
        // ignorant l'autre.
        await using var hote = HarnaisHttp.Hote(baseDeDonnees);
        using var portee = hote.Services.CreateScope();
        var ancien = await OuvrirAsync(portee.ServiceProvider);

        var premier = HarnaisHttp.ContexteAvecCookie(portee.ServiceProvider, ancien);
        await HarnaisHttp.ExecuterAsync(
            portee.ServiceProvider,
            Rafraichir(portee.ServiceProvider, premier, _maintenant),
            premier
        );
        var successeur = HarnaisHttp.CookieRendu(premier);
        Assert.NotNull(successeur);

        // Le voleur rejoue l'ancien jeton, une minute après.
        var rejeu = HarnaisHttp.ContexteAvecCookie(portee.ServiceProvider, ancien);
        await HarnaisHttp.ExecuterAsync(
            portee.ServiceProvider,
            Rafraichir(portee.ServiceProvider, rejeu, _maintenant.AddMinutes(1)),
            rejeu
        );

        // Et LE SUCCESSEUR LÉGITIME est mort avec le reste.
        var victime = HarnaisHttp.ContexteAvecCookie(portee.ServiceProvider, successeur);
        var (code, _) = await HarnaisHttp.ExecuterAsync(
            portee.ServiceProvider,
            Rafraichir(portee.ServiceProvider, victime, _maintenant.AddMinutes(2)),
            victime
        );

        Assert.Equal(StatusCodes.Status401Unauthorized, code);
    }

    [Fact]
    public async Task Un_rejeu_DANS_la_grace_ne_deconnecte_PAS()
    {
        // La contrepartie, et elle est indispensable : deux onglets qui se
        // rafraîchissent à la même seconde présentent la même chaîne sans
        // qu'aucun vol n'ait eu lieu. Sans la grâce, l'usage normal du produit
        // déconnecterait ses utilisateurs.
        await using var hote = HarnaisHttp.Hote(baseDeDonnees);
        using var portee = hote.Services.CreateScope();
        var ancien = await OuvrirAsync(portee.ServiceProvider);

        var premier = HarnaisHttp.ContexteAvecCookie(portee.ServiceProvider, ancien);
        await HarnaisHttp.ExecuterAsync(
            portee.ServiceProvider,
            Rafraichir(portee.ServiceProvider, premier, _maintenant),
            premier
        );

        // Dix secondes plus tard : DANS les trente de la grâce.
        var croise = HarnaisHttp.ContexteAvecCookie(portee.ServiceProvider, ancien);
        var (code, _) = await HarnaisHttp.ExecuterAsync(
            portee.ServiceProvider,
            Rafraichir(portee.ServiceProvider, croise, _maintenant.AddSeconds(10)),
            croise
        );

        Assert.Equal(StatusCodes.Status200OK, code);
        Assert.NotNull(HarnaisHttp.CookieRendu(croise));

        // Et le successeur du premier appel vit toujours : rien n'a été révoqué.
        var successeur = HarnaisHttp.CookieRendu(premier);
        Assert.NotNull(successeur);
        var suite = HarnaisHttp.ContexteAvecCookie(portee.ServiceProvider, successeur);
        var (encore, _) = await HarnaisHttp.ExecuterAsync(
            portee.ServiceProvider,
            Rafraichir(portee.ServiceProvider, suite, _maintenant.AddSeconds(15)),
            suite
        );

        Assert.Equal(StatusCodes.Status200OK, encore);
    }

    [Fact]
    public async Task Les_CAUSES_d_echec_ne_se_distinguent_pas()
    {
        // Un jeton inventé, un jeton révoqué, un réemploi détecté : trois
        // situations, une seule réponse. Dire « réemploi détecté » apprendrait
        // au voleur qu'il a été repéré — et lui confirmerait que le jeton qu'il
        // détient était authentique.
        await using var hote = HarnaisHttp.Hote(baseDeDonnees);
        using var portee = hote.Services.CreateScope();

        var invente = HarnaisHttp.ContexteAvecCookie(portee.ServiceProvider, "un-jeton-invente");
        var premier = await HarnaisHttp.ExecuterAsync(
            portee.ServiceProvider,
            Rafraichir(portee.ServiceProvider, invente, _maintenant),
            invente
        );

        var ancien = await OuvrirAsync(portee.ServiceProvider);
        var tour = HarnaisHttp.ContexteAvecCookie(portee.ServiceProvider, ancien);
        await HarnaisHttp.ExecuterAsync(
            portee.ServiceProvider,
            Rafraichir(portee.ServiceProvider, tour, _maintenant),
            tour
        );
        var rejeu = HarnaisHttp.ContexteAvecCookie(portee.ServiceProvider, ancien);
        var second = await HarnaisHttp.ExecuterAsync(
            portee.ServiceProvider,
            Rafraichir(portee.ServiceProvider, rejeu, _maintenant.AddMinutes(1)),
            rejeu
        );

        Assert.Equal(premier.Code, second.Code);
        Assert.Equal(premier.Corps, second.Corps);
    }

    [Fact]
    public async Task Un_echec_EFFACE_le_cookie_mort()
    {
        // Le garder ferait re-présenter la même chaîne morte à chaque tentative
        // — et sur un réemploi, rejouerait la révocation indéfiniment.
        await using var hote = HarnaisHttp.Hote(baseDeDonnees);
        using var portee = hote.Services.CreateScope();
        var contexte = HarnaisHttp.ContexteAvecCookie(portee.ServiceProvider, "un-jeton-invente");

        await HarnaisHttp.ExecuterAsync(
            portee.ServiceProvider,
            Rafraichir(portee.ServiceProvider, contexte, _maintenant),
            contexte
        );

        Assert.True(
            HarnaisHttp.CookieEfface(contexte),
            "le cookie mort est resté en place : le client le représentera à chaque essai."
        );
    }

    // ================================================================
    // Déconnexion
    // ================================================================

    [Fact]
    public async Task La_deconnexion_coupe_la_chaine_et_EFFACE_le_cookie()
    {
        await using var hote = HarnaisHttp.Hote(baseDeDonnees);
        using var portee = hote.Services.CreateScope();
        var ouverture = await OuvrirAsync(portee.ServiceProvider);
        var contexte = HarnaisHttp.ContexteAvecCookie(portee.ServiceProvider, ouverture);

        var (code, _) = await HarnaisHttp.ExecuterAsync(
            portee.ServiceProvider,
            PointsDEntree.DeconnecterAsync(
                portee.ServiceProvider.GetRequiredService<MagasinDeSessions>(),
                new HorlogeFixe(_maintenant),
                contexte,
                CancellationToken.None
            ),
            contexte
        );

        Assert.Equal(StatusCodes.Status204NoContent, code);
        Assert.True(HarnaisHttp.CookieEfface(contexte), "le cookie n'a pas été effacé");

        // Et la chaîne est réellement morte, pas seulement oubliée du client.
        var apres = HarnaisHttp.ContexteAvecCookie(portee.ServiceProvider, ouverture);
        var (refus, _) = await HarnaisHttp.ExecuterAsync(
            portee.ServiceProvider,
            Rafraichir(portee.ServiceProvider, apres, _maintenant),
            apres
        );

        Assert.Equal(StatusCodes.Status401Unauthorized, refus);
    }

    [Fact]
    public async Task La_deconnexion_reussit_MEME_sur_un_cookie_mort()
    {
        // Un bouton « se déconnecter » qui refuse de marcher pousse à fermer
        // l'onglet — en laissant la session vivante.
        await using var hote = HarnaisHttp.Hote(baseDeDonnees);
        using var portee = hote.Services.CreateScope();
        var contexte = HarnaisHttp.ContexteAvecCookie(portee.ServiceProvider, "un-jeton-invente");

        var (code, _) = await HarnaisHttp.ExecuterAsync(
            portee.ServiceProvider,
            PointsDEntree.DeconnecterAsync(
                portee.ServiceProvider.GetRequiredService<MagasinDeSessions>(),
                new HorlogeFixe(_maintenant),
                contexte,
                CancellationToken.None
            ),
            contexte
        );

        Assert.Equal(StatusCodes.Status204NoContent, code);
        Assert.True(HarnaisHttp.CookieEfface(contexte));
    }

    [Fact]
    public async Task La_deconnexion_TOTALE_sans_identite_est_REFUSEE()
    {
        await using var hote = HarnaisHttp.Hote(baseDeDonnees);
        using var portee = hote.Services.CreateScope();
        var contexte = HarnaisHttp.Contexte(portee.ServiceProvider);

        var (code, _) = await HarnaisHttp.ExecuterAsync(
            portee.ServiceProvider,
            PointsDEntree.DeconnecterPartoutAsync(
                new DemandeurFixe(null),
                portee.ServiceProvider.GetRequiredService<MagasinDeSessions>(),
                new HorlogeFixe(_maintenant),
                contexte,
                CancellationToken.None
            ),
            contexte
        );

        Assert.Equal(StatusCodes.Status401Unauthorized, code);
    }

    [Fact]
    public async Task La_deconnexion_TOTALE_revoque_TOUTES_les_sessions()
    {
        await using var hote = HarnaisHttp.Hote(baseDeDonnees);
        using var portee = hote.Services.CreateScope();
        var magasin = portee.ServiceProvider.GetRequiredService<MagasinDeSessions>();
        var utilisateur = await UtilisateurAsync(portee.ServiceProvider);

        var telephone = await magasin.OuvrirAsync(utilisateur, "telephone", _maintenant);
        var portable = await magasin.OuvrirAsync(utilisateur, "portable", _maintenant);
        var contexte = HarnaisHttp.Contexte(portee.ServiceProvider);

        var (code, _) = await HarnaisHttp.ExecuterAsync(
            portee.ServiceProvider,
            PointsDEntree.DeconnecterPartoutAsync(
                new DemandeurFixe(utilisateur),
                magasin,
                new HorlogeFixe(_maintenant),
                contexte,
                CancellationToken.None
            ),
            contexte
        );

        Assert.Equal(StatusCodes.Status204NoContent, code);

        foreach (var jeton in new[] { telephone.Jeton, portable.Jeton })
        {
            var apres = HarnaisHttp.ContexteAvecCookie(portee.ServiceProvider, jeton);
            var (refus, _) = await HarnaisHttp.ExecuterAsync(
                portee.ServiceProvider,
                Rafraichir(portee.ServiceProvider, apres, _maintenant),
                apres
            );

            Assert.Equal(StatusCodes.Status401Unauthorized, refus);
        }
    }

    // ================================================================
    // Liste des sessions
    // ================================================================

    [Fact]
    public async Task Lister_SANS_identite_est_REFUSE()
    {
        await using var hote = HarnaisHttp.Hote(baseDeDonnees);
        using var portee = hote.Services.CreateScope();

        var (code, _) = await HarnaisHttp.ExecuterAsync(
            portee.ServiceProvider,
            PointsDEntree.ListerAsync(
                new DemandeurFixe(null),
                portee.ServiceProvider.GetRequiredService<MagasinDeSessions>(),
                new HorlogeFixe(_maintenant),
                CancellationToken.None
            )
        );

        Assert.Equal(StatusCodes.Status401Unauthorized, code);
    }

    [Fact]
    public async Task Lister_ne_rend_que_les_sessions_DE_CELUI_QUI_DEMANDE()
    {
        // D36 en pratique : l'identité vient du jeton vérifié, et la liste ne
        // peut donc pas être détournée vers quelqu'un d'autre.
        await using var hote = HarnaisHttp.Hote(baseDeDonnees);
        using var portee = hote.Services.CreateScope();
        var magasin = portee.ServiceProvider.GetRequiredService<MagasinDeSessions>();

        var moi = await UtilisateurAsync(portee.ServiceProvider);
        var autre = await UtilisateurAsync(portee.ServiceProvider);
        await magasin.OuvrirAsync(moi, "la mienne", _maintenant);
        await magasin.OuvrirAsync(autre, "la sienne", _maintenant);

        var (code, corps) = await HarnaisHttp.ExecuterAsync(
            portee.ServiceProvider,
            PointsDEntree.ListerAsync(
                new DemandeurFixe(moi),
                magasin,
                new HorlogeFixe(_maintenant),
                CancellationToken.None
            )
        );

        Assert.Equal(StatusCodes.Status200OK, code);
        Assert.Contains("la mienne", corps, StringComparison.Ordinal);
        Assert.DoesNotContain("la sienne", corps, StringComparison.Ordinal);
    }

    // ================================================================
    // Le harnais
    // ================================================================

    private static Task<IResult> Rafraichir(
        IServiceProvider services,
        HttpContext contexte,
        DateTimeOffset maintenant
    ) =>
        PointsDEntree.RafraichirAsync(
            services.GetRequiredService<MagasinDeSessions>(),
            services.GetRequiredService<SignataireDeJetons>(),
            new HorlogeFixe(maintenant),
            contexte,
            CancellationToken.None
        );

    /// <summary>Un compte neuf et une session ouverte : rend le jeton en clair.</summary>
    private static async Task<string> OuvrirAsync(IServiceProvider services)
    {
        var utilisateur = await UtilisateurAsync(services);
        var ouverture = await services
            .GetRequiredService<MagasinDeSessions>()
            .OuvrirAsync(utilisateur, "epreuve", _maintenant);
        return ouverture.Jeton;
    }

    private static async Task<Guid> UtilisateurAsync(IServiceProvider services)
    {
        var email =
            "rotation-"
            + Guid.NewGuid().ToString("N", System.Globalization.CultureInfo.InvariantCulture)
            + "@exemple.test";
        var utilisateurs = services.GetRequiredService<UserManager<Utilisateur>>();
        var compte = new Utilisateur { UserName = email, Email = email };

        var resultat = await utilisateurs.CreateAsync(compte, _motDePasse);
        Assert.True(
            resultat.Succeeded,
            "le harnais n'a pas pu créer l'utilisateur : "
                + string.Join(", ", resultat.Errors.Select(e => e.Code))
        );

        return compte.Id;
    }
}
