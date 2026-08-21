using Microsoft.AspNetCore.Builder;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Palier.Api.Auth;
using Palier.Application.Pipeline;
using Palier.Infrastructure.Identite;

namespace Palier.Database.Tests;

/// <summary>
/// Les deux verrous de la porte nutrition, éprouvés par le SERVICE
/// d'autorisation réel — pas par la règle pure, qui l'est ailleurs.
/// </summary>
/// <remarks>
/// <para>
/// Ce que la règle pure ne peut pas dire : que les politiques sont
/// <b>enregistrées</b>, que le gestionnaire est <b>appelé</b>, et qu'il lit les
/// bons drapeaux sur le bon compte. Un réglage accepté n'est pas un réglage
/// appliqué.
/// </para>
/// </remarks>
[Collection(BaseFixture.Collection)]
public sealed class PolitiquesDAutorisationTests(BaseFixture baseDeDonnees)
{
    private const string _motDePasse = "brouette-hivernale-38-oscille";

    [Theory]
    [InlineData(true, true, true)] // adresse vérifiée + consentement → ouverte
    [InlineData(false, true, false)] // adresse non vérifiée          → fermée
    [InlineData(true, false, false)] // consentement refusé           → fermée
    [InlineData(false, false, false)] // ni l'un ni l'autre           → fermée
    public async Task La_nutrition_exige_les_DEUX_verrous(
        bool emailVerifie,
        bool consentement,
        bool ouverte
    )
    {
        var demandeur = new DemandeurMutable();
        await using var hote = Hote(demandeur);

        demandeur.Identifiant = await CompteAsync(hote.Services, emailVerifie, consentement);

        Assert.Equal(
            ouverte,
            await AutoriseAsync(hote.Services, PolitiquesDAutorisation.Nutrition)
        );
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task L_entrainement_reste_ouvert_dans_TOUS_les_cas(
        bool emailVerifie,
        bool consentement
    )
    {
        // docs/09-comptes.md § 2 : « en cas de refus : accès à l'entraînement ».
        // Un verrou qui fermerait aussi l'entraînement serait une régression
        // produit — et le genre qu'on ne remarque pas, parce qu'elle ressemble
        // à de la prudence.
        var demandeur = new DemandeurMutable();
        await using var hote = Hote(demandeur);

        demandeur.Identifiant = await CompteAsync(hote.Services, emailVerifie, consentement);

        Assert.True(
            await AutoriseAsync(hote.Services, PolitiquesDAutorisation.Entrainement),
            $"l'entraînement s'est fermé sur (email={emailVerifie}, consentement={consentement}) : "
                + "le produit refuse à quelqu'un de noter sa séance."
        );
    }

    [Fact]
    public async Task SANS_identite_les_DEUX_portes_sont_fermees()
    {
        // Y compris l'entraînement : « ouvert dans tous les cas » veut dire
        // « quels que soient les deux drapeaux », pas « ouvert à un anonyme ».
        var demandeur = new DemandeurMutable { Identifiant = null };
        await using var hote = Hote(demandeur);

        Assert.False(await AutoriseAsync(hote.Services, PolitiquesDAutorisation.Nutrition));
        Assert.False(await AutoriseAsync(hote.Services, PolitiquesDAutorisation.Entrainement));
    }

    [Fact]
    public async Task Un_compte_DISPARU_ferme_les_deux_portes()
    {
        // Un jeton reste valide quinze minutes après la suppression du compte.
        // Pendant ce quart d'heure, l'identité existe et le compte, non.
        var demandeur = new DemandeurMutable { Identifiant = Guid.NewGuid() };
        await using var hote = Hote(demandeur);

        Assert.False(await AutoriseAsync(hote.Services, PolitiquesDAutorisation.Nutrition));
        Assert.False(await AutoriseAsync(hote.Services, PolitiquesDAutorisation.Entrainement));
    }

    [Fact]
    public async Task Les_DEUX_politiques_sont_bien_ENREGISTREES()
    {
        // Quatrième question du franchissement : un contrôle qui n'a plus de
        // cible doit crier. Sans cette épreuve, une politique renommée ferait
        // lever toutes les autres avec le même message qu'un refus mal câblé.
        var demandeur = new DemandeurMutable();
        await using var hote = Hote(demandeur);
        var fournisseur = hote.Services.GetRequiredService<IAuthorizationPolicyProvider>();

        Assert.NotNull(await fournisseur.GetPolicyAsync(PolitiquesDAutorisation.Nutrition));
        Assert.NotNull(await fournisseur.GetPolicyAsync(PolitiquesDAutorisation.Entrainement));
    }

    // ================================================================
    // Le harnais
    // ================================================================

    /// <summary>
    /// L'API réelle, dont seule la LECTURE de l'identité est remplacée.
    /// </summary>
    /// <remarks>
    /// Ce qui est éprouvé ici est la règle appliquée à une identité, pas la
    /// façon de lire l'identité dans un jeton — celle-là l'est dans
    /// <c>IdentiteDepuisJetonTests</c>. Le reste de la composition, gestionnaire
    /// d'autorisation compris, est celui de production.
    /// </remarks>
    private WebApplication Hote(DemandeurMutable demandeur) =>
        HarnaisHttp.Hote(
            baseDeDonnees,
            services => services.AddSingleton<IIdentiteDemandeur>(demandeur)
        );

    /// <summary>Interroge le service d'autorisation RÉEL.</summary>
    private static async Task<bool> AutoriseAsync(IServiceProvider services, string politique)
    {
        using var portee = services.GetRequiredService<IServiceScopeFactory>().CreateScope();

        // Le principal est authentifié : ce que ces épreuves mesurent est le
        // second étage — la règle du domaine — et non le premier, qui est
        // l'authentification elle-même.
        var principal = new ClaimsPrincipal(new ClaimsIdentity([], "Epreuve"));

        var resultat = await portee
            .ServiceProvider.GetRequiredService<IAuthorizationService>()
            .AuthorizeAsync(principal, resource: null, politique);

        return resultat.Succeeded;
    }

    private static async Task<Guid> CompteAsync(
        IServiceProvider services,
        bool emailVerifie,
        bool consentement
    )
    {
        using var portee = services.GetRequiredService<IServiceScopeFactory>().CreateScope();
        var utilisateurs = portee.ServiceProvider.GetRequiredService<UserManager<Utilisateur>>();

        var email =
            "porte-"
            + Guid.NewGuid().ToString("N", System.Globalization.CultureInfo.InvariantCulture)
            + "@exemple.test";
        var compte = new Utilisateur
        {
            UserName = email,
            Email = email,
            EmailConfirmed = emailVerifie,
            ConsentementSanteLe = consentement ? DateTimeOffset.UtcNow : null,
        };

        var resultat = await utilisateurs.CreateAsync(compte, _motDePasse);
        Assert.True(
            resultat.Succeeded,
            "le harnais n'a pas pu créer l'utilisateur : "
                + string.Join(", ", resultat.Errors.Select(e => e.Code))
        );

        // La preuve que les deux drapeaux ont PRIS EFFET en base. Sans elle, un
        // mappage manquant sur `ConsentementSanteLe` rendrait toutes les
        // combinaisons identiques, et le tableau ci-dessus mentirait.
        using var relecture = services.GetRequiredService<IServiceScopeFactory>().CreateScope();
        var relu = await relecture
            .ServiceProvider.GetRequiredService<UserManager<Utilisateur>>()
            .FindByIdAsync(compte.Id.ToString("D", System.Globalization.CultureInfo.InvariantCulture));

        Assert.True(relu is not null, "le compte créé est introuvable");
        Assert.Equal(emailVerifie, relu.EmailConfirmed);
        Assert.Equal(consentement, relu.ConsentementSanteLe is not null);

        return compte.Id;
    }
}
