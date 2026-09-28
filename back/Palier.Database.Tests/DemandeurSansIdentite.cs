using Palier.Application.Pipeline;

namespace Palier.Database.Tests;

/// <summary>
/// Un demandeur qui ne rend JAMAIS d'identité — le visiteur anonyme, vu du
/// pipeline.
/// </summary>
/// <remarks>
/// <para>
/// Il a vécu dans <c>Palier.Infrastructure</c> pendant les lots 2 et 3, où il
/// était le service réellement enregistré : aucune authentification n'existait,
/// et tout cas d'usage devait échouer bruyamment. Le lot 4 l'a remplacé par
/// <c>IdentiteDepuisJeton</c>.
/// </para>
///
/// <para>
/// <b>Il est donc descendu ici.</b> Un type public dont le seul appelant est
/// une épreuve n'est plus du code de production : c'est un double de test resté
/// en amont, que rien ne détecte — <c>Palier.Infrastructure</c> n'a pas de
/// seuil de couverture, et Roslyn ne peut pas signaler un membre public inutilisé.
/// </para>
/// </remarks>
internal sealed class DemandeurSansIdentite : IIdentiteDemandeur
{
    public Guid? Identifiant => null;
}
