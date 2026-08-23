using Microsoft.EntityFrameworkCore;
using Palier.Application.Entrainement;
using Palier.Application.Pipeline;
using Palier.Infrastructure.Entites;

namespace Palier.Infrastructure.Entrainement;

// Le catalogue d'exercices.
//
// C'EST LA FORME « CATALOGUE MIXTE » DU SCHÉMA, et elle porte DEUX politiques
// RLS séparées plutôt qu'une seule avec un `OR` :
//
//   catalogue_public  for select  using (is_custom = false)
//   proprietaire      for all     using (owner_id = app.utilisateur_ou_null())
//
// La séparation n'est pas cosmétique : PostgreSQL ne garantit aucun ordre
// d'évaluation entre les branches d'un `OR`, et la branche publique ne doit
// pas pouvoir déclencher l'accesseur d'identité. C'est écrit dans la migration
// du socle, et ça a une conséquence directe ici.
//
// CONSÉQUENCE : le public est LISIBLE PAR TOUS et MODIFIABLE PAR PERSONNE. Un
// `DELETE` sur un exercice public ne trouve simplement aucune ligne — la
// politique de lecture est `for select` seulement. Le gestionnaire doit donc
// distinguer trois cas que le moteur, lui, réduit à « zéro ligne
// supprimée » : il n'existe pas, il existe mais il est public, il est à moi.

/// <summary>La traduction d'une ligne en réponse. UN seul endroit.</summary>
internal static class RenduDExercice
{
    public static ExerciceRendu Depuis(Exercise exercice) =>
        new(
            exercice.Id,
            exercice.Name,
            exercice.Equipment,
            exercice.PrimaryMuscles,
            exercice.SecondaryMuscles,
            exercice.IsUnilateral,
            exercice.DefaultIncrement,
            exercice.ContraindicatedFor,
            exercice.IsCustom
        );
}

/// <summary>Ce qu'une suppression d'exercice peut refuser.</summary>
public enum IssueDeSuppressionDExercice
{
    /// <summary>L'exercice est supprimé.</summary>
    Supprime,

    /// <summary>Il n'existe pas — ou appartient à quelqu'un d'autre.</summary>
    Introuvable,

    /// <summary>Il appartient au catalogue public : personne ne le supprime.</summary>
    Public,

    /// <summary>Des séries le référencent : le supprimer effacerait de l'historique.</summary>
    Utilise,
}

/// <summary>Liste le catalogue public ET les exercices de l'appelant.</summary>
[GestionnaireDeCasDUsage]
public sealed class ListerLeCatalogue(PalierDbContext contexte)
{
    public async Task<IReadOnlyList<ExerciceRendu>> ExecuterAsync(CancellationToken jeton)
    {
        // AUCUN filtre. Les deux politiques font le travail : le public sort
        // par `catalogue_public`, les siens par `proprietaire`, et ceux des
        // autres ne sortent pas. Écrire `where is_custom == false || owner_id
        // == moi` ici serait une troisième expression de la même règle — celle
        // qui divergerait le jour où les politiques changent.
        var exercices = await contexte
            .Exercises.AsNoTracking()
            .OrderBy(e => e.Name)
            .ToListAsync(jeton)
            .ConfigureAwait(false);

        return [.. exercices.Select(RenduDExercice.Depuis)];
    }
}

/// <summary>Crée un exercice personnalisé.</summary>
[GestionnaireDeCasDUsage]
public sealed class CreerUnExercice(PalierDbContext contexte, IIdentiteDemandeur demandeur)
{
    public async Task<ExerciceRendu> ExecuterAsync(
        CreationDExercice demande,
        CancellationToken jeton
    )
    {
        ArgumentNullException.ThrowIfNull(demande);

        var exercice = new Exercise
        {
            Name = demande.Nom.Trim(),
            Equipment = string.IsNullOrWhiteSpace(demande.Materiel) ? null : demande.Materiel.Trim(),
            PrimaryMuscles = [.. demande.PrimairesOuVide.Select(m => m.Trim())],
            SecondaryMuscles = [.. demande.SecondairesOuVide.Select(m => m.Trim())],
            IsUnilateral = demande.Unilateral,
            DefaultIncrement = demande.IncrementParDefaut,

            // Normalisées à la forme stockée. Sans cela, « Épaule » et
            // « epaule » cohabiteraient en base et l'intersection avec les
            // contraintes déclarées manquerait la moitié des cas.
            ContraindicatedFor =
            [
                .. demande.ContraintesOuVide.Select(c =>
                    Contraintes.Lire(c, out var lue)
                        ? Contraintes.EnBase(lue.Value)
                        : c
                ),
            ],

            // VRAI, toujours. Un exercice créé par un utilisateur qui entrerait
            // avec `is_custom = false` rejoindrait le catalogue PUBLIC de tout
            // le monde — et la politique `catalogue_public` le rendrait
            // visible de tous, sans que personne puisse plus le supprimer.
            IsCustom = true,

            // L'identité vient du demandeur, jamais de la requête — D36. Le
            // moteur le vérifie une seconde fois par le `with check` de la
            // politique `proprietaire`.
            OwnerId = demandeur.Identifiant.GetValueOrDefault(),
        };

        contexte.Exercises.Add(exercice);
        await contexte.SaveChangesAsync(jeton).ConfigureAwait(false);
        return RenduDExercice.Depuis(exercice);
    }
}

/// <summary>Supprime un exercice personnalisé — et refuse tout le reste.</summary>
[GestionnaireDeCasDUsage]
public sealed class SupprimerUnExercice(PalierDbContext contexte)
{
    public async Task<IssueDeSuppressionDExercice> ExecuterAsync(
        Guid identifiant,
        CancellationToken jeton
    )
    {
        var exercice = await contexte
            .Exercises.FirstOrDefaultAsync(e => e.Id == identifiant, jeton)
            .ConfigureAwait(false);

        if (exercice is null)
        {
            return IssueDeSuppressionDExercice.Introuvable;
        }

        // PUBLIC : refus explicite, et non « introuvable ». Contrairement aux
        // séances, l'existence d'un exercice public n'est pas un secret :
        // l'appelant vient de le lire dans le catalogue. Répondre 404 sur une
        // ressource qu'on vient de lui montrer serait un mensonge, et le
        // laisserait chercher un défaut de son côté.
        if (!exercice.IsCustom)
        {
            return IssueDeSuppressionDExercice.Public;
        }

        // UTILISÉ : `FK_sets_exercises_exercise_id` porte `Restrict`, donc le
        // moteur refuserait par une violation de clé étrangère — un 500 pour un
        // geste parfaitement compréhensible. Et le refus est le bon
        // comportement : supprimer l'exercice effacerait des séries de
        // l'historique, donc du volume et de la progression déjà mesurés.
        //
        // RLS borne cette recherche aux séries de l'appelant — ce qui suffit :
        // un exercice personnalisé n'appartient qu'à lui, donc seules ses
        // séries peuvent le référencer.
        var utilise = await contexte
            .WorkoutSets.AsNoTracking()
            .AnyAsync(s => s.ExerciseId == identifiant, jeton)
            .ConfigureAwait(false);

        if (utilise)
        {
            return IssueDeSuppressionDExercice.Utilise;
        }

        contexte.Exercises.Remove(exercice);
        await contexte.SaveChangesAsync(jeton).ConfigureAwait(false);
        return IssueDeSuppressionDExercice.Supprime;
    }
}
