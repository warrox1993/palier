using Palier.Application.Entrainement;
using Palier.Application.Pipeline;
using Palier.Infrastructure.Entrainement;

namespace Palier.Api.Entrainement;

/// <summary>
/// Les points d'entrée de la série.
/// </summary>
/// <remarks>
/// <b>La validation tombe AVANT la transaction.</b> Les bornes viennent du
/// domaine — <c>AjoutDeSerie.Faute</c> appelle les prédicats dont les fabriques
/// se servent — et le refus est un 400 nommé, jamais une exception qui
/// remonterait en 500. Une charge de −5 kg est une saisie, pas un défaut du
/// serveur.
/// </remarks>
internal static class Series
{
    public static void Router(RouteGroupBuilder groupe)
    {
        ArgumentNullException.ThrowIfNull(groupe);

        groupe.MapPost("/seances/{seanceId:guid}/series", AjouterAsync);
        groupe.MapDelete("/seances/{seanceId:guid}/series/{serieId:guid}", RetirerAsync);
    }

    public static async Task<IResult> AjouterAsync(
        Guid seanceId,
        AjoutDeSerie demande,
        IExecuteurDeCasDUsage executeur,
        AjouterUneSerie gestionnaire,
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

        var rendu = await executeur
            .ExecuterAsync(
                nameof(AjouterUneSerie),
                j => gestionnaire.ExecuterAsync(seanceId, demande, horloge, j),
                jeton
            )
            .ConfigureAwait(false);

        return rendu.Issue switch
        {
            IssueDAjoutDeSerie.Ajoutee => Results.Created(
                $"/api/v1/seances/{seanceId}/series/{rendu.Serie!.Id}",
                rendu.Serie
            ),

            // 404 pour la séance : son existence est un secret.
            IssueDAjoutDeSerie.SeanceIntrouvable => Results.Json(
                new Reponse("SeanceIntrouvable"),
                statusCode: StatusCodes.Status404NotFound
            ),

            // 400 pour l'exercice, et NON 404 : la ressource visée par l'URL
            // est la séance, qui existe. L'exercice est une valeur du CORPS —
            // une saisie invalide, au même titre qu'une charge hors bornes.
            _ => Results.Json(
                new Reponse("ExerciceIntrouvable"),
                statusCode: StatusCodes.Status400BadRequest
            ),
        };
    }

    public static async Task<IResult> RetirerAsync(
        Guid seanceId,
        Guid serieId,
        IExecuteurDeCasDUsage executeur,
        RetirerUneSerie gestionnaire,
        CancellationToken jeton
    )
    {
        ArgumentNullException.ThrowIfNull(executeur);
        ArgumentNullException.ThrowIfNull(gestionnaire);

        var retiree = await executeur
            .ExecuterAsync(
                nameof(RetirerUneSerie),
                j => gestionnaire.ExecuterAsync(seanceId, serieId, j),
                jeton
            )
            .ConfigureAwait(false);

        return retiree
            ? Results.NoContent()
            : Results.Json(
                new Reponse("SerieIntrouvable"),
                statusCode: StatusCodes.Status404NotFound
            );
    }
}
