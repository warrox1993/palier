using Palier.Api.Auth;

namespace Palier.Database.Tests;

/// <summary>
/// Ce que le produit fait au retour de Google — exigence 1 de
/// <c>docs/09-comptes.md</c> § 1, et le cœur de l'exigence 7.
///
/// La décision est PURE et éprouvée seule : le va-et-vient OAuth est du câblage
/// de framework, mais le choix « connecter, créer, proposer la liaison ou
/// refuser » est ce qui peut donner un compte à quelqu'un qui n'y a pas droit.
/// </summary>
public sealed class DecisionDeGoogleTests
{
    private const string _sujet = "1093847";
    private const string _adresse = "personne@exemple.test";

    // ================================================================
    // Cas 1 — la connexion existe déjà
    // ================================================================

    [Fact]
    public void Une_connexion_Google_EXISTANTE_ouvre_la_session()
    {
        Assert.Equal(
            SuiteDeGoogle.Connecter,
            DecisionDeGoogle.Trancher(
                new ConstatDeGoogle(_sujet, _adresse, EmailVerifie: true),
                connexionExiste: true,
                compteEmailExiste: true
            )
        );
    }

    [Fact]
    public void Une_connexion_EXISTANTE_ouvre_meme_si_Google_ne_verifie_plus_l_adresse()
    {
        // La liaison a déjà été établie et prouvée. Refuser ici déconnecterait
        // un utilisateur légitime parce que son administrateur Workspace a
        // changé un réglage — la vérification protège la CRÉATION du lien, pas
        // son usage ultérieur.
        Assert.Equal(
            SuiteDeGoogle.Connecter,
            DecisionDeGoogle.Trancher(
                new ConstatDeGoogle(_sujet, _adresse, EmailVerifie: false),
                connexionExiste: true,
                compteEmailExiste: true
            )
        );
    }

    // ================================================================
    // Cas 2 — personne ne connaît cette adresse
    // ================================================================

    [Fact]
    public void Une_adresse_INCONNUE_cree_le_compte()
    {
        Assert.Equal(
            SuiteDeGoogle.Creer,
            DecisionDeGoogle.Trancher(
                new ConstatDeGoogle(_sujet, _adresse, EmailVerifie: true),
                connexionExiste: false,
                compteEmailExiste: false
            )
        );
    }

    [Fact]
    public void Une_adresse_inconnue_et_NON_VERIFIEE_est_refusee()
    {
        // Créer un compte sur une adresse que Google ne garantit pas
        // permettrait de préempter l'adresse d'un tiers : le vrai propriétaire
        // trouverait ensuite un compte déjà pris à son nom.
        Assert.Equal(
            SuiteDeGoogle.Refuser,
            DecisionDeGoogle.Trancher(
                new ConstatDeGoogle(_sujet, _adresse, EmailVerifie: false),
                connexionExiste: false,
                compteEmailExiste: false
            )
        );
    }

    // ================================================================
    // Cas 3 — un compte email porte déjà cette adresse
    // ================================================================

    [Fact]
    public void Un_compte_email_EXISTANT_declenche_la_LIAISON_et_non_la_connexion()
    {
        // Lier automatiquement serait une prise de contrôle : quiconque crée un
        // compte Google portant l'adresse d'un utilisateur entrerait chez lui.
        // La liaison se PROPOSE, elle ne se fait pas.
        Assert.Equal(
            SuiteDeGoogle.ProposerLaLiaison,
            DecisionDeGoogle.Trancher(
                new ConstatDeGoogle(_sujet, _adresse, EmailVerifie: true),
                connexionExiste: false,
                compteEmailExiste: true
            )
        );
    }

    // ================================================================
    // Cas 4 — celui qui n'est pas théorique
    // ================================================================

    [Fact]
    public void Un_compte_existant_et_une_adresse_NON_VERIFIEE_sont_refuses()
    {
        // `email_verified` peut valoir false sur un compte Workspace mal
        // configuré. Proposer la liaison sur cette base laisserait un tiers
        // amorcer la prise d'un compte dont il ne possède pas l'adresse.
        Assert.Equal(
            SuiteDeGoogle.Refuser,
            DecisionDeGoogle.Trancher(
                new ConstatDeGoogle(_sujet, _adresse, EmailVerifie: false),
                connexionExiste: false,
                compteEmailExiste: true
            )
        );
    }

    // ================================================================
    // Les bornes : un constat incomplet ne décide de rien
    // ================================================================

    [Theory]
    [InlineData(null, _adresse)]
    [InlineData(_sujet, null)]
    [InlineData(null, null)]
    [InlineData("", _adresse)]
    [InlineData(_sujet, "")]
    public void Un_constat_INCOMPLET_est_refuse(string? sujet, string? adresse)
    {
        // Google est un tiers : sa réponse est une entrée hostile jusqu'à
        // preuve du contraire. Un jeton sans sujet ou sans adresse ne doit rien
        // ouvrir, et surtout pas créer un compte au nom vide.
        Assert.Equal(
            SuiteDeGoogle.Refuser,
            DecisionDeGoogle.Trancher(
                new ConstatDeGoogle(sujet, adresse, EmailVerifie: true),
                connexionExiste: false,
                compteEmailExiste: false
            )
        );
    }

    [Fact]
    public void Un_constat_incomplet_est_refuse_MEME_si_la_connexion_existe()
    {
        // La borne de la borne : le refus ne doit pas se laisser court-circuiter
        // par le premier cas, qui est évalué avant les autres.
        Assert.Equal(
            SuiteDeGoogle.Refuser,
            DecisionDeGoogle.Trancher(
                new ConstatDeGoogle(null, null, EmailVerifie: true),
                connexionExiste: true,
                compteEmailExiste: true
            )
        );
    }
}
