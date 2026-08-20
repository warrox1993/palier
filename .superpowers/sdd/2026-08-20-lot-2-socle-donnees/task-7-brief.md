# Brief — Tâche 7

> Extrait de `docs/superpowers/plans/2026-08-20-lot-2-socle-donnees.md`. C'est la source unique de tes exigences.
> Les valeurs exactes — code, chemins, chaînes de caractères — se reprennent **verbatim**.

## Contraintes globales

- **Node 24** (`.nvmrc`) et **.NET 10** (`global.json`). **npm** côté front, jamais pnpm ni yarn ; en CI `npm ci`.
- **Branche de travail :** `feat/lot-2-socle-donnees`. Dépôt distant `github.com/warrox1993/palier`, privé, branche par défaut `main`.
- **Messages de commit en français, à l'impératif, minuscule initiale.** Indexer des **chemins nommés** (`git commit -m <msg> -- <fichiers>`), jamais `git add -A` : plusieurs agents peuvent travailler dans le même worktree.
- **Deux assertions par épreuve, sans exception — D19.** Le **code de sortie** _et_ un **motif propre à l'outil**. Un code non nul prouve qu'il s'est passé quelque chose, jamais que l'outil a refusé : Prettier absent rend le code 2, Playwright sans test rend un code non nul, `gitleaks` introuvable rend 1 sous `cmd.exe`. Trois épreuves du lot 1 seraient passées au vert sans rien contrôler.
- **L'exclusion appartient à la commande, jamais au fichier de configuration — D20.** Quatre outils du lot 1 ont porté ce piège : un fichier ignoré par la configuration **reste ignoré même nommé explicitement en argument**. Pour chaque outil nouveau de ce lot, la question se **pose et se mesure** : _un fichier ignoré reste-t-il ignoré quand on le nomme en argument ?_
- **Aucun commentaire dans un fichier de configuration d'outil — D21.** Une clé inconnue peut désactiver la règle entière **en silence** : un `"_note"` a fait passer neuf violations à code 0. Le motif d'un choix vit dans l'épreuve qui le protège. Exception : les formats qui portent nativement des commentaires — YAML, `.mjs`, TOML — **à condition de l'avoir vérifié sur cet outil-là**.
- **Une branche jamais franchie est une branche qui ment.** Chaque épreuve de ce plan doit être **vue rouge** en provoquant sa violation, puis vue verte après le correctif. Une épreuve qui n'a été vue que verte n'a rien prouvé. Et **un réglage accepté sans erreur n'est pas un réglage appliqué** (P11) ; **un fichier correct dont le service est éteint ne protège rien** (D30).
- **Le franchissement en quatre questions :** _tourne-t-il_ · _mord-il_ · _refuse-t-il d'enregistrer à moitié_ · **crie-t-il quand il n'a plus de cible, ou que son fichier manque ?** Le premier test de chaque fichier d'épreuve vérifie que sa cible existe.
- **Aucune version de paquet figée dans les commandes d'installation.** Les versions citées dans le texte sont des **relevés du 20/08/2026**, à revérifier ; les fichiers de verrouillage figent.
- **Toute dépendance ajoutée passe la liste blanche de licences** — MIT, Apache-2.0, BSD, ISC, PostgreSQL (D13), par `node scripts/verifier-licences.mjs`.
- **`Palier.Domain` ne bouge pas.** Aucun calcul de conformité n'entre dans ce lot. La couverture à 100 % ne s'applique qu'à `[Palier.Domain]*` — `back/coverage.runsettings` le filtre, et **il ne faut pas y soumettre le nouveau projet de tests d'intégration**.
- **Aucun secret dans le dépôt.** Le mot de passe local du compose reste en clair — gitleaks l'accepte sous cette forme — mais **une chaîne de connexion complète `Host=…;Password=…` dans un fichier de code est refusée**. Les chaînes vont en `dotnet user-secrets` ou en variable d'environnement, jamais dans `appsettings.Development.json`.
- **Aucune chaîne de caractères en dur** côté front : tout passe par i18next, y compris les erreurs et les états vides.
- **`npm run verify` reste le point d'entrée unique — D25.** Aucun contrôle de ce lot ne crée une seconde liste. Le projet de tests d'intégration entre dans `back/Palier.sln`, donc dans l'étape `back:test`, donc dans `verify` — **sans étape nouvelle**.

### Les deux voies, et le blocage matériel

**Mesuré le 20/08/2026 sur le poste :** `wsl --status` rend « Le Sous-système Windows pour Linux n'est pas installé ». `Microsoft-Windows-Subsystem-Linux` et `Microsoft-Hyper-V` sont en **InstallState 2** (désactivés), seul `VirtualMachinePlatform` est à **1**. Le client `docker` 29.6.2 et `docker compose` v5.3.1 **répondent parfaitement** pendant que le démon est injoignable sur `npipe:////./pipe/dockerDesktopLinuxEngine`.

La tâche 0 exige des **droits administrateur et un redémarrage**. Personne d'autre que le porteur du projet ne peut la faire. Le plan est donc organisé en deux voies, et **la voie sèche se termine sans jamais attendre**.

| Tâche  | Titre court                                                         | Docker                                                                                  | Voie              |
| ------ | ------------------------------------------------------------------- | --------------------------------------------------------------------------------------- | ----------------- |
| **0**  | Prérequis matériel — WSL2 ou Hyper-V                                | c'est elle qui le débloque                                                              | **porteur**       |
| **1**  | Le dossier `db/` et son outillage                                   | non                                                                                     | sèche             |
| **2**  | `db/compose.yaml`, scripts `db:*`, branche « Docker éteint »        | **la branche de refus se franchit maintenant** ; la branche de succès attend la tâche 0 | sèche puis Docker |
| **3**  | Jetons, i18next, `chaine-en-dur` étendue, les trois exceptions Knip | non                                                                                     | sèche             |
| **4**  | Les trois dettes de l'intégration continue                          | non                                                                                     | sèche             |
| **5**  | Le projet de tests d'intégration, les trois rôles, le tag unique    | oui                                                                                     | Docker            |
| **6**  | La migration `SocleInitial`                                         | oui                                                                                     | Docker            |
| **7**  | Accesseurs, politiques, huit épreuves d'isolation                   | oui                                                                                     | Docker            |
| **8**  | La pose de l'identité côté C#, et la garde applicative              | oui                                                                                     | Docker            |
| **9**  | `GET /api/v1/sante`, `back/.env.example`, assertion de démarrage    | oui                                                                                     | Docker            |
| **10** | Sauvegarde et restauration sous `FORCE`                             | oui                                                                                     | Docker            |
| **11** | L'écran d'état, ses quatre états, le parcours au clavier            | oui                                                                                     | Docker            |
| **12** | Remesurer `verify` et arbitrer                                      | oui                                                                                     | Docker            |
| **13** | La reprise documentaire et le journal des décisions                 | non                                                                                     | sèche             |

**Ordre imposé :** 0 (en parallèle, par le porteur) · puis 1 → 2 → 3 → 4 sans attendre quoi que ce soit · puis 5 → 6 → 7 → 8 → 9 → 10 → 11 → 12 dès que la tâche 0 est franchie · puis 13.
Les tâches **3 et 4** sont indépendantes de 1 et 2 : elles peuvent partir en parallèle.

### Ce que ce lot n'écrit pas

Aucune authentification (les tables `AspNet*` sont **créées**, aucune ligne de code d'authentification n'est écrite) · aucun calcul de conformité · aucun appel à un modèle · ni Dexie ni service worker ni PWA · aucun déploiement, aucun `Dockerfile`, **aucun compte OVHcloud ouvert** · treize des dix-neuf tables · aucun écran produit · ni CQRS ni `Mediator.SourceGenerator` (D12 prévoit le repli « handlers écrits à la main » — **donc l'exception de licence que D13 renvoie au lot 2 n'est pas due, et il faut le dire au lieu de la laisser expirer en silence**) · pas de Tailwind (D42) · aucun formatage automatique du SQL (Prettier n'a aucun parseur SQL natif) · **aucune donnée réelle nulle part**.

---

## Tâche 7 : Les deux accesseurs, les politiques des cinq formes, et les huit épreuves d'isolation

**Docker : REQUIS. C'est le cœur du lot.**

**Fichiers :**

- Créer : `back/Palier.Database.Tests/IsolationTests.cs`
- Modifier : la migration `SocleInitial` (schéma `app`, accesseurs, politiques), `db/README.md`

**Interfaces :**

- Consomme : le schéma (tâche 6), la fixture en `palier_app` (tâche 5)
- Produit : les politiques et les accesseurs. Consommés par les tâches 8 à 11.

- [ ] **Étape 1 : le schéma `app` et ses DEUX accesseurs**

| Fonction                    | Comportement                                              | Pour                       |
| --------------------------- | --------------------------------------------------------- | -------------------------- |
| `app.utilisateur()`         | **lève**, `errcode 28000`, message **sans aucune valeur** | tables strictement privées |
| `app.utilisateur_ou_null()` | rend **NULL**                                             | tables à branche publique  |

Toutes deux lisent `current_setting('app.utilisateur', true)` — avec `missing_ok` à **`true`** : « If there is no such setting, `current_setting` throws an error **unless `missing_ok` is supplied and is `true`** (in which case NULL is returned) ». Sans ce second argument, la fonction lèverait une erreur **différente** de celle qu'on veut, et le message ne dirait pas ce qu'on croit.

> **Deux accesseurs, pas un.** Une politique de catalogue public qui appelle une fonction **qui lève** transforme toute lecture anonyme en erreur 500. Et le contournement naïf — `using (is_custom = false or owner_id = app.utilisateur())` — ne marche pas : la documentation **ne garantit aucun court-circuit**, elle dit que les expressions « will be evaluated for each row », et les politiques permissives multiples sont combinées par `OR` **sans ordre garanti**. La levée peut donc partir sur une ligne publique.

**Le message de la levée ne porte aucune valeur.** Ni identifiant, ni donnée. `01-conformite.md` § 4 : « Aucune donnée de santé dans les logs applicatifs ». La tentation d'y ajouter l'identifiant « pour déboguer » sera forte — l'épreuve de journalisation de la tâche 9 la garde.

- [ ] **Étape 2 : les politiques, chacune enveloppée dans un sous-select**

| Table                     | Politique                                                                                                                                       |
| ------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------- |
| `body_weight`, `workouts` | `using (owner_id = (select app.utilisateur()))` et `with check` identique                                                                       |
| `sets`                    | `using (exists (select 1 from workouts w where w.id = sets.workout_id and w.owner_id = (select app.utilisateur())))`                            |
| `exercises`               | **deux politiques permissives distinctes** : l'une `using (is_custom = false)`, l'autre `using (owner_id = (select app.utilisateur_ou_null()))` |
| `nutrient_refs`           | lecture publique, **écriture refusée**                                                                                                          |
| `AspNet*`                 | **aucune politique** — refus par défaut (D38)                                                                                                   |

> **Le sous-select n'est pas une optimisation, c'est ce qui supprime un faux arbitrage.** La documentation tranche la question laissée ouverte : l'expression est évaluée **par ligne**. `(select …)` force un **InitPlan** évalué une fois par instruction. On garde le `plpgsql` qui lève **et** la vitesse ; on n'échange pas la conformité contre la latence.
>
> **Réserve à connaître :** l'InitPlan étant évalué paresseusement, il **aggrave** le trou de la table vide. Raison de plus pour la garde applicative de la tâche 8.

**`AspNet*` sans politique, et c'est délibéré (D38).** « If no policy exists for the table, a default-deny policy is used, meaning that no rows are visible or can be modified. » La parade évidente — pas de RLS sur ces tables, ou `using (true)` — ferait de la **seule table sans barrière de ligne** celle qui portera les empreintes de mots de passe, les secrets TOTP, les jetons de rafraîchissement et les sessions. Le chemin de connexion lit `AspNetUsers` **par email avant que la moindre identité existe** : il ne peut pas passer par `app.utilisateur()`. **Le lot 4 doit concevoir ce chemin explicitement** — rôle dédié avec sa politique, ou fonction `SECURITY DEFINER` au périmètre minimal, **jamais une pose de l'identité d'autrui**. Poser le refus ici garantit qu'il sera _conçu_ et non _découvert_.

- [ ] **Étape 3 : `idle_in_transaction_session_timeout` sur le rôle applicatif**

`alter role palier_app set idle_in_transaction_session_timeout = …`

Motif : un cas d'usage appelant un modèle laisserait la transaction **ouverte pendant l'appel réseau** — _idle in transaction_, VACUUM bloqué, connexions épuisées. Aucun appel de modèle n'existe à ce lot ; la borne se pose maintenant parce qu'elle coûte une ligne et qu'elle ne se posera plus jamais au bon moment.

Et **un réglage accepté sans erreur n'est pas un réglage appliqué** (P11) : vérifier par `select setting from pg_settings` sur une connexion `palier_app`, pas en relisant la commande.

- [ ] **Étape 4 : écrire les huit épreuves d'isolation — toutes sur un vrai moteur, en `palier_app`**

`IsolationTests.cs`. Chaque test porte **deux assertions** : le comportement attendu **et** le motif (code SQL, nombre de lignes, nom de table).

| #     | Épreuve                                                        | Attendu                                                                                                                          |
| ----- | -------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------- |
| **1** | Table **non vide**, aucune identité posée                      | **erreur SQL `28000`**, jamais zéro ligne                                                                                        |
| **2** | `set_config(..., false)` au lieu de `true`                     | après `COMMIT`, sur la **même connexion**, `current_setting('app.utilisateur', true)` rend NULL ou vide, **et** une requête lève |
| **3** | A lit une ligne de B                                           | zéro ligne                                                                                                                       |
| **4** | A insère au nom de B                                           | refus par `WITH CHECK`                                                                                                           |
| **5** | `sets` : A ne voit aucune série d'une séance de B              | zéro ligne                                                                                                                       |
| **6** | Deux identités successives sur la **même connexion physique**  | la seconde ne voit **jamais** les lignes de la première                                                                          |
| **7** | `exercises` lu **sans identité**                               | les lignes `is_custom = false` **sortent, sans erreur** ; les lignes possédées ne sortent pas                                    |
| **8** | `palier_app` lit `AspNetUsers` → zéro ligne ; y insère → refus | les deux                                                                                                                         |

**L'épreuve 1 s'appelle explicitement « sur table peuplée » et porte son motif en commentaire :**

```csharp
// SUR TABLE PEUPLÉE, et le nom du test le dit.
// `ddl-rowsecurity.html` : l'expression d'une politique « will be evaluated
// FOR EACH ROW ». Sur une table VIDE, la politique n'est JAMAIS évaluée :
// aucune exception, zéro ligne. Cette épreuve y passerait pour la mauvaise
// raison. Le trou de la table vide est fermé par la garde applicative de la
// tâche 8, pas ici.
```

**L'épreuve 2 est la plus décisive du lot, et elle manquait à toutes les propositions antérieures.**

```
1. connexion unique, pooling désactivé (Pooling=false), ou une seule connexion physique
2. BEGIN ; select set_config('app.utilisateur', <A>, true) ; select ... ; COMMIT
3. sur la MÊME connexion, sans rien poser :
   select current_setting('app.utilisateur', true)   →  DOIT rendre NULL ou ''
   select * from workouts (table NON VIDE)           →  DOIT lever 28000
```

> **Pourquoi elle est décisive.** Le moteur ne garantit **rien du tout** si le troisième argument vaut `false` : la valeur passe en portée session, et l'on retombe exactement sur le mécanisme écarté — nettoyage délégué à Npgsql, annulé par `No Reset On Close`, le multiplexing, ou un PgBouncer en mode transaction, où la matrice marque `SET/RESET` comme « Never ».
>
> **Et l'épreuve 6 passe au vert avec `false`** : la seconde requête pose sa propre identité, écrase la précédente, et ne voit que ses lignes. Le test est vert, le mécanisme est cassé. Sans l'épreuve 2, **toute la sûreté du dispositif tient à un littéral booléen dans une ligne de C# que rien ne regarde** — la signature exacte du faux vert que le lot 1 a chassé vingt-trois fois.
>
> _Variante nommée et écartée :_ `SET LOCAL` porte la localité dans sa **syntaxe** et ne peut pas être fausse — mais elle n'accepte aucun paramètre lié et obligerait à concaténer un identifiant dans du SQL, **sur le chemin qui existe précisément pour se protéger de l'injection**. L'arbitrage est « argument lié » contre « localité syntaxique » ; il est tranché en faveur de l'argument lié, **et l'épreuve 2 remplace la garantie syntaxique**.

**Détail qui échoue bruyamment mais qu'il vaut mieux écrire :** `set_config` attend un `text` en deuxième argument. Un `Guid` passé en paramètre EF part en `uuid` et ne trouve pas la fonction. `.ToString()` est **obligatoire**.

- [ ] **Étape 5 : franchissement — les huit épreuves vues ROUGES**

Chacune se franchit en désarmant son correctif, **une par une** :

| #    | Ce qu'on désarme                                                                     | L'épreuve doit rougir                                                                                                     |
| ---- | ------------------------------------------------------------------------------------ | ------------------------------------------------------------------------------------------------------------------------- |
| 1    | retirer la politique de `workouts`                                                   | oui                                                                                                                       |
| 2    | passer le troisième argument de `set_config` à `false`                               | **oui — c'est le franchissement le plus important du lot**                                                                |
| 3, 4 | remplacer `using` par `using (true)`                                                 | oui                                                                                                                       |
| 5    | retirer le `exists` de `sets`                                                        | oui                                                                                                                       |
| 6    | poser l'identité hors transaction                                                    | oui                                                                                                                       |
| 7    | fusionner les deux politiques d'`exercises` en un seul `OR` avec `app.utilisateur()` | l'épreuve rougit, ou devient **instable selon le plan** — les deux sont des échecs, et l'instabilité est le pire des deux |
| 8    | ajouter `using (true)` sur `AspNetUsers`                                             | oui                                                                                                                       |

> **Une branche jamais franchie est une branche qui ment.** L'épreuve doit **rougir quand on désarme le correctif**, pas seulement passer quand tout va bien. Consigner les huit rouges au journal du lot, violation par violation. Une épreuve qui n'a été vue que verte n'a rien prouvé.

- [ ] **Étape 6 : mesurer le coût, plutôt que le supposer**

Le mécanisme ajoute **trois** allers-retours par cas d'usage — `BEGIN`, `set_config`, `COMMIT` — **pas un**. Mesurer la durée d'une requête sur `sets` avec un `exists` sur `workouts`, dont la politique de `workouts` s'applique **à son tour** dans la sous-requête. Comparer au plafond de **100 ms** de `docs/08-workflow.md` § 6.

> Le plafond **n'est pas acquis**, et la mesure porte sur Testcontainers, pas sur l'instance managée qui n'existe pas. Écrire les deux chiffres avec leur instrument, et dire ce qu'ils ne prouvent pas.

- [ ] **Étape 7 : commit**

```bash
git commit -m "eprouve l isolation des cinq formes de table" -- back db
```

---

---
