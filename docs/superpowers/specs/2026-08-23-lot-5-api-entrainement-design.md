# Lot 5 — l'API de l'entraînement

**Date :** 23 août 2026
**Statut :** premier jet, écrit en autonomie de nuit — **à relire et à valider avant toute exécution**
**Décisions sources :** D9, D36, D37, D39, D42
**Documents métier :** `docs/05-entrainement.md`, `docs/03-donnees.md`, `docs/07-roadmap.md` étape 1b

---

## 1. Objet, et une constatation qui change le périmètre

Le lot 5 est nommé « API — le reste du schéma y arrive, table par table ». **En le préparant, une chose est apparue : le premier tiers ne demande aucune table nouvelle.**

Le lot 2 a posé `workouts`, `sets`, `body_weight` et `exercises`. Le lot 3 a livré le domaine — `ForceEstimee`, `DetectionPlateau`, `VolumeParGroupe`, les grandeurs et les deux gardes de sécurité. Le lot 4 a livré l'identité et le pipeline qui pose cette identité en base.

**Ce qui manque entre les deux : les cas d'usage.** `Palier.Application` ne contient aujourd'hui que le pipeline, les autorisations et les sessions. Aucun gestionnaire n'existe — l'attribut `[GestionnaireDeCasDUsage]` n'est porté par personne.

Le lot 5 écrit donc ces gestionnaires, et les points d'entrée qui les appellent. **Les tables nouvelles arrivent ensuite, avec le cas d'usage qui les exige** — c'est exactement ce que D39 demande.

---

## 2. Ce que le socle garantit déjà, et qu'il ne faut pas réécrire

**L'identité est posée par le pipeline, et par lui seul.** `IExecuteurDeCasDUsage` ouvre la transaction et y pose l'identité du demandeur ; `ArchitectureTests` refuse tout type qui prendrait `PalierDbContext` en dehors de ce chemin. Un cas d'usage n'a donc **jamais** à filtrer sur `owner_id` : le moteur le fait, et RLS mord.

**C'est ce qui rend le lot 5 court.** Un `SELECT * FROM workouts` dans un gestionnaire rend les séances de l'appelant, et rien d'autre. Écrire le filtre à la main serait au mieux redondant, au pire une divergence entre deux façons de dire la même chose.

**Le domaine ne connaît pas la base.** `ForceEstimee` prend une charge et des répétitions, elle rend une estimation ; c'est le cas d'usage qui va chercher les séries. La règle de `CLAUDE.md` — « les calculs vivent dans des modules purs et testés, jamais dans les composants » — vaut aussi pour les gestionnaires.

---

## 3. Les cas d'usage, par ordre de dépendance

### 3.1 La séance — le cœur

```
POST   /api/v1/seances                    ouvre une séance
POST   /api/v1/seances/{id}/series        ajoute une série
PATCH  /api/v1/seances/{id}               clôt la séance, note l'énergie
GET    /api/v1/seances                    liste, paginée
GET    /api/v1/seances/{id}               une séance et ses séries
DELETE /api/v1/seances/{id}               supprime
```

**Aucune table nouvelle.** `workouts` porte déjà `energy_1_5` avec sa contrainte `CHECK`, `sets` porte la jointure, et les deux index de D39 servent exactement ces requêtes.

**Ce qui se juge ici** : qu'une séance ouverte par A soit invisible pour B — ce que `IsolationTests` prouve déjà au niveau du moteur, et qu'il faut prouver une seconde fois au niveau du cas d'usage, parce que ce sont deux garanties différentes.

### 3.2 Le poids corporel

```
POST /api/v1/poids        enregistre une mesure
GET  /api/v1/poids        la série, sur une fenêtre
```

`body_weight` porte `unique (owner_id, measured_on)` : deux mesures le même jour sont un remplacement, pas un doublon. **Le point d'entrée doit donc être idempotent par jour**, et le dire — sans quoi l'utilisateur qui corrige sa saisie reçoit une erreur de contrainte qu'il ne comprend pas.

**C'est ici que la sécurité mord.** `PerteDePoidsRapide` et `RestrictionSevere`, livrées au lot 3, se déclenchent sur cette série. Elles ne bloquent rien : elles produisent un constat que l'écran affichera. Le lot 5 les **appelle** et rend leur verdict ; il ne rédige aucun message — `01-conformite.md` sépare informer de prescrire, et le libellé vit ailleurs.

### 3.3 La force estimée et le volume

```
GET /api/v1/exercices/{id}/force-estimee   1RM estimé, et sa fiabilité
GET /api/v1/volume-hebdomadaire            par groupe musculaire
```

**Aucun calcul nouveau** : le domaine les porte, éprouvés à 100 % de couverture. Le cas d'usage lit les séries et les passe. La vue `weekly_volume` existe déjà.

### 3.4 Le catalogue d'exercices

```
GET    /api/v1/exercices              catalogue public + les siens
POST   /api/v1/exercices              crée un exercice personnalisé
DELETE /api/v1/exercices/{id}         supprime UN SIEN
```

`exercises` est la forme « catalogue mixte » : `owner_id` nullable, politique déjà posée. **La suppression d'un exercice public doit échouer**, et l'épreuve doit la provoquer.

---

## 4. Les tables nouvelles, et pourquoi elles n'arrivent pas toutes

D39 : « les treize autres tables arrivent au lot qui les utilise ». Le lot 5 en exige **deux**, et pas davantage :

| Table               | Le cas d'usage qui l'exige                                       | Forme                 |
| ------------------- | ---------------------------------------------------------------- | --------------------- |
| `exercise_feedback` | le ressenti par exercice — `05-entrainement.md` § 5              | possédée par jointure |
| `user_constraints`  | l'adaptation par contrainte — § 4, « le second différenciateur » | possédée directe      |

Les onze restantes appartiennent à la nutrition, à l'abonnement et à l'assistant. **Les poser ici produirait onze tables qu'aucun cas d'usage n'exerce** — le « garde-fou sans cible » que D39 refuse nommément.

Chacune arrive avec sa politique RLS, ses privilèges pour les trois rôles, et son épreuve d'isolation. **Le lot 4b a montré ce qui se paie quand on l'oublie** : `grant select on ALL TABLES` ne couvre pas les tables nées après lui, et trois épreuves de sauvegarde ont refusé un `pg_dump` devenu incomplet.

---

## 5. La forme d'un gestionnaire

```csharp
[GestionnaireDeCasDUsage]
internal sealed class OuvrirUneSeance(PalierDbContext contexte)
{
    public async Task<Guid> ExecuterAsync(OuvertureDeSeance demande, CancellationToken jeton)
    {
        // Aucun filtre sur owner_id : le pipeline a posé l'identité, RLS mord.
        // L'écrire ici serait une seconde façon de dire la même chose.
        var seance = new Workout { StartedAt = demande.Debut, Notes = demande.Notes };
        contexte.Workouts.Add(seance);
        await contexte.SaveChangesAsync(jeton).ConfigureAwait(false);
        return seance.Id;
    }
}
```

**L'attribut n'est pas décoratif** : `ArchitectureTests` refuse le dépôt si un type prend `PalierDbContext` sans le porter. C'est le seul mécanisme qui garantisse qu'aucun chemin n'atteint la base hors du pipeline.

---

## 6. Ce que ce lot ne contient pas

- **Aucun écran.** Ils appartiennent au lot 6, avec le socle d'écran et la direction visuelle.
- **Rien de la nutrition.** Elle est fermée à deux verrous depuis le lot 4, et son API est un lot à part entière.
- **Aucun programme généré.** `05-entrainement.md` § 1 décrit des structures selon la fréquence ; les proposer demande une conception produit qui n'a pas eu lieu.
- **Aucune duplication de séance.** `12-confort.md` la prévoit ; elle se pose mieux quand les écrans existent.

---

## 7. Les questions ouvertes — celles qui reviennent au porteur

**1. D39 n'est toujours pas validée.** Elle porte, en toutes lettres : « arbitrage à confirmer par le porteur du projet — sans sa validation explicite, cette décision n'est pas prise ». Le lot 2 a livré six tables au lieu de dix-neuf sur cette base, et le lot 5 continue sur la même. **Il faut trancher**, dans un sens ou dans l'autre, avant que l'écart grandisse encore.

**2. Le périmètre du lot 5.** Ce document propose : séances, séries, poids, force estimée, volume, catalogue, ressenti, contraintes. C'est cohérent et fermé, mais c'est **un choix** — on pourrait s'arrêter aux séances et aux séries, et livrer plus tôt.

**3. La pagination.** `GET /api/v1/seances` rend une liste qui grandit sans fin. Curseur ou décalage ? Le curseur est plus juste sur une liste où l'on insère ; le décalage est plus simple. Aucun document du dossier ne tranche.

**4. Les contraintes de `user_constraints`.** `05-entrainement.md` § 4 dit « adaptation par contrainte » sans énumérer les contraintes. Liste fermée en base, ou texte libre ? Une liste fermée se traduit et se raisonne ; un texte libre ne se raisonne pas et finit dans un prompt de modèle, ce que `01-conformite.md` encadre.

---

## 8. Pourquoi ce document s'arrête ici

`CLAUDE.md` § 1 : « Jamais de code avant qu'un plan ait été approuvé. » Le lot 4b faisait exception dans les faits parce qu'il fermait des exigences écrites noir sur blanc dans un document opposable — il n'y avait rien à arbitrer, seulement à exécuter.

**Le lot 5 est différent.** Les quatre questions ci-dessus changent ce qu'il faut écrire, et deux d'entre elles touchent au schéma — que `CLAUDE.md` § 6 range parmi les sujets sur lesquels je ne dois pas trancher seul.

La conception est donc écrite, et l'exécution attend.
