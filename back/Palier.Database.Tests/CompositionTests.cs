using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Palier.Api;
using Palier.Api.Auth;
using Palier.Application.Pipeline;

namespace Palier.Database.Tests;

/// <summary>
/// Ce que la composition REFUSE. Un secret absent doit arrêter le démarrage en
/// nommant sa variable — pas produire une API qui répond et échoue à la
/// première connexion, quand plus personne ne regarde les journaux de
/// démarrage.
/// </summary>
public sealed class CompositionTests
{
    // Quarante-quatre signes : au-delà des trente-deux octets exigés.
    private const string _cleValide = "cle-de-signature-des-epreuves-du-lot-quatre-";
    private const string _sansIdentifiants = "Host=127.0.0.1";

    [Fact]
    public void Sans_chaine_Palier_le_demarrage_est_REFUSE()
    {
        var refus = Assert.Throws<InvalidOperationException>(() =>
            Composition.Composer(Constructeur(palier: null))
        );

        Assert.Contains("ConnectionStrings__Palier", refus.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Sans_chaine_PalierAuth_le_demarrage_est_REFUSE()
    {
        var refus = Assert.Throws<InvalidOperationException>(() =>
            Composition.Composer(Constructeur(auth: null))
        );

        Assert.Contains("ConnectionStrings__PalierAuth", refus.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Sans_JWT_SIGNING_KEY_le_demarrage_est_REFUSE()
    {
        var refus = Assert.Throws<InvalidOperationException>(() =>
            Composition.Composer(Constructeur(cle: null))
        );

        Assert.Contains("JWT_SIGNING_KEY", refus.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Une_JWT_SIGNING_KEY_TROP_COURTE_est_refusee_au_DEMARRAGE()
    {
        // Le cas vicieux : la variable est là, l'API démarre, et la première
        // connexion lève un message de bibliothèque qui ne nomme ni la variable
        // ni la longueur. Le refus doit tomber ici.
        var refus = Assert.Throws<InvalidOperationException>(() =>
            Composition.Composer(Constructeur(cle: "trop-courte"))
        );

        Assert.Contains("JWT_SIGNING_KEY", refus.Message, StringComparison.Ordinal);
        Assert.Contains("32", refus.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Le_demandeur_resolu_lit_le_JETON_et_non_le_bouchon_du_lot_2()
    {
        // Le franchissement qui compte : « un réglage accepté n'est pas un
        // réglage appliqué ». On interroge le conteneur, on ne relit pas la
        // ligne d'enregistrement.
        var constructeur = Constructeur();
        Composition.Composer(constructeur);

        using var fournisseur = constructeur.Services.BuildServiceProvider();
        using var portee = fournisseur.CreateScope();

        Assert.IsType<IdentiteDepuisJeton>(
            portee.ServiceProvider.GetRequiredService<IIdentiteDemandeur>()
        );
    }

    /// <summary>
    /// Un constructeur dont la configuration ne porte QUE ce qu'on lui donne.
    /// </summary>
    /// <remarks>
    /// <c>Sources.Clear()</c> n'est pas un détail : sans lui, une variable
    /// d'environnement présente sur la machine de développement remplirait le
    /// trou qu'on cherche à provoquer, et les quatre épreuves de refus
    /// resteraient vertes sans jamais rien refuser.
    /// </remarks>
    private static WebApplicationBuilder Constructeur(
        string? palier = _sansIdentifiants,
        string? auth = _sansIdentifiants,
        string? cle = _cleValide
    )
    {
        var constructeur = WebApplication.CreateBuilder();
        constructeur.Configuration.Sources.Clear();

        var valeurs = new Dictionary<string, string?>(StringComparer.Ordinal);
        if (palier is not null)
        {
            valeurs["ConnectionStrings:Palier"] = palier;
        }
        if (auth is not null)
        {
            valeurs["ConnectionStrings:PalierAuth"] = auth;
        }
        if (cle is not null)
        {
            valeurs["JWT_SIGNING_KEY"] = cle;
        }

        constructeur.Configuration.AddInMemoryCollection(valeurs);

        // La preuve que le nettoyage a PRIS EFFET. Sans elle, une source
        // résiduelle rendrait tout ce fichier inoffensif en silence.
        Assert.Equal(palier, constructeur.Configuration.GetConnectionString("Palier"));
        Assert.Equal(cle, constructeur.Configuration["JWT_SIGNING_KEY"]);

        return constructeur;
    }
}
