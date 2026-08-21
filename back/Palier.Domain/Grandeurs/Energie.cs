namespace Palier.Domain.Grandeurs;

/// <summary>
/// Une quantité d'énergie alimentaire, en kilocalories.
/// </summary>
/// <remarks>
/// Ce type vit dans <c>Grandeurs</c> et non dans <c>Depense</c> : un type et un
/// espace de noms de même nom créent une ambiguïté que le compilateur tranche
/// en faveur de l'espace de noms, et l'erreur ne se lit pas dans le code
/// fautif. Le dossier des calculs de dépense s'appelle donc <c>Depense</c>.
///
/// Zéro est accepté — une journée sans apport enregistré est une donnée, pas
/// une anomalie. Le négatif ne l'est pas : il n'existe aucune énergie
/// alimentaire négative, et l'accepter laisserait un TDEE mal calculé passer
/// pour une mesure.
/// </remarks>
public readonly record struct Energie
{
    private Energie(decimal kilocalories) => Kilocalories = kilocalories;

    public decimal Kilocalories { get; }

    public static Energie DepuisKilocalories(decimal kilocalories) =>
        kilocalories >= 0m
            ? new Energie(kilocalories)
            : throw new ArgumentOutOfRangeException(nameof(kilocalories), kilocalories, null);
}
