namespace Palier.Domain.Grandeurs;

/// <summary>
/// D'où vient une mesure de composition corporelle.
/// </summary>
/// <remarks>
/// Cette distinction commande le choix de la formule de métabolisme de base.
/// Katch-McArdle est plus exacte que Mifflin-St Jeor chez le sportif, mais
/// seulement si le pourcentage de masse grasse l'est : les balances à
/// impédance domestiques mesurent en moyenne 4,4 points sous la DXA, et
/// l'erreur se propage à environ 16 kcal par point. Une formule exacte nourrie
/// d'une donnée fausse est moins fiable qu'une formule biaisée nourrie d'une
/// donnée juste — d'autant que le biais de Mifflin est systématique, donc
/// documentable, quand celui d'une balance est aléatoire et invisible.
/// </remarks>
public enum ProvenanceMesure
{
    /// <summary>DXA, pesée hydrostatique, plis cutanés par un professionnel.</summary>
    MesureFiable,

    /// <summary>Balance ou appareil à impédancemétrie.</summary>
    Impedancemetrie,

    /// <summary>Valeur saisie par l'utilisateur, sans mesure.</summary>
    Declaratif,
}
