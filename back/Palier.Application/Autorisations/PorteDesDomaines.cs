namespace Palier.Application.Autorisations;

/// <summary>
/// Ce qui ouvre — ou ferme — la nutrition et l'entraînement.
/// </summary>
/// <remarks>
/// <para>
/// <b>Le produit a deux domaines, et ils ne se ferment pas ensemble.</b>
/// <c>docs/09-comptes.md</c> pose deux règles qu'on confond facilement en une
/// seule :
/// </para>
///
/// <list type="number">
///   <item>
///     § 1 — « <b>pas de nutrition</b> sans email vérifié ». Pas « pas de
///     connexion » : <c>SignInOptions.RequireConfirmedEmail</c> bloquerait trop,
///     et c'est précisément pourquoi cette règle est applicative.
///   </item>
///   <item>
///     § 2 — le consentement santé est <b>refusable</b>, et « en cas de refus :
///     accès à l'<b>entraînement</b>, pas à la nutrition ».
///   </item>
/// </list>
///
/// <para>
/// <b>L'entraînement reste donc ouvert dans tous les cas.</b> Un verrou qui le
/// fermerait aussi serait une régression produit — et le genre de régression
/// qu'on ne remarque pas, parce qu'elle ressemble à de la prudence.
/// </para>
///
/// <para>
/// Ces deux fonctions sont pures et ne lisent aucun drapeau elles-mêmes : ce
/// sont les <i>règles</i>, pas leur source. La vérification d'adresse et le
/// recueil du consentement se posent ailleurs.
/// </para>
/// </remarks>
public static class PorteDesDomaines
{
    /// <summary>
    /// La nutrition exige les <b>deux</b> verrous. Trois des quatre
    /// combinaisons la ferment.
    /// </summary>
    public static bool NutritionOuverte(bool emailVerifie, bool consentementSante) =>
        emailVerifie && consentementSante;

    /// <summary>
    /// L'entraînement reste ouvert, <b>quels que soient</b> les deux drapeaux.
    /// </summary>
    /// <remarks>
    /// Les paramètres sont acceptés et délibérément ignorés. Une signature sans
    /// paramètres dirait « cette règle ne dépend de rien » ; celle-ci dit « elle
    /// a vu les deux drapeaux et a décidé de les ignorer ». La différence
    /// compte le jour où quelqu'un se demandera si l'oubli est un oubli.
    /// </remarks>
#pragma warning disable IDE0060, CA1801 // Ignorés DÉLIBÉRÉMENT — voir ci-dessus.
    public static bool EntrainementOuvert(bool emailVerifie, bool consentementSante) => true;
#pragma warning restore IDE0060, CA1801
}
