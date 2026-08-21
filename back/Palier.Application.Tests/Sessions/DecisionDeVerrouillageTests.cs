using Palier.Application.Sessions;

namespace Palier.Application.Tests.Sessions;

/// <summary>
/// Le verrouillage progressif — ce qu'Identity ne sait pas faire.
/// </summary>
/// <remarks>
/// <para>
/// <c>LockoutOptions</c> apporte un compteur d'échecs et <b>une</b> durée de
/// verrouillage. Il lui manque exactement les deux choses que
/// <c>docs/09-comptes.md</c> § 1 exige : une <b>fenêtre</b> — son compteur est
/// cumulatif, un échec d'il y a trois mois compte encore — et une
/// <b>escalade</b> — <c>DefaultLockoutTimeSpan</c> est une durée unique.
/// </para>
///
/// <para>
/// D'où cette décision, pure : aucune base, aucune horloge, l'instant est un
/// paramètre. C'est ce qui la rend éprouvable à 100 %, et c'est aussi ce qui
/// permet de vérifier « quinze minutes plus tard » sans attendre.
/// </para>
/// </remarks>
public sealed class DecisionDeVerrouillageTests
{
    private static readonly DateTimeOffset _maintenant = new(2026, 8, 21, 12, 0, 0, TimeSpan.Zero);

    private static EtatDeVerrouillage Vierge => new(0, null, 0, null);

    // ================================================================
    // Le compteur
    // ================================================================

    [Fact]
    public void Un_echec_isole_ne_verrouille_pas()
    {
        var apres = DecisionDeVerrouillage.ApresUnEchec(Vierge, _maintenant);

        Assert.False(apres.Verrouille);
        Assert.Equal(1, apres.EchecsConsecutifs);
        Assert.Null(apres.Jusqua);
    }

    [Fact]
    public void Le_CINQUIEME_echec_verrouille()
    {
        var etat = Vierge;

        for (var i = 0; i < DecisionDeVerrouillage.EchecsAvantVerrouillage - 1; i++)
        {
            var intermediaire = DecisionDeVerrouillage.ApresUnEchec(etat, _maintenant);
            Assert.False(intermediaire.Verrouille, $"verrouillé dès l'échec {i + 1}");
            etat = Suivant(etat, intermediaire, _maintenant);
        }

        var dernier = DecisionDeVerrouillage.ApresUnEchec(etat, _maintenant);

        Assert.True(dernier.Verrouille);
        Assert.Equal(_maintenant + DecisionDeVerrouillage.Paliers[0], dernier.Jusqua);
    }

    [Fact]
    public void Le_compteur_repart_a_UN_si_le_dernier_echec_est_HORS_FENETRE()
    {
        // La fenêtre est ce qui manque à Identity. Sans elle, quatre échecs
        // étalés sur trois mois plus un aujourd'hui verrouillent le compte —
        // et l'utilisateur qui tape mal son mot de passe une fois par trimestre
        // finit enfermé sans comprendre.
        var vieux = new EtatDeVerrouillage(
            4,
            _maintenant - DecisionDeVerrouillage.Fenetre - TimeSpan.FromSeconds(1),
            0,
            null
        );

        var apres = DecisionDeVerrouillage.ApresUnEchec(vieux, _maintenant);

        Assert.False(apres.Verrouille, "un échec hors fenêtre a compté");
        Assert.Equal(1, apres.EchecsConsecutifs);
    }

    [Fact]
    public void Un_echec_JUSTE_DANS_la_fenetre_compte_encore()
    {
        // Le bord exact. Sans cette épreuve, une comparaison stricte au lieu
        // d'une comparaison large — ou l'inverse — passerait inaperçue.
        var limite = new EtatDeVerrouillage(4, _maintenant - DecisionDeVerrouillage.Fenetre, 0, null);

        var apres = DecisionDeVerrouillage.ApresUnEchec(limite, _maintenant);

        Assert.True(apres.Verrouille, "un échec pile à la limite de la fenêtre a été oublié");
    }

    // ================================================================
    // L'escalade
    // ================================================================

    [Fact]
    public void Chaque_RECIDIVE_verrouille_plus_longtemps()
    {
        // Cinq minutes, puis quinze, puis soixante. Une durée unique laisse un
        // attaquant patient reprendre cinq essais toutes les cinq minutes,
        // indéfiniment : 1 440 essais par jour sur un seul compte.
        for (var subis = 0; subis < DecisionDeVerrouillage.Paliers.Count; subis++)
        {
            var etat = new EtatDeVerrouillage(
                DecisionDeVerrouillage.EchecsAvantVerrouillage - 1,
                _maintenant,
                subis,
                null
            );

            var apres = DecisionDeVerrouillage.ApresUnEchec(etat, _maintenant);

            Assert.True(apres.Verrouille);
            Assert.Equal(_maintenant + DecisionDeVerrouillage.Paliers[subis], apres.Jusqua);
        }
    }

    [Fact]
    public void L_escalade_PLAFONNE_au_dernier_palier()
    {
        // Sans plafond, une liste indexée par le nombre de récidives lèverait
        // — et l'exception tomberait sur le chemin de connexion, en production,
        // le jour où quelqu'un se verrouille une fois de trop.
        var acharne = new EtatDeVerrouillage(
            DecisionDeVerrouillage.EchecsAvantVerrouillage - 1,
            _maintenant,
            DecisionDeVerrouillage.Paliers.Count + 50,
            null
        );

        var apres = DecisionDeVerrouillage.ApresUnEchec(acharne, _maintenant);

        Assert.True(apres.Verrouille);
        Assert.Equal(_maintenant + DecisionDeVerrouillage.Paliers[^1], apres.Jusqua);
    }

    [Fact]
    public void Un_verrouillage_remet_le_compteur_d_echecs_a_zero()
    {
        // Sinon le verrouillage suivant tomberait au premier échec après
        // l'expiration du précédent, et non au cinquième.
        var etat = new EtatDeVerrouillage(
            DecisionDeVerrouillage.EchecsAvantVerrouillage - 1,
            _maintenant,
            0,
            null
        );

        var apres = DecisionDeVerrouillage.ApresUnEchec(etat, _maintenant);

        Assert.Equal(0, apres.EchecsConsecutifs);
        Assert.Equal(1, apres.VerrouillagesSubis);
    }

    // ================================================================
    // Le verrou lui-même
    // ================================================================

    [Fact]
    public void Un_compte_VERROUILLE_le_reste_jusqu_a_l_echeance()
    {
        var verrouille = new EtatDeVerrouillage(0, _maintenant, 1, _maintenant.AddMinutes(5));

        Assert.True(DecisionDeVerrouillage.EstVerrouille(verrouille, _maintenant));
        Assert.True(
            DecisionDeVerrouillage.EstVerrouille(verrouille, _maintenant.AddMinutes(4).AddSeconds(59))
        );
    }

    [Fact]
    public void Le_verrou_TOMBE_a_l_echeance()
    {
        var verrouille = new EtatDeVerrouillage(0, _maintenant, 1, _maintenant.AddMinutes(5));

        Assert.False(DecisionDeVerrouillage.EstVerrouille(verrouille, _maintenant.AddMinutes(5)));
    }

    [Fact]
    public void Un_compte_JAMAIS_verrouille_ne_l_est_pas()
    {
        Assert.False(DecisionDeVerrouillage.EstVerrouille(Vierge, _maintenant));
    }

    [Fact]
    public void Une_REUSSITE_efface_tout()
    {
        // Y compris les récidives, et c'est un compromis assumé : un attaquant
        // n'a jamais de réussite, donc son escalade tient. Le vrai utilisateur
        // qui retrouve son mot de passe repart à neuf, ce qui est le but.
        var abime = new EtatDeVerrouillage(4, _maintenant, 3, _maintenant.AddMinutes(60));

        var apres = DecisionDeVerrouillage.ApresUneReussite(abime);

        Assert.False(apres.Verrouille);
        Assert.Equal(0, apres.EchecsConsecutifs);
        Assert.Equal(0, apres.VerrouillagesSubis);
        Assert.Null(apres.Jusqua);
    }

    // ================================================================
    // Les paliers eux-mêmes
    // ================================================================

    [Fact]
    public void Les_paliers_sont_CROISSANTS_et_non_vides()
    {
        // Quatrième question du franchissement : un garde-fou sans cible doit
        // crier. Une liste vide ferait lever l'indexation ; une liste
        // décroissante ferait RÉCOMPENSER la récidive, et rien ne le dirait.
        Assert.NotEmpty(DecisionDeVerrouillage.Paliers);

        for (var i = 1; i < DecisionDeVerrouillage.Paliers.Count; i++)
        {
            Assert.True(
                DecisionDeVerrouillage.Paliers[i] > DecisionDeVerrouillage.Paliers[i - 1],
                $"le palier {i} ({DecisionDeVerrouillage.Paliers[i]}) n'est pas plus long que "
                    + $"le précédent ({DecisionDeVerrouillage.Paliers[i - 1]}) : la récidive est récompensée."
            );
        }
    }

    [Fact]
    public void Les_VALEURS_sont_celles_du_document()
    {
        // `docs/09-comptes.md` § 1 : « 5 tentatives par IP et par compte sur
        // 15 minutes, puis verrouillage temporaire progressif ».
        //
        // Cette épreuve existe parce que TOUTES les autres se réfèrent aux
        // constantes plutôt qu'aux nombres : elles resteraient vertes si le
        // seuil passait à cinquante. Elle est le seul endroit qui ancre le code
        // au document — et le seul qui rougira si quelqu'un desserre la vis.
        Assert.Equal(5, DecisionDeVerrouillage.EchecsAvantVerrouillage);
        Assert.Equal(TimeSpan.FromMinutes(15), DecisionDeVerrouillage.Fenetre);
        Assert.Equal(
            new[] { TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15), TimeSpan.FromMinutes(60) },
            DecisionDeVerrouillage.Paliers
        );
    }

    /// <summary>L'état qu'on garderait en base après une décision.</summary>
    private static EtatDeVerrouillage Suivant(
        EtatDeVerrouillage avant,
        ResultatDeVerrouillage apres,
        DateTimeOffset maintenant
    ) =>
        avant with
        {
            EchecsConsecutifs = apres.EchecsConsecutifs,
            DernierEchec = maintenant,
            VerrouillagesSubis = apres.VerrouillagesSubis,
            VerrouilleJusqua = apres.Jusqua,
        };
}
