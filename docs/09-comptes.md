# 09 — Comptes, onboarding, abonnement

---

## 1. Authentification

> **Reprise le 20/08/2026.** La version précédente confiait les deux voies de connexion et le
> hachage des mots de passe à **Supabase Auth**. D17 a réécrit l'authentification avec
> **ASP.NET Identity**, pour l'indépendance vis-à-vis d'un fournisseur ; D9 a placé un backend
> .NET entre le client et les données, et D16 met le front et l'API sous le même domaine.
> Aucune exigence de sécurité ne bouge, aucun délai ni seuil ne bouge : seul le mécanisme
> change, et il change de camp — ce qui était délégué est désormais **opéré**. Le relevé en fin
> de section dit, exigence par exigence, ce qu'ASP.NET Identity fournit et ce qui reste à écrire.

**Pas de lien magique.** Deux voies, toutes deux portées par ASP.NET Identity — D17.

### Google OAuth

Bouton en premier. Récupère email, nom et photo. Aucun scope supplémentaire demandé — pas d'accès aux contacts, au calendrier ni à quoi que ce soit d'autre.

Mécanisme : `Microsoft.AspNetCore.Authentication.Google` (`AddGoogle`), enregistré sur le même `AddAuthentication` que le reste. Les scopes `profile`, `email` et `openid` suffisent à ce que le bouton récupère, et la règle ci-dessus interdit d'en ajouter.

### Email et mot de passe

- Minimum 10 caractères (`PasswordOptions.RequiredLength` — le défaut d'Identity est 6, à porter à 10), vérification contre une liste de mots de passe compromis (l'API HaveIBeenPwned expose un contrôle par préfixe de hachage, sans transmettre le mot de passe)
- Hachage par le `PasswordHasher<TUser>` d'ASP.NET Identity — **ne jamais réimplémenter**. La règle est inchangée, l'algorithme non : **ce n'est pas bcrypt**. Vérifié à la source le 20/08/2026 (`PasswordHasher.cs`, branche `release/10.0`, et `PasswordHasherOptions` du paquet `Microsoft.Extensions.Identity.Core` v10.0.0) : le défaut est **PBKDF2 avec HMAC-SHA512, sel de 128 bits, sous-clé de 256 bits, 100 000 itérations** — `CompatibilityMode = IdentityV3`, `IterationCount = 100000`. Identity n'embarque ni Argon2, ni scrypt, ni bcrypt : seul PBKDF2 est implémenté. Le premier octet du haché porte la version de l'algorithme, donc un changement de paramétrage n'invalide pas les hachés déjà enregistrés. **Le paramétrage n'est acté par aucune décision à ce jour** — conserver le défaut ou relever le nombre d'itérations reste à arbitrer, et l'arbitrage se mesure (coût CPU par connexion sur l'instance OVHcloud), il ne se devine pas
- **Vérification d'email obligatoire** avant accès aux fonctions nutrition
- Réinitialisation par jeton à usage unique, valable 1 heure (les jetons du `DataProtectionTokenProvider` d'Identity valent **un jour** par défaut : `DataProtectionTokenProviderOptions.TokenLifespan` est à ramener à 1 heure. Cette option est partagée par tous les jetons du fournisseur — un fournisseur dédié est nécessaire si les autres durées doivent différer)
- Limitation : 5 tentatives par IP et par compte sur 15 minutes, puis verrouillage temporaire progressif
- 2FA par TOTP, optionnelle, disponible dès la V1 dans les réglages

### Récupération de compte

Perte d'accès à l'email : procédure manuelle, avec vérification d'éléments du compte (date de création, dernières séances). Jamais automatique — c'est le vecteur d'attaque classique.

### Session

Jeton d'accès de courte durée, jeton de rafraîchissement en cookie httpOnly, rotation à chaque usage. Déconnexion de tous les appareils disponible dans les réglages. Liste des sessions actives avec appareil et date.

**Le cookie reste un cookie de même site — D16.** Front et API sous le même domaine : pas de `SameSite=None`, pas de pré-requête CORS, surface CSRF minimale. Les attributs restent `httpOnly`, `Secure`, et `SameSite` en `Lax` ou `Strict`.

**Ce qu'ASP.NET Identity ne tient pas :** ni la rotation du jeton de rafraîchissement, ni la liste des sessions actives. « Déconnexion de tous les appareils » ne repose nativement que sur le tampon de sécurité (`SecurityStamp`), revalidé par intervalle (`SecurityStampValidatorOptions.ValidationInterval`) pour l'authentification par cookie, et borné par la durée de vie du jeton d'accès pour l'authentification par jeton — dans les deux cas l'effet est **différé**, jamais immédiat. Le détail est au relevé ci-dessous, ligne 6.

### Fusion de comptes

Si un utilisateur crée un compte email puis se connecte via Google avec la même adresse, proposer la liaison des deux méthodes sur un seul compte plutôt que de créer un doublon.

### Ce qu'ASP.NET Identity fournit, et ce qui reste à écrire

D17 le dit sans détour : « ASP.NET Identity en couvre une partie, pas tout ». Voici laquelle. Ce relevé ne réduit aucune exigence — il chiffre le travail que la décision a déplacé du fournisseur vers nous. Vérifié à la source le 20/08/2026 sur la documentation Microsoft en version `aspnetcore-10.0`.

| #   | Exigence                                                                | Ce qu'ASP.NET Identity fournit                                                                                                                                                                                                                                                                                                                                                                                                                               | Ce qui reste à écrire                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                               |
| --- | ----------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 1   | **Google OAuth**                                                        | `AddGoogle` (paquet `Microsoft.AspNetCore.Authentication.Google`), et le magasin des connexions externes : table `AspNetUserLogins`, `AddLoginAsync`, `FindByLoginAsync`                                                                                                                                                                                                                                                                                     | L'enregistrement du client chez Google, les points d'entrée de défi et de rappel, et la décision au retour — créer, lier, ou refuser. Les dix points d'entrée de `MapIdentityApi<TUser>` (`/register`, `/login`, `/refresh`, `/confirmEmail`, `/resendConfirmationEmail`, `/forgotPassword`, `/resetPassword`, `/manage/2fa`, `GET` et `POST /manage/info`) **n'en comportent aucun pour la connexion externe**                                                                                                                                                                                                                                                                                                                                                     |
| 2   | **Email vérifié avant la nutrition**                                    | `SignInOptions.RequireConfirmedEmail`, la génération du jeton et le point d'entrée `/confirmEmail`                                                                                                                                                                                                                                                                                                                                                           | Deux choses. L'**envoi** : `IEmailSender<TUser>` n'a aucune implémentation utilisable en production — le défaut, `NoOpEmailSender`, « ne fait rien » et existe pour détecter qu'on ne l'a pas remplacé. Et surtout le **découpage** : la règle du produit n'est pas « pas de connexion sans email vérifié » mais « **pas de nutrition** sans email vérifié » — l'entraînement reste ouvert, comme au refus du consentement santé (§ 2). C'est une règle d'autorisation applicative, sans équivalent dans Identity, et `RequireConfirmedEmail` seul bloquerait trop                                                                                                                                                                                                  |
| 3   | **Mot de passe contre HaveIBeenPwned**                                  | Rien pour ce contrôle. Le point d'extension prévu : `IPasswordValidator<TUser>`, enregistré par `IdentityBuilder.AddPasswordValidator<T>()`, appelé avant tout enregistrement de haché. `PasswordOptions` couvre la longueur et les classes de caractères                                                                                                                                                                                                    | Le validateur entier : appel par plage de préfixe (k-anonymat), délai d'attente, et **le comportement quand l'API est injoignable** — à arbitrer, un contrôle qui échoue ouvert ne protège personne, un contrôle qui échoue fermé bloque les inscriptions                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                           |
| 4   | **5 tentatives par IP et par compte / 15 min, verrouillage progressif** | La partie **compte** seulement : `LockoutOptions.MaxFailedAccessAttempts` (défaut 5) et `DefaultLockoutTimeSpan` (défaut 5 minutes), à condition d'appeler `PasswordSignInAsync` avec `lockoutOnFailure: true`                                                                                                                                                                                                                                               | Trois manques. (a) **La fenêtre de 15 minutes** n'existe pas comme option : le compteur d'échecs est cumulatif et remis à zéro par une connexion réussie. (b) **Le verrouillage progressif** : `DefaultLockoutTimeSpan` est une durée unique, sans escalade — l'escalade est à porter par l'application. (c) **La limitation par IP** est hors d'Identity : middleware de limitation de débit (`AddRateLimiter`, `UseRateLimiter`, partition sur `RemoteIpAddress`). Deux réserves à ne pas perdre : ce compteur vit en **mémoire du processus**, et derrière le proxy de l'hébergement (D15) l'adresse observée est celle du proxy tant que les en-têtes transférés ne sont pas configurés — sans quoi la limitation par IP compte tout le monde dans le même seau |
| 5   | **2FA TOTP**                                                            | Complet côté cryptographie : clé partagée, `AuthenticatorTokenProvider`, vérification du code, codes de récupération, mémorisation d'appareil. Exposé par `POST /manage/2fa`                                                                                                                                                                                                                                                                                 | Le **QR code** : la documentation le dit explicitement, « les modèles d'application web ASP.NET Core prennent en charge les authentificateurs, mais ne fournissent pas la génération de QR code ». L'écran de réglage, sa bibliothèque de QR code et l'affichage unique des codes de récupération sont à écrire. Rappel de la documentation, qui vaut règle : le code TOTP reste valable plusieurs authentifications avant d'expirer, il ne se journalise pas                                                                                                                                                                                                                                                                                                       |
| 6   | **Rotation des jetons de rafraîchissement**                             | Ne répond pas à l'exigence. `/refresh` rend bien un nouveau couple, mais les jetons sont rendus **dans le corps JSON**, jamais en cookie ; ce ne sont pas des JWT ; et la documentation les présente comme « une alternative à l'option cookie pour les clients qui ne peuvent pas utiliser de cookie », pour des scénarios simples. L'option `useCookies=true` donne un cookie d'authentification `httpOnly`, mais aucun jeton de rafraîchissement tournant | Le mécanisme entier tel que ce document l'exige : jeton d'accès court, jeton de rafraîchissement **en cookie `httpOnly`** (de même site — D16), **rotation à chaque usage** avec invalidation du précédent et détection de réemploi. Cela suppose un **magasin de jetons côté serveur**, qu'Identity ne fournit pas — et c'est ce même magasin qui portera la liste des sessions actives (appareil, date) et la déconnexion immédiate de tous les appareils                                                                                                                                                                                                                                                                                                         |
| 7   | **Fusion des comptes email et Google**                                  | Le magasin : plusieurs méthodes de connexion sur un même compte, via `AddLoginAsync`, `FindByLoginAsync`, `GetLoginsAsync`, `RemoveLoginAsync`                                                                                                                                                                                                                                                                                                               | Le parcours entier. Détecter au retour de Google qu'un compte email porte la même adresse, puis **ne lier qu'après preuve de possession** — lier sur la seule égalité des adresses est une prise de contrôle de compte dès lors que l'adresse rendue par le fournisseur n'est pas vérifiée. Identity ne porte aucune règle de fusion : c'est de l'applicatif de bout en bout                                                                                                                                                                                                                                                                                                                                                                                        |

**Ce que ce relevé implique et qu'il ne faut pas édulcorer.** Sur les sept exigences, deux sont natives à la configuration près (2 et 5, aux réserves ci-dessus), deux le sont partiellement (1 et 4), une n'a qu'un point d'extension (3), une n'a que son magasin (7), et une est **entièrement à construire** (6). D17 le formulait ainsi : « la sécurité de l'authentification devient un traitement que vous opérez, non un service délégué ». C'est aussi ce qui doit entrer à l'AIPD et au registre de `13-juridique.md`.

---

## 2. Onboarding

Six écrans, aucun sautable sauf mention contraire. Objectif : que le premier écran de séance soit utilisable immédiatement après.

| #   | Écran       | Contenu                                                                           |
| --- | ----------- | --------------------------------------------------------------------------------- |
| 1   | Bienvenue   | Ce que fait l'app en une phrase. Mention « assisté par IA »                       |
| 2   | Profil      | Sexe, date de naissance, taille, poids                                            |
| 3   | Activité    | Temps de marche quotidien, métier assis ou debout. Pas de catégories floues       |
| 4   | Contraintes | « Une blessure, une douleur ou une limitation ? » Régions, sévérité. **Sautable** |
| 5   | Objectif    | Prise de masse, maintien, perte. Avec la marge d'erreur du calcul affichée        |
| 6   | Programme   | Fréquence réaliste (3 à 7), puis proposition de structure                         |

**Écran 4 est le plus important du produit.** C'est lui qui active le différenciateur. Le formuler comme une aide, jamais comme un questionnaire médical : « pour qu'on évite de te proposer des exercices qui te font mal ».

**Consentement données de santé** présenté à l'écran 2, séparé des CGU, refusable. En cas de refus : accès à l'entraînement, pas à la nutrition.

À la fin : la première séance est prête, et l'utilisateur y est envoyé directement. Pas de tableau de bord vide.

### Reprise d'onboarding

Interruption possible à tout moment ; l'état est sauvegardé et repris à l'écran suivant.

---

## 3. Abonnement

### Structure

| Plan                       | Prix      | Contenu                                             |
| -------------------------- | --------- | --------------------------------------------------- |
| Essai                      | 14 jours  | Tout, sans carte bancaire                           |
| Complet                    | 20 €/mois | Tout                                                |
| Complet annuel             | 200 €/an  | Deux mois offerts                                   |
| Après essai, sans paiement | Gratuit   | **Lecture seule + saisie d'entraînement illimitée** |

**Le plan gratuit conserve la saisie d'entraînement et l'accès complet à l'historique.** Deux raisons : ne jamais prendre en otage les données de quelqu'un, et garder l'app installée — un utilisateur qui continue à noter ses séances revient. Ce qui bascule derrière le paiement : nutrition, assistant, analyse, progression RPG.

### Mécanique

- Stripe Checkout et Customer Portal — ne pas construire d'écran de facturation maison
- Webhooks pour les changements d'état, avec vérification de signature et idempotence
- TVA belge 21 %, Stripe Tax pour la gestion multi-pays
- Facture PDF automatique, accessible dans le compte

### Échec de paiement

Relance à J+1, J+3, J+7 par email. Bandeau discret dans l'app. Passage en gratuit à J+10, **sans suppression d'aucune donnée**. Reprise immédiate au paiement.

### Résiliation

En deux clics, sans appel ni email. Accès maintenu jusqu'à la fin de la période payée. Question facultative sur le motif — c'est la source d'information la plus utile du produit.

### Suppression de compte

Distincte de la résiliation. Confirmation par saisie de l'email. Export proposé avant. Effacement réel sous 30 jours, purge des sauvegardes comprise.

---

## 4. Notifications

Toutes désactivées par défaut, activées une par une. Notifications web push, plus email pour les sujets de compte.

| Notification                | Déclencheur                  | Défaut                                    |
| --------------------------- | ---------------------------- | ----------------------------------------- |
| Rappel de séance            | Jour habituel, heure choisie | Désactivé                                 |
| Rappel de pesée             | Matin, jours choisis         | Désactivé                                 |
| Dépassement de limite haute | À la saisie                  | **Activé** — c'est une alerte de sécurité |
| Fin de bloc                 | Semaine 5 ou 6               | Activé                                    |
| Mensurations                | 28 jours                     | Désactivé                                 |
| Compte et paiement          | Événement                    | Activé, non désactivable                  |

**Interdits :** aucune notification de série interrompue, aucun rappel culpabilisant, aucune relance après une période d'inactivité du type « tu nous manques ». Une personne qui arrête a ses raisons.

Plage horaire respectée : rien entre 21 h et 7 h, sauf réglage explicite.

---

## 5. Support

- **Signaler un problème** depuis n'importe quel écran : capture automatique du contexte technique, sans données de santé
- **Signaler un aliment erroné** : remontée vers OpenFoodFacts et correction locale immédiate
- **Signaler un exercice mal classé** : contre-indication manquante ou muscle mal attribué. Prioritaire — c'est un sujet de sécurité
- Centre d'aide : quelques pages, pas un wiki
- Délai de réponse annoncé et tenu

---

## 6. Interface d'administration

Nécessaire dès la V1 pour le diététicien partenaire.

- **Validation des règles** : références nutritionnelles, fourchettes, seuils, libellés. Chaque règle porte un état (brouillon, validé, date, validateur)
- **Journal des libellés** : toute formulation destinée à l'utilisateur passe par cette table, versionnée. Aucun texte nutritionnel en dur dans le code
- **Revue des sorties du modèle** : échantillon des réponses de l'assistant, avec signalement possible
- **File des signalements** : aliments, exercices, bugs
- **Tableau de bord de conformité** : nombre de rejets du filtre de sortie, escalades déclenchées, alertes TCA

Accès réservé, journalisé, avec 2FA obligatoire.
