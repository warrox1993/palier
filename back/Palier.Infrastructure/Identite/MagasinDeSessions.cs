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
/// <remarks>
/// <see cref="Utilisateur" /> suit exactement <see cref="Jeton" /> : il est
/// renseigné quand la rotation a produit un jeton, et nul autrement. Le point
/// d'entrée en a besoin pour SIGNER le jeton d'accès qui accompagne le
/// rafraîchissement — le lui faire relire en base l'obligerait à toucher le
/// contexte d'identité, que <c>ArchitectureTests</c> lui refuse.
///
/// <para>
/// Et il est nul sur un refus, délibérément : un appelant qui le lirait après
/// un réemploi signerait un jeton d'accès pour quelqu'un dont la famille vient
/// d'être révoquée.
/// </para>
/// </remarks>
public sealed record RotationDeSession(
    IssueDeRotation Issue,
    string? Jeton,
    Guid? Famille,
    Guid? Utilisateur = null
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

    /// <summary>
    /// Révoque la famille entière à laquelle appartient le jeton présenté.
    /// Rend <c>false</c> si aucune session ne porte cette empreinte.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>La famille, et pas seulement la session présentée.</b> Une rotation a
    /// pu produire un successeur avant la déconnexion : ne couper que la
    /// session du cookie le laisserait vivant, et l'onglet resté ouvert
    /// continuerait de se rafraîchir pendant deux semaines. L'utilisateur
    /// croirait s'être déconnecté.
    /// </para>
    ///
    /// <para>
    /// Le retour booléen n'est pas cosmétique : sans lui, une déconnexion sur un
    /// cookie périmé répondrait « c'est fait » sans que rien n'ait été fait.
    /// </para>
    /// </remarks>
    public async Task<bool> RevoquerFamilleAsync(
        string jetonPresente,
        DateTimeOffset maintenant,
        CancellationToken jeton = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jetonPresente);
        var empreinte = HachageDeJeton.Calculer(jetonPresente);

        var famille = await contexte
            .Sessions.AsNoTracking()
            .Where(s => s.TokenHash == empreinte)
            .Select(s => (Guid?)s.FamilyId)
            .FirstOrDefaultAsync(jeton)
            .ConfigureAwait(false);

        if (famille is not { } identifiant)
        {
            return false;
        }

        await contexte
            .Sessions.Where(s => s.FamilyId == identifiant && s.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAt, maintenant), jeton)
            .ConfigureAwait(false);

        return true;
    }

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
    /// Applique une décision de verrouillage au compte, <b>sous verrou de
    /// ligne</b>. Rend ce que la décision a produit, ou <c>null</c> si le compte
    /// a disparu entre la lecture et l'écriture.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Le verrou est le sujet.</b> Le compteur d'échecs est un
    /// lire-modifier-écrire : sans verrou, N tentatives simultanées lisent
    /// toutes le même compteur, une seule écriture atterrit, et le
    /// verrouillage — seule défense que la conception nomme contre un bourrage
    /// d'identifiants distribué — se contourne en tirant les essais en
    /// parallèle. Le <c>select … for update</c> dans une transaction sérialise
    /// les tentatives portant sur un même compte, exactement comme
    /// <see cref="FaireTournerAsync" /> le fait déjà pour la rotation.
    /// </para>
    ///
    /// <para>
    /// <b>Pourquoi le verrouillage d'un compte vit dans le magasin des
    /// sessions.</b> Il lui faut <see cref="PalierAuthDbContext" />, et
    /// <c>ArchitectureTests</c> ne l'accorde qu'à ce type. Ajouter une seconde
    /// exemption pour un second type ouvrirait une seconde porte : la liste des
    /// exemptions nommées est ce qui tient la garantie, et elle ne grandit pas
    /// pour une commodité de rangement.
    /// </para>
    ///
    /// <para>
    /// <b>La décision n'est pas prise ici.</b> Elle arrive en paramètre, et
    /// reçoit l'état <b>relu sous verrou</b> — jamais celui qu'un appelant
    /// aurait chargé plus tôt. <c>DecisionDeVerrouillage</c> reste ainsi pure
    /// et couverte à 100 %.
    /// </para>
    ///
    /// <para>
    /// <c>AsNoTracking</c> n'est pas une optimisation, et <c>ExecuteUpdate</c>
    /// non plus. Le compte a <b>déjà</b> été chargé dans ce contexte par
    /// <c>UserManager</c> au début de la connexion : sans elle, le suivi d'EF
    /// répondrait avec cet exemplaire périmé À LA PLACE de la ligne qu'on vient
    /// de verrouiller, et le verrou ne protégerait rien.
    /// </para>
    /// </remarks>
    public async Task<ResultatDeVerrouillage?> AppliquerLeVerrouillageAsync(
        Guid utilisateur,
        Func<Utilisateur, ResultatDeVerrouillage> decider,
        DateTimeOffset? dernierEchec,
        CancellationToken jeton = default
    )
    {
        ArgumentNullException.ThrowIfNull(decider);

        var strategie = contexte.Database.CreateExecutionStrategy();
        return await strategie
            .ExecuteAsync<ResultatDeVerrouillage?>(async () =>
            {
                var transaction = await contexte
                    .Database.BeginTransactionAsync(jeton)
                    .ConfigureAwait(false);
                await using (transaction.ConfigureAwait(false))
                {
                    var compte = await contexte
                        .Users.FromSql(
                            $"""
                            select * from public."AspNetUsers"
                             where "Id" = {utilisateur}
                             for update
                            """
                        )
                        .AsNoTracking()
                        .FirstOrDefaultAsync(jeton)
                        .ConfigureAwait(false);

                    if (compte is null)
                    {
                        return null;
                    }

                    var decide = decider(compte);

                    // ⚠ LE JETON DE CONCURRENCE EST RENOUVELÉ ICI, ET CE N'EST PAS
                    // UNE FORMALITÉ.
                    //
                    // `UserManager.UpdateAsync` le renouvelait à chaque écriture ;
                    // `ExecuteUpdateAsync` ne le fait pas. Le remplacer sans
                    // reprendre ce geste RETIRE une protection au lieu d'en
                    // ajouter une : toute autre écriture d'Identity partie d'un
                    // exemplaire périmé — et `UserStore.UpdateAsync` marque
                    // TOUTES les colonnes modifiées — réécrirait alors sa copie
                    // par-dessus le verrouillage.
                    //
                    // Mesuré : sans cette ligne, une requête des routes 2FA —
                    // qui n'exigent AUCUN mot de passe — chargée avant qu'un
                    // verrouillage ne tombe l'efface en écrivant, remettant
                    // `LockoutEnd` à nul ET `VerrouillagesSubis` à zéro, ce qui
                    // ramène l'escalade à son premier palier pour toujours.
                    //
                    // La course que ce verrou ferme n'est pas rouverte pour
                    // autant : les écritures concurrentes du verrouillage sont
                    // sérialisées par `for update`, et `ExecuteUpdate` ne
                    // contrôle aucun jeton de concurrence — il le pose.
                    var jetonDeConcurrence = Guid.NewGuid().ToString();

                    await contexte
                        .Users.Where(u => u.Id == utilisateur)
                        .ExecuteUpdateAsync(
                            u =>
                                u.SetProperty(x => x.AccessFailedCount, decide.EchecsConsecutifs)
                                    .SetProperty(
                                        x => x.VerrouillagesSubis,
                                        decide.VerrouillagesSubis
                                    )
                                    .SetProperty(x => x.LockoutEnd, decide.Jusqua)
                                    .SetProperty(x => x.DernierEchecLe, dernierEchec)
                                    .SetProperty(x => x.ConcurrencyStamp, jetonDeConcurrence),
                            jeton
                        )
                        .ConfigureAwait(false);

                    await transaction.CommitAsync(jeton).ConfigureAwait(false);
                    return decide;
                }
            })
            .ConfigureAwait(false);
    }

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
                    suivante.FamilyId,
                    suivante.OwnerId
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

        return new RotationDeSession(
            IssueDeRotation.Acceptee,
            neuf,
            session.FamilyId,
            session.OwnerId
        );
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
