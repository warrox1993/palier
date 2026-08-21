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

## 8. Ce que ce lot ne contient pas

Aucun envoi d'email, aucune connexion externe, aucune fusion de comptes. La règle d'autorisation « **pas de nutrition** sans email vérifié » est écrite et éprouvée — c'est une politique applicative, distincte de `RequireConfirmedEmail` qui bloquerait la connexion entière alors que l'entraînement doit rester ouvert. Le drapeau de vérification est posé par le lot 4b ; la règle qui le lit existe dès celui-ci.

Aucun message destiné à un utilisateur : les libellés vivent en base et passent par i18next.

---

## 9. Points ouverts

1. **L'adresse du proxy OVHcloud** pour `KnownProxies` — inconnue tant que l'instance n'existe pas. En attendant, la configuration est portée par variable d'environnement et l'épreuve utilise une adresse de test.
2. **La liste locale des mots de passe fréquents** : sa source exacte et son volume. Candidats — le sommet du classement HIBP par nombre d'occurrences, ou une liste publique établie. À trancher à l'implémentation, avec attribution CC BY.
3. **Le passage à 210 000 itérations** appelle une entrée datée dans `docs/decisions.md`, avec la mesure qui la fonde.

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
