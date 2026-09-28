using System.Security.Cryptography;
using Palier.Infrastructure.Coffre;

namespace Palier.Database.Tests;

/// <summary>
/// Le chiffrement du secret TOTP — D59, et ce que D58 avait laissé ouvert.
///
/// Deux propriétés portent tout le reste : le chiffré est LIÉ à son
/// propriétaire, et il NOMME la clé qui l'a produit. La première empêche de
/// déplacer un secret d'un compte vers un autre ; la seconde rend la rotation
/// possible sans réécrire toute la table d'un coup.
/// </summary>
public sealed class TrousseauTests
{
    private static readonly Guid _courante = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid _ancienne = new("22222222-2222-2222-2222-222222222222");

    private const string _secretTotp = "JBSWY3DPEHPK3PXP";

    // ================================================================
    // Épreuve 1 — l'aller-retour
    // ================================================================

    [Fact]
    public void Un_aller_retour_rend_la_valeur_de_depart()
    {
        var trousseau = Trousseau();
        var proprietaire = Guid.NewGuid();

        var chiffre = trousseau.Chiffrer(_secretTotp, proprietaire);

        Assert.StartsWith("v1:", chiffre, StringComparison.Ordinal);
        Assert.Equal(_secretTotp, trousseau.Dechiffrer(chiffre, proprietaire));
    }

    // ================================================================
    // Épreuve 2 — la raison d'être des données associées
    // ================================================================

    [Fact]
    public void Un_secret_DEPLACE_vers_un_autre_utilisateur_ne_se_dechiffre_pas()
    {
        // Sans cette liaison, un accès en écriture à la base suffirait pour se
        // connecter comme n'importe qui : il suffirait de recopier le secret
        // d'un compte dans le sien. Le chiffrement n'aurait alors protégé que
        // contre la LECTURE.
        var trousseau = Trousseau();
        var chiffre = trousseau.Chiffrer(_secretTotp, Guid.NewGuid());

        Assert.ThrowsAny<CryptographicException>(() => trousseau.Dechiffrer(chiffre, Guid.NewGuid()));
    }

    // ================================================================
    // Épreuve 3 — une clé inconnue refuse en le disant
    // ================================================================

    [Fact]
    public void Une_valeur_chiffree_par_une_cle_ABSENTE_du_trousseau_refuse_clairement()
    {
        var inconnue = Guid.NewGuid();
        var chiffre =
            $"v1:{inconnue:N}:{Convert.ToBase64String(new byte[12])}:"
            + Convert.ToBase64String(new byte[20]);

        var faute = Assert.Throws<InvalidOperationException>(
            () => Trousseau().Dechiffrer(chiffre, Guid.NewGuid())
        );

        // Deux assertions : le refus ET l'identifiant de la clé manquante. Sans
        // le second, le message ne dirait pas QUELLE enveloppe manque en base.
        Assert.Contains("clé de données", faute.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(inconnue.ToString("N"), faute.Message, StringComparison.Ordinal);
    }

    // ================================================================
    // Épreuve 4 — la détection du clair est EXPLICITE
    // ================================================================

    [Fact]
    public void Une_valeur_sans_prefixe_est_reconnue_comme_du_clair()
    {
        // Jamais d'heuristique sur l'alphabet Base32 : « ça ressemble à du
        // Base32 donc c'est du clair » est le genre de test qui se trompe un
        // jour sur un cas limite, et ce jour-là il écrase un secret valide.
        Assert.True(TrousseauDeChiffrement.EstEnClair(_secretTotp));
        Assert.False(TrousseauDeChiffrement.EstEnClair("v1:aa:bb:cc"));
    }

    // ================================================================
    // Épreuve 5 — la rotation reste lisible
    // ================================================================

    [Fact]
    public void Un_secret_porte_par_une_ANCIENNE_cle_se_dechiffre_mais_n_est_pas_a_jour()
    {
        var proprietaire = Guid.NewGuid();
        var vieux = new TrousseauDeChiffrement(
            new Dictionary<Guid, byte[]> { [_ancienne] = Cle(2) },
            _ancienne
        ).Chiffrer(_secretTotp, proprietaire);

        var trousseau = Trousseau();

        Assert.Equal(_secretTotp, trousseau.Dechiffrer(vieux, proprietaire));
        Assert.False(trousseau.EstAJour(vieux));
        Assert.True(trousseau.EstAJour(trousseau.Chiffrer(_secretTotp, proprietaire)));
    }

    // ================================================================
    // Épreuve 6 — l'intégrité, que GCM garantit
    // ================================================================

    [Fact]
    public void Un_chiffre_ALTERE_d_un_seul_octet_refuse()
    {
        var proprietaire = Guid.NewGuid();
        var trousseau = Trousseau();
        var morceaux = trousseau.Chiffrer(_secretTotp, proprietaire).Split(':');

        // On retourne un bit du corps chiffré. AES-GCM authentifie : le
        // déchiffrement doit refuser, et non rendre une valeur abîmée dont
        // personne ne verrait qu'elle est fausse.
        var octets = Convert.FromBase64String(morceaux[3]);
        octets[0] ^= 0x01;
        morceaux[3] = Convert.ToBase64String(octets);

        Assert.ThrowsAny<CryptographicException>(
            () => trousseau.Dechiffrer(string.Join(':', morceaux), proprietaire)
        );
    }

    // ================================================================
    // Épreuve 7 — le nonce n'est jamais réutilisé
    // ================================================================

    [Fact]
    public void Deux_chiffrements_de_la_MEME_valeur_different()
    {
        // Un nonce fixe sous AES-GCM est la faute qui casse tout le mode : deux
        // chiffrés sous le même couple clé/nonce se comparent, et l'un révèle
        // l'autre. Cette épreuve tomberait sur un nonce constant.
        var trousseau = Trousseau();
        var proprietaire = Guid.NewGuid();

        Assert.NotEqual(
            trousseau.Chiffrer(_secretTotp, proprietaire),
            trousseau.Chiffrer(_secretTotp, proprietaire)
        );
    }

    // ================================================================
    // Épreuve 8 — une forme inattendue refuse plutôt que de deviner
    // ================================================================

    [Theory]
    [InlineData("v1:trop:court")]
    [InlineData("v1:pas-un-guid:AAAA:BBBB")]
    [InlineData("v2:11111111111111111111111111111111:AAAA:BBBB")]
    public void Une_valeur_MAL_FORMEE_refuse(string valeur)
    {
        Assert.Throws<InvalidOperationException>(
            () => Trousseau().Dechiffrer(valeur, Guid.NewGuid())
        );
    }

    // ================================================================

    private static TrousseauDeChiffrement Trousseau() =>
        new(
            new Dictionary<Guid, byte[]> { [_courante] = Cle(1), [_ancienne] = Cle(2) },
            _courante
        );

    private static byte[] Cle(byte graine) => [.. Enumerable.Repeat(graine, 32)];
}
