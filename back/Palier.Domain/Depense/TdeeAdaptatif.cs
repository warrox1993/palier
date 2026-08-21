using Palier.Domain.Grandeurs;

namespace Palier.Domain.Depense;

/// <summary>
/// Ce qu'on sait d'une période d'observation : combien de jours ont été
/// saisis, combien de pesées ont eu lieu, et ce qu'il en ressort.
/// </summary>
public sealed record FenetreDeMesure(
    int JoursDeSaisieComplete,
    int NombreDePesees,
    int JoursCouverts,
    Energie ApportMoyenQuotidien,
    decimal VariationDePoidsKg);

/// <summary>
/// La dépense énergétique <b>mesurée</b> sur une période, plutôt qu'estimée par
/// une formule. <c>docs/04-nutrition.md</c> § 1 : « à la semaine 4, [la formule]
/// n'est plus utilisée du tout ».
/// </summary>
public static class TdeeAdaptatif
{
    /// <summary>Jours de saisie alimentaire jugée complète, minimum.</summary>
    public static int JoursDeSaisieMinimum => 14;

    /// <summary>Pesées sur la période, minimum.</summary>
    public static int PeseesMinimum => 10;

    /// <summary>
    /// Le contenu énergétique d'un kilogramme de masse corporelle perdue.
    /// </summary>
    /// <remarks>
    /// C'est la règle de Wishnofsky, et elle est contestée : elle ignore
    /// l'adaptation métabolique, et le modèle dynamique de Hall a montré qu'en
    /// <b>prédiction</b> elle annonce environ le double de la perte réelle sur
    /// une première année. L'usage fait ici est <b>rétrospectif</b> — inférer
    /// une dépense passée à partir d'une variation constatée — ce qui est un
    /// autre problème et reste défendable. La composition de la perte, faite de
    /// masse grasse et de masse maigre en proportions variables, affecte
    /// néanmoins le coefficient : il est exposé pour pouvoir être corrigé sans
    /// fouiller le code.
    /// </remarks>
    public static decimal KilocaloriesParKilogramme => 7700m;

    /// <summary>
    /// La dépense inférée, ou <c>null</c> si la fenêtre ne permet pas de
    /// conclure.
    /// </summary>
    /// <remarks>
    /// Rendre une valeur approximative serait pire que ne rien rendre : une
    /// fois sortie d'ici, elle serait indiscernable d'une valeur mesurée.
    /// </remarks>
    public static Energie? Calculer(FenetreDeMesure fenetre)
    {
        ArgumentNullException.ThrowIfNull(fenetre);

        if (fenetre.JoursCouverts <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(fenetre),
                fenetre.JoursCouverts,
                null);
        }

        if (fenetre.JoursDeSaisieComplete < JoursDeSaisieMinimum
            || fenetre.NombreDePesees < PeseesMinimum)
        {
            return null;
        }

        var ecartQuotidien =
            fenetre.VariationDePoidsKg * KilocaloriesParKilogramme / fenetre.JoursCouverts;
        var depense = fenetre.ApportMoyenQuotidien.Kilocalories - ecartQuotidien;

        return depense < 0m ? null : Energie.DepuisKilocalories(depense);
    }
}
