using Microsoft.EntityFrameworkCore;
using Palier.Application.Sessions;

namespace Palier.Infrastructure.Identite;

/// <summary>Une session active, telle qu'elle s'affiche dans les réglages.</summary>
public sealed record SessionActive(
    Guid Id,
    string? Appareil,
    DateTimeOffset OuverteLe,
    DateTimeOffset DerniereActivite
);

/// <summary>Ce qu'une ouverture de session rend à l'appelant.</summary>
public sealed record OuvertureDeSession(string Jeton, Guid Famille);

/// <summary>
/// Ce qu'une rotation rend. <see cref="Jeton" /> est nul dès que l'issue n'est
/// pas exploitable — inconnue, expirée, révoquée, réemploi.
/// </summary>
public sealed record RotationDeSession(IssueDeRotation Issue, string? Jeton, Guid? Famille);

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
/// Il ne passe pas par <c>ExecuteurDeCasDUsage</c>, et c'est le cœur du problème
/// que D38 laissait à concevoir : le rafraîchissement d'un jeton a lieu
/// <b>avant</b> qu'une identité existe, or le pipeline refuse sans identité. Le
/// chemin passe donc par un rôle dédié, jamais par une pose de l'identité
/// d'autrui.
/// </para>
///
/// <para>
/// <b>La décision de rotation n'est pas prise ici.</b> Elle vit dans
/// <see cref="DecisionDeRotation" />, pure et couverte à 100 %. Ce type
/// l'applique : il lit, il appelle, il écrit.
/// </para>
/// </remarks>
public sealed class MagasinDeSessions(PalierAuthDbContext contexte)
{
    /// <summary>Ouvre une session neuve, dans une famille neuve.</summary>
    public async Task<OuvertureDeSession> OuvrirAsync(
        Guid utilisateur,
        string? appareil,
        DateTimeOffset maintenant,
        CancellationToken jeton = default
    )
    {
        var famille = Guid.NewGuid();
        var valeur = await InscrireAsync(utilisateur, famille, appareil, maintenant, jeton)
            .ConfigureAwait(false);
        return new OuvertureDeSession(valeur, famille);
    }

    /// <summary>
    /// Fait tourner un jeton, ou refuse. La transaction et le verrou de ligne
    /// sont ce qui empêche deux requêtes concurrentes de consommer la même
    /// session : sans eux, les deux la verraient valide et la seconde passerait
    /// pour un vol.
    /// </summary>
    public async Task<RotationDeSession> FaireTournerAsync(
        string jetonPresente,
        string? appareil,
        DateTimeOffset maintenant,
        CancellationToken jeton = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jetonPresente);
        var empreinte = HachageDeJeton.Calculer(jetonPresente);

        var strategie = contexte.Database.CreateExecutionStrategy();
        return await strategie
            .ExecuteAsync(async () =>
            {
                var transaction = await contexte
                    .Database.BeginTransactionAsync(jeton)
                    .ConfigureAwait(false);
                await using (transaction.ConfigureAwait(false))
                {
                    // AsNoTracking n'est pas une optimisation ici : sans elle, une
                    // entité déjà chargée dans ce contexte répondrait À LA PLACE de
                    // la base, avec son ancien RevokedAt. Mesuré — une session
                    // révoquée par ExecuteUpdate ressortait « Acceptee ».
                    var session = await contexte
                        .Sessions.FromSql(
                            $"""
                            select * from public.sessions_refresh
                             where token_hash = {empreinte}
                             for update
                            """
                        )
                        .AsNoTracking()
                        .FirstOrDefaultAsync(jeton)
                        .ConfigureAwait(false);

                    var etat =
                        session is null
                            ? null
                            : new EtatDeSession(
                                session.Id,
                                session.FamilyId,
                                session.ExpiresAt,
                                session.ConsumedAt,
                                session.RevokedAt,
                                session.ReplacedById
                            );

                    var decision = DecisionDeRotation.Decider(etat, maintenant);

                    var resultat = await AppliquerAsync(
                            decision,
                            session,
                            appareil,
                            maintenant,
                            jeton
                        )
                        .ConfigureAwait(false);

                    await transaction.CommitAsync(jeton).ConfigureAwait(false);
                    return resultat;
                }
            })
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Révoque toutes les sessions d'un utilisateur. L'effet est <b>immédiat</b>,
    /// là où le <c>SecurityStamp</c> d'Identity ne produit qu'un effet différé,
    /// borné par l'intervalle de revalidation.
    /// </summary>
    public async Task RevoquerToutesAsync(
        Guid utilisateur,
        DateTimeOffset maintenant,
        CancellationToken jeton = default
    ) =>
        await contexte
            .Sessions.Where(s => s.OwnerId == utilisateur && s.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAt, maintenant), jeton)
            .ConfigureAwait(false);

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

    /// <summary>
    /// Supprime les sessions éteintes. Idempotente : un second passage rend zéro.
    /// </summary>
    /// <remarks>
    /// Une table de sessions qui ne se vide jamais grossit indéfiniment, et
    /// chaque ligne morte est une empreinte de jeton conservée sans raison.
    /// Aucune tâche de fond ici : la commande est appelée par le déploiement,
    /// qui sait déjà lancer des migrations.
    /// </remarks>
    public async Task<int> PurgerAsync(DateTimeOffset maintenant, CancellationToken jeton) =>
        await contexte
            .Sessions.Where(s => s.ExpiresAt <= maintenant || s.RevokedAt != null)
            .ExecuteDeleteAsync(jeton)
            .ConfigureAwait(false);

    private async Task<RotationDeSession> AppliquerAsync(
        ResultatDeRotation decision,
        SessionRafraichissement? session,
        string? appareil,
        DateTimeOffset maintenant,
        CancellationToken jeton
    )
    {
        if (decision.Issue == IssueDeRotation.RejeuDansLaGrace && session is not null)
        {
            // La première requête a déjà obtenu le successeur : on le rend, sans
            // rien consommer de plus. C'est ce qui évite de déconnecter un
            // utilisateur dont deux requêtes se sont croisées.
            var suivante = await contexte
                .Sessions.AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == decision.SessionARendre, jeton)
                .ConfigureAwait(false);

            // Le successeur porte son empreinte, pas son jeton : on ne peut pas
            // le reconstruire. La rotation en émet donc un neuf DANS LA MÊME
            // famille, et la précédente reste consommée.
            return suivante is null
                ? new RotationDeSession(IssueDeRotation.ReemploiDetecte, null, null)
                : new RotationDeSession(
                    IssueDeRotation.RejeuDansLaGrace,
                    await InscrireAsync(
                            suivante.OwnerId,
                            suivante.FamilyId,
                            appareil,
                            maintenant,
                            jeton
                        )
                        .ConfigureAwait(false),
                    suivante.FamilyId
                );
        }

        if (decision.RevoquerLaFamille && session is not null)
        {
            // Deux porteurs détiennent la même chaîne. Lequel se présente est
            // indécidable : les deux perdent l'accès.
            await contexte
                .Sessions.Where(s => s.FamilyId == session.FamilyId && s.RevokedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAt, maintenant), jeton)
                .ConfigureAwait(false);

            return new RotationDeSession(decision.Issue, null, null);
        }

        if (decision.Issue != IssueDeRotation.Acceptee || session is null)
        {
            return new RotationDeSession(decision.Issue, null, null);
        }

        var neuf = await InscrireAsync(
                session.OwnerId,
                session.FamilyId,
                appareil,
                maintenant,
                jeton
            )
            .ConfigureAwait(false);

        var empreinteNeuve = HachageDeJeton.Calculer(neuf);
        var identifiantNeuf = await contexte
            .Sessions.AsNoTracking()
            .Where(s => s.TokenHash == empreinteNeuve)
            .Select(s => (Guid?)s.Id)
            .FirstOrDefaultAsync(jeton)
            .ConfigureAwait(false);

        // La consommation passe par ExecuteUpdate et non par le suivi : la
        // session a été lue sans tracking, et une écriture qui dépendrait du
        // cache d'EF ne serait pas visible pour la lecture suivante.
        var identifiantSession = session.Id;
        await contexte
            .Sessions.Where(s => s.Id == identifiantSession)
            .ExecuteUpdateAsync(
                s =>
                    s.SetProperty(x => x.ConsumedAt, maintenant)
                        .SetProperty(x => x.LastSeenAt, maintenant)
                        .SetProperty(x => x.ReplacedById, identifiantNeuf),
                jeton
            )
            .ConfigureAwait(false);

        return new RotationDeSession(IssueDeRotation.Acceptee, neuf, session.FamilyId);
    }

    private async Task<string> InscrireAsync(
        Guid utilisateur,
        Guid famille,
        string? appareil,
        DateTimeOffset maintenant,
        CancellationToken jeton
    )
    {
        var valeur = HachageDeJeton.Engendrer();

        contexte.Sessions.Add(
            new SessionRafraichissement
            {
                Id = Guid.NewGuid(),
                OwnerId = utilisateur,
                TokenHash = HachageDeJeton.Calculer(valeur),
                FamilyId = famille,
                CreatedAt = maintenant,
                ExpiresAt = maintenant + ParametresDeSession.DureeDuRafraichissement,
                Device = appareil,
                LastSeenAt = maintenant,
            }
        );

        await contexte.SaveChangesAsync(jeton).ConfigureAwait(false);
        return valeur;
    }
}
