namespace Palier.Domain.Depense;

/// <summary>
/// Le niveau d'activité hors entraînement, déduit de deux questions concrètes
/// posées à l'accueil : le temps de marche quotidien et le caractère assis ou
/// debout du métier.
/// </summary>
/// <remarks>
/// Ces catégories sont imprécises, et <c>docs/04-nutrition.md</c> § 1 l'assume
/// : elles ne servent que trois semaines, le temps que le TDEE adaptatif les
/// remplace par une mesure réelle.
///
/// La lecture automatique des pas n'est pas une option — l'API REST de Google
/// Fit ferme, Health Connect n'est accessible que depuis une application
/// Android native, et HealthKit n'expose aucune API web.
/// </remarks>
public enum NiveauActivite
{
    /// <summary>Bureau, moins de 30 minutes de marche.</summary>
    BureauPeuDeMarche,

    /// <summary>Bureau, 30 à 60 minutes de marche.</summary>
    BureauMarcheModeree,

    /// <summary>Mixte assis et debout, marche régulière.</summary>
    MixteMarcheReguliere,

    /// <summary>Métier debout ou très actif.</summary>
    MetierDebout,
}
