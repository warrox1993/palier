using Microsoft.EntityFrameworkCore;
using Palier.Application.Entrainement;
using Palier.Application.Pipeline;
using Palier.Infrastructure.Entites;

namespace Palier.Infrastructure.Entrainement;

// Les gestionnaires de la séance.
//
// ILS VIVENT EN INFRASTRUCTURE, ET NON EN APPLICATION. Ils prennent
// `PalierDbContext`, qui vit ici : les placer dans `Palier.Application`
// obligerait soit à inverser le sens des dépendances, soit à faire entrer
// EF Core dans la couche applicative. C'est le motif déjà écrit en tête de
// `Palier.Application/Pipeline/Pipeline.cs` pour `ExecuteurDeCasDUsage`, et il
// vaut pareil ici.
//
// AUCUN FILTRE SUR `owner_id` À LA LECTURE. Le pipeline a posé l'identité dans
// la transaction, et RLS mord : un `FirstOrDefault` sur un identifiant qui
// appartient à quelqu'un d'autre rend `null`, sans que personne ait eu à
// l'écrire. Ajouter le filtre à la main créerait une seconde façon de dire la
// même chose — et le jour où les deux divergent, c'est la mauvaise qui gagne,
// en silence.

/// <summary>La traduction d'une ligne en réponse. UN seul endroit.</summary>
internal static class RenduDeSeance
{
    public static SeanceRendue Depuis(Workout seance) =>
        new(
            seance.Id,
            seance.StartedAt,
            seance.EndedAt,
            seance.SleepHours,
            seance.Energy15,
            seance.Note
        );
}

/// <summary>Ouvre une séance.</summary>
[GestionnaireDeCasDUsage]
public sealed class OuvrirUneSeance(PalierDbContext contexte, IIdentiteDemandeur demandeur)
{
    public async Task<Guid> ExecuterAsync(
        OuvertureDeSeance demande,
        TimeProvider horloge,
        CancellationToken jeton
    )
    {
        ArgumentNullException.ThrowIfNull(demande);
        ArgumentNullException.ThrowIfNull(horloge);

        var seance = new Workout
        {
            // L'identité vient du DEMANDEUR — donc du jeton vérifié — et jamais
            // du corps de la requête : D36, et `CLAUDE.md` § 4, « l'identité ne
            // se lit jamais dans la requête ».
            //
            // `GetValueOrDefault()` et non un `throw` : l'exécuteur a DÉJÀ
            // refusé l'absence d'identité avant d'ouvrir la transaction — une
            // levée ici serait une branche que rien ne peut franchir, donc du
            // code mort déguisé en prudence. Et si ce refus venait à sauter, le
            // moteur reste : `with check (owner_id = app.utilisateur())` refuse
            // un `Guid.Empty` par un 42501. Deux filets, aucune branche morte.
            OwnerId = demandeur.Identifiant.GetValueOrDefault(),
            StartedAt = demande.Debut ?? horloge.GetUtcNow(),
            SleepHours = demande.HeuresDeSommeil,
            Note = demande.Note,
        };

        contexte.Workouts.Add(seance);
        await contexte.SaveChangesAsync(jeton).ConfigureAwait(false);
        return seance.Id;
    }
}

/// <summary>Lit une séance ET ses séries. Rend <c>null</c> si elle n'existe pas — ou n'est pas la sienne.</summary>
[GestionnaireDeCasDUsage]
public sealed class LireUneSeance(PalierDbContext contexte)
{
    public async Task<SeanceDetaillee?> ExecuterAsync(Guid identifiant, CancellationToken jeton)
    {
        var seance = await contexte
            .Workouts.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == identifiant, jeton)
            .ConfigureAwait(false);

        // Les DEUX cas rendent `null`, et c'est voulu : « elle n'existe pas »
        // et « elle n'est pas à vous » doivent être indiscernables de
        // l'extérieur. Les distinguer transformerait la route en oracle
        // d'existence.
        if (seance is null)
        {
            return null;
        }

        // Les séries dans la MÊME transaction, donc sous la même identité.
        // Triées par rang : l'ordre d'insertion n'est pas garanti par
        // PostgreSQL, et un écran de séance qui affiche les séries dans le
        // désordre est un écran faux.
        var series = await contexte
            .WorkoutSets.AsNoTracking()
            .Where(s => s.WorkoutId == identifiant)
            .OrderBy(s => s.SetIndex)
            .ThenBy(s => s.LoggedAt)
            .ToListAsync(jeton)
            .ConfigureAwait(false);

        return new SeanceDetaillee(
            RenduDeSeance.Depuis(seance),
            [.. series.Select(RenduDeSerie.Depuis)]
        );
    }
}

/// <summary>Clôt une séance et note l'énergie.</summary>
[GestionnaireDeCasDUsage]
public sealed class CloturerUneSeance(PalierDbContext contexte)
{
    public async Task<SeanceRendue?> ExecuterAsync(
        Guid identifiant,
        ClotureDeSeance demande,
        TimeProvider horloge,
        CancellationToken jeton
    )
    {
        ArgumentNullException.ThrowIfNull(demande);
        ArgumentNullException.ThrowIfNull(horloge);

        var seance = await contexte
            .Workouts.FirstOrDefaultAsync(s => s.Id == identifiant, jeton)
            .ConfigureAwait(false);

        if (seance is null)
        {
            return null;
        }

        seance.EndedAt = demande.Fin ?? horloge.GetUtcNow();

        // `?? seance.Energy15` et non une affectation sèche : clore une séance
        // sans redonner l'énergie ne doit pas EFFACER celle qui était notée.
        seance.Energy15 = demande.Energie ?? seance.Energy15;

        await contexte.SaveChangesAsync(jeton).ConfigureAwait(false);
        return RenduDeSeance.Depuis(seance);
    }
}

/// <summary>Supprime une séance. Rend faux si elle n'existe pas — ou n'est pas la sienne.</summary>
[GestionnaireDeCasDUsage]
public sealed class SupprimerUneSeance(PalierDbContext contexte)
{
    public async Task<bool> ExecuterAsync(Guid identifiant, CancellationToken jeton)
    {
        var seance = await contexte
            .Workouts.FirstOrDefaultAsync(s => s.Id == identifiant, jeton)
            .ConfigureAwait(false);

        if (seance is null)
        {
            return false;
        }

        // Les séries partent avec elle : la clé étrangère de `sets` porte
        // `on delete cascade`, posée par la migration du socle. Les supprimer
        // ici en plus produirait deux chemins pour un même effet.
        contexte.Workouts.Remove(seance);
        await contexte.SaveChangesAsync(jeton).ConfigureAwait(false);
        return true;
    }
}

/// <summary>Liste les séances, de la plus récente à la plus ancienne, par pages.</summary>
/// <remarks>
/// <para>
/// <b>Par CURSEUR, et non par décalage.</b> Une liste triée par date
/// décroissante reçoit ses insertions EN TÊTE : entre le moment où le client
/// lit la page 1 et celui où il demande la page 2, une séance ouverte décale
/// tout d'un rang, et un <c>OFFSET 20</c> saute la vingtième — définitivement,
/// sans erreur et sans trou visible. Le curseur ne bouge pas quand la liste
/// grandit.
/// </para>
///
/// <para>
/// <b>Le curseur est un COUPLE.</b> Deux séances peuvent porter le même
/// <c>started_at</c> — un import, deux entraînements notés à la minute près —
/// et un curseur réduit à l'instant en sauterait une. Le couple
/// <c>(started_at, id)</c> est total : il n'y a jamais d'ex æquo.
/// </para>
///
/// <para>
/// <b><c>EF.Functions.LessThan</c> sur un tuple</b> traduit en comparaison de
/// ROW VALUES PostgreSQL — <c>(started_at, id) &lt; (@a, @b)</c> — et non en
/// deux conditions reliées par un <c>OR</c>. La différence est mesurable :
/// la forme row value se sert de l'index <c>(owner_id, started_at desc)</c>
/// posé par D39, là où le <c>OR</c> force souvent un parcours. Et elle
/// s'écrit une fois, sans risque de se tromper de bord.
/// </para>
/// </remarks>
[GestionnaireDeCasDUsage]
public sealed class ListerLesSeances(PalierDbContext contexte)
{
    public async Task<PageDeSeances> ExecuterAsync(
        DateTimeOffset? avant,
        Guid? avantId,
        int? limite,
        CancellationToken jeton
    )
    {
        var taille = PageDeSeances.Borner(limite);
        var requete = contexte.Workouts.AsNoTracking();

        // Les DEUX moitiés du curseur, ou aucune. Une seule serait un curseur
        // qu'on croit composite et qui ne l'est pas — le pire des deux mondes.
        if (avant is { } instant && avantId is { } dernier)
        {
            requete = requete.Where(s =>
                EF.Functions.LessThan(
                    ValueTuple.Create(s.StartedAt, s.Id),
                    ValueTuple.Create(instant, dernier)
                )
            );
        }

        // UNE de plus que la taille demandée. C'est ce qui permet de savoir
        // s'il reste quelque chose SANS compter la table entière — un
        // `count(*)` sur chaque page coûterait un parcours complet à chaque
        // fois, pour une information dont le client n'a pas besoin.
        var lot = await requete
            .OrderByDescending(s => s.StartedAt)
            .ThenByDescending(s => s.Id)
            .Take(taille + 1)
            .ToListAsync(jeton)
            .ConfigureAwait(false);

        var reste = lot.Count > taille;
        var borne = reste ? lot[taille - 1] : null;

        return new PageDeSeances(
            [.. lot.Take(taille).Select(RenduDeSeance.Depuis)],
            borne?.StartedAt,
            borne?.Id
        );
    }
}
