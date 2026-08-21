using Palier.Domain.Grandeurs;

namespace Palier.Domain.Objectifs;

/// <summary>
/// L'apport calorique visé, et l'indication que le plancher de sécurité a mordu.
/// </summary>
/// <remarks>
/// <see cref="PlancherAtteint" /> est un booléen, pas une phrase : l'interface
/// dira ce qu'il faut dire, dans la langue de l'utilisateur, à partir d'un
/// libellé versionné en base. Le domaine constate, il ne rédige pas.
/// </remarks>
public sealed record ApportCible(Energie Valeur, bool PlancherAtteint);

/// <summary>
/// Le calcul de l'apport visé à partir de la maintenance.
/// </summary>
public static class CalculApportCible
{
    /// <summary>Déficit maximal autorisé, en fraction de la maintenance.</summary>
    /// <remarks>
    /// <c>docs/04-nutrition.md</c> § 2 : « −20 % à +15 % de la maintenance,
    /// jamais au-delà ». Le document ajoute qu'un objectif de perte de poids
    /// n'est <b>jamais</b> pré-rempli : le défaut est la maintenance, et c'est
    /// la valeur par défaut du paramètre d'ajustement.
    /// </remarks>
    public static decimal DeficitMaximal => -0.20m;

    public static decimal SurplusMaximal => 0.15m;

    /// <summary>
    /// L'apport visé, plancher appliqué <b>en dernier</b>.
    /// </summary>
    /// <remarks>
    /// L'ordre compte : appliquer le plancher avant l'ajustement laisserait le
    /// déficit repasser dessous. Il est donc la dernière opération, et rien ne
    /// s'exécute après lui.
    /// </remarks>
    public static ApportCible Calculer(Energie maintenance, Sexe sexe, decimal ajustement = 0m)
    {
        if (ajustement < DeficitMaximal || ajustement > SurplusMaximal)
        {
            throw new ArgumentOutOfRangeException(nameof(ajustement), ajustement, null);
        }

        var vise = maintenance.Kilocalories * (1m + ajustement);
        var plancher = PlancherCalorique.Pour(sexe).Kilocalories;

        return vise < plancher
            ? new ApportCible(Energie.DepuisKilocalories(plancher), PlancherAtteint: true)
            : new ApportCible(Energie.DepuisKilocalories(vise), PlancherAtteint: false);
    }
}
