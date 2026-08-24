using Microsoft.EntityFrameworkCore;
using Palier.Application.Entrainement;
using Palier.Application.Pipeline;
using Palier.Infrastructure.Entites;

namespace Palier.Infrastructure.Entrainement;

// Les contraintes déclarées — `docs/05-entrainement.md` § 4.
//
// POSSÉDÉE DIRECTE : `user_constraints` porte son `owner_id`, et sa politique
// est la forme la plus simple du schéma. Rien de subtil ici, et c'est
// justement pourquoi il faut l'éprouver comme les autres : une forme simple
// est une forme qu'on relit sans attention.
//
// L'ADAPTATION PAR CONTRAINTE NE FILTRE RIEN, ET C'EST LA CONFORMITÉ QUI LE
// DIT. Le § 4 emploie le mot « filtrent » ; `00-produit.md` tranche ce qu'il
// peut vouloir dire — « voici ta valeur, voici la référence, voici l'écart »
// est une information, tandis que décider à la place de l'utilisateur « place
// l'éditeur en conseiller, ce qui est réglementé ». Et `01-conformite.md` § 2
// pose la formule canonique : « un chiffre, une référence, un écart. JAMAIS
// une action. »
//
// Retirer un exercice du catalogue EST une action. Le catalogue sort donc
// entier, MARQUÉ — voir `MarquageDeContrainte` — et c'est la SÉVÉRITÉ déclarée
// ici qui pilote la présentation, à l'écran. D63.
//
// LA SÉVÉRITÉ EST LE RÉGLAGE DE L'UTILISATEUR, et il est par contrainte : une
// épaule strictement contre-indiquée et un genou légèrement sensible
// n'appellent pas le même traitement, et un réglage global aurait forcé un
// seul comportement pour les deux.

/// <summary>Lit les contraintes déclarées par l'appelant.</summary>
[GestionnaireDeCasDUsage]
public sealed class ListerLesContraintes(PalierDbContext contexte)
{
    public async Task<IReadOnlyList<ContrainteRendue>> ExecuterAsync(CancellationToken jeton)
    {
        var lignes = await contexte
            .UserConstraints.AsNoTracking()
            .OrderBy(c => c.Region)
            .ToListAsync(jeton)
            .ConfigureAwait(false);

        return [.. lignes.Select(c => new ContrainteRendue(c.Region, c.Severity, c.Note, c.DeclaredAt))];
    }
}

/// <summary>
/// Remplace la liste COMPLÈTE des contraintes de l'appelant.
/// </summary>
/// <remarks>
/// <b>Un remplacement, et non un ajout.</b> Déclarer ses contraintes est un
/// ÉTAT, pas un événement : renvoyer deux fois la même liste doit produire le
/// même résultat, et retirer une contrainte guérie doit se faire en la
/// retirant de la liste — pas en devinant qu'un <c>DELETE</c> existe quelque
/// part.
/// </remarks>
[GestionnaireDeCasDUsage]
public sealed class RemplacerLesContraintes(PalierDbContext contexte, IIdentiteDemandeur demandeur)
{
    public async Task<IReadOnlyList<ContrainteRendue>> ExecuterAsync(
        DeclarationDeContraintes demande,
        TimeProvider horloge,
        CancellationToken jeton
    )
    {
        ArgumentNullException.ThrowIfNull(demande);
        ArgumentNullException.ThrowIfNull(horloge);

        // Une région déclarée deux fois est dédupliquée sur la RÉGION : la
        // dernière sévérité donnée l'emporte. Refuser le doublon obligerait le
        // client à dédupliquer avant d'envoyer, pour zéro bénéfice.
        var voulues = new Dictionary<string, DeclarationDUneContrainte>(StringComparer.Ordinal);

        foreach (var declaree in demande.RegionsOuVide)
        {
            Contraintes.Lire(declaree.Region, out var region);
            voulues[Contraintes.EnBase(region!.Value)] = declaree;
        }

        // Aucun filtre sur `owner_id` : RLS mord, donc ceci ne rend que SES
        // contraintes. C'est ce qui rend le remplacement sûr — sans RLS, il
        // effacerait celles de tout le monde.
        var existantes = await contexte
            .UserConstraints.ToListAsync(jeton)
            .ConfigureAwait(false);

        // Les retirées d'abord. Un `DELETE` puis `INSERT` de la liste entière
        // serait plus court à écrire, mais il changerait `declared_at` sur des
        // contraintes que l'utilisateur n'a pas touchées — et cette date dira
        // un jour depuis quand une contrainte dure.
        foreach (var partie in existantes.Where(c => !voulues.ContainsKey(c.Region)))
        {
            contexte.UserConstraints.Remove(partie);
        }

        foreach (var (region, declaree) in voulues)
        {
            var deja = existantes.Find(c => string.Equals(c.Region, region, StringComparison.Ordinal));

            if (deja is null)
            {
                contexte.UserConstraints.Add(
                    new UserConstraint
                    {
                        // L'identité vient du demandeur, jamais de la requête —
                        // D36, et le `with check` de la politique le revérifie.
                        OwnerId = demandeur.Identifiant.GetValueOrDefault(),
                        Region = region,
                        Severity = Severites.EnBase(declaree.SeveriteLue),
                        Note = declaree.NoteNettoyee,
                        DeclaredAt = horloge.GetUtcNow(),
                    }
                );
            }
            else
            {
                // La SÉVÉRITÉ et la NOTE se mettent à jour, la DATE non : une
                // contrainte qui passe de `modere` à `strict` reste la même
                // contrainte, déclarée le même jour.
                deja.Severity = Severites.EnBase(declaree.SeveriteLue);
                deja.Note = declaree.NoteNettoyee;
            }
        }

        await contexte.SaveChangesAsync(jeton).ConfigureAwait(false);

        return
        [
            .. (await contexte.UserConstraints.AsNoTracking().OrderBy(c => c.Region).ToListAsync(jeton).ConfigureAwait(false))
                .Select(c => new ContrainteRendue(c.Region, c.Severity, c.Note, c.DeclaredAt)),
        ];
    }
}

/// <summary>Retire UNE contrainte. Rend faux si elle n'était pas déclarée.</summary>
[GestionnaireDeCasDUsage]
public sealed class RetirerUneContrainte(PalierDbContext contexte)
{
    public async Task<bool> ExecuterAsync(Contrainte region, CancellationToken jeton)
    {
        var valeur = Contraintes.EnBase(region);

        var declaree = await contexte
            .UserConstraints.FirstOrDefaultAsync(c => c.Region == valeur, jeton)
            .ConfigureAwait(false);

        if (declaree is null)
        {
            return false;
        }

        contexte.UserConstraints.Remove(declaree);
        await contexte.SaveChangesAsync(jeton).ConfigureAwait(false);
        return true;
    }
}
