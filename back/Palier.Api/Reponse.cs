namespace Palier.Api;

/// <summary>
/// Le corps d'un refus de l'API : un CODE, jamais une phrase.
/// </summary>
/// <remarks>
/// <para>
/// Les libellés vivent en base et passent par i18next — <c>CLAUDE.md</c> § 4,
/// « aucune chaîne de caractères en dur ». Et un code se compare octet pour
/// octet dans une épreuve, là où une phrase invite à des nuances qui finissent
/// par distinguer deux causes d'échec.
/// </para>
///
/// <para>
/// <b>Il vit au namespace PARENT, et c'est délibéré.</b> Il a été écrit au lot
/// 4 dans <c>Palier.Api.Auth</c>, là où il servait seul ; le lot 5 en a le même
/// besoin. Le recopier créerait deux formes de refus qui divergeraient au
/// premier champ ajouté — et le front en lirait une des deux. C'est le critère
/// de <c>CLAUDE.md</c> § 4 : si la forme d'un refus change, les deux endroits
/// doivent changer ENSEMBLE, donc il n'y en a qu'un.
/// </para>
///
/// <para>
/// Le namespace parent le rend visible sans <c>using</c> depuis
/// <c>Palier.Api.Auth</c> comme depuis <c>Palier.Api.Entrainement</c>, et sans
/// que l'entraînement ait à dépendre de l'authentification.
/// </para>
/// </remarks>
internal sealed record Reponse(string Code);
