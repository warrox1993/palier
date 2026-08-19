# Brief — Application muscu & nutrition

Dossier de spécification destiné à Claude Code.

## Démarrage

**`DEMARRAGE.md`** — procédure exacte, étape par étape, avec les commandes et le premier prompt.

## Ordre de lecture

1. **`CLAUDE.md`** — règles de travail. Superpowers obligatoire, aucun agent créé, agentique et non vibecoding
2. `docs/00-produit.md` — cible, promesse, périmètre
3. `docs/01-conformite.md` — cadre juridique, ligne informer/prescrire, garde-fous. **Prime sur tout le reste**
4. `docs/02-design.md` — direction artistique, jetons, écran de séance
5. `docs/03-donnees.md` — schéma PostgreSQL et RLS
6. `docs/04-nutrition.md` — formules et références
7. `docs/05-entrainement.md` — volume, progression, contraintes
8. `docs/06-ia.md` — architecture LLM multi-fournisseur
9. `docs/07-roadmap.md` — séquence
10. `docs/08-workflow.md` — ingénierie agentique, harnais, définition de terminé
11. `docs/09-comptes.md` — auth, onboarding, abonnement, notifications, support, admin
12. `docs/10-progression.md` — radar, états, avatar
13. `docs/11-qualite.md` — résilience, accessibilité, i18n, mesure
14. `docs/12-confort.md` — duplication, import/export, performance, sécurité
15. `docs/13-juridique.md` — mineurs, transferts de données, structure, assurance
16. `docs/14-contenu.md` — catalogue d'exercices, programmes, exploitation
17. `docs/15-marque.md` — nom et dépôt de marque
18. `docs/16-projet.md` — arborescence, conventions, variables d'environnement, glossaire
19. `docs/17-donnees-sources.md` — licences des bases alimentaires, attribution, share-alike

## Premier prompt

> Lis `CLAUDE.md` puis l'ensemble de `docs/`. Vérifie que Superpowers est installé et actif.
> Ne code rien pour l'instant. Lance `/superpowers:brainstorm` sur l'étape 1 de la roadmap et
> pose-moi les questions nécessaires avant de proposer un plan.

## Les quatre règles à ne jamais contourner

1. **Informer, jamais prescrire.** Un chiffre, une référence, un écart. Aucune action recommandée
2. **Le LLM ne calcule pas.** Il reçoit des valeurs déjà produites par du code testé
3. **Brainstorm, plan validé, puis exécution.** Jamais de code avant accord sur un plan
4. **Produit fini, jamais prototype.** Les dix points de la définition de terminé sont cochés avant de passer à la suite
