using System.Collections.Immutable;
using Microsoft.EntityFrameworkCore;
using Palier.Application.Entrainement;
using Palier.Application.Pipeline;
using Palier.Domain.Entrainement;
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
    /// <param name="exercice">La ligne du catalogue.</param>
    /// <param name="declarees">
    /// Les contraintes de l'appelant, par région. VIDE hors du catalogue — la
    /// création d'un exercice n'a rien à marquer, puisque l'utilisateur vient
    /// de choisir lui-même ses contre-indications.
    /// </param>
    public static ExerciceRendu Depuis(
        Exercise exercice,
        IReadOnlyDictionary<string, string> declarees
    ) =>
        new(
            exercice.Id,
            exercice.Slug,
            exercice.NameFr,
            exercice.NameEn,
            exercice.InstructionsFr,
            exercice.InstructionsEn,
            exercice.CommonErrorsFr,
            exercice.CommonErrorsEn,
            exercice.MovementRole,
            exercice.Equipment,
            exercice.PrimaryMuscles,
            exercice.SecondaryMuscles,
            exercice.IsUnilateral,
            exercice.DefaultIncrement,
            exercice.ContraindicatedFor,
            exercice.IsCustom,
            // L'INTERSECTION, et rien de plus. Aucune ligne n'est retirée :
            // c'est le marquage qui informe, et l'écran qui présente.
            [
                .. exercice
                    .ContraindicatedFor.Where(declarees.ContainsKey)
                    .OrderBy(region => region, StringComparer.Ordinal)
                    .Select(region => new MarquageDeContrainte(region, declarees[region])),
            ]
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
    /// <summary>
    /// Rend le catalogue visible, éventuellement borné à un fragment de nom —
    /// dans l'une ou l'autre langue. Un terme nul rend le catalogue entier.
    /// </summary>
    public async Task<IReadOnlyList<ExerciceRendu>> ExecuterAsync(
        string? recherche,
        CancellationToken jeton
    )
    {
        // AUCUN filtre D'APPARTENANCE. Les deux politiques font ce travail : le
        // public sort par `catalogue_public`, les siens par `proprietaire`, et
        // ceux des autres ne sortent pas. Écrire `where is_custom == false ||
        // owner_id == moi` ici serait une troisième expression de la même règle
        // — celle qui divergerait le jour où les politiques changent.
        var lignes = contexte.Exercises.AsNoTracking();

        // Le terme est normalisé par la MÊME règle que la colonne — D73. Sans
        // cela, `Développé` chercherait un accent que la clé ne contient pas,
        // et ne trouverait jamais rien.
        var motif = CleDeRecherche.MotifPourLike(recherche);
        if (motif.Length > 0)
        {
            // `EF.Functions.Like` et non `Contains` : `Contains` engendre
            // `strpos(...) > 0`, dont la sémantique de casse dépend du
            // fournisseur. Ici les deux côtés sont déjà normalisés par la même
            // règle, et `Like` dit exactement ce qu'on veut.
            //
            // Le motif est un PARAMÈTRE LIÉ, et ses jokers sont ÉCHAPPÉS. Un
            // utilisateur qui tape `%` cherche un pour-cent : il ne pilote pas
            // la requête.
            lignes = lignes.Where(e => e.SearchKey != null && EF.Functions.Like(e.SearchKey, motif));
        }

        var exercices = await lignes
            .OrderBy(e => e.NameFr)
            .ToListAsync(jeton)
            .ConfigureAwait(false);

        // Les contraintes de l'appelant, dans la MÊME transaction, donc sous la
        // même identité. RLS les borne aux siennes.
        var declarees = await contexte
            .UserConstraints.AsNoTracking()
            .ToDictionaryAsync(c => c.Region, c => c.Severity, StringComparer.Ordinal, jeton)
            .ConfigureAwait(false);

        // ET LE CATALOGUE SORT ENTIER. Le marquage informe ; il ne retire
        // rien. Voir le doc de `MarquageDeContrainte` pour ce qui l'impose.
        return [.. exercices.Select(e => RenduDExercice.Depuis(e, declarees))];
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
            // Le nom donné devient le nom FRANÇAIS. Un exercice
            // personnalisé n'a ni traduction ni consignes : la contrainte
            // `CHECK` ne les exige que du catalogue public — D70.
            NameFr = demande.Nom.Trim(),

            // `aucun` : le rôle sert au ratio tirage/poussée, qui se calcule
            // sur le catalogue relu. Le faire deviner à l'utilisateur
            // fausserait un indicateur qu'il ne comprend pas encore.
            MovementRole = "aucun",
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

        // AUCUN marquage à la création : l'utilisateur vient de choisir
        // lui-même les contre-indications de son exercice. Les lui renvoyer
        // marquées lui apprendrait ce qu'il vient de taper.
        return RenduDExercice.Depuis(exercice, ImmutableDictionary<string, string>.Empty);
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

        // UTILISÉ : DEUX tables référencent `exercises` en `Restrict`, et il
        // faut les interroger toutes les deux.
        //
        //   FK_sets_exercises_exercise_id              (lot 2)
        //   FK_exercise_feedback_exercises_exercise_id (lot 5)
        //
        // La seconde a été ajoutée dans le même lot que ce gestionnaire, et le
        // commentaire qui vivait ici — « seules ses séries peuvent le
        // référencer » — est devenu faux le jour même sans que personne le
        // relise. Un ressenti se note SANS série : `NoterUnRessenti` ne vérifie
        // que la visibilité de la séance et de l'exercice. L'état « exercice
        // référencé uniquement par un ressenti » est donc atteignable par
        // l'API, et il produisait un 500 là où le contrat annonce un 409 nommé.
        //
        // Le refus reste le bon comportement : supprimer l'exercice effacerait
        // de l'historique — des séries, ou un ressenti qui a peut-être motivé
        // une orientation vers un professionnel.
        //
        // RLS borne les deux recherches aux données de l'appelant, ce qui
        // suffit : un exercice personnalisé n'appartient qu'à lui.
        var utilise =
            await contexte
                .WorkoutSets.AsNoTracking()
                .AnyAsync(s => s.ExerciseId == identifiant, jeton)
                .ConfigureAwait(false)
            || await contexte
                .ExerciseFeedbacks.AsNoTracking()
                .AnyAsync(f => f.ExerciseId == identifiant, jeton)
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
