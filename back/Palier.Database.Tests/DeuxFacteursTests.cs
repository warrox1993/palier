using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Palier.Api;
using Palier.Api.Auth;
using Palier.Infrastructure.Coffre;
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
                null,
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

    [Fact]
    public async Task Preparer_avec_la_2FA_ACTIVE_et_SANS_preuve_est_REFUSE_et_la_cle_NE_BOUGE_PAS()
    {
        // Préparer REMPLACE la clé : sur un compte déjà protégé, c'est le geste
        // qui DÉTRUIT l'authentificateur de l'utilisateur. Sans preuve de
        // possession, un jeton d'accès volé suffisait à en poser un autre, puis
        // à faire regénérer les dix codes de récupération — ce qui invalide
        // ceux de la victime. Elle ne passait plus sa propre porte.
        await using var hote = HarnaisHttp.Hote(baseDeDonnees);
        using var portee = hote.Services.CreateScope();
        var compte = await ActiveAsync(portee.ServiceProvider);
        var avant = await CleAsync(portee.ServiceProvider, compte);

        var (code, corps) = await PreparerBrutAsync(portee.ServiceProvider, compte, null);

        Assert.Equal(StatusCodes.Status400BadRequest, code);
        Assert.Equal("CodeInvalide", HarnaisHttp.Code(corps));
        Assert.Equal(avant, await CleAsync(portee.ServiceProvider, compte));
    }

    [Fact]
    public async Task Preparer_avec_le_code_de_l_AUTHENTIFICATEUR_rend_une_cle_neuve()
    {
        // L'autre bord de la même porte : elle se ferme sur le voleur, pas sur
        // qui tient encore son téléphone et change d'appareil.
        await using var hote = HarnaisHttp.Hote(baseDeDonnees);
        using var portee = hote.Services.CreateScope();
        var compte = await ActiveAsync(portee.ServiceProvider);
        var avant = await CleAsync(portee.ServiceProvider, compte);

        var (code, _) = await PreparerBrutAsync(
            portee.ServiceProvider,
            compte,
            await CodeValideAsync(portee.ServiceProvider, compte)
        );

        Assert.Equal(StatusCodes.Status200OK, code);
        Assert.NotEqual(avant, await CleAsync(portee.ServiceProvider, compte));
    }

    [Fact]
    public async Task Preparer_accepte_un_CODE_DE_RECUPERATION_et_le_CONSOMME()
    {
        // Le jour du téléphone perdu, et c'est le seul jour où ces codes
        // comptent. N'accepter que l'authentificateur laisserait l'utilisateur
        // se connecter en brûlant un code de secours à chaque fois, sans jamais
        // pouvoir réenrôler : au dixième, le compte serait injoignable — ce
        // dépôt n'a ni réinitialisation ni route de support.
        await using var hote = HarnaisHttp.Hote(baseDeDonnees);
        using var portee = hote.Services.CreateScope();
        var (compte, secours) = await ActiveAvecSecoursAsync(portee.ServiceProvider);
        var avant = await CleAsync(portee.ServiceProvider, compte);

        var (code, _) = await PreparerBrutAsync(portee.ServiceProvider, compte, secours[0]);

        Assert.Equal(StatusCodes.Status200OK, code);
        Assert.NotEqual(avant, await CleAsync(portee.ServiceProvider, compte));

        // Et il est CONSOMMÉ : un code de secours qui resterait valable ne
        // serait qu'un second mot de passe, plus court.
        var rejoue = await ConnecterAsync(portee.ServiceProvider, compte, secours[0]);
        Assert.Equal(StatusCodes.Status401Unauthorized, rejoue.Code);
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

    [Fact]
    public async Task Desactiver_accepte_lui_aussi_un_CODE_DE_RECUPERATION()
    {
        // Même preuve, même porte : `SecondFacteurValideAsync` est une
        // connaissance, et elle vit à un seul endroit. Sans cette voie, le
        // téléphone perdu n'aurait aucune sortie — ni retirer, ni refaire.
        await using var hote = HarnaisHttp.Hote(baseDeDonnees);
        using var portee = hote.Services.CreateScope();
        var (compte, secours) = await ActiveAvecSecoursAsync(portee.ServiceProvider);

        var (code, _) = await DesactiverAsync(portee.ServiceProvider, compte, secours[0]);

        Assert.Equal(StatusCodes.Status204NoContent, code);
        Assert.False((await CompteAsync(portee.ServiceProvider, compte)).TwoFactorEnabled);
    }

    // ================================================================
    // La VRAIE route — ce que la LIAISON accepte
    // ================================================================

    [Fact]
    public async Task Par_la_ROUTE_le_PREMIER_enrolement_passe_sans_aucun_corps()
    {
        // AUCUNE épreuve appelant `PreparerAsync` à la main ne peut voir ceci.
        // La liaison des API minimales rend un corps NON-NULLABLE obligatoire :
        // déclaré ainsi, ce premier enrôlement — qui n'a rien à prouver et
        // n'envoie donc rien — recevrait 400 avant même d'atteindre le
        // gestionnaire, avec un corps VIDE et aucun code.
        await using var hote = await EnEcouteAsync(baseDeDonnees);
        using var client = Client(hote);
        var compte = await InscritAsync(hote.Services);

        using var reponse = await PreparerParLaRouteAsync(client, compte);

        Assert.Equal(HttpStatusCode.OK, reponse.StatusCode);
        Assert.Contains(
            "otpauth://totp/",
            await reponse.Content.ReadAsStringAsync(),
            StringComparison.Ordinal
        );
    }

    [Fact]
    public async Task Par_la_ROUTE_le_refus_d_un_REENROLEMENT_porte_un_CODE()
    {
        // Un refus de ce dépôt porte un CODE, jamais rien : le front n'a que
        // cela à traduire. Celui de la couche de liaison rendrait un corps vide
        // — et rien, dans cette composition, ne le remplit : ni
        // `AddProblemDetails`, ni `UseStatusCodePages`, ni `UseExceptionHandler`.
        await using var hote = await EnEcouteAsync(baseDeDonnees);
        using var client = Client(hote);
        var compte = await ActiveAsync(hote.Services);

        using var reponse = await PreparerParLaRouteAsync(client, compte);

        Assert.Equal(HttpStatusCode.BadRequest, reponse.StatusCode);
        Assert.Equal("CodeInvalide", HarnaisHttp.Code(await reponse.Content.ReadAsStringAsync()));
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
    // Ce que la base retient, et ce qu'elle ne retient plus
    // ================================================================

    [Fact]
    public async Task Les_codes_de_recuperation_ne_sont_JAMAIS_ranges_en_clair()
    {
        // Le magasin d'Identity colle les dix codes bout à bout et les écrit
        // TELS QUELS dans `AspNetUserTokens.Value` — aucun protecteur de
        // données personnelles n'est enregistré, donc rien ne chiffre la
        // colonne. Qui lit la base tient alors dix seconds facteurs
        // UTILISABLES, pour chaque compte protégé.
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

        var codes = System
            .Text.Json.JsonDocument.Parse(corps)
            .RootElement.GetProperty("codes")
            .EnumerateArray()
            .Select(e => e.GetString() ?? string.Empty)
            .ToList();

        var jetons = await JetonsAsync(compte);

        foreach (var secours in codes)
        {
            Assert.DoesNotContain(
                jetons,
                jeton => jeton.Valeur.Contains(secours, StringComparison.Ordinal)
            );
        }

        // Et AUCUNE ligne ne subsiste sous les noms d'Identity. L'assertion
        // porte sur la LIGNE, et pas seulement sur les codes qu'on vient de
        // rendre : un lot plus ancien y survivrait sans qu'aucune comparaison
        // sur ceux-là ne le voie.
        Assert.DoesNotContain(jetons, EstLeLotDIdentity);
        Assert.Contains(
            jetons,
            jeton =>
                string.Equals(
                    jeton.Fournisseur,
                    GestionnaireDUtilisateurs.Fournisseur,
                    StringComparison.Ordinal
                )
                && string.Equals(
                    jeton.Nom,
                    GestionnaireDUtilisateurs.NomDuJeton,
                    StringComparison.Ordinal
                )
        );

        // Le compte, lui, sait toujours combien de codes il lui reste : le
        // décompte lit le lot haché, et non celui qu'Identity ne tient plus.
        Assert.Equal(
            DeuxFacteurs.NombreDeCodesDeRecuperation,
            await CompterAsync(portee.ServiceProvider, compte)
        );
    }

    [Fact]
    public async Task Une_REACTIVATION_efface_le_lot_reste_EN_CLAIR()
    {
        // Écrire les hachés À CÔTÉ du clair ne fermerait rien : les anciens
        // codes resteraient lisibles dans la base — inutilisables, mais
        // toujours là, et indéfiniment.
        await using var hote = HarnaisHttp.Hote(baseDeDonnees);
        using var portee = hote.Services.CreateScope();
        var compte = await ActiveAsync(portee.ServiceProvider);

        await RangerEnClairAsync(portee.ServiceProvider, compte, ["ancien-un", "ancien-deux"]);
        Assert.Contains(await JetonsAsync(compte), PorteLAncienLot);

        var (code, _) = await ActiverAsync(
            portee.ServiceProvider,
            compte,
            await CodeValideAsync(portee.ServiceProvider, compte)
        );
        Assert.Equal(StatusCodes.Status200OK, code);

        var jetons = await JetonsAsync(compte);
        Assert.DoesNotContain(jetons, EstLeLotDIdentity);
        Assert.DoesNotContain(jetons, PorteLAncienLot);
    }

    [Fact]
    public async Task Un_ANCIEN_code_EN_CLAIR_ouvre_encore_la_session_et_le_clair_DISPARAIT()
    {
        // Refuser les lots d'avant le correctif enfermerait dehors qui a perdu
        // son téléphone AVANT lui : `/2fa/activer` et `/2fa/desactiver` exigent
        // l'un et l'autre un code d'authentificateur, et les codes de
        // récupération sont donc le seul chemin qui lui reste. Le lot est
        // repris — haché tel quel — et le clair s'en va dans le même geste.
        await using var hote = HarnaisHttp.Hote(baseDeDonnees);
        using var portee = hote.Services.CreateScope();
        var compte = await ActiveAsync(portee.ServiceProvider);

        await RangerEnClairAsync(portee.ServiceProvider, compte, ["ancien-un", "ancien-deux"]);

        var premier = await ConnecterAsync(portee.ServiceProvider, compte, "ancien-un");
        Assert.Equal(StatusCodes.Status200OK, premier.Code);

        var jetons = await JetonsAsync(compte);
        Assert.DoesNotContain(jetons, EstLeLotDIdentity);
        Assert.DoesNotContain(jetons, PorteLAncienLot);

        // Le code consommé ne ressert pas ; son voisin, lui, ouvre encore.
        var second = await ConnecterAsync(portee.ServiceProvider, compte, "ancien-un");
        Assert.Equal(StatusCodes.Status401Unauthorized, second.Code);

        var voisin = await ConnecterAsync(portee.ServiceProvider, compte, "ancien-deux");
        Assert.Equal(StatusCodes.Status200OK, voisin.Code);
    }

    [Fact]
    public async Task Une_tentative_RATEE_efface_quand_meme_le_lot_EN_CLAIR()
    {
        // La reprise ne dépend PAS du code présenté. Sans cela, un lot resté en
        // clair attendrait qu'un code juste soit un jour saisi pour disparaître
        // — c'est-à-dire, sur un compte dormant, jamais.
        await using var hote = HarnaisHttp.Hote(baseDeDonnees);
        using var portee = hote.Services.CreateScope();
        var compte = await ActiveAsync(portee.ServiceProvider);

        await RangerEnClairAsync(portee.ServiceProvider, compte, ["ancien-un", "ancien-deux"]);

        var refus = await ConnecterAsync(portee.ServiceProvider, compte, "code-inconnu");
        Assert.Equal(StatusCodes.Status401Unauthorized, refus.Code);

        var jetons = await JetonsAsync(compte);
        Assert.DoesNotContain(jetons, EstLeLotDIdentity);
        Assert.DoesNotContain(jetons, PorteLAncienLot);

        // Et les deux codes que l'utilisateur détient restent valables.
        var ouverture = await ConnecterAsync(portee.ServiceProvider, compte, "ancien-deux");
        Assert.Equal(StatusCodes.Status200OK, ouverture.Code);
    }

    [Fact]
    public async Task Un_lot_EN_CLAIR_qui_DOUBLE_le_lot_hache_disparait_sans_le_remplacer()
    {
        // L'état que laisserait un effacement qui n'a pas abouti : les deux
        // lots côte à côte. Le haché est le plus récent, il fait foi — et le
        // clair s'en va sans rien remplacer. Reprendre le clair ici écraserait
        // le lot du jour par un lot périmé.
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

        await RangerEnClairAsync(
            portee.ServiceProvider,
            compte,
            ["ancien-un"],
            retirerLeHache: false
        );

        var ouverture = await ConnecterAsync(portee.ServiceProvider, compte, secours);
        Assert.Equal(StatusCodes.Status200OK, ouverture.Code);

        var jetons = await JetonsAsync(compte);
        Assert.DoesNotContain(jetons, EstLeLotDIdentity);
        Assert.DoesNotContain(jetons, PorteLAncienLot);

        var refus = await ConnecterAsync(portee.ServiceProvider, compte, "ancien-un");
        Assert.Equal(StatusCodes.Status401Unauthorized, refus.Code);
    }

    [Fact]
    public async Task Un_lot_ILLISIBLE_ne_valide_aucun_code()
    {
        // Une valeur qui n'est pas celle qu'on a écrite — colonne modifiée à la
        // main, format d'une autre version — se comporte comme un code faux, et
        // jamais comme une exception qui remonterait au client.
        await using var hote = HarnaisHttp.Hote(baseDeDonnees);
        using var portee = hote.Services.CreateScope();
        var compte = await ActiveAsync(portee.ServiceProvider);

        await EcraserLeLotAsync(portee.ServiceProvider, compte, "ceci-n-est-pas-du-base64");

        var refus = await ConnecterAsync(portee.ServiceProvider, compte, "peu-importe");
        Assert.Equal(StatusCodes.Status401Unauthorized, refus.Code);
        Assert.Equal(0, await CompterAsync(portee.ServiceProvider, compte));
    }

    // ================================================================
    // Le harnais
    // ================================================================

    private static async Task<PreparationDeDeuxFacteurs> PreparerAsync(
        IServiceProvider services,
        Guid compte,
        string? preuve = null
    )
    {
        var (code, corps) = await PreparerBrutAsync(services, compte, preuve);

        Assert.Equal(StatusCodes.Status200OK, code);
        var racine = System.Text.Json.JsonDocument.Parse(corps).RootElement;

        return new PreparationDeDeuxFacteurs(
            racine.GetProperty("uri").GetString() ?? string.Empty,
            racine.GetProperty("cle").GetString() ?? string.Empty
        );
    }

    /// <summary>
    /// Préparer sans rien présumer du statut. <paramref name="preuve" /> à
    /// <c>null</c> vaut <b>aucun corps</b> — la requête du premier enrôlement,
    /// qui n'a rien à envoyer.
    /// </summary>
    private static async Task<(int Code, string Corps)> PreparerBrutAsync(
        IServiceProvider services,
        Guid compte,
        string? preuve
    )
    {
        using var portee = Portee(services);
        return await HarnaisHttp.ExecuterAsync(
            portee.ServiceProvider,
            DeuxFacteurs.PreparerAsync(
                preuve is null ? null : new DemandeDeCode(preuve),
                new DemandeurFixe(compte),
                portee.ServiceProvider.GetRequiredService<UserManager<Utilisateur>>(),
                CancellationToken.None
            )
        );
    }

    /// <summary>
    /// L'API RÉELLE, routée et à l'écoute — le seul niveau où la LIAISON d'un
    /// corps de requête est observable.
    /// </summary>
    /// <remarks>
    /// Port <b>0</b> : le système en choisit un libre, et l'adresse effective se
    /// relit dans <c>Urls</c> après le démarrage. Un port écrit en dur ferait
    /// rougir la suite le jour où quelque chose d'autre l'occupe.
    /// </remarks>
    private static async Task<WebApplication> EnEcouteAsync(BaseFixture baseDeDonnees)
    {
        var hote = HarnaisHttp.Hote(baseDeDonnees);
        hote.Urls.Add("http://127.0.0.1:0");
        Composition.Router(hote);
        await hote.StartAsync();

        return hote;
    }

    private static HttpClient Client(WebApplication hote) =>
        new()
        {
            BaseAddress = new Uri(
                hote.Urls.FirstOrDefault()
                    ?? throw new InvalidOperationException(
                        "L'hôte n'annonce aucune adresse après son démarrage : la requête "
                            + "partirait dans le vide, et l'épreuve rougirait sans rien montrer."
                    ),
                UriKind.Absolute
            ),
        };

    /// <summary>
    /// Un POST sur la route réelle, avec un jeton d'accès réel et <b>aucun
    /// corps</b> — la requête exacte du premier enrôlement.
    /// </summary>
    private static async Task<HttpResponseMessage> PreparerParLaRouteAsync(
        HttpClient client,
        Guid compte
    )
    {
        using var requete = new HttpRequestMessage(
            HttpMethod.Post,
            PointsDEntree.Prefixe + "/2fa/preparer"
        );
        requete.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            JetonDAcces.Emettre(compte, HarnaisHttp.Cle, DateTimeOffset.UtcNow)
        );

        return await client.SendAsync(requete);
    }

    /// <summary>La clé d'authentificateur ENREGISTRÉE, telle que la base la porte.</summary>
    private static async Task<string?> CleAsync(IServiceProvider services, Guid compte)
    {
        using var portee = Portee(services);
        var utilisateurs = portee.ServiceProvider.GetRequiredService<UserManager<Utilisateur>>();
        var utilisateur = await utilisateurs.FindByIdAsync(
            compte.ToString("D", System.Globalization.CultureInfo.InvariantCulture)
        );

        Assert.True(utilisateur is not null, $"le compte {compte} a disparu");
        return await utilisateurs.GetAuthenticatorKeyAsync(utilisateur);
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

    // ================================================================
    // Le chiffrement de la clé partagée — D59
    // ================================================================

    [Fact]
    public async Task L_enrolement_n_ecrit_JAMAIS_la_cle_TOTP_en_clair()
    {
        // Ce que D58 avait laissé ouvert. On lit la ligne BRUTE, pas ce que le
        // gestionnaire veut bien rendre : c'est la seule façon de voir ce qui
        // touche le journal d'écriture anticipée.
        await using var hote = HarnaisHttp.Hote(baseDeDonnees);
        var compte = await ActiveAsync(hote.Services);

        var brut = await CleTotpBruteAsync(compte);

        Assert.StartsWith("v1:", brut, StringComparison.Ordinal);
        Assert.DoesNotContain(
            await CleTotpEnClairAsync(hote.Services, compte),
            brut,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public async Task Le_chiffrement_LIE_la_cle_TOTP_a_son_proprietaire()
    {
        // Le secret d'un compte, recopié dans un autre, ne doit pas ouvrir le
        // second. Sans cette liaison, un accès en écriture à la base suffirait
        // à se connecter comme n'importe qui.
        await using var hote = HarnaisHttp.Hote(baseDeDonnees);
        var premier = await ActiveAsync(hote.Services);
        var second = await ActiveAsync(hote.Services);

        await EcrireLaCleTotpAsync(second, await CleTotpBruteAsync(premier));

        using var portee = Portee(hote.Services);
        var utilisateurs = portee.ServiceProvider.GetRequiredService<UserManager<Utilisateur>>();
        var utilisateur = await utilisateurs.FindByIdAsync(
            second.ToString("D", System.Globalization.CultureInfo.InvariantCulture)
        );

        await Assert.ThrowsAnyAsync<System.Security.Cryptography.CryptographicException>(
            () => utilisateurs.GetAuthenticatorKeyAsync(utilisateur!)
        );
    }

    [Fact]
    public async Task Une_cle_TOTP_HERITEE_en_clair_est_migree_au_premier_contact()
    {
        // La reprise des comptes que D58 a laissés en clair. Elle est
        // invisible : la connexion suivante est normale, et la migration ne se
        // produit qu'une fois par utilisateur.
        await using var hote = HarnaisHttp.Hote(baseDeDonnees);
        var compte = await ActiveAsync(hote.Services);
        const string heritee = "JBSWY3DPEHPK3PXP";
        await EcrireLaCleTotpAsync(compte, heritee);

        var rendue = await CleTotpEnClairAsync(hote.Services, compte);

        Assert.Equal(heritee, rendue);
        Assert.StartsWith("v1:", await CleTotpBruteAsync(compte), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Une_cle_TOTP_portee_par_une_ANCIENNE_cle_de_donnees_est_rechiffree()
    {
        // La rotation, du côté qui la rend utilisable : le même geste qui migre
        // le clair remet à niveau ce qu'une clé plus ancienne portait.
        await using var hote = HarnaisHttp.Hote(baseDeDonnees);
        var compte = await ActiveAsync(hote.Services);

        var ancienne = new Guid("44444444-4444-4444-4444-444444444444");
        var vieux = new TrousseauDeChiffrement(
            new Dictionary<Guid, byte[]> { [ancienne] = [.. Enumerable.Repeat((byte)9, 32)] },
            ancienne
        ).Chiffrer("JBSWY3DPEHPK3PXP", compte);
        await EcrireLaCleTotpAsync(compte, vieux);

        // Le porteur du harnais connaît les DEUX clés : l'ancienne déchiffre,
        // la courante rechiffre.
        hote.Services.GetRequiredService<PorteurDeTrousseau>()
            .Poser(
                new TrousseauDeChiffrement(
                    new Dictionary<Guid, byte[]>
                    {
                        [ancienne] = [.. Enumerable.Repeat((byte)9, 32)],
                        [HarnaisHttp.CleDeDonnees] = [.. Enumerable.Repeat((byte)7, 32)],
                    },
                    HarnaisHttp.CleDeDonnees
                )
            );

        Assert.Equal("JBSWY3DPEHPK3PXP", await CleTotpEnClairAsync(hote.Services, compte));
        Assert.Contains(
            HarnaisHttp.CleDeDonnees.ToString("N"),
            await CleTotpBruteAsync(compte),
            StringComparison.Ordinal
        );
    }

    private async Task<string> CleTotpBruteAsync(Guid compte)
    {
        var jetons = await JetonsAsync(compte);
        var cle = jetons.Find(j =>
            string.Equals(j.Nom, "AuthenticatorKey", StringComparison.Ordinal)
        );

        Assert.False(
            string.IsNullOrEmpty(cle.Valeur),
            "aucune clé d'authentificateur en base : l'épreuve ne prouverait rien."
        );

        return cle.Valeur;
    }

    /// <summary>
    /// L'utilisateur est résolu DANS la portée qui lira sa clé. Le résoudre
    /// ailleurs le fait suivre par un autre contexte, et EF refuse alors de
    /// pister deux instances de même identifiant — mesuré : l'épreuve échoue
    /// sur « another instance with the same key value is already being tracked »,
    /// qui ne dit rien du chiffrement.
    /// </summary>
    private static async Task<string> CleTotpEnClairAsync(IServiceProvider services, Guid compte)
    {
        using var portee = Portee(services);
        var utilisateurs = portee.ServiceProvider.GetRequiredService<UserManager<Utilisateur>>();
        var utilisateur = await utilisateurs.FindByIdAsync(
            compte.ToString("D", System.Globalization.CultureInfo.InvariantCulture)
        );

        Assert.True(utilisateur is not null, $"le compte {compte} a disparu");

        return (await utilisateurs.GetAuthenticatorKeyAsync(utilisateur))!;
    }

    private async Task EcrireLaCleTotpAsync(Guid compte, string valeur)
    {
        await using var connexion = new Npgsql.NpgsqlConnection(baseDeDonnees.ChaineAuth);
        await connexion.OpenAsync();
        await using var commande = new Npgsql.NpgsqlCommand(
            """
            update public."AspNetUserTokens" set "Value" = $2
             where "UserId" = $1 and "Name" = 'AuthenticatorKey'
            """,
            connexion
        );
        commande.Parameters.AddWithValue(compte);
        commande.Parameters.AddWithValue(valeur);

        Assert.Equal(1, await commande.ExecuteNonQueryAsync());
    }

    private static async Task<Guid> ActiveAsync(IServiceProvider services) =>
        (await ActiveAvecSecoursAsync(services)).Compte;

    /// <summary>
    /// Le même compte, et les dix codes de récupération rendus à l'activation —
    /// les seuls justificatifs qui restent le jour où le téléphone est perdu.
    /// </summary>
    private static async Task<(Guid Compte, IReadOnlyList<string> Secours)> ActiveAvecSecoursAsync(
        IServiceProvider services
    )
    {
        var compte = await InscritAsync(services);
        await PreparerAsync(services, compte);

        var (code, corps) = await ActiverAsync(
            services,
            compte,
            await CodeValideAsync(services, compte)
        );
        Assert.Equal(StatusCodes.Status200OK, code);

        var secours = System
            .Text.Json.JsonDocument.Parse(corps)
            .RootElement.GetProperty("codes")
            .EnumerateArray()
            .Select(e => e.GetString() ?? string.Empty)
            .ToArray();

        Assert.Equal(DeuxFacteurs.NombreDeCodesDeRecuperation, secours.Length);
        return (compte, secours);
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

    /// <summary>Les jetons rangés pour ce compte, lus DANS LA BASE.</summary>
    /// <remarks>
    /// Par Npgsql et non par Identity : ce qui est en cause est l'état RÉEL de
    /// la colonne, pas ce que la bibliothèque veut bien en rendre.
    /// </remarks>
    private async Task<List<(string Fournisseur, string Nom, string Valeur)>> JetonsAsync(
        Guid compte
    )
    {
        await using var connexion = new NpgsqlConnection(baseDeDonnees.ChaineAuth);
        await connexion.OpenAsync();

        await using var commande = new NpgsqlCommand(
            """
            select "LoginProvider", "Name", coalesce("Value", '')
            from public."AspNetUserTokens"
            where "UserId" = $1
            """,
            connexion
        );
        commande.Parameters.AddWithValue(compte);

        var jetons = new List<(string Fournisseur, string Nom, string Valeur)>();
        await using var lecteur = await commande.ExecuteReaderAsync();
        while (await lecteur.ReadAsync())
        {
            jetons.Add((lecteur.GetString(0), lecteur.GetString(1), lecteur.GetString(2)));
        }

        return jetons;
    }

    /// <summary>Le jeton est-il celui qu'Identity range EN CLAIR ?</summary>
    /// <remarks>
    /// Les deux noms sont écrits ICI, en toutes lettres, et ne sont PAS lus
    /// dans le produit : une constante fausse là-bas ferait chercher cette
    /// assertion ailleurs, et la ligne survivrait sous les yeux d'une épreuve
    /// verte. Ce qui les accorde est le magasin d'Identity, qui écrit le lot de
    /// <see cref="RangerEnClairAsync" /> sous ses propres noms.
    /// </remarks>
    private static bool EstLeLotDIdentity((string Fournisseur, string Nom, string Valeur) jeton) =>
        string.Equals(jeton.Fournisseur, "[AspNetUserStore]", StringComparison.Ordinal)
        && string.Equals(jeton.Nom, "RecoveryCodes", StringComparison.Ordinal);

    /// <summary>Le jeton porte-t-il un code du lot d'épreuve ?</summary>
    /// <remarks>
    /// Le tiret suffit à trancher : ce que le correctif range est du base64
    /// séparé par des points-virgules, et le base64 n'a pas de tiret.
    /// </remarks>
    private static bool PorteLAncienLot((string Fournisseur, string Nom, string Valeur) jeton) =>
        jeton.Valeur.Contains("ancien-", StringComparison.Ordinal);

    /// <summary>Combien de codes de récupération le compte a-t-il encore.</summary>
    private static async Task<int> CompterAsync(IServiceProvider services, Guid compte)
    {
        using var portee = Portee(services);
        var utilisateurs = portee.ServiceProvider.GetRequiredService<UserManager<Utilisateur>>();

        return await utilisateurs.CountRecoveryCodesAsync(
            await CompteSuiviAsync(utilisateurs, compte)
        );
    }

    /// <summary>
    /// Range un lot de codes EN CLAIR, comme Identity le faisait avant le
    /// correctif.
    /// </summary>
    /// <remarks>
    /// L'écriture passe par le MAGASIN d'Identity, donc sous SES noms de
    /// fournisseur et de jeton — deux constantes privées que le produit ne peut
    /// que recopier. Le jour où elles changent, la reprise ne trouve plus rien
    /// et ces épreuves rougissent : c'est ce qui tient la recopie.
    /// </remarks>
    private static async Task RangerEnClairAsync(
        IServiceProvider services,
        Guid compte,
        string[] codes,
        bool retirerLeHache = true
    )
    {
        using var portee = Portee(services);
        var utilisateurs = portee.ServiceProvider.GetRequiredService<UserManager<Utilisateur>>();
        var utilisateur = await CompteSuiviAsync(utilisateurs, compte);

        if (retirerLeHache)
        {
            // Le compte doit ressembler EXACTEMENT à un compte d'avant le
            // correctif : le clair présent, le haché absent.
            var retire = await utilisateurs.RemoveAuthenticationTokenAsync(
                utilisateur,
                GestionnaireDUtilisateurs.Fournisseur,
                GestionnaireDUtilisateurs.NomDuJeton
            );

            Assert.True(
                retire.Succeeded,
                "le harnais n'a pas pu retirer le lot haché : "
                    + string.Join(", ", retire.Errors.Select(e => e.Code))
            );
        }

        var magasin = Assert.IsAssignableFrom<IUserTwoFactorRecoveryCodeStore<Utilisateur>>(
            portee.ServiceProvider.GetRequiredService<IUserStore<Utilisateur>>()
        );

        await magasin.ReplaceCodesAsync(utilisateur, codes, CancellationToken.None);

        var ecrit = await utilisateurs.UpdateAsync(utilisateur);
        Assert.True(
            ecrit.Succeeded,
            "le harnais n'a pas pu ranger le lot en clair : "
                + string.Join(", ", ecrit.Errors.Select(e => e.Code))
        );
    }

    /// <summary>Écrase le lot haché par une valeur qui n'en est pas un.</summary>
    private static async Task EcraserLeLotAsync(
        IServiceProvider services,
        Guid compte,
        string valeur
    )
    {
        using var portee = Portee(services);
        var utilisateurs = portee.ServiceProvider.GetRequiredService<UserManager<Utilisateur>>();

        var pose = await utilisateurs.SetAuthenticationTokenAsync(
            await CompteSuiviAsync(utilisateurs, compte),
            GestionnaireDUtilisateurs.Fournisseur,
            GestionnaireDUtilisateurs.NomDuJeton,
            valeur
        );

        Assert.True(
            pose.Succeeded,
            "le harnais n'a pas pu écraser le lot haché : "
                + string.Join(", ", pose.Errors.Select(e => e.Code))
        );
    }

    /// <summary>
    /// Le compte, chargé PAR CE gestionnaire : ce qu'il écrit ensuite passe par
    /// le contexte qui suit déjà l'entité. Un utilisateur chargé dans une autre
    /// portée en serait détaché.
    /// </summary>
    private static async Task<Utilisateur> CompteSuiviAsync(
        UserManager<Utilisateur> utilisateurs,
        Guid compte
    )
    {
        var utilisateur = await utilisateurs.FindByIdAsync(
            compte.ToString("D", System.Globalization.CultureInfo.InvariantCulture)
        );

        Assert.True(utilisateur is not null, $"le compte {compte} a disparu");
        return utilisateur;
    }
}
