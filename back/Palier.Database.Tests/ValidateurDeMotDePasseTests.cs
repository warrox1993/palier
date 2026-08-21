using System.Net;
using Palier.Infrastructure.Identite;

namespace Palier.Database.Tests;

/// <summary>
/// Le validateur à deux étages. L'épreuve qui compte le plus est celle du
/// réseau coupé : c'est elle qui prouve que le dilemme « échec ouvert ou fermé »
/// n'a plus lieu d'être.
/// </summary>
public sealed class ValidateurDeMotDePasseTests
{
    // ================================================================
    // Étage 1 — il ne peut pas échouer
    // ================================================================

    [Fact]
    public async Task Un_mot_de_passe_frequent_est_refuse_MEME_SANS_RESEAU()
    {
        // Le réseau est coupé net : toute requête lève. Si le contrôle
        // dépendait de l'API, ce mot de passe passerait.
        using var reseau = new ReseauCoupe();
        using var client = new HttpClient(reseau);
        var validateur = new ValidateurDeMotDePasse(client);

        var resultat = await validateur.ValidateAsync(null!, new Utilisateur(), "azertyuiop");

        Assert.False(
            resultat.Succeeded,
            "« azertyuiop » est passé alors que le réseau était coupé. L'étage local "
                + "ne mord pas, et le contrôle échoue OUVERT."
        );
        Assert.Contains(resultat.Errors, e => e.Code == "MotDePasseCompromis");
    }

    [Theory]
    [InlineData("motdepasse")]
    [InlineData("MotDePasse")] // la comparaison ignore la casse
    [InlineData("P@ssw0rd")]
    [InlineData("azerty123")]
    public async Task Les_variantes_frequentes_sont_refusees_sans_reseau(string motDePasse)
    {
        using var reseau = new ReseauCoupe();
        using var client = new HttpClient(reseau);
        var validateur = new ValidateurDeMotDePasse(client);

        Assert.False(
            (await validateur.ValidateAsync(null!, new Utilisateur(), motDePasse)).Succeeded,
            $"« {motDePasse} » est passé sans réseau."
        );
    }

    [Fact]
    public async Task Un_mot_de_passe_solide_passe_meme_sans_reseau()
    {
        // La contrepartie : le contrôle ne bloque JAMAIS les inscriptions.
        using var reseau = new ReseauCoupe();
        using var client = new HttpClient(reseau);
        var validateur = new ValidateurDeMotDePasse(client);

        var resultat = await validateur.ValidateAsync(
            null!,
            new Utilisateur(),
            "brouette-hivernale-38-oscille"
        );

        Assert.True(
            resultat.Succeeded,
            "une panne de HIBP a bloqué une inscription légitime : le contrôle échoue FERMÉ."
        );
    }

    [Fact]
    public void La_liste_embarquee_est_chargee_et_non_vide()
    {
        // Quatrième question du franchissement : sans cette assertion, une
        // ressource absente rendrait l'étage local silencieusement inoffensif,
        // et toutes les épreuves ci-dessus passeraient en ne lisant rien.
        Assert.True(
            ValidateurDeMotDePasse.NombreDeMotsDePasseFrequents > 50,
            $"la liste embarquée ne porte que "
                + $"{ValidateurDeMotDePasse.NombreDeMotsDePasseFrequents} entrée(s)."
        );
    }

    // ================================================================
    // Étage 2 — opportuniste, et qui distingue le remplissage
    // ================================================================

    [Fact]
    public async Task Un_mot_de_passe_connu_de_HIBP_est_refuse()
    {
        // « brouette-hivernale-38-oscille » n'est pas dans la liste locale.
        // C'est l'API qui le refuse, et elle seule.
        using var reponse = new ReponseFabriquee(SuffixeDe("brouette-hivernale-38-oscille"), 42);
        using var client = new HttpClient(reponse);
        var validateur = new ValidateurDeMotDePasse(client);

        Assert.False(
            (
                await validateur.ValidateAsync(
                    null!,
                    new Utilisateur(),
                    "brouette-hivernale-38-oscille"
                )
            ).Succeeded
        );
    }

    [Fact]
    public async Task Une_entree_de_REMPLISSAGE_ne_refuse_rien()
    {
        // Add-Padding fait rendre 800 à 1000 entrées, dont des fausses au compte
        // ZÉRO. Les prendre pour des correspondances refuserait des mots de
        // passe parfaitement sains — et l'utilisateur n'y comprendrait rien.
        using var reponse = new ReponseFabriquee(SuffixeDe("brouette-hivernale-38-oscille"), 0);
        using var client = new HttpClient(reponse);
        var validateur = new ValidateurDeMotDePasse(client);

        Assert.True(
            (
                await validateur.ValidateAsync(
                    null!,
                    new Utilisateur(),
                    "brouette-hivernale-38-oscille"
                )
            ).Succeeded,
            "une entrée de remplissage, au compte zéro, a été prise pour une correspondance."
        );
    }

    [Fact]
    public async Task Une_reponse_en_erreur_ne_bloque_pas_l_inscription()
    {
        using var reponse = new ReponseEnErreur(HttpStatusCode.ServiceUnavailable);
        using var client = new HttpClient(reponse);
        var validateur = new ValidateurDeMotDePasse(client);

        Assert.True(
            (
                await validateur.ValidateAsync(
                    null!,
                    new Utilisateur(),
                    "brouette-hivernale-38-oscille"
                )
            ).Succeeded
        );
    }

    [Fact]
    public async Task Un_mot_de_passe_vide_n_est_pas_du_ressort_de_ce_validateur()
    {
        // La longueur et la nullité relèvent de PasswordOptions. Doubler le
        // contrôle ici produirait deux messages pour une seule faute.
        using var reseau = new ReseauCoupe();
        using var client = new HttpClient(reseau);
        var validateur = new ValidateurDeMotDePasse(client);

        Assert.True((await validateur.ValidateAsync(null!, new Utilisateur(), "")).Succeeded);
        Assert.True((await validateur.ValidateAsync(null!, new Utilisateur(), null)).Succeeded);
    }

    private static string SuffixeDe(string motDePasse)
    {
#pragma warning disable CA5350 // Le format que l'API impose ; voir le validateur.
        var empreinte = Convert
            .ToHexString(
                System.Security.Cryptography.SHA1.HashData(
                    System.Text.Encoding.UTF8.GetBytes(motDePasse)
                )
            )
            .ToUpperInvariant();
#pragma warning restore CA5350
        return empreinte[5..];
    }

    /// <summary>Le réseau est coupé : toute requête lève.</summary>
    private sealed class ReseauCoupe : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        ) => throw new HttpRequestException("réseau coupé, pour l'épreuve");
    }

    /// <summary>Une réponse k-anonymat fabriquée, avec le compte voulu.</summary>
    private sealed class ReponseFabriquee(string suffixe, long compte) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            // Deux lignes de bruit avant la bonne : le validateur doit parcourir.
            var corps =
                $"0000000000000000000000000000000000A:3\n"
                + $"0000000000000000000000000000000000B:0\n"
                + $"{suffixe}:{compte}\n";

            return Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(corps) }
            );
        }
    }

    private sealed class ReponseEnErreur(HttpStatusCode code) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        ) => Task.FromResult(new HttpResponseMessage(code));
    }
}
