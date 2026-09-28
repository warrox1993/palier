using Palier.Infrastructure.Coffre;

namespace Palier.Api.Socle;

/// <summary>
/// D'où viennent les secrets, décidé UNE fois, avant la composition — D59, D80.
/// </summary>
/// <remarks>
/// <para>
/// Sorti de <c>Program.cs</c> pour la raison qui en a déjà sorti la composition :
/// l'épreuve qui prouve que le coffre local est refusé en production doit
/// emprunter le chemin RÉEL du démarrage, pas une copie de ses lignes.
/// </para>
///
/// <para>
/// Les deux méthodes vont ensemble. <see cref="Brancher" /> décide de la source
/// de configuration ; <see cref="ChargerLeTrousseauAsync" /> pose la clé de
/// données, avant que le port s'ouvre. Le mode rendu par la première est ce que
/// la seconde reçoit : une décision prise deux fois pourrait l'être de deux
/// façons.
/// </para>
/// </remarks>
internal static class SourceDesSecrets
{
    // Un AVERTISSEMENT, pas une information : un journal de démarrage qui dit
    // « coffre local » sur une machine qu'on croyait d'exploitation doit se
    // voir au premier coup d'œil.
    private static readonly Action<ILogger, Exception?> _coffreLocal = LoggerMessage.Define(
        LogLevel.Warning,
        new EventId(2080, "CoffreLocal"),
        "Coffre LOCAL : la clé de données vient de PALIER_CLE_LOCALE, et non d'OVHcloud KMS. "
            + "Ce mode n'existe qu'en Development (D80)."
    );

    /// <summary>
    /// Branche la source des secrets. En mode OKMS, le coffre rejoint la
    /// configuration, exactement comme avant D80 ; en mode local, rien ne
    /// s'ajoute : la configuration du poste — variables d'environnement,
    /// <c>dotnet user-secrets</c> — porte déjà tout.
    /// </summary>
    public static ModeDuCoffre Brancher(WebApplicationBuilder constructeur)
    {
        ArgumentNullException.ThrowIfNull(constructeur);

        var environnement = constructeur.Environment.EnvironmentName;
        var mode = CoffreLocal.Choisir(constructeur.Configuration, environnement);

        if (mode == ModeDuCoffre.Okms)
        {
            // L'API refuse de démarrer si le coffre est injoignable, si un
            // secret manque ou si l'environnement n'a pas de chemin — c'est
            // délibéré, un démarrage sans coffre serait un démarrage sans
            // configuration.
            constructeur.Configuration.AjouterLeCoffre(environnement);
        }

        return mode;
    }

    /// <summary>
    /// Étapes 6 et 7 de D59 : le trousseau, chargé avant que le port s'ouvre.
    /// Une table de clés vide, une enveloppe que le coffre refuse, ou une clé
    /// locale absente arrêtent ici.
    /// </summary>
    public static async Task ChargerLeTrousseauAsync(
        WebApplication application,
        ModeDuCoffre mode,
        CancellationToken jeton
    )
    {
        ArgumentNullException.ThrowIfNull(application);

        if (mode == ModeDuCoffre.Local)
        {
            application
                .Services.GetRequiredService<PorteurDeTrousseau>()
                .Poser(CoffreLocal.Trousseau(application.Configuration));
            _coffreLocal(application.Logger, null);
            return;
        }

        await application
            .Services.GetRequiredService<AmorcageDuTrousseau>()
            .ChargerAsync(jeton)
            .ConfigureAwait(false);
    }
}
