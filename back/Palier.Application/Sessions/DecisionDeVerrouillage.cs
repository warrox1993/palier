namespace Palier.Application.Sessions;

/// <summary>
/// Ce qu'on sait d'un compte au moment d'une tentative.
/// </summary>
/// <param name="EchecsConsecutifs">Les échecs rapprochés déjà comptés.</param>
/// <param name="DernierEchec">
/// Quand le dernier a eu lieu. C'est ce qui donne sa <b>fenêtre</b> au compteur :
/// sans cette date, un échec d'il y a trois mois compterait encore.
/// </param>
/// <param name="VerrouillagesSubis">Le nombre de verrouillages déjà infligés — l'escalade.</param>
/// <param name="VerrouilleJusqua">L'échéance du verrou en cours, s'il y en a un.</param>
public sealed record EtatDeVerrouillage(
    int EchecsConsecutifs,
    DateTimeOffset? DernierEchec,
    int VerrouillagesSubis,
    DateTimeOffset? VerrouilleJusqua
);

/// <summary>Ce qu'il faut écrire après une tentative.</summary>
public sealed record ResultatDeVerrouillage(
    bool Verrouille,
    DateTimeOffset? Jusqua,
    int EchecsConsecutifs,
    int VerrouillagesSubis
);

/// <summary>
/// Le verrouillage progressif. Logique pure : aucune base, aucune horloge —
/// l'instant est un paramètre.
/// </summary>
/// <remarks>
/// <para>
/// <b>Ce qu'Identity apporte, et les deux choses qui lui manquent.</b>
/// <c>LockoutOptions</c> donne un compteur d'échecs
/// (<c>MaxFailedAccessAttempts</c>) et <b>une</b> durée de verrouillage
/// (<c>DefaultLockoutTimeSpan</c>). <c>docs/09-comptes.md</c> § 1 en demande
/// davantage :
/// </para>
///
/// <list type="number">
///   <item>
///     <b>Une fenêtre de quinze minutes.</b> Le compteur d'Identity est
///     cumulatif et n'est remis à zéro que par une connexion réussie. Quatre
///     fautes de frappe étalées sur trois mois, plus une aujourd'hui, et le
///     compte se verrouille — l'utilisateur ne peut pas comprendre pourquoi.
///   </item>
///   <item>
///     <b>Une escalade.</b> Une durée unique de cinq minutes laisse un
///     attaquant patient reprendre cinq essais toutes les cinq minutes : mille
///     quatre cent quarante essais par jour sur un seul compte, indéfiniment.
///     Cinq, quinze, puis soixante minutes rendent l'acharnement coûteux sans
///     jamais enfermer définitivement un utilisateur légitime.
///   </item>
/// </list>
///
/// <para>
/// <b>Pourquoi ce n'est pas un doublon de la limitation par adresse.</b> Les
/// deux comptent des choses différentes : la limitation protège l'API d'un
/// déluge venu d'une machine, le verrouillage protège <i>un compte</i> d'un
/// acharnement — qui peut très bien venir de mille adresses distinctes.
/// </para>
/// </remarks>
public static class DecisionDeVerrouillage
{
    /// <summary>Cinq tentatives — <c>docs/09-comptes.md</c> § 1.</summary>
    public static int EchecsAvantVerrouillage => 5;

    /// <summary>
    /// Quinze minutes. Au-delà, le compteur repart de zéro : deux fautes de
    /// frappe séparées d'une demi-heure ne sont pas un acharnement.
    /// </summary>
    public static TimeSpan Fenetre => TimeSpan.FromMinutes(15);

    /// <summary>
    /// Cinq minutes, quinze, puis soixante — et soixante pour toutes les
    /// récidives suivantes.
    /// </summary>
    /// <remarks>
    /// Le plafond n'est pas une commodité d'implémentation : sans lui, un
    /// utilisateur qui s'acharne sur son propre compte finirait enfermé pour
    /// des durées qui n'ont plus de sens, et le support serait le seul recours.
    /// </remarks>
    public static IReadOnlyList<TimeSpan> Paliers { get; } =
        [TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15), TimeSpan.FromMinutes(60)];

    /// <summary>Le compte est-il verrouillé à cet instant ?</summary>
    public static bool EstVerrouille(EtatDeVerrouillage etat, DateTimeOffset maintenant)
    {
        ArgumentNullException.ThrowIfNull(etat);

        return etat.VerrouilleJusqua is { } echeance && echeance > maintenant;
    }

    /// <summary>Ce qu'il advient du compte après une tentative ratée.</summary>
    public static ResultatDeVerrouillage ApresUnEchec(
        EtatDeVerrouillage etat,
        DateTimeOffset maintenant
    )
    {
        ArgumentNullException.ThrowIfNull(etat);

        // Hors fenêtre, on repart de zéro — puis on compte celui-ci.
        //
        // La comparaison est LARGE : un échec pile à la limite compte encore.
        // Le bord est éprouvé des deux côtés, sans quoi une inégalité stricte
        // au lieu d'une large passerait inaperçue.
        var dansLaFenetre =
            etat.DernierEchec is { } dernier && maintenant - dernier <= Fenetre;

        var echecs = (dansLaFenetre ? etat.EchecsConsecutifs : 0) + 1;

        if (echecs < EchecsAvantVerrouillage)
        {
            return new ResultatDeVerrouillage(
                Verrouille: false,
                Jusqua: null,
                EchecsConsecutifs: echecs,
                VerrouillagesSubis: etat.VerrouillagesSubis
            );
        }

        // Le palier est celui de la récidive en cours, PLAFONNÉ. Sans le
        // plafond, l'indexation lèverait — et l'exception tomberait sur le
        // chemin de connexion, en production.
        var palier = Paliers[Math.Min(etat.VerrouillagesSubis, Paliers.Count - 1)];

        return new ResultatDeVerrouillage(
            Verrouille: true,
            Jusqua: maintenant + palier,
            // Remis à zéro : sinon le verrouillage suivant tomberait au premier
            // échec après l'expiration du précédent, et non au cinquième.
            EchecsConsecutifs: 0,
            VerrouillagesSubis: etat.VerrouillagesSubis + 1
        );
    }

    /// <summary>Ce qu'il advient du compte après une connexion réussie.</summary>
    /// <remarks>
    /// Tout est effacé, récidives comprises. C'est un compromis assumé : un
    /// attaquant n'obtient jamais de réussite, donc son escalade tient ; le
    /// véritable utilisateur qui retrouve son mot de passe repart à neuf, ce
    /// qui est précisément le but.
    /// </remarks>
    public static ResultatDeVerrouillage ApresUneReussite(EtatDeVerrouillage etat)
    {
        ArgumentNullException.ThrowIfNull(etat);

        return new ResultatDeVerrouillage(
            Verrouille: false,
            Jusqua: null,
            EchecsConsecutifs: 0,
            VerrouillagesSubis: 0
        );
    }
}
