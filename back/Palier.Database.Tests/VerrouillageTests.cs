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
    // La concurrence — CE QUE LA DÉCISION PURE NE PEUT PAS DIRE
    // ================================================================

    [Fact]
    public async Task Cinq_echecs_SIMULTANES_comptent_pour_CINQ()
    {
        // Le compteur d'échecs est un lire-modifier-écrire. Sans verrou de
        // ligne, cinq tentatives simultanées lisent toutes le MÊME compteur :
        // une seule écriture atterrit, les quatre autres tombent sur le
        // `ConcurrencyStamp` et crient — sans rien compter.
        //
        // Le verrouillage par compte est la seule défense que la conception
        // nomme contre un bourrage d'identifiants distribué : un attaquant
        // multipliait donc ses essais disponibles par la largeur de sa rafale.
        //
        // Mesuré sur ce dépôt AVANT correction : compteur = 1, perdues = 4.
        await using var hote = HarnaisHttp.Hote(baseDeDonnees);
        using var portee = hote.Services.CreateScope();
        var email = await InscritAsync(portee.ServiceProvider);

        var issues = await EnRafaleAsync(
            portee.ServiceProvider,
            email,
            _maintenant,
            Enumerable
                .Repeat(_mauvais, DecisionDeVerrouillage.EchecsAvantVerrouillage)
                .ToArray()
        );

        var perdues = issues.Count(i => i is null);
        var compte = await CompteAsync(portee.ServiceProvider, email);

        Assert.True(
            perdues == 0,
            $"{perdues} tentative(s) sur {issues.Length} ont crié sans rien enregistrer"
        );
        Assert.True(
            compte.VerrouillagesSubis == 1 && compte.LockoutEnd is not null,
            $"cinq échecs simultanés ont produit {compte.VerrouillagesSubis} verrouillage(s), "
                + $"un compteur à {compte.AccessFailedCount} et une échéance "
                + $"{(compte.LockoutEnd is null ? "absente" : "posée")}"
        );
    }

    [Fact]
    public async Task Une_REUSSITE_qui_court_AVEC_des_echecs_ne_fait_crier_personne()
    {
        // La réussite écrit sur LES MÊMES colonnes que l'échec. Tant que les
        // deux chemins ne sont pas sérialisés, l'un des deux perd son écriture
        // — et le perdant crie, c'est-à-dire répond 500 à une connexion par
        // ailleurs régulière.
        await using var hote = HarnaisHttp.Hote(baseDeDonnees);
        using var portee = hote.Services.CreateScope();
        var email = await InscritAsync(portee.ServiceProvider);

        // Un premier échec, séquentiel : sans lui la réussite sortirait par son
        // raccourci — un compte propre n'a rien à effacer — et ne toucherait
        // jamais la base.
        await ConnecterAsync(portee.ServiceProvider, email, _mauvais, _maintenant);

        // Trois échecs de plus au maximum : le compte ne peut pas se verrouiller
        // pendant la rafale, et la réussite ne peut donc pas être refusée pour
        // une raison étrangère à ce qu'on éprouve ici.
        var issues = await EnRafaleAsync(
            portee.ServiceProvider,
            email,
            _maintenant,
            Enumerable.Repeat(_mauvais, 3).Prepend(_bon).ToArray()
        );

        var perdues = issues.Count(i => i is null);

        Assert.True(perdues == 0, $"{perdues} tentative(s) ont crié sans rien enregistrer");
        Assert.True(
            issues[0] == StatusCodes.Status200OK,
            $"la réussite n'a pas abouti : elle a {(issues[0] is null ? "crié" : "été refusée")}"
        );

        // L'état final est celui d'UN ordre sériel : soit la réussite a écrit la
        // dernière et tout est effacé, soit un échec l'a suivie et la date du
        // dernier échec est posée. Jamais un mélange des deux.
        var compte = await CompteAsync(portee.ServiceProvider, email);
        var toutEfface =
            compte.AccessFailedCount == 0
            && compte.VerrouillagesSubis == 0
            && compte.LockoutEnd is null
            && compte.DernierEchecLe is null;
        var echecPosterieur =
            compte.DernierEchecLe == _maintenant
            && (compte.AccessFailedCount > 0 || compte.VerrouillagesSubis > 0);

        Assert.True(
            toutEfface || echecPosterieur,
            "l'état final ne correspond à AUCUN ordre sériel : compteur "
                + $"{compte.AccessFailedCount}, verrous {compte.VerrouillagesSubis}, échéance "
                + $"{(compte.LockoutEnd is null ? "absente" : "posée")}, dernier échec "
                + $"{(compte.DernierEchecLe is null ? "absent" : "posé")}"
        );
    }

    [Fact]
    public async Task Une_REUSSITE_decide_sur_l_etat_RELU_et_non_sur_l_exemplaire_charge()
    {
        // LA CONTREPARTIE DE LA CORRECTION, ET ELLE EST ASSUMÉE.
        //
        // La connexion charge son exemplaire du compte AVANT de vérifier le mot
        // de passe. Des échecs peuvent atterrir pendant ce temps, et verrouiller
        // le compte. La réussite ne décide donc PAS sur ce qu'elle a chargé :
        // elle reprend la ligne sous verrou, relit son état, et applique
        // `ApresUneReussite` — qui efface tout, délibérément.
        //
        // Avant correction, cette même séquence faisait CRIER la réussite
        // (`ConcurrencyFailure`, donc 500) et laissait le verrou en place.
        await using var hote = HarnaisHttp.Hote(baseDeDonnees);
        using var portee = hote.Services.CreateScope();
        var email = await InscritAsync(portee.ServiceProvider);

        // Un échec d'abord : le compte n'est plus vierge, donc la réussite ne
        // sortira pas par son raccourci.
        await ConnecterAsync(portee.ServiceProvider, email, _mauvais, _maintenant);

        // La portée de la réussite charge SON exemplaire, ici et maintenant.
        using var porteeDeLaReussite = portee
            .ServiceProvider.GetRequiredService<IServiceScopeFactory>()
            .CreateScope();
        var charge = await porteeDeLaReussite
            .ServiceProvider.GetRequiredService<UserManager<Utilisateur>>()
            .FindByEmailAsync(email);
        Assert.NotNull(charge);

        // Quatre échecs de plus atterrissent APRÈS cette lecture : le compte est
        // verrouillé, et l'exemplaire chargé l'ignore.
        for (var i = 0; i < DecisionDeVerrouillage.EchecsAvantVerrouillage - 1; i++)
        {
            await ConnecterAsync(portee.ServiceProvider, email, _mauvais, _maintenant);
        }

        var verrouille = await CompteAsync(portee.ServiceProvider, email);
        Assert.True(verrouille.LockoutEnd is not null, "le compte n'a pas été verrouillé");

        await porteeDeLaReussite
            .ServiceProvider.GetRequiredService<GardienDeVerrouillage>()
            .EnregistrerUneReussiteAsync(charge);

        var apres = await CompteAsync(portee.ServiceProvider, email);

        Assert.Null(apres.LockoutEnd);
        Assert.Equal(0, apres.VerrouillagesSubis);
        Assert.Equal(0, apres.AccessFailedCount);
        Assert.Null(apres.DernierEchecLe);
    }

    [Fact]
    public async Task Un_verrouillage_qui_ne_trouve_PLUS_son_compte_CRIE()
    {
        // Quatrième question du franchissement : un contrôle qui n'a pas pris
        // effet doit crier. La ligne verrouillée peut avoir disparu entre la
        // lecture et l'écriture — un compte supprimé pendant une connexion — et
        // un gardien qui se tairait laisserait croire le compteur à jour.
        await using var hote = HarnaisHttp.Hote(baseDeDonnees);
        using var portee = hote.Services.CreateScope();

        var fantome = new Utilisateur { Id = Guid.NewGuid() };

        var cri = await Assert.ThrowsAsync<InvalidOperationException>(
            () =>
                portee
                    .ServiceProvider.GetRequiredService<GardienDeVerrouillage>()
                    .EnregistrerUnEchecAsync(fantome, _maintenant)
        );

        Assert.Contains("n'est PAS protégé", cri.Message, StringComparison.Ordinal);
        Assert.Contains("a disparu", cri.Message, StringComparison.Ordinal);
    }

    // ================================================================
    // Le harnais
    // ================================================================

    /// <summary>
    /// Une rafale de tentatives SIMULTANÉES sur le même compte. Rend le code
    /// HTTP de chacune, dans l'ordre des mots de passe présentés, ou
    /// <c>null</c> pour celles qui ont crié sans rien enregistrer.
    /// </summary>
    private static async Task<int?[]> EnRafaleAsync(
        IServiceProvider services,
        string email,
        DateTimeOffset maintenant,
        string[] motsDePasse
    ) =>
        await Task.WhenAll(
            motsDePasse.Select(motDePasse =>
                TenterAsync(services, email, motDePasse, maintenant)
            )
        );

    private static async Task<int?> TenterAsync(
        IServiceProvider services,
        string email,
        string motDePasse,
        DateTimeOffset maintenant
    )
    {
        try
        {
            var (code, _) = await ConnecterAsync(services, email, motDePasse, maintenant);
            return code;
        }
        catch (InvalidOperationException)
        {
            // Le cri du gardien : cette tentative n'a RIEN enregistré. C'est
            // exactement ce que les épreuves ci-dessus comptent, et le laisser
            // remonter ferait échouer la rafale sans dire combien.
            return null;
        }
    }

    [Fact]
    public async Task Une_ECRITURE_PERIMEE_d_Identity_ne_peut_PAS_effacer_le_verrouillage()
    {
        // Le verrou de ligne ferme la course ENTRE écritures de verrouillage.
        // Il ne dit rien des AUTRES écritures d'Identity — et il y en a, sur des
        // routes qui n'exigent aucun mot de passe.
        //
        // `UserManager.UpdateAsync` renouvelait le jeton de concurrence à chaque
        // écriture, si bien qu'un exemplaire périmé se faisait refuser. Passer à
        // `ExecuteUpdateAsync` sans reprendre ce geste retirerait cette
        // protection : `UserStore.UpdateAsync` marque TOUTES les colonnes
        // modifiées, donc une requête 2FA chargée avant le verrouillage
        // réécrirait sa copie par-dessus — `LockoutEnd` à nul, et
        // `VerrouillagesSubis` à zéro, ce qui ramène l'escalade à son premier
        // palier pour toujours.
        //
        // Cette épreuve tient la ligne `.SetProperty(x => x.ConcurrencyStamp, …)`.
        await using var hote = HarnaisHttp.Hote(baseDeDonnees);
        using var portee = hote.Services.CreateScope();
        var email = await InscritAsync(portee.ServiceProvider);

        // Un exemplaire chargé AVANT le verrouillage, comme le ferait une route
        // 2FA au début de sa requête.
        using var porteePerimee = portee
            .ServiceProvider.GetRequiredService<IServiceScopeFactory>()
            .CreateScope();
        var perime = await porteePerimee
            .ServiceProvider.GetRequiredService<UserManager<Utilisateur>>()
            .FindByEmailAsync(email);
        Assert.NotNull(perime);

        for (var i = 0; i < DecisionDeVerrouillage.EchecsAvantVerrouillage; i++)
        {
            await ConnecterAsync(portee.ServiceProvider, email, _mauvais, _maintenant);
        }

        Assert.NotNull((await CompteAsync(portee.ServiceProvider, email)).LockoutEnd);

        // L'exemplaire périmé écrit maintenant, par le chemin d'Identity.
        var ecrit = await porteePerimee
            .ServiceProvider.GetRequiredService<UserManager<Utilisateur>>()
            .UpdateAsync(perime);

        Assert.False(
            ecrit.Succeeded,
            "une écriture partie d'un exemplaire périmé a été acceptée : le jeton de "
                + "concurrence ne protège plus les colonnes de verrouillage, et une route "
                + "qui n'exige aucun mot de passe peut effacer un verrouillage."
        );

        var apres = await CompteAsync(portee.ServiceProvider, email);
        Assert.NotNull(apres.LockoutEnd);
        Assert.Equal(1, apres.VerrouillagesSubis);
    }

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
