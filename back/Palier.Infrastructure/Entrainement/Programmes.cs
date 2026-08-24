using Microsoft.EntityFrameworkCore;
using Palier.Application.Entrainement;
using Palier.Application.Pipeline;
using Palier.Infrastructure.Entites;

namespace Palier.Infrastructure.Entrainement;

// Les programmes.
//
// C'EST LA MÊME FORME « CATALOGUE MIXTE » QUE LES EXERCICES — D72 — et elle
// porte les mêmes DEUX politiques séparées, sur les trois tables :
//
//   modeles_publics  for select  using (is_template = true)
//   proprietaire     for all     using (owner_id = app.utilisateur_ou_null())
//
// CONSÉQUENCE, identique à celle du catalogue : un modèle est LISIBLE PAR TOUS
// et MODIFIABLE PAR PERSONNE. Un `UPDATE` ou un `DELETE` sur un modèle ne
// trouve aucune ligne — la politique publique est `for select` seulement. Le
// gestionnaire doit donc distinguer trois cas que le moteur réduit à « zéro
// ligne touchée » : il n'existe pas, c'est un modèle, il est à moi.
//
// LES DEUX TABLES FILLES NE PORTENT PAS `owner_id`, et leurs politiques
// remontent au programme. Le dupliquer aurait créé deux vérités sur la même
// appartenance, dont l'une aurait fini par être fausse.

/// <summary>Ce qu'une écriture de programme peut refuser.</summary>
public enum IssueDEcritureDeProgramme
{
    /// <summary>Le programme est écrit.</summary>
    Ecrit,

    /// <summary>Il n'existe pas — ou appartient à quelqu'un d'autre.</summary>
    Introuvable,

    /// <summary>C'est un modèle du catalogue : personne ne le modifie.</summary>
    Modele,

    /// <summary>Un des exercices demandés n'existe pas, ou n'est pas visible.</summary>
    ExerciceInconnu,
}

/// <summary>Ce qu'une suppression de programme peut refuser.</summary>
public enum IssueDeSuppressionDeProgramme
{
    /// <summary>Le programme est supprimé.</summary>
    Supprime,

    /// <summary>Il n'existe pas — ou appartient à quelqu'un d'autre.</summary>
    Introuvable,

    /// <summary>C'est un modèle du catalogue : personne ne le supprime.</summary>
    Modele,
}

/// <summary>Liste les modèles ET les programmes de l'appelant.</summary>
[GestionnaireDeCasDUsage]
public sealed class ListerLesProgrammes(PalierDbContext contexte)
{
    public async Task<IReadOnlyList<ProgrammeEnListe>> ExecuterAsync(CancellationToken jeton)
    {
        // AUCUN filtre, comme pour le catalogue : les deux politiques font le
        // travail. Écrire `where is_template || owner_id == moi` serait une
        // troisième expression de la même règle — celle qui divergerait le jour
        // où les politiques changent.
        //
        // Le comptage des séances passe par une sous-requête plutôt que par un
        // chargement : neuf modèles et leurs trente-six séances feraient
        // trente-six lignes remontées pour n'en afficher que neuf.
        var programmes = await contexte
            .Programs.AsNoTracking()
            .OrderByDescending(p => p.IsTemplate)
            .ThenBy(p => p.NameFr)
            .Select(p => new ProgrammeEnListe(
                p.Id,
                p.Slug,
                p.NameFr,
                p.DescriptionFr,
                p.IsTemplate,
                p.FrequencyMin,
                p.FrequencyMax,
                p.TargetsConstraint,
                p.IsActive,
                contexte.ProgramDays.Count(d => d.ProgramId == p.Id)
            ))
            .ToListAsync(jeton)
            .ConfigureAwait(false);

        return programmes;
    }
}

/// <summary>Lit un programme et tout son contenu, exercices marqués.</summary>
[GestionnaireDeCasDUsage]
public sealed class LireUnProgramme(PalierDbContext contexte)
{
    public async Task<ProgrammeRendu?> ExecuterAsync(Guid identifiant, CancellationToken jeton)
    {
        var programme = await contexte
            .Programs.AsNoTracking()
            .SingleOrDefaultAsync(p => p.Id == identifiant, jeton)
            .ConfigureAwait(false);

        if (programme is null)
        {
            return null;
        }

        // Une seule requête pour tout le contenu, jointe au catalogue pour en
        // tirer le nom et les contre-indications. Charger les séances puis
        // leurs exercices puis leurs exercices du catalogue ferait trois allers
        // — et le troisième serait un N+1.
        var lignes = await (
            from jour in contexte.ProgramDays
            where jour.ProgramId == identifiant
            join pose in contexte.ProgramExercises on jour.Id equals pose.ProgramDayId into poses
            from pose in poses.DefaultIfEmpty()
            join exercice in contexte.Exercises on pose.ExerciseId equals exercice.Id into exercices
            from exercice in exercices.DefaultIfEmpty()
            orderby jour.Position, pose.Position
            select new
            {
                JourId = jour.Id,
                jour.LabelFr,
                JourPosition = jour.Position,
                Pose = pose,
                NomDeLExercice = exercice.NameFr,
                exercice.ContraindicatedFor,
            }
        )
            .AsNoTracking()
            .ToListAsync(jeton)
            .ConfigureAwait(false);

        // Les contraintes de l'appelant, dans la MÊME transaction, donc sous la
        // même identité. RLS les borne aux siennes.
        var declarees = await contexte
            .UserConstraints.AsNoTracking()
            .ToDictionaryAsync(c => c.Region, c => c.Severity, StringComparer.Ordinal, jeton)
            .ConfigureAwait(false);

        var seances = lignes
            .GroupBy(l => new
            {
                l.JourId,
                l.LabelFr,
                l.JourPosition,
            })
            .OrderBy(g => g.Key.JourPosition)
            .Select(g => new SeanceDeProgrammeRendue(
                g.Key.JourId,
                g.Key.LabelFr,
                g.Key.JourPosition,
                [
                    .. g.Where(l => l.Pose is not null)
                        .OrderBy(l => l.Pose.Position)
                        .Select(l => new ExerciceDeSeanceRendu(
                            l.Pose.Id,
                            l.Pose.ExerciseId,
                            l.NomDeLExercice,
                            l.Pose.Position,
                            l.Pose.TargetSets,
                            l.Pose.TargetRepsMin,
                            l.Pose.TargetRepsMax,
                            l.Pose.TargetRir,
                            l.Pose.RestSeconds,
                            l.Pose.Note,
                            // L'INTERSECTION, et rien de plus — D75. Aucune
                            // ligne n'est retirée du programme : le marquage
                            // informe, l'écran présente, et l'utilisateur
                            // décide. Il sait ce que son kinésithérapeute lui a
                            // dit ; le logiciel, non.
                            [
                                .. (l.ContraindicatedFor ?? [])
                                    .Where(declarees.ContainsKey)
                                    .OrderBy(region => region, StringComparer.Ordinal)
                                    .Select(region => new MarquageDeContrainte(
                                        region,
                                        declarees[region]
                                    )),
                            ]
                        )),
                ]
            ))
            .ToList();

        return new ProgrammeRendu(
            programme.Id,
            programme.Slug,
            programme.NameFr,
            programme.DescriptionFr,
            programme.NotesFr,
            programme.IsTemplate,
            programme.FrequencyMin,
            programme.FrequencyMax,
            programme.TargetsConstraint,
            programme.IsActive,
            seances
        );
    }
}

/// <summary>Crée un programme personnel, avec son contenu s'il en porte un.</summary>
[GestionnaireDeCasDUsage]
public sealed class CreerUnProgramme(PalierDbContext contexte, IIdentiteDemandeur demandeur)
{
    public async Task<(IssueDEcritureDeProgramme Issue, Guid Identifiant)> ExecuterAsync(
        ProgrammeValide valide,
        CancellationToken jeton
    )
    {
        ArgumentNullException.ThrowIfNull(valide);

        if (!await ExercicesVisiblesAsync(valide, jeton).ConfigureAwait(false))
        {
            return (IssueDEcritureDeProgramme.ExerciceInconnu, Guid.Empty);
        }

        var programme = new TrainingProgram
        {
            NameFr = valide.Nom,
            DescriptionFr = valide.Description,
            IsActive = valide.Actif,

            // FAUX, toujours. Un programme créé par un utilisateur qui entrerait
            // avec `is_template = true` rejoindrait le catalogue de tout le
            // monde — et `ck_programs_proprietaire` le refuse d'ailleurs, parce
            // qu'un modèle ne peut pas avoir de propriétaire.
            IsTemplate = false,

            // L'identité vient du demandeur, jamais de la requête — D36. Le
            // moteur le vérifie une seconde fois par le `with check` de la
            // politique `proprietaire`.
            OwnerId = demandeur.Identifiant.GetValueOrDefault(),
        };

        contexte.Programs.Add(programme);
        await contexte.SaveChangesAsync(jeton).ConfigureAwait(false);

        await PoserLeContenuAsync(programme.Id, valide.Seances, jeton).ConfigureAwait(false);

        return (IssueDEcritureDeProgramme.Ecrit, programme.Id);
    }

    /// <summary>
    /// Vérifie que chaque exercice demandé est VISIBLE de l'appelant.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Une méthode D'INSTANCE, et non statique avec le contexte en
    /// paramètre.</b> Statique, le compilateur aurait posé ce paramètre en
    /// champ de la machine d'état asynchrone qu'il engendre — et
    /// <c>ArchitectureTests</c> l'aurait vue tenir <c>PalierDbContext</c> hors
    /// d'un gestionnaire. Le refus était juste : ce chemin-là interroge la base
    /// sans identité.
    /// </para>
    ///
    /// <para>
    /// RLS borne déjà la lecture au catalogue public et aux exercices de
    /// l'appelant : ce qui ne sort pas de cette requête n'existe pas pour lui.
    /// Le contrôle ne réimplémente donc aucune règle d'autorisation, il
    /// constate.
    /// </para>
    ///
    /// <para>
    /// <b>Sans lui, la clé étrangère lèverait une exception de base</b> — que
    /// le pipeline traduirait en 500. Un identifiant inventé est une entrée
    /// hostile ordinaire, et elle mérite un 400 nommé.
    /// </para>
    /// </remarks>
    internal async Task<bool> ExercicesVisiblesAsync(
        ProgrammeValide valide,
        CancellationToken jeton
    )
    {
        var demandes = valide
            .Seances.SelectMany(s => s.Exercices)
            .Select(e => e.ExerciceId)
            .Distinct()
            .ToArray();

        if (demandes.Length == 0)
        {
            return true;
        }

        var visibles = await contexte
            .Exercises.AsNoTracking()
            .Where(e => demandes.Contains(e.Id))
            .CountAsync(jeton)
            .ConfigureAwait(false);

        return visibles == demandes.Length;
    }

    /// <summary>
    /// Écrit les séances et leurs exercices. LES RANGS SONT ATTRIBUÉS ICI.
    /// </summary>
    /// <remarks>
    /// Le client envoie un ORDRE, jamais des numéros. S'il les fournissait, il
    /// pourrait poser deux fois le rang 3 — que l'index unique refuserait, avec
    /// une erreur de base là où il n'y a aucun conflit réel.
    /// </remarks>
    internal async Task PoserLeContenuAsync(
        Guid programme,
        IReadOnlyList<SeanceValidee> seances,
        CancellationToken jeton
    )
    {
        var rangDeLaSeance = 1;
        foreach (var seance in seances)
        {
            var jour = new ProgramDay
            {
                ProgramId = programme,
                LabelFr = seance.Libelle,
                Position = rangDeLaSeance++,
            };

            contexte.ProgramDays.Add(jour);
            await contexte.SaveChangesAsync(jeton).ConfigureAwait(false);

            var rangDeLExercice = 1;
            foreach (var exercice in seance.Exercices)
            {
                contexte.ProgramExercises.Add(
                    new ProgramExercise
                    {
                        ProgramDayId = jour.Id,
                        ExerciseId = exercice.ExerciceId,
                        Position = rangDeLExercice++,
                        TargetSets = exercice.Series,
                        TargetRepsMin = exercice.RepetitionsMin,
                        TargetRepsMax = exercice.RepetitionsMax,
                        TargetRir = exercice.RirCible,
                        RestSeconds = exercice.ReposSecondes,
                        Note = exercice.Note,
                    }
                );
            }

            await contexte.SaveChangesAsync(jeton).ConfigureAwait(false);
        }
    }
}

/// <summary>Remplace le contenu d'un programme personnel.</summary>
[GestionnaireDeCasDUsage]
public sealed class RemplacerUnProgramme(PalierDbContext contexte, CreerUnProgramme creation)
{
    public async Task<IssueDEcritureDeProgramme> ExecuterAsync(
        Guid identifiant,
        ProgrammeValide valide,
        CancellationToken jeton
    )
    {
        ArgumentNullException.ThrowIfNull(valide);

        var programme = await contexte
            .Programs.SingleOrDefaultAsync(p => p.Id == identifiant, jeton)
            .ConfigureAwait(false);

        if (programme is null)
        {
            return IssueDEcritureDeProgramme.Introuvable;
        }

        // Le modèle est SORTI de la requête — la politique `modeles_publics` le
        // rend lisible. C'est l'écriture qui ne trouverait aucune ligne, et
        // elle passerait pour un succès. Le refus est donc explicite.
        if (programme.IsTemplate)
        {
            return IssueDEcritureDeProgramme.Modele;
        }

        // Les deux opérations sont EMPRUNTÉES à la création, jamais recopiées.
        // « Quels exercices sont visibles » et « comment poser un contenu » sont
        // une seule connaissance : deux copies auraient divergé le jour où l'une
        // des deux routes change de règle.
        if (!await creation.ExercicesVisiblesAsync(valide, jeton).ConfigureAwait(false))
        {
            return IssueDEcritureDeProgramme.ExerciceInconnu;
        }

        programme.NameFr = valide.Nom;
        programme.DescriptionFr = valide.Description;
        programme.IsActive = valide.Actif;

        // Les séances partent EN ENTIER, et la cascade emporte leurs exercices.
        // Réconcilier ligne à ligne aurait demandé une identité stable côté
        // client, que l'écran ne peut pas garantir quand on réordonne.
        var anciennes = await contexte
            .ProgramDays.Where(d => d.ProgramId == identifiant)
            .ToListAsync(jeton)
            .ConfigureAwait(false);

        contexte.ProgramDays.RemoveRange(anciennes);
        await contexte.SaveChangesAsync(jeton).ConfigureAwait(false);

        await creation.PoserLeContenuAsync(identifiant, valide.Seances, jeton)
            .ConfigureAwait(false);

        return IssueDEcritureDeProgramme.Ecrit;
    }
}

/// <summary>Copie un programme — un modèle, ou l'un des siens.</summary>
/// <remarks>
/// <b>Une copie, jamais une référence</b> — D74. Le programme produit est figé
/// le jour où il est pris : corriger un modèle demain ne modifiera pas ce que
/// quelqu'un s'est construit hier.
/// </remarks>
[GestionnaireDeCasDUsage]
public sealed class CopierUnProgramme(PalierDbContext contexte, IIdentiteDemandeur demandeur)
{
    public async Task<(IssueDEcritureDeProgramme Issue, Guid Identifiant)> ExecuterAsync(
        Guid source,
        CancellationToken jeton
    )
    {
        var origine = await contexte
            .Programs.AsNoTracking()
            .SingleOrDefaultAsync(p => p.Id == source, jeton)
            .ConfigureAwait(false);

        if (origine is null)
        {
            return (IssueDEcritureDeProgramme.Introuvable, Guid.Empty);
        }

        var copie = new TrainingProgram
        {
            NameFr = origine.NameFr,
            DescriptionFr = origine.DescriptionFr,

            // Les notes du modèle SUIVENT la copie. Elles expliquent pourquoi
            // un exercice est absent, et `14-contenu.md` § 2 dit ce que leur
            // perte coûterait : « Un utilisateur qui comprend pourquoi un
            // exercice est absent l'accepte ; sinon il le rajoute et se
            // blesse. »
            NotesFr = origine.NotesFr,
            FrequencyMin = origine.FrequencyMin,
            FrequencyMax = origine.FrequencyMax,
            TargetsConstraint = origine.TargetsConstraint,
            IsActive = true,

            // La copie n'est JAMAIS un modèle, et n'a JAMAIS de slug : le slug
            // est la clé naturelle du catalogue, et son index unique partiel ne
            // porte que sur les modèles.
            IsTemplate = false,
            Slug = null,
            OwnerId = demandeur.Identifiant.GetValueOrDefault(),
        };

        contexte.Programs.Add(copie);
        await contexte.SaveChangesAsync(jeton).ConfigureAwait(false);

        var jours = await contexte
            .ProgramDays.AsNoTracking()
            .Where(d => d.ProgramId == source)
            .OrderBy(d => d.Position)
            .ToListAsync(jeton)
            .ConfigureAwait(false);

        foreach (var jour in jours)
        {
            var nouveau = new ProgramDay
            {
                ProgramId = copie.Id,
                LabelFr = jour.LabelFr,
                Position = jour.Position,
            };

            contexte.ProgramDays.Add(nouveau);
            await contexte.SaveChangesAsync(jeton).ConfigureAwait(false);

            var poses = await contexte
                .ProgramExercises.AsNoTracking()
                .Where(x => x.ProgramDayId == jour.Id)
                .OrderBy(x => x.Position)
                .ToListAsync(jeton)
                .ConfigureAwait(false);

            foreach (var pose in poses)
            {
                contexte.ProgramExercises.Add(
                    new ProgramExercise
                    {
                        ProgramDayId = nouveau.Id,
                        ExerciseId = pose.ExerciseId,
                        Position = pose.Position,
                        TargetSets = pose.TargetSets,
                        TargetRepsMin = pose.TargetRepsMin,
                        TargetRepsMax = pose.TargetRepsMax,
                        TargetRir = pose.TargetRir,
                        RestSeconds = pose.RestSeconds,
                        Note = pose.Note,
                    }
                );
            }

            await contexte.SaveChangesAsync(jeton).ConfigureAwait(false);
        }

        return (IssueDEcritureDeProgramme.Ecrit, copie.Id);
    }
}

/// <summary>Supprime un programme personnel.</summary>
[GestionnaireDeCasDUsage]
public sealed class SupprimerUnProgramme(PalierDbContext contexte)
{
    public async Task<IssueDeSuppressionDeProgramme> ExecuterAsync(
        Guid identifiant,
        CancellationToken jeton
    )
    {
        var programme = await contexte
            .Programs.SingleOrDefaultAsync(p => p.Id == identifiant, jeton)
            .ConfigureAwait(false);

        if (programme is null)
        {
            return IssueDeSuppressionDeProgramme.Introuvable;
        }

        // Même distinction que pour un exercice public : l'appelant vient de le
        // lire, un 404 serait un mensonge.
        if (programme.IsTemplate)
        {
            return IssueDeSuppressionDeProgramme.Modele;
        }

        // La cascade emporte les séances et leurs exercices. Aucune table ne
        // référence un programme en `Restrict` : le supprimer n'entre en
        // conflit avec rien.
        contexte.Programs.Remove(programme);
        await contexte.SaveChangesAsync(jeton).ConfigureAwait(false);

        return IssueDeSuppressionDeProgramme.Supprime;
    }
}
