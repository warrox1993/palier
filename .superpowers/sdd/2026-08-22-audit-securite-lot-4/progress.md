# Audit de sécurité du lot 4 — journal de bord

**Branche :** `securite/lot-4-correctifs`, depuis `feat/lot-4-socle-session` (`b611c3b`)
**Nuit du 21 au 22 août 2026** · scan lancé à 18 h 34, dernier commit à 3 h 15
**Rapport :** `CLAUDE-SECURITY-20260821-170836/`

---

## Ce qui a changé

|                        |                                                                                                                |
| ---------------------- | -------------------------------------------------------------------------------------------------------------- |
| Scan                   | 149 agents, 12,6 M jetons, 70 min · 82 candidats → 31 dédupliqués → **14 pistes** vérifiées, statut `verified` |
| Défauts réels          | **6** — les 14 pistes étaient le même problème vu de plusieurs angles                                          |
| Corrigés               | **6 sur 6**                                                                                                    |
| Épreuves               | 198 en base + 176 domaine + 37 application = **411**                                                           |
| Couverture adaptateurs | 96,63 / 79,60 / 71,55 → **97,79 / 82,29 / 77,22**                                                              |
| `verify`               | 16 étapes, 434 s, aucune en échec                                                                              |

---

## Ce qui a cassé — et ce que je n'avais pas vu

**Dans trois cas sur six, la correction introduisait un défaut plus grave que
celui qu'elle fermait.** Aucun n'était visible en relecture ; il a fallu, chaque
fois, une sonde qui mesure.

1. **Le jeton de concurrence, retiré sans le dire.** Sérialiser le compteur
   d'échecs demandait de passer de `UpdateAsync` à `ExecuteUpdateAsync`. Le
   premier renouvelle le `ConcurrencyStamp`, le second non. Résultat mesuré :
   une requête des routes 2FA — **qui n'exigent aucun mot de passe** — chargée
   avant qu'un verrouillage ne tombe l'effaçait en écrivant, et remettait
   l'escalade à son premier palier pour toujours.

2. **Le sceau qui vivait quatorze jours.** Faire converger les deux onglets
   demandait de sceller le successeur. Mesuré : depuis le texte clair d'un jeton
   consommé **six heures plus tôt** et une lecture de la table, on remontait de
   sceau en sceau jusqu'au **jeton vivant**, qu'on faisait tourner sans révoquer
   la famille. Avant ce scellement, un jeton consommé ne valait plus rien passé
   trente secondes : la correction retirait cette propriété.

3. **Le clair laissé à côté du haché.** Hacher les codes de récupération écrivait
   dans un jeton neuf et laissait la ligne héritée intacte — la donnée que le
   défaut dénonce survivait au correctif, indéfiniment. Et les codes émis avant
   cessaient de fonctionner, avec un chemin de secours qui passait par une route
   exigeant le TOTP que l'utilisateur venait de perdre.

## Deux défauts d'épreuve, du même genre que ceux du lot 4

- **La tolérance dérivée du réglage éprouvé.** L'épreuve du canal temporel
  calculait sa tolérance depuis le budget : réduire le budget réduisait la
  tolérance d'autant, et elle rougissait pour la mauvaise raison. Même défaut
  que `Des_echecs_ESPACES` au lot 4. Seuil rendu littéral, avec une assertion de
  garde.

- **L'épreuve qui regardait le mauvais indicateur.** La même, mesurée : l'écart
  naturel n'est que de **5 ms sur 160** — déjà sous toute tolérance stable. Une
  épreuve bâtie sur l'écart serait restée verte sans égalisation. Ce qui
  distingue vraiment, c'est que les deux branches reviennent **au budget** et non
  à leur coût propre.

## Ce que le procédé de patchs a coûté et rapporté

Six rondes correcteur/vérificateur sur trois unités, **toutes déclinées** — la
règle du plugin veut qu'une seconde objection fasse tomber l'unité, et le
vérificateur trouvait à chaque fois un défaut réel de plus. Zéro patch produit
en cinq heures.

Mais les objections, elles, étaient justes et mesurées. Elles ont été reprises
une par une pour écrire les corrections à la main, dans le dépôt. **Le procédé a
servi d'analyse, pas de livraison** — et c'est ce qu'il faut en attendre.

Deux franchissements invalides à noter : retirer trois fichiers d'épreuves pour
éprouver le seuil de couverture cassait la **compilation** avant que le seuil ne
soit évalué ; et neutraliser l'égalisation temporelle rendait un paramètre
inutilisé, que le détecteur de code mort refuse. Un franchissement qui casse en
amont de sa cible ne prouve rien.

---

## Ce que je signale sans y avoir touché

- **Le secret TOTP reste en clair.** Il se chiffre, il ne se hache pas, et cela
  demande une décision de gestion de clé — où elle vit, comment elle tourne, ce
  qui se passe au redéploiement. Aucun trousseau n'a été fabriqué pour donner le
  change.
- **Le rôle d'administration de D41** n'existe pas : les deux politiques du
  produit sont des domaines, pas des rôles.
- **Le budget de 400 ms** est calé sur cette machine. Sur un vCPU mutualisé, le
  PBKDF2 sera plus lent et le budget devra suivre.
- **Une copie de travail résiste à la suppression** dans le répertoire de
  rapport — un processus la tenait encore. Elle est derrière un `.gitignore`,
  invisible à git ; `rm -rf CLAUDE-SECURITY-20260821-170836/.claude-security-run`
  la retirera.
- **L'écran d'état du front appelle `/api/v1/sante`**, désormais fermée. Il
  faudra qu'il présente un jeton, ou que la route soit scindée — D41 prévoit de
  reprendre la question de cet écran au lot 6.

---

## Un défaut de harnais, trouvé en poussant

**`git push` échouait en code 141, trois fois de suite, et ce n'était ni le
réseau ni le shell.**

Git ouvre la connexion SSH **puis** lance le hook `pre-push`. Ce hook est
`npm run verify`, qui dure désormais plus de sept minutes. Pendant ce temps la
connexion reste inactive, GitHub la ferme, et quand git veut enfin envoyer il
écrit dans un tube mort — SIGPIPE, 128 + 13 = 141.

Le diagnostic s'est fait par élimination, pas par supposition : `ssh -T
git@github.com` authentifie, `git ls-remote` répond, `npm run verify` sort en
code **0**, et PowerShell donne le même 141 que Git Bash — donc ni le réseau, ni
le hook, ni le shell.

La parade tient en une variable :

```bash
GIT_SSH_COMMAND="ssh -o ServerAliveInterval=20 -o ServerAliveCountMax=60" git push …
```

**Ce n'est qu'un pansement.** Le push du lot 4, à 320 s, passait de justesse ;
celui-ci, à 423 s, ne passait plus. Le seuil se rapproche à chaque lot. La vraie
correction est celle que `verify` réclame lui-même à chaque exécution — « une
boucle de rétroaction lente est un défaut à traiter » — et elle appartient au
porteur du projet : soit `ServerAliveInterval` entre dans la configuration SSH ou
dans `.husky/pre-push`, soit `verify` redescend sous le délai d'inactivité.

---

## Le franchissement

Chaque garde-fou écrit cette nuit a été vu rouge sur la violation qu'il refuse.

| Garde-fou                            | Violation                     | Motif constaté                                                   |
| ------------------------------------ | ----------------------------- | ---------------------------------------------------------------- |
| Jeton de concurrence du verrouillage | ligne retirée                 | « une écriture partie d'un exemplaire périmé a été acceptée »    |
| Effacement des sceaux périmés        | effacement retiré             | « un sceau périmé survit en base »                               |
| Route de santé fermée                | `RequireAuthorization` retiré | « la route de santé est de nouveau anonyme »                     |
| Égalisation temporelle               | délai neutralisé              | « la branche inconnue revient en 174 ms, sous le budget de 400 » |

---

## Les commits

|           |                                                                               |
| --------- | ----------------------------------------------------------------------------- |
| `b464882` | exige une preuve de possession avant de faire tourner le second facteur       |
| `e1f917d` | hache les codes de récupération et efface le clair qu'ils laissaient derrière |
| `16da8e9` | sérialise le compteur d'échecs, sans retirer le garde qu'il remplaçait        |
| `200711c` | fait converger les deux onglets sur une seule chaîne, et borne le sceau       |
| `6767165` | ferme la route de santé, que D41 voulait fermée depuis le lot 4               |
| `f05ff5d` | égalise le temps des refus de connexion, que le message seul ne masquait pas  |
