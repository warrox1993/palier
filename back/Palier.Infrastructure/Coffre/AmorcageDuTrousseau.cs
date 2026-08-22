using Microsoft.EntityFrameworkCore;

namespace Palier.Infrastructure.Coffre;

/// <summary>
/// Étapes 6 et 7 de la séquence de démarrage : lire les enveloppes en base, les
/// déballer au coffre, poser le trousseau.
///
/// Il s'exécute AVANT que le serveur accepte une requête. L'accroche au cycle
/// de vie appartient à `Palier.Api` : cette couche ne connaît pas le modèle
/// d'hébergement, et n'a aucune raison de le connaître pour lire deux tables.
///
/// AUCUNE REPRISE SUR ÉCHEC. La tentation serait de réessayer trois fois ;
/// l'API sort en erreur et l'orchestrateur de conteneurs la relance — c'est son
/// travail, il le fait déjà, et mieux qu'une boucle écrite ici. Écrire la
/// reprise dupliquerait une logique existante et ajouterait une branche à
/// éprouver pour rien.
///
/// IL N'ÉCRIT JAMAIS. `palier_app` n'a que <c>select</c> sur
/// <c>cles_de_donnees</c>, et aucune politique d'écriture n'existe : poser une
/// clé est un acte d'exploitation, pas un effet du démarrage.
/// </summary>
public sealed class AmorcageDuTrousseau(
    IDbContextFactory<PalierDbContext> fabrique,
    ClientOkms client,
    PorteurDeTrousseau porteur
)
{
    public async Task<TrousseauDeChiffrement> ChargerAsync(CancellationToken jeton)
    {
        var contexte = fabrique.CreateDbContext();
        await using var _ = contexte.ConfigureAwait(false);

        var enveloppes = await contexte
            .ClesDeDonnees.AsNoTracking()
            .OrderBy(c => c.CreeeLe)
            .ToListAsync(jeton)
            .ConfigureAwait(false);

        if (enveloppes.Count == 0)
        {
            throw new InvalidOperationException(
                "La table `cles_de_donnees` ne porte aucune clé de données. "
                    + "L'API n'en crée jamais : poser la première est un acte "
                    + "d'exploitation — `dotnet run --project back/Palier.Api -- "
                    + "poser-cle-de-donnees`."
            );
        }

        var cles = new Dictionary<Guid, byte[]>();
        foreach (var enveloppe in enveloppes)
        {
            try
            {
                cles[enveloppe.Id] = await client
                    .DeballerAsync(enveloppe.Enveloppe, jeton)
                    .ConfigureAwait(false);
            }
            catch (Exception faute) when (faute is InvalidOperationException or HttpRequestException)
            {
                // L'identifiant, jamais l'enveloppe : elle n'est pas secrète,
                // mais un journal qui la recopie apprend à la relire.
                throw new InvalidOperationException(
                    $"Le coffre n'a pas déballé l'enveloppe « {enveloppe.Id:N} ». "
                        + "Vérifier que la clé de service et la politique IAM sont "
                        + "toujours celles qui l'ont produite.",
                    faute
                );
            }
        }

        // La plus récente chiffre ; toutes déchiffrent. C'est ce qui rend la
        // rotation possible sans réécrire toute la table d'un coup.
        var trousseau = new TrousseauDeChiffrement(cles, enveloppes[^1].Id);
        porteur.Poser(trousseau);
        return trousseau;
    }
}
