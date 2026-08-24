# Journal des décisions

**Ce fichier se lit au démarrage de chaque session, pas à la fin.**

Une décision qu'on ne relit pas au démarrage se reprend : le débat recommence trois semaines plus tard sans que personne se souvienne pourquoi il avait été clos. Chaque entrée porte **ce qui la rouvrirait** — c'est la seule information qui permet de savoir quand y revenir légitimement.

---

## D1 — Nom du projet : `palier`

**Tranché le :** 19/08/2026
**Motif :** recommandation de `15-marque.md` § 3 — cohérent avec le système de progression, positif, prononçable.
**Ce qui la rouvrirait :** une antériorité trouvée aux registres BOIP ou EUIPO, ou une priorité donnée à l'international (le document note que le nom est « très francophone »).
**Reste à faire :** la recherche d'antériorité, que `15-marque.md` § 4 demande _avant_ de s'attacher au nom.

## D2 — Gestionnaire de paquets front : npm

**Tranché le :** 19/08/2026
**Motif :** cohérent avec `DEMARRAGE.md`, déjà autorisé dans les permissions, détecté nativement par les hébergeurs.
**Ce qui la rouvrirait :** une durée d'installation durablement pénalisante en CI.

## D3 — Node 24

**Tranché le :** 19/08/2026
**Motif :** version installée localement (24.16.0). Vérifié à la source : la documentation Vercel du 27/02/2026 donne 24.x comme version par défaut. Verrouillé par `.nvmrc` et `engines.node`.
**Ce qui la rouvrirait :** un hébergeur retenu qui ne la supporterait pas — à revérifier lors du choix OVH.

## D4 — Référentiel de sécurité : OWASP ASVS niveau 2 + Top 10 CI/CD

**Tranché le :** 19/08/2026
**Motif :** ASVS L2 est le niveau prévu pour une application traitant des données sensibles au sens de l'article 9 du RGPD. Le Top 10 CI/CD couvre le pipeline, que le Top 10 applicatif ignore.
**Ce qui la rouvrirait :** rien de prévisible. Le niveau 3 serait disproportionné.

## D5 — Suite de franchissement permanente

**Tranché le :** 19/08/2026
**Motif :** un garde-fou non éprouvé ment. Chaque contrôle est vérifié en provoquant la violation qu'il doit refuser, et ces épreuves restent dans la suite pour détecter une dégradation ultérieure.
**Ce qui la rouvrirait :** un coût d'entretien qui dépasserait le bénéfice — improbable, les épreuves sont figées une fois écrites.

## D6 — Sévérité : structurel bloquant, jugement en avertissement

**Tranché le :** 19/08/2026
**Motif :** les règles qui protègent l'architecture et la conformité bloquent ; celles qui relèvent du goût informent et se traitent en revue. Un seuil trop serré sur la duplication produit de l'abstraction prématurée.
**Ce qui la rouvrirait :** un relâchement constaté de la qualité sur les points laissés en avertissement.

## D7 — Direction visuelle : instrumentation

**Tranché le :** 19/08/2026
**Motif :** `02-design.md` la décrivait sans la nommer. Croisement d'industriel, de monospace technique et de discipline de grille. Référence de méthode : Teenage Engineering — la contrainte comme esthétique, le monospace comme signe d'honnêteté technique.
**Ce qui la rouvrirait :** un retour utilisateur massif sur l'austérité perçue.

## D8 — Échelle de mouvement : trois durées, une courbe

**Tranché le :** 19/08/2026
**Motif :** valeurs mesurées sur Linear, Stripe et Vercel, recoupées avec les jetons Material 3. Constante observée : une courbe domine, en sortie douce, avec deux ou trois durées. Retenu : 100 ms (retour au doigt), 150 ms (validation), 400 ms (le repère de la règle), courbe `cubic-bezier(0.25, 0.46, 0.45, 0.94)`.
**Ce qui la rouvrirait :** rien avant qu'un écran réel soit jugé trop lent ou trop sec à l'usage.

---

# Décisions d'architecture — 19/08/2026

Ces décisions **remplacent** la pile décrite dans `CLAUDE.md` § 3. Les documents concernés doivent être repris (voir la spec d'architecture).

## D9 — Backend séparé, plutôt que Supabase en accès direct

**Tranché le :** 19/08/2026, par le porteur du projet.
**Motif retenu :** point de contrôle unique entre le client et les données, frontières explicites entre les couches.
**Avis donné avant la décision, conservé pour mémoire :** l'accès direct via RLS concentre la sécurité dans le moteur PostgreSQL, où elle ne peut pas être contournée par oubli, et coûte beaucoup moins cher à une personne seule. Cet avis a été exposé, puis la décision inverse a été prise en connaissance de cause.
**Ce qui la rouvrirait :** un retard de livraison imputable au coût du backend, ou la constatation que l'authentification maison consomme plus de temps que le produit.

## D10 — Langage backend : C# / .NET

**Tranché le :** 19/08/2026, après hésitation explicite entre C# et Java.
**Motif :** quatre critères penchent du même côté, aucun ne penche vers Java dans ce contexte.

1. .NET SDK 10.0.303 est installé localement ; aucun JDK ne l'est.
2. C'est le langage compilé que le porteur du projet pratique.
3. L'écosystème web .NET est concentré — ASP.NET Core et EF Core — là où Java est fragmenté entre Spring Boot, Quarkus, Micronaut et plusieurs couches d'accès aux données. Moins de variantes, moins d'erreurs.
4. C# et TypeScript partagent leur concepteur, Anders Hejlsberg. Sur un projet où une seule personne alterne front et back toute la journée, le coût de changement de contexte compte.

**Ce qui ne l'a pas motivée, et ne doit pas la rouvrir :** la performance et le coût d'hébergement sont équivalents. Le goulot d'étranglement de cette application est PostgreSQL et le réseau, jamais le langage.
**Ce qui la rouvrirait :** l'arrivée d'une équipe déjà constituée sur une autre pile.

## D11 — Architecture : Clean Architecture, quatre projets

**Tranché le :** 19/08/2026, par le porteur du projet.
**Motif :** frontières explicites entre domaine, cas d'usage, infrastructure et exposition.
**Avis donné avant la décision, conservé :** une variante à trois projets avait été recommandée par YAGNI, l'arithmétique nutritionnelle ne justifiant pas une couche d'indirection par requête.
**Bénéfice décisif conservé dans les deux cas :** `Palier.Domain` ne référence aucun autre projet. Un calcul nutritionnel ne _peut pas_ atteindre la base ou le réseau — l'impossibilité est vérifiée par le compilateur, non par une règle de style. `01-conformite.md` § 3 en dépend directement.
**Ce qui la rouvrirait :** une lourdeur constatée sur les premiers cas d'usage.

## D12 — CQRS sans MediatR

**Tranché le :** 19/08/2026.
**Motif :** MediatR est passé sous **Reciprocal Public License 1.5** depuis juillet 2025 — vérifié sur la page de licence NuGet de la version 14.2.0. Cette licence ferme la « faille SaaS » : contrairement à la GPL, l'usage en service en ligne déclenche l'obligation de publier le code source. `palier` est un service commercial propriétaire à 20 €/mois. L'alternative est une licence commerciale payante.
**Retenu :** `Mediator.SourceGenerator` (martinothamar), licence **MIT** vérifiée, qui génère la répartition à la compilation au lieu de la résoudre par réflexion — les piles d'appel restent lisibles au débogueur.
**Repli si nécessaire :** des handlers écrits à la main, une cinquantaine de lignes, zéro dépendance.
**Ce qui la rouvrirait :** un changement de licence de `Mediator.SourceGenerator`, que le contrôle de licences en CI détectera.

## D13 — Contrôle des licences de dépendances en intégration continue

**Tranché le :** 19/08/2026.
**Motif :** l'affaire MediatR a été trouvée par méfiance envers un paquet précis. La suivante passera si personne ne regarde. Une liste blanche et un échec du build si une dépendance en sort, sur les deux écosystèmes.

**Liste blanche, précisée le 20/08/2026 à l'implémentation :** MIT, Apache-2.0, BSD-2-Clause, BSD-3-Clause, ISC, PostgreSQL, 0BSD, Unlicense, CC0-1.0, MIT-0. Cette décision en énonçait cinq ; les cinq ajoutées sont toutes équivalentes ou **plus permissives** que MIT — domaine public ou quasi. Aucune n'introduit d'obligation. L'écart a été relevé en revue et tranché ici plutôt que laissé entre le journal et le code.

**Le mécanisme d'exception, ajouté le 20/08/2026.** Une licence hors liste n'est pas automatiquement interdite : elle est **lue**, puis inscrite nominativement dans `EXCEPTIONS` avec son motif et ce qui la rouvrirait. Jamais ajoutée à la liste blanche, qui ne porte que des expressions permissives. Première exception : `@axe-core/playwright` en MPL-2.0 — copyleft par fichier, sans clause réseau, et `CLAUDE.md` § 3 impose axe-core nommément.

**Ce que le contrôle ne voit pas, à ce jour :** les dépendances **transitives** (`lightningcss`, MPL-2.0, arrive par Vite 8 et lui échappe). Une comparaison par jetons SPDX a remplacé la comparaison par sous-chaîne, qui acceptait `MITNFA` parce qu'il contient `MIT`.

**~~À traiter au lot 2~~ — amendé le 20/08/2026, en conception du lot 2.** `Mediator.SourceGenerator`, retenu par D12, ne publie **pas** d'expression SPDX — le contrôle le refuse aujourd'hui, pour cette raison et non pour sa licence, qui est MIT. Il devra entrer dans `EXCEPTIONS` le jour où CQRS sera implémenté, sans quoi `verify` cassera. **Or le lot 2 n'installe pas CQRS** : une route de santé ne justifie pas un générateur de source, et D12 prévoit explicitement le repli des « handlers écrits à la main, une cinquantaine de lignes, zéro dépendance ». L'exception **n'est donc pas due au lot 2**, et l'inscrire d'avance créerait une exception sans cible — précisément ce que D24 refuse. Elle est **redatée au lot qui installe réellement le paquet** : au découpage proposé par D43, le lot des cas d'usage, pas avant.

**Ce qui rend ce report inoffensif, et ce qui l'autorise.** L'échéance n'a pas besoin d'être surveillée, parce qu'elle **se signale d'elle-même** : le premier `dotnet restore` qui ramène `Mediator.SourceGenerator` fait échouer l'étape `licences`, donc `verify`, donc `git push`. C'est un report **détectable**, pas un report en silence — et c'est la seule raison pour laquelle il est légitime de le déplacer. Une échéance qu'aucun instrument ne peut déclencher se laisse expirer sans bruit ; c'est le défaut que D30 a mesuré et que D43 corrige pour le calendrier.

**Ce qui la rouvrirait :** rien. Le coût est nul, le risque évité est juridique.

## D14 — Accès aux données : EF Core

**Tranché le :** 19/08/2026, par le porteur du projet.
**Motif :** productivité, typage de bout en bout, migrations gérées.
**Conséquence à ne pas perdre de vue :** EF Core ne pilote ni les vues, ni les politiques RLS, ni les contraintes `CHECK`. Les trois vues centrales de `03-donnees.md` — dont `daily_intake`, décrite comme « la vue centrale du produit » — passent par `migrationBuilder.Sql(...)`. Le SQL du dossier ne disparaît pas : il migre dans les migrations.
**Ce qui la rouvrirait :** des requêtes d'agrégation dont EF produirait un SQL inacceptable — traitées au cas par cas par Dapper plutôt qu'en changeant d'approche.

## D15 — Hébergement : OVHcloud

**Tranché le :** 19/08/2026, par le porteur du projet.
**Motif :** fournisseur européen, donc hors portée du Cloud Act américain. Pour des données de santé au sens de l'article 9 et une clientèle belge, c'est l'argument de conformité le plus solide.
**Ce que cela implique :** OVH propose PostgreSQL managé, mais **aucune plateforme .NET clé en main**. Le backend est conteneurisé et déployé sur un VPS ou une instance Public Cloud. Un VPS est un serveur que l'on administre : certificats, mises à jour, supervision, sauvegardes. Ce travail n'existait pas avec une plateforme managée.
**Ce qui la rouvrirait :** une charge d'exploitation qui empiéterait sur le développement.

## D16 — Front et API sous le même domaine

**Tranché le :** 19/08/2026.
**Motif :** le cookie de rafraîchissement `httpOnly` qu'exige `09-comptes.md` § 1 reste un cookie de même site — pas de `SameSite=None`, pas de pré-requête CORS, surface CSRF minimale. Un sous-traitant de moins au registre des traitements.
**Ce qui la rouvrirait :** un besoin de diffusion mondiale du front qui justifierait un réseau de diffusion séparé.

## D17 — Authentification réécrite avec ASP.NET Identity

**Tranché le :** 19/08/2026, par le porteur du projet.
**Motif :** indépendance complète vis-à-vis d'un fournisseur.
**Conséquence assumée :** les sept exigences de `09-comptes.md` § 1 sont à implémenter — Google OAuth, vérification d'email obligatoire avant l'accès nutrition, contrôle du mot de passe contre HaveIBeenPwned, limitation à 5 tentatives par IP et par compte sur 15 minutes avec verrouillage progressif, 2FA TOTP, rotation des jetons de rafraîchissement, fusion des comptes email et Google. ASP.NET Identity en couvre une partie, pas tout.
**Ce que cela déplace :** la sécurité de l'authentification devient un traitement que vous opérez, non un service délégué. `13-juridique.md` doit en tenir compte dans l'AIPD et le registre.
**Ce qui la rouvrirait :** un retard imputable à cette réécriture.

---

# Décisions d'exécution du lot 1 — 20/08/2026

Ces décisions n'ont pas été prises en conception : elles ont été **mesurées** pendant la construction du harnais, et chacune a corrigé un défaut réel.

## D18 — Linter front : Oxlint, et non ESLint

**Tranché le :** 20/08/2026, à l'implémentation.
**Motif :** `typescript-eslint` déclare une contrainte de pair `typescript <6.1.0`. Le dépôt est sur TypeScript 7. La chaîne ESLint + `typescript-eslint` est donc mécaniquement inutilisable, pas seulement déconseillée. Oxlint 1.79 couvre les cinq règles bloquantes du plan sous leurs noms propres (`typescript/no-explicit-any`, `eslint/no-unused-vars`, `unicorn/filename-case`, `import/no-cycle`, `eslint/no-console`), et `oxlint-tsgolint` fournit les règles à information de types (`no-floating-promises`, `no-misused-promises`, `await-thenable`) sous la commande distincte `lint:types`.
**Ce que cela coûte :** l'écosystème de greffons ESLint n'est pas disponible. Les règles maison vivent donc dans `scripts/regles-projet.mjs`, une heuristique textuelle assumée comme telle.
**Ce qui la rouvrirait :** `typescript-eslint` publiant une version compatible TypeScript 7 **et** un besoin de règle qu'Oxlint ne couvre pas. Le second sans le premier ne suffit pas.

## D19 — Deux assertions par épreuve : le code de sortie et le motif

**Tranché le :** 20/08/2026, après mesure (ruling P8).
**Motif :** un code de sortie non nul prouve qu'il s'est passé _quelque chose_, jamais que l'outil a _refusé_. Mesuré trois fois sur ce lot :

- Prettier non installé rend le code 2 avec « No files matching the pattern were found » — une épreuve à assertion unique serait passée au vert sans que Prettier ait rien lu ;
- Playwright sans test collecté rend « No tests found », également en code non nul ;
- `gitleaks` absent du PATH rend le code 1 sous `cmd.exe` avec « n'est pas reconnu en tant que commande interne », **sans** que `lancerOutil` puisse lever : `cmd.exe` absorbe l'erreur de démarrage et rend un code ordinaire.

La réciproque est vraie aussi (ruling P10) : un rouge ne prouve pas la bonne cause. `knip.fixtures.json` rougissait sur onze fichiers — l'épreuve serait passée **sans l'orphelin qu'elle prétend détecter**.
**Ce qui la rouvrirait :** rien. Le coût est d'une ligne par épreuve.

## D20 — L'exclusion appartient à la commande, jamais au fichier de configuration

**Tranché le :** 20/08/2026, après mesure (rulings P6 et P9).
**Motif :** quatre outils du harnais, aucune exception à ce jour, portent le même piège — **un fichier ignoré par la configuration reste ignoré même nommé explicitement en argument** :

| Outil      | Ce qui trompe          | Effet mesuré                                                                                                          |
| ---------- | ---------------------- | --------------------------------------------------------------------------------------------------------------------- |
| Oxlint     | `ignorePatterns`       | fixture invisible même nommée ; `--no-ignore` ne l'annule pas                                                         |
| Prettier   | `.prettierignore`      | `--check` sur une fixture délibérément mal formatée rend **code 0** et « All matched files use Prettier code style! » |
| Playwright | `testIgnore`           | l'argument positionnel filtre la liste **déjà collectée** → « No tests found »                                        |
| gitleaks   | `[[allowlists]] paths` | dossier nommé en argument, fichier sauté : 7 459 octets scannés contre 7 756 sans l'exception                         |

Dans les quatre cas, l'épreuve du harnais serait passée **au vert sans rien contrôler** — le pire mode de défaillance possible pour cette suite.

**La parade, appliquée partout :** l'exclusion est portée par la ligne de commande de ce qui n'en veut pas (`--ignore-pattern`, motif nié `"!…"`), ou par une configuration dédiée à l'épreuve (`knip.fixtures.json`, `playwright.fixtures.config.ts`, `.gitleaks.regles.toml`, `tsconfig.fixtures.json`). Sous `cmd.exe`, ces motifs prennent des **guillemets doubles** : les simples ne délimitent pas et le motif échoue en silence.
**La question à poser à chaque nouvel outil :** _un fichier ignoré reste-t-il ignoré quand on le nomme explicitement ?_ La réponse est oui partout jusqu'ici.
**Ce qui la rouvrirait :** un outil dont l'argument explicite l'emporte réellement sur son fichier d'exclusion — à vérifier, pas à supposer.

## D21 — Aucun commentaire dans un fichier de configuration d'outil

**Tranché le :** 20/08/2026, après mesure (ruling P14).
**Motif :** un `"_note"` posé dans les options de `no-restricted-imports` pour documenter le choix des motifs a fait passer **neuf violations à code 0**, sans erreur ni avertissement. Ce n'est pas « la clé est ignorée » : c'est la règle entière qui cesse de s'appliquer. Même famille que le seuil `Threshold` accepté et ignoré par le collecteur VSTest de coverlet (ruling P11), et qu'une règle mal nommée acceptée par `.oxlintrc.json` sans rien faire.
**Conséquence :** le motif d'un choix vit dans **l'épreuve qui le protège**, jamais dans le fichier de configuration qu'il documente.
**L'exception, et sa condition :** les formats qui portent nativement des commentaires — YAML, `.mjs`, TOML — les acceptent, à condition de l'avoir **vérifié sur cet outil-là**. `.lintstagedrc.mjs` et `back/vitest.config.mjs` sont dans ce cas ; `front/knip.json` porte des commentaires JSONC, tolérés par Knip et vérifiés comme tels.
**Ce qui la rouvrirait :** rien.

## D22 — `lancerOutil` lève au lieu de rendre un code, quand l'outil n'a pas tourné

**Tranché le :** 20/08/2026, après mesure.
**Motif :** la primitive partagée par toutes les épreuves du harnais rendait « refusé » dans **trois** situations où l'outil n'avait jamais tourné : binaire introuvable, délai dépassé, sortie tronquée au-delà du tampon par défaut de Node (1 Mio — `gitleaks`, `knip` et `dotnet format` le dépassent, et `spawnSync` rend alors `status: null` **et** une sortie amputée du motif attendu). Une épreuve serait restée verte en ne contrôlant rien. Le tampon est porté à 64 Mio, le délai à cinq minutes, et les trois cas lèvent une erreur explicite au lieu de rendre un code.
**Ce que cela ne couvre pas, mesuré :** sous Windows, `shell: true` fait passer la commande par `cmd.exe`, qui absorbe l'erreur de démarrage et rend un code de sortie ordinaire. `lancerOutil` ne peut donc pas lever pour un binaire manquant sur cette plateforme — c'est la seconde assertion de D19 qui rattrape ce cas, et elle seule.
**Ce qui la rouvrirait :** rien.

## D23 — gitleaks s'installe hors de npm, et son absence refuse le commit

**Tranché le :** 20/08/2026, à l'implémentation.
**Motif :** le plan prévoyait `npm install -D gitleaks`. Vérifié le 20/08/2026 : le paquet `gitleaks` du registre npm est un **squat vide** — version 1.0.0, dépôt `ycjcl868/gitleaks`, aucun exécutable, un README pour tout contenu. L'installer aurait donné un harnais où `npx gitleaks` ne lance rien. Aucun des paquets voisins du registre n'est publié par le projet officiel. gitleaks est un binaire Go : il s'installe par le gestionnaire du système (`winget install Gitleaks.Gitleaks`, `brew install gitleaks`, ou les publications GitHub), et `DEMARRAGE.md` § 0 le porte en prérequis au même titre que Node et .NET.
**Le hook échoue fermé.** `.husky/pre-commit` teste la présence du binaire et **refuse le commit** s'il manque, avec les trois commandes d'installation. Un contrôle de secrets qui se laisse sauter ne protège personne. Les deux branches ont été franchies le 20/08/2026 : secret indexé → refus ; gitleaks absent → refus avec le message.
**Écart de commande, mesuré :** gitleaks 8.30.1 n'a plus `detect` ni `protect`. Les formes en vigueur sont `gitleaks dir <chemin>` et `gitleaks git --staged`.
**Ce qui la rouvrirait :** une publication npm officielle par le projet gitleaks, ou une action GitHub officielle qui rendrait le binaire local inutile en intégration continue — ce qui ne dispenserait pas du hook local.

## D24 — Trois signalements de Knip écartés, nommément

**Tranché le :** 20/08/2026, mesuré en retirant les exceptions et en comptant.
**Motif :** sans exception, Knip signale trois choses, toutes des faux positifs à ce stade :

| Signalement                                         | Motif de l'écartement                                                                                                                                         | Ce qui le rouvrirait                                                                                                                                                           |
| --------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `src/core/index.ts` — fichier inutilisé             | espace réservé dont le contenu entier est `export {}`. Il n'existe que pour donner une cible à la barrière de pureté d'Oxlint (`overrides` sur `src/core/**`) | **le lot 2** : dès que `core/` portera des modules réellement importés, cette ligne SORT de `entry`, sans quoi tout `core/` échappe définitivement à la détection de code mort |
| `@testing-library/jest-dom` — dépendance inutilisée | aucun test de composant n'existe encore ; `src/` ne contient aucun fichier de test                                                                            | le lot 2, qui livre le premier test de rendu                                                                                                                                   |
| `@testing-library/react` — dépendance inutilisée    | idem                                                                                                                                                          | idem                                                                                                                                                                           |

**Ce que cette décision n'autorise pas :** ajouter une exception sans l'inscrire ici avec sa condition de sortie. Une liste d'exceptions qu'on n'a pas datées devient une liste qu'on n'ose plus toucher.
**Ce qui la rouvrirait :** le lot 2, pour les trois lignes à la fois.

## D25 — Un seul point d'entrée de vérification : `npm run verify`

**Tranché le :** 20/08/2026.
**Motif :** deux plans séparés définissaient chacun leur liste de contrôles. Deux listes divergent — c'est précisément le défaut que le point d'entrée unique existe pour empêcher. `scripts/verify.mjs` porte les quinze étapes des deux écosystèmes, le hook de pré-envoi n'appelle **que** lui, et une épreuve (`tests-harness/verify.test.mjs`) refuse un hook qui énumérerait des contrôles au lieu de déléguer.
**Le corollaire, mesuré :** aucune des 84 épreuves d'alors ne lançait un **script npm** — toutes appelaient `npx <outil>` directement. Elles prouvaient que les outils refusent, jamais que les commandes du projet refusent. Deux régressions sont passées par ce trou. `front/tests/harness/commandes.test.ts` le ferme.
**Ce qui la rouvrirait :** rien sur le principe. La composition de la liste, elle, bouge à chaque lot.

## D26 — La durée de `verify` est mesurée et affichée, le seuil n'est pas relevé

**Tranché le :** 20/08/2026, mesuré.
**État :** `npm run verify` prend **119 s** sur la machine de développement (Windows 11, quinze étapes, toutes vertes), pour un seuil d'avertissement fixé à 90 s. L'avertissement se déclenche donc à chaque exécution.
**Motif de ne pas relever le seuil :** `08-workflow.md` § 5 fait de la lenteur de la boucle un **défaut à traiter**, pas un fait à enregistrer. Relever le seuil à 120 s ferait taire le signal sans rien changer au temps d'attente. Le détail par étape est affiché à chaque exécution, ce qui rend le prochain arbitrage mesurable plutôt qu'argumentaire.
**Le détail, au 20/08/2026 :** `front:test` 39 s · `back:format` 18 s · `back:build` 11 s (`--no-incremental`) · `back:audit` 12 s (réseau) · `back:test` 11 s · `licences` 5 s (réseau) · `front:knip` 5 s · le reste sous 3 s chacun.
**REMESURÉ à la clôture du lot 2 — 20/08/2026, seize étapes, toutes vertes, Docker levé.** Le chiffre a **plus que doublé**, et l'écart doit être lu avant d'être commenté : cinq exécutions successives ont rendu **278,1 s**, **276,8 s**, **338,0 s**, **340,2 s** et **281,7 s**. La dispersion de 60 s n'est pas du bruit — les deux exécutions à 338 et 340 s ont tourné pendant qu'un second agent compilait dans le même worktree, et la cinquième, seule sur la machine, est revenue à 282 s. **La mesure de référence est donc 280 s ± 3 s**, et les 340 s sont un artefact de contention, pas une propriété de la suite.

| Étape              | Lot 1 | Lot 2, dernière mesure | Écart                 |
| ------------------ | ----- | ---------------------- | --------------------- |
| `front:format`     | < 3 s | 3,7 s                  |                       |
| `front:lint`       | < 3 s | 3,0 s                  |                       |
| `front:lint:types` | < 3 s | 3,7 s                  |                       |
| `front:typecheck`  | < 3 s | 4,4 s                  |                       |
| `front:test`       | 39 s  | **58,4 s**             | **+19 s**             |
| `front:knip`       | 5 s   | 5,4 s                  |                       |
| `scripts:lint`     | < 3 s | 2,6 s                  |                       |
| `scripts:format`   | < 3 s | 3,3 s                  |                       |
| `regles`           | < 3 s | 0,4 s                  |                       |
| `back:format`      | 18 s  | 28,3 s                 | +10 s                 |
| `back:build`       | 11 s  | 19,2 s                 | +8 s                  |
| `back:test`        | 11 s  | **27,9 s**             | **+17 s**             |
| `licences`         | 5 s   | 20,9 s (réseau)        | +16 s                 |
| `back:audit`       | 12 s  | 20,5 s (réseau)        | +9 s                  |
| `harnais:back`     | —     | **135,0 s**            | **étape nouvelle**    |
| `front:build`      | < 3 s | 3,4 s                  |                       |
| **TOTAL**          | 119 s | **340,2 s**            | 282 s hors contention |

**Ce que la mesure dit, et qu'aucun des trois leviers de cette entrée n'adresse.** `harnais:back` — 135 s, **40 % du total** — n'existait pas au lot 1 : c'est l'étape ajoutée en fin de lot 1 pour que les épreuves du harnais backend soient enfin lancées par autre chose que la CI. Elle relance des outils entiers (`knip`, `oxlint`, `prettier`, `gitleaks`) en sous-processus, exprès, pour les voir refuser. **Le coût est le prix du garde-fou**, et il ne se paie qu'ici.

`front:test` passe de 39 s à 58 s : les épreuves de rendu du lot 2 s'y ajoutent, et elles relancent elles aussi des outils. `back:test` passe de 11 s à 28 s : un conteneur PostgreSQL démarre, la migration s'applique, et **39 épreuves** tournent sur un moteur réel là où il n'y en avait que 8.

**Aucun levier n'a été appliqué, et le troisième a changé de nature.** Le troisième levier — sortir les épreuves du harnais de `front:test` — était refusé au lot 1 parce que « l'étape deviendrait vide et verte sans rien contrôler ». **`front/src/` porte enfin des tests** : l'argument tombe. Mais **D5 et D25 interdisent de retirer une épreuve de `verify`** — elles peuvent changer d'étape, jamais quitter la suite. Le levier consiste donc à **déplacer**, pas à retrancher, et le gain net serait nul sur le total.

**Ce qui la rouvrirait :** le porteur du projet, à l'arbitrage. Trois leviers sont chiffrés et aucun n'a été appliqué d'office : sortir les épreuves du harnais de `front:test` ; retirer `--no-incremental` de `back:build` (−14 s au lot 1, +8 s de marge aujourd'hui) ; mettre en cache les réponses des registres npm et NuGet du contrôle de licences (`licences` + `back:audit` = **41 s**, dont l'essentiel est du réseau). **Un quatrième levier apparaît avec la mesure et n'appartient à personne d'autre : accepter, ou non, que `git push` exige désormais un moteur de conteneurs.** C'est un changement du contrat de la boucle. Le hook de pré-**commit** n'est pas touché.

## D27 — Lighthouse CI installé de façon éphémère, hors des dépendances déclarées

**Tranché le :** 20/08/2026, après mesure. **À soumettre au porteur du projet** — c'est un arbitrage entre un outil de mesure et un garde-fou de sécurité, et `CLAUDE.md` § 6 demande qu'il soit exposé, pas tranché en silence.
**Le fait mesuré :** `npm install -D @lhci/cli@0.15.1` fait passer `front` de **0 à 10 vulnérabilités, dont 7 de sévérité haute**, toutes transitives : `extract-zip` → `@puppeteer/browsers` → `puppeteer-core` → `lighthouse` → `@lhci/cli`. L'avis sur `extract-zip` porte la plage `*` : **aucune version corrigée n'existe**, donc aucun `overrides` npm ne peut le réparer.
**Ce que cela impliquerait de le déclarer :** le job `securite` lance `npm audit --audit-level=high`. Déclarer `@lhci/cli` le ferait échouer à chaque exécution — ou obligerait à abaisser le seuil à `critical`, ce qui aveuglerait le contrôle sur les dépendances **du produit**. Un garde-fou désarmé pour faire entrer un outil de mesure est un mauvais échange.
**Ce qui est fait à la place :** `npx --yes @lhci/cli@0.15.1 autorun` dans le seul job `performance`, à version figée. Lighthouse CI ne figure ni dans `package.json` ni dans le fichier de verrouillage, ne s'installe sur aucun poste de développement, et n'entre jamais dans le paquet livré. `front/.lighthouserc.json` porte les seuils, et une épreuve (`tests-harness/ci.test.mjs`) refuse qu'ils passent en simple avertissement.
**Ce que cela coûte, et qu'il faut savoir :** cette chaîne vulnérable **s'exécute quand même**, dans un job qui a accès au dépôt en lecture. Le risque n'est pas nul, il est déplacé — et il n'est plus visible de `npm audit`. C'est le point sur lequel le porteur du projet doit se prononcer.
**Ce qui la rouvrirait :** une version d'`extract-zip` corrigée, ou une publication de Lighthouse CI qui ne dépende plus de Puppeteer. À revérifier à chaque mise à jour proposée par Dependabot.

## D28 — Les actions GitHub sont épinglées par empreinte, jamais par étiquette

**Tranché le :** 20/08/2026.
**Motif :** une étiquette Git est mutable. `actions/checkout@v7` repointé par quelqu'un qui a pris le contrôle du dépôt de l'action exécuterait son code dans nos jobs, avec l'accès au dépôt et aux secrets. Une empreinte de commit ne se déplace pas. Empreintes relevées le 20/08/2026 par `gh api repos/<action>/git/ref/tags/<version>` : `actions/checkout` v7.0.1, `actions/setup-node` v7.0.0, `actions/setup-dotnet` v6.0.0. Le plan indiquait `v4` pour les trois ; les versions majeures courantes sont supérieures.
**Le corollaire, sans lequel l'épinglage nuit :** un pointeur figé ne se met plus à jour. `.github/dependabot.yml` suit donc l'écosystème `github-actions` — l'épinglage protège de la substitution, Dependabot de l'obsolescence. L'un sans l'autre laisse une faille ouverte.
**Ce qui le garde :** `tests-harness/ci.test.mjs` refuse toute valeur `uses:` qui ne serait pas un `owner/repo@` suivi de quarante caractères hexadécimaux. Franchi le 20/08/2026 en remettant une étiquette.
**Ce qui la rouvrirait :** rien.

## D29 — Aucune protection de branche : GitHub Free ne l'offre pas sur les dépôts privés

**Tranché le :** 20/08/2026, après échec de la configuration.

**Le fait, mesuré.** Les deux mécanismes de GitHub rendent le même refus, en HTTP 403, avec des droits d'administration pleins :

```
PUT  /repos/warrox1993/palier/branches/main/protection   → « Upgrade to GitHub Pro or make this repository public »
POST /repos/warrox1993/palier/rulesets                   → idem
```

Ce n'est ni un problème de droits ni de syntaxe : la page de tarification confirme que les _repository rules_ du plan Free sont réservées aux dépôts **publics**. `palier` est privé, et le restera — c'est un service commercial propriétaire, avec un abonnement prévu à 20 €/mois.

**Conséquence à ne pas édulcorer.** Il n'existe **aucune barrière côté serveur**. Rien n'empêche un push sur `main`, rien ne refuse une fusion dont la CI est rouge. Les 115 épreuves de franchissement produisent un rapport, pas un verrou.

**Ce qui a été écarté :**

| Option                                                                           | Pourquoi non                                                                                                |
| -------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------- |
| GitHub Pro                                                                       | Payant. Écarté par le porteur du projet, qui demande une solution gratuite                                  |
| Rendre le dépôt public                                                           | Le code deviendrait lisible par tous, sur un projet propriétaire                                            |
| Migrer vers un hébergeur dont les branches protégées sont gratuites sur le privé | Réel, mais impose de réécrire toute la CI et de quitter l'écosystème Actions. Coût sans rapport avec le lot |
| Interdire le push direct sur `main` par le hook local                            | Contournable par `git push --no-verify`, comme le hook lui-même                                             |
| Rétablir `main` automatiquement quand la CI échoue                               | Un robot qui réécrit l'historique sur un échec qu'il n'a pas compris fait plus de dégâts qu'il n'en répare  |

**Ce qui est fait à la place, et gratuitement.** `.github/workflows/gardien-main.yml` transforme la barrière absente en alarme :

- une CI rouge sur `main` ouvre une issue nominative, avec les jobs fautifs et le lien du journal ;
- l'alerte **se ferme d'elle-même** au premier passage vert, donc sa présence signifie toujours « `main` est cassée maintenant », jamais « elle l'a été un jour » ;
- il couvre exactement le trou que le hook local ne peut pas voir : trois défauts propres à Linux ont été trouvés le 20/08/2026, tous invisibles sous Windows.

**Ce que cela ne fait pas, et qu'il ne faut pas croire.** Le gardien n'empêche rien — le code cassé est déjà sur `main` quand il s'exécute. Il ne bloque aucune fusion. Il ne voit pas davantage un `--no-verify` que le hook. **La différence entre une alarme et un verrou reste entière**, et c'est le porteur du projet qui la comble, en regardant les issues ouvertes.

**Ce qui la garde.** `tests-harness/ci.test.mjs` vérifie que le gardien existe, suit bien le workflow `CI` sur `main`, porte `issues: write` — sans quoi il tournerait vert sans rien ouvrir — et sait aussi bien ouvrir que fermer. Franchi le 20/08/2026 en retirant la permission : l'épreuve rougit.

**Ce qui la rouvrirait :** un passage à un plan qui offre les règles de dépôt sur le privé, ou le jour où une deuxième personne rejoint le projet. À une seule paire de mains, la discipline peut tenir lieu de verrou ; à deux, elle ne le peut plus.

## D30 — Les alertes Dependabot étaient éteintes côté GitHub, le fichier ne suffit pas

**Tranché le :** 20/08/2026, après mesure d'un audit de complétude.

**Le fait.** `.github/dependabot.yml` existait depuis la tâche 21, avec ses quatre écosystèmes et ses délais de refroidissement. Une épreuve (`tests-harness/ci.test.mjs`) vérifiait qu'il couvrait bien npm, NuGet et les actions. Tout était vert.

Mais l'état du **service** GitHub, jamais interrogé :

```
GET  /repos/warrox1993/palier/vulnerability-alerts    → HTTP 404   (non activé)
GET  /repos/warrox1993/palier/automated-security-fixes → {"enabled": false}
```

`docs/08-workflow.md` § 5 liste « `npm audit` / Dependabot » comme brique du harnais. Seule la moitié `npm audit` tournait — en intégration continue, sur `front/` seulement. **Aucune alerte de vulnérabilité n'était signalée sur le dépôt.**

**Corrigé le jour même** par `PUT` sur les deux points d'entrée. Vérifié : `204` au lieu de `404`, et `{"enabled": true}`.

**La leçon, qui dépasse ce cas.** L'épreuve assertait le **contenu d'un fichier**, jamais l'**état du service** que ce fichier est censé configurer. C'est exactement le faux vert que ce lot a chassé toute la journée — un réglage accepté sans erreur n'est pas un réglage appliqué (P11), une règle mal nommée est acceptée sans rien faire, un fichier d'exclusion rend une fixture invisible de sa propre épreuve (P9). Ici, la variante est plus retorse : **le fichier était juste, et il ne servait à rien**, parce que la fonctionnalité qu'il paramètre était désactivée en amont.

**Ce qui reste ouvert.** Aucune épreuve ne garde cet état. La vérifier depuis la suite de tests demanderait un appel authentifié à l'API GitHub — fragile hors CI, et les droits par défaut de `GITHUB_TOKEN` ne couvrent pas forcément cette lecture. À traiter au lot 2 : soit une étape du job `securite`, soit une vérification manuelle inscrite au gabarit de rapport de lot.

**Ce qui la rouvrirait :** rien. Mais la classe de défaut qu'elle illustre — _un fichier de configuration correct dont le service est éteint_ — doit être cherchée partout ailleurs où une épreuve lit un fichier au lieu d'interroger le système.

## D31 — Deux points de la définition de terminé ne peuvent pas être gardés au lot 1

**Tranché le :** 20/08/2026, après un audit de complétude qui a compté 4 points pleinement tenus sur 12, 7 partiels et 1 absent.

**Le fait.** `docs/08-workflow.md` § 9 exige douze cases cochées, et `CLAUDE.md` § 1 rappelle qu'« il n'y a pas de _on finira plus tard_ ». Deux de ces points ne sont pourtant pas gardables aujourd'hui, pour une raison qui n'est pas de la paresse :

| Point                                              | Pourquoi il n'est pas gardable                                                                                                                                                                                 |
| -------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **États de chargement, d'erreur, vide et partiel** | On ne peut pas éprouver qu'un écran possède un état vide **sans écran**. Le mot n'apparaît nulle part dans le harnais, le plan, les décisions ni le journal — c'est le seul des douze points totalement absent |
| **Navigation au clavier**                          | axe-core couvre l'accessibilité statique du DOM ; le parcours au clavier se teste sur un parcours, et il n'y en a aucun                                                                                        |

**Ce qui n'est pas reporté, et qui existe déjà.** La moitié i18n du point est outillée : `chaine-en-dur` dans `scripts/regles-projet.mjs` refuse la copie en dur dans le JSX — nœuds de texte multilignes compris, ainsi que `title`, `alt`, `placeholder` et `aria-label`. Elle a mordu sur du vrai code (`App.tsx`) pendant le lot.

**Ce qui est reporté, nommément.** `i18next` n'est **pas installé**. L'installer maintenant produirait une dépendance que rien n'importe — Knip la signalerait comme morte, et il aurait raison. Elle arrive avec le premier écran.

**L'engagement, qui est le fond de cette décision.** Ces deux garde-fous s'écrivent **avec le premier écran du lot 2, pas après lui**. Concrètement, la première tâche qui produit un composant doit livrer dans le même lot :

1. une épreuve de franchissement qui **refuse** un écran dépourvu d'état vide ou d'état d'erreur ;
2. un parcours Playwright au clavier seul sur ce même écran ;
3. `i18next` installé et câblé, avec `chaine-en-dur` étendue aux constantes exportées — l'audit a montré qu'une chaîne déplacée dans une constante lui échappe.

**Pourquoi l'écrire plutôt que de le faire.** Écrire un garde-fou sans cible, c'est écrire une branche jamais franchie — le défaut que ce lot a combattu vingt-trois fois. Un contrôle qui n'a rien à contrôler passe au vert et ment. Mieux vaut une dette datée qu'un faux vert.

**LE VERDICT, écrit en toutes lettres le 20/08/2026, à la clôture du lot 2 : les TROIS livrables sont là. D31 n'a pas été reconduite.** Un « partiellement » n'existe pas, et il n'y en a pas eu :

| #   | Livrable                                                                    | Où                                        | La preuve du franchissement                                                                                                                                 |
| --- | --------------------------------------------------------------------------- | ----------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 1   | une épreuve qui **refuse** un écran dépourvu d'état vide ou d'état d'erreur | `front/tests/harness/etats-ecran.test.ts` | les **deux branches franchies SÉPARÉMENT** : état vide retiré → « L'écran « etat » n'a pas d'ÉTAT VIDE » ; état d'erreur retiré → « … pas d'ÉTAT D'ERREUR » |
| 2   | un parcours Playwright **au clavier seul** sur ce même écran                | `front/tests/e2e/etat-clavier.spec.ts`    | `tabIndex={-1}` sur le bouton → le parcours refuse ; bouton privé de nom accessible → axe-core refuse en `critical`                                         |
| 3   | `i18next` câblé, `chaine-en-dur` étendue aux constantes exportées           | tâche 3 du lot 2                          | acquis avant celui-ci                                                                                                                                       |

L'épreuve 1 ne lit **aucun fichier source** : elle monte chaque écran de `src/features/` dans les conditions qui produisent chaque état et regarde ce qui est rendu. Un contrôle textuel aurait été vert sur un composant qui contient le mot « erreur » dans un commentaire.

**Ce qui reste à surveiller :** l'épreuve 1 impose un contrat — tout écran marque ses états par `data-etat`. C'est le seul point commun exigible d'écrans qui n'ont ni les mêmes données ni les mêmes libellés ; c'est aussi une convention que rien n'enseigne à celui qui écrira le deuxième écran, sinon le message de refus lui-même.

**Ce qui la rouvrirait :** le premier écran. Si le lot 2 se termine sans ces trois livrables, cette décision a échoué et il faut le dire au lieu de la reconduire. — **Il ne s'est pas terminé sans eux.**

---

# Décisions de conception du lot 2 — 20/08/2026

Ces décisions ont été prises **en conception**, avant toute ligne de code du socle de données, et chacune a été mesurée sur le dépôt ou vérifiée à la source. Elles se lisent avec deux avertissements.

**Quatre d'entre elles portaient un arbitrage qui ne m'appartient pas** — D33 (activer WSL2 ou Hyper-V), D37 (`FORCE ROW LEVEL SECURITY`), D39 (l'écart au « schéma complet » de la feuille de route) et D42 (Tailwind). **D33 et D42 sont tranchées depuis** : WSL2 est installé, et le porteur a refusé Tailwind. L'arbitrage est nommé dans l'entrée, avec ce qu'il coûte de trancher dans un sens et dans l'autre. **Tant qu'il n'est pas rendu, la décision est proposée, pas prise.**

**D36 a été soumise à un jury de trois lentilles adversariales.** Les trois l'ont retenue — et les trois ont exigé des amendements bloquants avant adoption, parce que son plan de vérification réintroduisait en silence le mode de défaillance qu'elle prétendait supprimer. Ces amendements sont inscrits dans l'entrée, au même rang que la solution. Une décision qui cache son mode de défaillance est pire qu'une décision absente.

## D32 — Arborescence à trois racines : `front/`, `back/`, `db/`. Les migrations restent dans `back/`

**Tranché le :** 20/08/2026, en conception. La séparation `front` / `back` / `db` a été demandée par le porteur du projet ; l'emplacement des migrations est l'arbitrage technique qui en découle.

**Tranche :** `db/` est créé et porte exactement `compose.yaml`, `amorcage/` (rôles et privilèges), `referentiel/` (données de production — EFSA, exercices, programmes), `demonstration/` (jeu de développement synthétique), `SOURCES.md` (licence, version et date, fichier par fichier) et `README.md` (lever, appliquer, réinitialiser, sauvegarder). **Aucun `.csproj` sous `db/`.** Les migrations EF Core, le `DbContext`, les entités et les tests de politiques restent sous `back/Palier.Infrastructure/Migrations/` et `back/Palier.Database.Tests/`. Le SQL de `db/referentiel/` atteint les migrations par `<EmbeddedResource Include="..\..\db\referentiel\*.sql" />` dans `Palier.Infrastructure.csproj` — effet de bord voulu : si un fichier de référentiel disparaît, la compilation échoue au lieu de produire un schéma sans limites hautes EFSA.

**Motif :** mesuré configuration par configuration. Un `db/` de SQL, YAML et Markdown n'oblige à toucher que **trois** fichiers : `format:scripts` dans `package.json`, un bloc `docker-compose` dans `.github/dependabot.yml`, un bloc `[*.sql]` dans `.editorconfig`. Un `db/` portant un `.csproj` en casse **cinq de plus, dont deux en silence** — les deux ont été relus dans le dépôt le 20/08/2026 :

| Ce qui casserait en silence                                                            | Ce qui a été lu                                                                                  |
| -------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------ |
| `scripts/verifier-licences.mjs` parcourt littéralement `parcourir('back')` (ligne 196) | un projet sous `db/` échapperait **entièrement** au contrôle de licences de D13, sans un message |
| `.gitignore` n'ignore que `back/**/bin/` et `back/**/obj/` (lignes 6-7)                | les binaires de `db/` deviendraient des fichiers suivis au premier `git add -A`                  |

Sur un projet dont la leçon centrale du lot 1 est « un fichier de configuration juste dont le périmètre est faux ne dit rien » (D21, D24, D30), ajouter deux angles morts pour gagner un rangement est le mauvais échange.

**Ce qui a été écarté :** le déplacement des migrations vers un projet séparé. Il est supporté par EF Core, mais impose `MigrationsAssembly(...)`, un `IDesignTimeDbContextFactory` et deux options sur chaque commande — et si `MigrationsAssembly` manque, EF ne trouve aucune migration et **n'échoue pas** : il croit la base à jour. Le bénéfice que Microsoft donne à ce montage (projet de démarrage spécifique à une plateforme, ou plusieurs jeux de migrations) n'existe dans aucun des deux cas de `palier`.

**Ce qui la rouvrirait :** un second jeu de migrations (une base de recette au schéma distinct), ou EF Core 11 stable, qui apporte `.config/dotnet-ef.json` et supprime le coût ergonomique des deux options — au 20/08/2026 `Microsoft.EntityFrameworkCore` est stable en 10.0.11 et 11.0.0 n'existe qu'en `preview.7` (relevé sur api.nuget.org).

## D33 — Docker : oui, au lot 2, et pour PostgreSQL seul

**Tranché le :** 20/08/2026. Docker a été demandé par le porteur du projet en séance ; son **périmètre** est tranché ici.

**Tranche :** deux usages, pas un de plus — `db/compose.yaml` avec **un seul service** PostgreSQL pour le développement, et `Testcontainers.PostgreSql` pour les tests d'intégration. **Pas** de conteneur pour le backend en développement : `dotnet run` et `dotnet watch` restent sur l'hôte, avec le débogueur et `dotnet ef` natifs. **Pas** de `Dockerfile`. **Pas** de compose de production. L'image de production se fabriquera au lot 9 par `dotnet publish --os linux --arch x64 /t:PublishContainer`, qui produit une image OCI sans Dockerfile et sans démon quand on sort un tarball ; un `Dockerfile` ne sera écrit que si un besoin précis du lot 9 l'exige, et il sera alors **connu** au lieu d'être supposé.

**Motif :** ce n'est pas une préférence de confort, c'est une dépendance. `docs/08-workflow.md` § 6 fait du « test RLS vert pour chaque table » un **critère de sortie**, et `docs/03-donnees.md` § RLS explique pourquoi il compte double depuis D9 : le chemin nominal passant par l'API, **plus aucun test fonctionnel ne franchira RLS**. Ce test exige un vrai moteur, un rôle restreint et une seconde connexion — sans Docker, le lot 2 ne peut pas être déclaré terminé, et une défense en profondeur que rien n'éprouve est une défense qu'on croit avoir. À l'inverse, conteneuriser le backend en développement coûte des heures d'installation puis des secondes à chaque itération, pour un bénéfice de parité qui n'arrive qu'au lot 9 — et `docs/08-workflow.md` § 5 est explicite : « Si le build prend deux minutes, l'agent tourne en rond. »

**Le coût en argent est nul, et c'est vérifié.** Le porteur du projet a demandé une solution gratuite. Docker Desktop l'est sous **deux** seuils cumulés — moins de 250 salariés **et** moins de 10 M$ de revenu annuel ; un indépendant, puis une SRL naissante, sont dedans sans ambiguïté. Le client 29.6.2 et Compose v5.3.1 sont déjà installés sur le poste (mesuré le 20/08/2026).

**Ce qui a été écarté, et ce que cela laisse ouvert :** Podman Desktop et Rancher Desktop suppriment la question de licence **pour toujours**, au prix d'une configuration de Testcontainers (`DOCKER_HOST` vers la socket Podman, `TESTCONTAINERS_RYUK_DISABLED=true` en rootless). Le seuil contractuel de Docker Desktop reste donc une chose à surveiller, et le moment de le refuser est maintenant, pas dans deux ans.

**Conséquence à ne pas édulcorer :** `scripts/verify.mjs` lance `dotnet test back/Palier.sln` sur la **solution entière**. Dès que `Palier.Database.Tests` y entre, `npm run verify` — donc `git push`, puisque `.husky/pre-push` n'appelle que lui — exige un moteur de conteneurs, **sans qu'aucune ligne de `verify.mjs` ait changé**. Le refuser demanderait un `--filter`, c'est-à-dire une seconde liste de contrôles, exactement ce que D25 existe pour empêcher. Le hook de **pré-commit**, lui, ne lance que lint-staged et gitleaks : Docker n'est nécessaire qu'au moment du push.

**Arbitrage à confirmer par le porteur du projet — bloquant, et personne d'autre ne peut le rendre.** Mesuré le 20/08/2026 sur le poste : `Microsoft-Windows-Subsystem-Linux` et `Microsoft-Hyper-V` sont en `InstallState 2` (désactivés), seul `VirtualMachinePlatform` est actif, et `wsl --status` rend « n'est pas installé ». Activer l'un des deux demande des **droits administrateur et un redémarrage**. Ce que coûte de le faire : une soirée, une fois. Ce que coûte de ne pas le faire : **tout le lot 2 tombe**, puisque le test RLS sur un vrai moteur est son critère de sortie. Et rien ne le signale aujourd'hui — `docker --version` répond parfaitement pendant que le démon est injoignable, ce qui est précisément la classe de faux vert de D30.

**Ce qui la rouvrirait :** l'arrivée d'un second développeur — l'argument « il n'installe rien d'autre que Docker » redevient réel et la conteneurisation du backend en développement reprend de la valeur ; ou un besoin de configuration au lot 9 que `dotnet publish /t:PublishContainer` n'expose pas (utilisateur non-root, paquets système, ordre des couches).

## D34 — PostgreSQL 18.6, image Debian, un seul endroit où le tag est écrit

**Tranché le :** 20/08/2026, en conception.

**Tranche :** `image: postgres:18.6` dans `db/compose.yaml`. **Pas la variante `alpine`.** Le builder Testcontainers **lit ce tag dans le compose** au lieu de le redéclarer. La version majeure de production sera la 18, et le local copie la production, jamais l'inverse.

**Motif :** mesuré le 20/08/2026 sur la page « Capabilities and Limitations » des Public Cloud Databases d'OVHcloud (dernière mise à jour du 28/05/2026) : les versions offertes sont **14, 15, 16, 17 et 18**. `gen_random_uuid()`, employé partout dans le schéma de `docs/03-donnees.md`, est en cœur depuis la 13, sans extension à installer.

**Ce qui a été écarté :** `alpine`, pour une raison mesurable et non esthétique. musl n'implémente pas `LC_COLLATE` comme glibc, le tri y est octet par octet, et l'instance managée tourne sur glibc — un `ORDER BY` sur un nom d'exercice trierait différemment sur le poste et en production, c'est-à-dire exactement la divergence qu'une base locale existe pour supprimer. Écarté aussi : redéclarer le tag dans le builder Testcontainers. Deux déclarations du même tag divergent — c'est le défaut nommé par D25, et il se ferme par une **lecture** plutôt que par une discipline.

**Ce qui n'est délibérément pas étendu :** D28 épingle les actions GitHub par empreinte parce qu'une étiquette mutable y exécute du code avec accès au dépôt et aux secrets. Le conteneur PostgreSQL n'exécute rien qui touche au dépôt et ne voit aucun secret ; la mutabilité du tag `18.6` y est un **bénéfice**, puisqu'elle apporte les correctifs. L'épinglage par empreinte reste possible et n'est refusé par rien.

**Ce que le lot 2 a MESURÉ, et qui corrige une affirmation de cette entrée.** « Le builder Testcontainers lit ce tag dans le compose » : c'est fait, et la fixture lit aussi `POSTGRES_INITDB_ARGS` (D44), le nom de la base et le compte d'administration. Mais **la lecture ne suffisait pas** : `tests-harness/db.test.mjs` parcourt le dépôt pour refuser toute seconde déclaration, et le 20/08/2026 il a mordu sur une **ligne de commentaire** de `back/Palier.Database.Tests/CollationTests.cs` — « 2.41 sur `postgres:18.6` ». `npm run verify` était **rouge à HEAD** avant la tâche 9, et personne ne l'avait vu. La divergence ne se glisse pas seulement dans du code : elle se glisse dans une phrase qui explique le code.

**Ce qui la rouvrirait :** OVHcloud retirant la 18 de son offre, ou le choix d'une version de production différente au lot 9 — auquel cas le compose et Testcontainers suivent, dans ce sens et jamais l'inverse.

## D35 — La clé d'`AspNetUsers` est un `uuid`, et les tables d'identité naissent au lot 2

**Tranché le :** 20/08/2026, en conception.

**Tranche :** `Utilisateur : IdentityUser<Guid>` et `PalierDbContext : IdentityDbContext<Utilisateur, IdentityRole<Guid>, Guid>`. Les **14** clauses `references auth.users` de `docs/03-donnees.md` deviennent `references "AspNetUsers"("Id")`, en `uuid`, et tous les `owner_id` restent `uuid`. Les tables `AspNet*` sont **créées** par la migration `SocleInitial` du lot 2 ; aucune ligne de code d'authentification n'est écrite — cela reste le lot 4.

**Motif :** ferme le point 2 de « Ce qui n'est pas tranché ici » de `docs/03-donnees.md`, qui l'exige « à arbitrer **avant la première migration** ». `uuid` plutôt que le `text` que produit la configuration par défaut d'Identity, pour trois raisons cumulées : le schéma entier de `docs/03-donnees.md` est en `uuid` avec `gen_random_uuid()` ; la spec d'architecture du 19/08 § 8 prévoit des identifiants UUID v7 générés côté client pour l'idempotence de la file de retry ; et un `owner_id` en `text` alourdit tous les index de jointure du produit, dont `workouts (owner_id, started_at desc)`.

**Pourquoi au lot 2 et non au lot 4 :** créer les tables d'identité maintenant évite une reprise de schéma portant sur **quatorze** clés étrangères — et `CLAUDE.md` § 6 interdit de changer le schéma seul après la première mise en production. Le type de la clé est la seule chose qui doive être tranchée une fois pour toutes ; le reste du schéma peut s'étaler, et D39 l'étale.

**Ce qui la rouvrirait :** rien. Après la première migration appliquée en production, un changement de type de clé primaire n'est plus une décision : c'est une migration de données, et elle se traite comme telle.

## D36 — L'identité parvient au moteur par `set_config('app.utilisateur', $1, true)`, en portée transaction, doublée d'une garde applicative

**Tranché le :** 20/08/2026, en conception, **après un jury de trois lentilles adversariales**. Les trois ont retenu le mécanisme ; les trois ont exigé des amendements bloquants avant adoption. Ils sont intégrés à la tranche ci-dessous, et le mode de défaillance qu'ils ont mis au jour est écrit plus bas, au même rang que la solution.

**Tranche — six pièces, dont aucune n'est optionnelle.**

1. Chaque cas d'usage — **commandes ET requêtes** — s'exécute dans une transaction ouverte par un comportement du pipeline `Palier.Application`, et l'identité est posée juste après le `BEGIN` par `select set_config('app.utilisateur', {identifiant}, true)`, l'identifiant en **paramètre lié** (`Guid.ToString()`, jamais un `uuid` — `set_config` attend un `text` en deuxième argument, et un `Guid` passé en paramètre EF part en `uuid` et ne trouve pas la fonction).
2. Le comportement **refuse avant d'ouvrir la transaction** quand l'identité manque, avec le nom du cas d'usage dans le message. La fonction SQL est le filet, jamais le garde unique.
3. Côté base, **deux** accesseurs, dans un schéma `app` : `app.utilisateur()` qui **lève** (`errcode 28000`, message sans aucune valeur) pour les tables strictement privées, et `app.utilisateur_ou_null()` qui rend `NULL`, réservé aux tables à branche publique.
4. Les politiques appellent l'accesseur **enveloppé dans un sous-select** — `using (owner_id = (select app.utilisateur()))` — pour forcer une évaluation unique par instruction plutôt qu'une par ligne.
5. Le pipeline passe par `Database.CreateExecutionStrategy().ExecuteAsync(...)`.
6. `idle_in_transaction_session_timeout` est posé sur le rôle applicatif.

**Motif :** c'est le seul des mécanismes comparés dont le nettoyage est garanti par le **moteur** et non par le pilote. Vérifié mot pour mot à la source (postgresql.org/docs/17, `functions-admin.html`) : « If `is_local` is `true`, the new value will only apply during the current transaction. » Aucune valeur ne peut survivre au `COMMIT` ni au `ROLLBACK`, quels que soient `No Reset On Close`, le multiplexing d'Npgsql, ou un pool PgBouncer en mode transaction — que la documentation OVHcloud propose et décrit par ses propres mots comme « the default transaction-based pooling », et où la matrice des fonctionnalités de PgBouncer marque `SET/RESET` comme « Never ». **La défaillance possible est donc l'identité absente, jamais l'identité d'un autre.** C'est le faux vert que le lot 1 a chassé vingt-trois fois, appliqué cette fois à des données de l'article 9.

Le point 5 est une obligation, pas une précaution — Microsoft Learn, _Connection Resiliency_, vérifié : « if your code initiates a transaction using `BeginTransactionAsync()` … You will receive an exception … does not support user-initiated transactions. Use the execution strategy returned by `DbContext.Database.CreateExecutionStrategy()`. » Le jour où quelqu'un activera `EnableRetryOnFailure` sur une base managée qui clignote, **toutes** les requêtes lèveraient d'un coup. Le point 6 borne le scénario où un cas d'usage appelant un modèle laisserait une transaction ouverte pendant l'appel réseau.

**Ce qui a été écarté, et pourquoi.**

| Écarté                                                        | Pourquoi                                                                                                                                                                                                                                                                                                                                                                                                                                                                                    |
| ------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Un paramètre posé au bail de connexion, en **portée session** | Le nettoyage dépend du pilote. Npgsql l'assure par `DISCARD ALL` — mais sa propre documentation Performance précise que ces instructions « aren't actually sent when closing the connection — they're written into Npgsql's internal write buffer ». `No Reset On Close`, le multiplexing ou un PgBouncer en mode transaction l'annulent. La fuite est alors **l'identité d'autrui** : ni erreur, ni journal, ni test rouge                                                                 |
| `SET ROLE` par utilisateur                                    | Même portée session, même dépendance au pilote, et un objet de catalogue par compte                                                                                                                                                                                                                                                                                                                                                                                                         |
| `SET LOCAL app.utilisateur TO '<guid>'`                       | Sa localité est **syntaxique** : elle ne peut pas être fausse, ce qui est un avantage réel sur `set_config(…, true)`. Mais `SET LOCAL` n'accepte pas de paramètre lié — il faudrait concaténer un identifiant dans du SQL, sur le chemin qui existe précisément pour se protéger de l'injection. L'arbitrage réel est **« argument lié » contre « localité syntaxique »** ; il est tranché en faveur du premier, et il n'est légitime **que si la localité est éprouvée** (voir ci-dessous) |

**Le mode de défaillance, nommé au lieu d'être caché.** C'est la trouvaille du jury, et elle vise l'argument central.

1. **Toute la sûreté du dispositif tient à un littéral booléen dans une ligne de C#, que rien ne regardait.** Avec `false` au lieu de `true`, la valeur persiste en **session** et l'on retombe exactement sur le mécanisme écarté ci-dessus : fuite silencieuse entre utilisateurs dès que le pool réutilise la connexion. Les sept épreuves initialement proposées **passent toutes au vert avec `false`** — y compris celle qui semblait décisive (« deux requêtes successives, deux identités, sur la même connexion physique »), parce que la seconde requête pose sa propre identité, écrase la précédente, et ne voit que ses lignes. Test vert, mécanisme cassé. **L'épreuve qui discrimine est obligatoire :** une seule connexion physique (`Pooling=false`) ; `BEGIN` / `set_config(<A>, true)` / `select` / `COMMIT` ; puis, sur la **même** connexion et sans rien poser, `select current_setting('app.utilisateur', true)` **doit** rendre `NULL` ou vide, et une lecture d'une table **non vide doit** lever `28000`. Cette épreuve rougit sur `false`. Aucune des sept ne le faisait.
2. **« L'échec crie » est faux sur une table vide, et il faut cesser de l'écrire sans réserve.** `ddl-rowsecurity.html`, vérifié : l'expression d'une politique « will be evaluated **for each row** prior to any conditions or functions coming from the user's query ». Zéro ligne parcourue → zéro évaluation → **aucune exception**. Sur un compte neuf, une table fraîchement créée, les premiers jours de production, « identité absente » et « cet utilisateur n'a pas de données » redeviennent indiscernables. Pire, la loudness devient **dépendante du plan d'exécution** : sur un parcours d'index, le planificateur peut hisser la fonction `stable` en clé de parcours et l'évaluer une fois au démarrage — donc lever même sur table vide ; en parcours séquentiel sur table vide, non. **Une propriété de sécurité qui dépend du plan n'est pas une propriété.** D'où le point 2 de la tranche — la garde applicative en amont — et d'où l'obligation d'une épreuve **sur table vide** en plus de l'épreuve sur table peuplée.
3. **L'épreuve « sans identité → erreur SQL » est vraie sur certaines tables et fausse sur d'autres.** Sur les tables à branche publique (`exercises`, `foods`, `nutrient_refs`), la politique est de la forme `is_custom = false or owner_id = (select app.utilisateur())`, et PostgreSQL court-circuite le `OR` de gauche à droite : sur une ligne publique, l'accesseur **n'est jamais appelé**, aucune exception, les lignes sortent. C'est le comportement voulu, mais il impose que **chaque épreuve nomme la table sur laquelle elle porte** — sinon la même épreuve prouve deux choses contradictoires selon ce qu'on lui donne.

**Ce que cette décision n'achète pas.**

- **Elle ne protège pas de l'injection SQL.** Un SQL injecté peut reposer `app.utilisateur`. La phrase de `docs/03-donnees.md` § RLS qui range « une injection SQL ou une requête brute » parmi ce que RLS protège est trop généreuse : RLS y protège du **filtre oublié**, pas de l'attaquant délibéré. C'est du contenu métier — la correction est **soumise au porteur du projet**, pas appliquée en silence.
- **Elle n'empêche pas structurellement un chemin sans transaction.** Un `DbContext` utilisé hors pipeline — `IHostedService`, tâche de fond, contrôle de santé, file de rejeu, migration au démarrage — n'ouvre pas de transaction, donc ne pose pas d'identité : bruyant sur table peuplée, **silencieux sur table vide** (point 2 ci-dessus). Un test d'architecture interdisant l'usage de `DbContext` hors des handlers ferme la classe entière et coûte une heure ; il est au plan du lot 2. C'est la différence entre « le pipeline le fait » et « rien d'autre ne peut le faire ».

**Ce que cette décision réécrit :** la ligne « **Transaction — les commandes seulement, jamais les requêtes** » de `docs/superpowers/specs/2026-08-19-architecture-backend-csharp-design.md` § 5, ligne 108. Elle est incompatible avec ce mécanisme, et la laisser produirait une politique qui mord sur les écritures et disparaît sur les lectures — le chemin le moins dangereux gardé, le plus dangereux ouvert.

**Ce que cela coûte :** **trois** allers-retours par cas d'usage (`BEGIN`, `set_config`, `COMMIT`), pas un.

**Ce que le lot 2 a MESURÉ — les trois réserves ci-dessus sont traitées, et une l'est mieux qu'annoncé.**

- **Le coût des trois allers-retours** est éprouvé par `Le_cout_des_trois_allers_retours_est_mesure_et_non_suppose` : un cas d'usage complet (`BEGIN` · `set_config` · `select count(*) from sets` avec sa politique par jointure · `COMMIT`), sur 20 tours, une connexion `Pooling=false` et 50 séries, reste **sous le plafond de 100 ms** de `docs/08-workflow.md` § 6. **Ce chiffre ne prouve pas le plafond en production** : conteneur local, boucle locale, quelques dizaines de lignes. Il établit seulement que le mécanisme lui-même ne le consomme pas.
- **Le trou de la table vide** est **fermé du bon côté** : la garde applicative refuse AVANT d'ouvrir la transaction, et son épreuve n'assertionne rien sur le comportement du moteur. Mesuré en chemin, et **délibérément non revendiqué** : avec l'enveloppe `(select app.utilisateur())`, PostgreSQL 18.6 lève `28000` **même sur table vide**, pour les trois formes de requête essayées. C'est mieux qu'annoncé — et rien ne promet que le planificateur l'évaluera toujours. Une propriété de sécurité qui dépend du plan d'exécution n'est pas une propriété.
- **Le test d'architecture « il est au plan du lot 2 » est ÉCRIT**, et il couvre **quatre** voies et non trois : constructeur, propriété, champ, **et paramètre de méthode** — `void Poser(PalierDbContext)` passait intégralement, franchi le 20/08/2026. Il porte une liste d'**exemptions nommées**, une seule à ce jour (`Palier.Api.Socle.LecteurDeSocle`, la route de santé et l'assertion de démarrage), gardée par une épreuve qui refuse une exemption dont la cible n'existe plus.

**Ce qui la rouvrirait :** la mesure d'un surcoût inacceptable sur l'instance OVHcloud réelle — à mesurer contre le plafond de 100 ms de `docs/08-workflow.md` § 6, pas à supposer. Ou une version de PostgreSQL offrant nativement une identité de session à portée transaction.

## D37 — Trois rôles PostgreSQL, trois chaînes de connexion, `FORCE ROW LEVEL SECURITY`, et une assertion contre la base réelle

**Tranché le :** 20/08/2026, en conception — **sauf `FORCE`, qui reste un arbitrage à confirmer** (voir plus bas).

**Tranche :** trois rôles, tous **non superutilisateurs**.

| Rôle                | Ce qu'il est                                                                                                        |
| ------------------- | ------------------------------------------------------------------------------------------------------------------- |
| `palier_migrations` | propriétaire des tables, sans `BYPASSRLS`, utilisé par `dotnet ef database update` et le seed, **jamais** par l'API |
| `palier_app`        | l'API — ne possède aucun objet, sans `BYPASSRLS`, `NOCREATEDB NOCREATEROLE`, privilèges accordés objet par objet    |
| `palier_sauvegarde` | `pg_dump` / `pg_restore` — seul candidat à `BYPASSRLS`                                                              |

`back/.env.example`, **qui n'existe pas encore dans le dépôt** (vérifié le 20/08/2026 : seul `front/.env.example` est présent ; `docs/16-projet.md` § 3 le _documente_, il n'est pas écrit), est créé et porte les trois : `ConnectionStrings__Palier`, `ConnectionStrings__PalierMigrations`, `ConnectionStrings__PalierSauvegarde`. `alter table … force row level security` sur toute table portant des données personnelles. Et l'API **refuse de démarrer** si l'une de trois requêtes de catalogue échoue : `rolsuper` ou `rolbypassrls` vrai sur `current_user` ; `current_user` propriétaire d'une table de `public` ; une table de `public` sans `relrowsecurity` **et** `relforcerowsecurity`.

**Motif :** « Superusers and roles with the `BYPASSRLS` attribute always bypass the row security system when accessing a table. Table owners normally bypass row security as well » (`ddl-rowsecurity.html`, vérifié). Sans deux rôles distincts, les migrations créent les tables, le rôle qui les a créées en est propriétaire, et **toutes les politiques ne font rien** — silencieusement. C'est ce que `docs/03-donnees.md` § RLS désigne déjà comme « la première chose à éprouver, avant les politiques elles-mêmes ».

**L'assertion au démarrage est le cœur de la décision, et elle vient de D30.** Sept épreuves vertes sur un Testcontainer ne démontrent rien sur l'instance OVHcloud, où deux inconnues décident si RLS mord **du tout** : le compte d'administration a-t-il `BYPASSRLS`, et les tables créées par les migrations appartiennent-elles au rôle de l'API ? La page « Capabilities » d'OVHcloud dit que la création d'utilisateurs par le Panneau de contrôle et l'API se fait « with default admin roles and privileges » et que « the only specific privilege you can set is `replication` » : le rôle applicatif restreint **ne peut donc pas** être créé par l'interface OVH, il l'est par SQL depuis le compte d'administration. Une assertion au démarrage est la seule preuve qui porte sur le **traitement** plutôt que sur le code — c'est ce qu'un auditeur peut lire, et c'est ce que D30 laisse explicitement « à traiter au lot 2 ».

**Arbitrage à confirmer par le porteur du projet : `FORCE ROW LEVEL SECURITY`, oui ou non, avec le prix sur la table.**

- **Sans lui**, un accès direct sous le rôle propriétaire n'est pas protégé — c'est la **première** des trois familles que `docs/03-donnees.md` nomme (outil d'administration, restauration, identifiants fuités, tâche lancée à la main sur le VPS).
- **Avec lui**, le propriétaire cesse de contourner RLS, et la sauvegarde change de nature. `pg_dump` pose `row_security = off` et « If the user does not have sufficient privileges to bypass row security, then an error is thrown » (`app-pgdump.html`, vérifié) ; `--enable-row-security` impose un dump au format `INSERT`, puisque « the COPY FROM during restore does not support row security ». Il faut donc un rôle `BYPASSRLS`, dont la création exige un superutilisateur ou un rôle qui le porte déjà.
- **Et ce point n'est pas mesurable avant d'avoir une instance.** L'offre OVHcloud repose sur Aiven, dont le compte d'administration n'est pas superutilisateur, et « Only superuser roles or roles with `BYPASSRLS` can specify `BYPASSRLS` » (`sql-createrole.html`). Le lot 2 mesure la branche « dump réussi » sur Testcontainers ; si elle n'est pas obtenue, l'arbitrage revient au porteur **avec le prix connu, pas en découverte**.

**Ce que `FORCE` coûte, quel que soit l'arbitrage :** la sauvegarde et la restauration deviennent un **livrable éprouvé du lot 2**, et non une promesse trimestrielle qui rencontrerait ce mur au pire moment (`docs/14-contenu.md` § 7 : « Une sauvegarde jamais restaurée n'est pas une sauvegarde »). Le seed, lancé sous le rôle propriétaire, est concerné au même titre — et les deux corrections réflexes, accorder `BYPASSRLS` au rôle de migration ou retirer `FORCE` le temps du seed, sont **exactement les deux façons d'éteindre RLS sans que rien ne le signale**. Le chemin du seed se conçoit, avec son épreuve.

**LA MESURE EST FAITE, dans les deux sens — 20/08/2026, PostgreSQL 18.6, Testcontainers et base locale.** La tâche 10 a provoqué les quatre configurations :

| Configuration du rôle de sauvegarde                 | `pg_dump`                                                                          |
| --------------------------------------------------- | ---------------------------------------------------------------------------------- |
| `BYPASSRLS` **et** `SELECT` sur tables et séquences | **code 0**, 41 287 octets                                                          |
| `BYPASSRLS`, **sans** `SELECT`                      | code 1 — `permission denied for table __EFMigrationsHistory`                       |
| `BYPASSRLS` et `SELECT` sur les tables seules       | code 1 — `failed to get data for sequence "AspNetRoleClaims_Id_seq"`               |
| **`NOBYPASSRLS`** avec `SELECT` complet             | code 1 — `query would be affected by row-level security policy`, **40 829 octets** |

Et sous `palier_migrations`, le rôle **propriétaire** : code 1, `query would be affected by row-level security policy for table "AspNetRoleClaims"` — la contrainte documentée est donc **réelle sur cette version du moteur**, provoquée et non citée.

**Trois choses que cette entrée ne disait pas, et que la mesure a apprises :**

1. **`BYPASSRLS` contourne les POLITIQUES, jamais les PRIVILÈGES.** Le lot n'accordait **aucun** `select` à `palier_sauvegarde` : le rôle existait, portait l'attribut, et ne pouvait rien sauvegarder. Deux barrières distinctes, et l'entrée n'en nommait qu'une.
2. **Les séquences aussi.** `grant select on all tables` ne suffit pas ; `AspNetRoleClaims_Id_seq` bloque le dump.
3. **UN DUMP RATÉ RESSEMBLE À UN DUMP.** Privé de `BYPASSRLS`, `pg_dump` écrit tout le schéma, bute sur le premier `COPY` refusé, et laisse **40 829 octets** là où le dump complet en fait **41 287**. `ls -l` ne distingue pas les deux. **Seul le code de sortie le fait**, et c'est écrit dans `db/README.md`.

**Le verdict :** `BYPASSRLS` sur `palier_sauvegarde` est **nécessaire**, pas prudentiel. La branche « dump réussi » **est obtenue**, et la restauration l'est aussi — dans une base neuve, sous `palier_migrations`, avec le **même nombre de lignes**, le même nombre de tables, le même nombre de politiques, et **zéro table sans `FORCE`**. `FORCE` ne s'échange donc contre rien sur Testcontainers.

**Ce qui la rouvrirait :** la constatation, sur l'instance OVHcloud réelle, qu'aucun rôle `BYPASSRLS` n'est créable. C'est la seule inconnue qui reste, et elle n'est pas mesurable avant d'avoir une instance. Dans ce cas `FORCE` s'échange contre la capacité de dumper la base, et l'arbitrage revient au porteur — **avec le prix chiffré ci-dessus, pas en découverte**.

## D38 — Les tables d'identité naissent en refus par défaut ; leur chemin d'accès est conçu au lot 4

**Tranché le :** 20/08/2026, en conception.

**Tranche :** `enable row level security` **et** `force row level security` sur les tables `AspNet*`, et **aucune politique**. Conséquence assumée et éprouvée : `palier_app` ne lit et n'écrit strictement rien dans ces tables.

**Motif :** « If no policy exists for the table, a default-deny policy is used, meaning that no rows are visible or can be modified » (`ddl-rowsecurity.html`, vérifié).

**Ce qui a été écarté :** ne pas mettre RLS sur les tables d'identité, ou y poser une politique `using (true)`. La parade est évidente et elle est mauvaise — elle ferait de la **seule table sans barrière de ligne** celle qui portera les empreintes de mots de passe, les secrets TOTP, les jetons de rafraîchissement et les sessions ; c'est-à-dire l'endroit où un filtre oublié ou un `FromSqlRaw` produirait la fuite la plus coûteuse du produit, et le seul où la défense en profondeur serait absente.

**Ce que cela bloque, et qui est le but :** le chemin de connexion lit `AspNetUsers` par email **avant** que la moindre identité existe ; il ne peut donc pas passer par `app.utilisateur()`. Avec le refus par défaut, il **casse — fermé et bruyant**, donc jamais en fuite. Le lot 4 doit concevoir ce chemin explicitement : rôle dédié avec sa politique, ou fonction `SECURITY DEFINER` au périmètre minimal, jamais une pose de l'identité d'autrui. Poser le refus au lot 2 garantit qu'il sera **conçu** et non découvert au premier `dotnet ef database update`.

**Contradiction signalée, pas résolue en silence :** `docs/08-workflow.md` § 6 exige « test RLS vert pour **chaque** table ». Les tables d'identité, les catalogues publics et les tables possédées par jointure n'entrent pas dans le modèle « A ne lit jamais une ligne de B ». La liste des tables hors de ce modèle doit être **nommée** dans `docs/03-donnees.md`, avec la forme de politique de chacune — sinon la même épreuve prouve deux choses contradictoires selon la table. C'est du contenu métier : soumis au porteur, pas tranché ici.

**Ce qui la rouvrirait :** le lot 4, obligatoirement. Cette décision est une porte fermée avec son écriteau, pas une position définitive.

## D39 — Le schéma arrive par tranches, avec le cas d'usage qui l'exige

**Tranché le :** 20/08/2026, en conception. **Arbitrage à confirmer par le porteur du projet — sans sa validation explicite, cette décision n'est pas prise.**

**Tranche :** la migration `SocleInitial` du lot 2 porte **six** tables, pas les dix-neuf : les tables d'identité, `nutrient_refs` (référence en lecture publique), `exercises` (catalogue public et personnalisé, `owner_id` nullable), `workouts` (possédée directe), `sets` (possédée par jointure), `body_weight` (possédée directe, avec `unique (owner_id, measured_on)`). Plus une vue (`weekly_volume`, écrite par `migrationBuilder.Sql(...)` avec son `Down`), une contrainte `CHECK` (`energy_1_5 between 1 and 5`, déjà au schéma) et deux index du schéma (`workouts (owner_id, started_at desc)` et `sets (workout_id)`). Les treize autres tables arrivent au lot qui les utilise.

**Motif :** `docs/08-workflow.md` § 6 exige un test de politique par table. Dix-neuf tables migrées d'un bloc, ce sont dix-neuf tests RLS sur des tables qu'aucun cas d'usage n'exerce — **du garde-fou sans cible**, exactement ce que D31 refuse et ce que le lot 1 a passé sa journée à combattre. Une migration EF Core est additive : poser une table au moment du besoin ne coûte rien, à la condition que le **type de la clé** soit tranché une seule fois, ce que D35 fait.

**Et la tranche n'est pas arbitraire :** elle contient une représentante de chacune des **cinq formes de table** du schéma — identité, référence publique, catalogue mixte, possédée directe, possédée par jointure. Chaque forme de politique est donc éprouvée dès le lot 2, et le lot suivant ajoute des tables sans inventer de mécanisme.

**Ce que l'arbitrage coûte, dans les deux sens.** Trancher **pour** la tranche : un écart assumé à `docs/07-roadmap.md` étape 1, qui dit « schéma complet », et `CLAUDE.md` § 5 range la feuille de route **au-dessus** de `CLAUDE.md` — l'écart doit donc être écrit dans la feuille de route, pas seulement ici. Trancher **contre** : dix-neuf tables et dix-neuf tests de politiques au lot 2, dont treize sans aucun cas d'usage pour les exercer, sur un lot dont le critère de sortie est déjà « chaque épreuve vue rouge en provoquant sa violation ».

**Ce qui la rouvrirait :** le porteur du projet, qui seul peut valider cet écart.

## D40 — Aucune donnée de production sur un poste de développement

**Tranché le :** 20/08/2026, en conception.

**Tranche :** interdiction de restaurer un dump de la base de production, ou tout extrait de celle-ci, sur une machine de développement ou dans un volume Docker local. Le développement se fait sur `db/demonstration/`, entièrement synthétique. Toute enquête sur des données réelles se fait sur le serveur, sous journalisation.

**Motif :** c'est le seul chemin par lequel des données de l'article 9 atterriraient dans un volume Docker sur un portable Windows que le projet ne chiffre pas et ne supervise pas. **Aucun document du dossier ne le dit** — cherché dans `01-conformite.md`, `12-confort.md`, `13-juridique.md`, `14-contenu.md` et `16-projet.md`. Or le seed est entièrement synthétique (EFSA, CIQUAL, contenus rédigés) : l'interdiction ne coûte **rien aujourd'hui**, où la base est vide, et se paierait très cher si elle arrivait après le premier incident. Elle entre à l'AIPD comme mesure **organisationnelle**, aux côtés des mesures techniques de D36 et D37.

**Ce que cela ne protège pas :** rien ne l'empêche mécaniquement. C'est une règle, pas un verrou — comme pour D29, la différence entre une alarme et une barrière reste entière, et elle se comble par la discipline d'une seule paire de mains.

**Ce qui la rouvrirait :** rien. Un besoin de reproduction sur données réelles se traite par un jeu anonymisé produit **sur le serveur**, jamais par un dump rapatrié.

## D41 — L'écran d'état est un instrument, et sa fin de vie est datée

**Tranché le :** 20/08/2026, en conception.

**Tranche :** `front/src/features/etat/` livre un écran qui lit `GET /api/v1/sante` et porte ses quatre états : chargement, vide (schéma migré, référentiel non chargé), erreur (conteneur arrêté), contenu. Il n'affiche aucune donnée de santé et aucun compte. Il n'entre pas au périmètre V1 de `docs/00-produit.md`. Au lot 4 il passe derrière l'authentification et un rôle d'administration ; **au lot 6, la question « le garde-t-on ou le supprime-t-on » est reprise explicitement et tranchée**, pas laissée à l'inertie.

**Motif :** D31 exige trois livrables « avec le premier écran du lot 2, pas après lui », et dit d'elle-même que les reconduire serait son échec. L'écran d'état est le plus petit écran qui ait honnêtement quatre états, et surtout le seul dont l'état d'erreur se **provoque** — `npm run db:down`, recharger — au lieu de se simuler. C'est la seule forme d'épreuve que ce projet accepte : provoquer l'absence, pas seulement constater la présence.

**Ce qui a été écarté :** un écran produit (séance, nutrition). Il ne peut pas être livré au lot 2, puisqu'il suppose l'authentification et le domaine ; et le construire contre une donnée simulée produirait une épreuve qui garde un **simulacre** — une marche au-dessus du défaut que D31 voulait éviter.

**Ce que cela coûte :** un écran hors périmètre V1 vit sur un domaine public jusqu'au lot 4. C'est le prix de l'échéance, et c'est pourquoi elle est datée.

**LIVRÉ ET PROVOQUÉ — 20/08/2026.** L'écran existe (`front/src/features/etat/`), avec ses quatre états. Le pari de cette décision — « le seul dont l'état d'erreur se **provoque** » — a été tenu, et voici la mesure, pas sa relecture :

```
1. base levée, écran chargé       -> contenu
2. npm run db:down ...
3. conteneur arrêté, après action -> erreur
   texte affiché                  -> Le socle de données ne répond pas
                                     La base est arrêtée ou injoignable. …
4. npm run db:up ...
5. base relevée, après action     -> contenu
```

L'action a été déclenchée **au clavier** — `Tab`, `Tab`, `Entrée` — sur un vrai navigateur, un vrai serveur de développement et une vraie API : aucun rechargement truqué, aucun bouchon. Côté API, `GET /api/v1/sante` rend **503** conteneur arrêté, et la ligne de journal ne porte que le **type** d'échec, jamais l'instruction ni ses paramètres.

**Ce que le franchissement a appris, et qui n'était pas prévu :** `vite preview` rend `index.html` avec un code **200** sur toute route inconnue. Sans contrôle du type de contenu, l'écran aurait cru recevoir un état. Le module d'accès refuse donc aussi un corps qui n'est pas du JSON — et c'est ce piège qui donne aux épreuves Playwright un état d'erreur **déterministe**, sans qu'aucune donnée soit simulée.

**Ce qui la rouvrirait :** le lot 6, obligatoirement. Sans cette échéance écrite, l'écran resterait par inertie.

## D42 — Pas de Tailwind, ni aucune bibliothèque de composants. CSS écrit à la main.

**Tranché le :** 20/08/2026, **par le porteur du projet**, qui a répondu à l'arbitrage : « pas de tailwind, garde les jetons avec html css et js+typescript ». Ce n'est plus un choix par défaut valable jusqu'à réponse — c'est la décision, et elle vaut au-delà du lot 2.

**Ce que cela ferme.** Aucune bibliothèque de classes utilitaires, aucun jeu de composants tout fait — ni Tailwind, ni shadcn, ni Material, ni leurs équivalents. Le style s'écrit en CSS, alimenté par les jetons. `CLAUDE.md` § 3, qui annonçait Tailwind dans la pile depuis l'origine, est corrigé.

**Ce que cela protège, et qui est le vrai motif.** `docs/design/references-visuelles.md` § 2 pose que les bibliothèques toutes faites « sont précisément la source du look générique : elles livrent dégradés, glassmorphism, cartes arrondies à ombre douce ». Le porteur du projet a été explicite depuis le début : « je refuse d'avoir un design type claude par défaut », « je ne veux plus de design saas ultra générique ». Installer une bibliothèque de composants revenait à réintroduire ce que la direction visuelle interdit — le coût technique décrit ci-dessous n'était que le second argument.

**Tranche :** `front/src/ui/jetons.ts` exporte les dix couleurs, l'échelle typographique 11/13/15/19/24/30/38, la base 4 px, le rayon 2 px, la zone tactile de 48 px de `docs/02-design.md` § 4, et les trois durées avec la courbe de D8 ; il génère les variables CSS que consomment les feuilles de style.

**Motif :** `CLAUDE.md` § 3 annonce Tailwind dans la pile, mais `CLAUDE.md` § 6 classe l'ajout d'une **dépendance lourde** parmi ce qui ne se tranche pas seul. Et le coût technique n'est pas neutre : la règle `couleur-hors-jetons` de `scripts/regles-projet.mjs` cherche un motif `#RRGGBB` dans les `.ts`, `.tsx` et `.css` ; des classes utilitaires Tailwind n'en portent aucun, et la règle deviendrait **aveugle sans rien signaler** — la classe de défaut de D21.

**Ce que l'arbitrage coûte, dans les deux sens.** Ne rien installer laisse les deux voies ouvertes et ne ferme rien ; le prix est d'écrire des feuilles de style à la main jusqu'au lot 6. Installer Tailwind au lot 2 ferme la voie inverse **au moment où l'on a le moins d'information** — un seul écran — et oblige à réécrire `couleur-hors-jetons` pour qu'elle voie encore quelque chose, sans quoi la règle passe au vert en ne contrôlant rien.

**Ce que cette décision répare accessoirement :** `front/src/ui/jetons.ts` est exactement la cible que la règle attend déjà par son exclusion `/src[\\/]ui[\\/]jetons\./` (ligne 299, lue le 20/08/2026) — et ce fichier **n'existe pas**. Aujourd'hui, `couleur-hors-jetons` n'a aucune cible et ne protège rien.

**Ce qui la rouvrirait :** rien sur le principe. Sur la mise en œuvre, le lot 6 dira si le CSS écrit à la main tient à l'échelle d'un jeu de composants complet — et si une aide s'avérait nécessaire, ce serait un outil qui laisse les valeurs visibles dans le code (modules CSS, variables natives), jamais un catalogue de classes qui rendrait `couleur-hors-jetons` aveugle.

## D43 — La feuille de route porte les durées constatées, lot par lot

**Tranché le :** 20/08/2026, en conception.

**Tranche :** `docs/07-roadmap.md` gagne un tableau « étape ↔ lot ↔ date de début ↔ date de fin ↔ durée constatée », rempli à chaque fin de lot depuis le rapport de lot, dans **ce fichier** et non dans un second document.

**Motif :** D9 se rouvre sur « un retard de livraison imputable au coût du backend, ou la constatation que l'authentification maison consomme plus de temps que le produit » ; D17 se rouvre sur « un retard imputable à cette réécriture ». **Rien dans ce dépôt ne mesure le temps écoulé par étape.** Deux décisions structurantes ont donc une condition de réouverture qu'aucun instrument ne peut déclencher — c'est la classe de défaut de D30 transposée au calendrier : la règle existe, rien ne l'observe.

**Pourquoi dans `07-roadmap.md` et pas dans un `07b-lots.md` :** deux documents portant l'ordre de construction divergeraient, et **aucune épreuve de franchissement ne peut vérifier que deux textes en prose disent la même chose**. C'est le motif exact de D25.

**Ce que cela ne fait pas :** un tableau ne se remplit pas tout seul. Comme le gardien de `main` de D29, c'est un instrument et non un verrou — il ne mesure que si le rapport de fin de lot l'alimente.

**Ce qui la rouvrirait :** rien.

## D44 — Collation ICU en `fr-BE`, et non le fournisseur du système

**Tranché le :** 20/08/2026, après mesure sur conteneur jetable puis sur la base réelle.

**Ce qui a déclenché la vérification.** Le porteur du projet a demandé si `postgres:18.6` Debian était le bon choix. La réponse est oui — PostgreSQL 18.6 est la version courante au 13/08/2026, OVHcloud propose bien PostgreSQL 18 en base managée, et la variante Debian est justifiée : musl n'implémente pas `LC_COLLATE`, le tri y devient octet par octet quelle que soit la variable `LANG`. Mais la vérification a montré autre chose.

**Ce que la mesure a corrigé dans notre propre raisonnement.** Il avait été avancé qu'une base en `en_US.utf8` trierait mal le français. **C'est faux**, et la mesure le dit :

| Configuration                    | Résultat sur la même liste                |
| -------------------------------- | ----------------------------------------- |
| `en_US.utf8`, fournisseur `libc` | `eau < Éclair < élan < Ève < œuf < zèbre` |
| `fr-BE`, fournisseur ICU         | `eau < Éclair < élan < Ève < œuf < zèbre` |

Identiques. La glibc applique ISO 14651, qui gère déjà les accents, la casse et la ligature « œ ». L'argument du tri cassé était excessif et a été retiré.

**Le motif réel, qui lui tient.** Sous le fournisseur `libc`, `datcollversion` **est la version de la glibc de l'image** — 2.41 ici. Elle change avec l'image de base, avec une mise à jour du socle de l'hébergeur, avec un passage de Debian 13 à 14. Or un changement d'ordre de tri **invalide les index sur les colonnes texte** : les requêtes rendent alors des résultats faux, et PostgreSQL ne le signale que par un avertissement au démarrage que personne ne lit.

Sous ICU, la version est celle de la bibliothèque — 153.128 — versionnée indépendamment du système, et la locale est inscrite **dans la base** (`datlocale`) au lieu d'être héritée d'une variable d'environnement. Troisième effet, plus prosaïque : `en_US.utf8` doit **exister** dans l'image ; ICU n'a pas cette dépendance.

**Ce qui est fait.** `POSTGRES_INITDB_ARGS: --locale-provider=icu --icu-locale=fr-BE --encoding=UTF8` dans `db/compose.yaml`. Et — c'est le point qui compte autant que le réglage — **la fixture Testcontainers LIT cette valeur dans le compose** au lieu de la redéclarer. Sans cela, les épreuves d'isolation tourneraient sur la collation par défaut de l'image pendant que la base locale et l'instance managée seraient en ICU : une divergence entre ce qu'on teste et ce qu'on exploite, qui ne se manifesterait pas par un test rouge mais par un ordre de résultats faux en production.

**Ce qui la garde.** `back/Palier.Database.Tests/CollationTests.cs`, deux épreuves qui interrogent le **moteur** et non le fichier — un argument accepté par `initdb` n'est pas un argument appliqué (P11). Franchies le 20/08/2026 en retirant le réglage du compose : « Le cluster utilise le fournisseur de collation « c » et non « i » (ICU) », avec la commande de correction dans le message. La seconde épreuve vérifie l'ordre français lui-même, ce qui rend le motif d'écarter `alpine` **vérifiable** au lieu d'être une note dans un commentaire.

**Ce que cela n'achète pas.** La version d'ICU changera, elle aussi — simplement de façon explicite et versionnée, au lieu de suivre l'image système. L'épreuve relève `datcollversion` sans l'asserter sur une valeur, précisément pour cette raison. Et rien n'a été mesuré sur l'instance managée d'OVHcloud, qui n'existe pas encore : si elle imposait un fournisseur ou une locale, cette décision se rouvrirait.

**Ce qui la rouvrirait :** une contrainte d'OVHcloud sur le fournisseur de collation, ou un besoin de tri propre à une autre langue quand l'anglais arrivera — `docs/11-qualite.md` prévoit le français et l'anglais dès la première ligne, et la locale du **cluster** ne peut pas être les deux. Le cas échéant, la collation se pose par colonne ou par requête, pas par base.

## D45 — Application web, et la résilience hors ligne sort de la définition de fini

**Tranché le :** 20/08/2026, **par le porteur du projet** : « c'est web à 100 % la priorité », et sur le mode déconnecté : « on peut le laisser en soi ce n'est pas un problème mais je doute que ce soit utile, aujourd'hui on a toujours internet ».

**Le défaut trouvé en cherchant.** L'exigence hors ligne était **essaimée dans cinq documents**, et le porteur du projet indiquait l'avoir déjà corrigée. Vérifié : `docs/11-qualite.md` n'a qu'**un seul commit**, `82aeecd` du 19/08 — sa correction n'a jamais atteint le dépôt. C'est le ruling P7 appliqué à quelqu'un d'autre que nous : une correction qui n'atteint pas tous ses lecteurs n'existe pas. Corriger un document sur cinq laissait l'exigence revenir par les quatre autres.

**Ce qui change, et c'est le cœur.** « Gestion hors ligne » figurait dans la **définition de fini** de `00-produit.md` et de `07-roadmap.md`. Une exigence portée là bloque **toutes** les livraisons : chaque écran devait porter sa résilience dès sa première ligne, avant même qu'une API existe. Portée par un lot, elle en bloque un seul. Elle est donc retirée de la définition de fini et devient le **chantier du lot 7**.

**Ce qui ne change pas.** `11-qualite.md` § 1 reste valable **mot pour mot**. Et son motif n'est pas celui qu'on lui prêtait : il ne s'agit pas d'utiliser l'application sans réseau — les salles ont du wifi, le porteur a raison — mais de **ne pas perdre une série saisie** quand le réseau hoquette entre deux répétitions. « On ne jette jamais silencieusement une donnée saisie » est une exigence de fiabilité de la saisie, pas de mode déconnecté. La nuance explique pourquoi le chantier reste au programme au lieu d'être supprimé.

**Trois choses distinctes, qui se décident séparément, et dont deux ne sont pas tranchées :**

|                                                                                              | Statut au 20/08/2026                                                                                   |
| -------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------ |
| Le **mode déconnecté** — service worker, pré-cache de 500 aliments, consultation sans réseau | **À décider au lot 7.** C'est la partie la plus coûteuse et la moins justifiée                         |
| L'**écriture locale d'abord** — la saisie va en IndexedDB puis part au réseau                | **À décider au lot 7.** Sans elle, chaque série attend la réponse de l'API avant de s'afficher validée |
| La **PWA installable** — icône sur l'écran d'accueil, plein écran                            | **Reportée**, lot 6. Indépendante des deux autres                                                      |

**Pourquoi ne pas trancher maintenant, alors que le porteur penche vers le retrait.** Parce qu'aucune donnée n'existe. Et parce que `07-roadmap.md` prévoit déjà le moment exact où elle existera : l'étape 2 impose que **le fondateur utilise l'application pour son propre bloc pendant au moins trois semaines, sans autre outil**. C'est là qu'on saura si le réseau lâche à la salle, combien de fois, et ce que ça coûte. Décider avant, dans un sens ou dans l'autre, serait deviner — construire une résilience inutile ou supprimer une protection qu'on regrettera.

**Le design reste conçu pour les petits écrans.** Le porteur avait dit « la priorité de cette app va être sur téléphone » ; il dit maintenant « web à 100 % ». Les deux ne se contredisent pas : le premier parlait de la **conception visuelle**, le second de la **plateforme**. Une application web, consultée dans un navigateur de téléphone, dessinée pour cet écran d'abord.

**Ce qui la rouvrirait :** les trois semaines d'usage de l'étape 2. Si le réseau tient, le mode déconnecté saute et le lot 7 se réduit. S'il lâche, on saura exactement quoi construire — et sur quelle fréquence réelle.

## D46 — Les photos ne sont jamais stockées : elles traversent le modèle et disparaissent

**Tranché le :** 21/08/2026, **par le porteur du projet** : « les photos vont uniquement aller dans l'IA Claude ou Gemini pour analyse des repas et des ingrédients, on ne va pas garder ces photos nulle part ». Puis, en précisant ce qui alimente les tables : « ce qui va alimenter les tables, ce sont les données que l'IA va renvoyer en format JSON, pas les photos en elles-mêmes ».

**Le trou que cette décision referme.** `07-roadmap.md` signalait le 20/08 un point « décidé nulle part » : Supabase Storage était l'endroit implicite où les photos allaient, et D15 l'a emporté sans lui donner de successeur. La réponse n'est pas de choisir un remplaçant — OVH Object Storage aurait été le candidat — mais de constater que **le besoin n'existait pas**. La photo est un mode de saisie, au même titre que le clavier ou le code-barres. Elle n'a jamais eu vocation à devenir une donnée.

**Le flux, désormais explicite :**

```
photo (navigateur) → EXIF retiré → modèle → JSON structuré → validé (D47) → tables
                                                                    ↓
                                                            la photo est jetée
```

**Ce que cela supprime.** Plus de stockage d'objets, plus d'URL signées à durée limitée, plus de chiffrement au repos à garantir, plus de purge à orchestrer à la suppression d'un compte, plus de sous-traitant de stockage au registre des traitements. **Aucune donnée de santé au repos sous forme d'image.** Un chantier entier disparaît de l'architecture.

**Ce que cela ne supprime pas, et qu'il ne faut pas confondre.** Ne pas stocker n'est pas ne pas transmettre. La photo part toujours chez Anthropic ou Google, et `13-juridique.md` § 2 continue de s'appliquer **intégralement** : accord de sous-traitance signé, minimisation du contexte, consentement séparé pour l'assistant, option de désactivation.

**Ce que cela déplace, et rend plus urgent.** `08-workflow.md` § 6 exige la suppression des métadonnées EXIF, « les photos contiennent des coordonnées GPS ». Cette exigence changeait autrefois de moment — on nettoyait avant de stocker. Elle se déplace **avant l'envoi au modèle, et côté navigateur**. Sans cela, on transmet à un fournisseur américain les coordonnées GPS du domicile de l'utilisateur, attachées à une photo de son repas. C'est désormais le **seul** moment où ce nettoyage peut avoir lieu : il n'y a plus d'étape ultérieure pour rattraper l'oubli.

**Ce que cela coûte, et qui est réel.** `04-nutrition.md` § 6 justifiait la conservation par la traçabilité : pouvoir revérifier ce qu'une extraction a produit. Sur un complément, les doses extraites alimentent la comparaison aux limites hautes EFSA — si le modèle lit « 25 mg » là où l'étiquette porte « 2,5 mg », plus rien ne permet de le constater après coup. **D47 est la contrepartie de cette perte**, et les deux décisions ne se séparent pas.

**Ce qui la rouvrirait :** un taux d'erreur d'extraction constaté qui rendrait la vérification a posteriori nécessaire — auquel cas la photo redeviendrait une pièce justificative, et non une donnée du produit.

**Documents à reprendre :** `03-donnees.md` (champ `supplements.photo_path`, supprimé), `04-nutrition.md` § 6 (la phrase sur la conservation), `13-juridique.md` (registre des traitements, ligne stockage).

---

## D47 — Le JSON du modèle est validé, plausibilisé et confirmé avant d'entrer en base

**Tranché le :** 21/08/2026, en conséquence directe de D46.

**Pourquoi cette décision existe.** Puisque c'est le JSON qui alimente les tables et non la photo, c'est le JSON qu'il faut contrôler. Et il le faut d'autant plus que la photo ne subsiste plus pour arbitrer : le contrôle doit avoir lieu **avant** l'écriture, parce qu'il n'y aura pas d'après.

**Trois contrôles, dans cet ordre, avant qu'une valeur touche une table :**

**1. Schéma.** Types, unités, champs obligatoires, valeurs d'énumération. `06-ia.md` § 1 impose déjà des « sorties structurées quand la réponse alimente une interface ». Côté C#, c'est FluentValidation dans le pipeline de `Palier.Application`, avant le handler.

**2. Plausibilité, calculée depuis les données du produit.** `nutrient_refs` porte déjà les limites hautes EFSA. Une dose extraite qui dépasse l'UL d'un ordre de grandeur n'est pas un dépassement à signaler — c'est une erreur de lecture. Un zinc à 250 mg sur une étiquette de complément, c'est une virgule mal placée.

**La distinction est un enjeu de sécurité, pas de confort.** Le produit doit _alerter_ sur un dépassement réel et _rejeter_ une valeur aberrante. Les confondre, c'est soit affoler l'utilisateur pour une erreur de lecture, soit laisser une donnée fausse fonder la comparaison qui est la raison d'être du produit.

**3. Confirmation humaine.** `04-nutrition.md` § 6 l'impose déjà pour les repas — « le système propose des lignes pré-remplies, **que l'utilisateur valide** ». Étendu aux compléments, où l'enjeu est plus élevé puisque c'est là que se joue la comparaison aux limites hautes. **La traçabilité passe de l'image à l'acte de validation** : ce n'est plus la photo qui atteste, c'est l'utilisateur qui a vu la valeur et l'a confirmée.

**Où vivent ces contrôles.** Dans `Palier.Domain` et `Palier.Application`, jamais dans l'adaptateur du modèle. Un contrôle logé dans ce qu'il contrôle ne contrôle rien — c'est le motif de D11, appliqué ici.

**Ce que cela n'est pas.** Le modèle ne calcule toujours rien : il **extrait**, ce que `06-ia.md` § 1 autorise explicitement (« extraire des doses d'une étiquette »). La frontière de `01-conformite.md` § 3 reste intacte — les seuils, les références et la comparaison restent dans le code déterministe.

**Ce qui la rouvrirait :** rien. Un contrôle de plausibilité sur des données de santé ne se retire pas.

---

## D48 — Mifflin-St Jeor est conservée, malgré la méta-analyse qui la déconseille

**Tranché le :** 21/08/2026, pendant le lot 3, après vérification des formules contre la littérature primaire.

**Ce que la vérification a trouvé.** La méta-analyse de référence sur les sportifs (Sports Medicine, 2023) conclut que cinq équations satisfont les critères d'exactitude **sans différence significative** avec la mesure — Cunningham 1980 et 1991, Harris-Benedict, De Lorenzo, Ten-Haaf — tandis que **Mifflin-St Jeor sous-estime significativement** et figure parmi celles à éviter. Ten-Haaf 2014 place 80,2 % des sujets à ±10 % de la mesure, contre 40,7 à 63,7 % pour les autres.

**Pourquoi elle est conservée quand même.** Trois faits l'emportent.

**Ten-Haaf n'est validée que de 18 à 35 ans** — c'est le titre même de l'article, et aucune validation externe au-delà n'a été trouvée. La population du produit ne s'arrête pas à 35 ans ; l'y appliquer serait extrapoler hors du domaine où l'équation a été construite. Mifflin-St Jeor a été développée sur une population de 19 à 78 ans.

**Le biais de Mifflin est systématique, donc documentable.** Celui qu'introduirait une donnée d'entrée fausse ne l'est pas.

**Et le choix ne dure que trois semaines.** `04-nutrition.md` § 1 : « à la semaine 4, elle n'est plus utilisée du tout ». Optimiser une estimation qu'un TDEE mesuré remplace au bout de vingt et un jours revient à polir ce qu'on va jeter. Ce qui compte est que l'estimation sorte **avec sa fourchette**, et que le passage à la mesure se fasse vite.

**Ce qui la rouvrirait :** une validation externe de Ten-Haaf au-delà de 35 ans.

---

## D49 — Katch-McArdle n'est retenue que sur une mesure fiable de masse grasse

**Tranché le :** 21/08/2026, en conséquence de D48.

**Le fait qui tranche.** Katch-McArdle — la révision 1991 de Cunningham, `370 + 21,6 × masse maigre` — est sans biais significatif chez le sportif. Mais elle n'est plus exacte que Mifflin-St Jeor **que si le pourcentage de masse grasse l'est**. Les balances à impédance domestiques mesurent en moyenne **4,4 points sous la DXA**, et l'erreur se propage à environ **16 kcal par point** : sur cinq points d'écart, 80 kcal, davantage que le biais qu'on cherchait à corriger.

Une formule exacte nourrie d'une donnée fausse est moins fiable qu'une formule biaisée nourrie d'une donnée juste.

**Ce que cela impose au produit.** La **provenance** de la mesure devient une donnée, au même titre que sa valeur : le type `PourcentageMasseGrasse` ne peut pas être construit sans elle. Trois provenances sont distinguées — mesure fiable (DXA, pesée hydrostatique, plis cutanés par un professionnel), impédancemétrie, déclaratif — et seule la première déclenche Katch-McArdle.

**Ce que cela ouvre :** une colonne de provenance dans le profil utilisateur. Changement de schéma, donc décision du porteur du projet.

**Ce qui la rouvrirait :** une génération de balances dont l'écart à la DXA descendrait sous un point.

---

## D50 — Le coût d'une séance se calcule par les METs, pas par un forfait

**Tranché le :** 21/08/2026, pendant le lot 3.

**La question posée était de calculer la dépense à partir de la charge, des répétitions et du RIR. La réponse de la littérature est négative.** La revue systématique de 2024 sur les méthodes d'estimation de la dépense en musculation recense la calorimétrie indirecte, le lactate sanguin, les moniteurs portables et les METs — **aucune formule fondée sur charge × déplacement × répétitions, ni sur le volume de charge**.

**Ce qui remplace le forfait.** `1 MET = 1 kcal/kg/h`, donc `MET × masse × durée`. Le forfait de 5 kcal/min que portait `04-nutrition.md` § 1 ne distinguait ni le sexe ni la masse : pour une femme de 55 kg il sortait par le haut de la fourchette mesurée chez la femme, qui va de 2,3 à 5,2 kcal/min.

**La valeur du MET ne vit pas dans le code.** Les sources publiées donnent de 3,5 à 9,0 selon la version du Compendium et le code d'activité retenu, et l'accès automatisé au Compendium 2024 est refusé (HTTP 403, trois tentatives). La valeur arrive en paramètre et vit en base avec son code, sa version et sa date — même traitement que les limites hautes, et pour la même raison.

**Ce qui reste à faire :** relever à la main la valeur du code « resistance training, multiple exercises, 8-15 reps at varied resistance » du Compendium 2024, qui est le cas d'usage du produit.

**Ce qui la rouvrirait :** la publication d'une formule validée reliant le volume de charge à une dépense.

---

## D51 — Les références sanitaires ont quatre statuts, et aucune valeur ne vit dans le code

**Tranché le :** 21/08/2026, pendant le lot 3. C'est le résultat le plus structurant de la vérification.

**L'EFSA ne produit pas une seule sorte de valeur.** Une _tolerable upper intake level_ autorise à parler de dépassement. Un _safe level of intake_ ne l'autorise pas : l'avis qui l'établit précise que « le niveau où le risque commence à augmenter n'est pas défini ». Et pour certains nutriments, aucune valeur n'est dérivable des données disponibles.

Les confondre annoncerait un danger là où la science n'en définit aucun. La comparaison rend donc **quatre** issues : sous la référence, au-dessus, référence indicative, aucune référence.

**Les valeurs bougent, et récemment.** Vérifié à la source primaire le 21/08/2026 :

| Nutriment       | Valeur en vigueur            | Statut                    | Remplace                    |
| --------------- | ---------------------------- | ------------------------- | --------------------------- |
| Vitamine B6     | 12 mg/j (2023)               | UL                        | 25 mg/j (SCF, 2000)         |
| Sélénium        | 255 µg/j (2023)              | UL                        | 300 µg/j (SCF, 2000)        |
| Fer             | 40 mg/j (2024)               | **safe level**, aucune UL | 45 mg/j — valeur américaine |
| Manganèse       | 8 mg/j (2023)                | **safe level**            | —                           |
| DHA supplémenté | 1 g/j (adopté le 15/12/2025) | **safe level**            | —                           |
| Vitamine C      | aucune                       | non dérivable             | —                           |

Un produit qui aurait figé la vitamine B6 à 25 mg laisserait passer **sans rien dire** un apport de 20 mg, soit 167 % de la limite en vigueur.

**Deux pièges que la valeur seule ne porte pas.** La **forme chimique** change la limite : la niacine vaut 10 mg en acide nicotinique et **900 mg en nicotinamide**, un facteur 90 sur la même ligne d'étiquette. La **source** change le périmètre : l'UL du magnésium ne vaut que pour les sels solubles des compléments.

**Ce que le domaine en fait.** `Palier.Domain` ne connaît **aucune** valeur de limite haute. Il reçoit une référence portant sa valeur, son statut, sa source et sa date, et il compare. Une épreuve du harnais — `back/tests-harness/references-nutriments.test.mjs` — refuse qu'une constante de référence soit écrite dans le domaine.

**Ce que cela ouvre :** deux colonnes dans `nutrient_refs`, pour la forme chimique et le périmètre. Changement de schéma, donc décision du porteur du projet.

**Ce qui la rouvrirait :** rien. Une valeur de référence qui bouge n'a pas sa place dans du code compilé.

---

## D52 — Le volume par groupe musculaire a une cible, celle de l'ACSM 2026

**Tranché le :** 21/08/2026, pendant le lot 3.

**Ce qui manquait.** `05-entrainement.md` calcule un volume hebdomadaire par groupe musculaire **sans jamais dire à quoi le comparer**. Le trou était réel et personne ne l'avait signalé.

**Ce qui le comble.** Le _Position Stand_ de l'ACSM sur l'entraînement en résistance, publié en 2026 — première mise à jour depuis dix-sept ans, bâtie sur **137 revues systématiques et plus de 30 000 participants**. Trois de ses conclusions concernent ce produit :

- environ **dix séries hebdomadaires par groupe musculaire** pour l'hypertrophie, avec une relation dose-réponse au-delà, et **tous les groupes majeurs entraînés au moins deux fois par semaine** ;
- de **30 à 100 % du 1RM**, les gains sont équivalents à volume égalisé, dès lors que chaque série est menée proche de l'échec ;
- s'entraîner à **deux ou trois répétitions en réserve produit les mêmes gains** de masse et de force que l'échec absolu, **avec moins de fatigue accumulée et un risque de blessure moindre**.

Ce dernier point valide la promesse du produit : la règle de progression de `05-entrainement.md` § 2, qui s'appuie sur un RIR cible, n'est pas une prudence commerciale mais la recommandation de référence.

**Le ratio tirage/poussée de 1,3 est conservé mais déclassé.** La littérature ne soutient aucune valeur précise — les recommandations vont de 1:1 à 3:1, et la pertinence même d'un ratio fixe est débattue. Il devient un repère d'équilibre, jamais un seuil de santé, et le produit ne dira pas qu'un ratio inférieur expose à une blessure.

**Ces trois références arrivent en paramètre**, comme les limites hautes.

**Ce qui la rouvrirait :** la prochaine mise à jour du _Position Stand_.

---

## D53 — Le seuil de fiabilité du 1RM était juste, et la littérature le confirme

**Tranché le :** 21/08/2026, pendant le lot 3. **Cette entrée corrige un signalement erroné de ma part.**

`05-entrainement.md` § 3 signale une marge d'erreur au-delà de 12 répétitions effectives et refuse tout affichage au-delà de 15. J'avais d'abord signalé ce seuil comme plus permissif que la littérature, sur la foi d'une formulation générale — « les équations sont plus précises sous dix répétitions ».

**La vérification donne raison au document.** L'erreur de prédiction reste sous 0,03 entre 3 et 8 répétitions, l'exactitude est maximale au 5RM, elle se dégrade au-delà de 10, et « beyond 12 reps, prediction error increases significantly » — précisément le seuil que porte le document.

**Ce que le domaine en fait.** Trois paliers de fiabilité — bonne jusqu'à 8 répétitions effectives, moyenne jusqu'à 12, faible jusqu'à 15 — et **rien du tout au-delà**. `ForceEstimee.Epley` rend `null` plutôt qu'une charge : un nombre rendu quand même serait indiscernable d'une estimation valide.

**Ce qui la rouvrirait :** rien de connu.

---

## D54 — Un quatrième rôle PostgreSQL porte le chemin d'authentification

**Tranché le :** 21/08/2026, au démarrage du lot 4. **Choisi par le porteur du projet parmi trois options.**

D38 avait fermé les six tables `AspNet*` en refus par défaut — `enable` + `force row level security`, aucune politique — en laissant à concevoir le chemin qui les atteindrait. Le lot 4 a buté sur ce mur : l'authentification lit `AspNetUsers` **avant** qu'une identité existe, or `ExecuteurDeCasDUsage` refuse sans identité, `ArchitectureTests` interdit tout autre accès au contexte, et D38 a fermé les tables.

**Le rôle `palier_auth`** est le seul chemin. Il ne possède rien, ne contourne pas RLS, et n'a **aucun privilège** sur `workouts`, `sets` ni `body_weight` — trois épreuves de `RolesTests` l'exigent en code 42501. Le contexte `PalierAuthDbContext` ne déclare que les tables d'identité et les sessions : ce qui n'est pas déclaré n'est pas atteignable, et la barrière du moteur n'est plus la seule.

**Les deux voies écartées, et pourquoi.**

- **Fonctions `SECURITY DEFINER`** — elles auraient obligé à réécrire cinq interfaces du magasin Identity, et chacune aurait dû porter `SET search_path` sous peine de rouvrir CVE-2018-1058. Beaucoup de surface pour une barrière que le rôle donne gratuitement.
- **Un drapeau de contexte applicatif** — la barrière aurait dépendu d'une variable posée par l'application. Une barrière qu'un défaut applicatif peut lever n'est pas une barrière.

**Ce qui la rouvrirait :** un magasin Identity qui n'irait plus en base — improbable — ou un besoin de lire les tables d'identité depuis le chemin des cas d'usage, qui serait d'abord un défaut de conception à corriger.

---

## D55 — Le hachage passe à 210 000 itérations, et la mesure est écrite

**Tranché le :** 21/08/2026, pendant le lot 4. `docs/09-comptes.md` § 1 laissait l'arbitrage ouvert : « conserver le défaut ou relever le nombre d'itérations reste à arbitrer, et l'arbitrage **se mesure**, il ne se devine pas ».

**L'algorithme réel, vérifié en décodant le format du haché octet par octet :** PBKDF2 **HMAC-SHA512**, sel de 128 bits, sous-clé de 256 bits, **100 000 itérations** par défaut. La documentation Microsoft annonce tantôt SHA-256, tantôt 10 000 — aucune des deux n'est ce que le code fait.

**La mesure**, le 21/08/2026 sur seize cœurs : **56,7 ms** à 100 000 itérations contre **111,2 ms** à 210 000. Soit **54 ms de plus par connexion**. Sur un vCPU mutualisé, compter le double.

**210 000** est ce qu'OWASP recommande pour PBKDF2-HMAC-SHA512 — la fonction que cette version emploie réellement, pas celle que la documentation annonce.

**Ce que la mesure a changé dans le code :** l'ordre des contrôles. La limitation par adresse vient **avant** le hachage, sans quoi 111 ms deviennent un levier d'épuisement de ressources.

**Ce qui la rouvrirait :** une recommandation OWASP révisée, ou une mesure sur l'instance OVHcloud réelle qui montrerait un coût par connexion incompatible avec la charge attendue. Le marqueur de version en tête du haché rend le changement sûr dans les deux sens.

---

## D56 — La fenêtre de grâce de rotation vaut trente secondes

**Tranché le :** 21/08/2026, pendant le lot 4.

RFC 9700 (janvier 2025) impose de révoquer la famille entière au réemploi d'un jeton de rafraîchissement : deux porteurs détiennent la même chaîne, lequel est le voleur est indécidable. Appliquée sans nuance, la règle **déconnecte les utilisateurs légitimes** — deux onglets qui se rafraîchissent à la même seconde présentent aussi la même chaîne.

**Trente secondes**, le défaut d'Okta, réglable de 0 à 60 chez lui. Assez court pour qu'un jeton volé ne serve pas : un attaquant qui rejoue à la seconde près est dans la course, pas dans une exploitation. Assez long pour couvrir un aller-retour réseau dégradé, un second onglet, une reprise de connexion.

**Ce que le rejeu dans la grâce fait exactement :** il rend un jeton **neuf de la même famille**, et ne consomme rien de plus. Le successeur porte son empreinte et non son jeton — il ne peut pas être reconstruit.

**Ce qui la rouvrirait :** une mesure de déconnexions intempestives en exploitation, qui la ferait monter ; ou un incident de vol de jeton exploité dans la fenêtre, qui la ferait descendre. Aucune des deux ne se devine : `ParametresDeSession.FenetreDeGrace` est à un seul endroit, et `DecisionDeRotation` est couverte à 100 %.

---

## D57 — Les seuils de couverture des adaptateurs sont posés au niveau atteint

**Tranché le :** 21/08/2026, fin du lot 4.

| Projet                                 | Seuil                                               | Ce qu'il détecte                                                         |
| -------------------------------------- | --------------------------------------------------- | ------------------------------------------------------------------------ |
| `Palier.Domain`                        | **100 %** ligne, branche, méthode                   | le code mort **public** — un membre que rien n'appelle n'est pas couvert |
| `Palier.Application`                   | **100 %** ligne, branche, méthode                   | idem                                                                     |
| `Palier.Infrastructure` + `Palier.Api` | **96 / 79 / 71 %** (mesuré : 96,63 / 79,60 / 71,55) | un **cliquet**, pas un détecteur                                         |

**Un seuil sous la couverture réelle laisse celle-ci redescendre sans rien dire** — c'est un contrôle qui approuve. Les valeurs sont donc les entiers immédiatement inférieurs aux mesures, et rien de plus bas.

**Ce que le seuil des adaptateurs ne fait PAS, et qu'il faut savoir :** à 71 % de méthodes, la détection de code mort public **n'existe pas** sur ces deux projets. Un membre public inutilisé y passe. Roslyn ne peut pas le signaler — par construction, il pourrait être appelé depuis l'extérieur de l'assemblage — et seule la couverture le ferait.

Le chiffre des méthodes est plombé par le code **généré** : migrations EF, `Designer.cs`, `ModelSnapshot`. Les exclure demanderait une décision datée avec son échéance (`CLAUDE.md` § 4) ; elle n'a **pas** été prise, et le seuil vit donc avec eux.

**Ce qui la rouvrirait :** chaque montée réelle de la couverture doit relever le seuil dans le même geste — sans quoi le cliquet ne cliquette pas. Et une décision d'exclure le code généré, si le chiffre des méthodes devient un obstacle plutôt qu'une mesure.

---

## D58 — Les six défauts de l'audit de sécurité sont corrigés, et trois d'entre eux ont révélé pire qu'eux-mêmes

**Tranché le :** 22/08/2026, après l'audit multi-agents de la branche `feat/lot-4-socle-session`.

Un scan de sécurité a rendu 14 pistes vérifiées sur `back` et `db` — 82 candidats, 31 après déduplication, 93 votes d'un panel à trois voix. Les 14 pistes sont **six défauts** : plusieurs chercheurs avaient trouvé le même problème par des chemins différents.

**Ce qui est corrigé, et ce que la correction a coûté d'apprendre :**

| Défaut                                                   | Correction                                                                   | Ce que la relecture adversariale a trouvé EN PLUS                                                                                                 |
| -------------------------------------------------------- | ---------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------- |
| `/2fa/preparer` réenrôlait le second facteur sans preuve | Code d'authentificateur **ou de récupération** exigé quand la 2FA est active | Un paramètre de corps non nullable cassait le **premier enrôlement** ; une garde limitée à l'authentificateur enfermait qui a perdu son téléphone |
| Codes de récupération en clair en base                   | Hachés par PBKDF2 à 210 000 itérations                                       | Le premier jet **laissait la ligne en clair** à côté, indéfiniment, et invalidait silencieusement les codes existants                             |
| Compteur d'échecs non atomique                           | `select … for update` dans une transaction, pour les deux écritures          | Passer à `ExecuteUpdateAsync` **retirait le jeton de concurrence** : une route 2FA sans mot de passe effaçait alors un verrouillage               |
| La grâce fourchait la famille de jetons                  | Successeur scellé sous une clé dérivée du jeton présenté                     | Le sceau vivait **quatorze jours** pour un usage de trente secondes, et les sceaux s'enchaînaient jusqu'au jeton vivant                           |
| `/api/v1/sante` anonyme                                  | `RequireAuthorization()` — D41 l'exigeait depuis le lot 4                    | Le rôle d'administration que D41 demande aussi n'existe pas encore dans le produit                                                                |
| Le temps des refus trahissait l'existence d'un compte    | Budget constant de 400 ms sur toutes les branches de refus                   | L'épreuve dérivait sa tolérance du budget qu'elle éprouvait, et l'écart naturel (5 ms) était déjà sous toute tolérance stable                     |

**La leçon qui vaut au-delà de ces six.** Dans trois cas sur six, **la correction introduisait un défaut plus grave que celui qu'elle fermait**, et aucun n'aurait été vu par relecture : il a fallu une sonde qui mesure. Un correctif de sécurité n'est pas terminé quand il ferme la faille nommée — il l'est quand on a demandé ce qu'il **arrête** de faire, et pas seulement ce qu'il commence.

**Ce qui reste ouvert, et qui appartient au porteur du projet :**

- **Le secret TOTP reste en clair.** Il est relu à chaque vérification, donc il se chiffre — il ne se hache pas — et cela demande une décision de gestion de clé : où elle vit, comment elle tourne, ce qui se passe au redéploiement. Aucun trousseau n'a été fabriqué pour faire semblant.
- **Le rôle d'administration de D41.** `PolitiquesDAutorisation` ne porte que deux politiques, qui sont des **domaines**, pas des rôles.
- **Le budget de 400 ms** est calé sur une machine de développement. À revoir sur l'instance OVHcloud, où le PBKDF2 sera plus lent.

**Ce qui la rouvrirait :** un audit ultérieur, que ces corrections n'exemptent de rien — un scan est non déterministe, et le relancer construit la couverture dans le temps.

---

## D59 — Le coffre des secrets est OVHcloud KMS, et non Azure Key Vault

**Tranché le :** 22/08/2026. **Choisi par le porteur du projet**, qui avait d'abord nommé Azure Key Vault et a retenu OVHcloud après que la contradiction lui eut été signalée.

D58 laissait ouverte la seule chose qui manquait au chiffrement du secret TOTP : où vit la clé. La demande initiale était Azure Key Vault. **Elle contredisait D15**, et pas à la marge : D15 pose OVHcloud parce qu'un fournisseur européen est hors portée du CLOUD Act, ce qui compte pour des données de santé relevant de l'article 9. Confier à Microsoft la clé qui ouvre ces données aurait laissé la donnée en Europe et fait basculer la clé sous une autre juridiction — un déplacement qui vide D15 de son sens sans jamais déplacer un octet.

**Ce qui a été mesuré, et non supposé.** Une sonde jetable exécutée le 22/08 contre le domaine réel a établi que l'URL du jeton OAuth2 est `https://www.ovh.com/auth/oauth2/token` — aucune des trois adresses en `ovhcloud.com` ne répond autre chose qu'une page marketing en `404` —, que `datakey` rend une clé de 32 octets et une enveloppe au format JWE compact portant `x-key-ver`, et que `datakey/decrypt` coûte **30 ms de médiane** (27 min, 33 max). Le coffre injoignable échoue en 91 ms.

**Ce que `x-key-ver` a changé au design.** OVH versionne la clé maîtresse et fait voyager le numéro _dans_ l'enveloppe. La rotation de la clé maîtresse ne demande donc aucun code : le format la porte. Seule la rotation de la clé de données restait à concevoir, et elle l'est.

**Ce que la politique IAM accorde :** sept actions sur cinquante-neuf, une ressource, une identité. Aucune suppression, aucune modification, aucune signature — un compte de service volé permet de lire et d'écrire, jamais de rendre les données illisibles.

**Les voies écartées.**

- **Azure Key Vault, AWS KMS, Google Cloud KMS** — trois fournisseurs sous CLOUD Act. Écartés par D15, pas par leurs mérites techniques.
- **Un secret simple pour la clé du TOTP, sans enveloppe** — plus simple, et strictement plus faible : une seule compromission suffirait. L'enveloppe stockée en base exige **deux compromissions indépendantes**, le serveur _et_ la base.
- **Déchiffrer au coffre à chaque vérification TOTP** — 30 ms le permettraient. C'est la disponibilité qui l'interdit : chaque connexion à deux facteurs dépendrait alors de la joignabilité d'un service tiers.

**Ce qui la rouvrirait :** un modèle de menace où le vidage mémoire du processus devient crédible — l'alternative serait alors l'appel par vérification, dont le coût est mesuré. Ou une rupture de service OKMS répétée, qui remettrait en cause le refus de démarrer.

**Conception détaillée :** `docs/superpowers/specs/2026-08-22-coffre-des-secrets-design.md`.

---

## D60 — MailKit porte l'envoi d'emails, et le fournisseur reste interchangeable

**Tranché le :** 23/08/2026, au lot 4b. **Le porteur du projet a choisi le SMTP générique** parmi trois options, puis a précisé que le fournisseur serait OVHcloud.

L'exigence 2 de `docs/09-comptes.md` § 1 — « pas de nutrition sans email vérifié » — était livrée à moitié depuis le lot 4 : la règle existait, l'**envoi** non. `IEmailSender<TUser>` n'avait que son défaut, `NoOpEmailSender`, qui « ne fait rien » et existe pour qu'on remarque qu'on ne l'a pas remplacé.

**Pourquoi SMTP plutôt qu'une API de fournisseur.** Brevo et Postmark exposent des API HTTP, ce qui aurait évité toute dépendance nouvelle — le produit sait déjà parler HTTP, le coffre en est la preuve. Mais le code aurait alors **connu son fournisseur** : en changer aurait demandé de réécrire l'adaptateur. En SMTP, l'hôte, le port, les identifiants et l'expéditeur sont de la configuration ; passer d'OVHcloud à un autre ne touche pas une ligne.

**Pourquoi MailKit et non `System.Net.Mail`.** `SmtpClient` est explicitement déconseillé par Microsoft pour du code neuf — sa propre documentation renvoie à MailKit. Ce n'est pas une préférence de style : `SmtpClient` ne gère correctement ni STARTTLS moderne, ni l'authentification OAuth2, ni les jeux de caractères des en-têtes.

**Ce qui a été vérifié à la source le 23/08/2026**, et non supposé : version **4.17.0**, publiée le **26/05/2026**, licence **MIT**. L'âge de la version satisfait le délai d'adoption de la doctrine des dépendances — un paquet compromis est généralement retiré en quelques heures, et trois mois valent mieux que trente jours.

**Un piège rencontré en la vérifiant, qui vaut d'être écrit.** `dotnet package search MailKit --exact-match` a rendu **`1.10.0`** — une version qui n'a rien à voir avec la réalité. C'est l'index NuGet interrogé directement qui a donné la bonne. Une version lue dans la sortie de `dotnet package search` ne prouve rien.

**Les voies écartées.**

- **Une API de fournisseur (Brevo, Postmark)** — aucune dépendance nouvelle, mais le code connaît son fournisseur. Écartée pour cette raison seule.
- **`System.Net.Mail.SmtpClient`** — aucune dépendance non plus, et déconseillée par son propre éditeur.
- **Aucun envoi, et une vérification d'adresse par un autre canal** — il n'y en a pas d'autre.

**Ce qui la rouvrirait :** un besoin de suivi de délivrabilité — ouvertures, rebonds, plaintes — que SMTP ne rapporte pas et qu'une API de fournisseur expose. Ce jour-là, l'adaptateur change et le reste du produit ne bouge pas : c'est précisément ce que le port `IEmailSender<Utilisateur>` garantit.

---

## D61 — La détection de perte rapide lit des MOYENNES hebdomadaires, pas des pesées

**Tranché le :** 24/08/2026, au lot 5. **Pris en autonomie**, le porteur du projet ayant demandé que ce lot soit exécuté pendant son sommeil. **À relire** : la décision touche un garde-fou de santé.

`PerteDePoidsRapide.EstDetectee` existait depuis le lot 3, éprouvée et couverte à 100 %. En la branchant sur l'API, une chose est apparue qu'aucune de ses épreuves ne pouvait montrer : **elle suppose que ses entrées sont hebdomadaires**. Elle compare des valeurs consécutives et lit chaque écart comme « une semaine ». Lui passer la série brute de `body_weight`, où l'utilisateur pèse quand il veut — donc souvent tous les jours — lui aurait fait mesurer des JOURS en croyant mesurer des semaines.

**L'effet n'est pas un faux positif, c'est un SILENCE.** La variation d'un jour à l'autre reste sous le seuil de 1 %, donc la boucle sort à la première comparaison et rend `false`. La détection serait restée muette exactement chez les utilisateurs les plus assidus — ceux qui pèsent tous les jours parce qu'ils surveillent leur poids de près, c'est-à-dire la population que `docs/01-conformite.md` § 5 nomme comme celle qu'il faut protéger. Aucune épreuve n'aurait rougi : le domaine faisait correctement ce qu'on lui demandait, sur des entrées qui ne voulaient pas dire ce qu'il croyait.

**Ce qui a été retenu : la MOYENNE de la semaine, et non sa dernière pesée.** Le poids corporel varie de 1 à 2 % d'un jour à l'autre — eau, glycogène, contenu digestif — soit **plus que le seuil lui-même**. Un échantillon hebdomadaire unique ferait donc du seuil un générateur de faux positifs : quatre creux successifs suffiraient à déclencher l'alerte chez quelqu'un dont le poids ne bouge pas. Une épreuve construit exactement ce cas et vérifie que le constat ne tombe pas.

Et un garde-fou qui crie sans motif est un garde-fou qu'on finit par ignorer, puis par désactiver. Sur ce sujet-là, le coût d'une alerte de trop n'est pas nul : il use la seule alerte qui compte.

**La semaine est la semaine ISO**, par `System.Globalization.ISOWeek`. Elle ne dépend ni de la culture du serveur ni du fuseau de l'utilisateur, contrairement au premier jour de semaine du calendrier, qui change de pays en pays. Une épreuve garde le passage du 31 décembre au 1er janvier, où un regroupement par année civile aurait scindé une même semaine en deux et inventé une comparaison hebdomadaire d'un jour.

**Une semaine incomplète compte quand même** — la moyenne d'une pesée vaut cette pesée. Refuser les semaines partielles retarderait la détection de sept jours au pire moment : celui où quelqu'un vient de commencer à perdre vite.

**Où vit le calcul.** Dans `Palier.Domain`, avec le seuil et la fenêtre qu'il sert. Le mettre dans le gestionnaire aurait placé une règle de sécurité hors du seul projet tenu à 100 % de couverture, et hors de portée des épreuves qui la gardent.

**Les voies écartées.**

- **Passer la série brute au domaine** — c'est l'état par défaut, celui qu'on obtient en ne se posant pas la question. Silencieux chez les utilisateurs assidus.
- **La dernière pesée de chaque semaine** — plus simple à écrire et à expliquer, mais elle échantillonne un signal plus bruyant que le seuil qu'on lui applique.
- **La moyenne mobile sur sept jours** — techniquement supérieure au découpage par semaines calendaires, qui traite mal une série commençant un jeudi. Écartée pour aujourd'hui : `01-conformite.md` § 5 parle de semaines, et une moyenne mobile changerait le sens de la règle en même temps que son calcul. C'est un raffinement, pas une correction.
- **Exiger une pesée hebdomadaire du produit** — une contrainte d'interface pour éviter un calcul de dix lignes, et qui rendrait la détection dépendante d'un comportement qu'on ne contrôle pas.

**Ce qui la rouvrirait :** un avis clinique — le kinésithérapeute que `docs/14-contenu.md` prévoit — sur la bonne façon de lire une série de poids. C'est le genre de question où une source médicale prime sur un raisonnement de conception, et cette décision est prise faute de l'avoir demandée.

---

## D62 — Les quatre arbitrages du lot 5

**Tranchés le :** 24/08/2026. **Pris en autonomie**, le porteur du projet ayant demandé que le lot 5 soit exécuté pendant son sommeil. Chacun porte ce qui le défait ; deux d'entre eux ne sont pas des choix.

### La pagination va au CURSEUR, et le curseur est un couple

`GET /api/v1/seances?avant={instant}&avantId={guid}&limite={n}`.

**Le décalage aurait été faux.** Une liste triée par date décroissante reçoit ses insertions **en tête** : entre le moment où le client lit la page 1 et celui où il demande la page 2, une séance ouverte décale tout d'un rang, et un `OFFSET 20` saute la vingtième — définitivement, sans erreur et sans trou visible dans la réponse.

**Le couple, parce que l'instant seul ne suffit pas.** Deux séances peuvent porter le même `started_at` — un import, deux entraînements notés à la minute près — et un `<` strict en aurait sauté une pour toujours. `(started_at, id)` est un ordre total.

`EF.Functions.LessThan` sur un tuple traduit en comparaison de **row values** PostgreSQL, `(started_at, id) < (@a, @b)`, qui se sert de l'index `workouts (owner_id, started_at desc)` posé par D39 — là où un `OR` force souvent un parcours. Trouvé par Context7, pas de mémoire.

**Ce qui la défait :** un besoin de sauter à la page N, qu'aucun écran ne demande.

### Les deux listes sont FERMÉES, et le document les donne

`docs/05-entrainement.md` nomme les quatre contraintes au § 4 — cervicale, lombaire, épaule, genou — et les trois états de ressenti au § 5 — `good`, `meh`, `pain`. **Il n'y avait rien à trancher** : la liste est fermée par le document métier.

**Le texte libre aurait été un piège.** Il ne se traduit pas, il ne se compare pas — « épaule », « epaule », « Épaule droite » sont trois valeurs pour une machine — et il finirait dans un prompt de modèle, ce que `docs/01-conformite.md` encadre strictement.

La forme stockée est en **minuscules sans accent**, des deux côtés : `user_constraints.region` et `exercises.contraindicated_for` se comparent, et une collation qui traiterait « e » et « é » différemment selon l'environnement rendrait le filtrage dépendant de la configuration du serveur. Le libellé accentué vit dans i18next.

**Ce qui la défait :** une cinquième région réclamée par un utilisateur. Elle s'ajoute alors à l'énumération, à la contrainte `CHECK` et aux libellés — pas par le corps d'une requête.

### Le ratio tirage/poussée est REPORTÉ

`docs/05-entrainement.md` § 4 le définit, `VolumeParGroupe.SurSeptJours` sait le calculer depuis le lot 3, et `SerieEffectuee` attend un `RoleMouvement`.

**Ce rôle n'existe nulle part en base.** `exercises` porte `primary_muscles` et `secondary_muscles` ; aucune colonne ne dit si un mouvement tire ou pousse. Le déduire des muscles serait faux — un pull-over travaille les pectoraux **et** le grand dorsal, un rowing inversé et un développé partagent le deltoïde — et **un ratio faux vaut moins que pas de ratio** : il ferait modifier un programme sur une mesure inventée.

La colonne se pose au lot qui **remplit** le catalogue — étape 1 bis, 250 à 400 exercices — où le rôle se renseigne exercice par exercice, avec le reste. L'ajouter maintenant créerait une colonne vide sur un catalogue vide : le « garde-fou sans cible » que D39 refuse.

**Ce qui la défait :** rien, jusqu'au catalogue. Le volume par muscle, lui, est livré.

### Le filtrage du catalogue par les contraintes N'EST PAS TRANCHÉ

C'est le seul point que ce lot laisse **délibérément ouvert**, et il appartient au porteur.

Le § 4 décrit un filtrage automatique par intersection. Il laisse ouvert ce qu'on fait d'un exercice contre-indiqué : **l'exclure** du catalogue, ou **l'afficher marqué**. Les deux se défendent — exclure protège, marquer informe — et le choix se voit par l'utilisateur, donc `CLAUDE.md` § 6 s'applique.

**Le lot 5 stocke et expose.** L'épreuve `Declarer_une_contrainte_ne_FILTRE_PAS_encore_le_catalogue` rougira le jour où le filtrage arrivera, avec un message qui explique pourquoi. Sans elle, le filtrage se serait installé au détour d'une implémentation, dans la forme que quelqu'un aurait trouvée évidente, et **personne n'aurait jamais posé la question**.

### Et D39, considérée comme prise

Elle portait « arbitrage à confirmer par le porteur du projet ». Trois lots s'appuient dessus, et la défaire coûterait treize tables et treize épreuves RLS que rien n'exercerait. Le lot 5 l'a appliquée à la lettre : **deux** tables nouvelles, chacune avec sa politique, ses trois rôles et son épreuve d'isolation.

**Ce qui la défait :** un mot du porteur. Le coût de revenir en arrière augmente à chaque lot ; il est aujourd'hui plus élevé qu'hier.
