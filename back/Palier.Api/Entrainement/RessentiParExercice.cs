using Palier.Application.Entrainement;
using Palier.Application.Pipeline;
using Palier.Infrastructure.Entrainement;

namespace Palier.Api.Entrainement;

/// <summary>
/// Les points d'entrée du ressenti par exercice.
/// </summary>
/// <remarks>
/// <b>L'API rend des CODES, jamais les messages du § 5.</b> Deux <c>pain</c>
/// consécutifs doivent mener à « un retrait proposé, une variante, ET une
/// orientation vers un professionnel » — une formulation qui touche à la santé,
/// donc qui vit en base, versionnée et validée, jamais dans une chaîne C#.
/// C'est la ligne que <c>docs/01-conformite.md</c> trace entre informer et
/// prescrire.
/// </remarks>
internal static class RessentiParExercice
{
    public static void Router(RouteGroupBuilder groupe)
    {
        ArgumentNullException.ThrowIfNull(groupe);

        groupe.MapPut("/seances/{seanceId:guid}/ressenti", NoterAsync);
        groupe.MapGet("/exercices/{exerciceId:guid}/ressenti", ListerAsync);
    }

    public static async Task<IResult> NoterAsync(
        Guid seanceId,
        NoteDeRessenti demande,
        IExecuteurDeCasDUsage executeur,
        NoterUnRessenti gestionnaire,
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

        var issue = await executeur
            .ExecuterAsync(
                nameof(NoterUnRessenti),
                j => gestionnaire.ExecuterAsync(seanceId, demande, j),
                jeton
            )
            .ConfigureAwait(false);

        return issue switch
        {
            // 204 et non 201 : la route est idempotente — elle pose l'état
            // « voici ce que cet exercice m'a donné dans cette séance ». Se
            // raviser doit produire le même résultat que noter la première
            // fois, et c'est ce qu'on veut encourager.
            IssueDeRessenti.Note => Results.NoContent(),

            IssueDeRessenti.SeanceIntrouvable => Results.Json(
                new Reponse("SeanceIntrouvable"),
                statusCode: StatusCodes.Status404NotFound
            ),

            _ => Results.Json(
                new Reponse("ExerciceIntrouvable"),
                statusCode: StatusCodes.Status400BadRequest
            ),
        };
    }

    public static async Task<IResult> ListerAsync(
        Guid exerciceId,
        int? combien,
        IExecuteurDeCasDUsage executeur,
        ListerLesRessentis gestionnaire,
        CancellationToken jeton
    )
    {
        ArgumentNullException.ThrowIfNull(executeur);
        ArgumentNullException.ThrowIfNull(gestionnaire);

        var ressentis = await executeur
            .ExecuterAsync(
                nameof(ListerLesRessentis),
                j => gestionnaire.ExecuterAsync(exerciceId, combien, j),
                jeton
            )
            .ConfigureAwait(false);

        return ressentis is null
            ? Results.Json(
                new Reponse("ExerciceIntrouvable"),
                statusCode: StatusCodes.Status404NotFound
            )
            : Results.Ok(ressentis);
    }
}
