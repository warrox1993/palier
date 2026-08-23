using Palier.Application.Entrainement;
using Palier.Application.Pipeline;
using Palier.Infrastructure.Entrainement;

namespace Palier.Api.Entrainement;

/// <summary>
/// Les points d'entrée du catalogue d'exercices.
/// </summary>
/// <remarks>
/// <b>Trois codes de statut différents pour trois refus différents</b>, et
/// chacun a son motif. 404 quand l'exercice n'est pas visible — son existence
/// est un secret. 403 quand il est public — l'appelant vient de le lire, un 404
/// serait un mensonge. 409 quand des séries l'utilisent — ce n'est ni une
/// permission manquante ni une ressource absente, c'est un conflit d'état, et
/// c'est exactement ce que 409 dit.
/// </remarks>
internal static class Catalogue
{
    public static void Router(RouteGroupBuilder groupe)
    {
        ArgumentNullException.ThrowIfNull(groupe);

        groupe.MapGet("/exercices", ListerAsync);
        groupe.MapPost("/exercices", CreerAsync);
        groupe.MapDelete("/exercices/{identifiant:guid}", SupprimerAsync);
    }

    public static async Task<IResult> ListerAsync(
        IExecuteurDeCasDUsage executeur,
        ListerLeCatalogue gestionnaire,
        CancellationToken jeton
    )
    {
        ArgumentNullException.ThrowIfNull(executeur);
        ArgumentNullException.ThrowIfNull(gestionnaire);

        var exercices = await executeur
            .ExecuterAsync(nameof(ListerLeCatalogue), gestionnaire.ExecuterAsync, jeton)
            .ConfigureAwait(false);

        return Results.Ok(exercices);
    }

    public static async Task<IResult> CreerAsync(
        CreationDExercice demande,
        IExecuteurDeCasDUsage executeur,
        CreerUnExercice gestionnaire,
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

        var exercice = await executeur
            .ExecuterAsync(
                nameof(CreerUnExercice),
                j => gestionnaire.ExecuterAsync(demande, j),
                jeton
            )
            .ConfigureAwait(false);

        return Results.Created($"/api/v1/exercices/{exercice.Id}", exercice);
    }

    public static async Task<IResult> SupprimerAsync(
        Guid identifiant,
        IExecuteurDeCasDUsage executeur,
        SupprimerUnExercice gestionnaire,
        CancellationToken jeton
    )
    {
        ArgumentNullException.ThrowIfNull(executeur);
        ArgumentNullException.ThrowIfNull(gestionnaire);

        var issue = await executeur
            .ExecuterAsync(
                nameof(SupprimerUnExercice),
                j => gestionnaire.ExecuterAsync(identifiant, j),
                jeton
            )
            .ConfigureAwait(false);

        return issue switch
        {
            IssueDeSuppressionDExercice.Supprime => Results.NoContent(),

            IssueDeSuppressionDExercice.Public => Results.Json(
                new Reponse("ExercicePublic"),
                statusCode: StatusCodes.Status403Forbidden
            ),

            IssueDeSuppressionDExercice.Utilise => Results.Json(
                new Reponse("ExerciceUtilise"),
                statusCode: StatusCodes.Status409Conflict
            ),

            _ => Results.Json(
                new Reponse("ExerciceIntrouvable"),
                statusCode: StatusCodes.Status404NotFound
            ),
        };
    }
}
