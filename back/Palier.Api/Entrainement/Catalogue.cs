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

    /// <summary>
    /// Rend le catalogue visible. Le paramètre de requête <c>recherche</c> le
    /// borne à un fragment de nom, français ou anglais, accents optionnels ;
    /// absent, la route rend le catalogue entier.
    /// </summary>
    /// <remarks>
    /// La recherche existe pour la construction d'un programme — D73 : sur deux
    /// cent cinquante mouvements, taper un nom est le seul geste praticable au
    /// doigt. Elle est bornée pour la même raison que tout le reste : un terme
    /// d'un mégaoctet est une entrée hostile ordinaire.
    /// </remarks>
    public static async Task<IResult> ListerAsync(
        IExecuteurDeCasDUsage executeur,
        ListerLeCatalogue gestionnaire,
        CancellationToken jeton,
        string? recherche = null
    )
    {
        ArgumentNullException.ThrowIfNull(executeur);
        ArgumentNullException.ThrowIfNull(gestionnaire);

        if (recherche is { Length: > LongueurMaximaleDeLaRecherche })
        {
            return Results.Json(
                new Reponse("RechercheTropLongue"),
                statusCode: StatusCodes.Status400BadRequest
            );
        }

        var exercices = await executeur
            .ExecuterAsync(
                nameof(ListerLeCatalogue),
                j => gestionnaire.ExecuterAsync(recherche, j),
                jeton
            )
            .ConfigureAwait(false);

        return Results.Ok(exercices);
    }

    /// <summary>La longueur maximale du terme cherché.</summary>
    /// <remarks>
    /// Aussi longue que le plus long nom d'exercice acceptable : chercher plus
    /// long que ce qui peut exister ne trouverait rien par construction.
    /// </remarks>
    internal const int LongueurMaximaleDeLaRecherche = CreationDExercice.LongueurMaximaleDuNom;

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
