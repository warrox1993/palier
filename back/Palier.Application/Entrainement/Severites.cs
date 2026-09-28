using System.Diagnostics.CodeAnalysis;

namespace Palier.Application.Entrainement;

/// <summary>
/// La gravité d'une contrainte déclarée — <c>docs/03-donnees.md</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>C'est LE réglage de l'utilisateur pour l'adaptation par contrainte</b>, et
/// il est <b>par contrainte</b>, pas global. Une épaule strictement
/// contre-indiquée et un genou légèrement sensible n'appellent pas le même
/// traitement : un réglage global aurait forcé un seul comportement pour les
/// deux, et l'utilisateur aurait choisi le pire des deux compromis.
/// </para>
///
/// <para>
/// <b>Elle ne filtre RIEN.</b> Elle voyage avec le marquage du catalogue, et
/// c'est l'écran qui en déduit la présentation. La distinction est celle que
/// <c>docs/00-produit.md</c> pose : « voici ta valeur, voici la référence,
/// voici l'écart » est une information ; décider à la place de l'utilisateur
/// place l'éditeur en conseiller, ce qui est réglementé.
/// </para>
/// </remarks>
public enum Severite
{
    /// <summary>Sensible. L'exercice se signale, rien ne se replie.</summary>
    Leger,

    /// <summary>La valeur par défaut, quand l'utilisateur ne se prononce pas.</summary>
    Modere,

    /// <summary>Contre-indication ferme. L'écran replie par défaut, sans jamais masquer.</summary>
    Strict,
}

/// <summary>La traduction entre l'énumération et ce qui vit en base.</summary>
public static class Severites
{
    /// <summary>
    /// Ce qu'on retient quand l'utilisateur ne se prononce pas.
    /// </summary>
    /// <remarks>
    /// <b><c>Modere</c>, et non <c>Leger</c>.</b> Le défaut d'un produit dont la
    /// promesse est d'éviter les blessures ne peut pas être le moins protecteur
    /// des trois — quelqu'un qui prend la peine de déclarer une contrainte
    /// signale déjà qu'elle compte. Et ce n'est pas non plus <c>Strict</c>, qui
    /// replierait des exercices pour une gêne passagère.
    /// </remarks>
    public const Severite ParDefaut = Severite.Modere;

    private static readonly IReadOnlyDictionary<Severite, string> _versLaBase = new Dictionary<
        Severite,
        string
    >
    {
        [Severite.Leger] = "leger",
        [Severite.Modere] = "modere",
        [Severite.Strict] = "strict",
    };

    /// <summary>La forme stockée d'une sévérité.</summary>
    public static string EnBase(Severite severite) =>
        _versLaBase.TryGetValue(severite, out var valeur)
            ? valeur
            : throw new ArgumentOutOfRangeException(nameof(severite), severite, null);

    /// <summary>
    /// Lit une sévérité venue de l'extérieur. Une valeur ABSENTE prend le
    /// défaut ; une valeur INCONNUE échoue.
    /// </summary>
    /// <remarks>
    /// La distinction compte : ne rien dire est un choix légitime — le formulaire
    /// peut ne pas poser la question — alors que dire « urgent » est une erreur
    /// qu'il faut signaler plutôt que d'interpréter.
    /// </remarks>
    public static bool Lire(string? valeur, [NotNullWhen(true)] out Severite? severite)
    {
        if (string.IsNullOrWhiteSpace(valeur))
        {
            severite = ParDefaut;
            return true;
        }

        foreach (var paire in _versLaBase)
        {
            if (string.Equals(paire.Value, valeur, StringComparison.OrdinalIgnoreCase))
            {
                severite = paire.Key;
                return true;
            }
        }

        severite = null;
        return false;
    }

    /// <summary>Les trois valeurs, dans leur forme stockée.</summary>
    public static IReadOnlyCollection<string> Toutes =>
        (IReadOnlyCollection<string>)_versLaBase.Values;
}
