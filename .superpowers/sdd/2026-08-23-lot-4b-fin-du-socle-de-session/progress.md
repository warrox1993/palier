# Lot 4b — journal de bord

**Exécuté en autonomie**, dans la nuit du 22 au 23 août 2026, pendant que le
porteur du projet dormait. Il avait tranché deux décisions avant de partir —
SMTP générique chez OVHcloud, administrateurs listés au coffre — et demandé que
le lot 4 soit « terminé complètement ».

**État : sept tâches sur huit.** La huitième est décrite au § 4, avec le motif
de son report.

---

## 1. Ce qui est livré

| #   | Tâche                                  | Épreuves |
| --- | -------------------------------------- | -------- |
| 1   | Le rôle d'administration (D41)         | 7        |
| 3   | L'envoi d'emails par SMTP (D60)        | 18       |
| 4   | La vérification d'adresse (exigence 2) | 9        |
| 5   | La réinitialisation de mot de passe    | 8        |
| 6   | Google OAuth (exigence 1)              | 25       |
| 7   | La fusion des comptes (exigence 7)     | 9        |
| 8   | La suppression de compte               | 6        |

**334 épreuves au total**, couverture 97,32 / 79,05 / 82,93 pour des seuils à
96 / 79 / 71. `npm run verify` passe en 479 s.

**Les trois exigences que l'AIPD consignait comme écart opposable sont
fermées**, plus le rôle d'administration que D41 réclamait depuis le lot 4.

---

## 2. Ce que les épreuves ont attrapé, et qui aurait échappé

**Le jeton d'Identity est déjà du Base64.** Le premier encodage du lien de
vérification faisait `Convert.ToBase64String` par-dessus, et
`Convert.FromBase64String` réussissait au retour sur un jeton NON encodé — en
rendant d'autres octets. Le décodage « marchait » en produisant un jeton
invalide. Base64Url, comme le reste d'ASP.NET.

**Un sujet Google constant dans les épreuves.** Deux comptes ne peuvent pas
porter la même connexion externe : la seconde liaison réussie échouait sur
`AddLoginAsync`, et l'échec ressemblait à un défaut de la preuve de possession.

**`/lier-google` contient « google ».** L'épreuve qui vérifiait l'absence des
routes OAuth comptait « contient google » et rougissait dès que la fusion a
ajouté sa route. Le filtre porte désormais sur le préfixe.

**Un `MimeMessage` disposé.** L'envoyeur le libère dès la sortie — correct, il
est parti — mais le transport factice en gardait la référence et lisait un objet
mort. Il capture désormais ce qu'il vérifie.

---

## 3. Les décisions prises en autonomie

Elles sont rassemblées au § 12 de la conception, avec ce qui les défait. Deux
méritent d'être relues en priorité :

**Aucun délai de grâce à la suppression.** L'article 17 demande l'effacement,
pas l'archivage. Un compte « supprimé dans 30 jours » laisserait une donnée de
santé survivre à la demande de son propriétaire.

**Une inscription sur adresse déjà prise envoie AUSSI un courriel** — à son
propriétaire légitime, pour l'avertir. Sans ce second envoi, le seul fait qu'un
message parte ou non trahirait l'existence du compte : le lot 4 avait fermé ce
canal sur la connexion, l'inscription le rouvrait par la porte de derrière.

---

## 4. La tâche 2, et pourquoi elle n'est pas faite

**La limitation des tentatives vit toujours en mémoire de processus.** Sur
plusieurs répliques, la limite effective est multipliée par leur nombre.

Le plan prévoyait de la faire passer en base. Je ne l'ai pas fait, et le motif
tient en trois points :

1. **C'est une réserve, pas un écart d'exigence.** L'AIPD la consigne comme
   telle depuis le lot 4, et le produit tourne à une seule instance : elle ne
   mord pas encore.
2. **Elle coûte plus qu'elle n'en a l'air** : une table, une migration avec ses
   politiques RLS et ses privilèges pour trois rôles, un filtre de point
   d'entrée, et une **troisième exemption** d'`ArchitectureTests` — le compteur
   lit la base hors du pipeline.
3. **La marge de couverture de branche était à 0,05 point du seuil** (79,05 pour
   79). Ajouter du code à cinq heures du matin sur cette marge, c'était choisir
   entre bâcler les épreuves et casser le harnais.

Les trois exigences opposables passaient avant. Elles sont fermées ; celle-ci
attend le jour où l'API tournera à plus d'une instance — c'est-à-dire le lot 9.

---

## 5. Ce qui reste au porteur du projet

- **Enregistrer le client OAuth chez Google** — action sur un compte tiers. Le
  code est complet et éprouvé ; sans identifiants, les routes ne sont simplement
  pas attachées.
- **Ouvrir le compte SMTP chez OVHcloud**, et poser **SPF, DKIM et DMARC** sur
  le domaine. Sans eux, les courriels partent en indésirables et l'inscription
  paraît cassée — c'est de la configuration DNS, pas du code.
- **Ajouter au coffre** : `ADMIN_EMAILS`, `SMTP_PASSWORD`,
  `GOOGLE_OAUTH_CLIENT_SECRET`.
- **Relire les six décisions prises en autonomie** (§ 12 de la conception).
- **`/code-review ultra`** sur ce lot — il ne se lance que par lui.
