using Palier.Application.Sessions;

namespace Palier.Infrastructure.Identite;

/// <summary>
/// Applique <see cref="DecisionDeVerrouillage" /> au compte : il lit, il
/// appelle, il écrit.
/// </summary>
/// <remarks>
/// <para>
/// <b>La décision n'est pas prise ici.</b> Elle vit dans
/// <c>Palier.Application</c>, pure et couverte à 100 % — c'est ce qui permet
/// d'éprouver « quinze minutes plus tard » et « la quatrième récidive » sans
/// attendre ni monter une base.
/// </para>
///
/// <para>
/// <b>Où l'état est rangé.</b> Deux champs viennent d'Identity —
/// <c>AccessFailedCount</c> pour les échecs, <c>LockoutEnd</c> pour l'échéance.
/// Deux sont ajoutés — <c>DernierEchecLe</c>, qui donne sa fenêtre au compteur,
/// et <c>VerrouillagesSubis</c>, qui porte l'escalade. Réutiliser les deux
/// premiers plutôt que d'en créer des jumeaux évite deux vérités sur le même
/// fait : le jour où le support lit <c>LockoutEnd</c> dans la base, il lit la
/// bonne valeur.
/// </para>
///
/// <para>
/// <b>Les DEUX écritures passent par le même chemin sérialisé</b> —
/// <see cref="MagasinDeSessions.AppliquerLeVerrouillageAsync" />, qui verrouille
/// la ligne du compte et RELIT son état avant d'appeler la décision. Le lire ici
/// puis l'écrire là faisait de N tentatives simultanées une seule tentative
/// comptée. Et faire emprunter à la réussite un chemin d'écriture différent de
/// celui de l'échec laisserait leur ordre au hasard : deux écritures sur les
/// mêmes quatre colonnes doivent se ranger derrière le même verrou.
/// </para>
/// </remarks>
public sealed class GardienDeVerrouillage(MagasinDeSessions magasin)
{
    /// <summary>L'état du compte, tel que la décision l'attend.</summary>
    public static EtatDeVerrouillage Etat(Utilisateur utilisateur)
    {
        ArgumentNullException.ThrowIfNull(utilisateur);

        return new EtatDeVerrouillage(
            utilisateur.AccessFailedCount,
            utilisateur.DernierEchecLe,
            utilisateur.VerrouillagesSubis,
            utilisateur.LockoutEnd
        );
    }

    /// <summary>Le compte est-il verrouillé à cet instant ?</summary>
    public static bool EstVerrouille(Utilisateur utilisateur, DateTimeOffset maintenant) =>
        DecisionDeVerrouillage.EstVerrouille(Etat(utilisateur), maintenant);

    /// <summary>Enregistre une tentative ratée et rend ce qui a été décidé.</summary>
    /// <remarks>
    /// Seul l'IDENTIFIANT de <paramref name="utilisateur" /> sert : la décision
    /// s'applique à l'état relu sous verrou, et l'exemplaire reçu — chargé au
    /// début de la connexion — n'est ni lu ni modifié.
    /// </remarks>
    public async Task<ResultatDeVerrouillage> EnregistrerUnEchecAsync(
        Utilisateur utilisateur,
        DateTimeOffset maintenant
    )
    {
        ArgumentNullException.ThrowIfNull(utilisateur);

        // La date du dernier échec est écrite À CHAQUE échec, y compris celui
        // qui verrouille. Sans elle, la fenêtre n'aurait pas de point de départ
        // et le compteur redeviendrait cumulatif — le défaut d'Identity qu'on
        // vient de corriger.
        return await EcrireAsync(
                utilisateur.Id,
                relu => DecisionDeVerrouillage.ApresUnEchec(Etat(relu), maintenant),
                dernierEchec: maintenant
            )
            .ConfigureAwait(false);
    }

    /// <summary>Efface tout après une connexion réussie.</summary>
    /// <remarks>
    /// Elle emprunte le même chemin verrouillé que l'échec, et décide donc sur
    /// l'état RELU. Des échecs peuvent avoir verrouillé le compte depuis que la
    /// connexion a chargé son exemplaire : ils sont alors effacés, ce que
    /// <see cref="DecisionDeVerrouillage.ApresUneReussite" /> assume en toutes
    /// lettres. Ce qui compte est que l'ordre soit décidé par le verrou, et non
    /// par le hasard d'un jeton de concurrence.
    /// </remarks>
    public async Task EnregistrerUneReussiteAsync(Utilisateur utilisateur)
    {
        ArgumentNullException.ThrowIfNull(utilisateur);

        // Rien à écrire si rien n'a bougé : une écriture par connexion réussie
        // coûterait un aller-retour à chaque ouverture de session, pour rien.
        // Le raccourci lit l'exemplaire déjà chargé, et c'est suffisant — un
        // compte propre à la lecture n'a RIEN à effacer, et ouvrir une
        // transaction pour n'écrire aucun changement ne protégerait personne.
        if (
            utilisateur.AccessFailedCount == 0
            && utilisateur.VerrouillagesSubis == 0
            && utilisateur.LockoutEnd is null
            && utilisateur.DernierEchecLe is null
        )
        {
            return;
        }

        await EcrireAsync(
                utilisateur.Id,
                relu => DecisionDeVerrouillage.ApresUneReussite(Etat(relu)),
                dernierEchec: null
            )
            .ConfigureAwait(false);
    }

    private async Task<ResultatDeVerrouillage> EcrireAsync(
        Guid compte,
        Func<Utilisateur, ResultatDeVerrouillage> decider,
        DateTimeOffset? dernierEchec
    )
    {
        var ecrit = await magasin
            .AppliquerLeVerrouillageAsync(compte, decider, dernierEchec)
            .ConfigureAwait(false);

        // Quatrième question du franchissement : un contrôle qui n'a pas pris
        // effet doit CRIER. Un verrouillage qu'on croit posé et qui ne l'est pas
        // laisse la porte ouverte, en silence — et le compteur repartirait de
        // zéro à chaque tentative.
        return ecrit
            ?? throw new InvalidOperationException(
                "Le verrouillage n'a pas pu être enregistré : le compte a disparu entre la "
                    + "lecture et l'écriture. Le compte n'est PAS protégé et le compteur "
                    + "d'échecs ne progresse pas."
            );
    }
}
