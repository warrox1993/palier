using Palier.Application.Entrainement;

namespace Palier.Application.Tests;

/// <summary>
/// La déclaration de contraintes — ce qui se refuse sans base.
/// </summary>
public sealed class EntrainementContraintesTests
{
    [Fact]
    public void Une_liste_VIDE_est_legitime() =>
        // C'est ainsi qu'on déclare n'avoir aucune contrainte, et c'est ainsi
        // qu'on retire la dernière. La refuser rendrait le retrait impossible
        // par la route de remplacement.
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
        Assert.Null(
            new DeclarationDeContraintes(["cervicale", "lombaire", "epaule", "genou"]).Faute
        );

    [Fact]
    public void La_CASSE_est_toleree() =>
        Assert.Null(new DeclarationDeContraintes(["Cervicale", "GENOU"]).Faute);

    [Fact]
    public void Les_DOUBLONS_sont_toleres() =>
        // Déclarer deux fois « genou » dit la même chose que le déclarer une
        // fois. Un refus obligerait le client à dédupliquer avant d'envoyer,
        // pour zéro bénéfice — et `unique (owner_id, region)` reste le dernier
        // mot en base.
        Assert.Null(new DeclarationDeContraintes(["genou", "genou"]).Faute);

    [Theory]
    [InlineData("poignet")]
    [InlineData("")]
    [InlineData("cervicale,lombaire")]
    public void Une_region_HORS_LISTE_est_refusee(string region) =>
        Assert.Equal("ContrainteInvalide", new DeclarationDeContraintes([region]).Faute);

    [Fact]
    public void Une_liste_PLUS_LONGUE_que_la_liste_fermee_est_refusee() =>
        // Cinq entrées valides ne peuvent nommer que quatre régions distinctes,
        // donc la liste porte forcément des doublons — tolérés — mais une
        // liste de mille entrées serait un déni de service à peu de frais.
        // Le plafond est la taille de la liste fermée elle-même : il n'y a
        // rien à borner d'autre.
        Assert.Equal(
            "TropDeContraintes",
            new DeclarationDeContraintes(
                ["genou", "genou", "genou", "genou", "genou"]
            ).Faute
        );

    [Fact]
    public void Le_PLAFOND_suit_la_liste_fermee() =>
        // Si une cinquième région s'ajoute à l'énumération, ce plafond suit
        // tout seul. L'épreuve garde le lien plutôt qu'un nombre écrit deux
        // fois.
        Assert.Null(
            new DeclarationDeContraintes([.. Contraintes.Toutes]).Faute
        );

    [Fact]
    public void Une_contrainte_rendue_porte_sa_region_et_sa_date()
    {
        var instant = new DateTimeOffset(2026, 8, 23, 9, 0, 0, TimeSpan.Zero);
        var rendue = new ContrainteRendue("lombaire", instant);

        Assert.Equal("lombaire", rendue.Region);
        Assert.Equal(instant, rendue.DeclareeLe);
    }
}
