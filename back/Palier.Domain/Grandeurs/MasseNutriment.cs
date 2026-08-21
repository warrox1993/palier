namespace Palier.Domain.Grandeurs;

/// <summary>
/// Une masse de nutriment, indissociable de son unité.
/// </summary>
/// <remarks>
/// L'addition convertit vers l'unité <b>la plus fine</b> des deux opérandes,
/// et jamais l'inverse : convertir vers la plus grossière perdrait les
/// décimales d'un apport en microgrammes, et la perte serait silencieuse.
/// </remarks>
public readonly record struct MasseNutriment
{
    private MasseNutriment(decimal valeur, UniteNutriment unite)
    {
        Valeur = valeur;
        Unite = unite;
    }

    public decimal Valeur { get; }

    public UniteNutriment Unite { get; }

    public static MasseNutriment De(decimal valeur, UniteNutriment unite)
    {
        if (valeur < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(valeur), valeur, null);
        }

        if (!Enum.IsDefined(unite))
        {
            throw new ArgumentOutOfRangeException(nameof(unite), unite, null);
        }

        return new MasseNutriment(valeur, unite);
    }

    public MasseNutriment ConvertieEn(UniteNutriment cible)
    {
        if (!Enum.IsDefined(cible))
        {
            throw new ArgumentOutOfRangeException(nameof(cible), cible, null);
        }

        return new MasseNutriment(Valeur * FacteurVersMicrogrammes(Unite) / FacteurVersMicrogrammes(cible), cible);
    }

    /// <summary>
    /// La somme de deux masses, dans l'unité la plus fine des deux.
    /// </summary>
    /// <remarks>
    /// Une surcharge de l'opérateur <c>+</c> aurait été plus lisible à
    /// l'appel, mais CA2225 exige alors une méthode nommée <c>Add</c> — en
    /// anglais, dans un domaine écrit en français. Une méthode seule évite
    /// d'avoir deux portes pour la même pièce, dont l'une dans la mauvaise
    /// langue.
    /// </remarks>
    public static MasseNutriment Somme(MasseNutriment a, MasseNutriment b)
    {
        var cible = LaPlusFine(a.Unite, b.Unite);
        return new MasseNutriment(a.ConvertieEn(cible).Valeur + b.ConvertieEn(cible).Valeur, cible);
    }

    /// <summary>
    /// Combien de microgrammes vaut une unité. Le microgramme est le pivot :
    /// toutes les conversions y passent, ce qui évite d'écrire six formules
    /// pour trois unités et de se tromper dans l'une d'elles.
    /// </summary>
    private static decimal FacteurVersMicrogrammes(UniteNutriment unite) =>
        unite switch
        {
            UniteNutriment.Gramme => 1_000_000m,
            UniteNutriment.Milligramme => 1_000m,
            _ => 1m,
        };

    private static UniteNutriment LaPlusFine(UniteNutriment a, UniteNutriment b) =>
        FacteurVersMicrogrammes(a) <= FacteurVersMicrogrammes(b) ? a : b;
}
