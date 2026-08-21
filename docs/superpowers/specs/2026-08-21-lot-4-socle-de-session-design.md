# Lot 4 — le socle de session

**Date :** 21 août 2026
**Statut :** premier jet, en attente de relecture
**Décisions sources :** D9, D15, D16, D17, D35, D36
**Document métier :** `docs/09-comptes.md` § 1, dont le relevé des sept exigences

---

## 1. Objet et périmètre

Ce lot livre **tout ce qui ne dépend d'aucun tiers** : le magasin de sessions, la rotation des jetons de rafraîchissement, la limitation des tentatives, le validateur de mots de passe et la 2FA. Il rend le produit capable d'inscrire, de connecter, de faire tourner un jeton, de lister les sessions actives et de déconnecter tous les appareils.

**Hors périmètre, et pour un motif précis.** Google OAuth exige des identifiants à créer chez Google ; l'envoi d'emails exige un fournisseur non encore décidé (`14-contenu.md` cite Brevo et Postmark) et un compte ouvert ; la fusion de comptes dépend du premier. Les écrire sans pouvoir les exécuter donnerait deux chemins jamais franchis — ce que ce projet appelle un garde-fou qui ment. Ils partent au lot 4b.

**Ce que le lot 2 a déjà posé :** les sept tables d'Identity, `Utilisateur : IdentityUser<Guid>` (D35), et le paquet `Microsoft.AspNetCore.Identity.EntityFrameworkCore` 10.0.11.

---

## 2. Ce que la vérification a établi

Chaque élément a été recoupé contre sa source primaire le 21/08/2026. Trois résultats changent le design.

### Le hachage des mots de passe, mesuré et non deviné

`09-comptes.md` dit que l'arbitrage « se mesure, il ne se devine pas ». Deux documentations se contredisaient — la référence Microsoft annonce tantôt PBKDF2-SHA256 à 10 000 itérations, tantôt 100 000. **Le format du haché tranche.** Un mot de passe haché par `PasswordHasher<T>` de `Microsoft.Extensions.Identity.Core` 10.0.11, puis décodé octet par octet :

```
version du format : 0x01  (IdentityV3)
PRF               : 2  (HMAC-SHA512)
itérations        : 100 000
sel               : 128 bits
sous-clé          : 256 bits
```

Le document du projet avait raison, la documentation avait tort.

**Le coût, mesuré sur cette machine** (16 cœurs logiques, `Rfc2898DeriveBytes.Pbkdf2`, moyenne de cinq tours après un tour à blanc) :

| PRF     | Itérations | ms par hachage | Connexions/s sur un cœur |
| ------- | ---------: | -------------: | -----------------------: |
| SHA-512 |    100 000 |           56,7 |                     17,6 |
| SHA-512 |    210 000 |          111,2 |                      9,0 |
| SHA-256 |    600 000 |          209,4 |                      4,8 |

**Décision : relever à 210 000 itérations.** C'est ce qu'OWASP recommande pour PBKDF2-HMAC-SHA512, et le défaut d'Identity est en dessous. Le surcoût mesuré est de **54 ms par connexion**, sur un produit qui traite des données de santé relevant de l'article 9 du RGPD. Le marqueur de version en tête du haché rend l'opération sûre : les hachés existants restent vérifiables et sont recalculés à la connexion suivante.

**Ce que cette mesure ne dit pas.** Elle a été prise sur un poste de développement, pas sur l'instance OVHcloud, qui n'existe pas encore. Un vCPU mutualisé est couramment deux fois plus lent : compter **~220 ms** par connexion. Ce n'est pas un problème de débit — le produit n'aura pas neuf connexions par seconde avant longtemps — mais c'est un **vecteur de déni de service** : chaque tentative de connexion coûte 220 ms de CPU. D'où le § 5, et l'ordre dans lequel il place les contrôles.

### La rotation des jetons est une pratique normalisée, pas une invention

La **RFC 9700**, publiée en janvier 2025 comme _Best Current Practice_ de la sécurité OAuth 2.0, impose aux clients publics soit des jetons liés à leur porteur, soit la **rotation avec détection de réemploi**. Le mécanisme y est décrit tel quel : un nouveau jeton à chaque usage, et si un jeton déjà consommé se représente, c'est la signature d'un vol — toute la famille est révoquée.

Les durées viennent d'OWASP : **5 à 15 minutes** pour un jeton d'accès portant des données de santé, **7 à 30 jours au maximum** pour un jeton de rafraîchissement. Retenu : **15 minutes** et **14 jours**.

### La limitation par IP est un piège à deux faces

La documentation d'ASP.NET Core met en garde contre sa propre recommandation : « créer des partitions de limitation fondées sur l'adresse IP du client rend l'application vulnérable aux attaques par déni de service employant l'usurpation d'adresse source ».

**Le danger réel n'est pas l'usurpation TCP**, impossible sans compléter la poignée de main, mais l'en-tête `X-Forwarded-For`, que le client contrôle. Sans configuration, deux échecs symétriques :

| Configuration                         | Ce qui se passe                                                                      |
| ------------------------------------- | ------------------------------------------------------------------------------------ |
| `ForwardedHeaders` absent             | l'IP observée est celle du proxy OVH — **tout le monde dans le même seau**           |
| `X-Forwarded-For` accepté sans limite | l'attaquant change d'IP déclarée à chaque requête — **la limite ne s'applique plus** |

La seule configuration correcte accepte l'en-tête **du seul proxy connu**, et une seule fois :

```csharp
options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
options.KnownProxies.Add(/* l'adresse du proxy OVH */);
options.ForwardLimit = 1;
```

Ce réglage **s'éprouve** : une requête portant un `X-Forwarded-For` forgé, émise depuis une adresse qui n'est pas le proxy connu, doit être comptée sur son adresse réelle. Un réglage accepté n'est pas un réglage appliqué.

---

## 2 bis. Le chemin d'accès sans identité — ce que D38 laissait à concevoir

C'est la question centrale du lot, et elle était absente du premier jet de cette spec.

**Le problème, énoncé par D38 elle-même :** « le chemin de connexion lit `AspNetUsers` par email **avant** que la moindre identité existe ; il ne peut donc pas passer par `app.utilisateur()` ». Le lot 2 a fermé la porte — `enable` **et** `force row level security` sans aucune politique, donc refus par défaut — précisément pour que le lot 4 **conçoive** ce chemin au lieu de le découvrir.

Trois obstacles se cumulent aujourd'hui, tous éprouvés :

| Obstacle                                                                                | Preuve                                                               |
| --------------------------------------------------------------------------------------- | -------------------------------------------------------------------- |
| `palier_app` n'a **aucun privilège** sur les tables `AspNet*`                           | refus `42501`, épreuve 8 d'`IsolationTests`                          |
| Le propriétaire lui-même est bloqué par `force` sans politique                          | zéro ligne visible, même épreuve                                     |
| `ExecuteurDeCasDUsage` **refuse** avant d'ouvrir la transaction quand l'identité manque | D36, et `ArchitectureTests` interdit tout autre accès au `DbContext` |

### La décision : un quatrième rôle

D37 en a déjà trois — `palier_app`, `palier_migrations`, `palier_sauvegarde` — chacun avec sa chaîne et son périmètre. **`palier_auth` est le quatrième**, et il suit exactement la même forme : il ne possède aucune table, il ne contourne pas RLS, et ses privilèges sont accordés objet par objet.

```sql
-- Ne possède rien, pas de BYPASSRLS, privilèges nommés un par un.
create role palier_auth login password :'mdp';

grant select, insert, update, delete on
  public."AspNetUsers", public."AspNetUserTokens", public."AspNetUserLogins",
  public."AspNetUserClaims", public."AspNetUserRoles",
  public.sessions_refresh
  to palier_auth;

-- La politique désigne le RÔLE, jamais une variable de session.
create policy authentification on public."AspNetUsers"
  for all to palier_auth using (true) with check (true);
```

**Ce qui a été écarté, et pourquoi.**

Les **fonctions `SECURITY DEFINER`**, que D38 citait comme seconde voie, butent sur deux obstacles. ASP.NET Identity interroge le `DbContext` en LINQ à travers `UserStore<TUser>` : l'y brancher exigerait de réimplémenter `IUserStore`, `IUserPasswordStore`, `IUserEmailStore`, `IUserLockoutStore` et `IUserTwoFactorStore` — un magasin entier, réécrit à la main, sur le chemin le plus sensible du produit. Et chaque fonction devrait porter `SET search_path` sous peine de rouvrir **CVE-2018-1058**, où un objet homonyme créé dans un schéma accessible détourne la fonction vers du code choisi par l'attaquant.

Le **drapeau de contexte en session** — `set_config('app.contexte', 'authentification')` — a été écarté pour une raison de fond : la barrière ne dépendrait plus du **rôle** mais d'une variable que le code applicatif contrôle. Un `FromSqlRaw` distrait, et toute la protection des tables d'identité tombe d'un coup, sans que rien le signale. C'est l'esprit de l'interdit de D38 — « jamais une pose de l'identité d'autrui ».

### Ce que le quatrième rôle impose

**L'assertion de démarrage doit le contrôler aussi.** D37 vérifie aujourd'hui que le rôle de la chaîne `Palier` ne contourne pas RLS et ne possède aucune table de `public`. Ajouter un rôle sans étendre ce contrôle reviendrait à ouvrir un accès **sans le garde-fou qui le surveille** — exactement ce que le lot 1 a payé quatre fois. Les trois requêtes de D37 s'appliquent donc à `palier_auth` : pas de `BYPASSRLS`, aucune table possédée, et refus de servir sinon.

**Une seconde exemption nommée** au test d'architecture, pour le contexte d'authentification. `ArchitectureTests` porte déjà `Palier.Api.Socle.LecteurDeSocle` avec son écriteau, et une épreuve refuse le dépôt le jour où un type exempté disparaît. La nouvelle exemption suit la même forme et porte son motif en toutes lettres.

**Le périmètre du rôle est le minimum vital, et il reste large sur une table.** `palier_auth` voit toutes les lignes d'`AspNetUsers` — c'est inhérent au problème : chercher un compte par email avant de savoir qui se présente exige de pouvoir lire la table. Aucune des trois voies ne l'évite. Ce qui change entre elles, c'est **qui** détient ce pouvoir : ici un rôle dédié, dont c'est la seule fonction, et qui n'a aucun privilège sur les données de santé.

### Ce que cela laisse à trancher, hors de ce lot

D38 signalait une contradiction **non résolue** : `08-workflow.md` § 6 exige « test RLS vert pour chaque table », mais les tables d'identité, les catalogues publics et les tables possédées par jointure n'entrent pas dans le modèle « A ne lit jamais une ligne de B ». La liste des tables hors de ce modèle doit être **nommée dans `docs/03-donnees.md`**, avec la forme de politique de chacune. C'est du contenu métier, il appartient au porteur du projet, et il reste ouvert.

---

## 3. Le magasin de sessions

C'est le cœur du lot, et la seule exigence des sept qu'Identity ne couvre **pas du tout**.

### La table

| Colonne             | Rôle                                                                      |
| ------------------- | ------------------------------------------------------------------------- |
| `id`                | clé, `uuid`                                                               |
| `utilisateur_id`    | vers `AspNetUsers(Id)`, `uuid` (D35)                                      |
| `empreinte`         | **SHA-256 du jeton**, jamais le jeton                                     |
| `famille_id`        | partagée par toute la chaîne issue d'une même connexion                   |
| `cree_le`           | horodatage de création                                                    |
| `expire_le`         | création + 14 jours                                                       |
| `consomme_le`       | posé à la rotation — c'est lui qui rend la détection de réemploi possible |
| `revoque_le`        | posé par une déconnexion ou par la révocation d'une famille               |
| `remplace_par`      | vers le jeton suivant de la chaîne, pour l'audit                          |
| `appareil`          | user-agent, pour la liste des sessions actives                            |
| `derniere_activite` | pour l'affichage                                                          |

**Le jeton n'est jamais stocké en clair.** SHA-256 suffit ici et PBKDF2 serait une erreur : un jeton de rafraîchissement est une valeur aléatoire de 256 bits, pas un mot de passe — il n'y a rien à deviner, donc rien à ralentir. Y appliquer 210 000 itérations coûterait 220 ms à chaque rafraîchissement pour une protection nulle.

### La rotation, et ce qu'elle détecte

```
présentation d'un jeton
      │
      ├─ introuvable ────────────────▶ refus
      ├─ révoqué ou expiré ──────────▶ refus
      ├─ DÉJÀ CONSOMMÉ ──────────────▶ refus + RÉVOCATION DE TOUTE LA FAMILLE
      └─ valide ─────────────────────▶ consommé, nouveau jeton dans la même famille
```

**La troisième branche est la raison d'être du mécanisme.** Un jeton déjà consommé qui se représente signifie que deux porteurs détiennent la même chaîne : le légitime et un voleur. Lequel des deux se présente n'a pas d'importance — les deux perdent l'accès, et l'utilisateur se reconnecte.

### La fenêtre de grâce, sans laquelle la rotation déconnecte les innocents

**Le défaut est réel, connu, et il frappe des utilisateurs légitimes.** Deux requêtes de rafraîchissement concurrentes portant le même jeton — un second onglet, un rejeu réseau, une reprise de connexion — le voient toutes deux valide, le consomment toutes deux, et la seconde déclenche la détection de réemploi. L'utilisateur est déconnecté sans qu'aucun vol n'ait eu lieu. Auth.js, better-auth et les intégrateurs OAuth en portent tous des rapports.

**La parade retenue : une fenêtre de grâce de 30 secondes.** Un jeton déjà consommé, présenté dans les trente secondes suivant sa consommation, rend **le jeton qui l'a remplacé** au lieu de révoquer la famille. Au-delà, la détection mord normalement.

Trente secondes est le défaut d'Okta, qui laisse régler de 0 à 60. Je m'aligne sur cette référence plutôt que d'inventer une valeur : elle est assez courte pour qu'un jeton volé ne serve pas — un attaquant qui rejoue à la seconde près est déjà dans la fenêtre de course, pas dans une exploitation — et assez longue pour couvrir un aller-retour réseau dégradé.

**Ce que la fenêtre ne dégrade pas :** hors de ces trente secondes, la sémantique de détection reste entière. Un jeton consommé il y a une heure et rejoué révoque toujours toute la famille.

### La politique RLS de cette table

`sessions_refresh` vit dans `public`, donc l'assertion de démarrage exige `enable` **et** `force row level security`. Sa politique désigne **`palier_auth` et lui seul** — même forme que les tables `AspNet*` du § 2 bis, pour le même motif : c'est le rôle qui décide, jamais une variable de session.

`palier_app` n'a **aucun privilège** sur cette table. Une session porte l'empreinte d'un jeton et l'appareil de son porteur : elle n'a rien à faire sur le chemin des données de santé.

**Et c'est ce magasin qui rend la déconnexion immédiate.** `09-comptes.md` le relève : le `SecurityStamp` d'Identity ne produit qu'un effet **différé**, borné par l'intervalle de revalidation ou par la durée du jeton d'accès. Révoquer les familles en base agit au rafraîchissement suivant, soit au plus tard quinze minutes — et immédiatement pour tout ce qui passe par le magasin.

---

## 4. Le jeton d'accès et le cookie

**Jeton d'accès** : JWT signé par `JWT_SIGNING_KEY`, quinze minutes, portant l'identifiant de l'utilisateur. C'est lui qui alimente `set_config('app.utilisateur', …)` de D36 — l'identité vient du jeton vérifié, jamais de la requête.

**Jeton de rafraîchissement** : valeur aléatoire de 256 bits, transportée en **cookie**, jamais dans le corps JSON. Les points d'entrée natifs `MapIdentityApi` rendent leurs jetons en JSON ; ce document exige l'inverse, et c'est l'une des raisons pour lesquelles ils ne sont pas utilisés tels quels.

Les attributs, permis par D16 qui met front et API sous le même domaine :

| Attribut   | Valeur                       | Motif                                                              |
| ---------- | ---------------------------- | ------------------------------------------------------------------ |
| `HttpOnly` | oui                          | inaccessible au JavaScript, donc au vol par injection              |
| `Secure`   | oui                          | jamais en clair                                                    |
| `SameSite` | `Strict`                     | possible grâce au même domaine ; supprime la surface CSRF          |
| `Path`     | la route de rafraîchissement | le cookie ne part pas sur les autres requêtes — moins d'exposition |

---

## 5. La limitation, dans cet ordre

L'ordre compte, parce que le hachage coûte 220 ms : **tout ce qui peut refuser avant lui doit refuser avant lui.**

1. **Par adresse IP** — `PartitionedRateLimiter` partitionné sur `RemoteIpAddress`, sous réserve du § 2. S'exécute avant que la requête n'atteigne le contrôleur.
2. **Par compte** — `MaxFailedAccessAttempts = 5`, avec `lockoutOnFailure: true`.
3. **Le hachage** — en dernier.

### Les trois manques d'Identity, et ce qu'ils deviennent

| Ce que le document exige        | Ce qu'Identity donne                   | Ce qui est écrit                                                            |
| ------------------------------- | -------------------------------------- | --------------------------------------------------------------------------- |
| 5 tentatives **sur 15 minutes** | un compteur cumulatif, sans fenêtre    | une fenêtre glissante : les échecs antérieurs à 15 minutes ne comptent plus |
| verrouillage **progressif**     | `DefaultLockoutTimeSpan`, durée unique | une escalade — 5 min, 15, 60, puis palier                                   |
| limitation **par IP**           | rien, c'est hors d'Identity            | le limiteur du § 5.1                                                        |

### Ce que le limiteur ne sait pas faire, et qui doit être écrit quelque part

**Le limiteur d'ASP.NET Core compte en mémoire de processus.** Sur plusieurs répliques derrière un répartiteur, chacune tient son propre compteur : trois répliques triplent la limite réelle. Le déploiement OVHcloud doit donc rester **à une seule instance**, ou passer à un compteur partagé le jour où il grandit. Ce n'est pas un défaut du code : c'est une **condition d'exploitation**, et elle appartient au lot de déploiement.

---

## 6. Le validateur de mots de passe, à deux étages

`09-comptes.md` laisse ouverte la question du comportement quand HaveIBeenPwned est injoignable — échec ouvert ou fermé. **La question repose sur une prémisse fausse.** ASVS 5.0 autorise le contrôle « localement **ou** via une API » : rien n'oblige à ne dépendre que du réseau.

| Étage                        | Contenu                                                             | Peut-il échouer ?      |
| ---------------------------- | ------------------------------------------------------------------- | ---------------------- |
| **1 — local, obligatoire**   | les mots de passe fréquents, en base, chargés par un seed versionné | **non** — aucun réseau |
| **2 — réseau, opportuniste** | l'API k-anonymat de HIBP, délai d'attente court                     | oui, sans conséquence  |

Si HIBP ne répond pas, l'étage 1 a déjà tranché. Le contrôle **n'échoue jamais ouvert**, et ne bloque jamais les inscriptions.

**Pourquoi l'étage local suffit à porter le risque.** Le bourrage d'identifiants représente 22 % des fuites en 2024-2025, premier vecteur devant l'hameçonnage, et il s'appuie sur les mots de passe **fréquents** — exactement ce que couvre une liste locale. NIST SP 800-63B révision 4, finalisée en juillet 2025, vise « _commonly-used, expected, or compromised_ » : le _commonly-used_ est le cœur du risque. Un mot de passe vu une seule fois dans une fuite n'est pas devinable ; l'étage 2 l'ajoute quand il le peut.

**Pourquoi pas la liste complète.** Environ 30 Go, disproportionné pour un VPS au démarrage. Les données HIBP sont sous **CC BY 4.0** : l'attribution est obligatoire et sera portée par `17-donnees-sources.md`, qui gère déjà ce cas pour d'autres sources.

Point d'extension : `IPasswordValidator<Utilisateur>`, enregistré par `AddPasswordValidator<T>`, appelé avant tout enregistrement de haché. La longueur minimale passe de 6 à **10** (`PasswordOptions.RequiredLength`).

---

## 7. La 2FA par TOTP

Identity fournit toute la cryptographie — clé partagée, vérification, codes de récupération, mémorisation d'appareil. Il ne fournit **pas** le QR code, et sa documentation le dit.

**Le backend rend l'URI, le front dessine le QR.** Aucune dépendance ajoutée côté serveur :

```
otpauth://totp/Palier:{email}?secret={base32}&issuer=Palier&algorithm=SHA1&digits=6&period=30
```

**`algorithm=SHA1` est délibéré, et c'est contre-intuitif.** SHA-1 est proscrit ailleurs, mais ici il n'est pas utilisé comme fonction de hachage résistante aux collisions : c'est le HMAC d'un compteur de trente secondes, dont la sécurité ne dépend pas de cette propriété. C'est surtout le **seul algorithme que toutes les applications d'authentification acceptent** — SHA-256 et SHA-512 sont prévus par la RFC 6238 mais inégalement implémentés, et un utilisateur dont l'application refuse le QR code perd l'accès à son compte. Six chiffres, trente secondes.

**Le code TOTP ne se journalise jamais** : il reste valable plusieurs authentifications avant d'expirer.

---

## 7 bis. Le cycle de vie des sessions

### La purge des sessions éteintes

Une table de sessions qui ne se vide jamais grossit indéfiniment, et chaque ligne morte est une empreinte de jeton conservée sans raison. Une **commande idempotente** supprime les lignes dont `expire_le` ou `revoque_le` a dépassé une rétention courte.

**Pas de tâche de fond dans ce lot.** Un ordonnanceur interne serait un mécanisme de plus à surveiller, et il n'appartient pas au socle de session : la commande est appelée par le déploiement, qui sait déjà lancer des migrations. Idempotente, donc rejouable sans dommage.

### La suppression de compte

RGPD article 17 : l'effacement se fait « sans délai indu ». Les sessions d'un compte supprimé **partent avec lui** — la contrainte de clé étrangère vers `AspNetUsers` porte `on delete cascade`, et une épreuve vérifie qu'aucune session ne survit à son utilisateur.

**Elle se fait en deux temps, et l'ordre compte.** Le pipeline vérifie d'abord l'identité du demandeur — on ne supprime que son propre compte — puis le service d'identité, sous `palier_auth`, exécute la suppression. `palier_app` n'a aucun privilège sur `AspNetUsers` et ne peut donc pas la faire lui-même : c'est la conséquence directe du § 2 bis, et elle est voulue.

---

## 7 ter. Les projets de tests, et leurs seuils

`CLAUDE.md` § 4 est catégorique : « tout projet backend sans seuil de couverture n'a **aucune détection de code mort public** […] chaque projet qui acquiert des tests acquiert son seuil dans le même geste ». Aujourd'hui, seul `Palier.Domain.Tests` en porte un. Ce lot écrit dans `Application`, `Infrastructure` et `Api` — **trois projets sans filet**.

| Projet créé                   | Seuil       | Motif                                                                                                         |
| ----------------------------- | ----------- | ------------------------------------------------------------------------------------------------------------- |
| `Palier.Application.Tests`    | **100 %**   | Cas d'usage et validation : de la logique pure, testable sans simulacre, comme `Domain`                       |
| `Palier.Infrastructure.Tests` | à la mesure | Adaptateurs EF Core et Identity. Annoncer 100 % serait une promesse que du code d'infrastructure ne tient pas |
| `Palier.Api.Tests`            | à la mesure | Tests d'intégration sur base réelle, via Testcontainers comme `Palier.Database.Tests`                         |

**Les deux seuils « à la mesure » se fixent une fois le code écrit, jamais avant** — et ils se fixent **au niveau atteint**, pas en dessous. Un seuil posé sous la couverture réelle est un seuil qui ne mord pas : il laisse la couverture redescendre sans rien dire.

---

## 8. Ce que ce lot ne contient pas

Aucun envoi d'email, aucune connexion externe, aucune fusion de comptes. La règle d'autorisation « **pas de nutrition** sans email vérifié » est écrite et éprouvée — c'est une politique applicative, distincte de `RequireConfirmedEmail` qui bloquerait la connexion entière alors que l'entraînement doit rester ouvert. Le drapeau de vérification est posé par le lot 4b ; la règle qui le lit existe dès celui-ci.

Aucun message destiné à un utilisateur : les libellés vivent en base et passent par i18next.

**Aucun écran non plus.** Ce lot est backend, et il est vérifiable de bout en bout **au sens des tests d'intégration** — inscription, connexion, rotation, réemploi détecté, déconnexion de tous les appareils — pas au sens où vous pourriez vous connecter dans un navigateur. Les écrans appartiennent au lot 6, qui porte le socle d'écran ; les livrer ici imposerait de concevoir la direction visuelle de l'authentification avant celle du produit.

---

## 9. Points ouverts

### À trancher à l'implémentation — de mon ressort

1. **La liste locale des mots de passe fréquents** : sa source exacte et son volume. Candidats — le sommet du classement HIBP par nombre d'occurrences, ou une liste publique établie. Avec attribution CC BY.
2. **Les seuils de couverture de `Infrastructure` et `Api`**, fixés au niveau atteint une fois le code écrit (§ 7 ter).

### À trancher par le porteur du projet

3. **La liste des tables hors du modèle « A ne lit jamais une ligne de B »**, à nommer dans `docs/03-donnees.md` avec la forme de politique de chacune. D38 signalait déjà cette contradiction avec `08-workflow.md` § 6 sans la résoudre — elle reste ouverte, et ce lot ajoute `sessions_refresh` à la liste des tables concernées.

### Bloqué par l'extérieur

4. **L'adresse du proxy OVHcloud** pour `KnownProxies` — inconnue tant que l'instance n'existe pas. La configuration est portée par variable d'environnement, et l'épreuve utilise une adresse de test. **Tant qu'elle n'est pas renseignée en exploitation, la limitation par IP ne protège pas** : c'est à vérifier au déploiement, pas ici.

### Décisions à consigner

5. Quatre entrées datées dans `docs/decisions.md` : le **quatrième rôle** `palier_auth` et ce qu'il écarte ; le passage à **210 000 itérations** avec la mesure qui le fonde ; la **fenêtre de grâce de 30 s** ; et les **seuils de couverture** des trois nouveaux projets.

---

## 10. Sources

- [RFC 9700 — OAuth 2.0 Security Best Current Practice](https://www.scalekit.com/blog/oauth-2-0-best-practices-rfc9700) — janvier 2025
- [OWASP Password Storage Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Password_Storage_Cheat_Sheet.html) — 210 000 itérations pour PBKDF2-HMAC-SHA512
- [OWASP Session Management Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Session_Management_Cheat_Sheet.html)
- [OWASP ASVS 5.0 — Authentication](https://github.com/OWASP/ASVS/blob/master/5.0/en/0x15-V6-Authentication.md) — contrôle « localement ou via une API »
- [NIST SP 800-63B révision 4](https://www.enzoic.com/blog/nist-sp-800-63b-rev4/) — finalisée en juillet 2025
- [ASP.NET Core — limitation de débit](https://github.com/dotnet/aspnetcore.docs/blob/main/aspnetcore/performance/rate-limit.md)
- [ASP.NET Core — proxy et répartiteur de charge](https://github.com/dotnet/aspnetcore.docs/blob/main/aspnetcore/host-and-deploy/proxy-load-balancer.md)
- [Have I Been Pwned — Pwned Passwords](https://haveibeenpwned.com/Passwords) — CC BY 4.0
- [RFC 6238 — TOTP](https://www.authgear.com/post/what-is-totp/)
