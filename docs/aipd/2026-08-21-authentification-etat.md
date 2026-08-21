# Authentification — état des mesures techniques

> **Ce document existe pour une raison précise.** `docs/13-juridique.md` § 5 :
> « l'authentification devient une **mesure technique de l'AIPD**, et non plus
> une garantie de sous-traitant. Les sept exigences […] sont à décrire comme
> telles, **avec leur état d'implémentation**. […] **l'écart se documente, il
> ne se suppose pas comblé**. »
>
> Sans lui, le lot 4 livrerait cinq exigences sur sept en laissant croire que
> les sept sont couvertes. Ce serait faux, et ce serait faux dans un document
> opposable.

**Date de l'état :** 21 août 2026, fin du lot 4.
**Portée :** les sept exigences de `docs/09-comptes.md` § 1.
**Ce qui fait foi :** le code du dépôt à cette date, et les épreuves qui le
gardent. Chaque ligne ci-dessous nomme les épreuves qui la vérifient — une
mesure sans épreuve est une intention.

---

## 1. L'état des sept exigences

| #   | Exigence                                           | État                | Ce qui est livré                                                                                                                                             | Ce qui manque, et pourquoi                                                                                                                                                                                                                      |
| --- | -------------------------------------------------- | ------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------ | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 1   | **Google OAuth**                                   | ⛔ **Reportée**     | Rien. Le magasin des connexions externes existe (`AspNetUserLogins`), il n'est pas utilisé.                                                                  | L'enregistrement du client chez Google, les points d'entrée de défi et de rappel, la décision au retour. **Aucune ligne de code n'en a été écrite** : l'écart est entier.                                                                       |
| 2   | **Email vérifié avant la nutrition**               | 🟡 **Partielle**    | La **règle** : `PorteDesDomaines.NutritionOuverte` et la politique `nutrition`, éprouvées sur les quatre combinaisons.                                       | L'**envoi** du courriel. `IEmailSender<TUser>` n'a aucune implémentation utilisable ; le défaut `NoOpEmailSender` « ne fait rien ». Sans envoi, aucun compte ne se vérifie.                                                                     |
| 3   | **Mot de passe contre HaveIBeenPwned**             | ✅ **Livrée**       | Validateur à deux étages : liste embarquée obligatoire, API k-anonymat opportuniste, en-tête `Add-Padding`, entrées de remplissage écartées.                 | Rien pour cette exigence.                                                                                                                                                                                                                       |
| 4   | **5 tentatives / 15 min, verrouillage progressif** | 🟡 **Sous réserve** | Le verrouillage par compte, avec fenêtre glissante et escalade 5 → 15 → 60 min. La limitation par adresse, avec traitement des en-têtes transférés.          | La limitation compte **en mémoire de processus** : sur plusieurs répliques, la limite effective est multipliée par leur nombre. Et `TRUSTED_PROXIES` doit être renseignée en exploitation, faute de quoi tout le monde tombe dans le même seau. |
| 5   | **2FA TOTP**                                       | ✅ **Livrée**       | Préparation, activation contre code valide, désactivation contre code valide, codes de récupération, porte à la connexion.                                   | Le **QR code**, laissé au navigateur — délibérément, pour ne pas ajouter de dépendance. L'écran reste à écrire.                                                                                                                                 |
| 6   | **Rotation des jetons de rafraîchissement**        | ✅ **Livrée**       | Magasin serveur, rotation à chaque usage, détection de réemploi avec révocation de famille, fenêtre de grâce de 30 s, cookie `HttpOnly` de chemin restreint. | Rien pour cette exigence.                                                                                                                                                                                                                       |
| 7   | **Fusion des comptes email et Google**             | ⛔ **Reportée**     | Rien. Elle dépend entièrement de l'exigence 1.                                                                                                               | Le parcours entier, **et la preuve de possession** : lier sur la seule égalité des adresses est une prise de contrôle de compte.                                                                                                                |

**Le compte, sans l'édulcorer : trois livrées, deux partielles ou sous réserve,
deux entièrement reportées.**

### Ce que les deux exigences reportées impliquent aujourd'hui

L'exigence 1 étant absente, **le seul moyen de créer un compte est l'adresse et
le mot de passe**. L'exigence 2 n'étant que partielle, **aucun compte ne peut
aujourd'hui atteindre la nutrition** : la règle est en place et le moyen de la
satisfaire n'existe pas encore. Ce n'est pas une faille, c'est un état
intermédiaire — mais il doit être connu de qui lit ce document, sans quoi
« email vérifié avant la nutrition » se lirait comme une fonction disponible.

---

## 2. Durées de conservation, pour le registre (RGPD article 30)

Ce lot crée quatre catégories de données. **Aucune n'est une donnée de santé**,
et aucune ne part au journal applicatif — `docs/01-conformite.md` § 4.

| Donnée                                     | Où                                                                                    | Durée                                                                                          | Ce qu'elle est, et ce qu'elle n'est pas                                                                                                                                           |
| ------------------------------------------ | ------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Empreinte du jeton de rafraîchissement** | `sessions_refresh.token_hash`                                                         | **14 jours** à compter de l'émission                                                           | Un SHA-256 sur 32 octets. Le jeton lui-même n'est **jamais** stocké : une épreuve vérifie qu'aucune ligne ne porte sa valeur en clair.                                            |
| **Famille de session**                     | `sessions_refresh.family_id`                                                          | **14 jours**, idem                                                                             | Un identifiant aléatoire reliant les jetons d'une même chaîne. Il ne porte aucune information sur l'utilisateur.                                                                  |
| **Appareil déclaré**                       | `sessions_refresh.device`                                                             | **14 jours**, idem                                                                             | Le `User-Agent`, coupé à 200 signes, **jamais interprété**. Il sert à ce que l'utilisateur reconnaisse ses propres sessions à l'écran des réglages.                               |
| **Compteur d'échecs et date du dernier**   | `AspNetUsers.AccessFailedCount`, `DernierEchecLe`, `VerrouillagesSubis`, `LockoutEnd` | **Fenêtre glissante de 15 minutes** pour le compteur ; effacés à la première connexion réussie | Aucun mot de passe, aucune tentative, aucune adresse n'y est conservée — seulement un nombre et une date.                                                                         |
| **Consentement santé**                     | `AspNetUsers.ConsentementSanteLe`                                                     | Durée de vie du compte                                                                         | Une **date**, et non un booléen : l'article 7 § 1 impose de pouvoir démontrer que le consentement a été donné. C'est une donnée **sur** un consentement, pas une donnée de santé. |

**Effacement.** Les sessions d'un compte supprimé disparaissent avec lui, par
cascade de clé étrangère — éprouvé dans les deux sens : la cascade emporte les
sessions du compte visé, et **ne déborde pas** sur les autres. Les sessions
éteintes se purgent par une commande idempotente.

**Ce que ce lot ne referme pas**, et qui appartient au § 3 de
`docs/09-comptes.md` : la suppression de compte complète — confirmation par
saisie de l'adresse, export proposé avant, effacement réel sous 30 jours,
**purge des sauvegardes comprise**. Ce lot livre la cascade. Il ne livre ni
l'écran, ni l'export, ni la politique de sauvegarde.

---

## 3. Ce qui change au tableau des sous-traitants

`docs/13-juridique.md` § 4 portait « Authentification, mots de passe, sessions,
jetons → **Supabase Auth** ». C'est faux depuis D17 : ce traitement est opéré
par le responsable de traitement lui-même. La ligne est corrigée, et elle
renvoie désormais à ce document pour son état.

**Ce que le changement déplace, et qu'il ne faut pas perdre.** La sécurité de
l'authentification n'est plus une garantie contractuelle obtenue d'un tiers :
c'est une mesure technique dont la démonstration incombe au responsable de
traitement. Les épreuves du dépôt en sont la trace — et c'est pourquoi ce
document nomme les mesures plutôt que de les résumer.

---

## 4. Ce qui reste à faire porter ailleurs

- **`TRUSTED_PROXIES` en exploitation.** Vide, l'API ignore tout en-tête
  transféré — comportement sûr, mais qui fait compter l'adresse du répartiteur
  OVHcloud pour tout le monde. À renseigner au déploiement.
- **`JWT_SIGNING_KEY` propre à chaque environnement**, au moins 32 octets.
  L'API refuse de démarrer sans elle.
- **La limitation sur plusieurs répliques.** Le jour où l'API tournera à plus
  d'une instance, le compteur devra passer par un magasin partagé.
- **L'envoi de courriels**, sans lequel l'exigence 2 reste incomplète.
