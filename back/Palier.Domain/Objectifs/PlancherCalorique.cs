using Palier.Domain.Grandeurs;

namespace Palier.Domain.Objectifs;

/// <summary>
/// Le plancher calorique de <c>docs/01-conformite.md</c> § 5.
/// </summary>
/// <remarks>
/// « Impossible de fixer un objectif sous 1 200 kcal (femme) ou 1 500 kcal
/// (homme). <b>Non contournable</b> », et « ces règles sont testées
/// unitairement et ne peuvent pas être désactivées par configuration ».
///
/// Il n'existe donc <b>aucun paramètre</b> permettant de lever ce plancher, et
/// c'est délibéré : une application de comptage calorique attire mécaniquement
/// des personnes en trouble du comportement alimentaire, et une option de
/// contournement serait la première qu'elles chercheraient.
///
/// Ces deux valeurs sont les bornes <b>basses</b> des recommandations
/// cliniques, qui vont de 1 200 à 1 500 kcal pour la femme et de 1 500 à 1 800
/// pour l'homme.
/// </remarks>
public static class PlancherCalorique
{
    public static decimal FemmeKcal => 1200m;

    public static decimal HommeKcal => 1500m;

    public static Energie Pour(Sexe sexe) =>
        sexe switch
        {
            Sexe.Femme => Energie.DepuisKilocalories(FemmeKcal),
            Sexe.Homme => Energie.DepuisKilocalories(HommeKcal),
            _ => throw new ArgumentOutOfRangeException(nameof(sexe), sexe, null),
        };
}
