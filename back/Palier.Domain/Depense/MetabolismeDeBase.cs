using Palier.Domain.Grandeurs;

namespace Palier.Domain.Depense;

/// <summary>
/// Métabolisme de base. Voir <c>docs/04-nutrition.md</c> § 1.
/// Module pur : aucun accès réseau, base ou interface.
/// </summary>
public static class MetabolismeDeBase
{
    /// <summary>
    /// Mifflin-St Jeor, forme canonique de l'article de 1990.
    /// </summary>
    /// <remarks>
    /// La méta-analyse de référence sur les sportifs (Sports Medicine, 2023)
    /// la déconseille : elle sous-estime significativement, quand cinq autres
    /// équations ne diffèrent pas de la mesure. Elle est pourtant conservée,
    /// et la spec du lot 3 § 3 en donne les motifs — Ten-Haaf, la meilleure
    /// candidate, n'est validée que de 18 à 35 ans quand la population du
    /// produit ne s'arrête pas là ; et l'estimation ne vit que trois semaines
    /// avant que le TDEE mesuré la remplace.
    /// </remarks>
    public static Energie MifflinStJeor(Sexe sexe, Masse masse, Taille taille, Age age)
    {
        var socle =
            (10m * masse.Kilogrammes) + (6.25m * taille.Centimetres) - (5m * age.Annees);

        return sexe switch
        {
            Sexe.Homme => Energie.DepuisKilocalories(socle + 5m),
            Sexe.Femme => Energie.DepuisKilocalories(socle - 161m),
            _ => throw new ArgumentOutOfRangeException(nameof(sexe), sexe, null),
        };
    }

    /// <summary>
    /// Katch-McArdle, à partir de la seule masse maigre.
    /// </summary>
    /// <remarks>
    /// C'est la révision 1991 de l'équation de Cunningham. Sa version de 1980,
    /// <c>500 + 22 × MM</c>, est une autre équation : les confondre déplace le
    /// résultat d'environ 150 kcal sur une masse maigre de 60 kg.
    ///
    /// Le sexe n'entre pas dans l'équation : la composition corporelle le
    /// contient déjà.
    /// </remarks>
    public static Energie KatchMcArdle(Masse masseMaigre) =>
        Energie.DepuisKilocalories(370m + (21.6m * masseMaigre.Kilogrammes));

    /// <summary>
    /// Choisit la formule selon la donnée disponible <b>et sa provenance</b>.
    /// </summary>
    /// <remarks>
    /// Katch-McArdle n'est retenue que sur une <see
    /// cref="ProvenanceMesure.MesureFiable" />. Une balance à impédance
    /// mesure en moyenne 4,4 points sous la DXA, et l'erreur se propage à
    /// environ 16 kcal par point : la formule la plus exacte devient alors la
    /// moins fiable des deux, parce qu'elle amplifie une donnée fausse là où
    /// Mifflin-St Jeor porte un biais systématique, donc documentable.
    /// </remarks>
    public static Energie Estimer(
        Sexe sexe,
        Masse masse,
        Taille taille,
        Age age,
        PourcentageMasseGrasse? masseGrasse) =>
        masseGrasse is { Provenance: ProvenanceMesure.MesureFiable } fiable
            ? KatchMcArdle(fiable.MasseMaigreDe(masse))
            : MifflinStJeor(sexe, masse, taille, age);
}
