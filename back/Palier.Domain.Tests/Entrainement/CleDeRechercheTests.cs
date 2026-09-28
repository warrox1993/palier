using Palier.Domain.Entrainement;

namespace Palier.Domain.Tests.Entrainement;

/// <summary>
/// La normalisation des noms pour la recherche — D73.
/// </summary>
public sealed class CleDeRechercheTests
{
    [Theory]
    [InlineData("Développé couché", "DEVELOPPE COUCHE")]
    [InlineData("developpe couche", "DEVELOPPE COUCHE")]
    [InlineData("DÉVELOPPÉ COUCHÉ", "DEVELOPPE COUCHE")]
    [InlineData("Élévation latérale", "ELEVATION LATERALE")]
    [InlineData("Soulevé de terre", "SOULEVE DE TERRE")]
    public void Les_accents_et_la_casse_DISPARAISSENT(string tape, string attendu)
    {
        // C'est tout l'intérêt de la colonne : le clavier d'un téléphone ne
        // propose pas spontanément les accents, et sans cette normalisation la
        // recherche par nom serait inutilisable là où elle sert le plus.
        Assert.Equal(attendu, CleDeRecherche.Normaliser(tape));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public void Un_terme_VIDE_rend_une_chaine_vide(string? rien)
    {
        // La chaîne vide dit au gestionnaire « aucun filtre ». Rendre `null`
        // l'aurait obligé à distinguer deux absences qui veulent dire la même
        // chose.
        Assert.Equal(string.Empty, CleDeRecherche.Normaliser(rien));
    }

    [Fact]
    public void Les_espaces_de_bordure_sont_RETIRES()
    {
        Assert.Equal("SQUAT", CleDeRecherche.Normaliser("  squat  "));
    }

    [Fact]
    public void Un_signe_hors_de_la_table_TRAVERSE_intact()
    {
        // Les chiffres, les tirets et la ponctuation ne sont pas des lettres
        // accentuées : les toucher casserait « rotation externe 90° » ou
        // « t-bar ».
        Assert.Equal("T-BAR 90 %", CleDeRecherche.Normaliser("t-bar 90 %"));
    }

    [Fact]
    public void Les_DEUX_tables_ont_la_meme_longueur()
    {
        // Si elles divergeaient, `translate` tronquerait côté moteur — les
        // caractères en trop seraient SUPPRIMÉS au lieu d'être remplacés — et
        // la normalisation du C# lèverait sur un index hors bornes. Deux
        // comportements différents pour la même faute, dont un silencieux.
        Assert.Equal(CleDeRecherche.Accentuees.Length, CleDeRecherche.Nues.Length);
    }

    [Fact]
    public void CHAQUE_lettre_accentuee_a_son_equivalent_nu()
    {
        // La correspondance une à une, vérifiée par la normalisation
        // elle-même. Une table mal alignée d'un cran donnerait « É » → « a » et
        // passerait l'épreuve de longueur ci-dessus sans broncher.
        for (var rang = 0; rang < CleDeRecherche.Accentuees.Length; rang++)
        {
            var accentuee = CleDeRecherche.Accentuees[rang].ToString();
            Assert.Equal(CleDeRecherche.Nues[rang].ToString(), CleDeRecherche.Normaliser(accentuee));
        }
    }

    // ================================================================
    // Le motif LIKE — les jokers ne sont pas au client
    // ================================================================

    [Fact]
    public void Le_motif_ENCADRE_le_terme_de_jokers()
    {
        // Chercher un FRAGMENT, et non une égalité : « squat » doit trouver
        // « Squat barre nuque ».
        Assert.Equal("%SQUAT%", CleDeRecherche.MotifPourLike("squat"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Un_terme_VIDE_ne_produit_AUCUN_motif(string? rien)
    {
        // La chaîne vide dit au gestionnaire « aucun filtre ». Rendre « %% »
        // aurait produit une clause inutile sur chaque listage.
        Assert.Equal(string.Empty, CleDeRecherche.MotifPourLike(rien));
    }

    [Fact]
    public void Le_POUR_CENT_tapé_est_échappé()
    {
        // Sans cela, un terme réduit à « % » devient « %%% » et rend le
        // catalogue entier — l'utilisateur pilote le motif.
        Assert.Equal(@"%\%%", CleDeRecherche.MotifPourLike("%"));
    }

    [Fact]
    public void Le_SOULIGNE_tapé_est_échappé()
    {
        // « _ » remplace n'importe quel caractère unique en LIKE. Non échappé,
        // « a_b » trouverait « axb ».
        Assert.Equal(@"%A\_B%", CleDeRecherche.MotifPourLike("a_b"));
    }

    [Fact]
    public void La_CONTRE_OBLIQUE_est_échappée_EN_PREMIER()
    {
        // L'ordre décide. Échapper les jokers d'abord doublerait ensuite les
        // contre-obliques qu'on vient d'introduire, et le motif chercherait
        // autre chose que ce qui a été tapé.
        //
        // Ici : `a\b` doit devenir `a\\b` — une contre-oblique échappée, donc
        // cherchée littéralement — et non `a\b`, où elle échapperait le « b ».
        Assert.Equal(@"%A\\B%", CleDeRecherche.MotifPourLike(@"a\b"));
    }

    [Fact]
    public void Un_terme_MELANGEANT_les_trois_reste_littéral()
    {
        Assert.Equal(@"%100\%\_A\\B%", CleDeRecherche.MotifPourLike(@"100%_a\b"));
    }

    [Fact]
    public void Le_motif_NORMALISE_aussi_les_accents()
    {
        // Il passe par `Normaliser` : deux règles à tenir séparément auraient
        // fini par diverger.
        Assert.Equal("%DEVELOPPE%", CleDeRecherche.MotifPourLike("Développé"));
    }

    [Fact]
    public void L_expression_SQL_PORTE_les_deux_tables()
    {
        // Elle est engendrée à partir des constantes plutôt que recopiée : cette
        // épreuve constate que le lien tient. Si quelqu'un réécrivait
        // l'expression en dur, elle rougirait à la première modification de la
        // table.
        var expression = CleDeRecherche.ExpressionSql;

        Assert.Contains(CleDeRecherche.Accentuees, expression, StringComparison.Ordinal);
        Assert.Contains(CleDeRecherche.Nues, expression, StringComparison.Ordinal);

        // `upper` et non `lower` : la casse choisie doit être celle de
        // `Normaliser`, sinon les deux clés ne se rencontrent jamais.
        Assert.Contains("upper(", expression, StringComparison.Ordinal);
        Assert.DoesNotContain("lower(", expression, StringComparison.Ordinal);

        // `coalesce` sur les deux noms : sans lui, `'x' || null` vaudrait
        // `null`, et la clé de tout exercice personnalisé serait nulle.
        Assert.Contains("coalesce(name_fr", expression, StringComparison.Ordinal);
        Assert.Contains("coalesce(name_en", expression, StringComparison.Ordinal);
    }
}
