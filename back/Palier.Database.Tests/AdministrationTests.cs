using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Palier.Api.Auth;
using Palier.Application.Pipeline;
using Palier.Infrastructure.Identite;

namespace Palier.Database.Tests;

/// <summary>
/// Le rôle d'administration, que D41 réclamait depuis le lot 4 et que
/// <c>PolitiquesDAutorisation</c> n'avait pas : ses deux politiques étaient des
/// DOMAINES, pas des rôles.
///
/// La liste vit au coffre, par adresse. Pour se promouvoir, il faut donc
/// compromettre le COFFRE — une écriture en base ne suffit pas, là où la table
/// <c>AspNetUserRoles</c> aurait fait de la même compromission un contrôle
/// total du produit.
/// </summary>
[Collection(BaseFixture.Collection)]
public sealed class AdministrationTests(BaseFixture baseDeDonnees)
{
    private const string _motDePasse = "brouette-hivernale-38-oscille";


    // ================================================================
    // Épreuve 1 — la liste ouvre
    // ================================================================

    [Fact]
    public async Task Une_adresse_DE_LA_LISTE_ouvre_l_administration()
    {
        var adresse = AdresseUnique();
        var demandeur = new DemandeurMutable();
        await using var hote = Hote(demandeur, adresse);

        demandeur.Identifiant = await CompteAsync(hote.Services, adresse, verifiee: true);

        Assert.True(await AutoriseAsync(hote.Services));
    }

    // ================================================================
    // Épreuve 2 — et elle seule
    // ================================================================

    [Fact]
    public async Task Une_adresse_ABSENTE_de_la_liste_n_ouvre_PAS_l_administration()
    {
        var demandeur = new DemandeurMutable();
        await using var hote = Hote(demandeur, AdresseUnique());

        demandeur.Identifiant = await CompteAsync(hote.Services, AdresseUnique(), verifiee: true);

        Assert.False(await AutoriseAsync(hote.Services));
    }

    // ================================================================
    // Épreuve 3 — la casse ne fait pas perdre son rôle
    // ================================================================

    [Fact]
    public async Task La_CASSE_de_l_adresse_ne_change_rien()
    {
        // Identity normalise les adresses ; comparer brut ferait qu'un
        // administrateur perdrait son rôle en s'inscrivant « Gardien@… » alors
        // que la liste dit « gardien@… ». Le défaut serait invisible jusqu'au
        // jour où il faut la route de santé.
        var adresse = AdresseUnique();
        var demandeur = new DemandeurMutable();
        await using var hote = Hote(demandeur, adresse.ToUpperInvariant());

        demandeur.Identifiant = await CompteAsync(hote.Services, adresse, verifiee: true);

        Assert.True(await AutoriseAsync(hote.Services));
    }

    // ================================================================
    // Épreuve 4 — celle qui porte la sécurité
    // ================================================================

    [Fact]
    public async Task Une_adresse_NON_VERIFIEE_n_ouvre_PAS_l_administration()
    {
        // L'inscription est ouverte. Sans cette condition, quiconque s'inscrit
        // avec l'adresse d'un administrateur en devient un — sans jamais
        // prouver qu'il la possède.
        var adresse = AdresseUnique();
        var demandeur = new DemandeurMutable();
        await using var hote = Hote(demandeur, adresse);

        demandeur.Identifiant = await CompteAsync(hote.Services, adresse, verifiee: false);

        Assert.False(await AutoriseAsync(hote.Services));
    }

    // ================================================================
    // Épreuve 5 — la borne : une liste vide n'ouvre à personne
    // ================================================================

    [Fact]
    public async Task Une_liste_VIDE_n_ouvre_l_administration_a_PERSONNE()
    {
        // Le piège classique de la liste blanche : « vide » interprété comme
        // « pas de restriction ». `TRUSTED_PROXIES` a payé exactement cette
        // erreur au lot 4, et le commentaire de `.env.example` la raconte.
        var demandeur = new DemandeurMutable();
        await using var hote = Hote(demandeur, "");

        demandeur.Identifiant = await CompteAsync(hote.Services, AdresseUnique(), verifiee: true);

        Assert.False(await AutoriseAsync(hote.Services));
    }

    // ================================================================
    // Épreuve 6 — sans identité, rien
    // ================================================================

    [Fact]
    public async Task Sans_IDENTITE_l_administration_est_fermee()
    {
        var demandeur = new DemandeurMutable();
        await using var hote = Hote(demandeur, AdresseUnique());

        demandeur.Identifiant = null;

        Assert.False(await AutoriseAsync(hote.Services));
    }

    // ================================================================
    // Épreuve 7 — la politique EXISTE, et la route la porte
    // ================================================================

    [Fact]
    public async Task La_politique_d_administration_est_ENREGISTREE()
    {
        // Quatrième question du franchissement : les six épreuves ci-dessus
        // seraient vertes si la politique refusait toujours faute d'exister.
        var demandeur = new DemandeurMutable();
        await using var hote = Hote(demandeur, AdresseUnique());

        var fournisseur = hote.Services.GetRequiredService<IAuthorizationPolicyProvider>();

        Assert.NotNull(
            await fournisseur.GetPolicyAsync(PolitiquesDAutorisation.Administration)
        );
    }

    // ================================================================

    private WebApplication Hote(DemandeurMutable demandeur, string administrateurs) =>
        HarnaisHttp.Hote(
            baseDeDonnees,
            services => services.AddSingleton<IIdentiteDemandeur>(demandeur),
            new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["ADMIN_EMAILS"] = administrateurs,
            }
        );

    /// <summary>
    /// L'adresse est engendrée PAR L'ÉPREUVE et passée aux deux endroits — la
    /// liste et le compte. La rendre unique côté compte seulement la
    /// désalignerait de la liste, et toutes les épreuves passeraient au vert en
    /// refusant systématiquement.
    /// </summary>
    private static string AdresseUnique() =>
        string.Create(CultureInfo.InvariantCulture, $"admin-{Guid.NewGuid():N}@exemple.test");

    private static async Task<bool> AutoriseAsync(IServiceProvider services)
    {
        using var portee = services.GetRequiredService<IServiceScopeFactory>().CreateScope();
        var principal = new ClaimsPrincipal(new ClaimsIdentity([], "Epreuve"));

        var resultat = await portee
            .ServiceProvider.GetRequiredService<IAuthorizationService>()
            .AuthorizeAsync(principal, resource: null, PolitiquesDAutorisation.Administration);

        return resultat.Succeeded;
    }

    private static async Task<Guid> CompteAsync(
        IServiceProvider services,
        string adresse,
        bool verifiee
    )
    {
        using var portee = services.GetRequiredService<IServiceScopeFactory>().CreateScope();
        var utilisateurs = portee.ServiceProvider.GetRequiredService<UserManager<Utilisateur>>();

        var compte = new Utilisateur
        {
            UserName = adresse,
            Email = adresse,
            EmailConfirmed = verifiee,
        };

        var resultat = await utilisateurs.CreateAsync(compte, _motDePasse);
        Assert.True(
            resultat.Succeeded,
            "le harnais n'a pas pu créer l'utilisateur : "
                + string.Join(", ", resultat.Errors.Select(e => e.Code))
        );

        return compte.Id;
    }
}
