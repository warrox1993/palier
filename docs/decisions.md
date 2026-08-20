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

**À traiter au lot 2 :** `Mediator.SourceGenerator`, retenu par D12, ne publie **pas** d'expression SPDX — le contrôle le refuse aujourd'hui, pour cette raison et non pour sa licence, qui est MIT. Il devra entrer dans `EXCEPTIONS` le jour où CQRS sera implémenté, sans quoi `verify` cassera.

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
**Ce qui la rouvrirait :** le porteur du projet, à l'arbitrage. Trois leviers sont chiffrés et aucun n'a été appliqué d'office : sortir les épreuves du harnais de `front:test` (−39 s, mais `src/` ne contient aucun test aujourd'hui, donc l'étape deviendrait vide et verte sans rien contrôler) ; retirer `--no-incremental` de `back:build` ; mettre en cache les réponses des registres npm et NuGet du contrôle de licences.

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
