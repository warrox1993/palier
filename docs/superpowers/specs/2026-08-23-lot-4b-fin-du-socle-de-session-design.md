# Lot 4b — la fin du socle de session

**Date :** 23 août 2026
**Statut :** premier jet, écrit en autonomie de nuit — **à relire au réveil**
**Décisions sources :** D15, D17, D35, D38, D41, D59
**Documents métier :** `docs/09-comptes.md` § 1 (les sept exigences), `docs/14-contenu.md` § 4 (les emails)

---

## 1. Objet et périmètre

Le lot 4 a livré trois exigences sur sept et laissé les quatre autres nommées, non écrites. `docs/aipd/2026-08-21-authentification-etat.md` les consigne comme un écart opposable. **Ce lot les ferme.**

| #   | Exigence                         | Ce qui manquait                                |
| --- | -------------------------------- | ---------------------------------------------- |
| 1   | Google OAuth                     | tout : défi, rappel, décision au retour        |
| 2   | Email vérifié avant la nutrition | l'**envoi** — la règle existait, le moyen non  |
| 7   | Fusion des comptes               | le parcours entier, et sa preuve de possession |

Plus trois dettes que le lot 4 avait consignées : le **rôle d'administration** de D41, la **limitation des tentatives** qui vit en mémoire de processus, et la **suppression de compte** livrée à moitié.

**Ce lot est écrit sans son porteur**, qui dort. Les décisions qu'il a prises avant de partir sont marquées comme telles ; celles que j'ai tranchées seul portent la mention **« tranché en autonomie »** et sont rassemblées au § 12 pour qu'il puisse les défaire d'un coup d'œil.

---

## 2. Les deux décisions du porteur

**Les emails partent par SMTP générique, chez OVHcloud.** Le code ne connaît aucun fournisseur : hôte, port, identifiants et adresse d'expédition sont de la configuration. Changer de fournisseur ne touche pas une ligne. OVHcloud a été retenu au moment du choix — cohérent avec D15, et le compte existe déjà.

Conséquence : une dépendance nouvelle, **MailKit** (MIT). `System.Net.Mail.SmtpClient` est explicitement déconseillé par Microsoft pour du code neuf — sa documentation renvoie à MailKit. Elle demande une décision datée, écrite avec ce lot.

**Les administrateurs sont listés au coffre, par adresse.** Une clé de plus dans `palier/dev` et `palier/prod`. Pour se promouvoir, un attaquant doit compromettre le **coffre** : une écriture en base ne suffit pas, là où la table `AspNetUserRoles` aurait fait de la même compromission un contrôle total.

---

## 3. L'envoi d'emails

### Le port et son adaptateur

`IEmailSender<Utilisateur>` est l'interface d'Identity, et son défaut — `NoOpEmailSender` — « ne fait rien ». Il existe pour qu'on remarque qu'on ne l'a pas remplacé ; le remplacer est exactement l'objet de ce paragraphe.

```
Palier.Infrastructure/Courrier/
├── ReglagesDuCourrier.cs      hôte, port, identifiants, expéditeur — validés au démarrage
├── EnvoyeurSmtp.cs            IEmailSender<Utilisateur>, sur MailKit
└── Modeles/                   les gabarits, fr et en
```

**L'API refuse de démarrer si les réglages manquent**, comme pour le coffre et pour `JWT_SIGNING_KEY`. Un produit qui démarre sans pouvoir envoyer d'email laisse ses utilisateurs bloqués à l'inscription sans que rien ne le signale.

### Ce qui n'est PAS journalisé

Ni l'adresse, ni le jeton, ni le corps. Une adresse est une donnée personnelle ; un jeton de vérification est un secret à usage unique. Le journal dit « courriel de vérification envoyé », et rien d'autre — pas même à qui.

### Les gabarits

**Tranché en autonomie :** les quatre emails de ce lot sont **vérification d'adresse**, **bienvenue**, **réinitialisation de mot de passe** et **confirmation de résiliation**. Les cinq autres de `docs/14-contenu.md` § 4 — fin d'essai, échec de paiement, passage en gratuit, export prêt, récapitulatif hebdomadaire — dépendent de l'abonnement et de l'export, qui n'existent pas encore. Les écrire ici produirait quatre gabarits que rien n'envoie.

Français et anglais, texte brut **et** HTML. Aucune chaîne en dur : les gabarits vivent en ressources, comme le reste des libellés.

---

## 4. La vérification d'adresse

Trois points d'entrée, que `MapIdentityApi` aurait fournis si le lot 4 ne l'avait pas écarté (D17) :

```
POST /api/v1/auth/verifier-l-adresse          { userId, code }
POST /api/v1/auth/renvoyer-la-verification    { email }
```

**Le second ne dit jamais si l'adresse existe.** Il rend `202 Accepted` dans tous les cas, et n'envoie que si le compte existe et n'est pas déjà vérifié. Sans cela, il devient un oracle d'énumération — exactement le défaut que le lot 4 a corrigé sur `/register` en découvrant que `RequireUniqueEmail` valait `false` par défaut.

Le lien de vérification pointe vers le front, qui appelle le premier point d'entrée. **Tranché en autonomie :** la base de l'URL est une configuration (`APP_URL`), pas une constante — le front vit sur un autre port en développement.

**Le drapeau que la porte lit existe déjà.** `PorteDesDomaines.NutritionOuverte` interroge `EmailConfirmed`, écrit et éprouvé au lot 4. Ce lot ne fait que lui donner de quoi passer à `true`.

---

## 5. La réinitialisation de mot de passe

```
POST /api/v1/auth/mot-de-passe-oublie         { email }
POST /api/v1/auth/reinitialiser-le-mot-de-passe  { userId, code, nouveauMotDePasse }
```

Même règle d'oracle : `202` systématique sur le premier. Le second passe par le validateur à deux étages du lot 4 — liste embarquée obligatoire, HaveIBeenPwned opportuniste — parce qu'un mot de passe choisi après réinitialisation n'est pas moins exposé qu'un autre.

**Il révoque toutes les sessions.** Une réinitialisation est ce qu'on fait quand on croit son compte compromis ; laisser vivre les jetons de rafraîchissement existants viderait le geste de son sens. Le mécanisme existe — la révocation de famille du lot 4.

---

## 6. Google OAuth

### Les deux points d'entrée

```
GET /api/v1/auth/google              → 302 vers Google
GET /api/v1/auth/google/rappel       → décide, puis 302 vers le front
```

`AddGoogle` du paquet `Microsoft.AspNetCore.Authentication.Google`, scopes `openid`, `profile`, `email` — et rien d'autre : `docs/09-comptes.md` § 1 l'interdit explicitement.

### La décision au retour, et c'est là que tout se joue

Quatre cas, dans cet ordre :

1. **Une connexion Google existe déjà** (`FindByLoginAsync`) → on connecte.
2. **Aucune connexion, aucun compte avec cette adresse** → on crée le compte, `EmailConfirmed = true` — Google a vérifié l'adresse, et son jeton le dit par la revendication `email_verified`.
3. **Un compte email existe avec la même adresse, et Google dit l'avoir vérifiée** → on propose la liaison (§ 7), on ne la fait pas.
4. **Un compte existe et Google ne l'a PAS vérifiée** → on refuse. Lier sur une adresse non vérifiée est une prise de contrôle de compte.

Le cas 4 n'est pas théorique : la revendication `email_verified` peut valoir `false` sur un compte Google Workspace mal configuré. **Le code lit cette revendication et ne la suppose jamais vraie.**

### Ce qui ne passe jamais par l'URL

Aucun jeton dans la redirection finale. Le rappel pose le cookie de rafraîchissement — `HttpOnly`, chemin restreint, comme au lot 4 — et redirige vers le front sans paramètre. Un jeton dans une URL finit dans l'historique du navigateur, dans les journaux du serveur et dans l'en-tête `Referer`.

---

## 7. La fusion des comptes

**Elle ne se fait jamais automatiquement.** Le cas 3 ci-dessus produit un état intermédiaire : « un compte existe avec cette adresse, voulez-vous les lier ? »

La preuve de possession est **le mot de passe du compte existant** :

```
POST /api/v1/auth/lier-google    { email, motDePasse, jetonDeLiaison }
```

Le `jetonDeLiaison` est à usage unique, vit dix minutes, et porte l'identifiant Google constaté au retour. Sans lui, ce point d'entrée deviendrait un moyen de lier n'importe quel compte Google à n'importe quel compte dont on connaît le mot de passe — ce qui n'est pas une attaque, mais n'est pas non plus un parcours.

**Tranché en autonomie :** le jeton de liaison est scellé par le même dispositif que le sceau du successeur (AES-GCM sous clé dérivée), et non stocké en base. Une table de plus pour un objet qui vit dix minutes coûterait un schéma, une purge et une épreuve de purge.

Si le compte existant a la **2FA active**, le mot de passe ne suffit pas : le code TOTP est exigé en plus. Le lot 4 a établi que le second facteur garde le compte, pas seulement la connexion.

---

## 8. Le rôle d'administration

```
ADMIN_EMAILS = "jean@exemple.be,autre@exemple.be"     ← au coffre
```

Une politique `administration` s'ajoute à `PolitiquesDAutorisation`, qui n'en portait que deux — et toutes deux étaient des **domaines**, pas des rôles. `GET /api/v1/sante` la porte.

**La comparaison est insensible à la casse et normalisée**, comme celle d'Identity : `Jean@Exemple.be` et `jean@exemple.be` désignent le même compte. Comparer brut ferait qu'un administrateur perdrait son rôle en changeant la casse de son adresse.

**Une adresse non vérifiée n'ouvre pas l'administration.** Sans cette condition, quiconque s'inscrit avec l'adresse d'un administrateur en devient un — et l'inscription est ouverte.

---

## 9. La limitation partagée entre répliques

Le lot 4 a livré la limitation par adresse en **mémoire de processus**, et l'a consigné : sur trois répliques, la limite effective vaut quinze tentatives au lieu de cinq.

**Tranché en autonomie : le compteur passe en base**, table `tentatives_de_connexion`, et non dans un cache tiers. Motifs :

- **Aucune dépendance nouvelle.** Redis serait le réflexe ; il ajouterait un service à déployer, à surveiller et à sauvegarder pour un compteur qui tient en une ligne par adresse.
- **PostgreSQL sait le faire.** Un `INSERT … ON CONFLICT DO UPDATE` avec une fenêtre glissante est atomique, et le moteur est déjà là.
- **Le volume est dérisoire.** Une ligne par adresse active, purgée au-delà de la fenêtre.

Le verrouillage **par compte**, lui, était déjà en base — `AspNetUsers.AccessFailedCount` — et ne change pas.

La table porte RLS activée et forcée, comme toute table de `public` (D37), en forme « référence publique » : lecture et écriture pour `palier_app`, aucune donnée personnelle au sens de l'article 9 — une adresse IP et un compteur.

---

## 10. La suppression de compte

Le lot 4 n'a livré que la cascade des sessions. Ce lot ajoute :

```
POST /api/v1/compte/demander-la-suppression      → envoie un courriel de confirmation
POST /api/v1/compte/confirmer-la-suppression     { code }
```

**Tranché en autonomie : pas de délai de grâce.** L'idée d'un compte « supprimé dans 30 jours » est séduisante et se paie cher — un état de plus dans chaque requête, une purge à écrire, et une donnée de santé qui survit à la demande de son propriétaire. L'article 17 demande l'effacement, pas l'archivage. La confirmation par courriel suffit à protéger contre le geste accidentel.

**L'export préalable n'est pas dans ce lot** : il n'existe pas encore. Le courriel de confirmation le dit clairement — « vos données seront effacées et ne pourront pas être récupérées » — plutôt que de promettre un export qui n'arriverait jamais.

---

## 11. Ce que ce lot ne contient pas

- **Aucun écran.** Comme le lot 4 : ils appartiennent au lot 6. Les parcours sont vérifiables par épreuves d'intégration, pas dans un navigateur.
- **Les cinq emails d'abonnement et d'export** — voir § 3.
- **L'enregistrement du client OAuth chez Google.** C'est une action sur un compte qui n'est pas le mien. Le code est complet et éprouvé contre un fournisseur OAuth simulé ; il suffira de poser deux valeurs au coffre.
- **L'ouverture du compte SMTP chez OVHcloud** — même raison, mêmes conséquences.

---

## 12. Les décisions prises en autonomie, à confirmer au réveil

| #   | Décision                                                            | Ce qui la défait                                  |
| --- | ------------------------------------------------------------------- | ------------------------------------------------- |
| 1   | Quatre emails seulement — les cinq autres dépendent de l'abonnement | vouloir les gabarits d'avance                     |
| 2   | `APP_URL` en configuration pour la base des liens                   | une URL unique pour tous les environnements       |
| 3   | Le jeton de liaison est scellé, non stocké                          | vouloir l'auditer en base                         |
| 4   | La limitation partagée passe par PostgreSQL, pas Redis              | un volume qui dépasserait ce qu'une table absorbe |
| 5   | Pas de délai de grâce à la suppression                              | une obligation contractuelle de rétention         |
| 6   | La 2FA est exigée en plus du mot de passe pour lier Google          | juger le parcours trop lourd                      |

---

## 13. Points ouverts

**1. Les identifiants Google et SMTP au coffre.** Deux clés de plus dans `palier/dev` et `palier/prod` : `GOOGLE_OAUTH_CLIENT_SECRET` — que D59 avait écartée faute d'usage, et qui en a un maintenant — et `SMTP_PASSWORD`. Les autres réglages SMTP ne sont pas des secrets et restent dans l'environnement.

**2. L'écran de liaison.** Le cas 3 du § 6 redirige vers une page que le lot 6 écrira. En attendant, la redirection porte un paramètre que le front d'aujourd'hui ignore — visible, sans effet.

**3. La délivrabilité.** SPF, DKIM et DMARC sur le domaine sont une configuration DNS, pas du code. Sans eux, les courriels partent en indésirables et l'inscription paraît cassée.
