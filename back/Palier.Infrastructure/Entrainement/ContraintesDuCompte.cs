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
// CE QUE CE LOT NE FAIT PAS : filtrer le catalogue. Le § 4 décrit un filtrage
// automatique par intersection avec `exercises.contraindicated_for`, mais il
// laisse ouvert ce qu'on fait d'un exercice contre-indiqué — l'exclure, ou
// l'afficher marqué. C'est une décision de produit VISIBLE par l'utilisateur,
// donc `CLAUDE.md` § 6 s'applique : elle appartient au porteur. Le lot 5
// stocke et expose ; une épreuve inversée rougira le jour où le filtrage
// arrivera, pour forcer à venir ici et à décider plutôt qu'à laisser un
// comportement s'installer.

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

        return [.. lignes.Select(c => new ContrainteRendue(c.Region, c.DeclaredAt))];
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

        var voulues = demande
            .RegionsOuVide.Select(r =>
            {
                Contraintes.Lire(r, out var lue);
                return Contraintes.EnBase(lue!.Value);
            })
            .ToHashSet(StringComparer.Ordinal);

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
        foreach (var partie in existantes.Where(c => !voulues.Contains(c.Region)))
        {
            contexte.UserConstraints.Remove(partie);
        }

        var deja = existantes.Select(c => c.Region).ToHashSet(StringComparer.Ordinal);

        foreach (var neuve in voulues.Where(r => !deja.Contains(r)))
        {
            contexte.UserConstraints.Add(
                new UserConstraint
                {
                    // L'identité vient du demandeur, jamais de la requête —
                    // D36, et le `with check` de la politique le revérifie.
                    OwnerId = demandeur.Identifiant.GetValueOrDefault(),
                    Region = neuve,
                    DeclaredAt = horloge.GetUtcNow(),
                }
            );
        }

        await contexte.SaveChangesAsync(jeton).ConfigureAwait(false);

        return
        [
            .. (await contexte.UserConstraints.AsNoTracking().OrderBy(c => c.Region).ToListAsync(jeton).ConfigureAwait(false))
                .Select(c => new ContrainteRendue(c.Region, c.DeclaredAt)),
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
