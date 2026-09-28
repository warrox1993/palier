using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Palier.Infrastructure.Coffre;

namespace Palier.Database.Tests;

/// <summary>
/// L'API RÉELLE, lancée comme un processus — D80 et D82.
/// </summary>
/// <remarks>
/// <para>
/// Les épreuves en mémoire de <see cref="CoffreLocalTests" /> jugent la
/// décision. Celles-ci jugent ce qu'un exploitant obtiendrait en lançant le
/// binaire : <c>Program.cs</c> compris, qu'aucune épreuve en mémoire n'emprunte.
/// C'est le seul endroit où « le coffre local est impossible en production » se
/// prouve de bout en bout, et non par la lecture d'une méthode.
/// </para>
///
/// <para>
/// <b>Le binaire lancé est celui de <c>back/Palier.Api/bin</c></b>, jamais la
/// copie posée à côté des épreuves : coverlet instrumente celle-là pendant
/// <c>dotnet test</c>, et un processus enfant qui la chargerait écrirait dans
/// le rapport de couverture d'un autre.
/// </para>
///
/// <para>
/// Le témoin positif — le même lancement en <c>Development</c>, qui démarre —
/// n'est pas décoratif. Sans lui, un refus en production pourrait venir d'une
/// variable oubliée ou d'un binaire introuvable, et l'épreuve serait verte pour
/// une autre raison que la sienne.
/// </para>
/// </remarks>
[Collection(BaseFixture.Collection)]
public sealed class DemarrageDeLApiTests(BaseFixture baseDeDonnees)
{
    private static readonly TimeSpan _delai = TimeSpan.FromSeconds(60);

    private static readonly Regex _ecoute = new(
        @"Now listening on: (http://127\.0\.0\.1:\d+)",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1)
    );

    [Fact]
    public async Task En_PRODUCTION_l_API_refuse_de_demarrer_en_coffre_local()
    {
        using var api = Lancer("Production", coffreLocal: true);

        var (code, sortie) = await api.AttendreLaFinAsync();

        Assert.NotEqual(0, code);
        Assert.Contains("Le coffre local est refusé en « Production »", sortie, StringComparison.Ordinal);
        Assert.DoesNotContain("Now listening", sortie, StringComparison.Ordinal);
    }

    [Fact]
    public async Task En_PRODUCTION_sans_PALIER_COFFRE_l_API_exige_toujours_OKMS()
    {
        using var api = Lancer("Production", coffreLocal: false);

        var (code, sortie) = await api.AttendreLaFinAsync();

        Assert.NotEqual(0, code);
        Assert.Contains("Le coffre ne peut pas s'ouvrir", sortie, StringComparison.Ordinal);
        Assert.DoesNotContain("Now listening", sortie, StringComparison.Ordinal);
    }

    [Fact]
    public async Task En_Development_le_coffre_local_demarre_et_sert_OpenAPI_et_Scalar()
    {
        using var api = Lancer("Development", coffreLocal: true);

        var adresse = await api.AttendreLEcouteAsync();

        Assert.Contains("Coffre LOCAL", api.Sortie, StringComparison.Ordinal);

        using var client = new HttpClient { BaseAddress = new Uri(adresse) };

        using var document = JsonDocument.Parse(await client.GetStringAsync(new Uri("/openapi/v1.json", UriKind.Relative)));
        var racine = document.RootElement;

        // Le schéma est déclaré…
        Assert.Equal(
            "bearer",
            racine.GetProperty("components").GetProperty("securitySchemes").GetProperty("Bearer").GetProperty("scheme").GetString()
        );

        var chemins = racine.GetProperty("paths");

        // …exigé sur une route protégée…
        Assert.True(
            chemins.GetProperty("/api/v1/seances").GetProperty("get").TryGetProperty("security", out _),
            "La liste des séances est protégée : le document doit le dire."
        );

        // …et ABSENT d'une route anonyme, qui afficherait sinon un cadenas
        // qu'elle n'a pas.
        Assert.False(
            chemins.GetProperty("/api/v1/auth/connexion").GetProperty("post").TryGetProperty("security", out _),
            "La connexion est anonyme : aucune exigence de jeton ne doit y figurer."
        );

        using var interfaceScalar = await client.GetAsync(new Uri("/scalar", UriKind.Relative));
        Assert.True(
            interfaceScalar.IsSuccessStatusCode,
            $"/scalar a rendu {(int)interfaceScalar.StatusCode}."
        );
    }

    // ================================================================

    private Processus Lancer(string environnement, bool coffreLocal)
    {
        var binaire = Binaire();
        var depart = new ProcessStartInfo(
            Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet",
            [binaire]
        )
        {
            WorkingDirectory = Path.GetDirectoryName(binaire)!,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        // Rien ne vient du poste : une variable présente sur la machine de
        // développement ferait réussir ou échouer l'épreuve pour une autre
        // raison que la sienne.
        foreach (var cle in depart.Environment.Keys.ToArray())
        {
            if (
                cle.StartsWith("OKMS_", StringComparison.Ordinal)
                || cle.StartsWith("PALIER_", StringComparison.Ordinal)
                || cle.StartsWith("ConnectionStrings", StringComparison.OrdinalIgnoreCase)
                || cle.StartsWith("ASPNETCORE_", StringComparison.Ordinal)
                || cle.StartsWith("SMTP_", StringComparison.Ordinal)
                || cle.StartsWith("GOOGLE_", StringComparison.Ordinal)
                || cle is "DOTNET_ENVIRONMENT" or "JWT_SIGNING_KEY" or "APP_URL" or "ADMIN_EMAILS"
            )
            {
                depart.Environment.Remove(cle);
            }
        }

        // Posées VIDES, et non absentes : une variable d'environnement vide
        // l'emporte sur un secret utilisateur qui porterait la même clé.
        foreach (var variable in ReglagesDuCoffre.Variables)
        {
            depart.Environment[variable] = string.Empty;
        }

        depart.Environment["ASPNETCORE_ENVIRONMENT"] = environnement;
        depart.Environment["ASPNETCORE_URLS"] = "http://127.0.0.1:0";
        depart.Environment["ConnectionStrings__Palier"] = baseDeDonnees.ChaineApp;
        depart.Environment["ConnectionStrings__PalierAuth"] = baseDeDonnees.ChaineAuth;
        depart.Environment["JWT_SIGNING_KEY"] = HarnaisHttp.Cle;
        depart.Environment["SMTP_HOST"] = "relais.invalid";
        depart.Environment["SMTP_PORT"] = "587";
        depart.Environment["SMTP_FROM"] = "palier@exemple.test";
        depart.Environment["APP_URL"] = "https://palier.test";

        if (coffreLocal)
        {
            depart.Environment[CoffreLocal.CleDuMode] = CoffreLocal.ValeurDuMode;
            depart.Environment[CoffreLocal.CleDeLaCle] = Convert.ToBase64String(new byte[32]);
        }

        return new Processus(Process.Start(depart)!);
    }

    /// <summary>
    /// <c>back/Palier.Api/bin/&lt;configuration&gt;/net10.0/Palier.Api.dll</c>,
    /// la configuration étant celle sous laquelle les épreuves tournent.
    /// </summary>
    private string Binaire()
    {
        var segments = AppContext.BaseDirectory.Split(
            Path.DirectorySeparatorChar,
            StringSplitOptions.RemoveEmptyEntries
        );
        var configuration = segments[Array.LastIndexOf(segments, "bin") + 1];

        var binaire = Path.Combine(
            baseDeDonnees.Racine,
            "back",
            "Palier.Api",
            "bin",
            configuration,
            "net10.0",
            "Palier.Api.dll"
        );

        Assert.True(
            File.Exists(binaire),
            $"Binaire de l'API introuvable : {binaire}. `dotnet build back/Palier.sln` le produit."
        );
        return binaire;
    }

    /// <summary>
    /// Un processus dont la sortie est lue au fil de l'eau, et qui meurt avec
    /// l'épreuve — tué s'il vit encore, jamais abandonné.
    /// </summary>
    private sealed class Processus : IDisposable
    {
        private readonly Process _processus;
        private readonly StringBuilder _sortie = new();
        private readonly Lock _verrou = new();

        public Processus(Process processus)
        {
            _processus = processus;
            _processus.OutputDataReceived += (_, e) => Ajouter(e.Data);
            _processus.ErrorDataReceived += (_, e) => Ajouter(e.Data);
            _processus.BeginOutputReadLine();
            _processus.BeginErrorReadLine();
        }

        public string Sortie
        {
            get
            {
                lock (_verrou)
                {
                    return _sortie.ToString();
                }
            }
        }

        public async Task<(int Code, string Sortie)> AttendreLaFinAsync()
        {
            using var borne = new CancellationTokenSource(_delai);
            await _processus.WaitForExitAsync(borne.Token);
            return (_processus.ExitCode, Sortie);
        }

        public async Task<string> AttendreLEcouteAsync()
        {
            var limite = DateTime.UtcNow + _delai;
            while (DateTime.UtcNow < limite)
            {
                var trouve = _ecoute.Match(Sortie);
                if (trouve.Success)
                {
                    return trouve.Groups[1].Value;
                }

                Assert.False(
                    _processus.HasExited,
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"L'API s'est arrêtée (code {(_processus.HasExited ? _processus.ExitCode : 0)}) :\n{Sortie}"
                    )
                );
                await Task.Delay(100);
            }

            Assert.Fail($"L'API n'a pas ouvert son port en {_delai.TotalSeconds} s :\n{Sortie}");
            return string.Empty;
        }

        public void Dispose()
        {
            if (!_processus.HasExited)
            {
                _processus.Kill(entireProcessTree: true);
                _processus.WaitForExit();
            }

            _processus.Dispose();
        }

        private void Ajouter(string? ligne)
        {
            if (ligne is null)
            {
                return;
            }

            lock (_verrou)
            {
                _sortie.AppendLine(ligne);
            }
        }
    }
}
