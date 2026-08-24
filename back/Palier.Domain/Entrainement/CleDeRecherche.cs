namespace Palier.Domain.Entrainement;

/// <summary>
/// La normalisation d'un nom d'exercice pour la recherche — D73.
/// </summary>
/// <remarks>
/// <para>
/// <b>UNE connaissance, UN endroit.</b> La même règle vit nécessairement deux
/// fois : dans la colonne générée <c>exercises.search_key</c>, que le moteur
/// calcule, et dans la normalisation du terme tapé par l'utilisateur, que le
/// serveur calcule. Les deux DOIVENT changer ensemble — sinon le jour où l'on
/// ajoute une lettre accentuée d'un côté, la recherche cesse silencieusement de
/// trouver ce qu'elle trouvait la veille.
/// </para>
///
/// <para>
/// Les deux tables de caractères sont donc écrites ici, et l'expression SQL de
/// la colonne est <b>engendrée</b> à partir d'elles. Recopier la liste dans le
/// <c>DbContext</c> aurait donné deux listes à garder identiques à la main.
/// </para>
/// </remarks>
public static class CleDeRecherche
{
    /// <summary>Les lettres accentuées reconnues, EN MAJUSCULES.</summary>
    /// <remarks>
    /// <b>Celles du français et de l'anglais</b>, les deux seules langues du
    /// produit. L'allonger sans nécessité coûterait en lisibilité ce qu'elle ne
    /// rapporterait à personne : aucun nom du catalogue ne porte de caractère
    /// hors de cette liste.
    ///
    /// <para>
    /// <b>Majuscules et non minuscules</b>, et ce n'est pas un détail de goût :
    /// CA1308 refuse <c>ToLowerInvariant</c> pour une normalisation, parce que
    /// la mise en minuscules perd de l'information sur certains alphabets là où
    /// la mise en majuscules n'en perd pas. La colonne du moteur suit — elle
    /// applique <c>upper</c>, pas <c>lower</c> — sans quoi les deux clés ne se
    /// rencontreraient jamais.
    /// </para>
    /// </remarks>
    public const string Accentuees = "ÁÀÂÄÃÅÉÈÊËÍÌÎÏÓÒÔÖÕÚÙÛÜŸÇÑ";

    /// <summary>Leur équivalent sans accent, dans le MÊME ordre.</summary>
    public const string Nues = "AAAAAAEEEEIIIIOOOOOUUUUYCN";

    /// <summary>
    /// L'expression SQL de la colonne générée, construite à partir des tables
    /// ci-dessus.
    /// </summary>
    /// <remarks>
    /// <c>coalesce</c> sur les deux noms : le nom anglais est NUL pour un
    /// exercice personnalisé, et <c>'x' || null</c> vaut <c>null</c> en SQL —
    /// sans lui, la clé de tout exercice personnalisé serait nulle, et aucun ne
    /// se trouverait jamais.
    /// </remarks>
    public static string ExpressionSql =>
        $"translate(upper(coalesce(name_fr, '') || ' ' || coalesce(name_en, '')), "
        + $"'{Accentuees}', '{Nues}')";

    /// <summary>Normalise un terme tapé par l'utilisateur.</summary>
    /// <returns>
    /// Le terme en majuscules et sans accent, ou une chaîne vide s'il n'y avait
    /// rien à chercher.
    /// </returns>
    public static string Normaliser(string? terme)
    {
        if (string.IsNullOrWhiteSpace(terme))
        {
            return string.Empty;
        }

        // `Invariant` et non la culture courante : en turc, la bascule de casse
        // de la lettre `i` ne donne pas ce qu'on attend, et « incline »
        // cesserait de trouver « Incliné » selon la machine qui exécute — un
        // défaut qui ne se reproduirait jamais en développement.
        var majuscules = terme.Trim().ToUpperInvariant();

        var sortie = new System.Text.StringBuilder(majuscules.Length);
        foreach (var signe in majuscules)
        {
            var rang = Accentuees.IndexOf(signe, StringComparison.Ordinal);
            sortie.Append(rang >= 0 ? Nues[rang] : signe);
        }

        return sortie.ToString();
    }

    /// <summary>
    /// Le motif <c>LIKE</c> correspondant à un terme, jokers ÉCHAPPÉS.
    /// </summary>
    /// <returns>
    /// Le motif prêt à être passé en PARAMÈTRE LIÉ, ou une chaîne vide si le
    /// terme ne cherchait rien.
    /// </returns>
    /// <remarks>
    /// <para>
    /// <b>Sans cet échappement, l'utilisateur pilote le motif.</b> Un terme
    /// réduit à <c>%</c> devient <c>%%%</c> et rend le catalogue entier ; un
    /// <c>_</c> remplace n'importe quel caractère ; et une suite de jokers coûte
    /// au moteur bien plus qu'une recherche ordinaire. Ce n'est pas une
    /// injection — le motif reste un paramètre lié — mais c'est la même erreur
    /// de fond : une entrée qui décide de ce que la requête fait.
    /// </para>
    ///
    /// <para>
    /// <b>La contre-oblique est échappée EN PREMIER.</b> L'inverse
    /// double-échapperait les jokers qu'on vient d'introduire, et un terme
    /// contenant <c>\</c> chercherait autre chose que ce qui a été tapé.
    /// </para>
    ///
    /// <para>
    /// La contre-oblique est le caractère d'échappement par DÉFAUT de
    /// <c>LIKE</c> en PostgreSQL — mesuré sur le cluster du projet, pas
    /// supposé : <c>'TAUX 50%' like '%\%%'</c> rend vrai, <c>'SQUAT BARRE'</c>
    /// rend faux.
    /// </para>
    /// </remarks>
    public static string MotifPourLike(string? terme)
    {
        var normalise = Normaliser(terme);
        if (normalise.Length == 0)
        {
            return string.Empty;
        }

        var echappe = normalise
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);

        return "%" + echappe + "%";
    }
}
