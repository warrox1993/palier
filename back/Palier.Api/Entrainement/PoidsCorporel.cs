using Palier.Application.Entrainement;
using Palier.Application.Pipeline;
using Palier.Infrastructure.Entrainement;

namespace Palier.Api.Entrainement;

/// <summary>
/// Les points d'entrée du poids corporel.
/// </summary>
/// <remarks>
/// <para>
/// <b>200 et non 201 sur l'enregistrement</b>, y compris à la première pesée du
/// jour. La route est idempotente : elle pose l'état « voici mon poids du
/// tel jour », et rejouer la même requête doit produire le même résultat. Un
/// 201 dirait « une ressource a été créée », ce qui est faux une fois sur
/// deux et pousserait un client à traiter les deux cas différemment.
/// </para>
///
/// <para>
/// <b>Le constat voyage avec la série, pas avec l'enregistrement.</b> Écrire sa
/// pesée et apprendre dans la même réponse qu'on perd trop vite ferait de la
/// saisie un moment de jugement — et `01-conformite.md` § 5 met en garde
/// contre exactement cela. La série se lit quand l'utilisateur va voir sa
/// courbe, c'est-à-dire quand il cherche à comprendre.
/// </para>
/// </remarks>
internal static class PoidsCorporel
{
    public static void Router(RouteGroupBuilder groupe)
    {
        ArgumentNullException.ThrowIfNull(groupe);

        groupe.MapPut("/poids", EnregistrerAsync);
        groupe.MapGet("/poids", ListerAsync);
    }

    public static async Task<IResult> EnregistrerAsync(
        MesureDePoids demande,
        IExecuteurDeCasDUsage executeur,
        EnregistrerLePoids gestionnaire,
        TimeProvider horloge,
        CancellationToken jeton
    )
    {
        ArgumentNullException.ThrowIfNull(executeur);
        ArgumentNullException.ThrowIfNull(gestionnaire);
        ArgumentNullException.ThrowIfNull(demande);
        ArgumentNullException.ThrowIfNull(horloge);

        // La date du jour est calculée UNE fois et passée partout : la lire
        // deux fois ferait qu'une requête arrivant à minuit pile validerait
        // contre un jour et écrirait dans l'autre.
        var aujourdHui = DateOnly.FromDateTime(horloge.GetUtcNow().UtcDateTime);

        if (demande.Faute(aujourdHui) is { } faute)
        {
            return Results.Json(new Reponse(faute), statusCode: StatusCodes.Status400BadRequest);
        }

        var rendu = await executeur
            .ExecuterAsync(
                nameof(EnregistrerLePoids),
                j => gestionnaire.ExecuterAsync(demande, aujourdHui, j),
                jeton
            )
            .ConfigureAwait(false);

        return Results.Ok(rendu);
    }

    public static async Task<IResult> ListerAsync(
        int? jours,
        IExecuteurDeCasDUsage executeur,
        ListerLePoids gestionnaire,
        TimeProvider horloge,
        CancellationToken jeton
    )
    {
        ArgumentNullException.ThrowIfNull(executeur);
        ArgumentNullException.ThrowIfNull(gestionnaire);
        ArgumentNullException.ThrowIfNull(horloge);

        var aujourdHui = DateOnly.FromDateTime(horloge.GetUtcNow().UtcDateTime);

        var serie = await executeur
            .ExecuterAsync(
                nameof(ListerLePoids),
                j => gestionnaire.ExecuterAsync(jours, aujourdHui, j),
                jeton
            )
            .ConfigureAwait(false);

        // 200 avec une série vide, jamais 404 : « je n'ai pas encore pesé » est
        // une réponse, pas une erreur — et `11-qualite.md` exige un état vide
        // traité.
        return Results.Ok(serie);
    }
}
