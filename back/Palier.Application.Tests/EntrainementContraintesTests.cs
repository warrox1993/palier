using Palier.Application.Entrainement;

namespace Palier.Application.Tests;

/// <summary>
/// La déclaration de contraintes et sa sévérité — ce qui se refuse sans base.
/// </summary>
/// <remarks>
/// <b>La sévérité est LE réglage de l'utilisateur pour l'adaptation par
/// contrainte</b>, et il est par contrainte, pas global. Une épaule strictement
/// contre-indiquée et un genou légèrement sensible n'appellent pas le même
/// traitement ; un réglage global aurait forcé un seul comportement pour les
/// deux, et l'utilisateur aurait choisi le pire des deux compromis.
/// </remarks>
public sealed class EntrainementContraintesTests
{
    private static DeclarationDUneContrainte Une(
        string region = "genou",
        string? severite = null,
        string? note = null
    ) => new(region, severite, note);

    private static DeclarationDeContraintes Liste(params string[] regions) =>
        new([.. regions.Select(r => Une(r))]);

    // ================================================================
    // La liste
    // ================================================================

    [Fact]
    public void Une_liste_VIDE_est_legitime() =>
        // C'est ainsi qu'on déclare n'avoir aucune contrainte, et qu'on retire
        // la dernière. La refuser rendrait le retrait impossible par la route
        // de remplacement.
        Assert.Null(new DeclarationDeContraintes([]).Faute);

    [Fact]
    public void Une_liste_ABSENTE_vaut_une_liste_vide()
    {
        var sans = new DeclarationDeContraintes(null);

        Assert.Null(sans.Faute);
        Assert.Empty(sans.RegionsOuVide);
    }

    [Fact]
    public void Les_QUATRE_regions_du_document_passent() =>
        Assert.Null(Liste("cervicale", "lombaire", "epaule", "genou").Faute);

    [Fact]
    public void La_CASSE_est_toleree() => Assert.Null(Liste("Cervicale", "GENOU").Faute);

    [Fact]
    public void Les_DOUBLONS_sont_toleres() =>
        // Déclarer deux fois « genou » dit la même chose, et la dernière
        // sévérité donnée l'emporte. Un refus obligerait le client à
        // dédupliquer avant d'envoyer, pour zéro bénéfice.
        Assert.Null(Liste("genou", "genou").Faute);

    [Theory]
    [InlineData("poignet")]
    [InlineData("")]
    [InlineData("cervicale,lombaire")]
    public void Une_region_HORS_LISTE_est_refusee(string region) =>
        Assert.Equal("ContrainteInvalide", Liste(region).Faute);

    [Fact]
    public void Une_contrainte_NULLE_dans_la_liste_est_refusee_SANS_lever() =>
        // Un tableau JSON peut porter `null` parmi ses éléments. Sans ce
        // contrôle, la lecture de `.Region` aurait levé — un 500 sur une
        // requête mal formée, là où un 400 dit ce qui ne va pas.
        Assert.Equal(
            "ContrainteInvalide",
            new DeclarationDeContraintes([null!]).Faute
        );

    [Fact]
    public void Une_liste_PLUS_LONGUE_que_la_liste_fermee_est_refusee() =>
        Assert.Equal(
            "TropDeContraintes",
            Liste("genou", "genou", "genou", "genou", "genou").Faute
        );

    [Fact]
    public void Le_PLAFOND_suit_la_liste_fermee() =>
        // Si une cinquième région s'ajoute à l'énumération, ce plafond suit
        // tout seul. L'épreuve garde le lien plutôt qu'un nombre écrit deux
        // fois.
        Assert.Null(Liste([.. Contraintes.Toutes]).Faute);

    // ================================================================
    // La sévérité — le réglage
    // ================================================================

    [Theory]
    [InlineData("leger")]
    [InlineData("modere")]
    [InlineData("strict")]
    public void Les_TROIS_severites_du_document_passent(string severite) =>
        Assert.Null(new DeclarationDeContraintes([Une(severite: severite)]).Faute);

    [Fact]
    public void Une_severite_ABSENTE_prend_le_defaut()
    {
        // Ne rien dire est un choix légitime : le formulaire peut ne pas poser
        // la question. Refuser aurait obligé chaque client à trancher pour
        // l'utilisateur.
        var sans = Une(severite: null);

        Assert.Null(sans.Faute);
        Assert.Equal(Severites.ParDefaut, sans.SeveriteLue);
    }

    [Fact]
    public void Le_defaut_est_MODERE_et_non_LEGER() =>
        // Le défaut d'un produit dont la promesse est d'éviter les blessures ne
        // peut pas être le moins protecteur des trois — quelqu'un qui prend la
        // peine de déclarer une contrainte signale déjà qu'elle compte. Et ce
        // n'est pas `strict` non plus, qui replierait des exercices pour une
        // gêne passagère.
        Assert.Equal(Severite.Modere, Severites.ParDefaut);

    [Theory]
    [InlineData("urgent")]
    [InlineData("grave")]
    [InlineData("1")]
    public void Une_severite_INCONNUE_est_refusee(string severite) =>
        // Distincte de l'absence : dire « urgent » est une erreur qu'il faut
        // signaler, pas interpréter.
        Assert.Equal(
            "SeveriteInvalide",
            new DeclarationDeContraintes([Une(severite: severite)]).Faute
        );

    [Fact]
    public void La_CASSE_d_une_severite_est_toleree()
    {
        Assert.True(Severites.Lire("STRICT", out var lue));
        Assert.Equal(Severite.Strict, lue);
    }

    [Theory]
    [InlineData(Severite.Leger, "leger")]
    [InlineData(Severite.Modere, "modere")]
    [InlineData(Severite.Strict, "strict")]
    public void La_forme_stockee_d_une_severite_est_sans_accent(Severite s, string attendu) =>
        Assert.Equal(attendu, Severites.EnBase(s));

    [Fact]
    public void Une_severite_FORCEE_leve() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Severites.EnBase((Severite)42));

    [Fact]
    public void Les_TROIS_severites_sont_exposees() =>
        Assert.Equal(["leger", "modere", "strict"], Severites.Toutes);

    // ================================================================
    // La note
    // ================================================================

    [Fact]
    public void Une_note_ABSENTE_passe() => Assert.Null(Une(note: null).Faute);

    [Fact]
    public void Une_note_VIDE_vaut_une_note_absente() =>
        // Un champ laissé vide dans un formulaire arrive comme chaîne vide.
        // La stocker créerait une donnée de santé qui ne dit rien.
        Assert.Null(Une(note: "   ").NoteNettoyee);

    [Fact]
    public void Une_note_est_DEBARRASSEE_de_ses_espaces() =>
        Assert.Equal("opérée en 2019", Une(note: "  opérée en 2019  ").NoteNettoyee);

    [Fact]
    public void Une_note_TROP_LONGUE_est_refusee() =>
        // La colonne est `text`, donc PostgreSQL ne borne rien.
        Assert.Equal(
            "NoteTropLongue",
            Une(
                note: new string('a', DeclarationDUneContrainte.LongueurMaximaleDeLaNote + 1)
            ).Faute
        );

    [Fact]
    public void Une_note_A_LA_BORNE_passe() =>
        Assert.Null(
            Une(note: new string('a', DeclarationDUneContrainte.LongueurMaximaleDeLaNote)).Faute
        );

    [Fact]
    public void La_REGION_est_contrôlée_AVANT_la_severite()
    {
        // L'ordre est figé par l'épreuve : sans elle, une réorganisation
        // changerait le code rendu à l'utilisateur, et le front traduirait
        // autre chose que la cause réelle.
        var fautive = new DeclarationDeContraintes([Une("poignet", "urgent")]);
        Assert.Equal("ContrainteInvalide", fautive.Faute);
    }

    // ================================================================
    // La forme rendue
    // ================================================================

    [Fact]
    public void Une_contrainte_rendue_porte_ses_quatre_champs()
    {
        var instant = new DateTimeOffset(2026, 8, 23, 9, 0, 0, TimeSpan.Zero);
        var rendue = new ContrainteRendue("lombaire", "strict", "hernie L5", instant);

        Assert.Equal("lombaire", rendue.Region);
        Assert.Equal("strict", rendue.Severite);
        Assert.Equal("hernie L5", rendue.Note);
        Assert.Equal(instant, rendue.DeclareeLe);
    }
}
