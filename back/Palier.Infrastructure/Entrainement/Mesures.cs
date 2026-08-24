using Microsoft.EntityFrameworkCore;
using Palier.Application.Entrainement;
using Palier.Application.Pipeline;
using Palier.Domain.Entrainement;
using Palier.Domain.Grandeurs;

namespace Palier.Infrastructure.Entrainement;

// Ce que le produit sait dire des données : la force estimée, le plateau, le
// volume.
//
// CES GESTIONNAIRES NE CALCULENT RIEN. Epley, les paliers de fiabilité, la
// règle des trois séances, le dépliage primaire/secondaire — tout vit dans
// `Palier.Domain` ou dans la vue SQL, tous deux éprouvés. Recopier une formule
// ici créerait une seconde implémentation d'une même connaissance, sur des
// valeurs que l'utilisateur voit et sur lesquelles il modifie son
// entraînement.

/// <summary>La force estimée et le plateau, pour un exercice.</summary>
[GestionnaireDeCasDUsage]
public sealed class LireLaProgression(PalierDbContext contexte)
{
    public async Task<ProgressionRendue?> ExecuterAsync(
        Guid exerciceId,
        CancellationToken jeton
    )
    {
        // L'exercice d'abord : rendre une progression vide pour un identifiant
        // inventé ferait croire à un exercice sans historique, là où il n'y a
        // pas d'exercice du tout.
        var visible = await contexte
            .Exercises.AsNoTracking()
            .AnyAsync(e => e.Id == exerciceId, jeton)
            .ConfigureAwait(false);

        if (!visible)
        {
            return null;
        }

        // Les séries DURES, avec la date de leur séance. L'échauffement est
        // exclu ICI et non plus loin : une série d'échauffement lourde à deux
        // répétitions gonflerait l'estimation, et le produit proposerait une
        // progression sur un maximum qui n'a jamais été soulevé.
        //
        // `WeightKg > 0` et `Reps >= 1` ne dupliquent aucune borne : c'est le
        // domaine de définition d'Epley, qui refuse une charge nulle, et une
        // série sans répétition notée n'est pas exploitable.
        var lignes = await (
            from serie in contexte.WorkoutSets.AsNoTracking()
            join seance in contexte.Workouts.AsNoTracking() on serie.WorkoutId equals seance.Id
            where
                serie.ExerciseId == exerciceId
                && !serie.IsWarmup
                && serie.WeightKg > 0m
                && serie.Reps >= 1
            select new
            {
                seance.StartedAt,
                Charge = serie.WeightKg!.Value,
                Repetitions = serie.Reps!.Value,
                serie.Rir,
            }
        )
            .ToListAsync(jeton)
            .ConfigureAwait(false);

        if (lignes.Count == 0)
        {
            // L'ÉTAT VIDE, et non une absence de réponse. « Je n'ai pas encore
            // de donnée sur cet exercice » se dit ; `11-qualite.md` exige que
            // cet état soit traité plutôt que confondu avec une erreur.
            return new ProgressionRendue(exerciceId, null, null, EnPlateau: false, 0);
        }

        var meilleure = lignes
            .Select(l =>
                ForceEstimee.Epley(
                    Charge.DepuisKilogrammes(l.Charge),
                    Repetitions.De(l.Repetitions),
                    l.Rir is { } reserve ? Rir.De(reserve) : null
                )
            )
            .Where(e => e is not null)
            .OrderByDescending(e => e!.UnRepetitionMaximum.Kilogrammes)
            .FirstOrDefault();

        // Une séance = sa charge maximale, et les répétitions faites À cette
        // charge. C'est la forme que `DetectionPlateau` consomme, et la
        // définition du document : « même charge maximale sur trois séances
        // consécutives sans progression du nombre de répétitions ».
        var seances = lignes
            .GroupBy(l => DateOnly.FromDateTime(l.StartedAt.UtcDateTime))
            .Select(jour =>
            {
                var chargeMax = jour.Max(l => l.Charge);
                return new SeanceDExercice(
                    jour.Key,
                    Charge.DepuisKilogrammes(chargeMax),
                    jour.Where(l => l.Charge == chargeMax).Max(l => l.Repetitions)
                );
            })
            .ToArray();

        return new ProgressionRendue(
            exerciceId,
            meilleure?.UnRepetitionMaximum.Kilogrammes,
            meilleure?.Fiabilite.ToString(),
            DetectionPlateau.EstEnPlateau(seances),
            lignes.Count
        );
    }
}

/// <summary>Le volume hebdomadaire par muscle, lu dans la vue.</summary>
[GestionnaireDeCasDUsage]
public sealed class LireLeVolume(PalierDbContext contexte)
{
    public async Task<BilanDeVolume> ExecuterAsync(
        int? semaines,
        DateOnly aujourdHui,
        CancellationToken jeton
    )
    {
        var nombre = BilanDeVolume.BornerLesSemaines(semaines);

        // ON RAISONNE EN LUNDIS, parce que la vue rend des lundis :
        // `date_trunc('week', ...)` de PostgreSQL ramène chaque ligne au lundi
        // de sa semaine ISO, à minuit UTC.
        //
        // La version précédente soustrayait `7 × nombre` jours à la date du
        // jour, ce qui préserve le jour de la semaine : un lundi, la borne
        // tombait sur un lundi, donc SUR une valeur de la vue, donc incluse par
        // le `>=`. La même requête rendait N+1 semaines les lundis et N les
        // autres jours — et le plafond de cent quatre semaines en valait cent
        // cinq un jour sur sept.
        //
        // En partant du lundi courant, le compte est le même quel que soit le
        // jour de l'appel. L'invariance est vraie par construction, pas par
        // coïncidence avec l'opérateur de comparaison.
        var lundiCourant = aujourdHui.AddDays(-(((int)aujourdHui.DayOfWeek + 6) % 7));
        var depuis = new DateTimeOffset(
            lundiCourant.AddDays(-7 * (nombre - 1)).ToDateTime(TimeOnly.MinValue),
            TimeSpan.Zero
        );

        // AUCUN filtre sur `owner_id` : la vue porte `security_invoker = true`,
        // donc elle s'exécute sous les politiques de l'appelant. C'est ce qui
        // rend cette requête sûre — et une épreuve d'isolation le prouve sur la
        // vue elle-même, parce que ce réglage se perd sans bruit.
        var lignes = await contexte
            .WeeklyVolumes.AsNoTracking()
            .Where(v => v.Week >= depuis)
            .ToListAsync(jeton)
            .ConfigureAwait(false);

        return new BilanDeVolume(
            [
                .. lignes
                    .GroupBy(v => DateOnly.FromDateTime(v.Week.UtcDateTime))
                    .OrderByDescending(semaine => semaine.Key)
                    .Select(semaine => new SemaineDeVolume(
                        semaine.Key,
                        [
                            .. semaine
                                .OrderByDescending(v => v.HardSets)
                                .ThenBy(v => v.Muscle, StringComparer.Ordinal)
                                .Select(v => new VolumeDUnMuscle(v.Muscle, v.HardSets)),
                        ]
                    )),
            ]
        );
    }
}
