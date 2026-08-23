using Palier.Application.Entrainement;
using Palier.Application.Pipeline;
using Palier.Infrastructure.Entrainement;

namespace Palier.Api.Entrainement;

/// <summary>
/// Les points d'entrée des contraintes déclarées.
/// </summary>
/// <remarks>
/// <para>
/// <b><c>PUT</c> et non <c>POST</c> sur la liste.</b> Déclarer ses contraintes
/// est un ÉTAT, pas un événement : renvoyer deux fois la même liste doit
/// produire le même résultat. Un <c>POST</c> aurait laissé croire à un ajout,
/// et l'utilisateur qui retire une contrainte guérie n'aurait eu aucun geste
/// évident à faire.
/// </para>
///
/// <para>
/// <b>Ces données relèvent de l'article 9.</b> Une contrainte cervicale ou
/// lombaire est une information de santé — c'est pour cela que la politique RLS
/// de cette table appelle l'accesseur qui LÈVE, sans branche publique, et que
/// rien de tout cela ne part au journal.
/// </para>
/// </remarks>
internal static class ContraintesDuCompte
{
    public static void Router(RouteGroupBuilder groupe)
    {
        ArgumentNullException.ThrowIfNull(groupe);

        groupe.MapGet("/contraintes", ListerAsync);
        groupe.MapPut("/contraintes", RemplacerAsync);
        groupe.MapDelete("/contraintes/{region}", RetirerAsync);
    }

    public static async Task<IResult> ListerAsync(
        IExecuteurDeCasDUsage executeur,
        ListerLesContraintes gestionnaire,
        CancellationToken jeton
    )
    {
        ArgumentNullException.ThrowIfNull(executeur);
        ArgumentNullException.ThrowIfNull(gestionnaire);

        var contraintes = await executeur
            .ExecuterAsync(nameof(ListerLesContraintes), gestionnaire.ExecuterAsync, jeton)
            .ConfigureAwait(false);

        // 200 avec une liste vide : « je n'ai aucune contrainte » est la
        // réponse la plus fréquente, et ce n'est pas une absence de données.
        return Results.Ok(contraintes);
    }

    public static async Task<IResult> RemplacerAsync(
        DeclarationDeContraintes demande,
        IExecuteurDeCasDUsage executeur,
        RemplacerLesContraintes gestionnaire,
        TimeProvider horloge,
        CancellationToken jeton
    )
    {
        ArgumentNullException.ThrowIfNull(executeur);
        ArgumentNullException.ThrowIfNull(gestionnaire);
        ArgumentNullException.ThrowIfNull(demande);

        if (demande.Faute is { } faute)
        {
            return Results.Json(new Reponse(faute), statusCode: StatusCodes.Status400BadRequest);
        }

        var contraintes = await executeur
            .ExecuterAsync(
                nameof(RemplacerLesContraintes),
                j => gestionnaire.ExecuterAsync(demande, horloge, j),
                jeton
            )
            .ConfigureAwait(false);

        return Results.Ok(contraintes);
    }

    public static async Task<IResult> RetirerAsync(
        string region,
        IExecuteurDeCasDUsage executeur,
        RetirerUneContrainte gestionnaire,
        CancellationToken jeton
    )
    {
        ArgumentNullException.ThrowIfNull(executeur);
        ArgumentNullException.ThrowIfNull(gestionnaire);

        // La région vient de l'URL, donc de l'extérieur : elle se valide comme
        // n'importe quelle entrée. Sans ce contrôle, `Contraintes.EnBase`
        // lèverait plus bas — un 500 pour une URL mal tapée.
        if (!Contraintes.Lire(region, out var lue))
        {
            return Results.Json(
                new Reponse("ContrainteInvalide"),
                statusCode: StatusCodes.Status400BadRequest
            );
        }

        var retiree = await executeur
            .ExecuterAsync(
                nameof(RetirerUneContrainte),
                j => gestionnaire.ExecuterAsync(lue.Value, j),
                jeton
            )
            .ConfigureAwait(false);

        // 404 quand elle n'était pas déclarée. Rendre 204 dans les deux cas
        // serait plus « idempotent » sur le papier, mais empêcherait le client
        // de distinguer « c'est fait » de « il n'y avait rien » — et cette
        // route agit sur une donnée de santé, où le silence est le mauvais
        // choix par défaut.
        return retiree
            ? Results.NoContent()
            : Results.Json(
                new Reponse("ContrainteNonDeclaree"),
                statusCode: StatusCodes.Status404NotFound
            );
    }
}
