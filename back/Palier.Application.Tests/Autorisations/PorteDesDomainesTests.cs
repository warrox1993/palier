using Palier.Application.Autorisations;

namespace Palier.Application.Tests.Autorisations;

/// <summary>
/// Les deux verrous de la porte nutrition, et la porte d'entraînement qui ne se
/// verrouille pas.
/// </summary>
public sealed class PorteDesDomainesTests
{
    [Theory]
    [InlineData(true, true, true)] // adresse vérifiée + consentement → ouverte
    [InlineData(false, true, false)] // adresse non vérifiée          → fermée
    [InlineData(true, false, false)] // consentement refusé           → fermée
    [InlineData(false, false, false)] // ni l'un ni l'autre           → fermée
    public void La_nutrition_exige_les_DEUX_verrous(
        bool emailVerifie,
        bool consentement,
        bool ouverte
    )
    {
        // Les quatre combinaisons existent réellement, et trois ferment. Ne
        // tester que la première et la dernière laisserait passer un `||` mis
        // pour un `&&` : « vérifié OU consenti » ouvrirait la nutrition à qui
        // a refusé le consentement.
        Assert.Equal(ouverte, PorteDesDomaines.NutritionOuverte(emailVerifie, consentement));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void L_entrainement_reste_ouvert_dans_TOUS_les_cas(bool emailVerifie, bool consentement)
    {
        // docs/09-comptes.md § 2 : « en cas de refus : accès à l'entraînement ».
        //
        // Un verrou qui fermerait aussi l'entraînement serait une régression
        // produit — et le genre qu'on ne remarque pas, parce qu'elle ressemble à
        // de la prudence. Cette épreuve est là pour qu'elle rougisse.
        Assert.True(
            PorteDesDomaines.EntrainementOuvert(emailVerifie, consentement),
            $"l'entraînement s'est fermé sur (email={emailVerifie}, consentement={consentement}) : "
                + "le produit refuse à quelqu'un de noter sa séance parce qu'il n'a pas "
                + "consenti au traitement de données de santé."
        );
    }
}
