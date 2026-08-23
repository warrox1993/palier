# 07 — Séquence de construction

> **Repris le 20/08/2026** pour appliquer les décisions D9 à D43. Ce document était le **dernier
> du dossier** à décrire encore Supabase et Vercel sans en-tête de reprise, alors qu'il est de
> **rang 3** dans la hiérarchie de `CLAUDE.md` § 5 : un agent qui applique cette hiérarchie lit le
> rang 3 et réintroduit une pile abandonnée.
>
> **Trois lignes seulement étaient factuellement fausses** — celle d'ESLint (D18), celle du projet
> Supabase (D9, D15, D17) et celle du déploiement Vercel (D15). Le reste est soit vrai tel quel,
> soit vrai **mais devenu plus cher**, et c'est la seconde catégorie qui compte : une ligne qui
> n'a pas changé pendant que son coût était multiplié ment plus efficacement qu'une ligne fausse.
>
> **Les jugements de ce document ne dépendaient d'aucune pile technique et sont conservés mot pour
> mot** : « toute estimation plus courte est une estimation fausse », « si le point 2 échoue, le
> projet devient un journal d'entraînement », « un produit qu'on ne veut pas utiliser soi-même ne
> se vend pas », le seuil de refus de la bêta, le paragraphe sur le marché mondial et celui sur
> l'avatar 3D. Seul le **chiffre** de la durée est révisé, et il l'est avec sa base.

**On séquence la livraison, jamais la qualité.** Chaque étape ci-dessous se termine par un ensemble fini : testé, accessible, traduit, avec ses états d'erreur. La gestion hors ligne en a été retirée le 20/08/2026 — D45, elle est le chantier du lot 7. Une étape n'est pas close tant que la définition de terminé de `08-workflow.md` n'est pas cochée intégralement.

Aucune étape ne produit un prototype. L'ordre existe parce qu'on ne peut pas écrire dix mille lignes simultanément, pas parce qu'on accepterait de livrer à moitié.

---

## Durée — révisée le 20/08/2026

**Toute estimation plus courte est une estimation fausse.** Cette phrase est conservée. Le chiffre unique qu'elle accompagnait ne l'est pas, parce qu'il confondait **trois natures de temps** dont une seule se compresse.

| Nature                                     | Ce que c'est                                                                                                                         | Compressible ?                                              |
| ------------------------------------------ | ------------------------------------------------------------------------------------------------------------------------------------ | ----------------------------------------------------------- |
| **(a) Semaines de développement**          | écrire le code, les tests, les migrations, les écrans                                                                                | **Oui** — c'est la seule, et c'est le pari agentique        |
| **(b) Fenêtres calendaires irréductibles** | trois semaines d'usage personnel (étape 2) **et** huit semaines de bêta (étape 8) — **onze semaines**, écrites dans ce document même | **Non.** Aucune architecture, aucun agent ne les raccourcit |
| **(c) Délais externes de l'étape 0**       | avocat, diététicien, statut, assurance, marque, kinésithérapeute, DPA                                                                | **Non**, et **rien n'est engagé au 20/08/2026**             |

**Ce qui fonde (a), et sa réserve.** Mesuré sur ce dépôt : `git log` porte **91 commits sur deux journées calendaires** (22 le 19/08/2026, 69 le 20/08/2026) et le lot 1 est livré. La réserve est aussi importante que le chiffre — **un harnais est le travail le plus accélérable qui soit** : déterministe, vérifiable par une commande, sans jugement produit et sans dépendance externe. Rien ne prouve que ce rythme tienne sur le domaine nutritionnel, dont chaque libellé passe par un diététicien, ni sur le catalogue d'exercices, dont l'étape 1 bis dit elle-même qu'il est « le poste le plus long et le plus sous-estimé ».

**Estimation révisée : 8 à 12 mois** jusqu'à l'ouverture payante, si une fourchette unique est maintenue.

**C'est une estimation, pas une mesure, et voici sa base.** Elle part des 6 à 9 mois d'origine et y ajoute ce que le changement d'architecture du 19/08 a déplacé du fournisseur vers nous. Les trois lignes ci-dessous sont estimées en conception du lot 2, le 20/08/2026 ; aucune n'est constatée.

| Ce qui l'allonge                       | Estimation                                                        | Sur quelle base                                                                                                                                                                                                                                                                                                                                                                                                                                                                                     |
| -------------------------------------- | ----------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **D17 — authentification maison**      | **+3 à 5 semaines**                                               | le relevé de `09-comptes.md` § 1 chiffre exigence par exigence ce qu'ASP.NET Identity fournit : sur **sept** exigences, deux sont natives à la configuration près, deux sont partielles, une n'a qu'un point d'extension (`IPasswordValidator`), une n'a que son magasin, et **une est entièrement à construire** — rotation des jetons de rafraîchissement, magasin serveur, détection de réemploi, sessions actives. `IEmailSender<TUser>` n'a « aucune implémentation utilisable en production » |
| **D15 — serveur administré**           | **+1 à 2 semaines** de mise en place, **plus un impôt récurrent** | D15 le dit : « un VPS est un serveur que l'on administre : certificats, mises à jour, supervision, sauvegardes. Ce travail n'existait pas avec une plateforme managée. » L'impôt récurrent ne se rattrape pas, il se paie chaque mois                                                                                                                                                                                                                                                               |
| **D9, D11, D12, D14 — backend séparé** | surcoût **étalé**, non chiffré ici                                | chaque accès aux données passe désormais par un cas d'usage, un handler et une couche d'infrastructure au lieu d'un appel client. Le surcoût ne tombe pas sur une étape : il se répartit sur les étapes **2, 3, 3 bis, 4, 5 et 7**                                                                                                                                                                                                                                                                  |

**Ce qui rend cette estimation vérifiable, et c'est nouveau.** D43 ajoute en fin de document un tableau des **durées constatées** lot par lot. La prochaine révision de cette fourchette se fera sur des durées mesurées, pas sur des estimations empilées — et c'est aussi l'instrument sans lequel les conditions de réouverture de D9 (« un retard de livraison imputable au coût du backend ») et de D17 (« un retard imputable à cette réécriture ») ne peuvent pas se déclencher.

---

## Étape 0 — Avant tout code

Ces trois points conditionnent la partie nutrition. L'entraînement peut avancer en parallèle.

0. **Lire `16-projet.md`** — arborescence, conventions, variables d'environnement, glossaire. C'est le document qui évite à Claude Code d'inventer ses propres conventions.
1. **Avocat en droit de la santé numérique.** 300 à 500 €. Valider le positionnement, les CGU, la politique de confidentialité
2. **Diététicien agréé partenaire.** Accord écrit : périmètre de validation, responsabilité, rémunération ou participation
3. **Statut.** Indépendant complémentaire pour développer (200-300 €), **SRL avant l'ouverture payante** (1 500-2 500 €). Voir `13-juridique.md`
4. **Assurance responsabilité civile professionnelle** avec extension cyber, avant l'ouverture. 400-900 €/an
5. **Nom et dépôt de marque.** Recherche d'antériorité BOIP et EUIPO, puis dépôt Benelux en classes 9, 41 et 42 — 352 €. Voir `15-marque.md`
6. **Kinésithérapeute relecteur** pour les programmes adaptés aux contraintes. Distinct du diététicien
7. **Vérification des régions et DPA** de chaque fournisseur de modèle, et conception de la minimisation du contexte. Voir `13-juridique.md`

**Aucun de ces sept points n'est affecté par D9 à D43, et les montants sont exacts.** Le point 7 en sort même **renforcé** : `13-juridique.md` § 2 dit désormais que « le choix d'OVHcloud protège la base, jamais les transferts vers les modèles ».

**Quatre points ajoutés le 20/08/2026, créés par D15 et D17. Aucun n'existait, et aucun ne peut attendre l'étape où il servira, à cause de son délai.**

8. **DPA avec OVHcloud.** `13-juridique.md` § 4 pose la question sans la trancher : qui le signe, et sous quelle entité ? Ce point est **couplé au point 3** — la signature dépend du statut, donc leur ordre n'est pas libre
9. **Quel produit OVHcloud : VPS ou instance Public Cloud ?** Bloquant pour l'AIPD, parce que « le partage des responsabilités n'y est pas identique ». Rien n'oblige à provisionner quoi que ce soit pour trancher ce point, mais il doit être tranché
10. **La certification HDS est-elle pertinente ?** Question pour l'avocat, à joindre au point 1
11. **Fournisseur d'email transactionnel européen avec DPA** (`14-contenu.md` § 4). Ce n'était pas un point d'étape 0 quand un fournisseur envoyait les mails ; c'est devenu un **prérequis contractuel de l'authentification**, puisque `IEmailSender<TUser>` n'a aucune implémentation utilisable en production et que la vérification d'email de `09-comptes.md` § 1 n'a sinon aucun moyen d'envoi

**Si le point 2 échoue, le projet devient un journal d'entraînement.**

Le point 7 est structurant : il conditionne l'architecture de la couche IA et doit être tranché avant d'écrire le premier appel au modèle. Autant le savoir en semaine 1 qu'en mois 6.

---

## Étape 1a — Harnais (livré)

**Le harnais avant le produit.** Livré par le **lot 1**, le 20/08/2026.

- TypeScript strict, **Oxlint 1.79 et `oxlint-tsgolint`** — et non ESLint : `typescript-eslint` déclare une contrainte de pair `typescript <6.1.0`, le dépôt est sur TypeScript 7, la chaîne est mécaniquement inutilisable (D18)
- Prettier, Vitest, Playwright, axe-core, hooks pre-commit et pre-push, CI GitHub Actions. Voir `08-workflow.md`
- Ce que la ligne d'origine ne disait pas, et qui est en place : `scripts/regles-projet.mjs` (les règles qu'aucun linter ne connaît), le contrôle de licences sur npm et NuGet (D13), gitleaks installé hors npm (D23), semgrep, les actions GitHub épinglées par empreinte (D28), le gardien de `main` qui remplace la protection de branche absente (D29), et le **point d'entrée unique `npm run verify`** (D25)

**Ce que le lot 1 n'a pas livré, et qu'il a daté :** les états d'interface et le parcours au clavier ne sont pas gardables sans écran, et `i18next` n'est pas installé (D31). Les trois livrables arrivent avec le premier écran, au lot 2.

---

## Étape 1b — Socle

**L'étape 1 d'origine n'était pas un lot : c'était sept chantiers sous un même titre.** Confondre le harnais et le socle fait croire l'étape close alors que `front/src/` ne contient qu'`App.tsx`, `main.tsx` et un `core/index.ts` dont le contenu entier est `export {}`, et que `back/Palier.Api/Program.cs` n'expose aucune route. Le découpage en lots est en fin de document.

- **Base de données : PostgreSQL managé chez OVHcloud** (versions 14 à 18 offertes, vérifié le 20/08/2026) et backend ASP.NET Core conteneurisé sur **VPS ou instance Public Cloud** — D9, D15, D17. Lequel des deux **n'est pas tranché**, et l'AIPD a besoin de le savoir (point 9 de l'étape 0). **Rien n'est provisionné au lot 2 et rien ne doit l'être** : le substitut de développement est une base locale en conteneur, PostgreSQL 18.6 (D33, D34)
- **Schéma, RLS dès la création, migrations versionnées.** Valable ; le mécanisme change. Migrations EF Core dans `back/Palier.Infrastructure/Migrations/` (D14, D32) ; vues, politiques et contraintes `CHECK` par `migrationBuilder.Sql(...)`. **RLS devient une défense en profondeur** : l'autorisation se décide dans `Palier.Application`, cas d'usage par cas d'usage (D9), et les tests de politiques comptent double, puisque **plus aucun test fonctionnel ne franchira RLS**
  - **Critère de sortie, et non note de bas de page** — le mode de défaillance que `03-donnees.md` nomme : « une migration générée sans ces trois blocs produit un schéma qui compile, qui démarre, et qui n'applique ni les vues, ni RLS, ni les bornes. Rien ne le signale au démarrage. »
  - **Trois tables absentes du schéma** sont à ajouter : le journal versionné des libellés, le journal des appels au modèle, et les tables d'identité ASP.NET
  - **« Schéma complet » est remplacé par la tranche de six tables de D39** — une représentante de chacune des cinq formes du schéma, les treize autres arrivant avec le cas d'usage qui les exige. **Cet écart est un arbitrage soumis au porteur du projet** : sans sa validation, D39 n'est pas prise et cette ligne reprend sa forme d'origine
  - Les deux points laissés ouverts par `03-donnees.md` sont désormais **fermés** : le type de la clé d'`AspNetUsers` par D35, le mécanisme d'identité par D36
- **Auth : Google OAuth et email/mot de passe**, vérification d'email, limitation de débit, 2FA optionnelle. **Aucune de ces exigences ne baisse** — mais ce n'est plus une puce, c'est un lot entier (**lot 4**), et le relevé de `09-comptes.md` § 1 le chiffre exigence par exigence. Deux points qui n'apparaissaient pas : la règle du produit n'est pas « pas de connexion sans email vérifié » mais « **pas de nutrition** sans email vérifié », qui n'a aucun équivalent dans Identity ; et le QR code TOTP n'est pas fourni. Voir aussi la spec d'architecture § 16
- **Squelette PWA, jetons de design de `02-design.md`, i18n français/anglais.** Valable mot pour mot. `i18next` n'est pas installé et **arrive avec le premier écran**, accompagné des deux garde-fous que D31 a reportés — une épreuve qui refuse un écran dépourvu d'état vide ou d'état d'erreur, et un parcours Playwright au clavier seul. **Tailwind n'est pas installé au lot 2** ; les jetons vivent dans `front/src/ui/jetons.ts` (D42, arbitrage soumis au porteur). Seul l'hébergement des fichiers construits change : Caddy au lieu de Vercel
- **Couche de résilience : Dexie, file de retry persistée, indicateur de synchronisation.** Valable mot pour mot, **déplacé au lot 7**. `11-qualite.md` § 1 reste intégralement valide. Deux changements : la file rejoue des appels **à l'API**, plus à Supabase ; et les identifiants sont générés côté client en UUID v7 pour que la reprise soit idempotente (spec d'architecture § 8). Elle dépend donc de l'API : impossible avant elle
- **Déploiement, et plafond de dépense.** La ligne d'origine cachait deux choses distinctes, qu'il faut séparer :
  - _Le déploiement_ — image conteneur sur un serveur administré, pipeline à écrire, et un retour arrière qui **n'est plus une fonction de la plateforme**. `14-contenu.md` § 5 : « redéployer l'image précédente. Le mécanisme, et le délai qu'il permet de tenir, restent à décider et à éprouver — pas à supposer. » **Lot 9**
  - _Le plafond de dépense_ — **le garde-fou survit, sa cible change.** Un VPS est à prix fixe ; le risque de facture se déplace sur les appels aux modèles. `LLM_MONTHLY_BUDGET_EUR` figure déjà au modèle de `back/.env.example` de `16-projet.md` § 3, et `14-contenu.md` § 6 porte la ligne d'alerte « Coût des modèles ». **Ne pas jeter l'intention en supprimant Vercel**
  - La clause de portabilité Cloudflare de `CLAUDE.md` § 3 tombe aussi — Workers n'exécute pas .NET. Elle est remplacée par une **portabilité par conteneur**, qui la sert mieux
- **Abstraction `LLMProvider`.** Valable, déplacée au **lot 8**. Devient `ILlmProvider` en C# dans `Palier.Infrastructure` : l'interface TypeScript de `06-ia.md` § 2 est un résidu de l'ancienne pile. Journal des appels : horodatage, fournisseur, modèle, tâche, jetons — **jamais le contenu** (`13-juridique.md` § 2)

**Livrable de l'étape 1b — réécrit.** « Inscription par les deux voies » et « un appel modèle de test sur chaque fournisseur » appartiennent désormais aux lots 4 et 8. Ce que livre le **lot 2** : un environnement de base reproductible, un schéma migré avec un RLS qui **mord**, l'isolation entre deux utilisateurs prouvée sur un vrai moteur, une route `GET /api/v1/sante` dont l'assertion de démarrage **refuse de servir** si RLS ne mord pas, et un écran à quatre états dont l'erreur se **provoque** en arrêtant le conteneur. CI verte, audit d'accessibilité au vert.

---

## Étape 1 bis — Contenu

En parallèle du développement, car c'est le poste le plus long et le plus sous-estimé :

- Catalogue de 250 à 400 exercices avec contre-indications
- Schémas vectoriels des mouvements — commencer par les 60 exercices les plus utilisés
- Programmes modèles, relus par le kinésithérapeute
- Pages éducatives
- Emails transactionnels, français et anglais

Compter 6 à 10 semaines de travail, étalées. Voir `14-contenu.md`.

**Zéro dépendance à la pile abandonnée, et cette étape peut démarrer aujourd'hui.**

---

## Étape 2 — Onboarding et entraînement

- Catalogue d'exercices avec contre-indications
- Programmes modèles par contrainte
- **Écran de séance**, selon la structure imposée de `02-design.md`
- Suggestion de progression, détection de plateau
- Ressenti par exercice
- Volume hebdomadaire, ratio tirage/poussée
- Courbes
- Onboarding six écrans, avec l'écran contraintes
- Structures full body, upper/lower, PPL et split selon la fréquence
- Répétition de séance, copie des charges, correction rétroactive, jour de repos
- Export JSON, CSV et PDF ; import Hevy et Strong
- Quatre états d'interface sur chaque écran

Livrable : le fondateur utilise l'application pour son propre bloc, pendant au moins trois semaines, sans autre outil.

**Ne pas passer à l'étape 3 avant que cette condition soit remplie.** Un produit qu'on ne veut pas utiliser soi-même ne se vend pas.

**Ces trois semaines sont aussi une fenêtre calendaire qu'aucune architecture et aucun agent ne raccourcissent, et elles comptent dans la durée du projet.**

---

## Étape 3 — Nutrition, sans assistant

- Import CIQUAL et EFSA
- Intégration OpenFoodFacts, scan de code-barres
- Saisie manuelle et recherche
- Objectifs pré-remplis, modifiables, avec planchers de sécurité
- Agrégation alimentation + compléments
- **Composant « règle d'écart »**, l'élément signature
- Alerte sur dépassement des limites hautes
- Garde-fous TCA complets et testés

Livrable : validation écrite du diététicien sur les règles et les libellés.

---

## Étape 3 bis — Hydratation et confort nutrition

**Hydratation** : cible dynamique, saisie en un appui, contenants personnalisés, règle graduée. Peu de travail, fort effet sur l'usage quotidien — c'est le geste le plus fréquent du produit.

**Confort nutrition** :

Duplication de journée, repas enregistrés, recettes, suggestions contextuelles, prises de compléments récurrentes, favoris, recherche globale.

---

## Étape 4 — Vision

- Photo d'étiquette de complément, extraction des doses
- Photo de repas, avec dialogue de quantification
- Comparaison qualité/coût entre les deux fournisseurs sur cas réels

**Point ajouté le 20/08/2026, et décidé nulle part : le stockage des photos.** Supabase Storage était implicite ; il n'y a plus rien à sa place, et **aucune décision D1 à D43 n'en parle, aucun document non plus**. Trois voies, dont le coût de conformité diffère : le disque du serveur administré, l'Object Storage d'OVHcloud, ou **ne rien stocker et transmettre la photo directement au modèle** — la plus économe en conformité. `12-confort.md` impose déjà « type MIME vérifié, taille limitée, métadonnées EXIF supprimées » (les photos portent des coordonnées GPS). Le choix a un effet sur l'AIPD, pas seulement sur le code.

---

## Étape 5 — Progression

Radar à sept axes, six états dérivés, auto-tests de mobilité, table `progression_snapshots` avec les valeurs sources pour audit. Avatar 3D **non inclus** — reporté après l'ouverture.

---

## Étape 6 — Assistant

- Contexte mis en cache
- Routage par tâche
- Filtre de sortie, testé
- Détections prioritaires
- Mention IA

---

## Étape 7 — Compte, abonnement, administration

- Stripe Checkout et Customer Portal, webhooks idempotents, TVA
- Plan gratuit, échec de paiement, résiliation, suppression de compte
- Notifications, toutes désactivées par défaut sauf alertes de sécurité
- **Interface d'administration pour le diététicien**, journal des libellés versionné
- Support : signalements, centre d'aide
- Mesure : Plausible ou Umami auto-hébergé, tableau de bord de conformité. Le choix UE reste ; **« auto-hébergé » devient un service de plus sur le serveur qu'on administre** — un coût d'exploitation permanent, pas un coût de développement
- Documents légaux : CGU, confidentialité, registre des traitements, AIPD. **Valable, alourdi** : `13-juridique.md` § 4 nomme trois ajouts non cosmétiques — l'authentification devient une **mesure technique de l'AIPD** (D17), l'exploitation du serveur devient une mesure au sens de l'article 32 (D15), et la détection d'incident repose désormais sur votre supervision. À inscrire également comme mesures : le mécanisme d'isolation (D36), l'assertion de démarrage (D37) et l'interdiction de données réelles en local (D40)
- **Vérification d'âge** : 16 ans minimum, nutrition verrouillée sous 18 ans
- Environnements recette et production séparés, supervision, sauvegardes testées. **C'est la ligne dont le coût a le plus changé sans qu'elle change elle-même.** Elle vaut désormais : deux environnements à provisionner **et administrer** ; les onze lignes de supervision de `14-contenu.md` §§ 6-7, dont **cinq sont écrites « à décider »** (serveur, TLS, correctifs, sauvegardes, authentification) ; restauration testée **trimestriellement** ; RPO 24 h et RTO 4 h ; procédure de reprise exécutable par un tiers ; document scellé de continuité. **Et la conséquence de D37 :** sous `FORCE ROW LEVEL SECURITY`, `pg_dump` échoue pour un rôle qui ne contourne pas RLS et `COPY FROM` est refusé à la restauration — la sauvegarde n'est plus une commande, c'est un chemin qui se conçoit et s'éprouve
- Tests de charge. **Le versionnement de l'API descend au lot 2** : `/api/v1/` dès la première route coûte zéro, alors que `14-contenu.md` § 8 décrit le rattrapage après coup comme « un problème insoluble ». La première route du projet est `GET /api/v1/sante`
- Procédures de modération et ressources d'urgence validées par un professionnel

---

## Étape 8 — Bêta fermée

Vingt utilisateurs recrutés dans le réseau existant : salles de sport, kinésithérapeutes, coachs de la région liégeoise. Gratuit, en échange de retours.

Mesure unique : **combien ont enregistré vingt séances ou trente journées alimentaires après huit semaines ?**

En dessous de la moitié, ne pas ouvrir le paiement — corriger d'abord.

**Ces huit semaines sont une fenêtre calendaire irréductible, à compter dans la durée du projet.**

---

## Étape 9 — Ouverture

- Stripe, 20 €/mois
- Français uniquement au lancement
- Anglais et autres marchés seulement une fois la rétention prouvée

**Le marché mondial dès le premier jour multiplie le risque juridique sans revenu en face.** Chaque pays a ses propres règles sur le conseil nutritionnel, et les États-Unis en ont une par État.

---

## Étapes et lots — correspondance et durées constatées

**D43.** Ce tableau vit dans **ce fichier et nulle part ailleurs** : deux documents portant l'ordre de construction divergeraient, et aucune épreuve de franchissement ne peut vérifier que deux textes en prose disent la même chose (D25).

Les **étapes** sont des jalons produit, avec leurs critères de sortie. Les **lots** sont des unités d'exécution. **Elles divergent déjà** — l'étape 1 exigeait le harnais _et_ le socle, le lot 1 n'a livré que le harnais.

| Étape      | Lot                                                            | Début      | Fin        | Durée constatée |
| ---------- | -------------------------------------------------------------- | ---------- | ---------- | --------------- |
| 1a         | **Lot 1 — harnais** (livré)                                    | 19/08/2026 | 20/08/2026 | 2 jours         |
| 1b         | **Lot 2 — socle de données** + les 3 livrables D31 (livré)     | 20/08/2026 | 20/08/2026 | 1 jour          |
| 1b         | **Lot 3 — domaine** (livré)                                    | 21/08/2026 | 21/08/2026 | 1 jour          |
| 1b         | **Lot 4 — socle de session** (livré)                           | 21/08/2026 | 21/08/2026 | 1 jour          |
| 1b         | **Lot 4b — fin du socle de session** (livré)                   | 23/08/2026 | 23/08/2026 | 1 nuit          |
| 1b · 2 · 3 | **Lot 5 — API** (le reste du schéma y arrive, table par table) | —          | —          | —               |
| 1b · 2     | **Lot 6 — socle d'écran complet et PWA**                       | —          | —          | —               |
| 1b         | **Lot 7 — résilience front** (Dexie, file de retry)            | —          | —          | —               |
| 6          | **Lot 8 — couche modèle**                                      | —          | —          | —               |
| 7          | **Lot 9 — déploiement**                                        | —          | —          | —               |

**À remplir à chaque fin de lot, depuis le rapport de lot.** Un tableau ne se remplit pas tout seul : c'est un instrument, pas un verrou. Sans lui, les conditions de réouverture de D9 et D17 n'ont rien qui puisse les déclencher.

Les lignes sont remplies **par mesure et non par souvenir**. `git log` compte, au 23/08/2026 :

| Jour       | Commits | Ce qui y a été livré                              |
| ---------- | ------: | ------------------------------------------------- |
| 19/08/2026 |      22 | lot 1, première moitié                            |
| 20/08/2026 |      99 | fin du lot 1, lot 2                               |
| 21/08/2026 |      45 | lot 3, lot 4                                      |
| 22/08/2026 |      21 | audit de sécurité, coffre des secrets (D59)       |
| 23/08/2026 |       7 | lot 4b — Google, courriel, fusion, administration |

La colonne « durée constatée » ne reçoit jamais une estimation. **Le lot 4b porte « 1 nuit » et non « 1 jour »** : il a été exécuté en autonomie entre le 22/08 au soir et le 23/08 au matin, et arrondir à la journée effacerait précisément ce que ce tableau existe pour mesurer.

**Deux écarts à la spec d'architecture § 14, à valider par le porteur du projet :**

1. son lot 4 (données) **remonte en lot 2** et son lot 2 (socle d'écran) **descend en lot 6**, parce que les quatre états d'un écran ne peuvent pas être honnêtes sans donnée réelle — un état d'erreur qu'on simule garde un simulacre (D41) ;
2. le schéma **s'étale** au lieu d'être posé d'un bloc (D39).

---

## Ce qui n'est pas au programme

Application Android native, fonctions communautaires, objets connectés, notation des produits, plans de repas générés, marque blanche.

Chacun de ces points peut devenir pertinent. Aucun ne l'est avant cent abonnés payants.

**L'avatar 3D en fait partie.** Le radar délivre l'essentiel de la satisfaction pour une fraction du travail. Construire l'avatar avant d'avoir des utilisateurs, c'est deux mois investis sans aucun retour.
