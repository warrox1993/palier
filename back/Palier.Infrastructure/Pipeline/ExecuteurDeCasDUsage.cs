using Microsoft.EntityFrameworkCore;
using Palier.Application.Pipeline;

namespace Palier.Infrastructure.Pipeline;

/// <summary>
/// LE SEUL ENDROIT DU CODE OÙ L'IDENTITÉ EST POSÉE — D36.
///
/// Trois choses, dans cet ordre, et aucune n'est optionnelle :
///
/// 1. refuser AVANT d'ouvrir la transaction quand l'identité manque ;
/// 2. ouvrir la transaction, puis
///    <c>select set_config('app.utilisateur', $1, true)</c>, identifiant en
///    PARAMÈTRE LIÉ ;
/// 3. le tout dans <c>CreateExecutionStrategy().ExecuteAsync(...)</c>.
///
/// `ArchitectureTests`, dans `Palier.Database.Tests`, refuse que tout autre type
/// prenne <c>PalierDbContext</c> en dépendance. C'est la différence entre « le
/// pipeline le fait » et « rien d'autre ne peut le faire ».
/// </summary>
[GestionnaireDeCasDUsage]
public sealed class ExecuteurDeCasDUsage(PalierDbContext contexte, IIdentiteDemandeur demandeur)
    : IExecuteurDeCasDUsage
{
    public async Task<T> ExecuterAsync<T>(
        string nomDuCasDUsage,
        Func<CancellationToken, Task<T>> corps,
        CancellationToken jeton = default
    )
    {
        ArgumentNullException.ThrowIfNull(corps);

        // (1) LE REFUS VIENT EN PREMIER, avant le moindre aller-retour.
        //
        // Sur une table VIDE, l'accesseur SQL n'est jamais appelé — RLS évalue
        // ses politiques « for each row ». Sans cette garde, « identité
        // absente » et « cet utilisateur n'a pas de données » seraient
        // indiscernables les premiers jours de production, précisément quand
        // toutes les tables sont vides.
        //
        // On cesse donc d'annoncer que l'échec crie inconditionnellement. La
        // fonction SQL est le FILET, jamais le garde unique.
        var identifiant =
            demandeur.Identifiant ?? throw new IdentiteAbsenteException(nomDuCasDUsage);

        // (3) La stratégie d'exécution ENVELOPPE la transaction, elle ne
        // l'accompagne pas. Microsoft Learn, _Connection Resiliency_ : « if
        // your code initiates a transaction using BeginTransactionAsync() …
        // You will receive an exception … does not support user-initiated
        // transactions. Use the execution strategy returned by
        // DbContext.Database.CreateExecutionStrategy(). »
        //
        // Ce n'est pas une précaution : le jour où quelqu'un activera
        // `EnableRetryOnFailure` sur une base managée qui clignote, TOUTES les
        // requêtes lèveraient d'un coup. Une épreuve INVERSÉE le garde —
        // `EnableRetryOnFailure` actif, et tout reste vert.
        // `ConfigureAwait(false)` partout : ce projet est une BIBLIOTHÈQUE,
        // consommée par un hôte dont on ne connaît pas le contexte de
        // synchronisation. C'est le cas que CA2007 vise vraiment, et la règle
        // reste active ici — elle n'est éteinte que sur les projets `*.Tests`.
        var strategie = contexte.Database.CreateExecutionStrategy();
        return await strategie
            .ExecuteAsync(
                jeton,
                async (token) =>
                {
                    var transaction = await contexte
                        .Database.BeginTransactionAsync(token)
                        .ConfigureAwait(false);
                    // Le second `ConfigureAwait` porte sur la LIBÉRATION, que
                    // `await using` attend implicitement. Sans lui, la
                    // disposition reprendrait sur le contexte de l'appelant.
                    await using var _ = transaction.ConfigureAwait(false);

                    // (2) L'identité, en paramètre LIÉ. `ExecuteSqlAsync` reçoit une
                    // FormattableString et paramètre chaque trou : rien n'est
                    // concaténé dans du SQL.
                    //
                    // `.ToString()` est OBLIGATOIRE : `set_config` attend un `text`
                    // en deuxième argument, et un `Guid` passé tel quel part en
                    // `uuid` — le moteur ne trouve alors pas la fonction.
                    //
                    // LE TROISIÈME ARGUMENT EST `true`, ET C'EST LÀ QUE TIENT TOUT.
                    // « If is_local is true, the new value will only apply during
                    // the current transaction. » À `false`, la valeur passe en
                    // portée SESSION, survit à la connexion rendue au pool, et
                    // l'utilisateur suivant hérite de l'identité du précédent —
                    // sans erreur, sans journal. L'épreuve
                    // `La_portee_transaction_ne_survit_pas_au_commit_sur_la_meme_connexion`
                    // est la SEULE du dépôt qui distingue les deux.
                    var pose = identifiant.ToString();
                    await contexte
                        .Database.ExecuteSqlAsync(
                            $"select set_config('app.utilisateur', {pose}, true)",
                            token
                        )
                        .ConfigureAwait(false);

                    var resultat = await corps(token).ConfigureAwait(false);
                    await transaction.CommitAsync(token).ConfigureAwait(false);
                    return resultat;
                }
            )
            .ConfigureAwait(false);
    }
}

/// <summary>
/// Le demandeur tant qu'aucune authentification n'existe : il ne rend JAMAIS
/// d'identité.
///
/// Ce n'est pas un bouchon complaisant, c'est l'état réel du produit à ce lot —
/// et il fait échouer tout cas d'usage, bruyamment, en nommant l'appel. Le lot 4
/// le remplace par un demandeur qui lit la revendication du jeton. Aucune ligne
/// de code d'authentification n'est écrite ici, et cette classe n'en est pas
/// une : elle constate une absence.
/// </summary>
public sealed class DemandeurSansIdentite : IIdentiteDemandeur
{
    public Guid? Identifiant => null;
}
