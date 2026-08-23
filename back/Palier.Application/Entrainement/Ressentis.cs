using System.Diagnostics.CodeAnalysis;

namespace Palier.Application.Entrainement;

/// <summary>
/// Ce qu'un exercice a donné, du point de vue du pratiquant —
/// <c>docs/05-entrainement.md</c> § 5.
/// </summary>
/// <remarks>
/// <para>
/// <b>Trois états, et le document les nomme :</b> <c>good</c>, <c>meh</c>,
/// <c>pain</c>. Ce n'est pas une échelle, et il ne faut surtout pas la traiter
/// comme telle : « douleur » n'est pas « très mauvais », c'est un signal d'une
/// autre nature. Un entier de 1 à 3 aurait invité à faire des moyennes.
/// </para>
///
/// <para>
/// <b>Ce que ces états déclenchent</b> — et que le lot 5 ne fait PAS encore :
/// un <c>pain</c> sur les trois dernières séances rappelle les leviers ; deux
/// <c>pain</c> consécutifs proposent un retrait et <b>orientent vers un
/// professionnel</b> ; trois <c>meh</c> consécutifs proposent une variante. Le
/// lot 5 enregistre et expose ; la règle des seuils arrive avec l'écran qui la
/// montre.
/// </para>
/// </remarks>
public enum Ressenti
{
    /// <summary>L'exercice s'est bien passé.</summary>
    Bon,

    /// <summary>Sans plus. Trois de suite valent une proposition de variante.</summary>
    Moyen,

    /// <summary>Douleur. Le seul des trois qui puisse mener à une orientation médicale.</summary>
    Douleur,
}

/// <summary>La traduction entre l'énumération et ce qui vit en base.</summary>
/// <remarks>
/// <b>La forme stockée est celle du document</b> — <c>good</c>, <c>meh</c>,
/// <c>pain</c> — et non une traduction française. Le document métier, la
/// contrainte <c>CHECK</c> et cette table s'accordent donc mot pour mot, ce qui
/// rend la vérification possible à l'œil nu. Le libellé que l'utilisateur voit
/// vit dans i18next, où il se traduit.
/// </remarks>
public static class Ressentis
{
    private static readonly IReadOnlyDictionary<Ressenti, string> _versLaBase = new Dictionary<
        Ressenti,
        string
    >
    {
        [Ressenti.Bon] = "good",
        [Ressenti.Moyen] = "meh",
        [Ressenti.Douleur] = "pain",
    };

    /// <summary>La forme stockée d'un ressenti.</summary>
    public static string EnBase(Ressenti ressenti) =>
        _versLaBase.TryGetValue(ressenti, out var valeur)
            ? valeur
            : throw new ArgumentOutOfRangeException(nameof(ressenti), ressenti, null);

    /// <summary>Lit un ressenti venu de l'extérieur, sans lever.</summary>
    public static bool Lire(string? valeur, [NotNullWhen(true)] out Ressenti? ressenti)
    {
        foreach (var paire in _versLaBase)
        {
            if (string.Equals(paire.Value, valeur, StringComparison.OrdinalIgnoreCase))
            {
                ressenti = paire.Key;
                return true;
            }
        }

        ressenti = null;
        return false;
    }

    /// <summary>Les trois valeurs, dans leur forme stockée.</summary>
    /// <remarks>
    /// Elles doivent correspondre EXACTEMENT à la contrainte
    /// <c>ck_exercise_feedback_feeling</c> de la migration. Une épreuve
    /// d'intégration provoque le désaccord en tentant d'insérer chacune : si
    /// l'une était refusée par le moteur, elle rougirait.
    /// </remarks>
    public static IReadOnlyCollection<string> Tous => (IReadOnlyCollection<string>)_versLaBase.Values;
}
