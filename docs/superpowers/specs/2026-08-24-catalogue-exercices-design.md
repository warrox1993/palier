# Le catalogue d'exercices — conception

**Date :** 24 août 2026
**Décisions sources :** D39, D62, D63
**Documents métier :** `docs/14-contenu.md` § 1, `docs/05-entrainement.md` § 4 et § 6,
`docs/16-projet.md` § 4, `docs/17-donnees-sources.md`

---

## 1. Ce que l'étude a renversé

J'avais signalé deux questions comme appartenant au porteur : **les champs
manquants au schéma** et **d'où vient le contenu**. La relecture des documents
en a tranché une, et a montré que l'autre n'était pas une question.

**`docs/16-projet.md` § 4 nomme déjà le fichier, son dossier et sa source :**

| Fichier            | Contenu                                  | Source                   |
| ------------------ | ---------------------------------------- | ------------------------ |
| `02-exercises.sql` | 60 exercices prioritaires puis extension | Rédigé, relu par le kiné |

« Rédigé » — pas importé. La question « d'où vient le contenu » était donc
déjà répondue, et le tableau porte même le périmètre du premier jalon.

**Et le reste de la chaîne existe déjà**, posé au lot 2 sans jamais avoir servi :

- `db/referentiel/`, vide, avec son `.gitkeep`
- `db/SOURCES.md`, dont le tableau est vide et dit pourquoi : « le contrôle a
  besoin de sa cible, et une cible absente se signale au lieu de se remplacer »
- la règle `source-non-attribuee` de `scripts/regles-projet.mjs`, qui refuse le
  dépôt si un `*.sql` du référentiel n'a pas sa ligne d'attribution
- la politique `migrations_referentiel` sur `exercises`, qui autorise
  `palier_migrations` à écrire sous `FORCE ROW LEVEL SECURITY`

**Ce lot est le premier client de ce dispositif.** Il ne le construit pas, il
l'emprunte — et le fait de l'emprunter est ce qui prouvera qu'il fonctionne.

---

## 2. Ce qui manque vraiment

### 2.1 Le mécanisme d'application

`scripts/db.mjs` sait lever, arrêter et réinitialiser la base. **Il ne sait pas
charger le référentiel**, et aucune commande npm ne le fait.

`docs/16-projet.md` § 4 dit seulement où les données vivent et sous quel rôle
elles arrivent. Le comment reste à écrire.

### 2.2 Six colonnes et une table

`docs/14-contenu.md` § 1 : « Chaque entrée porte : nom **français et anglais**,
équipement, muscles primaires et secondaires, caractère unilatéral, incrément de
charge par défaut, régions contre-indiquées, **consignes d'exécution**,
**erreurs fréquentes**, **variantes liées**. »

La table en porte cinq sur neuf.

---

## 3. Les décisions

### D66 — le catalogue est RÉDIGÉ, jamais importé

Confirmée par `16-projet.md`, et voici pourquoi c'est aussi le bon choix.

**Les bases libres sont anglophones.** wger, Free Exercise DB et leurs dérivées
portent des noms et des consignes en anglais. Le produit exige le français ET
l'anglais dès la V1 (`11-qualite.md` § i18n) : il faudrait traduire 400 entrées,
c'est-à-dire les rédiger.

**Leurs licences contaminent.** `17-donnees-sources.md` traite l'ODbL avec une
prudence explicite — attribution, share-alike, keep-open — et pose une règle qui
tranche : « ne jamais fusionner les sources dans une table unifiée enrichie,
c'est exactement ce qui créerait une base dérivée au sens de l'ODbL ». Or un
catalogue d'exercices EST une table unifiée enrichie : on y ajoute des
contre-indications, des consignes, un rôle de mouvement. Importer sous ODbL
obligerait à rouvrir le catalogue entier sous ODbL.

**Et leurs contre-indications ne sont pas les nôtres.** `05-entrainement.md` § 4
donne un tableau précis de quatre régions et de leurs exclusions. Une base
externe porte d'autres catégories, d'autres seuils, d'autres silences.

**Ce qui la rouvrirait :** une base sous licence permissive (MIT, CC0), en
français, dont les contre-indications suivent le § 4. Elle n'existe pas
aujourd'hui.

### D67 — bilingue par COLONNES suffixées, pas par table de traductions

`name_fr`, `name_en`, `instructions_fr`, `instructions_en`, `common_errors_fr`,
`common_errors_en`.

**Deux langues, figées par le document** : « Français et anglais dès la V1 ».
Aucune troisième n'est annoncée.

**Et surtout, `not null` rend l'oubli impossible.** Une table
`exercise_translations` permettrait un exercice sans sa ligne anglaise —
silencieusement, et l'écran afficherait un trou. Des colonnes contraintes
refusent l'insertion. Sur une table de référence qu'on remplit à la main, cette
différence est ce qui compte.

**Ce qui la défait :** une troisième langue. Ce jour-là, la table de traductions
vaudra sa migration — et on saura ce qu'elle coûte parce qu'on aura mesuré ce
que les colonnes coûtaient.

### D68 — un SLUG comme clé naturelle du catalogue

`slug text`, unique **là où `is_custom = false`**.

Il résout deux problèmes d'un coup :

1. **L'idempotence du référentiel.** `02-exercises.sql` doit pouvoir être rejoué
   — après un `db:reset`, après une correction. Un `on conflict (slug) do update`
   le rend rejouable ; sans clé naturelle, il faudrait figer des UUID dans le
   fichier, ce qui les rendrait impossibles à relire.
2. **Les variantes.** Un exercice se lie à un autre par son slug, pas par un
   UUID qu'aucun humain ne peut vérifier.

Nul pour les exercices personnalisés : l'utilisateur n'écrit pas de slug, et
l'unicité ne doit pas l'empêcher de nommer son exercice comme un exercice du
catalogue.

### D69 — le rôle du mouvement arrive ICI, et ferme le report de D62

D62 reportait la colonne `movement_role` avec un motif précis : « la colonne se
pose au lot qui **remplit** le catalogue, où le rôle se renseigne exercice par
exercice, avec le reste. L'ajouter maintenant créerait une colonne vide sur un
catalogue vide. »

**C'est ce lot.** `movement_role` arrive donc, en liste fermée — `tirage`,
`poussee`, `aucun` — et le ratio tirage/poussée de `05-entrainement.md` § 4
devient calculable. Le deltoïde latéral y est `aucun`, comme le document
l'exige : « il ne tire ni ne pousse ».

### D70 — les champs du catalogue sont exigés du CATALOGUE, pas des exercices personnalisés

Un utilisateur qui crée son exercice donne un nom, des muscles, un incrément. Il
n'écrit ni consignes d'exécution, ni traduction anglaise.

Les colonnes sont donc **nullables**, et une contrainte les exige quand
`is_custom = false` :

```sql
check (
  is_custom
  or (slug is not null and name_en is not null and instructions_fr is not null
      and instructions_en is not null and common_errors_fr is not null
      and common_errors_en is not null)
)
```

**C'est le garde-fou qui compte.** Sans lui, une entrée incomplète entrerait au
catalogue public — visible de tous, affichée sans consigne — et rien ne le
signalerait. Une épreuve la provoque.

### D71 — les variantes par TABLE DE LIENS, jamais par tableau d'UUID

`exercise_variants(exercise_id, variant_id)`, deux clés étrangères.

Un `uuid[]` aurait coûté une colonne au lieu d'une table. Mais PostgreSQL ne
contraint pas les références dans un tableau : une variante qui pointe sur un
exercice supprimé y resterait, et la proposition de remplacement du § 6 —
« un exercice qu'on n'aime pas est un exercice qu'on cesse de faire » —
proposerait un exercice qui n'existe plus.

Le dépôt refuse ailleurs les raccourcis qui pourrissent en silence ; celui-ci
n'est pas différent.

---

## 4. Ce que ce lot NE prétend pas

**Le catalogue n'est PAS relu par un professionnel.** `docs/14-contenu.md` § 2
l'exige — « relus par un kinésithérapeute pour la partie contraintes » — et cette
relecture n'a pas eu lieu.

Les contre-indications livrées sont **dérivées du tableau de
`05-entrainement.md` § 4**, mécaniquement : un mouvement à charge axiale est
contre-indiqué cervicale, une charnière lourde est contre-indiquée lombaire, et
ainsi de suite. C'est une application du document, pas un avis médical.

**Cela doit se voir, pas se supposer.** Le fichier de référentiel le porte en
tête, `db/SOURCES.md` le porte dans sa colonne Source, et le journal de bord du
lot le redit. C'est la seule façon honnête de livrer un contenu de santé qui
attend encore sa validation.

**Aucune illustration.** `14-contenu.md` § 1 les chiffre à 3-4 semaines de
travail vectoriel. Elles appartiennent à l'étape 1 bis, pas à ce lot.

---

## 5. Le périmètre retenu

**Soixante exercices**, comme le tableau de `16-projet.md` § 4 le dit, choisis
selon `14-contenu.md` : « les 60 mouvements couvrant 90 % des programmes ».

Répartis par famille, avec les mouvements socles de `05-entrainement.md` § 6 en
premier — presse, charnière de hanche, développés, tirages — puis les
accessoires qui les complètent.

**Les incréments ne sont pas inventés** : `05-entrainement.md` § 3 les donne —
« 5 kg à la presse, 2,5 kg aux poulies et machines, 2 kg aux haltères, 1 kg sur
les élévations et les mouvements de rotateurs ».
