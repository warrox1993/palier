# Lot 4 — socle de session : plan d'implémentation

> **Pour les exécutants :** SOUS-SKILL REQUISE — `superpowers:executing-plans`. Les étapes utilisent des cases à cocher (`- [ ]`).

**But :** livrer l'authentification par email et mot de passe, avec un magasin de sessions à rotation, sans dépendre d'aucun tiers externe.

**Architecture :** un quatrième rôle PostgreSQL `palier_auth` porte le seul chemin d'accès aux tables d'identité, fermées par D38. La logique de rotation vit dans `Palier.Application` — pure et testable à 100 % — et son magasin dans `Palier.Infrastructure`. Chaque accès nouveau s'accompagne du garde-fou qui le surveille.

**Pile :** .NET 10, ASP.NET Identity 10.0.11, EF Core, PostgreSQL, xUnit, Testcontainers.

**Spec :** `docs/superpowers/specs/2026-08-21-lot-4-socle-de-session-design.md`

## Contraintes globales

- **`palier_auth` ne possède aucune table et n'a pas `bypassrls`** — mêmes propriétés que `palier_app`, vérifiées par l'assertion de démarrage.
- **`palier_app` ne gagne aucun privilège** sur les tables d'identité ni sur `sessions_refresh`. L'épreuve 8 d'`IsolationTests` reste verte telle quelle.
- **Toute table de `public` a RLS `enable` ET `force`** — l'API refuse de démarrer sinon.
- **Aucune valeur secrète dans le dépôt.** Les chaînes vivent en variable d'environnement ; `.gitleaks.regles.toml` refuse un fichier suivi qui en porterait une.
- **Aucune chaîne destinée à un utilisateur** dans le backend : codes et énumérations, les libellés vivent en base.
- **Le domaine reste en français**, l'infrastructure en anglais quand elle miroite le schéma.
- **Namespaces file-scoped**, `CA1707` éteinte pour les tests, préfixe `_` sur les champs statiques privés (`IDE1006`).
- **`public const decimal` interdit** — utiliser une propriété statique : une constante décimale engendre un constructeur statique mort qui fait chuter la couverture (leçon du lot 3).
- **Commandes :** `dotnet test back/Palier.sln --settings back/coverage.runsettings` ; `npm run verify` pour les seize étapes.

---

## Structure des fichiers

```
db/amorcage/01-roles.sql                       + palier_auth, ses grants
back/Palier.Application/
├── Pipeline/Pipeline.cs                       (existe)
└── Sessions/
    ├── DecisionDeRotation.cs                  la logique PURE, 100 % couverte
    ├── ResultatDeRotation.cs                  les quatre issues
    └── ParametresDeSession.cs                 durées et fenêtre de grâce
back/Palier.Infrastructure/
├── Identite/Utilisateur.cs                    (existe)
├── Identite/SessionRafraichissement.cs        l'entité
├── Identite/PalierAuthDbContext.cs            le contexte du rôle auth
├── Identite/MagasinDeSessions.cs              l'adaptateur
├── Identite/HachageDeJeton.cs                 SHA-256
└── Migrations/…_SessionsEtRolesAuth.cs        table, RLS, privilèges
back/Palier.Api/
├── Composition.cs                             (modifié)
├── Socle/AssertionDIsolation.cs               (modifié — contrôle palier_auth)
├── Auth/PointsDEntree.cs                      inscription, connexion, refresh…
├── Auth/JetonDAcces.cs                        JWT
├── Auth/CookieDeRafraichissement.cs           attributs du cookie
├── Auth/IdentiteDepuisJeton.cs                IIdentiteDemandeur réel
└── Auth/Limitation.cs                         ForwardedHeaders + rate limiter
back/Palier.Application.Tests/                 NOUVEAU — seuil 100 %
back/Palier.Infrastructure.Tests/              NOUVEAU — seuil à la mesure
back/Palier.Api.Tests/                         NOUVEAU — intégration, Testcontainers
```

---

## Tâche 1 : le quatrième rôle

**Fichiers :** modifier `db/amorcage/01-roles.sql`, `back/.env.example` · tester `back/Palier.Database.Tests/RolesTests.cs`

**Produit :** le rôle `palier_auth` et la chaîne `ConnectionStrings__PalierAuth`.

- [ ] **Étape 1 — écrire les épreuves qui échouent**, dans `RolesTests.cs`, sur le modèle des trois rôles existants :

```csharp
[Fact]
public async Task Le_role_auth_existe_et_ne_contourne_pas_RLS()
{
    var (existe, bypass, superuser) = await ProprietesDuRole("palier_auth");
    Assert.True(existe, "le rôle palier_auth est absent de l'amorçage");
    Assert.False(bypass, "palier_auth ne doit PAS porter bypassrls");
    Assert.False(superuser, "palier_auth ne doit PAS être superutilisateur");
}

[Fact]
public async Task Le_role_auth_ne_possede_aucune_table()
{
    var possedees = await TablesPossedeesPar("palier_auth");
    Assert.True(possedees.Count == 0,
        $"palier_auth possède {possedees.Count} table(s) : {string.Join(", ", possedees)}. "
        + "Un propriétaire contourne ses propres politiques.");
}

[Fact]
public async Task Le_role_auth_n_a_aucun_privilege_sur_les_donnees_de_sante()
{
    // workouts, sets, body_weight : le rôle d'authentification n'a rien à y faire.
    foreach (var table in new[] { "workouts", "sets", "body_weight" })
    {
        var refus = await Assert.ThrowsAsync<PostgresException>(async () =>
        {
            await using var c = await Ouvrir(baseDeDonnees.ChaineAuth);
            await using var cmd = new NpgsqlCommand($"select count(*) from public.{table}", c);
            await cmd.ExecuteScalarAsync();
        });
        Assert.True(refus.SqlState == "42501",
            $"palier_auth atteint `{table}` : attendu 42501, reçu {refus.SqlState}");
    }
}
```

- [ ] **Étape 2 — lancer, constater le rouge.** `dotnet test back/Palier.Database.Tests` → échec : le rôle n'existe pas.

- [ ] **Étape 3 — ajouter le rôle** à `db/amorcage/01-roles.sql`, à la suite des trois autres, avec le commentaire qui dit pourquoi il existe :

```sql
-- `palier_auth` — LE SEUL CHEMIN vers les tables d'identité, fermées par D38.
-- Il ne possède rien, ne contourne pas RLS, et n'a AUCUN privilège sur les
-- données de santé : sa seule fonction est de reconnaître qui se présente.
create role palier_auth
  login password :'mdp_auth'
  nosuperuser nobypassrls nocreatedb nocreaterole;

grant connect on database palier to palier_auth;
grant usage on schema public to palier_auth;
```

- [ ] **Étape 4 — déclarer la chaîne** dans `back/.env.example`, sous la section authentification, sans valeur :

```
# `PalierAuth` — le rôle palier_auth, SEUL chemin vers les tables d'identité
# (D38). Il n'a aucun privilège sur workouts, sets ni body_weight.
ConnectionStrings__PalierAuth=
```

- [ ] **Étape 5 — étendre `BaseFixture`** pour exposer `ChaineAuth`, sur le modèle de `ChaineApp`.

- [ ] **Étape 6 — vert.** - [ ] **Étape 7 — commiter** : `git commit -m "ouvre un quatrième rôle pour le seul chemin des tables d'identité"`

---

## Tâche 2 : la table des sessions, ses politiques et ses privilèges

**Fichiers :** créer `back/Palier.Infrastructure/Identite/SessionRafraichissement.cs`, une migration EF · tester `back/Palier.Database.Tests/IsolationTests.cs`, `SchemaTests.cs`

**Consomme :** le rôle de la tâche 1.
**Produit :** la table `sessions_refresh` et l'accès de `palier_auth` aux tables `AspNet*`.

**L'entité** (anglais pour les colonnes, miroir du schéma — même règle que `Entites.cs`) :

```csharp
public sealed class SessionRafraichissement
{
    public Guid Id { get; set; }
    public Guid OwnerId { get; set; }
    public required byte[] TokenHash { get; set; }   // SHA-256, 32 octets
    public Guid FamilyId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? ConsumedAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public Guid? ReplacedById { get; set; }
    public string? Device { get; set; }
    public DateTimeOffset LastSeenAt { get; set; }
}
```

- [ ] **Étape 1 — écrire les épreuves qui échouent** dans `IsolationTests.cs` :

```csharp
[Fact]
public async Task Sur_sessions_refresh_palier_app_n_a_aucun_privilege()
{
    var refus = await Assert.ThrowsAsync<PostgresException>(async () =>
    {
        await using var c = await Ouvrir(baseDeDonnees.ChaineApp);
        await using var cmd = new NpgsqlCommand("select count(*) from public.sessions_refresh", c);
        await cmd.ExecuteScalarAsync();
    });
    Assert.True(refus.SqlState == "42501",
        $"palier_app lit sessions_refresh : attendu 42501, reçu {refus.SqlState}");
}

[Fact]
public async Task Sur_sessions_refresh_palier_auth_lit_et_ecrit()
{
    var utilisateur = await Utilisateur();
    await using var c = await Ouvrir(baseDeDonnees.ChaineAuth);
    await using var insert = new NpgsqlCommand(
        """
        insert into public.sessions_refresh
          (id, owner_id, token_hash, family_id, created_at, expires_at, last_seen_at)
        values (gen_random_uuid(), $1, $2, gen_random_uuid(), now(), now() + interval '14 days', now())
        """, c);
    insert.Parameters.AddWithValue(utilisateur);
    insert.Parameters.AddWithValue(new byte[32]);
    Assert.Equal(1, await insert.ExecuteNonQueryAsync());
}

[Fact]
public async Task Sur_AspNetUsers_palier_auth_lit_desormais()
{
    await Utilisateur();
    var vues = await Compter("""select count(*) from public."AspNetUsers" """, baseDeDonnees.ChaineAuth);
    Assert.True(vues > 0, "palier_auth ne lit pas AspNetUsers : le chemin de connexion est fermé");
}

[Fact]
public async Task La_suppression_d_un_utilisateur_emporte_ses_sessions()
{
    // RGPD article 17 : les sessions ne survivent pas au compte.
    var utilisateur = await UtilisateurAvecSession();
    await SupprimerUtilisateur(utilisateur);
    var restantes = await Compter(
        $"select count(*) from public.sessions_refresh where owner_id = '{utilisateur}'",
        baseDeDonnees.ChaineAdministrateur);
    Assert.True(restantes == 0, $"{restantes} session(s) survivent à leur utilisateur");
}
```

- [ ] **Étape 2 — rouge.**

- [ ] **Étape 3 — écrire la migration.** `dotnet ef migrations add SessionsEtRolesAuth`, puis compléter avec `migrationBuilder.Sql` (D14) :

```sql
alter table public.sessions_refresh enable row level security;
alter table public.sessions_refresh force row level security;

create policy authentification on public.sessions_refresh
  for all to palier_auth using (true) with check (true);

grant select, insert, update, delete on public.sessions_refresh to palier_auth;

-- Les tables d'identité s'ouvrent à palier_auth, et à lui seul.
create policy authentification on public."AspNetUsers"
  for all to palier_auth using (true) with check (true);
-- idem AspNetUserTokens, AspNetUserLogins, AspNetUserClaims, AspNetUserRoles

grant select, insert, update, delete on
  public."AspNetUsers", public."AspNetUserTokens", public."AspNetUserLogins",
  public."AspNetUserClaims", public."AspNetUserRoles"
  to palier_auth;
```

La clé étrangère porte `on delete cascade` vers `AspNetUsers("Id")`. Index sur `token_hash` (unique) et sur `owner_id`.

- [ ] **Étape 4 — vert, puis vérifier que l'épreuve 8 est TOUJOURS verte** : `palier_app` doit rester à zéro privilège sur `AspNet*`. Si elle rougit, un `grant` a débordé.

- [ ] **Étape 5 — commiter** : `git commit -m "ouvre les tables d'identité au seul rôle d'authentification"`

---

## Tâche 3 : l'assertion de démarrage contrôle le nouveau rôle

**Fichiers :** modifier `back/Palier.Api/Socle/LecteurDeSocle.cs`, `AssertionDIsolation.cs` · tester `back/Palier.Database.Tests/AssertionDemarrageTests.cs`

**Pourquoi cette tâche existe :** ajouter un accès sans étendre le contrôle qui le surveille est l'erreur que le lot 1 a payée quatre fois. Les trois requêtes de D37 s'appliquent désormais aux **deux** rôles.

- [ ] **Étape 1 — écrire l'épreuve qui échoue** :

```csharp
[Fact]
public async Task L_API_refuse_de_servir_si_palier_auth_contourne_RLS()
{
    await AvecRoleModifie("palier_auth", "bypassrls", async () =>
    {
        var ex = await Assert.ThrowsAsync<IsolationNonGarantieException>(
            () => AssertionSur(baseDeDonnees.ChaineApp).VerifierAsync());
        Assert.Contains("palier_auth", ex.Cause, StringComparison.Ordinal);
    });
}

[Fact]
public async Task L_API_refuse_de_servir_si_palier_auth_possede_une_table()
{
    await AvecTablePossedeePar("palier_auth", async () =>
    {
        var ex = await Assert.ThrowsAsync<IsolationNonGarantieException>(
            () => AssertionSur(baseDeDonnees.ChaineApp).VerifierAsync());
        Assert.Contains("palier_auth", ex.Cause, StringComparison.Ordinal);
    });
}
```

- [ ] **Étape 2 — rouge** : l'assertion ne regarde qu'un rôle.
- [ ] **Étape 3 — étendre `LecteurDeSocle`** pour interroger les deux rôles, et `AssertionDIsolation` pour nommer le rôle fautif dans la cause.
- [ ] **Étape 4 — vert.**
- [ ] **Étape 5 — franchissement réel** : rendre `palier_auth` `bypassrls` sur la base de test, démarrer l'API, vérifier qu'elle **n'écoute pas** (`grep -c "Now listening" → 0`), puis rétablir.
- [ ] **Étape 6 — commiter** : `git commit -m "étend le refus de servir au rôle d'authentification"`

---

## Tâche 4 : le contexte du rôle auth, et son garde-fou d'architecture

**Fichiers :** créer `back/Palier.Infrastructure/Identite/PalierAuthDbContext.cs` · modifier `back/Palier.Database.Tests/ArchitectureTests.cs`

**Le piège à éviter :** `ArchitectureTests` surveille qui prend `PalierDbContext`. Un contexte **nouveau** échapperait au contrôle par construction — c'est-à-dire qu'on ouvrirait une seconde porte en croyant n'en surveiller qu'une.

**Produit :**

```csharp
public sealed class PalierAuthDbContext(DbContextOptions<PalierAuthDbContext> options)
    : IdentityDbContext<Utilisateur, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<SessionRafraichissement> Sessions => Set<SessionRafraichissement>();
}
```

**Il ne génère aucune migration** : le schéma appartient à `PalierDbContext`. `FabriqueDeConception` ne le référence pas.

- [ ] **Étape 1 — écrire l'épreuve qui échoue** :

```csharp
[Fact]
public void Seul_le_magasin_de_sessions_prend_PalierAuthDbContext()
{
    var fautifs = TypesPrenantEnDependance("Palier.Infrastructure.Identite.PalierAuthDbContext")
        .Where(t => !_exemptionsAuth.Contains(t.FullName)).ToArray();
    Assert.True(fautifs.Length == 0,
        $"{fautifs.Length} type(s) prennent PalierAuthDbContext hors magasin : "
        + string.Join(", ", fautifs.Select(t => t.FullName)));
}

[Fact]
public void Chaque_exemption_auth_designe_un_type_qui_EXISTE_ENCORE()
{
    foreach (var nom in _exemptionsAuth)
        Assert.True(TypeExiste(nom),
            $"L'exemption « {nom} » ne désigne plus rien. Une exemption qui survit à sa "
            + "cible couvre, en silence, tout ce qui reprendrait ce nom.");
}
```

- [ ] **Étape 2 — rouge.** - [ ] **Étape 3 — créer le contexte et l'exemption nommée**, avec son écriteau :

```csharp
/// <c>Palier.Infrastructure.Identite.MagasinDeSessions</c> — le seul type
/// autorisé à prendre le contexte du rôle d'authentification. Motif : il porte
/// le chemin que D38 laissait à concevoir, et il n'atteint AUCUNE table de
/// donnée de santé — palier_auth n'y a aucun privilège, vérifié en tâche 1.
private static readonly string[] _exemptionsAuth =
    ["Palier.Infrastructure.Identite.MagasinDeSessions"];
```

- [ ] **Étape 4 — vert.** - [ ] **Étape 5 — commiter** : `git commit -m "surveille le second contexte comme le premier"`

---

## Tâche 5 : le projet de tests de l'application, et son seuil

**Fichiers :** créer `back/Palier.Application.Tests/Palier.Application.Tests.csproj` · modifier `back/Palier.sln`

**Pourquoi maintenant :** `CLAUDE.md` § 4 — « chaque projet qui acquiert des tests acquiert son seuil dans le même geste ». Créer le projet sans son seuil laisserait le trou ouvert pour toutes les tâches suivantes.

- [ ] **Étape 1 — créer le projet** sur le modèle exact de `Palier.Domain.Tests.csproj`, avec le bloc de couverture :

```xml
<PropertyGroup>
  <CollectCoverage>true</CollectCoverage>
  <CoverletOutputFormat>cobertura</CoverletOutputFormat>
  <Include>[Palier.Application]*</Include>
  <Threshold>100</Threshold>
  <ThresholdType>line,branch,method</ThresholdType>
  <ThresholdStat>total</ThresholdStat>
</PropertyGroup>
```

- [ ] **Étape 2 — l'ajouter à la solution**, puis vérifier que `dotnet build back/Palier.sln` passe.
- [ ] **Étape 3 — vérifier que le seuil MORD** : ajouter une méthode publique non testée dans `Palier.Application`, lancer, constater l'échec, la retirer.
- [ ] **Étape 4 — commiter** : `git commit -m "donne un seuil de couverture au projet applicatif dès sa naissance"`

---

## Tâche 6 : la décision de rotation, pure et couverte à 100 %

**Fichiers :** créer `back/Palier.Application/Sessions/{DecisionDeRotation,ResultatDeRotation,ParametresDeSession}.cs` · tester `back/Palier.Application.Tests/Sessions/DecisionDeRotationTests.cs`

**C'est le cœur du lot, et il est pur** — aucune base, aucun réseau, donc 100 % atteignable.

**Produit :**

```csharp
public enum IssueDeRotation { Acceptee, RejeuDansLaGrace, ReemploiDetecte, Inconnue, Expiree, Revoquee }

public sealed record EtatDeSession(
    Guid Id, Guid FamilleId, DateTimeOffset ExpireLe,
    DateTimeOffset? ConsommeeLe, DateTimeOffset? RevoqueeLe, Guid? RemplaceePar);

public sealed record ResultatDeRotation(IssueDeRotation Issue, Guid? JetonARendre, bool RevoquerLaFamille);

public static class ParametresDeSession
{
    public static TimeSpan DureeDuJetonDAcces => TimeSpan.FromMinutes(15);
    public static TimeSpan DureeDuRafraichissement => TimeSpan.FromDays(14);
    public static TimeSpan FenetreDeGrace => TimeSpan.FromSeconds(30);
}

public static class DecisionDeRotation
{
    public static ResultatDeRotation Decider(EtatDeSession? session, DateTimeOffset maintenant);
}
```

- [ ] **Étape 1 — écrire les épreuves qui échouent.** Une par branche, sinon le seuil refuse :

```csharp
private static readonly DateTimeOffset _t = new(2026, 8, 21, 12, 0, 0, TimeSpan.Zero);
private static EtatDeSession Session(DateTimeOffset? consommee = null, DateTimeOffset? revoquee = null,
    Guid? remplacee = null, DateTimeOffset? expire = null) =>
    new(Guid.NewGuid(), Guid.NewGuid(), expire ?? _t.AddDays(1), consommee, revoquee, remplacee);

[Fact]
public void Un_jeton_inconnu_est_refuse() =>
    Assert.Equal(IssueDeRotation.Inconnue, DecisionDeRotation.Decider(null, _t).Issue);

[Fact]
public void Un_jeton_valide_est_accepte() =>
    Assert.Equal(IssueDeRotation.Acceptee, DecisionDeRotation.Decider(Session(), _t).Issue);

[Fact]
public void Un_jeton_expire_est_refuse() =>
    Assert.Equal(IssueDeRotation.Expiree,
        DecisionDeRotation.Decider(Session(expire: _t.AddSeconds(-1)), _t).Issue);

[Fact]
public void Un_jeton_revoque_est_refuse() =>
    Assert.Equal(IssueDeRotation.Revoquee,
        DecisionDeRotation.Decider(Session(revoquee: _t.AddMinutes(-5)), _t).Issue);

// LA branche qui protège les innocents : deux onglets, un rejeu réseau.
[Fact]
public void Un_rejeu_dans_les_trente_secondes_rend_le_jeton_suivant()
{
    var suivant = Guid.NewGuid();
    var r = DecisionDeRotation.Decider(
        Session(consommee: _t.AddSeconds(-29), remplacee: suivant), _t);
    Assert.Equal(IssueDeRotation.RejeuDansLaGrace, r.Issue);
    Assert.Equal(suivant, r.JetonARendre);
    Assert.False(r.RevoquerLaFamille);
}

// LA branche qui attrape les voleurs.
[Fact]
public void Un_rejeu_au_dela_de_la_grace_revoque_toute_la_famille()
{
    var r = DecisionDeRotation.Decider(
        Session(consommee: _t.AddSeconds(-31), remplacee: Guid.NewGuid()), _t);
    Assert.Equal(IssueDeRotation.ReemploiDetecte, r.Issue);
    Assert.True(r.RevoquerLaFamille);
    Assert.Null(r.JetonARendre);
}

[Fact]
public void La_borne_exacte_de_trente_secondes_reste_dans_la_grace() =>
    Assert.Equal(IssueDeRotation.RejeuDansLaGrace,
        DecisionDeRotation.Decider(Session(consommee: _t.AddSeconds(-30), remplacee: Guid.NewGuid()), _t).Issue);

// Consommée sans remplaçant : la chaîne est cassée, on ne peut rien rendre.
[Fact]
public void Un_jeton_consomme_sans_remplacant_est_un_reemploi() =>
    Assert.Equal(IssueDeRotation.ReemploiDetecte,
        DecisionDeRotation.Decider(Session(consommee: _t.AddSeconds(-1), remplacee: null), _t).Issue);

// La révocation prime sur la grâce : une famille révoquée ne se ranime pas.
[Fact]
public void Un_jeton_revoque_ET_consomme_reste_refuse() =>
    Assert.Equal(IssueDeRotation.Revoquee,
        DecisionDeRotation.Decider(
            Session(consommee: _t.AddSeconds(-1), revoquee: _t.AddSeconds(-1), remplacee: Guid.NewGuid()), _t).Issue);

[Fact]
public void Les_parametres_suivent_la_spec()
{
    Assert.Equal(TimeSpan.FromMinutes(15), ParametresDeSession.DureeDuJetonDAcces);
    Assert.Equal(TimeSpan.FromDays(14), ParametresDeSession.DureeDuRafraichissement);
    Assert.Equal(TimeSpan.FromSeconds(30), ParametresDeSession.FenetreDeGrace);
}
```

- [ ] **Étape 2 — rouge.** - [ ] **Étape 3 — implémenter**, ordre des gardes : `null` → révoquée → expirée → consommée (grâce ou réemploi) → acceptée.
- [ ] **Étape 4 — vert ET 100 %.** - [ ] **Étape 5 — commiter** : `git commit -m "décide la rotation sans déconnecter les innocents"`

---

## Tâche 7 : le magasin de sessions

**Fichiers :** créer `back/Palier.Infrastructure/Identite/{MagasinDeSessions,HachageDeJeton}.cs` · tester `back/Palier.Infrastructure.Tests/`

**Produit :**

```csharp
public static class HachageDeJeton
{
    public static byte[] Calculer(string jeton);            // SHA-256, 32 octets
    public static string Engendrer();                        // 256 bits, base64url
}

public interface IMagasinDeSessions
{
    Task<(string Jeton, Guid Famille)> OuvrirAsync(Guid utilisateur, string? appareil, CancellationToken j);
    Task<ResultatDeRotation> FaireTournerAsync(string jeton, CancellationToken j);
    Task RevoquerToutesAsync(Guid utilisateur, CancellationToken j);
    Task<IReadOnlyList<SessionActive>> ListerAsync(Guid utilisateur, CancellationToken j);
    Task<int> PurgerAsync(DateTimeOffset avant, CancellationToken j);
}
```

**Pourquoi SHA-256 et non PBKDF2 :** le jeton est une valeur aléatoire de 256 bits, pas un mot de passe. Il n'y a rien à deviner, donc rien à ralentir ; 210 000 itérations coûteraient 220 ms par rafraîchissement pour une protection nulle.

- [ ] **Étape 1 — créer `Palier.Infrastructure.Tests`** avec `CollectCoverage` **sans seuil pour l'instant** (il sera fixé à la mesure en tâche 18).
- [ ] **Étape 2 — écrire les épreuves qui échouent**, sur base réelle (Testcontainers, modèle de `BaseFixture`) : ouverture, rotation acceptée, rejeu dans la grâce, réemploi révoquant la famille entière, révocation globale, purge.
- [ ] **Étape 3 — rouge.** - [ ] **Étape 4 — implémenter.** La rotation s'exécute **dans une transaction**, avec `select … for update` sur la ligne, pour que deux requêtes concurrentes ne la lisent pas simultanément.
- [ ] **Étape 5 — vert.** - [ ] **Étape 6 — commiter** : `git commit -m "range les sessions derrière l'empreinte de leur jeton"`

---

## Tâche 8 : le hachage à 210 000 itérations

**Fichiers :** modifier `back/Palier.Api/Composition.cs` · tester `back/Palier.Api.Tests/`

- [ ] **Étape 1 — créer `Palier.Api.Tests`** (Testcontainers, sans seuil pour l'instant).
- [ ] **Étape 2 — écrire l'épreuve qui échoue**, qui décode le format produit :

```csharp
[Fact]
public void Le_hachage_applique_deux_cent_dix_mille_iterations()
{
    var hache = Convert.FromBase64String(Hacheur().HashPassword(new Utilisateur(), "MotDePasse123!"));
    Assert.Equal(0x01, hache[0]);                                             // IdentityV3
    Assert.Equal(2u, BinaryPrimitives.ReadUInt32BigEndian(hache.AsSpan(1, 4))); // HMAC-SHA512
    Assert.Equal(210_000u, BinaryPrimitives.ReadUInt32BigEndian(hache.AsSpan(5, 4)));
}
```

- [ ] **Étape 3 — rouge** : 100 000 par défaut.
- [ ] **Étape 4 — configurer**, avec le motif en commentaire (mesure du 21/08 : 56,7 → 111,2 ms) :

```csharp
constructeur.Services.Configure<PasswordHasherOptions>(o => o.IterationCount = 210_000);
```

- [ ] **Étape 5 — vert.** - [ ] **Étape 6 — commiter** : `git commit -m "relève le hachage au niveau que l'OWASP recommande pour ce PRF"`

---

## Tâche 9 : le validateur de mots de passe, à deux étages

**Fichiers :** créer `back/Palier.Infrastructure/Identite/ValidateurDeMotDePasse.cs`, `db/referentiel/mots-de-passe-frequents.…` · tester `back/Palier.Infrastructure.Tests/`

- [ ] **Étape 1 — épreuves qui échouent** : un mot de passe de la liste locale est refusé **même quand l'API est injoignable** ; un mot de passe absent de la liste passe ; l'API en panne ne bloque jamais une inscription ; longueur < 10 refusée.
- [ ] **Étape 2 — rouge.** - [ ] **Étape 3 — implémenter** `IPasswordValidator<Utilisateur>` : étage local en base d'abord, puis appel k-anonymat avec délai court, échec réseau ignoré.
- [ ] **Étape 4 — charger la liste** par un seed versionné, avec l'attribution CC BY dans `docs/17-donnees-sources.md`.
- [ ] **Étape 5 — porter `RequiredLength` à 10.**
- [ ] **Étape 6 — vert.** - [ ] **Étape 7 — commiter** : `git commit -m "refuse un mot de passe fréquent même quand le réseau est coupé"`

---

## Tâche 10 : le jeton d'accès et l'identité réelle

**Fichiers :** créer `back/Palier.Api/Auth/{JetonDAcces,IdentiteDepuisJeton}.cs` · modifier `Composition.cs`

**Ce que cela remplace :** `DemandeurSansIdentite`, le bouchon du lot 2.

- [ ] **Étape 1 — épreuves qui échouent** : un JWT valide donne l'identifiant attendu ; un JWT expiré ne donne rien ; un JWT signé par une autre clé ne donne rien ; l'absence de jeton donne `null`.
- [ ] **Étape 2 — rouge.** - [ ] **Étape 3 — implémenter**, quinze minutes, clé depuis `JWT_SIGNING_KEY`, refus de démarrer si la variable manque.
- [ ] **Étape 4 — brancher** `IIdentiteDemandeur` sur le jeton vérifié, jamais sur la requête (D36).
- [ ] **Étape 5 — vert.** - [ ] **Étape 6 — commiter** : `git commit -m "fait venir l'identité du jeton vérifié, et de nulle part ailleurs"`

---

## Tâche 11 : inscription et connexion

**Fichiers :** créer `back/Palier.Api/Auth/PointsDEntree.cs`, `CookieDeRafraichissement.cs`

**Routes :** `POST /api/v1/auth/inscription`, `POST /api/v1/auth/connexion`

- [ ] **Étape 1 — épreuves d'intégration qui échouent** : inscription réussie ; email déjà pris refusé sans révéler lequel ; connexion valide rendant un JWT **et** posant le cookie ; mauvais mot de passe refusé avec le **même message** qu'un email inconnu.
- [ ] **Étape 2 — rouge.** - [ ] **Étape 3 — implémenter.** Les attributs du cookie :

```csharp
new CookieOptions
{
    HttpOnly = true, Secure = true,
    SameSite = SameSiteMode.Strict,          // possible : même domaine, D16
    Path = "/api/v1/auth/rafraichir",        // ne part pas sur les autres requêtes
    Expires = maintenant + ParametresDeSession.DureeDuRafraichissement,
}
```

- [ ] **Étape 4 — vérifier qu'aucune donnée de santé ni aucun mot de passe n'atteint le journal** (`JournalisationTests`).
- [ ] **Étape 5 — vert.** - [ ] **Étape 6 — commiter** : `git commit -m "ouvre l'inscription et la connexion, sans distinguer les causes d'échec"`

---

## Tâche 12 : le rafraîchissement, la déconnexion et la liste des sessions

**Routes :** `POST /api/v1/auth/rafraichir`, `POST /api/v1/auth/deconnexion`, `POST /api/v1/auth/deconnexion-totale`, `GET /api/v1/auth/sessions`

- [ ] **Étape 1 — épreuves qui échouent**, dont **le franchissement du réemploi** : ouvrir une session, faire tourner, rejouer l'ancien jeton **après la grâce**, vérifier que toute la famille est révoquée et que le suivant ne marche plus.
- [ ] **Étape 2 — rouge.** - [ ] **Étape 3 — implémenter.**
- [ ] **Étape 4 — franchissement de la grâce** : rejouer **dans** les 30 s et vérifier que l'utilisateur **n'est pas** déconnecté.
- [ ] **Étape 5 — vert.** - [ ] **Étape 6 — commiter** : `git commit -m "fait tourner les jetons et révoque la famille au moindre rejeu tardif"`

---

## Tâche 13 : la limitation, et le piège du proxy

**Fichiers :** créer `back/Palier.Api/Auth/Limitation.cs`

**Le piège, rappelé :** sans `ForwardedHeaders`, tout le monde tombe dans le seau du proxy ; avec un `X-Forwarded-For` libre, la limite ne vaut plus rien.

- [ ] **Étape 1 — épreuves qui échouent** :

```csharp
[Fact]
public async Task Un_X_Forwarded_For_forge_depuis_une_adresse_inconnue_est_IGNORE()
{
    // Six tentatives depuis la MÊME adresse réelle, en changeant l'en-tête à chaque fois.
    // Si l'en-tête était cru, aucune ne serait limitée.
    for (var i = 0; i < 6; i++)
        reponse = await Client.PostAsync("/api/v1/auth/connexion", Corps(),
            enTetes: new() { ["X-Forwarded-For"] = $"203.0.113.{i}" });
    Assert.Equal(HttpStatusCode.TooManyRequests, reponse.StatusCode);
}

[Fact]
public async Task Six_tentatives_depuis_la_meme_adresse_sont_limitees() { /* 429 */ }
```

- [ ] **Étape 2 — rouge.** - [ ] **Étape 3 — implémenter** `ForwardedHeaders` (`KnownProxies` par variable d'environnement, `ForwardLimit = 1`) puis `AddRateLimiter` partitionné sur `RemoteIpAddress`.
- [ ] **Étape 4 — écrire dans le code le commentaire** qui dit que le limiteur compte **en mémoire de processus** : sur plusieurs répliques, la limite est multipliée par leur nombre.
- [ ] **Étape 5 — vert.** - [ ] **Étape 6 — commiter** : `git commit -m "compte les tentatives par adresse réelle, jamais par en-tête déclaré"`

---

## Tâche 14 : le verrouillage progressif

- [ ] **Étape 1 — épreuves qui échouent** : cinq échecs verrouillent ; un échec vieux de plus de 15 minutes ne compte plus ; le second verrouillage dure plus longtemps que le premier (5 → 15 → 60 min, puis palier).
- [ ] **Étape 2 — rouge.** - [ ] **Étape 3 — implémenter** la fenêtre glissante et l'escalade, qu'Identity ne sait pas faire.
- [ ] **Étape 4 — vert.** - [ ] **Étape 5 — commiter** : `git commit -m "fait grandir le verrouillage à chaque récidive"`

---

## Tâche 15 : la 2FA par TOTP

**Routes :** `POST /api/v1/auth/2fa/preparer`, `POST /api/v1/auth/2fa/activer`, `POST /api/v1/auth/2fa/desactiver`

- [ ] **Étape 1 — épreuves qui échouent** : l'URI rendue porte `algorithm=SHA1`, `digits=6`, `period=30` ; un code valide active ; un code faux refuse ; les codes de récupération sont rendus **une seule fois** ; le code TOTP n'apparaît dans **aucun** journal.
- [ ] **Étape 2 — rouge.** - [ ] **Étape 3 — implémenter.** Le backend rend l'URI `otpauth://`, le front dessinera le QR — aucune dépendance ajoutée.
- [ ] **Étape 4 — vert.** - [ ] **Étape 5 — commiter** : `git commit -m "rend l'URI TOTP et laisse le QR au navigateur"`

---

## Tâche 16 : la purge, et la cascade des sessions

**Périmètre réduit après relecture des documents.** `09-comptes.md` § 3 décrit une suppression de compte que ce lot **ne peut pas** livrer en entier : « Confirmation par saisie de l'email. Export proposé avant. Effacement réel sous 30 jours, purge des sauvegardes comprise. » Trois de ces quatre éléments demandent un écran, un export complet et une politique de sauvegarde. **Ce lot livre la cascade, et le dit.**

- [ ] **Étape 1 — épreuves qui échouent** :

```csharp
[Fact] public async Task La_purge_supprime_les_sessions_expirees() { }
[Fact] public async Task La_purge_epargne_les_sessions_vivantes() { }
[Fact] public async Task La_purge_est_idempotente() { /* deux passages, même état, second rend 0 */ }
[Fact] public async Task La_suppression_d_un_utilisateur_emporte_TOUTES_ses_sessions() { }
[Fact] public async Task Les_sessions_d_un_AUTRE_utilisateur_survivent() { /* la cascade ne déborde pas */ }
```

- [ ] **Étape 2 — rouge.** - [ ] **Étape 3 — implémenter** la purge idempotente. La cascade est portée par la contrainte de la tâche 2 ; cette tâche l'**éprouve**, elle ne la crée pas.
- [ ] **Étape 4 — vert.** - [ ] **Étape 5 — commiter** : `git commit -m "purge les sessions éteintes, et vérifie qu'aucune ne survit à son compte"`

---

## Tâche 16 bis : les deux verrous de la porte nutrition

**Ce que la relecture a corrigé :** le premier jet ne connaissait qu'un verrou. Il y en a deux, de sources différentes.

| Verrou             | Source              | Règle                                                                            |
| ------------------ | ------------------- | -------------------------------------------------------------------------------- |
| Email vérifié      | `09-comptes.md` § 1 | « pas de **nutrition** sans email vérifié » — la connexion, elle, reste ouverte  |
| Consentement santé | `09-comptes.md` § 2 | refusable ; « en cas de refus : accès à l'**entraînement**, pas à la nutrition » |

**Fichiers :** créer `back/Palier.Api/Auth/PolitiquesDAutorisation.cs` · tester `back/Palier.Api.Tests/`

- [ ] **Étape 1 — épreuves qui échouent**, une par combinaison — les quatre existent réellement :

```csharp
[Theory]
[InlineData(true,  true,  true )]  // email vérifié + consentement  → nutrition ouverte
[InlineData(false, true,  false)]  // email non vérifié             → nutrition fermée
[InlineData(true,  false, false)]  // consentement refusé           → nutrition fermée
[InlineData(false, false, false)]  // ni l'un ni l'autre            → nutrition fermée
public async Task La_nutrition_exige_les_DEUX_verrous(bool email, bool consentement, bool ouverte) { }

[Theory]
[InlineData(false, false)]
[InlineData(true,  false)]
public async Task L_entrainement_reste_ouvert_dans_TOUS_les_cas(bool email, bool consentement)
{
    // 09-comptes.md § 2 : « en cas de refus : accès à l'entraînement ».
    // Un verrou qui fermerait aussi l'entraînement serait une régression produit.
}
```

- [ ] **Étape 2 — rouge.** - [ ] **Étape 3 — implémenter** deux politiques d'autorisation nommées. Elles lisent des drapeaux **posés ailleurs** — la vérification d'email au lot 4b, le consentement à l'accueil — et ce lot ne fournit que les règles.
- [ ] **Étape 4 — vert.** - [ ] **Étape 5 — commiter** : `git commit -m "ferme la nutrition à deux verrous, et laisse l'entraînement ouvert"`

---

## Tâche 17 : les seuils des deux projets restants

- [ ] **Étape 1 — mesurer** la couverture atteinte sur `Palier.Infrastructure` et `Palier.Api`.
- [ ] **Étape 2 — poser le seuil AU NIVEAU ATTEINT**, jamais en dessous : un seuil sous la couverture réelle laisse celle-ci redescendre sans rien dire.
- [ ] **Étape 3 — vérifier que chaque seuil MORD** : retirer un test, constater l'échec, le remettre.
- [ ] **Étape 4 — commiter** : `git commit -m "fige la couverture atteinte des deux projets d'adaptateurs"`

---

## Tâche 17 bis : ce que l'AIPD et le registre attendent

**Ce n'est pas du code, et c'est pourtant un livrable.** `13-juridique.md` : « l'authentification devient une **mesure technique de l'AIPD** […] les sept exigences sont à décrire comme telles, **avec leur état d'implémentation** […] **l'écart se documente, il ne se suppose pas comblé** ». Sans cette tâche, le lot livrerait cinq exigences sur sept en laissant croire que les sept sont couvertes.

**Fichiers :** créer `docs/aipd/2026-08-21-authentification-etat.md` · modifier `docs/13-juridique.md`

- [ ] **Étape 1 — écrire l'état des sept exigences**, une ligne chacune, avec ce qui est livré et ce qui est reporté. Le tableau du § 7 quater de la spec en donne le contenu exact : trois livrées (3, 5, 6), une partielle (2 — la règle sans l'envoi), une sous réserve (4 — le proxy), deux reportées (1 et 7).

- [ ] **Étape 2 — écrire les durées de conservation** que ce lot crée, pour le registre (RGPD article 30) : empreinte de jeton et famille de session, 14 jours ; appareil, idem ; compteur d'échecs, fenêtre glissante de 15 minutes. Préciser qu'**aucune n'est une donnée de santé** et qu'aucune ne part au journal.

- [ ] **Étape 3 — mettre à jour la ligne du tableau des sous-traitants** de `docs/13-juridique.md` : « Authentification, mots de passe, sessions, jetons » n'est plus « Supabase Auth » mais opéré par le responsable de traitement, et **l'état de cette opération est désormais documenté**.

- [ ] **Étape 4 — commiter** : `git commit -m "documente l'écart entre les sept exigences et ce que ce lot en couvre"`

---

## Tâche 18 : clôture

- [ ] **Étape 1 — `npm run verify`**, les seize étapes.

- [ ] **Étape 2 — cocher la définition de terminé**, les douze points de `08-workflow.md` § 9, **et nommer les trois qui ne s'appliquent pas** : accessibilité clavier, états d'interface, textes bilingues. Ce lot n'a pas d'écran ; le report est écrit au § 7 quinquies de la spec et revient au lot qui livrera les écrans. **Un point sans objet se déclare, il ne se saute pas.**

- [ ] **Étape 3 — consigner quatre décisions** dans `docs/decisions.md` : le rôle `palier_auth` et les deux voies écartées ; les 210 000 itérations avec la mesure ; la fenêtre de grâce de 30 s ; les seuils des trois projets.

- [ ] **Étape 4 — remplir la ligne du lot 4** dans `docs/07-roadmap.md`, par mesure (D43).

- [ ] **Étape 5 — journal de bord** dans `.superpowers/sdd/2026-08-21-lot-4-socle-de-session/progress.md`, aux quatre sections du contrat de sortie. Y porter **ce que le lot ne referme pas** : la portabilité, la suppression de compte complète, la purge des sauvegardes, et la liste des tables hors modèle RLS qui appartient au porteur du projet.

- [ ] **Étape 6 — pousser**, puis **signaler le moment du `/code-review ultra`** au porteur du projet.

---

## Auto-revue du plan

**Couverture de la spec.** § 2 hachage → tâche 8. § 2 rotation/durées → tâches 6, 12. § 2 limitation par IP → tâche 13. § 2 bis chemin d'accès → tâches 1, 2, 3, 4. § 3 magasin → tâches 2, 7. § 3 grâce → tâche 6. § 3 politique RLS → tâche 2. § 4 jeton et cookie → tâches 10, 11. § 5 limitation → tâches 13, 14. § 6 validateur → tâche 9. § 7 TOTP → tâche 15. § 7 bis purge et cascade → tâche 16. § 7 ter projets et seuils → tâches 5, 7, 8, 17. § 7 quater AIPD et durées → tâche 17 bis. § 7 quinquies définition de terminé → tâche 18, étape 2. § 8 deux verrous → tâche 16 bis. § 8 hors périmètre → aucun code, vérifié par absence.

**Ce que la relecture des documents a corrigé**, après coup et non d'emblée :

| Document               | Ce qui manquait au plan                                       | Réparation        |
| ---------------------- | ------------------------------------------------------------- | ----------------- |
| `08-workflow.md` § 9   | les douze points, dont trois sans objet ici                   | tâche 18, étape 2 |
| `13-juridique.md`      | l'état d'implémentation des sept exigences, exigé pour l'AIPD | tâche 17 bis      |
| `13-juridique.md`      | les durées de conservation, pour le registre article 30       | tâche 17 bis      |
| `09-comptes.md` § 2    | le **second** verrou de la nutrition — le consentement santé  | tâche 16 bis      |
| `09-comptes.md` § 3    | une suppression de compte trop ambitieuse pour ce lot         | tâche 16, réduite |
| `01-conformite.md` § 4 | la portabilité, absente de la suite prévue                    | tâche 18, étape 5 |

**Points ouverts portés :** la liste locale de mots de passe (tâche 9, étape 4), les seuils à la mesure (tâche 17), l'adresse du proxy en variable d'environnement (tâche 13). La liste des tables hors modèle RLS reste au porteur du projet — signalée en clôture, non tranchée ici.

**Cohérence des types.** `ResultatDeRotation` et `IssueDeRotation` sont définis en tâche 6 et consommés en 7 et 12. `IMagasinDeSessions` est défini en 7 et consommé en 12 et 16. `SessionRafraichissement` est défini en 2 et consommé en 7. `ParametresDeSession` est défini en 6 et consommé en 11 et 12.
