using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Palier.Api.Auth;
using Palier.Infrastructure.Identite;

namespace Palier.Database.Tests;

/// <summary>
/// La double authentification par TOTP, sur le compte réel.
/// </summary>
/// <remarks>
/// <para>
/// Les codes valides ne sont pas fabriqués à la main : ils viennent de
/// <c>GenerateTwoFactorTokenAsync</c>, la méthode publique d'Identity qui
/// utilise <b>exactement</b> l'implémentation que la vérification emploie.
/// Recoder la RFC 6238 dans l'épreuve reviendrait à éprouver deux
/// implémentations l'une contre l'autre, et une épreuve verte ne dirait plus
/// laquelle est juste.
/// </para>
/// </remarks>
[Collection(BaseFixture.Collection)]
public sealed class DeuxFacteursTests(BaseFixture baseDeDonnees)
{
    private static readonly DateTimeOffset _maintenant = new(2026, 8, 21, 12, 0, 0, TimeSpan.Zero);
    private const string _motDePasse = "brouette-hivernale-38-oscille";

    // ================================================================
    // L'URI d'enrôlement
    // ================================================================

    [Fact]
    public void L_URI_porte_ALGORITHM_DIGITS_et_PERIOD()
    {
        // Ce sont les défauts de la RFC 6238, et la plupart des
        // authentificateurs les supposent quand ils manquent. « La plupart » est
        // le problème : ceux qui ne les supposent pas produisent des codes que
        // le serveur refuse, et l'utilisateur n'a aucun moyen de comprendre.
        var uri = DeuxFacteurs.Uri("quelqu.un@exemple.test", "JBSWY3DPEHPK3PXP");

        Assert.Contains("algorithm=SHA1", uri, StringComparison.Ordinal);
        Assert.Contains("digits=6", uri, StringComparison.Ordinal);
        Assert.Contains("period=30", uri, StringComparison.Ordinal);
        Assert.StartsWith("otpauth://totp/", uri, StringComparison.Ordinal);
        Assert.Contains("secret=JBSWY3DPEHPK3PXP", uri, StringComparison.Ordinal);
        Assert.Contains("issuer=palier", uri, StringComparison.Ordinal);
    }

    [Fact]
    public void L_URI_ECHAPPE_l_adresse()
    {
        // Une adresse porte un `@`, et rien n'interdit qu'elle porte un `+` ou
        // un `&`. Non échappée, elle couperait l'URI en deux et
        // l'authentificateur lirait des paramètres qui n'existent pas.
        var uri = DeuxFacteurs.Uri("prenom+etiquette@exemple.test", "JBSWY3DPEHPK3PXP");

        Assert.DoesNotContain("+etiquette", uri, StringComparison.Ordinal);
        Assert.Contains("%40", uri, StringComparison.Ordinal);
    }

    [Fact]
    public void La_cle_est_GROUPEE_par_quatre()
    {
        // Pour qui saisit à la main, faute de caméra.
        Assert.Equal("JBSW Y3DP EHPK 3PXP", DeuxFacteurs.Grouper("JBSWY3DPEHPK3PXP"));
        Assert.Equal("ABC", DeuxFacteurs.Grouper("ABC"));
        Assert.Equal("ABCD E", DeuxFacteurs.Grouper("ABCDE"));
    }

    // ================================================================
    // Préparer
    // ================================================================

    [Fact]
    public async Task Preparer_SANS_identite_est_REFUSE()
    {
        await using var hote = HarnaisHttp.Hote(baseDeDonnees);
        using var portee = hote.Services.CreateScope();

        var (code, _) = await HarnaisHttp.ExecuterAsync(
            portee.ServiceProvider,
            DeuxFacteurs.PreparerAsync(
                new DemandeurFixe(null),
                portee.ServiceProvider.GetRequiredService<UserManager<Utilisateur>>(),
                CancellationToken.None
            )
        );

        Assert.Equal(StatusCodes.Status401Unauthorized, code);
    }

    [Fact]
    public async Task Preparer_rend_une_cle_NEUVE_a_chaque_appel()
    {
        // Réutiliser une clé existante ferait qu'une préparation abandonnée —
        // l'écran fermé, le QR jamais scanné — laisserait la clé connue de qui
        // l'a vue passer.
        await using var hote = HarnaisHttp.Hote(baseDeDonnees);
        using var portee = hote.Services.CreateScope();
        var compte = await InscritAsync(portee.ServiceProvider);

        var premiere = await PreparerAsync(portee.ServiceProvider, compte);
        var seconde = await PreparerAsync(portee.ServiceProvider, compte);

        Assert.NotEqual(premiere.Cle, seconde.Cle);
    }

    // ================================================================
    // Activer
    // ================================================================

    [Fact]
    public async Task Activer_avec_un_code_FAUX_n_active_RIEN()
    {
        // Sans cette vérification, activer la double authentification sur un QR
        // jamais scanné enfermerait l'utilisateur hors de son propre compte à
        // la connexion suivante.
        await using var hote = HarnaisHttp.Hote(baseDeDonnees);
        using var portee = hote.Services.CreateScope();
        var compte = await InscritAsync(portee.ServiceProvider);
        await PreparerAsync(portee.ServiceProvider, compte);

        var (code, corps) = await ActiverAsync(portee.ServiceProvider, compte, "000000");

        Assert.Equal(StatusCodes.Status400BadRequest, code);
        Assert.Equal("CodeInvalide", HarnaisHttp.Code(corps));
        Assert.False((await CompteAsync(portee.ServiceProvider, compte)).TwoFactorEnabled);
    }

    [Fact]
    public async Task Activer_avec_un_code_valide_rend_DIX_codes_de_recuperation()
    {
        await using var hote = HarnaisHttp.Hote(baseDeDonnees);
        using var portee = hote.Services.CreateScope();
        var compte = await InscritAsync(portee.ServiceProvider);
        await PreparerAsync(portee.ServiceProvider, compte);

        var (code, corps) = await ActiverAsync(
            portee.ServiceProvider,
            compte,
            await CodeValideAsync(portee.ServiceProvider, compte)
        );

        Assert.Equal(StatusCodes.Status200OK, code);
        Assert.True((await CompteAsync(portee.ServiceProvider, compte)).TwoFactorEnabled);

        var codes = System
            .Text.Json.JsonDocument.Parse(corps)
            .RootElement.GetProperty("codes")
            .EnumerateArray()
            .Select(e => e.GetString())
            .ToList();

        Assert.Equal(DeuxFacteurs.NombreDeCodesDeRecuperation, codes.Count);
        Assert.Equal(codes.Count, codes.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public async Task Un_code_ESPACE_est_accepte()
    {
        // Les authentificateurs affichent « 123 456 » pour la lisibilité. Un
        // utilisateur qui recopie l'espace verrait son code refusé sans jamais
        // comprendre pourquoi.
        await using var hote = HarnaisHttp.Hote(baseDeDonnees);
        using var portee = hote.Services.CreateScope();
        var compte = await InscritAsync(portee.ServiceProvider);
        await PreparerAsync(portee.ServiceProvider, compte);

        var brut = await CodeValideAsync(portee.ServiceProvider, compte);
        var espace = brut[..3] + " " + brut[3..];

        var (code, _) = await ActiverAsync(portee.ServiceProvider, compte, espace);

        Assert.Equal(StatusCodes.Status200OK, code);
    }

    // ================================================================
    // Désactiver
    // ================================================================

    [Fact]
    public async Task Desactiver_SANS_code_est_REFUSE()
    {
        // L'épreuve qui compte. Sans cette exigence, un jeton d'accès volé
        // suffirait à retirer la double authentification — puis à s'installer.
        // Une protection ne doit pas se démonter avec la seule chose contre
        // laquelle elle protège.
        await using var hote = HarnaisHttp.Hote(baseDeDonnees);
        using var portee = hote.Services.CreateScope();
        var compte = await ActiveAsync(portee.ServiceProvider);

        var (code, _) = await DesactiverAsync(portee.ServiceProvider, compte, null);

        Assert.Equal(StatusCodes.Status400BadRequest, code);
        Assert.True(
            (await CompteAsync(portee.ServiceProvider, compte)).TwoFactorEnabled,
            "la double authentification a été retirée sans aucune preuve de possession"
        );
    }

    [Fact]
    public async Task Desactiver_avec_un_code_valide_retire_la_protection()
    {
        await using var hote = HarnaisHttp.Hote(baseDeDonnees);
        using var portee = hote.Services.CreateScope();
        var compte = await ActiveAsync(portee.ServiceProvider);

        var (code, _) = await DesactiverAsync(
            portee.ServiceProvider,
            compte,
            await CodeValideAsync(portee.ServiceProvider, compte)
        );

        Assert.Equal(StatusCodes.Status204NoContent, code);
        Assert.False((await CompteAsync(portee.ServiceProvider, compte)).TwoFactorEnabled);
    }

    // ================================================================
    // La connexion
    // ================================================================

    [Fact]
    public async Task Une_connexion_SANS_code_est_refusee_quand_la_2FA_est_active()
    {
        // Sans cette porte, la double authentification serait décorative : elle
        // s'afficherait dans les réglages et n'empêcherait rien.
        await using var hote = HarnaisHttp.Hote(baseDeDonnees);
        using var portee = hote.Services.CreateScope();
        var compte = await ActiveAsync(portee.ServiceProvider);

        var (code, corps) = await ConnecterAsync(portee.ServiceProvider, compte, null);

        Assert.Equal(StatusCodes.Status401Unauthorized, code);
        Assert.Equal("DeuxFacteursRequis", HarnaisHttp.Code(corps));
    }

    [Fact]
    public async Task Une_connexion_AVEC_le_bon_code_reussit()
    {
        await using var hote = HarnaisHttp.Hote(baseDeDonnees);
        using var portee = hote.Services.CreateScope();
        var compte = await ActiveAsync(portee.ServiceProvider);

        var (code, _) = await ConnecterAsync(
            portee.ServiceProvider,
            compte,
            await CodeValideAsync(portee.ServiceProvider, compte)
        );

        Assert.Equal(StatusCodes.Status200OK, code);
    }

    [Fact]
    public async Task Un_CODE_DE_RECUPERATION_ouvre_la_session_et_est_CONSOMME()
    {
        // Le jour du téléphone perdu est le seul jour où ces codes comptent. Ne
        // vérifier que l'authentificateur les rendrait décoratifs.
        await using var hote = HarnaisHttp.Hote(baseDeDonnees);
        using var portee = hote.Services.CreateScope();
        var compte = await InscritAsync(portee.ServiceProvider);
        await PreparerAsync(portee.ServiceProvider, compte);

        var (_, corps) = await ActiverAsync(
            portee.ServiceProvider,
            compte,
            await CodeValideAsync(portee.ServiceProvider, compte)
        );
        var secours = System
            .Text.Json.JsonDocument.Parse(corps)
            .RootElement.GetProperty("codes")[0]
            .GetString();

        var premier = await ConnecterAsync(portee.ServiceProvider, compte, secours);
        Assert.Equal(StatusCodes.Status200OK, premier.Code);

        // Et il ne resservira pas : un code de secours qui resterait valable ne
        // serait qu'un second mot de passe, plus court.
        var second = await ConnecterAsync(portee.ServiceProvider, compte, secours);
        Assert.Equal(StatusCodes.Status401Unauthorized, second.Code);
    }

    [Fact]
    public async Task Un_code_de_2FA_FAUX_compte_comme_un_ECHEC()
    {
        // Sans cela, la limitation par compte s'arrêterait au mot de passe, et
        // six chiffres seraient devinables en un million d'essais — sans jamais
        // verrouiller.
        await using var hote = HarnaisHttp.Hote(baseDeDonnees);
        using var portee = hote.Services.CreateScope();
        var compte = await ActiveAsync(portee.ServiceProvider);

        await ConnecterAsync(portee.ServiceProvider, compte, "000000");

        Assert.Equal(1, (await CompteAsync(portee.ServiceProvider, compte)).AccessFailedCount);
    }

    // ================================================================
    // Le harnais
    // ================================================================

    private static async Task<PreparationDeDeuxFacteurs> PreparerAsync(
        IServiceProvider services,
        Guid compte
    )
    {
        using var portee = Portee(services);
        var (code, corps) = await HarnaisHttp.ExecuterAsync(
            portee.ServiceProvider,
            DeuxFacteurs.PreparerAsync(
                new DemandeurFixe(compte),
                portee.ServiceProvider.GetRequiredService<UserManager<Utilisateur>>(),
                CancellationToken.None
            )
        );

        Assert.Equal(StatusCodes.Status200OK, code);
        var racine = System.Text.Json.JsonDocument.Parse(corps).RootElement;

        return new PreparationDeDeuxFacteurs(
            racine.GetProperty("uri").GetString() ?? string.Empty,
            racine.GetProperty("cle").GetString() ?? string.Empty
        );
    }

    private static async Task<(int Code, string Corps)> ActiverAsync(
        IServiceProvider services,
        Guid compte,
        string? code
    )
    {
        using var portee = Portee(services);
        return await HarnaisHttp.ExecuterAsync(
            portee.ServiceProvider,
            DeuxFacteurs.ActiverAsync(
                new DemandeDeCode(code),
                new DemandeurFixe(compte),
                portee.ServiceProvider.GetRequiredService<UserManager<Utilisateur>>(),
                CancellationToken.None
            )
        );
    }

    private static async Task<(int Code, string Corps)> DesactiverAsync(
        IServiceProvider services,
        Guid compte,
        string? code
    )
    {
        using var portee = Portee(services);
        return await HarnaisHttp.ExecuterAsync(
            portee.ServiceProvider,
            DeuxFacteurs.DesactiverAsync(
                new DemandeDeCode(code),
                new DemandeurFixe(compte),
                portee.ServiceProvider.GetRequiredService<UserManager<Utilisateur>>(),
                CancellationToken.None
            )
        );
    }

    private static async Task<(int Code, string Corps)> ConnecterAsync(
        IServiceProvider services,
        Guid compte,
        string? codeDeDeuxFacteurs
    )
    {
        using var portee = Portee(services);
        var utilisateur = await CompteAsync(services, compte);
        var contexte = HarnaisHttp.Contexte(portee.ServiceProvider);

        return await HarnaisHttp.ExecuterAsync(
            portee.ServiceProvider,
            PointsDEntree.ConnecterAsync(
                new DemandeDIdentifiants(utilisateur.Email, _motDePasse, codeDeDeuxFacteurs),
                portee.ServiceProvider.GetRequiredService<UserManager<Utilisateur>>(),
                portee.ServiceProvider.GetRequiredService<MagasinDeSessions>(),
                portee.ServiceProvider.GetRequiredService<SignataireDeJetons>(),
                portee.ServiceProvider.GetRequiredService<GardienDeVerrouillage>(),
                portee.ServiceProvider.GetRequiredService<IPasswordHasher<Utilisateur>>(),
                new HorlogeFixe(_maintenant),
                contexte,
                CancellationToken.None
            ),
            contexte
        );
    }

    /// <summary>Un code TOTP valide, calculé comme le ferait l'authentificateur.</summary>
    /// <remarks>
    /// <para>
    /// <b>Pourquoi la RFC est réimplémentée ici, contre l'habitude de ce dépôt.</b>
    /// <c>GenerateTwoFactorTokenAsync</c> semblait le chemin évident, et il ne
    /// l'est pas : <c>AuthenticatorTokenProvider.GenerateAsync</c> rend
    /// <b>toujours</b> la chaîne vide, par conception — le provider ne génère
    /// rien, il ne fait que valider. C'est l'application authentificatrice qui
    /// calcule le code.
    /// </para>
    ///
    /// <para>
    /// Mesuré le 21/08/2026 : sept épreuves rouges sur « le code engendré fait
    /// 0 signe ». L'assertion de longueur ci-dessous reste, pour que ce chemin
    /// ne redevienne jamais silencieux.
    /// </para>
    ///
    /// <para>
    /// Ce calcul tient <b>le rôle du téléphone</b>, pas celui du serveur : c'est
    /// donc bien la vérification d'Identity qui est éprouvée, contre un code
    /// produit indépendamment d'elle.
    /// </para>
    /// </remarks>
    private static async Task<string> CodeValideAsync(IServiceProvider services, Guid compte)
    {
        using var portee = Portee(services);
        var utilisateurs = portee.ServiceProvider.GetRequiredService<UserManager<Utilisateur>>();
        var utilisateur = await utilisateurs.FindByIdAsync(
            compte.ToString("D", System.Globalization.CultureInfo.InvariantCulture)
        );

        Assert.True(utilisateur is not null, $"le compte {compte} a disparu");

        var cle = await utilisateurs.GetAuthenticatorKeyAsync(utilisateur);
        Assert.False(
            string.IsNullOrEmpty(cle),
            "la clé d'authentificateur est absente : toutes les épreuves qui suivent "
                + "vérifieraient un code contre un compte sans clé."
        );

        var code = Totp(DecoderBase32(cle), DateTimeOffset.UtcNow);

        Assert.True(
            code.Length == DeuxFacteurs.Chiffres,
            $"le code calculé fait {code.Length} signe(s) au lieu de {DeuxFacteurs.Chiffres}."
        );

        return code;
    }

    /// <summary>RFC 6238, HMAC-SHA1, pas de trente secondes, six chiffres.</summary>
    private static string Totp(byte[] cle, DateTimeOffset instant)
    {
        var pas = instant.ToUnixTimeSeconds() / DeuxFacteurs.PeriodeEnSecondes;
        var compteur = System.Buffers.Binary.BinaryPrimitives.ReverseEndianness(pas);

#pragma warning disable CA5350 // La RFC 6238 impose HMAC-SHA1 ; voir DeuxFacteurs.Algorithme.
        var empreinte = System.Security.Cryptography.HMACSHA1.HashData(
            cle,
            BitConverter.GetBytes(compteur)
        );
#pragma warning restore CA5350

        // Troncature dynamique : les quatre bits de poids faible du dernier
        // octet donnent le décalage du mot de quatre octets à lire.
        var decalage = empreinte[^1] & 0x0F;
        var binaire =
            ((empreinte[decalage] & 0x7F) << 24)
            | ((empreinte[decalage + 1] & 0xFF) << 16)
            | ((empreinte[decalage + 2] & 0xFF) << 8)
            | (empreinte[decalage + 3] & 0xFF);

        return (binaire % 1_000_000).ToString(
            "D6",
            System.Globalization.CultureInfo.InvariantCulture
        );
    }

    /// <summary>Base32 RFC 4648, l'encodage qu'Identity emploie pour la clé.</summary>
    private static byte[] DecoderBase32(string cle)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

        var octets = new List<byte>(cle.Length * 5 / 8);
        var tampon = 0;
        var bits = 0;

        foreach (var signe in cle.TrimEnd('=').ToUpperInvariant())
        {
            var valeur = alphabet.IndexOf(signe, StringComparison.Ordinal);
            Assert.True(valeur >= 0, $"« {signe} » n'appartient pas à l'alphabet base32.");

            tampon = (tampon << 5) | valeur;
            bits += 5;

            if (bits >= 8)
            {
                bits -= 8;
                octets.Add((byte)((tampon >> bits) & 0xFF));
            }
        }

        return [.. octets];
    }

    /// <summary>Un compte dont la double authentification est ACTIVE.</summary>
    private static async Task<Guid> ActiveAsync(IServiceProvider services)
    {
        var compte = await InscritAsync(services);
        await PreparerAsync(services, compte);

        var (code, _) = await ActiverAsync(
            services,
            compte,
            await CodeValideAsync(services, compte)
        );
        Assert.Equal(StatusCodes.Status200OK, code);

        return compte;
    }

    private static async Task<Utilisateur> CompteAsync(IServiceProvider services, Guid compte)
    {
        using var portee = Portee(services);
        var utilisateur = await portee
            .ServiceProvider.GetRequiredService<UserManager<Utilisateur>>()
            .FindByIdAsync(compte.ToString("D", System.Globalization.CultureInfo.InvariantCulture));

        Assert.True(utilisateur is not null, $"le compte {compte} a disparu");
        return utilisateur;
    }

    private static async Task<Guid> InscritAsync(IServiceProvider services)
    {
        var email =
            "totp-"
            + Guid.NewGuid().ToString("N", System.Globalization.CultureInfo.InvariantCulture)
            + "@exemple.test";

        using var portee = Portee(services);
        var compte = new Utilisateur { UserName = email, Email = email };
        var resultat = await portee
            .ServiceProvider.GetRequiredService<UserManager<Utilisateur>>()
            .CreateAsync(compte, _motDePasse);

        Assert.True(
            resultat.Succeeded,
            "le harnais n'a pas pu créer l'utilisateur : "
                + string.Join(", ", resultat.Errors.Select(e => e.Code))
        );
        return compte.Id;
    }

    /// <summary>
    /// Une portée NEUVE par appel : sans elle, le suivi d'EF servirait
    /// l'utilisateur déjà chargé et l'épreuve lirait son propre cache au lieu
    /// de la base.
    /// </summary>
    private static IServiceScope Portee(IServiceProvider services) =>
        services.GetRequiredService<IServiceScopeFactory>().CreateScope();
}
