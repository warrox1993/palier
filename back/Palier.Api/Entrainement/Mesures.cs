using Palier.Application.Pipeline;
using Palier.Infrastructure.Entrainement;

namespace Palier.Api.Entrainement;

/// <summary>
/// Les points d'entrée de la mesure : progression et volume.
/// </summary>
/// <remarks>
/// Ces deux routes ne portent AUCUN calcul et AUCUNE phrase. Elles rendent des
/// nombres et des codes ; ce qu'on en dit à l'utilisateur vit en base,
/// versionné et validé — <c>09-comptes.md</c> — et
/// <c>docs/05-entrainement.md</c> § 2 le dit du plateau en toutes lettres : le
/// système « signale, SANS prescrire de solution ».
/// </remarks>
internal static class Mesures
{
    public static void Router(RouteGroupBuilder groupe)
    {
        ArgumentNullException.ThrowIfNull(groupe);

        groupe.MapGet("/exercices/{exerciceId:guid}/progression", ProgressionAsync);
        groupe.MapGet("/volume-hebdomadaire", VolumeAsync);
    }

    public static async Task<IResult> ProgressionAsync(
        Guid exerciceId,
        IExecuteurDeCasDUsage executeur,
        LireLaProgression gestionnaire,
        CancellationToken jeton
    )
    {
        ArgumentNullException.ThrowIfNull(executeur);
        ArgumentNullException.ThrowIfNull(gestionnaire);

        var progression = await executeur
            .ExecuterAsync(
                nameof(LireLaProgression),
                j => gestionnaire.ExecuterAsync(exerciceId, j),
                jeton
            )
            .ConfigureAwait(false);

        // 404 quand l'exercice n'existe pas — ou n'est pas visible. Une
        // progression vide pour un identifiant inventé ferait croire à un
        // exercice sans historique, là où il n'y a pas d'exercice du tout.
        return progression is null
            ? Results.Json(
                new Reponse("ExerciceIntrouvable"),
                statusCode: StatusCodes.Status404NotFound
            )
            : Results.Ok(progression);
    }

    public static async Task<IResult> VolumeAsync(
        int? semaines,
        IExecuteurDeCasDUsage executeur,
        LireLeVolume gestionnaire,
        TimeProvider horloge,
        CancellationToken jeton
    )
    {
        ArgumentNullException.ThrowIfNull(executeur);
        ArgumentNullException.ThrowIfNull(gestionnaire);
        ArgumentNullException.ThrowIfNull(horloge);

        var aujourdHui = DateOnly.FromDateTime(horloge.GetUtcNow().UtcDateTime);

        var bilan = await executeur
            .ExecuterAsync(
                nameof(LireLeVolume),
                j => gestionnaire.ExecuterAsync(semaines, aujourdHui, j),
                jeton
            )
            .ConfigureAwait(false);

        // 200 avec zéro semaine : quelqu'un qui n'a pas encore d'entraînement
        // n'est pas une erreur.
        return Results.Ok(bilan);
    }
}
