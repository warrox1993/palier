using Microsoft.AspNetCore.Identity;
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
/// </remarks>
public sealed class GardienDeVerrouillage(UserManager<Utilisateur> utilisateurs)
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
    public async Task<ResultatDeVerrouillage> EnregistrerUnEchecAsync(
        Utilisateur utilisateur,
        DateTimeOffset maintenant
    )
    {
        ArgumentNullException.ThrowIfNull(utilisateur);

        var decide = DecisionDeVerrouillage.ApresUnEchec(Etat(utilisateur), maintenant);

        utilisateur.AccessFailedCount = decide.EchecsConsecutifs;
        utilisateur.VerrouillagesSubis = decide.VerrouillagesSubis;
        utilisateur.LockoutEnd = decide.Jusqua;

        // La date du dernier échec est écrite À CHAQUE échec, y compris celui
        // qui verrouille. Sans elle, la fenêtre n'aurait pas de point de départ
        // et le compteur redeviendrait cumulatif — le défaut d'Identity qu'on
        // vient de corriger.
        utilisateur.DernierEchecLe = maintenant;

        await EcrireAsync(utilisateur).ConfigureAwait(false);
        return decide;
    }

    /// <summary>Efface tout après une connexion réussie.</summary>
    public async Task EnregistrerUneReussiteAsync(Utilisateur utilisateur)
    {
        ArgumentNullException.ThrowIfNull(utilisateur);

        // Rien à écrire si rien n'a bougé : une écriture par connexion réussie
        // coûterait un aller-retour à chaque ouverture de session, pour rien.
        if (
            utilisateur.AccessFailedCount == 0
            && utilisateur.VerrouillagesSubis == 0
            && utilisateur.LockoutEnd is null
            && utilisateur.DernierEchecLe is null
        )
        {
            return;
        }

        var decide = DecisionDeVerrouillage.ApresUneReussite(Etat(utilisateur));

        utilisateur.AccessFailedCount = decide.EchecsConsecutifs;
        utilisateur.VerrouillagesSubis = decide.VerrouillagesSubis;
        utilisateur.LockoutEnd = decide.Jusqua;
        utilisateur.DernierEchecLe = null;

        await EcrireAsync(utilisateur).ConfigureAwait(false);
    }

    private async Task EcrireAsync(Utilisateur utilisateur)
    {
        var ecrit = await utilisateurs.UpdateAsync(utilisateur).ConfigureAwait(false);

        if (!ecrit.Succeeded)
        {
            // Quatrième question du franchissement : un contrôle qui n'a pas
            // pris effet doit CRIER. Un verrouillage qu'on croit posé et qui ne
            // l'est pas laisse la porte ouverte, en silence — et le compteur
            // repartirait de zéro à chaque tentative.
            throw new InvalidOperationException(
                "Le verrouillage n'a pas pu être enregistré : "
                    + string.Join(", ", ecrit.Errors.Select(e => e.Code))
                    + ". Le compte n'est PAS protégé et le compteur d'échecs ne progresse pas."
            );
        }
    }
}
