using Microsoft.AspNetCore.Identity;

namespace Palier.Infrastructure.Identite;

/// <summary>
/// L'utilisateur d'ASP.NET Identity, dont la clé est un <c>uuid</c> et non le
/// <c>text</c> par défaut — D35.
///
/// Trois raisons cumulées. Le schéma entier de `docs/03-donnees.md` est en
/// `uuid` avec `gen_random_uuid()`. La spec d'architecture § 8 prévoit des
/// identifiants UUID v7 générés côté client, pour l'idempotence de la file de
/// retry. Et un `owner_id` en `text` alourdirait tous les index de jointure du
/// produit, à commencer par `workouts (owner_id, started_at desc)`.
///
/// Les 14 clauses `references auth.users` de `docs/03-donnees.md` deviennent
/// `references "AspNetUsers"("Id")`, en `uuid`.
///
/// AUCUNE LIGNE DE CODE D'AUTHENTIFICATION N'EST ÉCRITE À CE LOT. Les tables
/// sont créées, c'est tout ; le lot 4 fait le reste. Créer les tables
/// maintenant évite une reprise de schéma portant sur quatorze clés
/// étrangères, que `CLAUDE.md` § 6 interdit après la première mise en
/// production.
///
/// CE QUI ROUVRE D35 : rien. Après la première migration appliquée en
/// production, un changement de type de clé primaire n'est plus une décision,
/// c'est une migration de données.
/// </summary>
public sealed class Utilisateur : IdentityUser<Guid>
{
    /// <summary>
    /// Quand le dernier échec de connexion a eu lieu.
    /// </summary>
    /// <remarks>
    /// C'est ce qui donne sa <b>fenêtre</b> au compteur d'échecs. Identity n'a
    /// pas cette date : son <c>AccessFailedCount</c> est cumulatif et n'est
    /// remis à zéro que par une connexion réussie — quatre fautes de frappe
    /// étalées sur trois mois plus une aujourd'hui verrouillent le compte.
    /// </remarks>
    public DateTimeOffset? DernierEchecLe { get; set; }

    /// <summary>
    /// Le nombre de verrouillages déjà infligés — l'ESCALADE.
    /// </summary>
    /// <remarks>
    /// Identity n'a qu'une durée unique, <c>DefaultLockoutTimeSpan</c>. Elle
    /// laisse un attaquant patient reprendre cinq essais toutes les cinq
    /// minutes, indéfiniment. Ce compteur fait grandir la peine à chaque
    /// récidive — voir <c>DecisionDeVerrouillage</c>, qui décide, pure.
    /// </remarks>
    public int VerrouillagesSubis { get; set; }

    /// <summary>
    /// Quand le consentement au traitement des données de santé a été donné, ou
    /// <c>null</c> s'il ne l'a pas été.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Une DATE et non un booléen : l'article 7 § 1 du RGPD impose de pouvoir
    /// démontrer que le consentement a été donné, et « oui » sans « quand » ne
    /// démontre rien. C'est aussi ce que le registre de l'article 30 attend.
    /// </para>
    ///
    /// <para>
    /// <b>Ce n'est pas une donnée de santé</b>, c'est une donnée SUR un
    /// consentement — elle peut donc être journalisée, contrairement à ce
    /// qu'elle autorise.
    /// </para>
    ///
    /// <para>
    /// Le consentement est REFUSABLE : `docs/09-comptes.md` § 2 — « en cas de
    /// refus : accès à l'entraînement, pas à la nutrition ». Ce champ nul est
    /// donc un état normal du produit, pas un compte incomplet.
    /// </para>
    /// </remarks>
    public DateTimeOffset? ConsentementSanteLe { get; set; }
}
