# État du projet et point de reprise

_Écrit le 25/08/2026 à la demande du porteur : « on reprend jeudi » — soit le **27/08/2026**._

**À lire en premier par la session qui reprend.** `CLAUDE.md` § 1 impose de relire l'état avant toute action, y compris avant une question de clarification. Ce fichier est cet état, mesuré et non remémoré.

---

## 1. Reprendre — les quatre commandes, dans l'ordre

```bash
git log --oneline -15 main            # ce qui est arrivé pendant l'attente
ls .superpowers/sdd/                  # le dernier répertoire nomme le lot en cours
tail -60 docs/decisions.md            # les décisions prises entre-temps
npm run db:up && npm run db:referentiel
```

**La dernière ligne n'est pas optionnelle.** La base locale a été recréée le 25/08 par un `db:down` / `db:up` : son volume est vide, et les migrations comme le référentiel doivent être réappliqués. Sans cela, les épreuves d'intégration montent leur propre conteneur et passent, pendant que toute mesure faite à la main sur `palier-db` ment.

Pour appliquer les migrations à la base locale :

```bash
export ConnectionStrings__PalierMigrations="Host=localhost;Port=5432;Database=palier;Username=palier_migrations;Password=motdepasse_local_migrations"
dotnet ef database update --project back/Palier.Infrastructure --startup-project back/Palier.Infrastructure --context PalierDbContext
```

---

## 2. L'état, mesuré le 25/08/2026

|                |                                                                                           |
| -------------- | ----------------------------------------------------------------------------------------- |
| Branche        | `coffre/secrets-okms`, alignée sur `origin`                                               |
| Dernier commit | `aa3a41e` — corrige la nutrition à la source                                              |
| Tables posées  | **10 sur 19** que `docs/03-donnees.md` prévoit                                            |
| Routes         | **43**                                                                                    |
| Épreuves       | ~1 000 — couverture **100 %** sur `Domain` et `Application`, 96/79/71 sur les adaptateurs |
| Décisions      | **D1 à D79**                                                                              |
| Exercices      | 255, avec 216 variantes                                                                   |
| Programmes     | **60** — 11 fondamentaux, 49 issus des méthodes                                           |
| Front          | **759 lignes, un seul écran** — celui de l'état du socle                                  |

---

## 3. Ce qui a été fait le 24 et le 25/08

| Commit    | Contenu                                                                   |
| --------- | ------------------------------------------------------------------------- |
| `dd79af1` | Le système de programmes — 3 tables, RLS, 7 routes, 11 modèles. D72 à D75 |
| `67b0869` | Deux études en fan-out — méthodes et sexe. **D78 et son garde-fou**       |
| `2deefa8` | 49 programmes issus des méthodes, après trois tours de machine            |
| `aa3a41e` | La nutrition corrigée à la source. **D79**                                |

**Quatre workflows, environ 190 agents, 22 millions de jetons.** Chaque conclusion contestée par un agent sceptique remontant aux sources primaires — c'est ce dispositif qui a produit tout ce qui suit.

---

## 4. CE QUI RESTE SUR LE BACKEND, dans l'ordre recommandé

### 4.1 — `profiles` et le verrou d'âge — LE SEUL POINT DE NON-CONFORMITÉ

`docs/13-juridique.md` § 1 exige un refus d'inscription sous 16 ans et un verrou sur la nutrition, le poids et la progression entre 16 et 17 ans. **Rien de cela n'est appliqué** : `POST /api/v1/poids` accepte n'importe qui.

Ce n'est pas un oubli mais une dépendance — le produit ne connaît pas l'âge tant que `profiles` n'existe pas.

**La règle est déjà écrite et éprouvée** : `Palier.Domain/Comptes/AgeDuCompte.cs`, quatre bornes provoquées — la veille des 16 ans, le jour des 16, la veille des 18, le jour des 18 — plus les dates impossibles. Il reste à :

1. poser la table `profiles` (schéma en `03-donnees.md` l. 45-56), sa migration, sa RLS ;
2. **rendre `birth_date` immuable** — « non modifiable ensuite sans intervention du support » ;
3. brancher `AccesDuCompte` sur les routes existantes de poids, et sur celles de nutrition à venir ;
4. provoquer chaque refus sur un vrai moteur.

### 4.2 — `measurements` et `cardio_sessions`

Petites, indépendantes, sans piège connu. Schéma en `03-donnees.md` l. 152-168.

### 4.3 — La décision sur `nutrient_refs.statut`, AVANT de coder la nutrition

`docs/04-nutrition.md` § 7 point 8 : la table manque **trois** colonnes — `perimetre`, `forme_chimique` et surtout `statut`.

Sans `statut`, toute ligne dont `ul` est non nul devient une limite haute établie. Or le fer (40 mg) et le manganèse (8 mg) portent des _safe levels of intake_, dont l'EFSA écrit qu'« intakes above the safe levels of intake do not necessarily mean that there is a risk ». Le produit annoncerait un dépassement là où l'autorité l'interdit.

Second point, structurel : `ai_male`, `ai_female` et `ul` cohabitent dans **la même ligne**. La ligne magnésium vaut donc `(350, 300, 250)` — une cible au-dessus de son propre plafond, parce que les deux ne portent pas sur le même périmètre.

**C'est un changement de schéma : il demande une décision datée avant tout code.**

### 4.4 — La nutrition

`foods`, `intake_entries`, `user_targets`, `supplements`. Le plus gros bloc restant.

**Le domaine est prêt** : `Repartir` répartit sous contrainte et ne peut plus produire de nombre négatif ni lever sur un profil banal. `Hydratation.Calculer` rend une cible de boissons fondée sur le sexe.

**Quatre valeurs de micronutriments sont périmées** et attendent chacune leur vérification à la source : vitamine B6 (12 mg et non 25), sélénium (255 µg et non 300), fer (aucune UL n'existe), niacine (deux UL, rapport 90).

### 4.5 — L'hydratation

`hydration_containers`, `hydration_entries`. **Décision ouverte** : rattacher ou non un profil minéral aux contenants — une eau minérale du haut de la fourchette apporte 60 % de la limite haute du magnésium en 1,5 L, sans aucun complément.

### 4.6 — Le lot 8, la couche modèle

`ILlmProvider`, l'abstraction multi-fournisseur, le journal des appels — horodatage, fournisseur, modèle, tâche, jetons, **jamais le contenu**. Rien n'est commencé.

---

## 5. Ce qui ne relève pas du code, et qui bloque la mise en ligne

**La relecture par un kinésithérapeute** — 255 exercices et 60 programmes. `docs/14-contenu.md` § 2 l'exige. La réserve est écrite en tête de chaque fichier de référentiel, dans `db/SOURCES.md` et dans le journal des décisions.

**La relecture par un diététicien** — les quatorze points du § 7 de `04-nutrition.md`, dont le choix du référentiel gouvernant : EFSA et ANSES divergent frontalement sur les lipides (20-35 % contre 35-40 %, qui ne se recoupent qu'au point unique de 35).

**Aucune machine ne remplace ces deux relectures.** Trois tours de vérification les rendent plus rapides, pas inutiles.

---

## 6. Les pièges de ce dépôt, à connaître avant de perdre du temps

**Le push ment.** `git push` peut rendre **code 0** sans que rien ne parte — le hook `pre-push` relance `verify`, qui dure dix minutes. **Toujours contrôler le distant** : `git log --oneline -1 origin/<branche>`. Voir la mémoire dédiée.

**`verify` est instable — quatre occurrences le 24 et le 25/08.** Un outil externe lancé depuis une épreuve, sous charge parallèle, rend un code non nul sans avoir travaillé : seuil de couverture sur `back:test`, sortie partielle de `lint:types` sur `front:test`, restauration NuGet sur `harnais:back`. **Mesuré : un échec sur trois passages, sur un état antérieur à toute modification.** Relancer suffit, mais l'un d'eux a déjà bloqué un push. La cause n'est pas connue.

**Un workflow peut se mettre en PAUSE sans le dire.** Un fichier de sortie vide ne veut pas dire « en cours ». `TaskStop` distingue les trois états ; un run en pause se reprend par `Workflow({scriptPath, resumeFromRunId})`, et les agents déjà rendus reviennent du cache sans consommer un jeton.

**Le heredoc bash mange les échappements.** Trois occurrences dans cette session, dont deux sur des `\\` en C#. Écrire les fichiers avec l'outil `Write`, jamais par heredoc, dès qu'une contre-oblique est en jeu.

**Le budget de recherche web est de 200 appels par session**, et il a été épuisé le 25/08. La position de l'ISSN sur les protéines n'a pas pu être vérifiée : elle est nommée dans `04-nutrition.md` **sans aucune valeur attribuée**, et c'est écrit dans le document.

---

## 7. Une chose à ne pas défaire

`ArchitectureTests.AUCUN_type_d_entrainement_ne_prend_le_SEXE_en_dependance` protège une **absence**. Deux études — 64 agents — concluent qu'aucune différence liée au sexe ne justifie d'adapter un programme, et que brancher le sexe sur une décision d'entraînement fabriquerait une différence que la littérature ne soutient pas.

L'épreuve a été provoquée : une sonde posée dans `Palier.Application.Entrainement` a été refusée sur ses deux voies. Le sexe reste légitime côté **nutrition** — Mifflin-St Jeor en dépend.
