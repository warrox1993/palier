# Le coffre des secrets — plan d'implémentation

> **Pour l'agent exécutant :** SOUS-SKILL REQUIS — `superpowers:subagent-driven-development` (recommandé) ou `superpowers:executing-plans`. Les étapes sont cochables (`- [ ]`).

**But :** sortir les secrets de l'environnement du serveur vers OVHcloud KMS, et chiffrer le secret TOTP que D58 avait laissé en clair.

**Architecture :** un fournisseur de configuration ASP.NET Core lit six paires au Secret Manager au démarrage ; un service hébergé charge le trousseau de clés de données depuis la base et le coffre ; `GestionnaireDUtilisateurs` chiffre et déchiffre le secret TOTP au passage. L'API ne crée jamais de clé — une commande d'exploitation la pose.

**Pile :** .NET 10, ASP.NET Core, EF Core, `HttpClient` nu (aucun SDK .NET n'existe pour OKMS), `System.Security.Cryptography.AesGcm`, xUnit, Testcontainers.

**Spec :** `docs/superpowers/specs/2026-08-22-coffre-des-secrets-design.md` — à lire avant la tâche 1. Le plan argumente depuis elle.

## Contraintes globales

Elles s'appliquent à **toutes** les tâches, sans être répétées.

- **TDD strict.** Le test s'écrit d'abord, on le voit ROUGE, puis on implémente. `CLAUDE.md` : _« Tout code écrit avant son test est supprimé et réécrit. »_
- **Deux assertions par épreuve de garde-fou** : le refus **et** un motif propre à sa cause. Un refus qui tombe pour la mauvaise raison est un garde-fou qui ment.
- **Aucune valeur de secret dans un journal** — ni entière, ni tronquée, ni hachée. Aucune donnée de santé non plus.
- **Aucun secret dans le dépôt.** `back/.env` est ignoré ; gitleaks tourne au `pre-commit`.
- **Aucune requête construite par concaténation.** Paramètres liés, y compris pour un identifiant « qui vient forcément de chez nous ».
- **Seuils de couverture :** `Palier.Database.Tests` est à `96,79,71` (D57). Il ne descend pas. S'il monte, il est relevé dans le même commit — un seuil sous le niveau atteint cesse de détecter le code mort public.
- **Messages de commit en français, à l'impératif.**
- Le `pre-push` exécute `npm run verify` (~7 min). Pousser avec `GIT_SSH_COMMAND="ssh -o ServerAliveInterval=20 -o ServerAliveCountMax=60"`, sans quoi GitHub ferme la connexion et `git push` sort en 141.

## Structure des fichiers

```
back/Palier.Infrastructure/Coffre/
├── ClientOkms.cs                     jeton OAuth2 + trois appels REST          tâche 2
├── ReglagesDuCoffre.cs               les cinq variables d'amorçage, validées   tâche 2
├── SourceDeConfigurationOkms.cs      IConfigurationSource                       tâche 3
├── FournisseurDeConfigurationOkms.cs IConfigurationProvider                     tâche 3
├── TrousseauDeChiffrement.cs         les clés en mémoire, chiffrer/déchiffrer   tâche 4
├── CleDeDonnees.cs                   l'entité EF de `cles_de_donnees`           tâche 1
└── AmorcageDuTrousseau.cs            IHostedService, étapes 6 et 7              tâche 5

back/Palier.Api/Outils/
└── PoserUneCleDeDonnees.cs           la commande d'exploitation                 tâche 6

back/Palier.Infrastructure/Identite/
└── GestionnaireDUtilisateurs.cs      + 2 surcharges                             tâche 7

back/Palier.Database.Tests/
├── CoffreTests.cs                    client, configuration, trousseau       tâches 2-4
├── CleDeDonneesTests.cs              RLS, privilèges, commande             tâches 1, 6
├── AmorcageDuTrousseauTests.cs       les six refus                          tâches 5, 8
└── DeuxFacteursTests.cs              + chiffrement du TOTP                      tâche 7
```

`Palier.Domain` et `Palier.Application` ne bougent pas : la cryptographie n'est pas une connaissance du domaine, et aucun cas d'usage ne chiffre.

---

## Tâche 1 : La table `cles_de_donnees` et sa politique RLS

**Fichiers :**

- Créer : `back/Palier.Infrastructure/Coffre/CleDeDonnees.cs`
- Modifier : `back/Palier.Infrastructure/PalierDbContext.cs`
- Créer : une migration EF
- Créer : `back/Palier.Database.Tests/CleDeDonneesTests.cs`

**Interfaces :**

- Produit : `CleDeDonnees { Guid Id; string Enveloppe; DateTimeOffset CreeeLe; }`, `DbSet<CleDeDonnees> ClesDeDonnees` sur `PalierDbContext`.

- [ ] **Étape 1 — Écrire les épreuves ROUGES**

```csharp
[Fact]
public async Task palier_app_LIT_les_cles_de_donnees()
{
    await using var administrateur = _base.Contexte(_base.ChaineMigrations);
    var id = Guid.NewGuid();
    await administrateur.Database.ExecuteSqlAsync(
        $"insert into public.cles_de_donnees (id, enveloppe, creee_le) values ({id}, 'jwe', now())");

    await using var app = _base.Contexte(_base.ChaineApp);
    var lues = await app.ClesDeDonnees.ToListAsync();

    Assert.Single(lues);
    Assert.Equal(id, lues[0].Id);
}

[Fact]
public async Task palier_app_NE_PEUT_PAS_ecrire_une_cle_de_donnees()
{
    await using var app = _base.Contexte(_base.ChaineApp);
    var faute = await Assert.ThrowsAsync<PostgresException>(() =>
        app.Database.ExecuteSqlAsync(
            $"insert into public.cles_de_donnees (id, enveloppe, creee_le) values ({Guid.NewGuid()}, 'forge', now())"));

    // Deux assertions : le refus ET son motif. Un refus pour cause de
    // colonne manquante passerait la première et pas la seconde.
    Assert.Equal("42501", faute.SqlState);
    Assert.Contains("cles_de_donnees", faute.Message, StringComparison.Ordinal);
}

[Fact]
public async Task La_table_porte_RLS_activee_ET_forcee()
{
    // Sans quoi `LecteurDeSocle` refuse le démarrage de l'API entière.
    await using var administrateur = _base.Contexte(_base.ChaineAdministrateur);
    var etat = await administrateur.Database
        .SqlQuery<(bool Active, bool Forcee)>(
            $"""
            select c.relrowsecurity, c.relforcerowsecurity
              from pg_class c join pg_namespace n on n.oid = c.relnamespace
             where n.nspname = 'public' and c.relname = 'cles_de_donnees'
            """)
        .SingleAsync();

    Assert.True(etat.Active, "RLS n'est pas activée sur cles_de_donnees.");
    Assert.True(etat.Forcee, "RLS n'est pas FORCÉE : le propriétaire la contournerait.");
}
```

- [ ] **Étape 2 — Les faire tourner, constater le ROUGE**

Run : `dotnet test back/Palier.Database.Tests --filter CleDeDonneesTests`
Attendu : ÉCHEC — `relation "public.cles_de_donnees" does not exist`.

- [ ] **Étape 3 — L'entité et le contexte**

```csharp
// back/Palier.Infrastructure/Coffre/CleDeDonnees.cs
namespace Palier.Infrastructure.Coffre;

/// <summary>
/// L'enveloppe d'une clé de données, telle qu'OKMS la rend : un JWE compact
/// que seul le coffre sait déballer. Rien ici n'est secret — sans le compte
/// de service, une enveloppe est du bruit.
/// </summary>
public sealed class CleDeDonnees
{
    public Guid Id { get; init; }
    public required string Enveloppe { get; init; }
    public DateTimeOffset CreeeLe { get; init; }
}
```

Dans `PalierDbContext` : `public DbSet<CleDeDonnees> ClesDeDonnees => Set<CleDeDonnees>();` et la configuration `ToTable("cles_de_donnees")`, colonnes `id`, `enveloppe`, `creee_le`.

- [ ] **Étape 4 — La migration, avec sa politique**

`dotnet ef migrations add CoffreDesSecrets --project back/Palier.Infrastructure --startup-project back/Palier.Api`

Puis, dans le `Up` — D14 impose `migrationBuilder.Sql` pour les vues et contraintes :

```csharp
migrationBuilder.Sql(
    """
    alter table public.cles_de_donnees enable row level security;
    alter table public.cles_de_donnees force row level security;

    -- Forme « référence publique », celle de nutrient_refs. `using (true)`
    -- ne concède rien : une enveloppe est illisible sans le coffre.
    create policy lecture_publique on public.cles_de_donnees
      for select to palier_app
      using (true);

    -- AUCUNE politique d'écriture : « If no policy exists for the table, a
    -- default-deny policy is used ». Et le privilège manquant refuse déjà.
    grant select on public.cles_de_donnees to palier_app;
    """
);
```

- [ ] **Étape 5 — Vérifier le VERT, et que le socle tient**

Run : `dotnet test back/Palier.Database.Tests --filter "CleDeDonneesTests|AssertionDemarrageTests|IsolationTests"`
Attendu : tout passe. `AssertionDemarrageTests` est le témoin — si la nouvelle table manquait RLS, il rougirait.

- [ ] **Étape 6 — Commit**

```bash
git add back/Palier.Infrastructure back/Palier.Database.Tests/CleDeDonneesTests.cs
git commit -m "pose la table des clés de données, en lecture seule pour l'API"
```

---

## Tâche 2 : Le client OKMS

**Fichiers :**

- Créer : `back/Palier.Infrastructure/Coffre/ReglagesDuCoffre.cs`, `ClientOkms.cs`
- Créer : `back/Palier.Database.Tests/CoffreTests.cs`

**Interfaces :**

- Consomme : rien.
- Produit :

  ```csharp
  sealed record ReglagesDuCoffre(string Endpoint, string OkmsId, string CleId,
                                 string ClientId, string ClientSecret)
  { public static ReglagesDuCoffre Depuis(IConfiguration source); }

  sealed class ClientOkms(HttpClient http, ReglagesDuCoffre reglages)
  {
      Task<IReadOnlyDictionary<string, string>> LireLeSecretAsync(string chemin, CancellationToken j);
      Task<(string Enveloppe, byte[] Clair)> CreerUneCleDeDonneesAsync(CancellationToken j);
      Task<byte[]> DeballerAsync(string enveloppe, CancellationToken j);
  }
  ```

- [ ] **Étape 1 — Écrire les épreuves ROUGES**

Le client se teste sans réseau, par un `HttpMessageHandler` factice. Trois comportements comptent : l'URL exacte, le jeton porté, et le refus qui ne divulgue rien.

```csharp
[Fact]
public async Task Le_jeton_est_demande_a_www_ovh_com_et_non_a_ovhcloud_com()
{
    // Mesuré le 22/08/2026 : les trois adresses en ovhcloud.com rendent une
    // page marketing en 404. Cette épreuve fige la seule qui répond.
    var vues = new List<string>();
    var client = ClientAvec(vues, JetonPuis("""{"data":{"CHAINE":"valeur"}}"""));

    await client.LireLeSecretAsync("palier/dev", TestContext.Current.CancellationToken);

    Assert.Equal("https://www.ovh.com/auth/oauth2/token", vues[0]);
}

[Fact]
public async Task Chaque_appel_au_coffre_porte_le_jeton()
{
    var entetes = new List<string?>();
    var client = ClientAvec(entetes: entetes, JetonPuis("""{"data":{"CHAINE":"valeur"}}"""));

    await client.LireLeSecretAsync("palier/dev", TestContext.Current.CancellationToken);

    Assert.Equal("Bearer jeton-de-lepreuve", entetes[^1]);
}

[Fact]
public async Task Un_refus_du_coffre_ne_divulgue_ni_identifiant_ni_secret()
{
    var client = ClientAvec(reponse: new HttpResponseMessage(HttpStatusCode.Unauthorized));

    var faute = await Assert.ThrowsAsync<InvalidOperationException>(() =>
        client.LireLeSecretAsync("palier/dev", TestContext.Current.CancellationToken));

    Assert.Contains("coffre", faute.Message, StringComparison.OrdinalIgnoreCase);
    Assert.DoesNotContain("client-de-lepreuve", faute.Message, StringComparison.Ordinal);
    Assert.DoesNotContain("secret-de-lepreuve", faute.Message, StringComparison.Ordinal);
}

[Fact]
public async Task Une_cle_de_donnees_rend_32_octets_et_son_enveloppe()
{
    // Forme mesurée : { "key": "<JWE>", "plaintext": "<base64 de 32 octets>" }
    var client = ClientAvec(reponse: Json(
        """{"key":"eyJhbGciOiJkaXIi.aa.bb.cc","plaintext":"HcLxC2yurpdz4UFS8mNHVmxwNx6RaiLI5ultaBqfTJc="}"""));

    var (enveloppe, clair) = await client.CreerUneCleDeDonneesAsync(TestContext.Current.CancellationToken);

    Assert.Equal(32, clair.Length);
    Assert.StartsWith("eyJhbGciOiJkaXIi", enveloppe, StringComparison.Ordinal);
}
```

- [ ] **Étape 2 — Constater le ROUGE**

Run : `dotnet test back/Palier.Database.Tests --filter CoffreTests`
Attendu : ÉCHEC de compilation — `ClientOkms` n'existe pas.

- [ ] **Étape 3 — `ReglagesDuCoffre`, qui refuse ce qui manque**

```csharp
public sealed record ReglagesDuCoffre(
    string Endpoint, string OkmsId, string CleId, string ClientId, string ClientSecret)
{
    private static readonly string[] _requises =
        ["OKMS_ENDPOINT", "OKMS_ID", "OKMS_KEY_ID", "OKMS_CLIENT_ID", "OKMS_CLIENT_SECRET"];

    public static ReglagesDuCoffre Depuis(IConfiguration source)
    {
        var absentes = _requises.Where(c => string.IsNullOrWhiteSpace(source[c])).ToArray();
        if (absentes.Length > 0)
        {
            // Le NOM de la variable, jamais la valeur d'une autre.
            throw new InvalidOperationException(
                $"Le coffre ne peut pas s'ouvrir : {string.Join(", ", absentes)} "
                + "absente(s) de l'environnement. Voir back/.env.example.");
        }

        return new(source["OKMS_ENDPOINT"]!.TrimEnd('/'), source["OKMS_ID"]!,
                   source["OKMS_KEY_ID"]!, source["OKMS_CLIENT_ID"]!, source["OKMS_CLIENT_SECRET"]!);
    }

    public string Racine => $"{Endpoint}/api/{OkmsId}/v1";
}
```

- [ ] **Étape 4 — `ClientOkms`**

Trois appels, aucun renouvellement de jeton : le client meurt avec le démarrage (§ 5 de la spec).

```csharp
public sealed class ClientOkms(HttpClient http, ReglagesDuCoffre reglages)
{
    private const string _urlDuJeton = "https://www.ovh.com/auth/oauth2/token";
    private string? _jeton;

    private async Task<string> JetonAsync(CancellationToken jeton)
    {
        if (_jeton is not null) { return _jeton; }

        using var reponse = await http.PostAsync(_urlDuJeton, new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = reglages.ClientId,
                ["client_secret"] = reglages.ClientSecret,
                ["scope"] = "all",
            }), jeton).ConfigureAwait(false);

        // Le corps N'EST PAS journalisé : il porterait l'identifiant.
        if (!reponse.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Le coffre a refusé l'authentification ({(int)reponse.StatusCode}).");
        }

        var doc = JsonDocument.Parse(
            await reponse.Content.ReadAsStringAsync(jeton).ConfigureAwait(false));
        return _jeton = doc.RootElement.GetProperty("access_token").GetString()!;
    }
    // LireLeSecretAsync, CreerUneCleDeDonneesAsync, DeballerAsync suivent
    // le même motif : JetonAsync, appel, refus muet sur les valeurs.
}
```

Chemins REST, mesurés : `POST {racine}/servicekey/{cleId}/datakey` avec `{"size":256}` → `201 {key, plaintext}` ; `POST {racine}/servicekey/{cleId}/datakey/decrypt` avec `{"key":"<jwe>"}` → `200` ; `GET {racine}/secret/{chemin}/data` pour la lecture d'un secret.

- [ ] **Étape 5 — Vérifier le VERT**

Run : `dotnet test back/Palier.Database.Tests --filter CoffreTests`

- [ ] **Étape 6 — Commit**

```bash
git commit -am "écris le client OKMS, dont l'URL du jeton a été mesurée"
```

---

## Tâche 3 : Le fournisseur de configuration

**Fichiers :**

- Créer : `SourceDeConfigurationOkms.cs`, `FournisseurDeConfigurationOkms.cs`
- Modifier : `back/Palier.Database.Tests/CoffreTests.cs`

**Interfaces :**

- Consomme : `ClientOkms`, `ReglagesDuCoffre` (tâche 2).
- Produit : `IConfigurationBuilder.AjouterLeCoffre(string environnement)`.

- [ ] **Étape 1 — Épreuves ROUGES**

```csharp
[Fact]
public async Task Les_six_paires_du_coffre_alimentent_la_configuration()
{
    var configuration = new ConfigurationBuilder()
        .AjouterLeCoffreFactice(new Dictionary<string, string>
        {
            ["ConnectionStrings__Palier"] = "Host=x",
            ["ConnectionStrings__PalierAuth"] = "Host=y",
            ["ConnectionStrings__PalierMigrations"] = "Host=z",
            ["ConnectionStrings__PalierSauvegarde"] = "Host=w",
            ["JWT_SIGNING_KEY"] = new string('k', 32),
            ["GOOGLE_OAUTH_CLIENT_SECRET"] = "secret",
        })
        .Build();

    // Le point de tout le design : rien d'autre dans le backend ne change.
    Assert.Equal("Host=x", configuration.GetConnectionString("Palier"));
}

[Fact]
public void Une_cle_manquante_refuse_en_la_NOMMANT_sans_divulguer_les_autres()
{
    var incomplet = new Dictionary<string, string>
    {
        ["ConnectionStrings__Palier"] = "Host=x",
        ["ConnectionStrings__PalierAuth"] = "Host=y",
        ["ConnectionStrings__PalierMigrations"] = "Host=z",
        ["ConnectionStrings__PalierSauvegarde"] = "Host=w",
        ["GOOGLE_OAUTH_CLIENT_SECRET"] = "secret",
        // JWT_SIGNING_KEY manque volontairement.
    };

    var faute = Assert.Throws<InvalidOperationException>(
        () => new ConfigurationBuilder().AjouterLeCoffreFactice(incomplet).Build());

    Assert.Contains("JWT_SIGNING_KEY", faute.Message, StringComparison.Ordinal);
    Assert.DoesNotContain("Host=x", faute.Message, StringComparison.Ordinal);
}

[Fact]
public void Un_chemin_introuvable_refuse_en_nommant_le_chemin_et_l_environnement()
{
    var faute = Assert.Throws<InvalidOperationException>(
        () => new ConfigurationBuilder().AjouterLeCoffreQuiRend404("palier/prod").Build());

    Assert.Contains("palier/prod", faute.Message, StringComparison.Ordinal);
}
```

- [ ] **Étape 2 — ROUGE**, puis **étape 3 — implémenter**

`FournisseurDeConfigurationOkms : ConfigurationProvider` dont `Load()` appelle `LireLeSecretAsync` de façon synchrone (`GetAwaiter().GetResult()` — `Load` n'est pas asynchrone, et c'est le démarrage, pas une requête), vérifie les six clés attendues, et remplit `Data`.

Le délai maximal de dix secondes (§ 5) se pose sur le `HttpClient` : `Timeout = TimeSpan.FromSeconds(10)`.

- [ ] **Étape 4 — VERT**, puis **étape 5 — commit**

```bash
git commit -am "lis la configuration depuis le coffre, sans changer un seul appelant"
```

---

## Tâche 4 : Le trousseau et le chiffrement

**Fichiers :**

- Créer : `back/Palier.Infrastructure/Coffre/TrousseauDeChiffrement.cs`
- Modifier : `CoffreTests.cs`

**Interfaces :**

- Produit :

  ```csharp
  sealed class TrousseauDeChiffrement(IReadOnlyDictionary<Guid, byte[]> cles, Guid courante)
  {
      string Chiffrer(string clair, Guid proprietaire);
      string Dechiffrer(string valeur, Guid proprietaire);
      bool EstAJour(string valeur);   // porte-t-elle la clé courante ?
      static bool EstEnClair(string valeur) => !valeur.StartsWith("v1:", StringComparison.Ordinal);
  }
  ```

- [ ] **Étape 1 — Épreuves ROUGES**

```csharp
[Fact]
public void Un_aller_retour_rend_la_valeur_de_depart()
{
    var trousseau = TrousseauDeLEpreuve();
    var proprietaire = Guid.NewGuid();

    var chiffre = trousseau.Chiffrer("JBSWY3DPEHPK3PXP", proprietaire);

    Assert.StartsWith("v1:", chiffre, StringComparison.Ordinal);
    Assert.Equal("JBSWY3DPEHPK3PXP", trousseau.Dechiffrer(chiffre, proprietaire));
}

[Fact]
public void Un_secret_DEPLACE_vers_un_autre_utilisateur_ne_se_dechiffre_pas()
{
    // La raison d'être des données associées. Sans elles, un accès en
    // écriture à la base permettrait de se connecter comme n'importe qui
    // en recopiant le secret d'un compte vers le sien.
    var trousseau = TrousseauDeLEpreuve();
    var chiffre = trousseau.Chiffrer("JBSWY3DPEHPK3PXP", Guid.NewGuid());

    Assert.Throws<CryptographicException>(() => trousseau.Dechiffrer(chiffre, Guid.NewGuid()));
}

[Fact]
public void Une_valeur_chiffree_par_une_cle_ABSENTE_du_trousseau_refuse_clairement()
{
    var chiffre = $"v1:{Guid.NewGuid():N}:{Convert.ToBase64String(new byte[12])}:aaaa";

    var faute = Assert.Throws<InvalidOperationException>(
        () => TrousseauDeLEpreuve().Dechiffrer(chiffre, Guid.NewGuid()));

    Assert.Contains("clé de données", faute.Message, StringComparison.OrdinalIgnoreCase);
}

[Fact]
public void Une_valeur_sans_prefixe_est_reconnue_comme_du_clair()
{
    // Détection EXPLICITE, jamais heuristique sur l'alphabet Base32.
    Assert.True(TrousseauDeChiffrement.EstEnClair("JBSWY3DPEHPK3PXP"));
    Assert.False(TrousseauDeChiffrement.EstEnClair("v1:aa:bb:cc"));
}

[Fact]
public void Un_secret_porte_par_une_ANCIENNE_cle_se_dechiffre_mais_n_est_pas_a_jour()
{
    var (ancienne, courante) = (Guid.NewGuid(), Guid.NewGuid());
    var trousseau = new TrousseauDeChiffrement(
        new Dictionary<Guid, byte[]> { [ancienne] = Cle(1), [courante] = Cle(2) }, courante);
    var proprietaire = Guid.NewGuid();
    var vieux = new TrousseauDeChiffrement(
        new Dictionary<Guid, byte[]> { [ancienne] = Cle(1) }, ancienne)
        .Chiffrer("JBSWY3DPEHPK3PXP", proprietaire);

    Assert.Equal("JBSWY3DPEHPK3PXP", trousseau.Dechiffrer(vieux, proprietaire));
    Assert.False(trousseau.EstAJour(vieux));
}
```

- [ ] **Étape 2 — ROUGE**, puis **étape 3 — implémenter**

```csharp
public string Chiffrer(string clair, Guid proprietaire)
{
    var nonce = RandomNumberGenerator.GetBytes(12);
    var octets = Encoding.UTF8.GetBytes(clair);
    var chiffre = new byte[octets.Length];
    var etiquette = new byte[16];

    using var gcm = new AesGcm(cles[courante], 16);
    gcm.Encrypt(nonce, octets, chiffre, etiquette, proprietaire.ToByteArray());

    return $"v1:{courante:N}:{Convert.ToBase64String(nonce)}:"
         + Convert.ToBase64String([.. chiffre, .. etiquette]);
}
```

`Dechiffrer` découpe sur `:` en quatre morceaux, refuse tout ce qui n'en a pas quatre, cherche la clé au trousseau, et laisse remonter `CryptographicException` si l'étiquette ne colle pas — c'est le refus, il ne se déguise pas.

- [ ] **Étape 4 — VERT**, puis **étape 5 — commit**

```bash
git commit -am "chiffre en liant chaque secret à son propriétaire"
```

---

## Tâche 5 : Le chargement du trousseau au démarrage

**Fichiers :**

- Créer : `back/Palier.Infrastructure/Coffre/AmorcageDuTrousseau.cs`
- Modifier : `back/Palier.Database.Tests/ArchitectureTests.cs` — la troisième exemption **et son motif**
- Créer : `back/Palier.Database.Tests/AmorcageDuTrousseauTests.cs`

**Interfaces :**

- Consomme : `ClientOkms` (2), `TrousseauDeChiffrement` (4), `PalierDbContext.ClesDeDonnees` (1).
- Produit : `PorteurDeTrousseau`, singleton, dont `Trousseau` lève tant que l'amorçage n'a pas eu lieu.

**Pourquoi un porteur, et pas le trousseau directement.** Le trousseau n'existe qu'après `StartAsync` ; `GestionnaireDUtilisateurs` est résolu par requête, donc plus tard. Enregistrer le trousseau lui-même en singleton obligerait le conteneur à le construire au premier besoin — c'est-à-dire à faire des appels réseau pendant une requête d'utilisateur, exactement ce que le § 5 de la spec écarte. Le porteur est une case vide au démarrage, remplie une fois, lue ensuite :

```csharp
public sealed class PorteurDeTrousseau
{
    private TrousseauDeChiffrement? _trousseau;

    public TrousseauDeChiffrement Trousseau =>
        _trousseau ?? throw new InvalidOperationException(
            "Le trousseau est lu avant son chargement. `AmorcageDuTrousseau` "
            + "doit s'exécuter avant que le serveur accepte une requête.");

    public void Poser(TrousseauDeChiffrement trousseau) => _trousseau = trousseau;
}
```

Ce refus n'est pas décoratif : il transforme une erreur d'ordonnancement, qui se manifesterait autrement par un déchiffrement silencieusement impossible, en un message qui dit sa cause.

- [ ] **Étape 1 — Épreuves ROUGES**

```csharp
[Fact]
public async Task Une_table_de_cles_VIDE_refuse_le_demarrage_en_nommant_la_commande()
{
    var faute = await Assert.ThrowsAsync<InvalidOperationException>(
        () => DemarrerAvecLeCoffre(cles: []).StartAsync(TestContext.Current.CancellationToken));

    Assert.Contains("aucune clé de données", faute.Message, StringComparison.OrdinalIgnoreCase);
    Assert.Contains("poser-cle-de-donnees", faute.Message, StringComparison.Ordinal);
}

[Fact]
public async Task Une_enveloppe_que_le_coffre_REFUSE_arrete_le_demarrage()
{
    var faute = await Assert.ThrowsAsync<InvalidOperationException>(
        () => DemarrerAvecCoffreQuiRefuse().StartAsync(TestContext.Current.CancellationToken));

    // L'identifiant de l'enveloppe, JAMAIS son contenu.
    Assert.Contains("enveloppe", faute.Message, StringComparison.OrdinalIgnoreCase);
    Assert.DoesNotContain("eyJhbGciOiJkaXIi", faute.Message, StringComparison.Ordinal);
}

[Fact]
public async Task Toutes_les_cles_sont_chargees_et_la_PLUS_RECENTE_devient_courante()
{
    var trousseau = await ChargerAvecDeuxCles(anciennePuisRecente: true);

    var chiffre = trousseau.Chiffrer("x", Guid.Empty);
    Assert.True(trousseau.EstAJour(chiffre));
}
```

- [ ] **Étape 2 — ROUGE**, puis **étape 3 — implémenter le service hébergé**

`AmorcageDuTrousseau : IHostedService` : `StartAsync` lit `ClesDeDonnees` triées par `CreeeLe`, refuse si vide, appelle `DeballerAsync` sur chacune, construit le trousseau. `StopAsync` ne fait rien.

- [ ] **Étape 4 — L'exemption d'architecture, avec son motif**

Dans `ArchitectureTests`, ajouter à `_exemptionsNommees` et, **au-dessus**, le motif rédigé — la règle exige les deux :

```csharp
/// <c>Palier.Infrastructure.Coffre.AmorcageDuTrousseau</c> — il lit
/// `cles_de_donnees` AVANT que le serveur accepte une requête, donc sans
/// identité à poser, exactement comme <c>LecteurDeSocle</c>. Motif
/// vérifiable ligne à ligne : il ne touche que cette table, qui ne porte
/// AUCUNE donnée personnelle — des enveloppes chiffrées, illisibles sans le
/// coffre. Il lit et n'écrit jamais : `palier_app` n'a pas `INSERT` sur
/// cette table, et aucune politique d'écriture n'existe (tâche 1).
"Palier.Infrastructure.Coffre.AmorcageDuTrousseau",
```

- [ ] **Étape 5 — Vérifier le VERT, exemptions comprises**

Run : `dotnet test back/Palier.Database.Tests --filter "AmorcageDuTrousseauTests|ArchitectureTests"`
Attendu : tout passe, **y compris** `Chaque_exemption_nommee_designe_un_type_qui_EXISTE_ENCORE`.

- [ ] **Étape 6 — Commit**

```bash
git commit -am "charge le trousseau au démarrage, et refuse si aucune clé n'est posée"
```

---

## Tâche 6 : La commande `poser-cle-de-donnees`

**Fichiers :**

- Créer : `back/Palier.Api/Outils/PoserUneCleDeDonnees.cs`
- Modifier : `back/Palier.Api/Program.cs`
- Modifier : `back/Palier.Database.Tests/CleDeDonneesTests.cs`

- [ ] **Étape 1 — Épreuves ROUGES**

```csharp
[Fact]
public async Task La_commande_pose_une_cle_et_ne_demarre_PAS_le_serveur()
{
    var code = await PoserUneCleDeDonnees.ExecuterAsync(
        _base.ChaineMigrations, CoffreFactice(), TestContext.Current.CancellationToken);

    Assert.Equal(0, code);
    await using var contexte = _base.Contexte(_base.ChaineApp);
    Assert.Single(await contexte.ClesDeDonnees.ToListAsync());
}

[Fact]
public async Task Rejouer_la_commande_AJOUTE_une_cle_sans_toucher_a_la_precedente()
{
    // C'est la rotation : les anciens secrets doivent rester lisibles.
    await PoserDeuxFois();

    await using var contexte = _base.Contexte(_base.ChaineApp);
    var cles = await contexte.ClesDeDonnees.OrderBy(c => c.CreeeLe).ToListAsync();
    Assert.Equal(2, cles.Count);
    Assert.NotEqual(cles[0].Enveloppe, cles[1].Enveloppe);
}
```

- [ ] **Étape 2 — ROUGE**, puis **étape 3 — implémenter**

Dans `Program.cs`, **avant** de construire l'hôte — la commande ne doit rien démarrer :

```csharp
if (args is ["poser-cle-de-donnees", ..])
{
    var amorcage = new ConfigurationBuilder().AddEnvironmentVariables().Build();
    using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
    return await PoserUneCleDeDonnees.ExecuterAsync(
        amorcage.GetConnectionString("PalierMigrations")
            ?? throw new InvalidOperationException(
                "ConnectionStrings__PalierMigrations est absente : la commande "
                + "écrit avec le rôle propriétaire, elle ne peut pas s'en passer."),
        new ClientOkms(http, ReglagesDuCoffre.Depuis(amorcage)),
        CancellationToken.None);
}
```

La chaîne vient de l'environnement et non du coffre : au premier passage, aucune clé n'existe, donc le fournisseur de configuration refuserait. C'est le seul chemin du produit qui lit une chaîne de connexion hors du coffre, et c'est ce qui le rend capable d'amorcer une base vierge.

`ExecuterAsync` appelle `CreerUneCleDeDonneesAsync`, insère l'enveloppe, rend `0`. Elle ne garde **pas** la clé en clair : elle n'en a aucun usage.

- [ ] **Étape 4 — VERT**, puis **étape 5 — commit**

```bash
git commit -am "ajoute la commande qui pose une clé de données, hors du démarrage"
```

---

## Tâche 7 : Le chiffrement du secret TOTP

**Fichiers :**

- Modifier : `back/Palier.Infrastructure/Identite/GestionnaireDUtilisateurs.cs`
- Modifier : `back/Palier.Database.Tests/DeuxFacteursTests.cs`

**Interfaces :**

- Consomme : `PorteurDeTrousseau` (5), injecté au constructeur de `GestionnaireDUtilisateurs` — **le porteur, pas le trousseau** : celui-ci n'existe pas encore quand le conteneur se construit. Dans le corps, `porteur.Trousseau`.
- Vérifié à la source (`Microsoft.Extensions.Identity.Core` 10.0.0) : `GetAuthenticatorKeyAsync` et `ResetAuthenticatorKeyAsync` sont `public virtual` ; `SetAuthenticatorKeyAsync` **n'existe pas** sur `UserManager` — d'où le passage par `SetAuthenticationTokenAsync`.

- [ ] **Étape 1 — Épreuves ROUGES**

```csharp
[Fact]
public async Task L_enrolement_n_ecrit_JAMAIS_la_cle_en_clair_en_base()
{
    var utilisateur = await Inscrire();
    await _utilisateurs.ResetAuthenticatorKeyAsync(utilisateur);

    // On lit la ligne brute, pas ce que le gestionnaire veut bien rendre.
    var brut = await LireLeJetonBrut(utilisateur.Id, "AuthenticatorKey");

    Assert.StartsWith("v1:", brut, StringComparison.Ordinal);
    Assert.NotEqual(await _utilisateurs.GetAuthenticatorKeyAsync(utilisateur), brut);
}

[Fact]
public async Task La_verification_d_un_code_TOTP_fonctionne_apres_chiffrement()
{
    // Le chemin complet : AuthenticatorTokenProvider passe par
    // GetAuthenticatorKeyAsync, qu'on a surchargé. S'il ne déchiffrait pas,
    // aucun code ne serait jamais valide.
    var utilisateur = await Inscrire();
    await _utilisateurs.ResetAuthenticatorKeyAsync(utilisateur);
    var cle = await _utilisateurs.GetAuthenticatorKeyAsync(utilisateur);

    Assert.True(await _utilisateurs.VerifyTwoFactorTokenAsync(
        utilisateur, _utilisateurs.Options.Tokens.AuthenticatorTokenProvider, CodeTotp(cle!)));
}

[Fact]
public async Task Une_cle_HERITEE_en_clair_est_migree_au_premier_contact()
{
    var utilisateur = await Inscrire();
    await EcrireLeJetonBrut(utilisateur.Id, "AuthenticatorKey", "JBSWY3DPEHPK3PXP");

    var rendue = await _utilisateurs.GetAuthenticatorKeyAsync(utilisateur);

    Assert.Equal("JBSWY3DPEHPK3PXP", rendue);
    Assert.StartsWith("v1:", await LireLeJetonBrut(utilisateur.Id, "AuthenticatorKey"),
        StringComparison.Ordinal);
}

[Fact]
public async Task Une_cle_portee_par_une_ANCIENNE_cle_de_donnees_est_rechiffree()
{
    var utilisateur = await AvecSecretChiffreParUneCleAncienne();

    await _utilisateurs.GetAuthenticatorKeyAsync(utilisateur);

    Assert.True(_trousseau.EstAJour(await LireLeJetonBrut(utilisateur.Id, "AuthenticatorKey")));
}
```

- [ ] **Étape 2 — ROUGE**, puis **étape 3 — les deux surcharges**

```csharp
public override async Task<IdentityResult> ResetAuthenticatorKeyAsync(Utilisateur utilisateur)
{
    // On n'appelle PAS base : il écrirait la clé en clair avant de rendre la
    // main, et le journal d'écriture anticipée en garderait la trace bien
    // après l'écrasement de la ligne.
    var cle = GenerateNewAuthenticatorKey();
    await SetAuthenticationTokenAsync(
        utilisateur, _fournisseurDIdentity, _nomDuJetonAuthentificateur,
        _trousseau.Chiffrer(cle, utilisateur.Id)).ConfigureAwait(false);
    return await UpdateSecurityStampAsync(utilisateur).ConfigureAwait(false);
}

public override async Task<string?> GetAuthenticatorKeyAsync(Utilisateur utilisateur)
{
    var valeur = await GetAuthenticationTokenAsync(
        utilisateur, _fournisseurDIdentity, _nomDuJetonAuthentificateur).ConfigureAwait(false);
    if (valeur is null) { return null; }

    var clair = TrousseauDeChiffrement.EstEnClair(valeur)
        ? valeur
        : _trousseau.Dechiffrer(valeur, utilisateur.Id);

    // Le même geste migre le clair hérité ET les anciennes clés de données.
    if (TrousseauDeChiffrement.EstEnClair(valeur) || !_trousseau.EstAJour(valeur))
    {
        await SetAuthenticationTokenAsync(
            utilisateur, _fournisseurDIdentity, _nomDuJetonAuthentificateur,
            _trousseau.Chiffrer(clair, utilisateur.Id)).ConfigureAwait(false);
    }

    return clair;
}
```

Ajouter la constante `private const string _nomDuJetonAuthentificateur = "AuthenticatorKey";` à côté de `_nomDuJetonDIdentity`.

- [ ] **Étape 4 — VERT**, épreuves du lot 4 comprises

Run : `dotnet test back/Palier.Database.Tests --filter DeuxFacteursTests`
Attendu : les épreuves existantes de la 2FA passent **sans modification** — si l'une d'elles doit changer, c'est le signe que la surcharge a cassé un comportement, pas que l'épreuve était fausse.

- [ ] **Étape 5 — Commit**

```bash
git commit -am "chiffre le secret TOTP, que l'audit avait laissé en clair"
```

---

## Tâche 8 : Le branchement, les six refus, les seuils

**Fichiers :**

- Modifier : `back/Palier.Api/Program.cs`, `back/Palier.Api/Composition.cs`
- Modifier : `back/Palier.Database.Tests/Palier.Database.Tests.csproj` (seuils)
- Modifier : `docs/16-projet.md` (les cinq variables et la commande)

- [ ] **Étape 1 — Épreuve ROUGE : les six refus, chacun provoqué**

Une épreuve par cause, deux assertions chacune. C'est la règle des garde-fous du lot 1 : _un contrôle dont on n'a jamais vu le rouge ne protège rien_.

```csharp
[Theory]
[InlineData("OKMS_ENDPOINT")]
[InlineData("OKMS_ID")]
[InlineData("OKMS_KEY_ID")]
[InlineData("OKMS_CLIENT_ID")]
[InlineData("OKMS_CLIENT_SECRET")]
public void Chaque_variable_d_amorcage_ABSENTE_refuse_le_demarrage(string absente)
{
    var faute = Assert.Throws<InvalidOperationException>(
        () => ReglagesDuCoffre.Depuis(ConfigurationSans(absente)));

    Assert.Contains(absente, faute.Message, StringComparison.Ordinal);
    Assert.Contains(".env.example", faute.Message, StringComparison.Ordinal);
}
```

- [ ] **Étape 2 — Brancher**

```csharp
builder.Configuration.AjouterLeCoffre(builder.Environment.EnvironmentName);
builder.Services.AddHostedService<AmorcageDuTrousseau>();
```

- [ ] **Étape 3 — Faire tourner tout le harnais**

Run : `npm run verify`
Attendu : code 0. Les 411 épreuves du dépôt passent, les nouvelles comprises.

- [ ] **Étape 4 — Relever les seuils si la couverture a monté**

Lire le rapport, ajuster `<Threshold>` dans `Palier.Database.Tests.csproj` au niveau atteint. Un seuil laissé sous le niveau réel cesse de détecter le code mort public — c'est la raison d'être du seuil.

- [ ] **Étape 5 — Commit et push**

```bash
git commit -am "branche le coffre au démarrage et éprouve ses six refus"
GIT_SSH_COMMAND="ssh -o ServerAliveInterval=20 -o ServerAliveCountMax=60" \
  git push -u origin coffre/secrets-okms
```

---

## Ce que ce plan laisse au porteur du projet

- **Les identifiants du coffre en intégration continue** (point ouvert n° 1 de la spec). Recommandation : les épreuves qui parlent au vrai coffre tournent au `pre-push` local, pas dans GitHub Actions — y mettre les identifiants donnerait à un fournisseur américain l'accès au coffre européen, et la politique IAM ne sait pas restreindre par chemin.
- **Écrire les six paires dans `palier/dev` et `palier/prod`** par la console OVHcloud, une fois. Le plan ne peut pas le faire : il n'a pas les valeurs de production.
- **La rotation du WAL** après la migration des secrets déjà en clair (point ouvert n° 2).
- **`/code-review ultra`** en fin de branche — il ne se lance que par lui.
