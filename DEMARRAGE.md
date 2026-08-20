# Démarrage — procédure exacte

À suivre dans l'ordre. Chaque étape a un critère de réussite vérifiable.

---

## Étape 0 — Prérequis

> **Ce document a été mis à jour le 20/08/2026** pour appliquer les décisions D3, D9, D10, D15 et D17 de `docs/decisions.md`. Il décrivait une pile Supabase + Vercel abandonnée le 19/08.

```bash
node --version    # 24 — voir .nvmrc, et D3
dotnet --version  # 10 ou plus
git --version
```

Comptes à créer :
- **OVHcloud** — PostgreSQL managé et une instance pour le backend conteneurisé. **Région européenne** : c'est l'argument de conformité principal pour des données de santé (D15)
- **GitHub** — dépôt privé

**Clés API Anthropic et Google : à créer maintenant.** L'étape 1 de la roadmap livre l'abstraction `LLMProvider` avec un appel de test sur chaque fournisseur — sans clés, ce livrable est inatteignable.

Le coût est négligeable : quelques appels de test valent des centimes. Fixe un plafond de dépense sur chaque console dès la création, et ne mets jamais ces clés côté client (voir `docs/16-projet.md`).

---

## Étape 1 — Le dépôt

```bash
mkdir mon-app && cd mon-app
git init
# décompresser le brief à la racine :
# CLAUDE.md, README.md, DEMARRAGE.md et docs/ doivent être ici
ls              # doit montrer CLAUDE.md, README.md, docs/
git add . && git commit -m "ajoute le dossier de spécification"
```

**Critère :** `cat CLAUDE.md | head -5` affiche le fichier.

---

## Étape 2 — Claude Code et les plugins

```bash
npm install -g @anthropic-ai/claude-code
cd mon-app
claude
```

Dans la session :

```
/plugin marketplace add obra/superpowers-marketplace
/plugin install superpowers@superpowers-marketplace
```

Puis les plugins officiels, depuis la marketplace `claude-plugins-official` déjà enregistrée :

```
/plugin install code-review
/plugin install security-guidance
/plugin install pr-review-toolkit
/plugin install commit-commands
```

**Critère :** `/help` liste trois commandes correspondant à *clarifier*, *planifier* et *exécuter*. **Leur nom exact dépend de la version installée** — la version courante les expose sous `brainstorming`, `writing-plans` et `executing-plans`, une version antérieure utilisait `/superpowers:brainstorm`, `write-plan`, `execute-plan`.

Ce qui compte est la présence des trois fonctions, pas le libellé. Si rien n'apparaît, redémarrer la session avant d'aller plus loin.

---

## Étape 3 — Permissions

Créer `.claude/settings.json` :

```json
{
  "permissions": {
    "deny": ["EnterPlanMode"],
    "allow": [
      "Bash(npm run *)",
      "Bash(git *)",
      "Bash(dotnet *)"
    ]
  }
}
```

`EnterPlanMode` doit être refusé : le mode Plan natif entre en conflit avec les workflows Superpowers.

**Critère :** le fichier existe et la session redémarre sans erreur.

---

## Étape 4 — Serveurs MCP

À ajouter au fur et à mesure, pas tous d'un coup. Pour démarrer, deux suffisent :

```
/mcp add github
/mcp add context7
```

Context7 est utile dès le premier jour : sur une bibliothèque, il donne la documentation de la version installée là où le web donne celle d'il y a deux ans. Playwright et Sentry viendront quand le besoin apparaîtra. Un serveur MCP inutilisé consomme du contexte à chaque session.

**Règle absolue : jamais d'accès en écriture sur la base de production.**

---

## Étape 5 — Le premier prompt

Copier tel quel :

> Lis `CLAUDE.md`, puis l'intégralité de `docs/` dans l'ordre numérique. Prends le temps qu'il faut.
>
> Ensuite, vérifie que Superpowers est actif via `/help`.
>
> **N'écris aucune ligne de code pour l'instant.**
>
> Quand tu as tout lu, réponds-moi avec :
> 1. Ta compréhension du produit en cinq lignes maximum
> 2. Les trois contraintes que tu considères comme non négociables
> 3. Les points du dossier qui te paraissent ambigus ou contradictoires
> 4. Ce qui te manque pour démarrer l'étape 1 de la roadmap
>
> Puis attends ma réponse avant toute action.

**Critère de réussite :** il cite la règle informer/prescrire, la séparation calcul/LLM, et la définition de terminé. S'il ne les cite pas, il n'a pas lu — relance-le sur `01-conformite.md` et `08-workflow.md`.

---

## Étape 6 — Le premier lot de travail

Une fois ses questions traitées :

> Lance la commande de clarification de Superpowers (`brainstorming` ou son équivalent selon ta version) sur l'étape 1 de `docs/07-roadmap.md` — le harnais et le socle.
>
> Rappel : le harnais avant le produit. Rien ne s'écrit avant que TypeScript strict, **Oxlint**, Prettier, Vitest, Playwright, les hooks pre-commit et pre-push, la CI et axe-core ne soient en place et vérifiés. Oxlint et non ESLint : `typescript-eslint` est incompatible avec TypeScript 7.

Puis, après le brainstorm :

> Écris le plan avec la commande de planification (`writing-plans` ou équivalent). Je le relis avant toute exécution.

---

## Ton rôle pendant les sessions

C'est la partie que la plupart des gens ratent. L'agent est bon dans la mesure où tu tiens ton bout.

| Fais | Ne fais pas |
|---|---|
| Lire chaque plan avant de valider | Répondre « ok continue » sans lire |
| Exiger le contrôle exécutable de chaque tâche | Accepter « ça devrait marcher » |
| Redémarrer une session qui patine | La prolonger en espérant que ça passe |
| Faire tourner l'app toi-même après chaque lot | Croire les captures d'écran sur parole |
| Commiter à chaque tâche terminée | Accumuler dix tâches non commitées |
| Renvoyer vers le document quand il dérive | Réexpliquer la règle de mémoire |

**La phrase à utiliser quand il dérive :** « Relis `docs/XX` et reprends. »

**La phrase à utiliser quand il dit avoir fini :** « Montre-moi le contrôle exécutable des douze points de la définition de terminé. »

**La phrase à utiliser quand il signale une contradiction :** « Bonne prise. Applique la hiérarchie de `CLAUDE.md` § 5, propose la correction, et attends ma validation. » Ne jamais le laisser trancher seul sur le contenu métier.

---

## Rythme

- **Une session = un lot de travail.** Pas trois fonctionnalités en parallèle
- **Une branche par fonctionnalité**, worktree si tu paralléliser
- **Redémarrer la session entre deux lots.** Le contexte accumulé dégrade la qualité, quel que soit le modèle
- **Le plan vit sur disque**, pas dans le contexte : il survit à la fin d'une session

---

## Les trois premières semaines

| Semaine | Objectif |
|---|---|
| 1 | Harnais complet front et backend, CI verte, solution .NET qui compile |
| 2 | Schéma complet avec RLS, tests de politiques verts, seed chargé |
| 3 | Premier écran de séance utilisable, avec ses quatre états |

Si à la fin de la semaine 1 la CI n'est pas verte, ne passe pas à la semaine 2. Le harnais est ce qui rend tout le reste possible.

---

## En parallèle, hors code

Ces trois appels conditionnent la moitié du produit et ne dépendent pas de Claude Code :

1. **Diététicien** — le verrou principal
2. **Avocat en droit de la santé numérique** — avant la première ligne de code métier
3. **Kinésithérapeute** — relecture des programmes adaptés

Passe-les cette semaine, pas dans deux mois.
