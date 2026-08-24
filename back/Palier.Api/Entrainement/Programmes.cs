using Palier.Application.Entrainement;
using Palier.Application.Pipeline;
using Palier.Infrastructure.Entrainement;

namespace Palier.Api.Entrainement;

/// <summary>
/// Les points d'entrée des programmes.
/// </summary>
/// <remarks>
/// <para>
/// <b>Trois codes de statut pour trois refus différents</b>, comme au
/// catalogue. 404 quand le programme n'est pas visible — son existence est un
/// secret. 403 quand c'est un modèle : l'appelant vient de le lire, un 404
/// serait un mensonge. 400 quand un exercice demandé n'existe pas — c'est la
/// requête qui est fautive, pas l'état du serveur.
/// </para>
///
/// <para>
/// <b>Il n'y a PAS de route pour modifier un modèle</b>, et ce n'est pas un
/// oubli : les neuf modèles viennent du référentiel, versionné dans le dépôt et
/// relu. Une route qui les modifierait ferait diverger la base du fichier au
/// premier appel.
/// </para>
/// </remarks>
internal static class Programmes
{
    public static void Router(RouteGroupBuilder groupe)
    {
        ArgumentNullException.ThrowIfNull(groupe);

        groupe.MapGet("/programmes", ListerAsync);
        groupe.MapGet("/programmes/{identifiant:guid}", LireAsync);
        groupe.MapPost("/programmes", CreerAsync);
        groupe.MapPut("/programmes/{identifiant:guid}", RemplacerAsync);
        groupe.MapPost("/programmes/{identifiant:guid}/copie", CopierAsync);
        groupe.MapDelete("/programmes/{identifiant:guid}", SupprimerAsync);
    }

    public static async Task<IResult> ListerAsync(
        IExecuteurDeCasDUsage executeur,
        ListerLesProgrammes gestionnaire,
        CancellationToken jeton
    )
    {
        ArgumentNullException.ThrowIfNull(executeur);
        ArgumentNullException.ThrowIfNull(gestionnaire);

        var programmes = await executeur
            .ExecuterAsync(nameof(ListerLesProgrammes), gestionnaire.ExecuterAsync, jeton)
            .ConfigureAwait(false);

        return Results.Ok(programmes);
    }

    public static async Task<IResult> LireAsync(
        Guid identifiant,
        IExecuteurDeCasDUsage executeur,
        LireUnProgramme gestionnaire,
        CancellationToken jeton
    )
    {
        ArgumentNullException.ThrowIfNull(executeur);
        ArgumentNullException.ThrowIfNull(gestionnaire);

        var programme = await executeur
            .ExecuterAsync(
                nameof(LireUnProgramme),
                j => gestionnaire.ExecuterAsync(identifiant, j),
                jeton
            )
            .ConfigureAwait(false);

        return programme is null
            ? Results.Json(
                new Reponse("ProgrammeIntrouvable"),
                statusCode: StatusCodes.Status404NotFound
            )
            : Results.Ok(programme);
    }

    public static async Task<IResult> CreerAsync(
        EcritureDeProgramme demande,
        IExecuteurDeCasDUsage executeur,
        CreerUnProgramme gestionnaire,
        CancellationToken jeton
    )
    {
        ArgumentNullException.ThrowIfNull(executeur);
        ArgumentNullException.ThrowIfNull(gestionnaire);
        ArgumentNullException.ThrowIfNull(demande);

        // La validation se fait ICI, À LA FRONTIÈRE, et une seule fois. Le
        // gestionnaire reçoit ensuite un type qui ne peut pas être invalide —
        // il n'a donc aucune branche à porter pour un cas que cette ligne rend
        // impossible.
        var (valide, faute) = ProgrammeValide.Lire(demande);
        if (valide is null)
        {
            return Results.Json(new Reponse(faute!), statusCode: StatusCodes.Status400BadRequest);
        }

        var (issue, identifiant) = await executeur
            .ExecuterAsync(
                nameof(CreerUnProgramme),
                j => gestionnaire.ExecuterAsync(valide, j),
                jeton
            )
            .ConfigureAwait(false);

        return issue == IssueDEcritureDeProgramme.Ecrit
            ? Results.Created($"/api/v1/programmes/{identifiant}", new { Id = identifiant })
            : Results.Json(
                new Reponse("ExerciceInconnu"),
                statusCode: StatusCodes.Status400BadRequest
            );
    }

    public static async Task<IResult> RemplacerAsync(
        Guid identifiant,
        EcritureDeProgramme demande,
        IExecuteurDeCasDUsage executeur,
        RemplacerUnProgramme gestionnaire,
        CancellationToken jeton
    )
    {
        ArgumentNullException.ThrowIfNull(executeur);
        ArgumentNullException.ThrowIfNull(gestionnaire);
        ArgumentNullException.ThrowIfNull(demande);

        var (valide, faute) = ProgrammeValide.Lire(demande);
        if (valide is null)
        {
            return Results.Json(new Reponse(faute!), statusCode: StatusCodes.Status400BadRequest);
        }

        var issue = await executeur
            .ExecuterAsync(
                nameof(RemplacerUnProgramme),
                j => gestionnaire.ExecuterAsync(identifiant, valide, j),
                jeton
            )
            .ConfigureAwait(false);

        return Traduire(issue, Results.NoContent());
    }

    public static async Task<IResult> CopierAsync(
        Guid identifiant,
        IExecuteurDeCasDUsage executeur,
        CopierUnProgramme gestionnaire,
        CancellationToken jeton
    )
    {
        ArgumentNullException.ThrowIfNull(executeur);
        ArgumentNullException.ThrowIfNull(gestionnaire);

        var (issue, copie) = await executeur
            .ExecuterAsync(
                nameof(CopierUnProgramme),
                j => gestionnaire.ExecuterAsync(identifiant, j),
                jeton
            )
            .ConfigureAwait(false);

        // La copie D'UN MODÈLE est le cas nominal — c'est même la raison d'être
        // de la route. `Modele` n'y est donc pas un refus, et la traduction
        // commune ne s'applique pas ici.
        return issue == IssueDEcritureDeProgramme.Ecrit
            ? Results.Created($"/api/v1/programmes/{copie}", new { Id = copie })
            : Results.Json(
                new Reponse("ProgrammeIntrouvable"),
                statusCode: StatusCodes.Status404NotFound
            );
    }

    public static async Task<IResult> SupprimerAsync(
        Guid identifiant,
        IExecuteurDeCasDUsage executeur,
        SupprimerUnProgramme gestionnaire,
        CancellationToken jeton
    )
    {
        ArgumentNullException.ThrowIfNull(executeur);
        ArgumentNullException.ThrowIfNull(gestionnaire);

        var issue = await executeur
            .ExecuterAsync(
                nameof(SupprimerUnProgramme),
                j => gestionnaire.ExecuterAsync(identifiant, j),
                jeton
            )
            .ConfigureAwait(false);

        return issue switch
        {
            IssueDeSuppressionDeProgramme.Supprime => Results.NoContent(),

            IssueDeSuppressionDeProgramme.Modele => Results.Json(
                new Reponse("ProgrammeModele"),
                statusCode: StatusCodes.Status403Forbidden
            ),

            _ => Results.Json(
                new Reponse("ProgrammeIntrouvable"),
                statusCode: StatusCodes.Status404NotFound
            ),
        };
    }

    /// <summary>La traduction des issues d'ÉCRITURE. Un seul endroit.</summary>
    private static IResult Traduire(IssueDEcritureDeProgramme issue, IResult succes) =>
        issue switch
        {
            IssueDEcritureDeProgramme.Ecrit => succes,

            IssueDEcritureDeProgramme.Modele => Results.Json(
                new Reponse("ProgrammeModele"),
                statusCode: StatusCodes.Status403Forbidden
            ),

            IssueDEcritureDeProgramme.ExerciceInconnu => Results.Json(
                new Reponse("ExerciceInconnu"),
                statusCode: StatusCodes.Status400BadRequest
            ),

            _ => Results.Json(
                new Reponse("ProgrammeIntrouvable"),
                statusCode: StatusCodes.Status404NotFound
            ),
        };
}
