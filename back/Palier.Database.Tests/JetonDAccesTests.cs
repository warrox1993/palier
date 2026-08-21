using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;
using Palier.Api.Auth;

namespace Palier.Database.Tests;

/// <summary>
/// Le jeton d'accès. Les épreuves qui comptent sont celles du refus : un jeton
/// expiré, mal signé, ou émis pour un autre produit ne doit ouvrir aucune porte.
/// </summary>
public sealed class JetonDAccesTests
{
    // L'horloge est celle de la bibliothèque, PAS une date figée. Une date
    // figée obligerait à remplacer LifetimeValidator pour valider, et la
    // vérification d'expiration éprouvée serait alors la nôtre, pas celle qui
    // tourne en production. Les instants sont donc relatifs à l'instant présent.
    private static DateTimeOffset Maintenant => DateTimeOffset.UtcNow;

    // Une clé de test, engendrée ici : quarante-quatre caractères sans
    // signification, qui ne ressemblent à aucun secret réel.
    private const string _cle = "cle-de-test-pour-les-epreuves-du-lot-quatre-";

    [Fact]
    public async Task Un_jeton_valide_porte_l_identifiant_de_l_utilisateur()
    {
        var utilisateur = Guid.NewGuid();

        var jeton = JetonDAcces.Emettre(utilisateur, _cle, Maintenant);
        var principal = await ValiderAsync(jeton);

        Assert.NotNull(principal);
        Assert.Equal(
            utilisateur.ToString("D", System.Globalization.CultureInfo.InvariantCulture),
            principal.FindFirstValue(JwtRegisteredClaimNames.Sub)
        );
    }

    [Fact]
    public async Task Un_jeton_ne_porte_NI_email_NI_role()
    {
        // Un JWT est signé, pas chiffré : tout ce qu'on y met est lisible par
        // qui le détient. Les drapeaux d'autorisation se lisent en base, à
        // chaque requête, là où ils sont à jour.
        var jeton = JetonDAcces.Emettre(Guid.NewGuid(), _cle, Maintenant);
        var principal = await ValiderAsync(jeton);

        Assert.NotNull(principal);
        Assert.Null(principal.FindFirstValue(ClaimTypes.Email));
        Assert.Null(principal.FindFirstValue(ClaimTypes.Role));
        Assert.Null(principal.FindFirstValue(JwtRegisteredClaimNames.Email));
    }

    [Fact]
    public async Task Un_jeton_expire_est_refuse()
    {
        // Émis il y a seize minutes : au-delà des quinze qu'il annonce.
        var jeton = JetonDAcces.Emettre(Guid.NewGuid(), _cle, Maintenant.AddMinutes(-16));

        Assert.Null(await ValiderAsync(jeton));
    }

    [Fact]
    public async Task La_tolerance_d_horloge_est_NULLE()
    {
        // Ce jeton n'est expiré que de TRENTE SECONDES. C'est ce qui donne à
        // cette épreuve son mordant : avec la tolérance de cinq minutes que la
        // bibliothèque applique par défaut, il serait accepté. Une épreuve bâtie
        // sur un jeton vieux de seize minutes resterait verte si ClockSkew
        // disparaissait — elle ne prouverait rien.
        var jeton = JetonDAcces.Emettre(
            Guid.NewGuid(),
            _cle,
            Maintenant.AddMinutes(-15).AddSeconds(-30)
        );

        Assert.Null(await ValiderAsync(jeton));

        // Et la contre-épreuve, qui nomme la cause : le MÊME jeton passe dès
        // qu'on rend la tolérance par défaut. Sans elle, un refus pour une tout
        // autre raison — signature, émetteur, format — se lirait comme une
        // preuve que ClockSkew mord.
        var tolerant = JetonDAcces.Validation(_cle);
        tolerant.ClockSkew = TimeSpan.FromMinutes(5);
        var avecTolerance = await new JsonWebTokenHandler().ValidateTokenAsync(jeton, tolerant);

        Assert.True(
            avecTolerance.IsValid,
            "le jeton est refusé même avec cinq minutes de tolérance : ce n'est donc pas "
                + "l'expiration qui l'a fait refuser, et l'épreuve ci-dessus ne prouve rien."
        );
    }

    [Fact]
    public async Task Un_jeton_signe_par_une_AUTRE_cle_est_refuse()
    {
        var jeton = JetonDAcces.Emettre(
            Guid.NewGuid(),
            "une-tout-autre-cle-de-quarante-quatre-signes",
            Maintenant
        );

        Assert.Null(await ValiderAsync(jeton));
    }

    [Fact]
    public void Une_cle_trop_courte_est_refusee_AU_DEMARRAGE()
    {
        // HMAC-SHA256 exige au moins 256 bits. Le refus vient d'ici, en toutes
        // lettres : sans lui, la bibliothèque lèverait à la PREMIÈRE CONNEXION,
        // avec un message qui ne nomme ni la variable ni la longueur attendue.
        var trop = new string('a', JetonDAcces.OctetsDeCleMinimum - 1);

        var refus = Assert.Throws<InvalidOperationException>(() =>
            JetonDAcces.Emettre(Guid.NewGuid(), trop, Maintenant)
        );

        Assert.Contains("JWT_SIGNING_KEY", refus.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Deux_jetons_emis_dans_la_meme_seconde_sont_distinguables()
    {
        // L'identifiant de jeton permet à la journalisation de distinguer deux
        // émissions sans jamais voir l'utilisateur.
        var utilisateur = Guid.NewGuid();
        var instant = Maintenant;

        var premier = await ValiderAsync(JetonDAcces.Emettre(utilisateur, _cle, instant));
        var second = await ValiderAsync(JetonDAcces.Emettre(utilisateur, _cle, instant));

        Assert.NotNull(premier);
        Assert.NotNull(second);
        Assert.NotEqual(
            premier.FindFirstValue(JwtRegisteredClaimNames.Jti),
            second.FindFirstValue(JwtRegisteredClaimNames.Jti)
        );
    }

    /// <summary>
    /// Valide avec EXACTEMENT les paramètres de production — aucun rappel
    /// remplacé, aucune horloge simulée.
    /// </summary>
    private static async Task<ClaimsPrincipal?> ValiderAsync(string jeton)
    {
        var resultat = await new JsonWebTokenHandler().ValidateTokenAsync(
            jeton,
            JetonDAcces.Validation(_cle)
        );
        return resultat.IsValid ? new ClaimsPrincipal(resultat.ClaimsIdentity) : null;
    }
}
