namespace Palier.Domain.Nutriments;

/// <summary>
/// Ce que l'autorité sanitaire a réellement établi pour un nutriment.
/// </summary>
/// <remarks>
/// L'EFSA ne produit pas une seule sorte de valeur, et les confondre ferait
/// annoncer un danger là où la science n'en définit aucun. Au 21 août 2026 :
///
/// <list type="bullet">
///   <item>limite haute établie — zinc 25 mg, vitamine B6 12 mg depuis 2023
///   (elle valait 25 mg auparavant), sélénium 255 µg depuis 2023 (300 µg
///   auparavant), vitamine D 100 µg, vitamine A préformée 3 000 µg ER ;</item>
///   <item>niveau sûr d'apport, sans limite haute — fer 40 mg, manganèse 8 mg,
///   DHA supplémenté 1 g depuis décembre 2025 ;</item>
///   <item>aucune valeur dérivable — vitamine C, données insuffisantes ;</item>
///   <item>jamais évalué — la grande majorité des nutriments.</item>
/// </list>
///
/// Ces valeurs ne figurent nulle part dans ce domaine : elles sont citées ici
/// pour expliquer la distinction, et vivent en base, versionnées et datées.
/// </remarks>
public enum StatutReference
{
    /// <summary>
    /// Une <i>tolerable upper intake level</i>. Un dépassement peut être
    /// signalé comme tel.
    /// </summary>
    LimiteHauteEtablie,

    /// <summary>
    /// Un <i>safe level of intake</i>. L'avis qui l'établit précise que « le
    /// niveau où le risque commence à augmenter n'est pas défini » : la valeur
    /// situe l'apport, elle n'autorise pas à parler de dépassement.
    /// </summary>
    NiveauSurDApport,

    /// <summary>
    /// L'autorité a examiné le nutriment et conclu qu'aucune valeur ne pouvait
    /// être dérivée des données disponibles.
    /// </summary>
    NonDerivable,

    /// <summary>Aucun avis n'existe pour ce nutriment.</summary>
    JamaisEvalue,
}
