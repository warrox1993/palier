using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Palier.Api.Socle;
using Palier.Infrastructure.Coffre;

namespace Palier.Database.Tests;

/// <summary>
/// Le coffre local — D80. Ce fichier éprouve ce qu'il REFUSE avant ce qu'il
/// permet : un mode de développement n'est acceptable que s'il ne peut pas
/// atteindre la production, et c'est ce refus qui se prouve.
/// </summary>
/// <remarks>
/// Les épreuves de processus — l'API réelle lancée en <c>Production</c> — vivent
/// dans <see cref="DemarrageDeLApiTests" />. Celles-ci éprouvent la décision
/// elle-même, et le chemin de démarrage qui l'emprunte.
/// </remarks>
public sealed class CoffreLocalTests
{
    /// <summary>Trente-deux octets sans signification, en Base64.</summary>
    private static readonly string _cle = Convert.ToBase64String([.. Enumerable.Range(1, 32).Select(i => (byte)i)]);

    // ================================================================
    // Le choix — trois refus, et le défaut inchangé
    // ================================================================

    [Fact]
    public void Sans_PALIER_COFFRE_le_mode_reste_OKMS()
    {
        // Le défaut ne bouge pas : aucune panne, aucune absence ne fait
        // basculer en local. Il faut le demander.
        Assert.Equal(ModeDuCoffre.Okms, CoffreLocal.Choisir(Configuration(), "Development"));
        Assert.Equal(ModeDuCoffre.Okms, CoffreLocal.Choisir(Configuration(), "Production"));
    }

    [Fact]
    public void En_Development_le_mode_local_est_accorde_quand_il_est_demande()
    {
        var configuration = Configuration((CoffreLocal.CleDuMode, "local"));

        Assert.Equal(ModeDuCoffre.Local, CoffreLocal.Choisir(configuration, "Development"));
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    [InlineData("development")]
    [InlineData("Demo")]
    [InlineData("")]
    public void Hors_de_Development_le_mode_local_est_REFUSE(string environnement)
    {
        // `development` en minuscules est dans la liste À DESSEIN : `IsDevelopment`
        // l'accepterait, ce contrôle non. Même règle que `CheminPour`, qui ne
        // devine pas un chemin de coffre sur une faute de casse.
        var configuration = Configuration(
            (CoffreLocal.CleDuMode, "local"),
            (CoffreLocal.CleDeLaCle, _cle)
        );

        var refus = Assert.Throws<InvalidOperationException>(
            () => CoffreLocal.Choisir(configuration, environnement)
        );

        Assert.Contains($"« {environnement} »", refus.Message, StringComparison.Ordinal);
        Assert.Contains("Development", refus.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Local")]
    [InlineData("oui")]
    [InlineData("okms")]
    [InlineData(" ")]
    public void Une_valeur_INCONNUE_de_PALIER_COFFRE_est_refusee(string valeur)
    {
        // Une liste blanche : ce qui n'est pas « local » ne se devine pas. Des
        // espaces ne passent pas pour une absence.
        var configuration = Configuration((CoffreLocal.CleDuMode, valeur));

        var refus = Assert.Throws<InvalidOperationException>(
            () => CoffreLocal.Choisir(configuration, "Development")
        );

        Assert.Contains(CoffreLocal.CleDuMode, refus.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Le_MELANGE_avec_les_variables_d_OKMS_est_refuse_en_les_nommant_sans_leur_valeur()
    {
        var configuration = Configuration(
            (CoffreLocal.CleDuMode, "local"),
            ("OKMS_CLIENT_ID", "EU.compte-de-service"),
            ("OKMS_CLIENT_SECRET", "secret-du-compte-de-service")
        );

        var refus = Assert.Throws<InvalidOperationException>(
            () => CoffreLocal.Choisir(configuration, "Development")
        );

        Assert.Contains("OKMS_CLIENT_SECRET", refus.Message, StringComparison.Ordinal);
        Assert.Contains("OKMS_CLIENT_ID", refus.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-du-compte-de-service", refus.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Le_controle_du_melange_lit_la_liste_des_reglages_et_non_une_copie()
    {
        // Chacune des cinq variables, seule, suffit à refuser. Une seconde liste
        // écrite ici pourrait en oublier une ; celle-ci est LUE.
        Assert.Equal(5, ReglagesDuCoffre.Variables.Count);

        foreach (var variable in ReglagesDuCoffre.Variables)
        {
            var configuration = Configuration((CoffreLocal.CleDuMode, "local"), (variable, "x"));
            Assert.Throws<InvalidOperationException>(
                () => CoffreLocal.Choisir(configuration, "Development")
            );
        }
    }

    // ================================================================
    // Le trousseau local
    // ================================================================

    [Fact]
    public void Sans_PALIER_CLE_LOCALE_le_trousseau_refuse_en_disant_comment_l_engendrer()
    {
        var refus = Assert.Throws<InvalidOperationException>(
            () => CoffreLocal.Trousseau(Configuration())
        );

        Assert.Contains(CoffreLocal.CleDeLaCle, refus.Message, StringComparison.Ordinal);
        Assert.Contains("openssl rand -base64 32", refus.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Une_cle_qui_n_est_pas_du_Base64_est_refusee()
    {
        var refus = Assert.Throws<InvalidOperationException>(
            () => CoffreLocal.Trousseau(Configuration((CoffreLocal.CleDeLaCle, "pas du base64 !")))
        );

        Assert.Contains("Base64", refus.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(16)]
    [InlineData(31)]
    [InlineData(33)]
    public void Une_cle_d_une_autre_TAILLE_que_32_octets_est_refusee(int octets)
    {
        var cle = Convert.ToBase64String(new byte[octets]);

        var refus = Assert.Throws<InvalidOperationException>(
            () => CoffreLocal.Trousseau(Configuration((CoffreLocal.CleDeLaCle, cle)))
        );

        Assert.Contains($"{octets} octet(s)", refus.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Le_trousseau_local_chiffre_et_dechiffre_en_liant_le_proprietaire()
    {
        var trousseau = CoffreLocal.Trousseau(Configuration((CoffreLocal.CleDeLaCle, _cle)));
        var proprietaire = Guid.NewGuid();

        var chiffre = trousseau.Chiffrer("JBSWY3DPEHPK3PXP", proprietaire);

        Assert.Equal("JBSWY3DPEHPK3PXP", trousseau.Dechiffrer(chiffre, proprietaire));
        Assert.True(trousseau.EstAJour(chiffre));

        // La liaison au propriétaire ne dépend pas de l'origine de la clé.
        Assert.ThrowsAny<System.Security.Cryptography.CryptographicException>(
            () => trousseau.Dechiffrer(chiffre, Guid.NewGuid())
        );
    }

    [Fact]
    public void Une_meme_cle_rend_le_MEME_identifiant_d_un_demarrage_a_l_autre()
    {
        // Sans cette stabilité, un secret TOTP chiffré avant un redémarrage
        // nommerait une clé que le trousseau suivant ne connaît plus.
        var avant = CoffreLocal.Trousseau(Configuration((CoffreLocal.CleDeLaCle, _cle)));
        var apres = CoffreLocal.Trousseau(Configuration((CoffreLocal.CleDeLaCle, _cle)));
        var proprietaire = Guid.NewGuid();

        var chiffre = avant.Chiffrer("secret", proprietaire);

        Assert.Equal("secret", apres.Dechiffrer(chiffre, proprietaire));
    }

    [Fact]
    public void Une_AUTRE_cle_ne_dechiffre_pas_et_le_dit()
    {
        var premiere = CoffreLocal.Trousseau(Configuration((CoffreLocal.CleDeLaCle, _cle)));
        var autre = CoffreLocal.Trousseau(
            Configuration((CoffreLocal.CleDeLaCle, Convert.ToBase64String(new byte[32])))
        );
        var proprietaire = Guid.NewGuid();

        var chiffre = premiere.Chiffrer("secret", proprietaire);

        // Un identifiant différent, donc un refus qui nomme la clé manquante,
        // et non une étiquette AES-GCM qui ne colle pas.
        var refus = Assert.Throws<InvalidOperationException>(
            () => autre.Dechiffrer(chiffre, proprietaire)
        );
        Assert.Contains("Aucune clé de données", refus.Message, StringComparison.Ordinal);
    }

    // ================================================================
    // Le chemin de démarrage — `SourceDesSecrets`, celui que `Program` emprunte
    // ================================================================

    [Fact]
    public void En_PRODUCTION_le_demarrage_refuse_le_coffre_local_meme_complet()
    {
        // Tout ce que le mode local demande est là — la clé comprise. Le refus
        // ne tient donc qu'à l'environnement, et c'est ce qui se prouve.
        var constructeur = Constructeur(
            "Production",
            (CoffreLocal.CleDuMode, "local"),
            (CoffreLocal.CleDeLaCle, _cle)
        );

        var refus = Assert.Throws<InvalidOperationException>(
            () => SourceDesSecrets.Brancher(constructeur)
        );

        Assert.Contains("« Production »", refus.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Sans_PALIER_COFFRE_le_demarrage_exige_toujours_le_vrai_coffre()
    {
        // Le chemin d'avant D80, refus compris : sans les variables d'OKMS, le
        // démarrage s'arrête, en Development comme ailleurs.
        var refus = Assert.Throws<InvalidOperationException>(
            () => SourceDesSecrets.Brancher(Constructeur("Development"))
        );

        Assert.Contains("Le coffre ne peut pas s'ouvrir", refus.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void En_Development_le_mode_local_n_ajoute_AUCUNE_source_de_configuration()
    {
        var constructeur = Constructeur("Development", (CoffreLocal.CleDuMode, "local"));
        var sources = constructeur.Configuration.Sources.Count;

        Assert.Equal(ModeDuCoffre.Local, SourceDesSecrets.Brancher(constructeur));
        Assert.Equal(sources, constructeur.Configuration.Sources.Count);
    }

    [Fact]
    public async Task En_mode_local_le_trousseau_est_POSE_avant_le_port()
    {
        var constructeur = Constructeur(
            "Development",
            (CoffreLocal.CleDuMode, "local"),
            (CoffreLocal.CleDeLaCle, _cle)
        );
        constructeur.Services.AddSingleton<PorteurDeTrousseau>();
        await using var application = constructeur.Build();
        var porteur = application.Services.GetRequiredService<PorteurDeTrousseau>();

        // Avant : le porteur refuse, comme il doit le faire hors des épreuves.
        Assert.Throws<InvalidOperationException>(() => porteur.Trousseau);

        await SourceDesSecrets.ChargerLeTrousseauAsync(
            application,
            ModeDuCoffre.Local,
            CancellationToken.None
        );

        var proprietaire = Guid.NewGuid();
        Assert.Equal(
            "secret",
            porteur.Trousseau.Dechiffrer(porteur.Trousseau.Chiffrer("secret", proprietaire), proprietaire)
        );
    }

    // ================================================================

    private static IConfiguration Configuration(params (string Cle, string Valeur)[] paires) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(paires.Select(p => new KeyValuePair<string, string?>(p.Cle, p.Valeur)))
            .Build();

    /// <summary>
    /// Un constructeur dont la configuration ne porte QUE ce qu'on lui donne :
    /// une variable présente sur le poste ne doit ni provoquer ni masquer un
    /// refus.
    /// </summary>
    private static WebApplicationBuilder Constructeur(
        string environnement,
        params (string Cle, string Valeur)[] paires
    )
    {
        var constructeur = WebApplication.CreateBuilder(
            new WebApplicationOptions { EnvironmentName = environnement }
        );
        constructeur.Configuration.Sources.Clear();
        constructeur.Configuration.AddInMemoryCollection(
            paires.Select(p => new KeyValuePair<string, string?>(p.Cle, p.Valeur))
        );
        return constructeur;
    }
}
