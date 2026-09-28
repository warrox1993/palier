using Microsoft.EntityFrameworkCore;
using Palier.Application.Entrainement;
using Palier.Application.Pipeline;
using Palier.Infrastructure.Entites;

namespace Palier.Infrastructure.Entrainement;

// Le ressenti par exercice — `docs/05-entrainement.md` § 5.
//
// POSSÉDÉ PAR JOINTURE, comme les séries : `exercise_feedback` n'a pas de
// colonne `owner_id`, et sa politique remonte jusqu'à `workouts`. Les mêmes
// conséquences s'appliquent, et le même contrôle applicatif précède le moteur
// pour que le refus soit un 404 lisible plutôt qu'un 42501 devenu 500.
//
// CE QUE CE LOT NE FAIT PAS : appliquer les seuils du § 5 — un `pain` sur trois
// séances, deux `pain` consécutifs, trois `meh` consécutifs. Ils produisent des
// messages destinés à l'utilisateur, dont l'un ORIENTE VERS UN PROFESSIONNEL ;
// `01-conformite.md` sépare informer de prescrire, et ces libellés vivent en
// base, versionnés et validés. Le lot 5 enregistre et expose ; la règle arrive
// avec l'écran qui la montre.

/// <summary>Ce qu'une note de ressenti peut refuser.</summary>
public enum IssueDeRessenti
{
    /// <summary>Le ressenti est enregistré — ou remplacé.</summary>
    Note,

    /// <summary>La séance n'existe pas — ou n'est pas la sienne.</summary>
    SeanceIntrouvable,

    /// <summary>L'exercice n'existe pas — ou n'est pas visible.</summary>
    ExerciceIntrouvable,
}

/// <summary>Note le ressenti d'un exercice. Deux fois vaut remplacement.</summary>
[GestionnaireDeCasDUsage]
public sealed class NoterUnRessenti(PalierDbContext contexte)
{
    /// <param name="seanceId">La séance notée.</param>
    /// <param name="exerciceId">L'exercice noté.</param>
    /// <param name="ressenti">
    /// DÉJÀ VALIDÉ — c'est une valeur d'énumération, pas une chaîne. Le
    /// gestionnaire n'a donc aucune lecture à faire, et aucun <c>!</c> à poser
    /// sur son résultat. `CLAUDE.md` § 4 : « valider à la frontière, une seule
    /// fois, puis faire confiance au type ».
    /// </param>
    /// <param name="jeton">Jeton d'annulation.</param>
    public async Task<IssueDeRessenti> ExecuterAsync(
        Guid seanceId,
        Guid exerciceId,
        Ressenti ressenti,
        CancellationToken jeton
    )
    {
        var seanceVisible = await contexte
            .Workouts.AsNoTracking()
            .AnyAsync(s => s.Id == seanceId, jeton)
            .ConfigureAwait(false);

        if (!seanceVisible)
        {
            return IssueDeRessenti.SeanceIntrouvable;
        }

        var exerciceVisible = await contexte
            .Exercises.AsNoTracking()
            .AnyAsync(e => e.Id == exerciceId, jeton)
            .ConfigureAwait(false);

        if (!exerciceVisible)
        {
            return IssueDeRessenti.ExerciceIntrouvable;
        }

        // `unique (workout_id, exercise_id)` : changer d'avis en cours de
        // séance est un REMPLACEMENT. Sans ce traitement, la seconde note
        // remonterait une violation de contrainte — donc un 500 — à quelqu'un
        // qui se ravise, ce qui est exactement ce qu'on veut encourager.
        var existant = await contexte
            .ExerciseFeedbacks.FirstOrDefaultAsync(
                f => f.WorkoutId == seanceId && f.ExerciseId == exerciceId,
                jeton
            )
            .ConfigureAwait(false);

        // La normalisation, et elle seule : « PAIN » saisi devient « pain »
        // stocké, ce que la contrainte `CHECK` exige. Aucune lecture de chaîne
        // ici — le type est déjà une valeur d'énumération.
        var valeur = Ressentis.EnBase(ressenti);

        if (existant is null)
        {
            contexte.ExerciseFeedbacks.Add(
                new ExerciseFeedback
                {
                    WorkoutId = seanceId,
                    ExerciseId = exerciceId,
                    Rating = valeur,
                }
            );
        }
        else
        {
            existant.Rating = valeur;
        }

        await contexte.SaveChangesAsync(jeton).ConfigureAwait(false);
        return IssueDeRessenti.Note;
    }
}

/// <summary>L'historique des ressentis d'un exercice, du plus récent au plus ancien.</summary>
[GestionnaireDeCasDUsage]
public sealed class ListerLesRessentis(PalierDbContext contexte)
{
    public async Task<IReadOnlyList<RessentiRendu>?> ExecuterAsync(
        Guid exerciceId,
        int? combien,
        CancellationToken jeton
    )
    {
        var visible = await contexte
            .Exercises.AsNoTracking()
            .AnyAsync(e => e.Id == exerciceId, jeton)
            .ConfigureAwait(false);

        if (!visible)
        {
            return null;
        }

        // L'ORDRE VIENT DE LA SÉANCE, et c'est une correction de fond.
        //
        // Le § 5 raisonne sur des séances CONSÉCUTIVES — « 2 pain consécutifs »,
        // « 3 meh consécutifs », « sur les 3 dernières séances ». La chronologie
        // qui compte est donc celle de l'ENTRAÎNEMENT, pas celle du moment où
        // l'on a tapé la note. Le lot 5 triait sur une date de saisie : noter
        // après coup le ressenti d'une séance ancienne l'aurait placée en tête,
        // et « deux `pain` consécutifs » aurait désigné deux séances qui ne se
        // suivent pas — une orientation vers un professionnel sur une suite
        // inventée.
        var lignes = await (
            from note in contexte.ExerciseFeedbacks.AsNoTracking()
            join seance in contexte.Workouts.AsNoTracking() on note.WorkoutId equals seance.Id
            where note.ExerciseId == exerciceId
            orderby seance.StartedAt descending, seance.Id descending
            select new RessentiRendu(note.WorkoutId, note.ExerciseId, note.Rating, seance.StartedAt)
        )
            .Take(RessentiRendu.BornerLHistorique(combien))
            .ToListAsync(jeton)
            .ConfigureAwait(false);

        return lignes;
    }
}
