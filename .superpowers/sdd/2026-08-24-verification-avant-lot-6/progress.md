# Vérification complète avant le lot 6

_24/08/2026. Demandée par le porteur : « avant de passer au point 6 tu dois faire
une vérification complète »._

**Un défaut réel trouvé et fermé. Une instabilité mesurée et attribuée. Un
chiffre corrigé.**

---

## 1. Le défaut : le référentiel était inapplicable, partout

`npm run db:referentiel` est le chemin par lequel les 255 exercices et les 9
programmes arrivent en production. Il échouait sur **toute** machine :

```
  02-exercises.sql … ÉCHEC
  Error response from daemon: No such container: palier-db
```

**La cause.** `scripts/referentiel.mjs` appelle `docker exec palier-db`.
`db/compose.yaml` déclarait un SERVICE nommé `palier-db` mais aucun
`container_name` : Compose engendre alors `<projet>-<service>-<n>`, soit
`db-palier-db-1`. Le service et le conteneur sont deux choses différentes, et
c'est cette confusion qui a coûté le défaut.

**Pourquoi rien ne le signalait.** `ReferentielExercicesTests` et
`ReferentielProgrammesTests` appliquent les fichiers `.sql` par **Npgsql** — un
chemin qui ne passe jamais par Docker. Les épreuves prouvaient que le SQL est
correct ; elles ne pouvaient rien dire du script qui l'applique. Seul un essai
manuel pouvait le montrer, et c'est ce qu'une vérification complète est censée
faire.

**La correction, en rouge-vert.**

| Étape                                                   | Preuve                                                     |
| ------------------------------------------------------- | ---------------------------------------------------------- |
| L'épreuve rougit AVANT la correction                    | `expected undefined to be truthy` — aucun `container_name` |
| `container_name: palier-db` posé                        | —                                                          |
| L'épreuve passe                                         | `Tests 1 passed`                                           |
| Conteneur recréé, référentiel relancé **sans variable** | `4 fichier(s) de référentiel appliqué(s)`                  |

L'épreuve vit dans `front/tests/harness/commandes.test.ts` et compare le nom que
`referentiel.mjs` attend au `container_name` que `compose.yaml` déclare. Elle
rougira le jour où l'un des deux bougera sans l'autre.

---

## 2. L'instabilité : préexistante, mesurée, non expliquée

`front:test` échoue par intermittence sur
`oxlint-architecture.test.ts > refuse une promesse flottante plantée dans tests/`.

**Attribution, mesurée et non supposée.** Mes modifications ont été mises de
côté par `git stash`, et la suite lancée trois fois sur l'état antérieur :

```
passage 1 :  Tests  1 failed | 117 passed (118)
passage 2 :  Tests  118 passed (118)
passage 3 :  Tests  118 passed (118)
```

**Un échec sur trois, sans mes modifications.** L'instabilité leur est donc
antérieure.

**Ce que l'échec dit.** L'épreuve écrit une sonde en violation dans `tests/`,
lance `npm run lint:types`, et vérifie DEUX choses : que le code de sortie n'est
pas nul, et que la sortie porte `no-floating-promises`. C'est la seconde qui
rougit — le code était bien non nul, mais pour une autre raison que le refus
attendu.

C'est exactement le mode de défaillance que le dépôt documente au ruling P8 :
« un code non nul ne prouve pas que l'outil a refusé ». **Ici la double
assertion a fait son travail** — une épreuve à une seule assertion serait passée
au vert en mentant.

**Je n'ai pas la cause.** L'hypothèse plausible est une contention : quatorze
fichiers d'épreuves tournent en parallèle, plusieurs lancent des outils externes
lourds, et `lint:types` rend une sortie partielle sous charge. C'est une
hypothèse, pas une mesure, et ce dépôt refuse de présenter l'une pour l'autre.

**C'est la SECONDE occurrence de ce mode.** Le lot 5 en a consigné une sur
`back:test`, § 7 de son journal. Deux instabilités du même genre, sur deux
suites différentes, méritent qu'on cherche la cause plutôt que de les compter.

---

## 3. Ce qui est prouvé, et par quelle mesure

| Ce qui est affirmé                             | La preuve                                                             |
| ---------------------------------------------- | --------------------------------------------------------------------- |
| `npm run verify` passe                         | Seize étapes, aucune ligne `ÉCHEC sur`, 525 s                         |
| Le dépôt est aligné                            | `dd79af1` en local ET sur `origin/coffre/secrets-okms`                |
| Les migrations s'appliquent sur une vraie base | `dotnet ef database update` → `Done.`                                 |
| Le référentiel s'applique par son chemin réel  | 4 fichiers, sans variable d'échappement                               |
| 255 exercices                                  | `select count(*) … is_custom = false` → **255**                       |
| 216 variantes                                  | → **216**, dont 18 réciproques                                        |
| 9 programmes, 36 séances, 157 poses            | → **9 / 36 / 157**                                                    |
| Aucun exercice sans clé de recherche           | → **0**                                                               |
| RLS activée ET forcée sur les trois tables     | `relrowsecurity` et `relforcerowsecurity` à `t`, 3 politiques chacune |

---

## 4. Un chiffre corrigé

J'avais annoncé **172 variantes** au porteur. La base en compte **216** : mon
comptage datait d'avant le troisième fichier d'exercices. Aucun document ne
portait le chiffre faux — il n'a vécu que dans la conversation.

---

## 5. Ce qui reste dû avant le lot 6

Rien de ceci ne bloque les écrans, mais rien ne doit être oublié :

1. **La relecture par un kinésithérapeute** — 255 exercices et 9 programmes.
   `14-contenu.md` § 2 l'exige. C'est la seule chose qui manque à ce contenu
   pour être livrable, et elle ne peut pas venir d'une session.
2. **D61, la moyenne hebdomadaire** du garde-fou de perte de poids. Corrigée en
   fenêtres glissantes par D64, mais elle touche un seuil de santé et attend le
   même avis clinique.
3. **Les seuils du ressenti** — « 2 `pain` consécutifs → orientation vers un
   professionnel ». Vérifié : rien dans le code. La règle arrive avec l'écran
   qui la montre.
4. **L'instabilité du § 2**, et sa jumelle du lot 5.
5. **Le lot 4b et le coffre OKMS** — environ 22 000 lignes jamais passées en
   revue.
