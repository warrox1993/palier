# Lot 1 — Harnais et socle d'outillage

**Date :** 19 août 2026
**Étape de la roadmap :** 1, premier des sept lots issus de son découpage
**Statut :** design validé, en attente du plan d'implémentation

---

## 1. Objet

Mettre en place l'environnement déterministe dans lequel tout le reste sera construit, et **prouver que chacun de ses garde-fous refuse effectivement ce qu'il prétend refuser**.

`08-workflow.md` § 5 : « Ces éléments sont en place avant la première fonctionnalité. » Ce lot ne produit aucune ligne de code produit.

### Ce que le lot couvre

Ossature du projet, TypeScript strict, ESLint, Prettier, Vitest, Playwright, axe-core, détection de code mort, règles de qualité, hooks pre-commit et pre-push, chaîne CI complète avec sécurité, dépôt GitHub protégé, et la suite de franchissement qui éprouve le tout.

### Ce que le lot ne couvre pas

Aucun composant, aucune route, aucun jeton de design appliqué, aucune connexion à Supabase, aucun appel à un modèle, aucun déploiement. Ces éléments appartiennent aux lots 2 à 7.

### Découpage de l'étape 1

| Lot | Contenu | Dépend de |
|---|---|---|
| **1** | Harnais et outillage | rien |
| 2 | Socle applicatif : PWA, routage, jetons de design, i18n, quatre états | lot 1 |
| 3 | Supabase : projet, migrations, schéma, RLS et tests de politiques, seed | compte Supabase |
| 4 | Auth : Google OAuth + email/mot de passe, vérification, limitation de débit, 2FA | lots 2 et 3 |
| 5 | Résilience : Dexie, file de retry persistée, indicateur de synchronisation | lot 2 |
| 6 | `LLMProvider` : interface, deux implémentations, routage, journal des coûts | clés API + lot 3 |
| 7 | Déploiement Vercel, spend limit, supervision | lots 1 et 2 |

---

## 2. Décisions arrêtées

| Décision | Choix | Motif |
|---|---|---|
| Nom du projet | `palier` | Recommandation de `15-marque.md` § 3. **La recherche d'antériorité BOIP et EUIPO reste à faire avant tout dépôt de marque.** Le dépôt et le paquet ne préemptent rien tant qu'il n'y a ni domaine ni utilisateur |
| Gestionnaire de paquets | npm | Cohérent avec `DEMARRAGE.md`, déjà autorisé dans `.claude/settings.json`, détecté nativement par Vercel |
| Branche principale | `main` | Convention GitHub et attente du brief. Renommage de `master` |
| Fonctions serveur | Vercel Functions, dossier `api/` | `16-projet.md` impose que tout appel aux modèles passe par une fonction serveur. Format portable vers Cloudflare Workers, contrairement aux services que `CLAUDE.md` interdit nommément — KV, Blob, Edge Config |
| Référentiel de sécurité | OWASP ASVS niveau 2 + OWASP Top 10 CI/CD | ASVS L2 est le niveau prévu pour une application traitant des données sensibles au sens de l'article 9 RGPD. Le Top 10 CI/CD couvre le pipeline, que le Top 10 applicatif ignore |
| Vérification du harnais | Suite de franchissement permanente | Un garde-fou non éprouvé ment. Rejouée à chaque exécution de CI, elle détecte une dégradation ultérieure le jour même |
| Sévérité des règles | Structurel bloquant, goût en avertissement | Les règles qui protègent l'architecture et la conformité mordent. Celles qui relèvent du jugement informent et se traitent en revue |

---

## 3. Ossature

```
palier/
├── .github/
│   ├── workflows/ci.yml
│   └── dependabot.yml
├── api/                          # fonctions serveur Vercel
├── src/
│   ├── app/                      # routes
│   ├── features/                 # par domaine métier
│   ├── core/                     # modules purs, testés, sans dépendance UI
│   ├── llm/
│   ├── ui/
│   ├── lib/
│   └── locales/{fr,en}.json
├── supabase/{migrations,seed,tests}/
├── tests/
│   ├── unit/  integration/  e2e/  compliance/
│   └── harness/                  # suite de franchissement
│       └── fixtures/             # violations délibérées, hors build
├── docs/
│   ├── securite/asvs-l2.md
│   └── superpowers/specs/
├── .env.example
├── eslint.config.js  knip.json  .jscpd.json
├── vite.config.ts  vitest.config.ts  playwright.config.ts
├── tsconfig.json  package.json  vercel.json
└── .nvmrc
```

Deux écarts assumés par rapport à `16-projet.md`, qui fait autorité sur l'arborescence : l'ajout de `api/` et de `tests/harness/`. Tout le reste est repris à l'identique.

Les dossiers que le lot 1 ne remplit pas ne sont pas créés. `src/features/nutrition/` naîtra au lot qui en a besoin ; `16-projet.md` décrit la cible, pas l'état du premier jour.

---

## 4. Chaîne d'outils

| Outil | Configuration | Ce qu'elle sert |
|---|---|---|
| TypeScript | `strict`, `noUncheckedIndexedAccess`, `exactOptionalPropertyTypes`, `noImplicitOverride` | erreurs avant exécution |
| ESLint 9, flat config | typescript-eslint, `no-explicit-any` en erreur | `CLAUDE.md` § 4 : « `any` interdit sauf justification en commentaire » |
| `eslint-plugin-jsx-a11y` | accessibilité à l'écriture | avant même axe-core |
| `eslint-plugin-i18next` | toute chaîne littérale dans le JSX | « aucune chaîne en dur, y compris erreurs et états vides » |
| `eslint-plugin-sonarjs` | duplication, complexité cognitive, chaînes répétées | qualité |
| `eslint-plugin-import` | cycles d'import, restrictions de chemins | architecture |
| Prettier + `eslint-config-prettier` | style unique | non négociable |
| Vitest + jsdom + Testing Library | `<fichier>.test.ts` à côté du source | convention `16-projet.md` § 2 |
| Playwright | Chromium **et WebKit** | produit PWA, mobile Safari compte |
| axe-core | `@axe-core/playwright` en e2e, `vitest-axe` en composant | « échec du build sur violation critique » |
| Knip | fichiers, exports, types, dépendances, clés i18n | code mort |
| jscpd | duplication de blocs | qualité |

**Version de Node :** fixée par `.nvmrc` et identique en local et en CI. La valeur retenue doit être alignée sur ce que supportent les fonctions Vercel — à vérifier à la création du projet Vercel, la version installée en local (24.16.0) n'étant pas nécessairement disponible côté hébergeur.

**Versions des paquets :** aucune version n'est figée dans ce document. Elles sont résolues à l'installation et verrouillées par le lockfile.

---

## 5. Règles mécanisées

Le principe : une règle écrite dans un document est violée dans trois mois ; une règle qui fait rougir la CI ne l'est jamais. Les règles ci-dessous existent déjà dans le dossier sous forme de prose.

### 5.1 Bloquantes — architecture et conformité

**Pureté de `src/core/`.** Interdiction d'import depuis `core/` vers `react`, `@supabase/*`, `dexie`, `src/lib/`, `src/ui/`, `src/features/`.

> `16-projet.md` § 1 : « `core/` ne contient que des fonctions pures, sans accès réseau ni base ni React. C'est ce qui rend les calculs testables à 100 % et auditables par le diététicien. »

Cette règle est aussi une règle de conformité : `01-conformite.md` § 3 fait reposer la séparation des deux couches dessus, et `08-workflow.md` § 6 en fait un critère de sortie — « aucun calcul nutritionnel ou de progression dans un composant ».

**Barrière de `src/llm/`.** Interdiction d'import depuis `llm/` vers tout module d'écriture sur `user_targets` et `supplements`.

> `06-ia.md` § 1, interdits du modèle : « écrire en base sur `user_targets` ou `supplements` ».

La barrière cesse d'être une consigne de prompt pour devenir une barrière de compilation.

**Conventions de nommage.** `@typescript-eslint/naming-convention` applique intégralement le tableau de `16-projet.md` § 2 : fichiers en `kebab-case.ts`, composants en `PascalCase.tsx`, fonctions et variables en `camelCase`, constantes en `SCREAMING_SNAKE_CASE`, types en `PascalCase` sans préfixe, booléens préfixés `is`/`has`/`can`, fonctions asynchrones nommées au verbe.

**Aucune chaîne d'interface en dur.** `eslint-plugin-i18next`, en erreur.

**Aucun `any` non justifié.** En erreur, dérogation par commentaire explicite comme le prévoit `CLAUDE.md` § 4.

**Aucune couleur hors des jetons.** Interdiction des littéraux hexadécimaux hors du fichier de jetons de design.

> `02-design.md` § 9 : « Aucune couleur hors des jetons définis. »

**Aucune variable ou import non utilisé.** `@typescript-eslint/no-unused-vars` en erreur.

**Aucun cycle d'import.** `import/no-cycle` en erreur.

### 5.2 Avertissements — jugement

Visibles dans la sortie de lint et le rapport de CI, non bloquants, traités en revue de code : duplication détectée par jscpd, complexité cognitive au-delà de 15, fichier au-delà de 300 lignes, nombres magiques hors `0`, `1`, `-1`, `2`.

**Motif de ce classement.** Un seuil de duplication trop serré produit mécaniquement de l'abstraction prématurée : deux fragments qui se ressemblent aujourd'hui mais changeront pour des raisons différentes doivent rester séparés. La duplication qui mérite d'être supprimée est celle qui n'a **qu'une seule raison de changer**, et cette distinction demande un jugement humain.

Ces seuils peuvent être resserrés lot par lot une fois le rythme pris.

### 5.3 Ce que rien de tout cela n'attrape

La duplication conceptuelle — deux fonctions qui font la même chose écrites différemment —, les mauvaises abstractions et le nommage trompeur. Ils relèvent de la revue de code entre chaque tâche, que `CLAUDE.md` impose déjà.

---

## 6. Code mort

**Seuil zéro dès le premier jour.** Sur un projet vide c'est gratuit ; sur un projet de six mois, les avertissements accumulés ne sont jamais traités et l'outil finit désactivé.

| Cible | Mécanisme | Où |
|---|---|---|
| Fichiers, exports, types jamais importés | Knip | CI + pre-push |
| Dépendances déclarées et jamais utilisées | Knip | idem |
| Clés i18n orphelines | Knip, greffon i18n | CI |
| Clés i18n utilisées mais absentes | Knip, greffon i18n | CI |
| Variables et imports non utilisés | ESLint | pre-commit |
| Lignes jamais exécutées dans `src/core/` | couverture à 100 % | CI |

Les deux sens du contrôle i18n comptent. Une clé déclarée jamais utilisée est du poids mort ; une clé utilisée jamais déclarée affiche une chaîne brute à l'utilisateur en production.

La couverture à 100 % sur `src/core/`, déjà exigée par `08-workflow.md` § 6, sert de second détecteur : dans un module pur, une ligne jamais couverte est soit du code mort, soit un test manquant.

**Faux positifs.** Knip signale les points d'entrée légitimes — routes, handlers de `api/`, fichiers de configuration, setup de tests — s'ils ne sont pas déclarés. Ils le seront explicitement dans `knip.json` dès la mise en place, et **le nombre de faux positifs écartés sera consigné dans le rapport de lot**. Un détecteur qui se trompe est un détecteur qu'on cesse de lire.

---

## 7. Chaîne CI/CD

### 7.1 Hooks locaux

| Hook | Contenu |
|---|---|
| pre-commit | lint et format sur les fichiers modifiés (lint-staged), détection de secrets (gitleaks) |
| pre-push | `tsc --noEmit`, tests unitaires, build, Knip |

Husky et lint-staged pour l'orchestration. Les tests complets restent au pre-push : un pre-commit qui prend trente secondes finit désactivé.

> `08-workflow.md` § 5 : « Refuse le commit si tests ou lint échouent », « refuse le push si le build échoue ».
> § 6 : « Aucun secret dans le dépôt — vérification automatisée. »

### 7.2 Pipeline

Quatre jobs parallèles, puis un job de synthèse bloquant :

```
qualité       lint · format · tsc --noEmit · tests unitaires · couverture · Knip · jscpd
sécurité      npm audit · gitleaks · Semgrep (règles OWASP)
build         build · budget de bundle · Lighthouse CI
e2e           Playwright (Chromium + WebKit) · axe-core
                     ↓
              franchissement du harnais        ← bloque la fusion
```

Le parallélisme et l'annulation des exécutions obsolètes (`concurrency group`) répondent à `08-workflow.md` § 5 : « Si le build prend deux minutes, l'agent tourne en rond. Investir dans la vitesse du harnais est prioritaire sur toute fonctionnalité. »

**Couverture :** 100 % sur `src/core/`, exigé nommément par `08-workflow.md` § 6. Aucun seuil global ailleurs — un chiffre global pousse à tester ce qui est facile.

**Ce qui fait échouer un job, et ce qui informe.** Dans le job qualité, lint, types, tests, couverture et Knip sont bloquants ; jscpd publie un rapport et ne fait jamais échouer, conformément au classement de la section 5.2. Dans le job build, le budget de bundle et Lighthouse CI sont bloquants, avec les seuils que fixe `12-confort.md` § 6 : **bundle initial sous 200 ko compressé**, **Lighthouse supérieur à 90 sur toutes les catégories**, premier affichage utile sous 1,5 s en 4G simulée.

**Réserve sur ces deux seuils au lot 1 :** appliqués à un squelette sans interface, ils passent trivialement et ne prouvent rien. Ils sont câblés ici pour exister avant le premier composant plutôt qu'après — c'est au lot 2 qu'ils commenceront à mordre. Le rapport de lot doit le dire, sous peine de présenter une CI verte comme une preuve de performance.

**Choix du scanner statique.** CodeQL, le scanner natif de GitHub, est gratuit sur les dépôts publics ; sur un dépôt privé il relève d'une offre payante. Le dépôt étant privé, le scan statique repose sur **Semgrep** et la détection de secrets sur **gitleaks**, tous deux libres. À reconfirmer au moment de l'activation, les offres GitHub évoluant.

### 7.3 Sécurité de la chaîne

Ces quatre points ne figurent dans aucun document du dossier et relèvent du Top 10 CI/CD :

- **Actions GitHub épinglées par empreinte SHA**, jamais par tag. Un tag est mutable : une action compromise puis repointée exécuterait du code arbitraire avec accès aux secrets du dépôt.
- **`permissions:` explicite et minimal** dans chaque workflow, `contents: read` par défaut. GitHub accorde des droits larges en l'absence de déclaration.
- **`npm ci` et jamais `npm install` en CI** : le lockfile fait foi, aucune résolution surprise.
- **Protection de branche sur `main`** : pas de push direct, CI verte obligatoire, historique linéaire.

### 7.4 Livraison

| Environnement | Déclencheur | Base |
|---|---|---|
| Preview | chaque PR | projet Supabase de recette |
| Production | fusion sur `main` | projet Supabase de production |

> `14-contenu.md` § 5 : « Aucune migration n'atteint la production sans être passée en recette. »

Les migrations s'appliquent donc à la recette dans le pipeline de PR, puis à la production après fusion. Retour arrière par promotion d'un déploiement antérieur, sans reconstruction.

En-têtes de sécurité déclarés dans `vercel.json` — CSP stricte, HSTS, X-Content-Type-Options, Referrer-Policy, Permissions-Policy — et vérifiés par un test de bout en bout qui les lit sur l'environnement de preview. `08-workflow.md` § 6 en fait un critère de sortie : « en-têtes vérifiés en production ».

**Spend limit Vercel activé à la création du projet**, conformément à `CLAUDE.md` § 3, « dès le jour 1 ».

La configuration effective de Vercel appartient au lot 7. Le lot 1 produit `vercel.json` et le pipeline ; il ne déploie pas.

### 7.5 Maintenance

- Dependabot hebdomadaire sur `npm` **et sur `github-actions`** — le second est presque toujours oublié, alors que c'est lui qui porte le risque d'exécution
- Mises à jour mineures regroupées en une PR, majeures séparées
- Vulnérabilités critiques corrigées sous 7 jours, comme l'impose `08-workflow.md` § 6
- La suite de franchissement tourne à chaque exécution : un garde-fou qui cesse de mordre après une mise à jour fait rougir la CI le jour même

---

## 8. Sécurité — ancrage ASVS

`docs/securite/asvs-l2.md` liste les exigences de l'ASVS niveau 2, chacune portant un état :

| État | Signification |
|---|---|
| `couverte` | avec le mécanisme qui la couvre et, s'il existe, le test qui la prouve |
| `non applicable` | avec le motif |
| `prévue lot N` | avec le lot qui la traitera |

Les exigences testables rejoignent `tests/compliance/`, dont `08-workflow.md` § 6 dit qu'elle « bloque le déploiement au même titre que les tests unitaires ».

Le lot 1 ne couvre pas ASVS L2. Il installe l'instrument, renseigne l'état initial de chaque exigence, et traite celles qui relèvent de la chaîne de build et de la gestion des secrets. Les exigences de contrôle d'accès reviennent au lot 3, celles d'authentification au lot 4, celles de journalisation au lot 7.

**Version du référentiel.** L'édition de l'ASVS et celle du Top 10 retenues seront relevées à la mise en place et inscrites en tête du fichier. Les numérotations OWASP ont changé récemment ; aucun identifiant n'est cité de mémoire dans ce document.

---

## 9. Suite de franchissement

### Mécanique

On ne peut pas laisser une violation dans le dépôt pour prouver qu'un contrôle mord — la CI serait rouge en permanence. Les violations vivent donc dans `tests/harness/fixtures/`, **exclues du typecheck et du build principaux**, et chaque épreuve lance l'outil concerné en sous-processus sur sa fixture pour vérifier qu'il **sort en erreur**.

### Épreuves

| Garde-fou | Violation provoquée | Attendu |
|---|---|---|
| TypeScript strict | `any` non justifié | `tsc` échoue |
| ESLint i18next | chaîne littérale dans du JSX | lint échoue |
| ESLint pureté `core/` | import de `react` depuis `core/` | lint échoue |
| ESLint nommage | fichier en `camelCase.ts` | lint échoue |
| Prettier | fichier mal formaté | `format:check` échoue |
| Vitest | test délibérément rouge | commande échoue |
| pre-commit | commit avec lint cassé | commit refusé |
| pre-push | push avec build cassé | push refusé |
| axe-core | bouton sans nom accessible | e2e échoue |
| gitleaks | fausse clé au format reconnu | détection |
| Knip | export orphelin | `knip` échoue |
| Couverture | fonction `core/` non testée | seuil échoue |

### Épreuve de la cible manquante

Chaque épreuve vérifie en outre que **la disparition de sa fixture la fait échouer**, au lieu de la faire passer au vert par absence de sujet. C'est le défaut le plus fréquent de ce type de suite, et celui qui la rend mensongère : un contrôle qui n'a plus rien à contrôler doit crier, pas approuver.

---

## 10. Critère de sortie

Le lot est terminé quand, et seulement quand :

- une pull request passe la CI complète au vert — quatre jobs, plus le franchissement
- les **douze épreuves** prouvent chacune un refus effectif
- l'épreuve de cible manquante prouve un échec, sur chacune des douze
- le dépôt GitHub privé existe, `main` protégée, historique linéaire
- `.env.example` versionné, `.env` ignoré, gitleaks muet sur l'ensemble de l'historique
- `docs/securite/asvs-l2.md` initialisé, chaque exigence portant un état
- Knip et jscpd exécutés, code mort à zéro, nombre de faux positifs écartés consigné
- les douze points de la définition de terminé de `08-workflow.md` § 9, pour ceux qui s'appliquent à un lot sans interface

---

## 11. Reporté, et à quel lot

| Élément | Lot | Motif |
|---|---|---|
| Sentry, sonde de disponibilité, alertes | 7 | rien à superviser avant qu'une application tourne |
| Scan dynamique OWASP ZAP | 7 | exige une application déployée avec des parcours réels |
| Limitation de débit | 4 et 6 | appartient aux lots qui créent les points d'entrée |
| Suppression EXIF, URL signées | lot de la saisie photo | idem |
| Tests de politiques RLS | 3 | il n'y a pas de table |
| Jetons de design appliqués | 2 | il n'y a pas de composant |
| Configuration Vercel effective | 7 | le lot 1 produit `vercel.json`, il ne déploie pas |

---

## 12. Points ouverts

Aucun ne bloque le lot 1. Tous doivent être tranchés avant les lots qu'ils concernent.

| Point | Bloque | Détail |
|---|---|---|
| Avatar « mode Miroir » | lot 5 de la roadmap | `10-progression.md` § 7 le construit sur les mensurations réelles, ce que `01-conformite.md` § 5 interdit « sans exception » et que `10-progression.md` § 1 interdit lui-même |
| Table du journal des libellés | lot 3 | exigée par `09-comptes.md` § 6, absente de `03-donnees.md` |
| Table du journal des appels au modèle | lots 3 et 6 | exigée par `06-ia.md` § 2 et `13-juridique.md` § 2, absente de `03-donnees.md` |
| Coût IA : 3 €/mois ou moins d'1 € | aucun | `00-produit.md` et `06-ia.md` divergent d'un facteur trois sur le même mix |
| Définition de « V1 » et « V2 » | lot 7 | jamais définies, alors que Stripe, l'avatar, la 2FA et l'export s'y réfèrent |
| `features/admin/` absent de l'arborescence | lot 7 | `09-comptes.md` § 6 exige l'interface du diététicien |
| Version de Node supportée par Vercel | lot 7 | à relever avant de figer `.nvmrc` |
| Recherche d'antériorité BOIP et EUIPO | dépôt de marque | `15-marque.md` § 4 la demande avant de s'attacher au nom |

---

## 13. Risques

**Le harnais paraît disproportionné pour un dépôt sans code.** C'est le pari explicite de `08-workflow.md` § 5, et il s'inverse dès le lot 2 : chaque règle mécanisée au lot 1 est une règle que personne n'aura à faire respecter à la main pendant six mois.

**Un excès de règles bloquantes rend le travail pénible.** D'où le classement en deux niveaux : seules l'architecture et la conformité bloquent. Le reste informe.

**Knip et jscpd peuvent produire du bruit.** Traité par la déclaration explicite des points d'entrée et par la consignation des faux positifs. Si le bruit persiste au lot 2, la configuration se corrige — l'outil ne se désactive pas.

**Une dépendance du harnais peut devenir non maintenue.** Dependabot le signale ; le franchissement détecte le jour même qu'un contrôle a cessé de mordre.
