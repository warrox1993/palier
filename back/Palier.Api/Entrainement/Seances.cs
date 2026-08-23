using Palier.Application.Entrainement;
using Palier.Application.Pipeline;
using Palier.Infrastructure.Entrainement;

namespace Palier.Api.Entrainement;

/// <summary>
/// Les points d'entrée de la séance.
/// </summary>
/// <remarks>
/// <para>
/// <b>Chaque route passe par <c>IExecuteurDeCasDUsage</c>.</b> C'est lui qui
/// ouvre la transaction et y pose l'identité ; un gestionnaire appelé
/// directement lirait la base SANS identité posée, et le moteur refuserait —
/// ou pire, ne refuserait pas, sur une table vide.
/// </para>
///
/// <para>
/// <b>404 partout où la ressource n'est pas la sienne, jamais 403.</b> Un 403
/// dirait « cet identifiant existe, mais pas pour vous » — et sur un produit de
/// santé, savoir qu'une séance porte tel identifiant, c'est déjà savoir que
/// quelqu'un s'entraîne. Les deux causes sont indiscernables de l'extérieur, et
/// le gestionnaire les rend indiscernables à la source en donnant <c>null</c>
/// pour les deux.
/// </para>
/// </remarks>
internal static class Seances
{
    /// <summary>Le chemin d'une séance, pour l'en-tête <c>Location</c>.</summary>
    private const string _base = "/api/v1/seances";

    public static void Router(RouteGroupBuilder groupe)
    {
        ArgumentNullException.ThrowIfNull(groupe);

        groupe.MapGet("/seances", ListerAsync);
        groupe.MapPost("/seances", OuvrirAsync);
        groupe.MapGet("/seances/{identifiant:guid}", LireAsync);
        groupe.MapPatch("/seances/{identifiant:guid}", CloturerAsync);
        groupe.MapDelete("/seances/{identifiant:guid}", SupprimerAsync);
    }

    public static async Task<IResult> OuvrirAsync(
        OuvertureDeSeance demande,
        IExecuteurDeCasDUsage executeur,
        OuvrirUneSeance gestionnaire,
        TimeProvider horloge,
        CancellationToken jeton
    )
    {
        ArgumentNullException.ThrowIfNull(executeur);
        ArgumentNullException.ThrowIfNull(gestionnaire);

        var identifiant = await executeur
            .ExecuterAsync(
                nameof(OuvrirUneSeance),
                j => gestionnaire.ExecuterAsync(demande, horloge, j),
                jeton
            )
            .ConfigureAwait(false);

        return Results.Created($"{_base}/{identifiant}", new { id = identifiant });
    }

    public static async Task<IResult> ListerAsync(
        DateTimeOffset? avant,
        Guid? avantId,
        int? limite,
        IExecuteurDeCasDUsage executeur,
        ListerLesSeances gestionnaire,
        CancellationToken jeton
    )
    {
        ArgumentNullException.ThrowIfNull(executeur);
        ArgumentNullException.ThrowIfNull(gestionnaire);

        var page = await executeur
            .ExecuterAsync(
                nameof(ListerLesSeances),
                j => gestionnaire.ExecuterAsync(avant, avantId, limite, j),
                jeton
            )
            .ConfigureAwait(false);

        // AUCUN refus sur une limite démesurée : elle est BORNÉE, en silence.
        // Un 400 sur `?limite=1000` obligerait le client à connaître notre
        // plafond pour ne pas se faire refuser, alors que la seule chose qui
        // compte est qu'on ne matérialise pas la table entière.
        return Results.Ok(page);
    }

    public static async Task<IResult> LireAsync(
        Guid identifiant,
        IExecuteurDeCasDUsage executeur,
        LireUneSeance gestionnaire,
        CancellationToken jeton
    )
    {
        ArgumentNullException.ThrowIfNull(executeur);
        ArgumentNullException.ThrowIfNull(gestionnaire);

        var seance = await executeur
            .ExecuterAsync(
                nameof(LireUneSeance),
                j => gestionnaire.ExecuterAsync(identifiant, j),
                jeton
            )
            .ConfigureAwait(false);

        return seance is null ? Introuvable() : Results.Ok(seance);
    }

    public static async Task<IResult> CloturerAsync(
        Guid identifiant,
        ClotureDeSeance demande,
        IExecuteurDeCasDUsage executeur,
        CloturerUneSeance gestionnaire,
        TimeProvider horloge,
        CancellationToken jeton
    )
    {
        ArgumentNullException.ThrowIfNull(executeur);
        ArgumentNullException.ThrowIfNull(gestionnaire);
        ArgumentNullException.ThrowIfNull(demande);

        // Le contrôle tombe AVANT la transaction. Laisser le `CHECK` de la
        // migration parler rendrait un 500 — « le serveur a un défaut » — là où
        // l'utilisateur a simplement tapé 6. Et une transaction ouverte pour
        // rien coûte une connexion du pool.
        if (!demande.EnergieValide)
        {
            return Results.Json(
                new Reponse("EnergieHorsBornes"),
                statusCode: StatusCodes.Status400BadRequest
            );
        }

        var seance = await executeur
            .ExecuterAsync(
                nameof(CloturerUneSeance),
                j => gestionnaire.ExecuterAsync(identifiant, demande, horloge, j),
                jeton
            )
            .ConfigureAwait(false);

        return seance is null ? Introuvable() : Results.Ok(seance);
    }

    public static async Task<IResult> SupprimerAsync(
        Guid identifiant,
        IExecuteurDeCasDUsage executeur,
        SupprimerUneSeance gestionnaire,
        CancellationToken jeton
    )
    {
        ArgumentNullException.ThrowIfNull(executeur);
        ArgumentNullException.ThrowIfNull(gestionnaire);

        var supprimee = await executeur
            .ExecuterAsync(
                nameof(SupprimerUneSeance),
                j => gestionnaire.ExecuterAsync(identifiant, j),
                jeton
            )
            .ConfigureAwait(false);

        return supprimee ? Results.NoContent() : Introuvable();
    }

    /// <summary>
    /// LE MÊME refus pour « elle n'existe pas » et « elle n'est pas à vous ».
    /// Écrit une seule fois pour que les deux ne puissent pas diverger.
    /// </summary>
    private static IResult Introuvable() =>
        Results.Json(new Reponse("SeanceIntrouvable"), statusCode: StatusCodes.Status404NotFound);
}
