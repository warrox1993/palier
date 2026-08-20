namespace Palier.Api.Socle;

/// <summary>
/// L'assertion de démarrage — D37, et elle vient de D30.
///
/// Elle implémente <see cref="IHostedLifecycleService" /> et fait son travail
/// dans <c>StartingAsync</c>, PAS dans <c>StartAsync</c>. Ce n'est pas un
/// raffinement : <c>GenericWebHostService</c> — Kestrel — est un
/// <c>IHostedService</c> enregistré par <c>WebApplication.CreateBuilder</c>,
/// donc AVANT celui-ci. Dans <c>StartAsync</c>, le refus tomberait après que le
/// serveur a commencé à écouter, et une requête pourrait passer pendant la
/// fenêtre. <c>StartingAsync</c> s'exécute avant TOUS les <c>StartAsync</c>.
///
/// Pourquoi une assertion au démarrage plutôt qu'une épreuve de plus : sept
/// épreuves vertes sur un Testcontainer ne démontrent RIEN sur l'instance
/// managée, où deux inconnues décident si RLS mord du tout — le compte
/// d'administration a-t-il BYPASSRLS, et les tables appartiennent-elles au rôle
/// de l'API. C'est la seule preuve qui porte sur le TRAITEMENT plutôt que sur le
/// code : à l'auditeur, on ne montre plus « sept tests verts en CI » mais « le
/// service refuse de démarrer si l'isolation n'est pas en place, et voici la
/// ligne de journal de chaque déploiement ».
/// </summary>
internal sealed partial class AssertionAuDemarrage(
    IServiceScopeFactory fabrique,
    ILogger<AssertionAuDemarrage> journal
) : IHostedLifecycleService
{
    public async Task StartingAsync(CancellationToken cancellationToken)
    {
        // La portée est créée ici, et non injectée : `PalierDbContext` est
        // enregistré en `Scoped`, un service hébergé est un singleton, et .NET
        // refuse de résoudre l'un depuis l'autre. Le contexte lui-même n'est
        // jamais tenu par ce type — seul `LecteurDeSocle` le tient, et c'est lui
        // qui est nommément exempté du test d'architecture.
        using var portee = fabrique.CreateScope();
        var assertion = portee.ServiceProvider.GetRequiredService<AssertionDIsolation>();
        var diagnostic = await assertion.VerifierAsync(cancellationToken).ConfigureAwait(false);

        // La ligne de journal du déploiement. AUCUNE donnée de santé, aucun
        // compte : un nom de rôle et un nombre de tables.
        IsolationVerifiee(journal, diagnostic.Role, diagnostic.NombreDeTables);
    }

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StartedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StoppingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StoppedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [LoggerMessage(
        EventId = 2037,
        Level = LogLevel.Information,
        Message = "Isolation vérifiée sur la base réelle : rôle « {Role} », "
            + "{NombreDeTables} table(s) de `public`, toutes avec RLS activée et forcée, "
            + "aucune possédée par ce rôle."
    )]
    private static partial void IsolationVerifiee(ILogger logger, string role, int nombreDeTables);
}
