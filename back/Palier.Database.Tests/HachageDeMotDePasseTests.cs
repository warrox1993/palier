using Microsoft.AspNetCore.Builder;
using System.Buffers.Binary;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Palier.Api;
using Palier.Infrastructure.Identite;

namespace Palier.Database.Tests;

/// <summary>
/// Le hachage des mots de passe, vérifié sur ce que la composition produit
/// RÉELLEMENT — pas sur une constante relue.
///
/// <para>
/// Le format <c>IdentityV3</c> se décode octet par octet : un marqueur de
/// version, l'identifiant de la fonction pseudo-aléatoire, le nombre
/// d'itérations, la longueur du sel. C'est la seule façon de savoir ce
/// qu'Identity fait vraiment — sa documentation annonce tantôt PBKDF2-SHA256 à
/// 10 000 itérations, tantôt 100 000, et les deux sont fausses pour la version
/// 10.0.11.
/// </para>
/// </summary>
public sealed class HachageDeMotDePasseTests
{
    [Fact]
    public void Le_hachage_applique_deux_cent_dix_mille_iterations_de_HMAC_SHA512()
    {
        var hache = Convert.FromBase64String(
            Hacheur().HashPassword(new Utilisateur(), "UnMotDePasseDeTest123!")
        );

        Assert.True(hache[0] == 0x01, $"format attendu IdentityV3 (0x01), reçu 0x{hache[0]:X2}");

        var prf = BinaryPrimitives.ReadUInt32BigEndian(hache.AsSpan(1, 4));
        Assert.True(
            prf == 2,
            $"fonction pseudo-aléatoire attendue HMAC-SHA512 (2), reçue {prf}. "
                + "OWASP ne recommande 210 000 itérations QUE pour SHA-512 — pour SHA-256 "
                + "le compte est de 600 000, et le seuil ci-dessous deviendrait insuffisant."
        );

        var iterations = BinaryPrimitives.ReadUInt32BigEndian(hache.AsSpan(5, 4));
        Assert.True(
            iterations == 210_000,
            $"itérations attendues 210 000, reçues {iterations}. Le défaut d'Identity est "
                + "100 000, soit EN DESSOUS de ce qu'OWASP recommande pour ce PRF. Mesuré le "
                + "21/08/2026 sur seize cœurs : 56,7 ms contre 111,2 ms, un surcoût de 54 ms "
                + "par connexion sur un produit qui traite des données de l'article 9."
        );

        var sel = BinaryPrimitives.ReadUInt32BigEndian(hache.AsSpan(9, 4));
        Assert.True(sel * 8 == 128, $"sel attendu 128 bits, reçu {sel * 8}");
    }

    [Fact]
    public void Deux_hachages_du_meme_mot_de_passe_different_par_leur_sel()
    {
        var hacheur = Hacheur();
        var utilisateur = new Utilisateur();

        var premier = hacheur.HashPassword(utilisateur, "UnMotDePasseDeTest123!");
        var second = hacheur.HashPassword(utilisateur, "UnMotDePasseDeTest123!");

        Assert.NotEqual(premier, second);

        // Et les deux restent vérifiables : le sel voyage avec le haché.
        Assert.Equal(
            PasswordVerificationResult.Success,
            hacheur.VerifyHashedPassword(utilisateur, premier, "UnMotDePasseDeTest123!")
        );
        Assert.Equal(
            PasswordVerificationResult.Success,
            hacheur.VerifyHashedPassword(utilisateur, second, "UnMotDePasseDeTest123!")
        );
    }

    [Fact]
    public void Un_hachage_produit_au_defaut_dIdentity_reste_verifiable()
    {
        // Le premier octet porte la version : relever le nombre d'itérations
        // n'invalide pas les hachés déjà enregistrés. Sans cette propriété, le
        // passage à 210 000 aurait déconnecté tous les comptes existants.
        var utilisateur = new Utilisateur();
        var ancien = new PasswordHasher<Utilisateur>().HashPassword(
            utilisateur,
            "UnMotDePasseDeTest123!"
        );

        Assert.Equal(
            PasswordVerificationResult.SuccessRehashNeeded,
            Hacheur().VerifyHashedPassword(utilisateur, ancien, "UnMotDePasseDeTest123!")
        );
    }

    /// <summary>
    /// Le hacheur tel que <see cref="Composition" /> le configure. Interroger la
    /// composition réelle et non une valeur recopiée : un réglage accepté n'est
    /// pas un réglage appliqué.
    /// </summary>
    private static IPasswordHasher<Utilisateur> Hacheur()
    {
        var constructeur = WebApplication.CreateBuilder();
        // Une chaîne MINIMALE, sans identifiants : la composition ne se connecte
        // pas, elle enregistre. Une chaîne complète, même factice, ferait mordre
        // la règle `chaine-connexion-postgres` de gitleaks — et elle aurait
        // raison : elle ne peut pas distinguer une valeur de test d'une vraie.
        const string sansIdentifiants = "Host=127.0.0.1";
        constructeur.Configuration["ConnectionStrings:Palier"] = sansIdentifiants;
        constructeur.Configuration["ConnectionStrings:PalierAuth"] = sansIdentifiants;
        constructeur.Configuration["JWT_SIGNING_KEY"] = "cle-de-signature-des-epreuves-du-lot-quatre";
        Composition.Composer(constructeur);

        using var fournisseur = constructeur.Services.BuildServiceProvider();
        return fournisseur.GetRequiredService<IPasswordHasher<Utilisateur>>();
    }
}
