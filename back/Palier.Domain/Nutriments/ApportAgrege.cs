using Palier.Domain.Grandeurs;

namespace Palier.Domain.Nutriments;

/// <summary>
/// L'apport d'un nutriment sur une journée, alimentation et compléments
/// additionnés.
/// </summary>
/// <remarks>
/// <c>docs/04-nutrition.md</c> § 4 en fait « la règle centrale du produit » :
/// « additionner alimentation et compléments par nutriment, puis comparer aux
/// références. C'est la fonctionnalité qui n'existe nulle part ailleurs, et
/// c'est la raison d'être du produit. »
/// </remarks>
public sealed record ApportAgrege(
    string Cle,
    MasseNutriment Alimentation,
    MasseNutriment Complements)
{
    public MasseNutriment Total => MasseNutriment.Somme(Alimentation, Complements);
}
