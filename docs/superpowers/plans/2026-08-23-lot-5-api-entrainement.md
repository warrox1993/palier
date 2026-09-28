# Lot 5 — l'API de l'entraînement · plan d'implémentation

> **Pour l'exécutant :** ce plan s'exécute tâche par tâche, en TDD. Chaque étape
> est cochable. La conception qu'il applique est
> `docs/superpowers/specs/2026-08-23-lot-5-api-entrainement-design.md` — les deux
> se lisent ensemble.

**Objectif :** livrer les cas d'usage de l'entraînement et les points d'entrée
qui les appellent, jusqu'au ressenti et aux contraintes.

**Architecture :** trois couches, et la frontière est celle qui existe déjà.
`Palier.Application/Entrainement/` porte les **contrats** — demandes, réponses,
énumérations — et les décisions pures. `Palier.Infrastructure/Entrainement/`
porte les **gestionnaires**, parce qu'ils prennent `PalierDbContext` et que ce
type vit là ; c'est exactement le motif écrit en tête de
`Palier.Application/Pipeline/Pipeline.cs`. `Palier.Api/Entrainement/` porte les
points d'entrée.

**Pile :** .NET 10, EF Core, PostgreSQL, xUnit. Aucune dépendance nouvelle.

**Conception :** `docs/superpowers/specs/2026-08-23-lot-5-api-entrainement-design.md`

---

## Contraintes globales

Ces règles valent pour **chaque** tâche. Elles ne sont pas répétées ensuite.

- **Aucun filtre sur `owner_id` dans un gestionnaire.** Le pipeline pose
  l'identité, RLS mord. L'écrire à la main serait une seconde façon de dire la
  même chose, et le jour où les deux divergent, c'est la mauvaise qui gagne.
- **Tout gestionnaire porte `[GestionnaireDeCasDUsage]`.** `ArchitectureTests`
  refuse le dépôt sans cet attribut sur un type qui prend `PalierDbContext`.
- **Tout point d'entrée passe par `IExecuteurDeCasDUsage.ExecuterAsync`.** C'est
  lui qui ouvre la transaction et y pose l'identité.
- **`RequireAuthorization(PolitiquesDAutorisation.Entrainement)`** sur chaque
  route. `PorteDesDomaines.EntrainementOuvert` rend `true` — c'est voulu, et la
  politique existe pour que le jour où elle se ferme, un seul endroit change.
- **Aucune chaîne en dur destinée à l'utilisateur.** Les messages d'erreur de
  l'API sont des **codes** — `seance_introuvable`, `exercice_public` — que le
  front traduit par i18next. Le libellé ne vit pas ici.
- **Aucune donnée de santé au journal.** Ni charge, ni poids, ni identifiant.
- **Deux assertions par épreuve** quand elle juge un refus : le **code de statut**
  et le **code d'erreur** rendu. Un 400 nu ne prouve pas quelle règle a mordu.
- **Chaque tâche qui touche une table nouvelle** livre dans le même commit : la
  politique RLS, `force row level security`, les privilèges des **trois** rôles
  — `palier_app`, `palier_migrations`, `palier_sauvegarde` — et son épreuve
  d'isolation. Le lot 4b a payé l'oubli du troisième : `grant select on ALL
TABLES` ne couvre pas les tables nées après lui, et `SauvegardeTests` rougit.
- **`npm run verify` passe** avant chaque commit.

### Le préfixe et le groupe

Toutes les routes de ce lot vivent sous `/api/v1`, dans un groupe unique attaché
par `Entrainement.Router(application)`, appelé depuis `Composition.Router`.

---

## Structure des fichiers

| Fichier                                                     | Responsabilité                            |
| ----------------------------------------------------------- | ----------------------------------------- |
| `Palier.Application/Entrainement/Contrats.cs`               | demandes et réponses, aucun comportement  |
| `Palier.Application/Entrainement/Contraintes.cs`            | l'énumération fermée des quatre régions   |
| `Palier.Application/Entrainement/Ressenti.cs`               | l'énumération fermée des trois états      |
| `Palier.Infrastructure/Entrainement/Seances.cs`             | les six gestionnaires de la séance        |
| `Palier.Infrastructure/Entrainement/Series.cs`              | ajouter et retirer une série              |
| `Palier.Infrastructure/Entrainement/PoidsCorporel.cs`       | enregistrer et lire, avec les deux gardes |
| `Palier.Infrastructure/Entrainement/Mesures.cs`             | force estimée et volume hebdomadaire      |
| `Palier.Infrastructure/Entrainement/Catalogue.cs`           | lister, créer, supprimer un exercice      |
| `Palier.Infrastructure/Entrainement/RessentiParExercice.cs` | le ressenti — table nouvelle              |
| `Palier.Infrastructure/Entrainement/ContraintesDuCompte.cs` | les contraintes — table nouvelle          |
| `Palier.Api/Entrainement/Entrainement.cs`                   | le groupe de routes et son attachement    |
| `Palier.Api/Entrainement/*.cs`                              | un fichier de routes par famille          |
| `Palier.Database.Tests/Entrainement*Tests.cs`               | une suite par tâche                       |

---

## Tâche 1 — la séance : ouvrir, clôturer, supprimer

**Fichiers :**

- Créer : `back/Palier.Application/Entrainement/Contrats.cs`
- Créer : `back/Palier.Infrastructure/Entrainement/Seances.cs`
- Créer : `back/Palier.Api/Entrainement/Entrainement.cs`
- Créer : `back/Palier.Api/Entrainement/Seances.cs`
- Modifier : `back/Palier.Api/Composition.cs` — appeler `Entrainement.Router`
- Épreuves : `back/Palier.Database.Tests/EntrainementSeancesTests.cs`

**Interfaces produites** — les tâches suivantes s'y branchent :

```csharp
public sealed record OuvertureDeSeance(DateTimeOffset? Debut, decimal? HeuresDeSommeil, string? Note);
public sealed record ClotureDeSeance(DateTimeOffset? Fin, int? Energie);
public sealed record SeanceRendue(Guid Id, DateTimeOffset Debut, DateTimeOffset? Fin, decimal? HeuresDeSommeil, int? Energie, string? Note);
```

- [ ] **Étape 1 : écrire l'épreuve qui échoue — une séance s'ouvre et se relit**

```csharp
[Fact]
public async Task Une_seance_ouverte_se_relit_avec_son_identifiant()
{
    var proprietaire = await _base.CreerUnUtilisateurAsync();
    await using var hote = HarnaisHttp.Hote(_base, demandeur: new DemandeurFixe(proprietaire));

    var (code, corps) = await HarnaisHttp.ExecuterAsync(
        hote, HttpMethod.Post, "/api/v1/seances",
        """{"debut":"2026-08-23T09:00:00Z","heuresDeSommeil":7.5}"""
    );

    Assert.Equal(StatusCodes.Status201Created, code);
    var id = JsonDocument.Parse(corps).RootElement.GetProperty("id").GetGuid();
    Assert.NotEqual(Guid.Empty, id);
}
```

- [ ] **Étape 2 : la lancer, vérifier qu'elle échoue**

`dotnet test back/Palier.Database.Tests --filter Une_seance_ouverte_se_relit`
Attendu : ÉCHEC — 404, la route n'existe pas.

- [ ] **Étape 3 : le gestionnaire minimal**

```csharp
[GestionnaireDeCasDUsage]
internal sealed class OuvrirUneSeance(PalierDbContext contexte)
{
    public async Task<Guid> ExecuterAsync(OuvertureDeSeance demande, TimeProvider horloge, CancellationToken jeton)
    {
        var seance = new Workout
        {
            StartedAt = demande.Debut ?? horloge.GetUtcNow(),
            SleepHours = demande.HeuresDeSommeil,
            Note = demande.Note,
        };
        contexte.Workouts.Add(seance);
        await contexte.SaveChangesAsync(jeton).ConfigureAwait(false);
        return seance.Id;
    }
}
```

`OwnerId` n'est PAS posé ici : c'est `app.current_user_id()` qui le remplit, par
la valeur par défaut de la colonne. Si la migration ne la porte pas, la poser
depuis `IIdentiteDemandeur` — jamais depuis la requête.

- [ ] **Étape 4 : le point d'entrée**

```csharp
private static async Task<IResult> OuvrirAsync(
    OuvertureDeSeance demande, IExecuteurDeCasDUsage executeur,
    OuvrirUneSeance gestionnaire, TimeProvider horloge, CancellationToken jeton)
{
    var id = await executeur.ExecuterAsync(
        nameof(OuvrirUneSeance),
        j => gestionnaire.ExecuterAsync(demande, horloge, j),
        jeton
    ).ConfigureAwait(false);

    return Results.Created($"/api/v1/seances/{id}", new { id });
}
```

- [ ] **Étape 5 : relancer, vérifier le vert**

- [ ] **Étape 6 : l'épreuve d'ISOLATION — celle qui compte**

```csharp
[Fact]
public async Task La_seance_de_A_est_invisible_pour_B()
{
    var a = await _base.CreerUnUtilisateurAsync();
    var b = await _base.CreerUnUtilisateurAsync();
    var demandeur = new DemandeurMutable { Identifiant = a };
    await using var hote = HarnaisHttp.Hote(_base, demandeur: demandeur);

    var (_, corps) = await HarnaisHttp.ExecuterAsync(hote, HttpMethod.Post, "/api/v1/seances", "{}");
    var id = JsonDocument.Parse(corps).RootElement.GetProperty("id").GetGuid();

    demandeur.Identifiant = b;
    var (code, _) = await HarnaisHttp.ExecuterAsync(hote, HttpMethod.Get, $"/api/v1/seances/{id}", null);

    // 404 et NON 403 : dire « interdit » confirmerait que l'identifiant existe.
    Assert.Equal(StatusCodes.Status404NotFound, code);
}
```

- [ ] **Étape 7 : la clôture — `PATCH`, et l'énergie bornée**

```csharp
[Theory]
[InlineData(0)]
[InlineData(6)]
public async Task Une_energie_hors_de_un_a_cinq_est_refusee(int energie)
{
    // 400, code « energie_hors_bornes ». La contrainte CHECK existe en base,
    // mais un 500 sur violation de contrainte n'est pas une réponse : le refus
    // se prononce AVANT, et l'épreuve prouve les deux bords.
}
```

- [ ] **Étape 8 : la suppression — `DELETE`, 204, et 404 pour la séance d'autrui**

- [ ] **Étape 9 : `npm run verify`, puis commit**

```bash
git add back/Palier.Application/Entrainement back/Palier.Infrastructure/Entrainement back/Palier.Api/Entrainement back/Palier.Api/Composition.cs back/Palier.Database.Tests/EntrainementSeancesTests.cs
git commit -m "ouvre, clôture et supprime une séance"
```

---

## Tâche 2 — la liste paginée par curseur

**Fichiers :**

- Modifier : `back/Palier.Infrastructure/Entrainement/Seances.cs`
- Modifier : `back/Palier.Api/Entrainement/Seances.cs`
- Épreuves : `back/Palier.Database.Tests/EntrainementSeancesTests.cs`

**Interfaces produites :**

```csharp
public sealed record PageDeSeances(IReadOnlyList<SeanceRendue> Elements, DateTimeOffset? Suivant);
```

`Suivant` est `null` quand la page est la dernière. Le client le repasse en
`?avant=`.

- [ ] **Étape 1 : l'épreuve qui échoue — deux pages, aucun doublon, aucun trou**

```csharp
[Fact]
public async Task La_pagination_par_curseur_ne_repete_ni_ne_saute_aucune_seance()
{
    // 5 séances, limite 2. On parcourt jusqu'à `suivant == null` et on
    // assertionne que les 5 identifiants sont sortis, chacun UNE fois.
    // C'est cette épreuve qui distingue le curseur du décalage : avec OFFSET,
    // une insertion entre deux pages ferait rater une ligne.
}
```

- [ ] **Étape 2 : la lancer, vérifier qu'elle échoue**

- [ ] **Étape 3 : le gestionnaire**

```csharp
[GestionnaireDeCasDUsage]
internal sealed class ListerLesSeances(PalierDbContext contexte)
{
    // 20 par défaut, 100 au plus. La borne HAUTE n'est pas décorative : sans
    // elle, `?limite=1000000` fait matérialiser toute la table en mémoire —
    // un déni de service que n'importe quel compte authentifié déclenche.
    private const int LimiteParDefaut = 20;
    private const int LimiteMaximale = 100;

    public async Task<PageDeSeances> ExecuterAsync(DateTimeOffset? avant, int? limite, CancellationToken jeton)
    {
        var taille = Math.Clamp(limite ?? LimiteParDefaut, 1, LimiteMaximale);

        var requete = contexte.Workouts.AsNoTracking().OrderByDescending(s => s.StartedAt);
        if (avant is { } curseur)
        {
            requete = requete.Where(s => s.StartedAt < curseur).OrderByDescending(s => s.StartedAt);
        }

        // On demande UNE de plus que la taille : c'est ce qui permet de savoir
        // s'il reste quelque chose sans compter la table entière.
        var lot = await requete.Take(taille + 1).ToListAsync(jeton).ConfigureAwait(false);
        var suivant = lot.Count > taille ? lot[taille - 1].StartedAt : (DateTimeOffset?)null;

        return new PageDeSeances(
            lot.Take(taille).Select(Rendre).ToList(),
            suivant
        );
    }
}
```

- [ ] **Étape 4 : le point d'entrée, `GET /api/v1/seances?avant=&limite=`**

- [ ] **Étape 5 : relancer, vérifier le vert**

- [ ] **Étape 6 : l'épreuve de la borne haute**

```csharp
[Fact]
public async Task Une_limite_demesuree_est_ramenee_a_cent()
{
    // 150 séances, `?limite=100000`. On assertionne que la page en rend 100.
    // Sans cette épreuve, la borne se retire sans que rien ne rougisse.
}
```

- [ ] **Étape 7 : `npm run verify`, puis commit**

```bash
git commit -m "pagine les séances par curseur, et borne la page à cent"
```

---

## Tâche 3 — les séries

**Fichiers :**

- Créer : `back/Palier.Infrastructure/Entrainement/Series.cs`
- Créer : `back/Palier.Api/Entrainement/Series.cs`
- Épreuves : `back/Palier.Database.Tests/EntrainementSeriesTests.cs`

**Interfaces consommées :** `SeanceRendue` (tâche 1).

**Interfaces produites :**

```csharp
public sealed record AjoutDeSerie(Guid ExerciceId, int Index, decimal Charge, int Repetitions, int? Rir, bool Echauffement);
public sealed record SerieRendue(Guid Id, Guid ExerciceId, int Index, decimal Charge, int Repetitions, int? Rir, bool Echauffement, DateTimeOffset Instant);
```

- [ ] **Étape 1 : l'épreuve qui échoue — une série s'ajoute et ressort avec la séance**

- [ ] **Étape 2 : la lancer, vérifier qu'elle échoue**

- [ ] **Étape 3 : le gestionnaire, avec la VALIDATION DE FRONTIÈRE**

Les grandeurs du domaine — `Charge`, `Repetitions` — ne se construisent pas
invalides. C'est là qu'on les construit, et c'est la seule fois :

```csharp
// Charge.Depuis et Repetitions.Depuis LÈVENT sur une valeur hors bornes.
// On les appelle ICI, à la frontière, et le reste du code tient un type qui
// ne PEUT PAS être faux — `CLAUDE.md` § 4 : « valider à la frontière, une
// seule fois, puis faire confiance au type ».
var charge = Charge.Depuis(demande.Charge);
var repetitions = Repetitions.Depuis(demande.Repetitions);
```

- [ ] **Étape 4 : l'épreuve du refus — charge négative, répétitions à zéro**

Deux assertions : 400, et le code `charge_invalide` / `repetitions_invalides`.

- [ ] **Étape 5 : l'épreuve d'isolation — B n'ajoute pas de série à la séance de A**

Attendu : **404**. Pas 403.

- [ ] **Étape 6 : l'épreuve de l'exercice inexistant**

400, code `exercice_introuvable`. Et l'épreuve doit provoquer le cas : sans
elle, une clé étrangère violée rendrait 500.

- [ ] **Étape 7 : `DELETE /api/v1/seances/{id}/series/{serieId}` — 204**

- [ ] **Étape 8 : `npm run verify`, puis commit**

```bash
git commit -m "ajoute et retire une série, en validant à la frontière"
```

---

## Tâche 4 — le poids corporel, et les deux gardes de sécurité

**Fichiers :**

- Créer : `back/Palier.Infrastructure/Entrainement/PoidsCorporel.cs`
- Créer : `back/Palier.Api/Entrainement/PoidsCorporel.cs`
- Épreuves : `back/Palier.Database.Tests/EntrainementPoidsTests.cs`

**Interfaces produites :**

```csharp
public sealed record MesureDePoids(DateOnly Jour, decimal Poids);
public sealed record SerieDePoids(IReadOnlyList<MesureDePoids> Mesures, ConstatDeSecurite? Constat);
```

- [ ] **Étape 1 : l'épreuve qui échoue — DEUX mesures le même jour REMPLACENT**

```csharp
[Fact]
public async Task Deux_mesures_le_meme_jour_remplacent_au_lieu_d_echouer()
{
    // POST 82.4 puis POST 81.9 sur le 2026-08-23.
    // Attendu : 200 les DEUX fois, et la série rend UNE ligne à 81.9.
    //
    // `body_weight` porte `unique (owner_id, measured_on)`. Sans traitement,
    // la seconde saisie rendrait une violation de contrainte — donc un 500 —
    // à un utilisateur qui corrige simplement une faute de frappe.
}
```

- [ ] **Étape 2 : la lancer, vérifier qu'elle échoue**

- [ ] **Étape 3 : le gestionnaire idempotent**

Chercher la mesure du jour, la mettre à jour si elle existe, l'insérer sinon.
Pas d'`ON CONFLICT` écrit à la main : EF Core suffit, et la requête reste
paramétrée.

- [ ] **Étape 4 : relancer, vérifier le vert**

- [ ] **Étape 5 : l'épreuve du CONSTAT — la perte rapide se signale**

```csharp
[Fact]
public async Task Une_perte_rapide_produit_un_constat_sans_bloquer_la_saisie()
{
    // Une série qui descend assez vite pour déclencher `PerteDePoidsRapide`.
    // DEUX assertions : la mesure est ENREGISTRÉE (200), et le constat est
    // présent dans la réponse.
    //
    // Le point qui compte : la garde n'EMPÊCHE rien. `01-conformite.md`
    // sépare informer de prescrire, et un produit qui refuserait une saisie
    // de poids se ferait contourner en cessant de saisir — ce qui supprime
    // justement le signal.
}
```

- [ ] **Étape 6 : l'API NE RÉDIGE AUCUN MESSAGE**

```csharp
[Fact]
public async Task Le_constat_rendu_ne_porte_aucune_phrase_destinee_a_l_utilisateur()
{
    // On assertionne que la réponse porte un CODE — « perte_rapide » — et
    // qu'aucun champ ne contient d'espace suivi d'une minuscule accentuée,
    // signature d'une phrase française. Le libellé vit en base, versionné et
    // validé — `09-comptes.md`. Une phrase écrite ici échapperait à cette
    // validation ET à i18next.
}
```

- [ ] **Étape 7 : l'épreuve d'isolation**

- [ ] **Étape 8 : `npm run verify`, puis commit**

```bash
git commit -m "enregistre le poids par jour, et rend le constat sans le rédiger"
```

---

## Tâche 5 — la force estimée et le volume hebdomadaire

**Fichiers :**

- Créer : `back/Palier.Infrastructure/Entrainement/Mesures.cs`
- Créer : `back/Palier.Api/Entrainement/Mesures.cs`
- Épreuves : `back/Palier.Database.Tests/EntrainementMesuresTests.cs`

**Interfaces consommées :** `ForceEstimee.Epley`, `VolumeParGroupe.SurSeptJours`,
`DetectionPlateau.EstEnPlateau` — le domaine, couvert à 100 %.

- [ ] **Étape 1 : l'épreuve qui échoue — le 1RM estimé sort de la meilleure série**

```csharp
[Fact]
public async Task La_force_estimee_repose_sur_la_meilleure_serie_DURE()
{
    // Trois séries dont une d'ÉCHAUFFEMENT plus lourde que les autres.
    // Attendu : l'échauffement est IGNORÉ. Une série d'échauffement à charge
    // élevée et deux répétitions gonflerait l'estimation, et le produit
    // proposerait une progression sur un maximum qui n'a jamais été soulevé.
}
```

- [ ] **Étape 2 : la lancer, vérifier qu'elle échoue**

- [ ] **Étape 3 : le gestionnaire — il LIT, il ne CALCULE pas**

```csharp
// Le calcul vit dans `Palier.Domain`. Ce gestionnaire va chercher les séries
// et les passe. Recopier la formule d'Epley ici créerait une seconde
// implémentation d'une même connaissance — exactement ce que `CLAUDE.md` § 4
// nomme « une connaissance, un endroit », sur une valeur que l'utilisateur voit.
var estimation = ForceEstimee.Epley(meilleure.Charge, meilleure.Repetitions, meilleure.Rir);
```

- [ ] **Étape 4 : l'épreuve du cas VIDE — aucune série sur cet exercice**

200 avec une estimation absente, **pas** 404 : « je n'ai pas encore de donnée »
est une réponse, pas une erreur. Et `11-qualite.md` exige un état vide traité.

- [ ] **Étape 5 : le volume — `GET /api/v1/volume-hebdomadaire`**

Il lit la vue `weekly_volume`, qui porte `security_invoker = true` : elle
s'exécute donc sous les politiques de l'APPELANT. L'épreuve d'isolation doit le
prouver — c'est le seul endroit du schéma où une erreur de configuration
laisserait fuir les données de tout le monde par un seul `SELECT`.

- [ ] **Étape 6 : l'épreuve d'isolation SUR LA VUE**

```csharp
[Fact]
public async Task La_vue_de_volume_n_expose_que_les_seances_de_l_appelant()
{
    // A fait 10 séries, B en fait 3. B interroge le volume : il voit 3.
    // Sans `security_invoker`, la vue s'exécuterait sous `palier_migrations`,
    // propriétaire de toutes les tables, et B verrait les 13.
}
```

- [ ] **Étape 7 : `npm run verify`, puis commit**

```bash
git commit -m "rend la force estimée et le volume, sans recalculer le domaine"
```

---

## Tâche 6 — le catalogue d'exercices

**Fichiers :**

- Créer : `back/Palier.Infrastructure/Entrainement/Catalogue.cs`
- Créer : `back/Palier.Api/Entrainement/Catalogue.cs`
- Épreuves : `back/Palier.Database.Tests/EntrainementCatalogueTests.cs`

- [ ] **Étape 1 : l'épreuve qui échoue — le catalogue rend le public ET les siens**

- [ ] **Étape 2 : la lancer, vérifier qu'elle échoue**

- [ ] **Étape 3 : les trois gestionnaires**

`exercises` est la forme « catalogue mixte » : `owner_id` nul pour le public. La
politique RLS l'expose déjà correctement — le gestionnaire ne filtre rien.

- [ ] **Étape 4 : l'épreuve qui PROVOQUE le refus — supprimer un exercice public**

```csharp
[Fact]
public async Task Supprimer_un_exercice_PUBLIC_est_refuse()
{
    // DEUX assertions : 403, et le code « exercice_public ».
    //
    // 403 et non 404 ici, contrairement aux séances : l'exercice public est
    // VISIBLE de tous, donc son existence n'est pas un secret. Répondre 404
    // sur une ressource que l'appelant vient de lire serait un mensonge.
}
```

- [ ] **Étape 5 : l'épreuve qui provoque le refus — supprimer l'exercice d'un AUTRE**

404, cette fois : celui-là n'est pas visible.

- [ ] **Étape 6 : l'épreuve — un exercice créé porte `is_custom = true`**

Sans quoi il se retrouverait dans le catalogue public de tout le monde.

- [ ] **Étape 7 : `npm run verify`, puis commit**

```bash
git commit -m "expose le catalogue mixte, et refuse de supprimer le public"
```

---

## Tâche 7 — le ressenti par exercice · TABLE NOUVELLE

**Fichiers :**

- Créer : `back/Palier.Application/Entrainement/Ressenti.cs`
- Créer : `back/Palier.Infrastructure/Entrainement/RessentiParExercice.cs`
- Créer : migration `AjouteLeRessenti`
- Modifier : `back/Palier.Infrastructure/Entites/Entites.cs`
- Épreuves : `back/Palier.Database.Tests/EntrainementRessentiTests.cs`,
  `SchemaTests.cs`, `IsolationTests.cs`, `SauvegardeTests.cs`

**Interfaces produites :**

```csharp
public enum Ressenti { Bon, Moyen, Douleur }
```

Trois états, `05-entrainement.md` § 5. **Fermé** : une valeur libre ne se traduit
pas, ne se raisonne pas, et finirait dans un prompt de modèle.

- [ ] **Étape 1 : l'épreuve de SCHÉMA qui échoue**

```csharp
[Fact]
public async Task La_table_exercise_feedback_porte_RLS_ACTIVEE_ET_FORCEE()
{
    // `relrowsecurity` ET `relforcerowsecurity`, les DEUX à `true`.
    // Sans `force`, le propriétaire de la table contourne les politiques —
    // et le propriétaire, c'est `palier_migrations`, qui applique le seed.
}
```

- [ ] **Étape 2 : la lancer, vérifier qu'elle échoue** — la table n'existe pas.

- [ ] **Étape 3 : l'entité et la migration**

La table est **possédée par jointure** : elle porte `workout_id`, et
l'appartenance se lit par la séance. Sa politique RLS ne teste donc pas un
`owner_id` local mais l'existence de la séance parente :

```sql
create policy ressenti_isolation on public.exercise_feedback for all
  using (exists (
    select 1 from public.workouts w
     where w.id = exercise_feedback.workout_id
       and w.owner_id = app.current_user_id()
  ))
  with check (exists (
    select 1 from public.workouts w
     where w.id = exercise_feedback.workout_id
       and w.owner_id = app.current_user_id()
  ));
```

`using` **et** `with check`, les deux. Sans `with check`, un utilisateur
insérerait une ligne rattachée à la séance d'un autre — il ne pourrait pas la
relire, mais elle serait là, et elle compterait dans les statistiques de sa
victime.

- [ ] **Étape 4 : les privilèges des TROIS rôles**

```sql
grant select, insert, update, delete on public.exercise_feedback to palier_app;
grant select on public.exercise_feedback to palier_sauvegarde;
create policy ressenti_referentiel on public.exercise_feedback for all
  to palier_migrations using (true) with check (true);
```

Le troisième rôle n'est pas facultatif : `SauvegardeTests` refuse un `pg_dump`
incomplet, et c'est ce qui a rougi au lot 4b.

- [ ] **Étape 5 : relancer les épreuves de schéma, vérifier le vert**

- [ ] **Étape 6 : l'épreuve d'ISOLATION, qui doit PROVOQUER le refus**

```csharp
[Fact]
public async Task Un_ressenti_rattache_a_la_seance_d_AUTRUI_est_refuse_a_l_INSERTION()
{
    // C'est `with check` qu'on éprouve ici, et lui seul : la lecture serait
    // refusée de toute façon par `using`. Une épreuve qui ne fait que lire
    // laisserait `with check` se retirer sans rougir.
}
```

- [ ] **Étape 7 : le gestionnaire et le point d'entrée**

```
POST /api/v1/seances/{id}/ressenti    { exerciceId, ressenti }
GET  /api/v1/exercices/{id}/ressenti  les N derniers
```

- [ ] **Étape 8 : l'épreuve de l'énumération fermée**

`{"ressenti":"excellent"}` → 400, code `ressenti_invalide`. Sans elle, une
valeur inconnue entrerait en base et les seuils du § 5 compteraient faux.

- [ ] **Étape 9 : `npm run verify`, puis commit**

```bash
git commit -m "pose le ressenti par exercice, possédé par jointure"
```

---

## Tâche 8 — les contraintes du compte · TABLE NOUVELLE

**Fichiers :**

- Créer : `back/Palier.Application/Entrainement/Contraintes.cs`
- Créer : `back/Palier.Infrastructure/Entrainement/ContraintesDuCompte.cs`
- Créer : migration `AjouteLesContraintes`
- Modifier : `back/Palier.Infrastructure/Entites/Entites.cs`
- Épreuves : `back/Palier.Database.Tests/EntrainementContraintesTests.cs`,
  `SchemaTests.cs`, `IsolationTests.cs`, `SauvegardeTests.cs`

**Interfaces produites :**

```csharp
/// Les quatre régions de `docs/05-entrainement.md` § 4, et rien d'autre.
public enum Contrainte { Cervicale, Lombaire, Epaule, Genou }
```

- [ ] **Étape 1 : l'épreuve de schéma qui échoue** — RLS activée ET forcée.

- [ ] **Étape 2 : la lancer, vérifier qu'elle échoue**

- [ ] **Étape 3 : l'entité et la migration**

**Possédée directe**, cette fois : `owner_id` porté par la table. Politique
classique, `using` et `with check` sur `app.current_user_id()`, plus
`unique (owner_id, region)` — déclarer deux fois la même contrainte est un
remplacement, pas un doublon.

- [ ] **Étape 4 : les privilèges des TROIS rôles**

- [ ] **Étape 5 : relancer, vérifier le vert**

- [ ] **Étape 6 : l'épreuve d'isolation, insertion ET lecture**

- [ ] **Étape 7 : les points d'entrée**

```
GET    /api/v1/contraintes           les siennes
PUT    /api/v1/contraintes           la liste complète, remplacée
DELETE /api/v1/contraintes/{region}  en retire une
```

`PUT` et non `POST` sur la liste : déclarer ses contraintes est un état, pas un
événement. Renvoyer deux fois la même liste doit donner le même résultat.

- [ ] **Étape 8 : l'épreuve de l'énumération fermée**

`{"regions":["poignet"]}` → 400, code `contrainte_invalide`. La liste est fermée
par le document métier ; une cinquième valeur s'ajoute à l'énumération, elle ne
s'infiltre pas par le corps d'une requête.

- [ ] **Étape 9 : l'épreuve qui vérifie que le CATALOGUE N'EST PAS ENCORE FILTRÉ**

```csharp
[Fact]
public async Task Declarer_une_contrainte_ne_FILTRE_PAS_encore_le_catalogue()
{
    // Le § 8 de la conception laisse cette décision au porteur : exclure
    // l'exercice contre-indiqué, ou l'afficher marqué. Tant qu'elle n'est
    // pas prise, le lot 5 STOCKE et EXPOSE, sans filtrer.
    //
    // Cette épreuve est là pour ROUGIR le jour où le filtrage arrive : elle
    // force à venir ici et à décider, plutôt qu'à laisser un comportement
    // s'installer sans qu'on l'ait choisi.
}
```

- [ ] **Étape 10 : `npm run verify`, puis commit**

```bash
git commit -m "pose les contraintes du compte, en liste fermée"
```

---

## Tâche 9 — la revue, et ce qu'on écrit avant de fermer

- [ ] **Étape 1 : `npm run verify` complet, à froid**

- [ ] **Étape 2 : vérifier la couverture des projets touchés**

`Palier.Domain` et `Palier.Application` restent à **100 %**. C'est ce seuil qui
fait office de détection de code mort public — `CLAUDE.md` § 4. Un contrat
déclaré et jamais appelé le fait tomber, et c'est voulu.

- [ ] **Étape 3 : consigner dans `docs/decisions.md`**

Une décision datée pour chacun des quatre arbitrages du § 7 de la conception,
avec ce qui la défait.

- [ ] **Étape 4 : mettre à jour `docs/07-roadmap.md`**

- [ ] **Étape 5 : signaler au porteur ce qui attend sa décision**

1. **D39** — tranchée en autonomie, à confirmer.
2. **Le filtrage du catalogue** par les contraintes — § 8, décision de produit.
3. **`/code-review ultra`** sur le lot — lui seul peut le lancer.

- [ ] **Étape 6 : commit final**

```bash
git commit -m "clôt le lot 5, et nomme ce qui attend une décision"
```

---

## Auto-revue du plan

**Couverture de la conception.** Les huit familles du § 3 et du § 4 ont chacune
leur tâche : séances (1, 2), séries (3), poids (4), force et volume (5),
catalogue (6), ressenti (7), contraintes (8). Le § 7.3 — pagination par curseur
— est la tâche 2 entière. Le § 7.4 — listes fermées — est éprouvé aux tâches 7
et 8. Le § 8 — le filtrage non tranché — a son épreuve inversée, tâche 8 étape 9.

**Cohérence des types.** `SeanceRendue` (tâche 1) est consommée par `PageDeSeances`
(tâche 2) et par la lecture d'une séance avec ses `SerieRendue` (tâche 3).
`Ressenti` et `Contrainte` sont deux énumérations distinctes, dans deux fichiers
distincts : elles ne partagent rien, et les fusionner créerait un couplage entre
deux règles indépendantes.

**Ce qui reste implicite, et qui l'assume.** Le corps exact de chaque épreuve des
tâches 4 à 8 est décrit par son intention et ses assertions plutôt que recopié
en entier : l'exécutant a le harnais (`HarnaisHttp`, `DemandeurMutable`) et six
suites existantes comme modèle. Ce qui n'est PAS laissé implicite, parce que s'y
tromper coûte cher : les codes de statut (404 contre 403), le `with check` en
plus du `using`, et le troisième rôle sur chaque table nouvelle.

---

## Journal d'exécution — ce que le plan n'avait pas prévu

Écrit au fil des tâches. Un plan qui ne se corrige pas ment sur ce qui a été
fait.

### Le seuil de couverture décide où vivent les épreuves

Le plan plaçait toutes les épreuves dans `Palier.Database.Tests`. C'était faux :
le seuil de 100 % de `Palier.Application` est mesuré par
**`Palier.Application.Tests` et par lui seul**. Un contrat couvert uniquement
par une épreuve d'intégration fait tomber le seuil — ce qui est arrivé dès la
tâche 2, `PageDeSeances` ayant été écrit avant son gestionnaire.

C'est le mécanisme voulu, pas un obstacle : `CLAUDE.md` § 4 en fait la seule
détection de code mort **public** dont Roslyn est incapable. Les bords se
jugent donc en unitaire, en microsecondes ; l'intégration ne garde que ce qui
exige un vrai moteur — isolation, clés étrangères, contraintes d'unicité.

Il l'a prouvé une seconde fois à la tâche 3 : deux accesseurs d'`AjoutDeSerie`
n'étaient lus nulle part, donc rien n'aurait rougi si le gestionnaire avait
ignoré le drapeau d'échauffement en écrivant en base.

### Trois prédicats nouveaux au domaine

Le plan supposait qu'on validerait en appelant les fabriques du domaine.
Celles-ci **lèvent**, et une valeur hors bornes tapée par un utilisateur est
une saisie, pas un défaut du programme. `Charge.EstValide`,
`Repetitions.EstValide`, `Rir.EstValide` et `Masse.EstValide` ont donc été
ajoutés, et les fabriques s'appuient dessus : les bornes ne sont écrites
qu'une fois.

### Le curseur est un COUPLE, pas un instant

Le plan proposait `?avant={instant}`. Deux séances peuvent porter le même
`started_at` — un import, deux entraînements notés à la minute près — et un
`<` strict en aurait sauté une définitivement. Le curseur porte donc
`(started_at, id)`, comparé par `EF.Functions.LessThan` sur un tuple, que
Npgsql traduit en row value PostgreSQL.

### La garde de perte de poids était SILENCIEUSE

Découvert en la branchant, et c'est le défaut le plus grave trouvé pendant ce
lot. `PerteDePoidsRapide.EstDetectee` suppose des entrées **hebdomadaires** ;
la série brute de `body_weight` est quotidienne chez qui pèse tous les jours.
La détection serait restée muette exactement chez les utilisateurs les plus
assidus.

**D61** tranche : moyenne hebdomadaire, semaines ISO. Voir `docs/decisions.md`.

### Le ratio tirage/poussée est REPORTÉ, et voici pourquoi

`docs/05-entrainement.md` § 4 le définit, `VolumeParGroupe.SurSeptJours` sait
le calculer depuis le lot 3, et `SerieEffectuee` attend un `RoleMouvement`.

**Ce rôle n'existe nulle part en base.** `exercises` porte `primary_muscles` et
`secondary_muscles` ; aucune colonne ne dit si un mouvement tire ou pousse. Le
déduire des muscles serait faux — un pull-over travaille les pectoraux ET le
grand dorsal, un rowing inversé et un développé partagent le deltoïde — et un
ratio faux vaut moins que pas de ratio : il ferait modifier un programme sur
une mesure inventée.

La colonne se pose au lot qui **remplit** le catalogue (étape 1 bis, 250 à 400
exercices), où le rôle se renseigne avec le reste. L'ajouter maintenant
créerait une colonne vide sur un catalogue vide — le « garde-fou sans cible »
que D39 refuse.

Le volume par muscle, lui, est livré : il ne demande que la vue
`weekly_volume`, qui existe depuis le lot 2.
