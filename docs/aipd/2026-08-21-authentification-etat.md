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
gardent — 388 au total, dont chaque garde-fou a été franchi, c'est-à-dire vu
rouge sur la violation qu'il refuse. Les fichiers d'épreuves sont nommés au
§ 5 ; une mesure sans épreuve est une intention, et ce document n'en porte
aucune.

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

---

## 5. Où sont les épreuves

Une mesure sans épreuve est une intention. Chaque exigence du § 1 renvoie ici,
et chaque fichier ci-dessous a vu ses garde-fous **franchis** — la violation
provoquée, le rouge constaté, le motif vérifié.

| Exigence                             | Fichiers                                                                                 |
| ------------------------------------ | ---------------------------------------------------------------------------------------- |
| 2 — email vérifié avant la nutrition | `PorteDesDomainesTests`, `PolitiquesDAutorisationTests`                                  |
| 3 — mot de passe contre HIBP         | `ValidateurDeMotDePasseTests`                                                            |
| 4 — limitation et verrouillage       | `LimitationTests`, `DecisionDeVerrouillageTests`, `VerrouillageTests`                    |
| 5 — 2FA TOTP                         | `DeuxFacteursTests`                                                                      |
| 6 — rotation des jetons              | `DecisionDeRotationTests`, `MagasinDeSessionsTests`, `RotationDesSessionsTests`          |
| Socle — identité, jeton, composition | `JetonDAccesTests`, `IdentiteDepuisJetonTests`, `CompositionTests`, `PointsDEntreeTests` |
| Isolation, rôles, cascade            | `IsolationTests`, `RolesTests`, `SchemaTests`, `SauvegardeTests`                         |

Les exigences **1** et **7** n'ont aucun fichier, et c'est la mesure exacte de
leur absence.

---

## 6. Ce que l'audit du 22 août 2026 a changé

Un scan de sécurité multi-agents a été passé sur `back` et `db` à la révision
`b611c3b`, puis **six défauts ont été corrigés** (D58). Ce paragraphe met à jour
ce que les sections précédentes affirment, et il prime sur elles là où elles
divergent.

**Ce qui se renforce dans le tableau du § 1 :**

| #   | Exigence                   | Ce qui change                                                                                                                                                                                                                                                                                                                          |
| --- | -------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 4   | Limitation et verrouillage | Le compteur d'échecs était une lecture-modification-écriture **non atomique** : cinq tentatives simultanées n'en comptaient qu'une, et le verrouillage par compte — seule défense nommée contre le bourrage distribué — se contournait en tirant les essais en parallèle. Il passe désormais par une transaction avec verrou de ligne. |
| 5   | 2FA TOTP                   | `/2fa/preparer` **réenrôlait le second facteur sans aucune preuve** : un jeton d'accès volé suffisait à rebinder le TOTP et à invalider les codes de récupération de la victime, définitivement. Une preuve de possession est exigée. Les codes de récupération sont désormais **hachés** ; ils étaient conservés en clair.            |
| 6   | Rotation des jetons        | Un rejeu dans la fenêtre de grâce **fourchait la famille** : voleur et victime repartaient chacun sur une chaîne vivante, et la détection de réemploi était éteinte pour toujours. Les deux convergent maintenant sur le successeur, scellé sous une clé dérivée du jeton présenté.                                                    |

**Ce qui s'ajoute aux durées de conservation du § 2 :**

| Donnée                  | Où                                  | Durée                                                 | Ce qu'elle est                                                                                                                                                                                                                                                                                         |
| ----------------------- | ----------------------------------- | ----------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| **Sceau du successeur** | `sessions_refresh.successor_sealed` | **Trente secondes** après la consommation de sa ligne | Un chiffré AES-GCM du jeton successeur, sous une clé dérivée du jeton parent — jamais stockée. Effacé par la rotation suivante et par la purge, y compris pour les familles dormantes. Sans ce bornage, il resterait déchiffrable quatorze jours et les sceaux s'enchaîneraient jusqu'au jeton vivant. |

**Ce qui reste ouvert, et doit figurer au registre comme tel :**

- ~~**Le secret TOTP demeure en clair**~~ — **fermé le 22/08/2026 par D59.** La
  décision de gestion de clé a été prise : coffre OVHcloud KMS, chiffrement
  AES-GCM-256 sous une clé de données dont l'enveloppe seule vit en base. Le
  secret est **lié à son propriétaire** par les données associées : recopié d'un
  compte vers un autre, il ne se déchiffre plus. Les secrets hérités en clair
  sont migrés au premier contact.

  **Deux compromissions indépendantes** sont désormais nécessaires : la lecture
  de la base ne donne que des chiffrés, et le compte de service du coffre ne
  donne rien sans elle. Reste au registre un point d'exploitation : les secrets
  écrits en clair avant le 22/08 ont laissé leur trace dans le journal
  d'écriture anticipée de PostgreSQL. Aucun code ne peut l'effacer — la
  réparation est une rotation du WAL et une sauvegarde neuve après migration.

- **`GET /api/v1/sante` est passée derrière l'authentification**, comme D41
  l'exigeait depuis le lot 4 — elle publiait l'identifiant exact de la dernière
  migration appliquée. Le **rôle d'administration** que D41 demande également
  n'existe pas encore dans le produit.

**Ce que cet audit ne dit pas.** Les quatre suites d'épreuves ont été écartées de
son périmètre, ainsi que `front/`, `scripts/` et la documentation. Il a jugé le
code de production, pas la preuve qu'il est gardé, et rien du navigateur.
