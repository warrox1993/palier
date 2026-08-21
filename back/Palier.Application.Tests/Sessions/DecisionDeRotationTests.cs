using Palier.Application.Sessions;

namespace Palier.Application.Tests.Sessions;

/// <summary>
/// La rotation des jetons de rafraîchissement, telle que la RFC 9700 la décrit :
/// un jeton neuf à chaque usage, et un jeton déjà consommé qui se représente
/// signifie que deux porteurs détiennent la même chaîne.
///
/// <para>
/// La fenêtre de grâce est ce qui sépare un vol d'un second onglet. Sans elle,
/// deux requêtes concurrentes portant le même jeton — un rejeu réseau, une
/// reprise de connexion — déconnectent un utilisateur qui n'a rien fait.
/// </para>
/// </summary>
public sealed class DecisionDeRotationTests
{
    private static readonly DateTimeOffset _maintenant = new(2026, 8, 21, 12, 0, 0, TimeSpan.Zero);

    private static EtatDeSession Session(
        DateTimeOffset? consommee = null,
        DateTimeOffset? revoquee = null,
        Guid? remplacee = null,
        DateTimeOffset? expire = null
    ) =>
        new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            expire ?? _maintenant.AddDays(1),
            consommee,
            revoquee,
            remplacee
        );

    [Fact]
    public void Un_jeton_inconnu_est_refuse()
    {
        var resultat = DecisionDeRotation.Decider(null, _maintenant);

        Assert.Equal(IssueDeRotation.Inconnue, resultat.Issue);
        Assert.False(resultat.RevoquerLaFamille);
    }

    [Fact]
    public void Un_jeton_valide_est_accepte()
    {
        Assert.Equal(
            IssueDeRotation.Acceptee,
            DecisionDeRotation.Decider(Session(), _maintenant).Issue
        );
    }

    [Fact]
    public void Un_jeton_expire_est_refuse()
    {
        Assert.Equal(
            IssueDeRotation.Expiree,
            DecisionDeRotation.Decider(
                Session(expire: _maintenant.AddSeconds(-1)),
                _maintenant
            ).Issue
        );
    }

    [Fact]
    public void Un_jeton_qui_expire_a_la_seconde_pres_est_encore_valide()
    {
        Assert.Equal(
            IssueDeRotation.Acceptee,
            DecisionDeRotation.Decider(Session(expire: _maintenant), _maintenant).Issue
        );
    }

    [Fact]
    public void Un_jeton_revoque_est_refuse()
    {
        Assert.Equal(
            IssueDeRotation.Revoquee,
            DecisionDeRotation.Decider(
                Session(revoquee: _maintenant.AddMinutes(-5)),
                _maintenant
            ).Issue
        );
    }

    // LA branche qui protège les innocents : deux onglets, un rejeu réseau.
    [Fact]
    public void Un_rejeu_dans_les_trente_secondes_rend_le_jeton_suivant()
    {
        var suivant = Guid.NewGuid();

        var resultat = DecisionDeRotation.Decider(
            Session(consommee: _maintenant.AddSeconds(-29), remplacee: suivant),
            _maintenant
        );

        Assert.Equal(IssueDeRotation.RejeuDansLaGrace, resultat.Issue);
        Assert.Equal(suivant, resultat.SessionARendre);
        Assert.False(
            resultat.RevoquerLaFamille,
            "un rejeu dans la grâce ne doit JAMAIS révoquer : c'est un utilisateur légitime."
        );
    }

    [Fact]
    public void La_borne_exacte_de_trente_secondes_reste_dans_la_grace()
    {
        Assert.Equal(
            IssueDeRotation.RejeuDansLaGrace,
            DecisionDeRotation.Decider(
                Session(consommee: _maintenant.AddSeconds(-30), remplacee: Guid.NewGuid()),
                _maintenant
            ).Issue
        );
    }

    // LA branche qui attrape les voleurs.
    [Fact]
    public void Un_rejeu_au_dela_de_la_grace_revoque_toute_la_famille()
    {
        var resultat = DecisionDeRotation.Decider(
            Session(consommee: _maintenant.AddSeconds(-31), remplacee: Guid.NewGuid()),
            _maintenant
        );

        Assert.Equal(IssueDeRotation.ReemploiDetecte, resultat.Issue);
        Assert.True(resultat.RevoquerLaFamille);
        Assert.Null(resultat.SessionARendre);
    }

    [Fact]
    public void Un_jeton_consomme_sans_remplacant_est_un_reemploi()
    {
        // La chaîne est cassée : il n'y a rien à rendre, même dans la grâce.
        var resultat = DecisionDeRotation.Decider(
            Session(consommee: _maintenant.AddSeconds(-1), remplacee: null),
            _maintenant
        );

        Assert.Equal(IssueDeRotation.ReemploiDetecte, resultat.Issue);
        Assert.True(resultat.RevoquerLaFamille);
    }

    // L'ordre des gardes compte : une famille révoquée ne se ranime pas par une
    // rotation qui se trouverait dans la fenêtre de grâce.
    [Fact]
    public void Un_jeton_revoque_ET_consomme_reste_refuse()
    {
        Assert.Equal(
            IssueDeRotation.Revoquee,
            DecisionDeRotation.Decider(
                Session(
                    consommee: _maintenant.AddSeconds(-1),
                    revoquee: _maintenant.AddSeconds(-1),
                    remplacee: Guid.NewGuid()
                ),
                _maintenant
            ).Issue
        );
    }

    [Fact]
    public void Un_jeton_expire_ET_consomme_est_refuse_pour_expiration()
    {
        Assert.Equal(
            IssueDeRotation.Expiree,
            DecisionDeRotation.Decider(
                Session(
                    consommee: _maintenant.AddSeconds(-1),
                    remplacee: Guid.NewGuid(),
                    expire: _maintenant.AddSeconds(-1)
                ),
                _maintenant
            ).Issue
        );
    }

    // L'état transporte l'identité de la session ET celle de sa famille : le
    // magasin a besoin des deux, l'une pour consommer la ligne, l'autre pour
    // révoquer la chaîne entière quand un réemploi est détecté.
    [Fact]
    public void L_etat_transporte_l_identite_de_la_session_et_de_sa_famille()
    {
        var session = Guid.NewGuid();
        var famille = Guid.NewGuid();

        var etat = new EtatDeSession(
            session,
            famille,
            _maintenant.AddDays(1),
            ConsommeeLe: null,
            RevoqueeLe: null,
            RemplaceePar: null
        );

        Assert.Equal(session, etat.Id);
        Assert.Equal(famille, etat.FamilleId);
    }

    [Fact]
    public void Les_parametres_suivent_la_spec()
    {
        Assert.Equal(TimeSpan.FromMinutes(15), ParametresDeSession.DureeDuJetonDAcces);
        Assert.Equal(TimeSpan.FromDays(14), ParametresDeSession.DureeDuRafraichissement);
        Assert.Equal(TimeSpan.FromSeconds(30), ParametresDeSession.FenetreDeGrace);
    }
}
