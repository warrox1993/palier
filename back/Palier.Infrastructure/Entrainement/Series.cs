using Microsoft.EntityFrameworkCore;
using Palier.Application.Entrainement;
using Palier.Application.Pipeline;
using Palier.Infrastructure.Entites;

namespace Palier.Infrastructure.Entrainement;

// Les séries.
//
// LA SÉRIE EST POSSÉDÉE PAR JOINTURE : la table `sets` n'a pas de colonne
// `owner_id`, et sa politique RLS remonte jusqu'à `workouts`. Deux
// conséquences qui ne se devinent pas :
//
//   1. Rattacher une série à la séance d'autrui échoue au moteur, par le
//      `with check` de la politique — pas par un contrôle applicatif.
//   2. Mais le moteur rend un 42501, donc une exception, donc un 500. Ce
//      n'est pas une réponse acceptable pour un identifiant simplement
//      inconnu. Le gestionnaire VÉRIFIE donc d'abord que la séance est
//      visible, et rend `null` sinon.
//
// Le contrôle applicatif ne REMPLACE pas la politique : il la précède, pour
// que le refus soit un 404 lisible plutôt qu'un défaut de serveur. La
// politique reste le dernier mot, et c'est elle qui tiendrait si ce contrôle
// disparaissait.

/// <summary>La traduction d'une ligne en réponse. UN seul endroit.</summary>
internal static class RenduDeSerie
{
    public static SerieRendue Depuis(WorkoutSet serie) =>
        new(
            serie.Id,
            serie.ExerciseId,
            serie.SetIndex,
            serie.WeightKg,
            serie.Reps,
            serie.Rir,
            serie.IsWarmup,
            serie.LoggedAt
        );
}

/// <summary>Ce qu'un ajout de série peut refuser.</summary>
public enum IssueDAjoutDeSerie
{
    /// <summary>La série est enregistrée.</summary>
    Ajoutee,

    /// <summary>La séance n'existe pas — ou n'est pas la sienne.</summary>
    SeanceIntrouvable,

    /// <summary>L'exercice n'existe pas — ou n'est pas visible.</summary>
    ExerciceIntrouvable,
}

/// <summary>Le résultat d'un ajout : l'issue, et la série si elle a été créée.</summary>
public sealed record AjoutRendu(IssueDAjoutDeSerie Issue, SerieRendue? Serie);

/// <summary>Ajoute une série à une séance.</summary>
[GestionnaireDeCasDUsage]
public sealed class AjouterUneSerie(PalierDbContext contexte)
{
    public async Task<AjoutRendu> ExecuterAsync(
        Guid seanceId,
        AjoutDeSerie demande,
        TimeProvider horloge,
        CancellationToken jeton
    )
    {
        ArgumentNullException.ThrowIfNull(demande);
        ArgumentNullException.ThrowIfNull(horloge);

        // La séance d'abord. Sans ce contrôle, une série rattachée à la séance
        // d'autrui partirait au moteur, qui la refuserait par un 42501 — donc
        // un 500, là où l'appelant mérite un 404.
        var seanceVisible = await contexte
            .Workouts.AsNoTracking()
            .AnyAsync(s => s.Id == seanceId, jeton)
            .ConfigureAwait(false);

        if (!seanceVisible)
        {
            return new AjoutRendu(IssueDAjoutDeSerie.SeanceIntrouvable, null);
        }

        // L'exercice ensuite. `FK_sets_exercises_exercise_id` refuserait un
        // identifiant inconnu par une violation de clé étrangère — encore un
        // 500 pour une saisie. Et la politique de `exercises` fait que « pas
        // visible » et « n'existe pas » sont déjà indiscernables ici.
        var exerciceVisible = await contexte
            .Exercises.AsNoTracking()
            .AnyAsync(e => e.Id == demande.ExerciceId, jeton)
            .ConfigureAwait(false);

        if (!exerciceVisible)
        {
            return new AjoutRendu(IssueDAjoutDeSerie.ExerciceIntrouvable, null);
        }

        var serie = new WorkoutSet
        {
            WorkoutId = seanceId,
            ExerciseId = demande.ExerciceId,
            SetIndex = demande.Index,
            WeightKg = demande.ChargeKg,
            Reps = demande.Repetitions,
            Rir = demande.Rir,
            IsWarmup = demande.Echauffement,
            LoggedAt = horloge.GetUtcNow(),
        };

        contexte.WorkoutSets.Add(serie);
        await contexte.SaveChangesAsync(jeton).ConfigureAwait(false);

        return new AjoutRendu(IssueDAjoutDeSerie.Ajoutee, RenduDeSerie.Depuis(serie));
    }
}

/// <summary>Retire une série. Rend faux si elle n'existe pas — ou n'est pas la sienne.</summary>
[GestionnaireDeCasDUsage]
public sealed class RetirerUneSerie(PalierDbContext contexte)
{
    public async Task<bool> ExecuterAsync(
        Guid seanceId,
        Guid serieId,
        CancellationToken jeton
    )
    {
        // Le `WorkoutId` est dans la condition, et ce n'est PAS redondant avec
        // RLS. RLS garantit que la série appartient à l'appelant ; il ne
        // garantit pas qu'elle appartient à la séance nommée dans l'URL. Sans
        // cette clause, `DELETE /seances/{A}/series/{s}` effacerait une série
        // de la séance B — ses propres données, mais pas celles qu'il visait.
        var serie = await contexte
            .WorkoutSets.FirstOrDefaultAsync(s => s.Id == serieId && s.WorkoutId == seanceId, jeton)
            .ConfigureAwait(false);

        if (serie is null)
        {
            return false;
        }

        contexte.WorkoutSets.Remove(serie);
        await contexte.SaveChangesAsync(jeton).ConfigureAwait(false);
        return true;
    }
}
