using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Palier.Api;
using Palier.Api.Auth;
using Palier.Application.Pipeline;
using Palier.Infrastructure.Coffre;

namespace Palier.Database.Tests;

/// <summary>
/// De quoi appeler un point d'entrée comme le ferait une requête réelle, et
/// lire ce qui en sort.
/// </summary>
/// <remarks>
/// <para>
/// <b>Aucun serveur de test.</b> Un <c>IResult</c> exécuté sur un
/// <c>HttpContext</c> écrit son statut, son corps et ses en-têtes exactement
/// comme en production : c'est le même code de sérialisation. Monter un serveur
/// n'ajouterait que le routage — et une dépendance de plus, qu'il faudrait
/// justifier par une décision datée.
/// </para>
///
/// <para>
/// Ce fichier existe parce que deux fichiers d'épreuves en ont besoin. Le
/// harnais est <b>une connaissance</b> — « comment exécuter un résultat et lire
/// ce qu'il a produit » — et une connaissance vit à un seul endroit.
/// </para>
/// </remarks>
internal static class HarnaisHttp
{
    /// <summary>
    /// Une clé de signature pour les épreuves : quarante-quatre signes sans
    /// signification, qui ne ressemblent à aucun secret réel.
    /// </summary>
    public const string Cle = "cle-de-signature-des-epreuves-du-lot-quatre-";

    /// <summary>
    /// Les réglages SANS LESQUELS `Composition.Composer` refuse — D60 fait
    /// tomber le refus au démarrage, et c'est voulu. Toute épreuve qui compose
    /// l'API les pose, y compris celles qui n'envoient rien : elles éprouvent
    /// autre chose, et ne doivent pas buter sur une exigence hors sujet.
    /// </summary>
    public static void PoserLeCourrier(IDictionary<string, string?> reglages)
    {
        ArgumentNullException.ThrowIfNull(reglages);

        reglages["SMTP_HOST"] = "relais.invalid";
        reglages["SMTP_PORT"] = "587";
        reglages["SMTP_FROM"] = "palier@exemple.test";
        reglages["APP_URL"] = "https://palier.test";
    }

    /// <summary>La clé de données des épreuves. Fixe : rien ici ne protège.</summary>
    public static readonly Guid CleDeDonnees = new("33333333-3333-3333-3333-333333333333");

    public static readonly TrousseauDeChiffrement Trousseau =
        new(
            new Dictionary<Guid, byte[]> { [CleDeDonnees] = [.. Enumerable.Repeat((byte)7, 32)] },
            CleDeDonnees
        );

    /// <summary>
    /// L'API réelle, composée par <see cref="Composition.Composer" />.
    /// </summary>
    /// <remarks>
    /// <c>Sources.Clear()</c> écarte les variables d'environnement de la
    /// machine : sans lui, une chaîne de connexion présente sur un poste de
    /// développement remplacerait celle du conteneur, et les épreuves
    /// tourneraient sur une autre base que celle qu'elles croient interroger.
    /// </remarks>
    public static WebApplication Hote(
        BaseFixture baseDeDonnees,
        Action<IServiceCollection>? ajuster = null,
        IReadOnlyDictionary<string, string?>? configurationEnPlus = null
    )
    {
        ArgumentNullException.ThrowIfNull(baseDeDonnees);

        var reglages = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["ConnectionStrings:Palier"] = baseDeDonnees.ChaineApp,
            ["ConnectionStrings:PalierAuth"] = baseDeDonnees.ChaineAuth,
            ["JWT_SIGNING_KEY"] = Cle,

        };

        PoserLeCourrier(reglages);

        // Ce que l'épreuve ajoute PRIME : une clé posée ici remplace le défaut,
        // ce qui permet d'éprouver une liste d'administrateurs vide aussi bien
        // qu'une liste peuplée.
        foreach (var paire in configurationEnPlus ?? new Dictionary<string, string?>())
        {
            reglages[paire.Key] = paire.Value;
        }

        var constructeur = WebApplication.CreateBuilder();
        constructeur.Configuration.Sources.Clear();
        constructeur.Configuration.AddInMemoryCollection(reglages);

        Composition.Composer(constructeur);

        // L'ajustement vient APRÈS la composition réelle : il remplace un
        // service précis sans recomposer l'application. Recomposer reviendrait
        // à éprouver la copie.
        ajuster?.Invoke(constructeur.Services);

        var hote = constructeur.Build();

        // Ce que `AmorcageDuTrousseau` ferait au démarrage réel. Sans lui, le
        // porteur reste vide et toute lecture du secret TOTP lèverait — ce qui
        // est exactement le refus qu'il doit produire hors des épreuves.
        hote.Services.GetRequiredService<PorteurDeTrousseau>().Poser(Trousseau);

        return hote;
    }

    /// <summary>
    /// Un contexte qui porte les services : <c>Results.Json</c> les résout à
    /// l'exécution, et un <c>DefaultHttpContext</c> nu le fait échouer sur
    /// « Value cannot be null. (Parameter 'provider') ».
    /// </summary>
    public static DefaultHttpContext Contexte(IServiceProvider services) =>
        new() { RequestServices = services };

    /// <summary>Un contexte qui présente déjà le cookie de rafraîchissement.</summary>
    public static DefaultHttpContext ContexteAvecCookie(IServiceProvider services, string valeur)
    {
        var contexte = Contexte(services);
        contexte.Request.Headers.Cookie = $"{CookieDeRafraichissement.Nom}={valeur}";
        return contexte;
    }

    /// <summary>Exécute un <c>IResult</c> et rend le statut et le corps réels.</summary>
    public static async Task<(int Code, string Corps)> ExecuterAsync(
        IServiceProvider services,
        Task<IResult> resultat,
        HttpContext? contexte = null
    )
    {
        contexte ??= Contexte(services);
        using var corps = new MemoryStream();
        contexte.Response.Body = corps;

        await (await resultat).ExecuteAsync(contexte);

        return (contexte.Response.StatusCode, Encoding.UTF8.GetString(corps.ToArray()));
    }

    /// <summary>Le champ <c>code</c> d'un corps JSON.</summary>
    public static string Code(string corps) =>
        JsonDocument.Parse(corps).RootElement.GetProperty("code").GetString() ?? string.Empty;

    /// <summary>
    /// La valeur du cookie de rafraîchissement dans les en-têtes de réponse,
    /// ou <c>null</c> s'il n'y en a pas — ou s'il a été effacé.
    /// </summary>
    /// <remarks>
    /// Un cookie effacé n'est pas un cookie absent : le serveur émet un
    /// <c>Set-Cookie</c> à valeur vide et de date passée. Les confondre ferait
    /// lire « la déconnexion a posé un cookie » là où elle vient de l'enlever.
    /// </remarks>
    public static string? CookieRendu(HttpContext contexte)
    {
        ArgumentNullException.ThrowIfNull(contexte);

        foreach (var entete in contexte.Response.Headers.SetCookie)
        {
            if (entete is null || !entete.StartsWith(CookieDeRafraichissement.Nom + "=", StringComparison.Ordinal))
            {
                continue;
            }

            var valeur = entete[(entete.IndexOf('=', StringComparison.Ordinal) + 1)..].Split(';')[0];
            return string.IsNullOrEmpty(valeur) ? null : valeur;
        }

        return null;
    }

    /// <summary>Vrai si la réponse EFFACE le cookie de rafraîchissement.</summary>
    public static bool CookieEfface(HttpContext contexte)
    {
        ArgumentNullException.ThrowIfNull(contexte);

        foreach (var entete in contexte.Response.Headers.SetCookie)
        {
            if (entete is not null
                && entete.StartsWith(CookieDeRafraichissement.Nom + "=;", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}

/// <summary>Une horloge qui ne bouge pas, pour éprouver les durées.</summary>
internal sealed class HorlogeFixe(DateTimeOffset instant) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => instant;
}

/// <summary>Un demandeur dont l'identité est posée par l'épreuve.</summary>
/// <remarks>
/// Il remplace <c>IdentiteDepuisJeton</c> là où l'épreuve porte sur ce que le
/// point d'entrée FAIT d'une identité, pas sur la façon dont elle est lue —
/// celle-là est éprouvée dans <c>IdentiteDepuisJetonTests</c>.
/// </remarks>
internal sealed class DemandeurFixe(Guid? identifiant) : IIdentiteDemandeur
{
    public Guid? Identifiant => identifiant;
}

/// <summary>Un demandeur dont l'épreuve change l'identité en cours de route.</summary>
/// <remarks>
/// Il évite de reconstruire un hôte — et donc un pool de connexions — à chaque
/// combinaison éprouvée. Enregistré en <b>singleton</b> : l'épreuve et le
/// gestionnaire d'autorisation doivent voir le MÊME objet.
/// </remarks>
internal sealed class DemandeurMutable : IIdentiteDemandeur
{
    public Guid? Identifiant { get; set; }
}
