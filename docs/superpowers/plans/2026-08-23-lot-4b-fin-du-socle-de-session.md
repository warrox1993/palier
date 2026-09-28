# Lot 4b — plan d'implémentation

> **Pour l'agent exécutant :** SOUS-SKILL REQUIS — `superpowers:executing-plans`. Étapes cochables.

**But :** fermer les quatre exigences que le lot 4 a laissées ouvertes, plus ses trois dettes consignées.

**Architecture :** SMTP générique via MailKit, administrateurs listés au coffre, OAuth Google par `AddGoogle`, limitation partagée en base. Rien de nouveau côté domaine — tout est adaptateur et point d'entrée.

**Pile :** .NET 10, ASP.NET Identity, MailKit, `Microsoft.AspNetCore.Authentication.Google`, EF Core, xUnit.

**Spec :** `docs/superpowers/specs/2026-08-23-lot-4b-fin-du-socle-de-session-design.md`

## Contraintes globales

- **TDD strict.** Test d'abord, rouge constaté, puis implémentation.
- **Deux assertions par garde-fou** : le refus **et** son motif propre.
- **Aucune donnée personnelle au journal** — ni adresse, ni jeton, ni corps de message.
- **Aucun oracle d'énumération** : les points d'entrée qui prennent une adresse rendent `202` que le compte existe ou non.
- **Seuils de couverture** : `Palier.Database.Tests` à `96,79,71`. Ils ne descendent pas.
- **Vérifier à la source** avant d'écrire contre une API tierce — Context7 ou Microsoft Learn, jamais de mémoire.
- Pousser avec `GIT_SSH_COMMAND="ssh -o ServerAliveInterval=20 -o ServerAliveCountMax=60"`.

## Ordre, et pourquoi

Les tâches 1 et 2 ne dépendent de rien et ferment deux dettes anciennes. La 3 est le socle des 4, 5 et 8. La 6 précède la 7.

---

## Tâche 1 : Le rôle d'administration (D41)

**Fichiers :** `Palier.Api/Auth/PolitiquesDAutorisation.cs`, `Palier.Api/Composition.cs`, `Palier.Database.Tests/AdministrationTests.cs`

**Produit :** politique `administration`, alimentée par `ADMIN_EMAILS` lue au coffre.

- [ ] **Étape 1 — Épreuves ROUGES**

```csharp
[Fact] public async Task Une_adresse_ABSENTE_de_la_liste_n_ouvre_pas_l_administration()
[Fact] public async Task Une_adresse_de_la_liste_ouvre_l_administration()
[Fact] public async Task La_casse_de_l_adresse_ne_change_RIEN()      // Jean@X.be == jean@x.be
[Fact] public async Task Une_adresse_NON_VERIFIEE_n_ouvre_PAS_l_administration()
[Fact] public async Task Une_liste_VIDE_n_ouvre_l_administration_a_PERSONNE()
```

La quatrième porte la sécurité : sans elle, quiconque s'inscrit avec l'adresse d'un administrateur en devient un, et l'inscription est ouverte. La cinquième est la borne — une liste vide ne doit pas valoir « tout le monde ».

- [ ] **Étape 2 — ROUGE**, puis **étape 3 — implémenter**

```csharp
// PolitiquesDAutorisation
public const string Administration = "administration";

options.AddPolicy(Administration, p => p.RequireAssertion(contexte =>
{
    var adresse = contexte.User.FindFirstValue(ClaimTypes.Email);
    var verifie = contexte.User.FindFirstValue(RevendicationEmailVerifie) == "true";
    return verifie && adresse is not null && administrateurs.Contains(adresse);
}));
```

`administrateurs` est un `HashSet<string>` construit une fois au démarrage avec `StringComparer.OrdinalIgnoreCase`.

- [ ] **Étape 4** — `MapGet(CheminDeSante, …).RequireAuthorization(Administration)`
- [ ] **Étape 5 — VERT**, **étape 6 — commit**

---

## Tâche 2 : La limitation partagée entre répliques

**Fichiers :** `Palier.Infrastructure/Entites/TentativeDeConnexion.cs`, migration, `Palier.Api/Auth/Limitation*.cs`, tests

**Produit :** table `tentatives_de_connexion`, compteur atomique à fenêtre glissante.

- [ ] **Étape 1 — Épreuves ROUGES**

```csharp
[Fact] public async Task Deux_INSTANCES_partagent_le_meme_compteur()
[Fact] public async Task Le_compteur_EXPIRE_avec_la_fenetre()
[Fact] public async Task Les_tentatives_d_une_AUTRE_adresse_ne_comptent_pas()
[Fact] public async Task La_table_porte_RLS_activee_ET_forcee()
[Fact] public async Task palier_sauvegarde_LIT_la_nouvelle_table()   // le piège du lot 4
```

La première est celle qui justifie la tâche : deux `LimitationPartagee` construites séparément, sur la même base, doivent voir le même compteur — c'est ce que la mémoire de processus ne faisait pas.

- [ ] **Étape 2 — ROUGE**, **étape 3 — la table**

```sql
create table public.tentatives_de_connexion (
  cle text primary key,              -- l'adresse IP normalisée
  compte int not null default 0,
  fenetre_ouverte_le timestamptz not null
);
alter table public.tentatives_de_connexion enable row level security;
alter table public.tentatives_de_connexion force row level security;
create policy limitation on public.tentatives_de_connexion
  for all to palier_app using (true) with check (true);
grant select, insert, update, delete on public.tentatives_de_connexion to palier_app;
create policy migrations_referentiel on public.tentatives_de_connexion
  for all to palier_migrations using (true) with check (true);
grant select on public.tentatives_de_connexion to palier_sauvegarde;
```

**Aucune donnée de santé, et aucun identifiant de compte** : une adresse IP et un compteur. C'est ce qui autorise `using (true)`.

- [ ] **Étape 4 — le compteur atomique**

```sql
insert into public.tentatives_de_connexion (cle, compte, fenetre_ouverte_le)
values (@cle, 1, @maintenant)
on conflict (cle) do update set
  compte = case when public.tentatives_de_connexion.fenetre_ouverte_le < @debutDeFenetre
                then 1 else public.tentatives_de_connexion.compte + 1 end,
  fenetre_ouverte_le = case when public.tentatives_de_connexion.fenetre_ouverte_le < @debutDeFenetre
                then @maintenant else public.tentatives_de_connexion.fenetre_ouverte_le end
returning compte;
```

Un seul aller-retour, atomique. Lire puis écrire laisserait deux répliques compter la même tentative une fois chacune.

- [ ] **Étape 5 — VERT**, **étape 6 — commit**

---

## Tâche 3 : L'envoi d'emails (SMTP, MailKit)

**Fichiers :** `Palier.Infrastructure/Courrier/{ReglagesDuCourrier,EnvoyeurSmtp,Gabarits}.cs`, `docs/decisions.md` (D60), tests

**Produit :** `IEmailSender<Utilisateur>` réel, et `Gabarits` qui rend sujet + corps en fr et en.

- [ ] **Étape 1 — la décision D60 pour MailKit**, avant la dépendance : motif, ce qu'elle remplace, ce qui la rouvrirait.
- [ ] **Étape 2 — Épreuves ROUGES**

```csharp
[Theory] [InlineData("SMTP_HOST")] [InlineData("SMTP_PORT")] [InlineData("SMTP_FROM")]
public void Chaque_reglage_ABSENT_refuse_le_demarrage_en_le_NOMMANT(string absent)

[Fact] public void Un_port_NON_NUMERIQUE_refuse_en_le_disant()
[Fact] public async Task Le_message_part_en_TEXTE_et_en_HTML()
[Fact] public async Task Le_journal_ne_porte_NI_adresse_NI_jeton()
[Theory] [InlineData("fr")] [InlineData("en")]
public void Chaque_gabarit_existe_dans_LES_DEUX_langues(string langue)
```

L'avant-dernière se vérifie sur un `ILogger` factice : aucune entrée ne contient l'adresse ni le lien.

- [ ] **Étape 3 — ROUGE**, **étape 4 — implémenter**

`EnvoyeurSmtp` ouvre une connexion par envoi, `SecureSocketOptions.StartTls`, et laisse remonter l'échec — un courriel qui ne part pas ne doit pas ressembler à un courriel parti.

- [ ] **Étape 5 — VERT**, **étape 6 — commit**

---

## Tâche 4 : La vérification d'adresse

**Fichiers :** `Palier.Api/Auth/PointsDEntree.cs` (+2 routes), `VerificationTests.cs`

- [ ] **Étape 1 — Épreuves ROUGES**

```csharp
[Fact] public async Task Le_renvoi_rend_202_meme_pour_une_adresse_INCONNUE()
[Fact] public async Task Le_renvoi_n_envoie_RIEN_pour_une_adresse_inconnue()
[Fact] public async Task Un_code_VALIDE_pose_EmailConfirmed()
[Fact] public async Task Un_code_d_un_AUTRE_compte_est_refuse()
[Fact] public async Task Un_compte_DEJA_verifie_ne_recoit_pas_un_second_courriel()
[Fact] public async Task La_nutrition_s_ouvre_APRES_verification_et_pas_avant()
```

Les deux premières vont ensemble : le `202` seul ne prouve rien si le courriel part quand même — le temps de réponse trahirait l'existence du compte.

La dernière relie ce lot au précédent : c'est la porte du lot 4 qui bascule.

- [ ] **Étape 2 — ROUGE**, **étape 3 — implémenter**, **étape 4 — VERT**, **étape 5 — commit**

---

## Tâche 5 : La réinitialisation de mot de passe

**Fichiers :** `PointsDEntree.cs` (+2 routes), `ReinitialisationTests.cs`

- [ ] **Étape 1 — Épreuves ROUGES**

```csharp
[Fact] public async Task La_demande_rend_202_meme_pour_une_adresse_INCONNUE()
[Fact] public async Task Un_mot_de_passe_FREQUENT_est_refuse_a_la_reinitialisation()
[Fact] public async Task La_reinitialisation_REVOQUE_toutes_les_sessions()
[Fact] public async Task Un_code_DEJA_employe_est_refuse()
```

La troisième est celle qui compte : une réinitialisation qui laisse vivre les jetons existants ne reprend pas le compte.

- [ ] **Étapes 2 à 5** — ROUGE, implémenter, VERT, commit

---

## Tâche 6 : Google OAuth

**Fichiers :** `Palier.Api/Auth/Google.cs`, `Composition.cs`, `GoogleTests.cs`

**Vérifier à la source avant d'écrire :** la forme de `AddGoogle` en .NET 10, le nom exact de la revendication `email_verified`, et le point d'extension du rappel.

- [ ] **Étape 1 — Épreuves ROUGES**, les quatre cas du § 6 de la spec

```csharp
[Fact] public async Task Une_connexion_Google_EXISTANTE_ouvre_la_session()
[Fact] public async Task Une_adresse_INCONNUE_cree_le_compte_avec_EmailConfirmed()
[Fact] public async Task Un_compte_email_EXISTANT_declenche_la_LIAISON_et_non_la_connexion()
[Fact] public async Task Une_adresse_NON_VERIFIEE_par_Google_est_REFUSEE()
[Fact] public async Task La_redirection_finale_ne_porte_AUCUN_jeton()
[Fact] public async Task Les_scopes_demandes_sont_EXACTEMENT_openid_profile_email()
```

La quatrième et la cinquième portent la sécurité du lot. La sixième garde l'interdiction de `09-comptes.md` § 1.

- [ ] **Étapes 2 à 5** — ROUGE, implémenter, VERT, commit

---

## Tâche 7 : La fusion des comptes

**Fichiers :** `Palier.Api/Auth/Liaison.cs`, `LiaisonTests.cs`

- [ ] **Étape 1 — Épreuves ROUGES**

```csharp
[Fact] public async Task Sans_MOT_DE_PASSE_la_liaison_est_refusee()
[Fact] public async Task Un_jeton_de_liaison_EXPIRE_est_refuse()
[Fact] public async Task Un_jeton_de_liaison_D_UN_AUTRE_compte_Google_est_refuse()
[Fact] public async Task Un_jeton_DEJA_employe_est_refuse()
[Fact] public async Task Avec_la_2FA_active_le_mot_de_passe_seul_ne_suffit_PAS()
[Fact] public async Task La_liaison_reussie_permet_la_connexion_par_LES_DEUX_voies()
```

- [ ] **Étapes 2 à 5** — ROUGE, implémenter, VERT, commit

---

## Tâche 8 : La suppression de compte

**Fichiers :** `Palier.Api/Auth/Suppression.cs`, `SuppressionTests.cs`

- [ ] **Étape 1 — Épreuves ROUGES**

```csharp
[Fact] public async Task La_demande_envoie_un_courriel_de_confirmation()
[Fact] public async Task Sans_CONFIRMATION_le_compte_survit()
[Fact] public async Task La_confirmation_efface_le_compte_ET_ses_donnees_de_sante()
[Fact] public async Task Un_code_de_confirmation_d_un_AUTRE_compte_est_refuse()
[Fact] public async Task Apres_suppression_les_sessions_ne_valent_plus_rien()
```

La troisième doit vérifier en SQL direct, sous `palier_migrations`, qu'aucune ligne ne subsiste dans `workouts`, `sets` et `body_weight` — la cascade se prouve en base, pas au travers de l'API qui la fait.

- [ ] **Étapes 2 à 5** — ROUGE, implémenter, VERT, commit

---

## Clôture

- [ ] `npm run verify` complet, code 0
- [ ] Les seuils de couverture relevés s'ils ont monté
- [ ] `docs/aipd/2026-08-21-authentification-etat.md` : les quatre exigences passent à livrées
- [ ] Le tableau des durées de `docs/07-roadmap.md`, rempli par mesure sur `git log`
- [ ] `back/.env.example` et `docs/16-projet.md` : les réglages SMTP, Google et `ADMIN_EMAILS`
- [ ] Pousser
