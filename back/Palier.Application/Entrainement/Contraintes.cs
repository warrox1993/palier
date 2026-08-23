using System.Diagnostics.CodeAnalysis;

namespace Palier.Application.Entrainement;

/// <summary>
/// Les régions que l'utilisateur peut déclarer contraintes, et sur lesquelles
/// un exercice peut être contre-indiqué.
/// </summary>
/// <remarks>
/// <para>
/// <b>La liste est FERMÉE, et elle n'est pas inventée ici :</b>
/// <c>docs/05-entrainement.md</c> § 4 nomme exactement ces quatre régions —
/// cervicale, lombaire, épaule, genou — chacune avec ses exclusions et ses
/// priorités. Il n'y avait donc rien à trancher.
/// </para>
///
/// <para>
/// <b>Le texte libre aurait été un piège.</b> Il ne se traduit pas, il ne se
/// compare pas — « épaule », « epaule », « Épaule droite » sont trois valeurs
/// distinctes pour une machine — et il finirait dans un prompt de modèle, ce
/// que <c>docs/01-conformite.md</c> encadre strictement. Une valeur
/// d'énumération se compare, se traduit, et alimente le filtrage par
/// intersection avec <c>exercises.contraindicated_for</c>.
/// </para>
///
/// <para>
/// <b>Ce qui la rouvrirait :</b> une cinquième région réclamée par un
/// utilisateur — poignet, cheville, hanche. Elle s'ajoute alors ICI, dans les
/// libellés, et dans les exercices du catalogue qui la portent. Pas par le
/// corps d'une requête.
/// </para>
/// </remarks>
public enum Contrainte
{
    /// <summary>Pas de charge axiale, rien au-dessus de la tête, pas de shrugs.</summary>
    Cervicale,

    /// <summary>Pas de soulevé de terre lourd, de squat barre ni de good morning chargé.</summary>
    Lombaire,

    /// <summary>Pas de développé militaire, de dips lestés ni d'écarté en amplitude maximale.</summary>
    Epaule,

    /// <summary>Pas de fentes profondes ni d'extensions lourdes en fin d'amplitude.</summary>
    Genou,
}

/// <summary>
/// La traduction entre l'énumération et ce qui vit en base.
/// </summary>
/// <remarks>
/// <b>La forme stockée est en minuscules SANS ACCENT</b> — <c>epaule</c> et non
/// <c>épaule</c>. Le tableau <c>text[]</c> de PostgreSQL se compare octet pour
/// octet, et une collation qui traiterait « e » et « é » différemment selon
/// l'environnement rendrait l'intersection dépendante de la configuration du
/// serveur. Le libellé accentué que l'utilisateur voit vit dans i18next, où il
/// est à sa place.
/// </remarks>
public static class Contraintes
{
    private static readonly IReadOnlyDictionary<Contrainte, string> _versLaBase = new Dictionary<
        Contrainte,
        string
    >
    {
        [Contrainte.Cervicale] = "cervicale",
        [Contrainte.Lombaire] = "lombaire",
        [Contrainte.Epaule] = "epaule",
        [Contrainte.Genou] = "genou",
    };

    /// <summary>La forme stockée d'une contrainte.</summary>
    public static string EnBase(Contrainte contrainte) =>
        _versLaBase.TryGetValue(contrainte, out var valeur)
            ? valeur
            // Une valeur d'énumération hors du dictionnaire ne peut venir que
            // d'un `(Contrainte)42` forcé, ou d'un membre ajouté ici sans être
            // ajouté au dictionnaire. Lever plutôt que rendre une chaîne vide :
            // une chaîne vide entrerait en base et y resterait.
            : throw new ArgumentOutOfRangeException(nameof(contrainte), contrainte, null);

    /// <summary>
    /// Lit une contrainte venue de l'extérieur. Rend faux sur tout ce qui n'est
    /// pas exactement l'une des quatre valeurs.
    /// </summary>
    /// <remarks>
    /// <c>OrdinalIgnoreCase</c> et non une comparaison de culture : la casse
    /// est un confort de saisie, mais une comparaison culturelle ferait
    /// dépendre le résultat de la locale du serveur — le fameux problème du
    /// « i » turc, où <c>"I".ToLower()</c> ne rend pas <c>"i"</c>.
    /// </remarks>
    public static bool Lire(string? valeur, [NotNullWhen(true)] out Contrainte? contrainte)
    {
        foreach (var paire in _versLaBase)
        {
            if (string.Equals(paire.Value, valeur, StringComparison.OrdinalIgnoreCase))
            {
                contrainte = paire.Key;
                return true;
            }
        }

        contrainte = null;
        return false;
    }

    /// <summary>Les quatre valeurs, dans leur forme stockée.</summary>
    public static IReadOnlyCollection<string> Toutes => (IReadOnlyCollection<string>)_versLaBase.Values;
}
