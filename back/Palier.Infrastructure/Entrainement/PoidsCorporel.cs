using Microsoft.EntityFrameworkCore;
using Palier.Application.Entrainement;
using Palier.Application.Pipeline;
using Palier.Domain.Grandeurs;
using Palier.Domain.Securite;
using Palier.Infrastructure.Entites;

namespace Palier.Infrastructure.Entrainement;

// Le poids corporel.
//
// DEUX MESURES LE MÊME JOUR SONT UN REMPLACEMENT, PAS UN DOUBLON.
// `body_weight` porte `unique (owner_id, measured_on)`. Sans traitement, la
// seconde saisie du jour remonterait une violation de contrainte — donc un
// 500 — à quelqu'un qui corrige simplement une faute de frappe. Le point
// d'entrée est donc IDEMPOTENT par jour, et l'épreuve le provoque.
//
// LE CONSTAT DE SÉCURITÉ N'EMPÊCHE RIEN. `01-conformite.md` § 5 impose « un
// message d'orientation vers un professionnel, jamais de renforcement de la
// restriction ». Un produit qui refuserait la saisie se ferait contourner en
// cessant de saisir, ce qui supprime le signal qu'on cherchait à lire.

/// <summary>Enregistre une pesée. Deux fois le même jour est un remplacement.</summary>
[GestionnaireDeCasDUsage]
public sealed class EnregistrerLePoids(PalierDbContext contexte, IIdentiteDemandeur demandeur)
{
    public async Task<PoidsRendu> ExecuterAsync(
        MesureDePoids demande,
        DateOnly aujourdHui,
        CancellationToken jeton
    )
    {
        ArgumentNullException.ThrowIfNull(demande);

        var jour = demande.Jour ?? aujourdHui;

        // Aucun filtre sur `owner_id` : RLS mord, donc cette recherche ne peut
        // trouver que SA pesée du jour. C'est ce qui rend l'idempotence sûre —
        // sans RLS, elle écraserait la pesée d'un autre.
        var existante = await contexte
            .BodyWeights.FirstOrDefaultAsync(m => m.MeasuredOn == jour, jeton)
            .ConfigureAwait(false);

        if (existante is null)
        {
            existante = new BodyWeight
            {
                // L'identité vient du demandeur, jamais de la requête — D36.
                // Le moteur le vérifie une seconde fois par le `with check`.
                OwnerId = demandeur.Identifiant.GetValueOrDefault(),
                MeasuredOn = jour,
                WeightKg = demande.PoidsKg,
            };
            contexte.BodyWeights.Add(existante);
        }
        else
        {
            existante.WeightKg = demande.PoidsKg;
        }

        await contexte.SaveChangesAsync(jeton).ConfigureAwait(false);
        return new PoidsRendu(existante.MeasuredOn, existante.WeightKg);
    }
}

/// <summary>Lit la série de pesées sur une fenêtre, et le constat qui en découle.</summary>
[GestionnaireDeCasDUsage]
public sealed class ListerLePoids(PalierDbContext contexte)
{
    public async Task<SerieDePoids> ExecuterAsync(
        int? fenetreEnJours,
        DateOnly aujourdHui,
        CancellationToken jeton
    )
    {
        var depuis = aujourdHui.AddDays(-SerieDePoids.BornerLaFenetre(fenetreEnJours));

        var mesures = await contexte
            .BodyWeights.AsNoTracking()
            .Where(m => m.MeasuredOn >= depuis)
            .OrderBy(m => m.MeasuredOn)
            .ToListAsync(jeton)
            .ConfigureAwait(false);

        return new SerieDePoids([.. mesures.Select(m => new PoidsRendu(m.MeasuredOn, m.WeightKg))], Constater(mesures, aujourdHui));
    }

    /// <summary>
    /// Le constat, DÉLÉGUÉ au domaine.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>La réduction hebdomadaire n'est pas facultative.</b>
    /// <c>PerteDePoidsRapide.EstDetectee</c> compare des valeurs CONSÉCUTIVES
    /// en supposant qu'une semaine les sépare. Lui passer des pesées
    /// quotidiennes lui ferait mesurer des jours en croyant mesurer des
    /// semaines — et la détection deviendrait muette exactement quand elle
    /// devrait parler, puisque la variation d'un jour à l'autre reste sous le
    /// seuil de 1 %.
    /// </para>
    ///
    /// <para>
    /// Le seuil, la fenêtre et la moyenne vivent tous dans
    /// <c>Palier.Domain</c>, couvert à 100 %. Ce gestionnaire LIT et PASSE ; il
    /// ne calcule rien, et ne rédige aucune phrase.
    /// </para>
    /// </remarks>
    private static string? Constater(List<BodyWeight> mesures, DateOnly aujourdHui)
    {
        var pesees = mesures
            .Select(m => new Pesee(m.MeasuredOn, Masse.DepuisKilogrammes(m.WeightKg)))
            .ToArray();

        var hebdomadaires = PerteDePoidsRapide.MoyennesHebdomadaires(pesees, aujourdHui);
        return PerteDePoidsRapide.EstDetectee(hebdomadaires) ? SerieDePoids.PerteRapide : null;
    }
}
