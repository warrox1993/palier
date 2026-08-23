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
    public async Task<IssueDeRessenti> ExecuterAsync(
        Guid seanceId,
        NoteDeRessenti demande,
        TimeProvider horloge,
        CancellationToken jeton
    )
    {
        ArgumentNullException.ThrowIfNull(demande);
        ArgumentNullException.ThrowIfNull(horloge);

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
            .AnyAsync(e => e.Id == demande.ExerciceId, jeton)
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
                f => f.WorkoutId == seanceId && f.ExerciseId == demande.ExerciceId,
                jeton
            )
            .ConfigureAwait(false);

        // La valeur est déjà validée par `NoteDeRessenti.Faute`, appelé au
        // point d'entrée. On la RELIT ici plutôt que de faire confiance à la
        // chaîne brute : c'est la normalisation qui compte — « PAIN » saisi
        // devient « pain » stocké, et la contrainte `CHECK` n'accepte que la
        // forme minuscule.
        Ressentis.Lire(demande.Ressenti, out var lu);
        var valeur = Ressentis.EnBase(lu!.Value);

        if (existant is null)
        {
            contexte.ExerciseFeedbacks.Add(
                new ExerciseFeedback
                {
                    WorkoutId = seanceId,
                    ExerciseId = demande.ExerciceId,
                    Feeling = valeur,
                    NotedAt = horloge.GetUtcNow(),
                }
            );
        }
        else
        {
            existant.Feeling = valeur;
            existant.NotedAt = horloge.GetUtcNow();
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

        // DU PLUS RÉCENT AU PLUS ANCIEN, et l'ordre n'est pas cosmétique : les
        // seuils du § 5 parlent de valeurs CONSÉCUTIVES — « 2 pain
        // consécutifs », « 3 meh consécutifs ». Un historique rendu dans le
        // désordre ferait conclure sur des suites qui n'ont pas eu lieu.
        var lignes = await contexte
            .ExerciseFeedbacks.AsNoTracking()
            .Where(f => f.ExerciseId == exerciceId)
            .OrderByDescending(f => f.NotedAt)
            .ThenByDescending(f => f.Id)
            .Take(RessentiRendu.BornerLHistorique(combien))
            .ToListAsync(jeton)
            .ConfigureAwait(false);

        return
        [
            .. lignes.Select(f => new RessentiRendu(
                f.WorkoutId,
                f.ExerciseId,
                f.Feeling,
                f.NotedAt
            )),
        ];
    }
}
