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

        // UN UPSERT ATOMIQUE, et non un lire-puis-décider.
        //
        // La version précédente cherchait la pesée du jour, puis insérait ou
        // mettait à jour selon ce qu'elle avait trouvé. Entre les deux, une
        // fenêtre : le pipeline ouvre sa transaction en READ COMMITTED — le
        // défaut de PostgreSQL — donc deux requêtes simultanées ne voyaient
        // pas l'insertion l'une de l'autre. Toutes deux inséraient, la seconde
        // heurtait `ux_body_weight_owner_id_measured_on` et remontait un 23505
        // en 500.
        //
        // Le déclencheur n'a rien d'exotique : un double appui sur
        // « Enregistrer » quand le réseau traîne, un rejeu automatique après
        // un délai perçu, deux onglets ouverts. Et le commentaire en tête de
        // ce fichier promettait exactement l'inverse — l'idempotence tenait en
        // séquentiel et cédait en concurrence.
        //
        // `on conflict ... do update` supprime la fenêtre : le moteur tranche
        // lui-même, en une instruction. Les valeurs sont LIÉES, jamais
        // concaténées — `CLAUDE.md` § 4.
        //
        // L'identité vient du demandeur, jamais de la requête — D36 — et le
        // `with check` de la politique la revérifie sur l'insertion comme sur
        // la mise à jour.
        var proprietaire = demandeur.Identifiant.GetValueOrDefault();

        await contexte
            .Database.ExecuteSqlInterpolatedAsync(
                $"""
                insert into public.body_weight (owner_id, measured_on, weight_kg)
                values ({proprietaire}, {jour}, {demande.PoidsKg})
                on conflict (owner_id, measured_on)
                do update set weight_kg = excluded.weight_kg
                """,
                jeton
            )
            .ConfigureAwait(false);

        return new PoidsRendu(jour, demande.PoidsKg);
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
        // DEUX FENÊTRES, ET C'EST LE FOND DE LA CORRECTION.
        //
        // Celle d'AFFICHAGE est demandée par le client : elle sert la courbe,
        // et il est légitime de vouloir « la dernière semaine ».
        //
        // Celle d'ANALYSE ne se négocie pas. Le domaine dit de combien de jours
        // il a besoin, et on lit AU MOINS cela. Sans cette distinction, un
        // `?jours=7` tronquait la série avant le constat : `MoyennesHebdo­madaires`
        // rendait deux fenêtres au lieu de quatre, `EstDetectee` sortait sur son
        // premier test, et la détection retournait `false` — indiscernable de
        // « rien à signaler » — chez quelqu'un qui perd deux pour cent par
        // semaine. Un garde-fou de l'article 9 éteint par un paramètre de
        // requête, ce que `CLAUDE.md` § 4 interdit en toutes lettres : « toute
        // donnée qui vient de l'extérieur est hostile ».
        var fenetreAffichage = SerieDePoids.BornerLaFenetre(fenetreEnJours);
        var debutAffichage = aujourdHui.AddDays(-fenetreAffichage);
        var debutAnalyse = aujourdHui.AddDays(
            -Math.Max(fenetreAffichage, PerteDePoidsRapide.JoursNecessaires)
        );

        // UNE seule requête, sur la plus large des deux. Deux allers-retours
        // coûteraient une seconde latence pour la même information, et la
        // fenêtre d'affichage est toujours incluse dans celle d'analyse.
        var mesures = await contexte
            .BodyWeights.AsNoTracking()
            .Where(m => m.MeasuredOn >= debutAnalyse)
            .OrderBy(m => m.MeasuredOn)
            .ToListAsync(jeton)
            .ConfigureAwait(false);

        return new SerieDePoids(
            [
                .. mesures
                    .Where(m => m.MeasuredOn >= debutAffichage)
                    .Select(m => new PoidsRendu(m.MeasuredOn, m.WeightKg)),
            ],
            // TOUTES les mesures lues, pas seulement celles qu'on affiche.
            Constater(mesures, aujourdHui)
        );
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
