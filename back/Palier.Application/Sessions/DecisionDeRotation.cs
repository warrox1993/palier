namespace Palier.Application.Sessions;

/// <summary>Les six issues possibles d'une présentation de jeton.</summary>
public enum IssueDeRotation
{
    /// <summary>Le jeton est valide : il est consommé, un nouveau prend sa place.</summary>
    Acceptee,

    /// <summary>
    /// Un jeton déjà consommé, représenté dans la fenêtre de grâce. C'est un
    /// utilisateur légitime dont deux requêtes se sont croisées, pas un voleur.
    /// </summary>
    RejeuDansLaGrace,

    /// <summary>
    /// Un jeton déjà consommé, représenté hors de la grâce. Deux porteurs
    /// détiennent la même chaîne : toute la famille tombe.
    /// </summary>
    ReemploiDetecte,

    /// <summary>Aucune session ne porte cette empreinte.</summary>
    Inconnue,

    Expiree,

    Revoquee,
}

/// <summary>
/// L'état d'une session, réduit à ce que la décision consomme. Aucune empreinte,
/// aucun jeton : cette logique est pure et ne voit jamais de secret.
/// </summary>
public sealed record EtatDeSession(
    Guid Id,
    Guid FamilleId,
    DateTimeOffset ExpireLe,
    DateTimeOffset? ConsommeeLe,
    DateTimeOffset? RevoqueeLe,
    Guid? RemplaceePar
);

/// <summary>
/// Ce que le magasin doit faire. <see cref="SessionARendre" /> n'est renseignée
/// que dans la fenêtre de grâce : c'est la session suivante de la chaîne, celle
/// que la première requête avait déjà obtenue.
/// </summary>
public sealed record ResultatDeRotation(
    IssueDeRotation Issue,
    Guid? SessionARendre,
    bool RevoquerLaFamille
);

/// <summary>Les durées de vie, en un seul endroit.</summary>
public static class ParametresDeSession
{
    /// <summary>
    /// Quinze minutes. OWASP place les jetons d'accès portant des données de
    /// santé entre 5 et 15 minutes.
    /// </summary>
    public static TimeSpan DureeDuJetonDAcces => TimeSpan.FromMinutes(15);

    /// <summary>
    /// Quatorze jours. OWASP borne les jetons de rafraîchissement à 7-30 jours.
    /// </summary>
    public static TimeSpan DureeDuRafraichissement => TimeSpan.FromDays(14);

    /// <summary>
    /// Trente secondes — le défaut d'Okta, réglable de 0 à 60 chez lui.
    /// </summary>
    /// <remarks>
    /// Assez court pour qu'un jeton volé ne serve pas : un attaquant qui rejoue
    /// à la seconde près est dans la course, pas dans une exploitation. Assez
    /// long pour couvrir un aller-retour réseau dégradé, un second onglet ou une
    /// reprise de connexion — les cas qui, sans cette fenêtre, déconnectent un
    /// utilisateur qui n'a rien fait.
    /// </remarks>
    public static TimeSpan FenetreDeGrace => TimeSpan.FromSeconds(30);
}

/// <summary>
/// Décide ce qu'il advient d'un jeton présenté. Logique pure : aucune base,
/// aucun réseau, aucune horloge — l'instant est un paramètre.
/// </summary>
public static class DecisionDeRotation
{
    /// <summary>
    /// L'ordre des gardes n'est pas indifférent. La révocation prime sur la
    /// grâce — une famille révoquée ne se ranime pas — et l'expiration prime
    /// aussi, sans quoi un jeton périmé rejoué dans les trente secondes
    /// obtiendrait un successeur.
    /// </summary>
    public static ResultatDeRotation Decider(EtatDeSession? session, DateTimeOffset maintenant)
    {
        if (session is null)
        {
            return new ResultatDeRotation(IssueDeRotation.Inconnue, null, RevoquerLaFamille: false);
        }

        if (session.RevoqueeLe is not null)
        {
            return new ResultatDeRotation(IssueDeRotation.Revoquee, null, RevoquerLaFamille: false);
        }

        if (session.ExpireLe < maintenant)
        {
            return new ResultatDeRotation(IssueDeRotation.Expiree, null, RevoquerLaFamille: false);
        }

        if (session.ConsommeeLe is { } consommeeLe)
        {
            // Le jeton a déjà servi. Deux lectures possibles, et une seule
            // seconde les sépare : une requête qui s'est croisée avec la
            // précédente, ou un porteur qui n'aurait pas dû l'avoir.
            var dansLaGrace = maintenant - consommeeLe <= ParametresDeSession.FenetreDeGrace;

            return dansLaGrace && session.RemplaceePar is { } suivante
                ? new ResultatDeRotation(
                    IssueDeRotation.RejeuDansLaGrace,
                    suivante,
                    RevoquerLaFamille: false
                )
                : new ResultatDeRotation(
                    IssueDeRotation.ReemploiDetecte,
                    null,
                    RevoquerLaFamille: true
                );
        }

        return new ResultatDeRotation(IssueDeRotation.Acceptee, null, RevoquerLaFamille: false);
    }
}
