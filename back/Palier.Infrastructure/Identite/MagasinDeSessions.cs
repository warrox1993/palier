using Microsoft.EntityFrameworkCore;

namespace Palier.Infrastructure.Identite;

/// <summary>Une session active, telle qu'elle s'affiche dans les réglages.</summary>
public sealed record SessionActive(
    Guid Id,
    string? Appareil,
    DateTimeOffset OuverteLe,
    DateTimeOffset DerniereActivite
);

/// <summary>
/// Le magasin des sessions de rafraîchissement.
/// </summary>
/// <remarks>
/// <b>C'est le SEUL type autorisé à prendre <see cref="PalierAuthDbContext" />.</b>
/// <c>ArchitectureTests</c> le nomme dans ses exemptions, avec son motif, et une
/// épreuve refuse le dépôt le jour où ce type disparaîtrait ou changerait de nom
/// — sans quoi l'exemption survivrait à sa cible et couvrirait en silence tout
/// ce qui reprendrait ce nom.
///
/// <para>
/// Il ne passe pas par <c>ExecuteurDeCasDUsage</c>, et c'est le cœur du
/// problème que D38 laissait à concevoir : le rafraîchissement d'un jeton a lieu
/// <b>avant</b> qu'une identité existe, or le pipeline refuse sans identité. Le
/// chemin passe donc par un rôle dédié, jamais par une pose de l'identité
/// d'autrui.
/// </para>
/// </remarks>
public sealed class MagasinDeSessions(PalierAuthDbContext contexte)
{
    /// <summary>Les sessions vivantes d'un utilisateur, pour l'écran des réglages.</summary>
    public async Task<IReadOnlyList<SessionActive>> ListerAsync(
        Guid utilisateur,
        DateTimeOffset maintenant,
        CancellationToken jeton = default
    ) =>
        await contexte
            .Sessions.Where(s =>
                s.OwnerId == utilisateur
                && s.RevokedAt == null
                && s.ConsumedAt == null
                && s.ExpiresAt > maintenant
            )
            .OrderByDescending(s => s.LastSeenAt)
            .Select(s => new SessionActive(s.Id, s.Device, s.CreatedAt, s.LastSeenAt))
            .ToListAsync(jeton)
            .ConfigureAwait(false);
}
