using Palier.Application.Entrainement;

namespace Palier.Application.Tests;

/// <summary>
/// Les contrats de l'entraînement — ce qui se décide SANS base.
/// </summary>
/// <remarks>
/// <para>
/// <b>Ces épreuves vivent ici et non dans <c>Palier.Database.Tests</c>, et ce
/// n'est pas un rangement.</b> Le seuil de <c>Palier.Application</c> est mesuré
/// par ces épreuves-là et par elles seules : un contrat couvert uniquement par
/// une épreuve d'intégration ferait tomber le seuil, et c'est exactement ce que
/// le dépôt veut — <c>CLAUDE.md</c> § 4, « un membre public que rien n'appelle
/// n'est pas couvert, donc le seuil échoue ». C'est la seule détection de code
/// mort PUBLIC dont Roslyn est incapable.
/// </para>
///
/// <para>
/// Le bénéfice second est qu'un bord se juge en quelques microsecondes, sans
/// conteneur PostgreSQL. Éprouver « 6 est refusé » à travers une transaction et
/// un moteur reviendrait à mesurer la plomberie plutôt que la règle.
/// </para>
/// </remarks>
public sealed class EntrainementContratsTests
{
    // ================================================================
    // L'énergie de fin de séance
    // ================================================================

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    public void Une_energie_DANS_les_bornes_est_valide(int energie) =>
        Assert.True(new ClotureDeSeance(null, energie).EnergieValide);

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(-1)]
    [InlineData(int.MaxValue)]
    public void Une_energie_HORS_des_bornes_est_refusee(int energie) =>
        Assert.False(new ClotureDeSeance(null, energie).EnergieValide);

    [Fact]
    public void Une_energie_ABSENTE_est_valide() =>
        // Clore une séance sans noter son énergie doit rester possible. Exiger
        // la note ferait laisser les séances ouvertes — et une séance jamais
        // close ne compte dans aucun volume hebdomadaire.
        Assert.True(new ClotureDeSeance(null, null).EnergieValide);

    [Fact]
    public void Les_bornes_annoncees_sont_celles_qui_sont_APPLIQUEES()
    {
        // Sans ces deux lignes, les constantes pourraient dire 1 et 5 pendant
        // que le test de validité en appliquerait d'autres : les épreuves
        // ci-dessus resteraient vertes en citant leurs propres nombres.
        Assert.True(new ClotureDeSeance(null, ClotureDeSeance.EnergieMinimale).EnergieValide);
        Assert.True(new ClotureDeSeance(null, ClotureDeSeance.EnergieMaximale).EnergieValide);
        Assert.False(new ClotureDeSeance(null, ClotureDeSeance.EnergieMinimale - 1).EnergieValide);
        Assert.False(new ClotureDeSeance(null, ClotureDeSeance.EnergieMaximale + 1).EnergieValide);
    }

    // ================================================================
    // La taille d'une page
    // ================================================================

    [Fact]
    public void Une_limite_DEMESUREE_est_ramenee_au_plafond() =>
        // Sans le plafond, `?limite=1000000` fait matérialiser toute la table
        // en mémoire — un déni de service qu'un compte authentifié déclenche en
        // une requête, et que ni RLS ni la limitation par adresse n'arrêtent.
        Assert.Equal(PageDeSeances.TailleMaximale, PageDeSeances.Borner(1_000_000));

    [Theory]
    [InlineData(0)]
    [InlineData(-42)]
    [InlineData(int.MinValue)]
    public void Une_limite_NULLE_OU_NEGATIVE_est_ramenee_a_un(int demandee) =>
        // Zéro rendrait une page vide dont le curseur ne progresse jamais : le
        // client boucle indéfiniment sans erreur. C'est un mode de défaillance
        // silencieux, donc celui qui coûte le plus cher à diagnostiquer.
        Assert.Equal(1, PageDeSeances.Borner(demandee));

    [Fact]
    public void Une_limite_ABSENTE_prend_la_valeur_par_defaut() =>
        Assert.Equal(PageDeSeances.TailleParDefaut, PageDeSeances.Borner(null));

    [Fact]
    public void Une_limite_RAISONNABLE_passe_telle_quelle() =>
        Assert.Equal(7, PageDeSeances.Borner(7));

    // ================================================================
    // Les formes rendues
    // ================================================================

    [Fact]
    public void Une_ouverture_conserve_ce_qu_on_lui_donne()
    {
        var instant = new DateTimeOffset(2026, 8, 23, 9, 0, 0, TimeSpan.Zero);
        var ouverture = new OuvertureDeSeance(instant, 7.5m, "jambes");

        Assert.Equal(instant, ouverture.Debut);
        Assert.Equal(7.5m, ouverture.HeuresDeSommeil);
        Assert.Equal("jambes", ouverture.Note);
    }

    [Fact]
    public void Une_page_VIDE_ne_porte_aucun_curseur()
    {
        // Le contrat de fin de parcours : pas de curseur, donc le client
        // s'arrête. Un curseur rendu sur une page vide ferait boucler.
        var page = new PageDeSeances([], null, null);

        Assert.Empty(page.Elements);
        Assert.Null(page.SuivantAvant);
        Assert.Null(page.SuivantAvantId);
    }

    [Fact]
    public void Une_page_PLEINE_porte_les_DEUX_moitiés_du_curseur()
    {
        // Les deux, ou aucune. Une seule serait un curseur qu'on croit
        // composite et qui ne l'est pas — le pire des deux mondes : il aurait
        // le coût du composite et le défaut du simple.
        var instant = new DateTimeOffset(2026, 8, 23, 9, 0, 0, TimeSpan.Zero);
        var identifiant = Guid.NewGuid();
        var seance = new SeanceRendue(identifiant, instant, null, null, null, null);
        var page = new PageDeSeances([seance], instant, identifiant);

        Assert.Single(page.Elements);
        Assert.Equal(identifiant, page.Elements[0].Id);
        Assert.Equal(instant, page.SuivantAvant);
        Assert.Equal(identifiant, page.SuivantAvantId);
    }

    [Fact]
    public void Une_seance_rendue_conserve_ses_six_champs()
    {
        var debut = new DateTimeOffset(2026, 8, 23, 9, 0, 0, TimeSpan.Zero);
        var fin = debut.AddHours(1);
        var rendue = new SeanceRendue(Guid.Empty, debut, fin, 7.5m, 4, "dos");

        Assert.Equal(Guid.Empty, rendue.Id);
        Assert.Equal(debut, rendue.Debut);
        Assert.Equal(fin, rendue.Fin);
        Assert.Equal(7.5m, rendue.HeuresDeSommeil);
        Assert.Equal(4, rendue.Energie);
        Assert.Equal("dos", rendue.Note);
    }
}
