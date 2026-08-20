# Journal des décisions

**Ce fichier se lit au démarrage de chaque session, pas à la fin.**

Une décision qu'on ne relit pas au démarrage se reprend : le débat recommence trois semaines plus tard sans que personne se souvienne pourquoi il avait été clos. Chaque entrée porte **ce qui la rouvrirait** — c'est la seule information qui permet de savoir quand y revenir légitimement.

---

## D1 — Nom du projet : `palier`

**Tranché le :** 19/08/2026
**Motif :** recommandation de `15-marque.md` § 3 — cohérent avec le système de progression, positif, prononçable.
**Ce qui la rouvrirait :** une antériorité trouvée aux registres BOIP ou EUIPO, ou une priorité donnée à l'international (le document note que le nom est « très francophone »).
**Reste à faire :** la recherche d'antériorité, que `15-marque.md` § 4 demande *avant* de s'attacher au nom.

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
**Bénéfice décisif conservé dans les deux cas :** `Palier.Domain` ne référence aucun autre projet. Un calcul nutritionnel ne *peut pas* atteindre la base ou le réseau — l'impossibilité est vérifiée par le compilateur, non par une règle de style. `01-conformite.md` § 3 en dépend directement.
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
