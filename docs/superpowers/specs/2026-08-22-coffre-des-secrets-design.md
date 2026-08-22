# Le coffre des secrets — OVHcloud KMS

**Date :** 22 août 2026
**Statut :** premier jet, en attente de relecture
**Décisions sources :** D12, D15, D36, D37, D38, D57, D58
**Documents métier :** `docs/01-conformite.md` § 4, `docs/09-comptes.md` § 1, `docs/13-juridique.md`

---

## 1. Objet et périmètre

Ce document livre deux choses que rien ne relie sinon le coffre qui les porte.

**Les secrets de configuration quittent l'environnement du serveur.** Les quatre chaînes de connexion PostgreSQL, la clé de signature des jetons et le secret client Google vivent aujourd'hui dans des variables d'environnement. Ils passent au Secret Manager d'OKMS, lus une fois au démarrage.

**Le secret TOTP de chaque utilisateur est chiffré.** L'audit de sécurité de la nuit du 21 au 22 août a haché les codes de récupération et laissé la clé partagée TOTP en clair dans `AspNetUserTokens` — D58 le consigne explicitement, faute d'une décision sur la clé de chiffrement. Ce document est cette décision.

**Pourquoi OVHcloud et pas Azure.** La demande initiale nommait Azure Key Vault. D15 pose l'hébergement chez OVHcloud pour une raison de fond : fournisseur européen, hors portée du CLOUD Act, ce qui compte pour des données de santé relevant de l'article 9. Confier les clés qui protègent ces données à un fournisseur américain aurait vidé D15 de son sens — la donnée serait restée en Europe pendant que la clé qui l'ouvre aurait basculé sous une autre juridiction.

**Hors périmètre, et pour un motif.** Le chiffrement des données de santé au repos reste au chiffrement de volume PostgreSQL : le faire au niveau applicatif rendrait toute requête d'agrégation impossible, et ce n'est pas ce que l'audit a signalé. La rotation automatique n'est pas écrite — la rotation manuelle l'est, et c'est ce qui manquait. KMIP n'est pas utilisé : REST suffit et se teste plus simplement.

---

## 2. Ce que la sonde a établi

Aucune de ces valeurs n'est reprise d'une documentation. Une sonde jetable les a mesurées le 22/08/2026 contre le domaine réel, après que la politique IAM eut été posée.

**L'URL du jeton OAuth2 n'est documentée nulle part.** Trois candidats sur le domaine `ovhcloud.com` rendent un `404` en HTML — la page marketing, pas une erreur d'API. La bonne adresse est :

```
https://www.ovh.com/auth/oauth2/token
```

Un jeton y vit **3 599 secondes**, soit 59 minutes.

**Ce que `datakey` produit.** Un `201` et deux champs :

```json
{ "key": "eyJhbGciOiJkaXIi…", "plaintext": "HcLxC2yurpdz4UFS8mNHVmxwNx6RaiLI5ultaBqfTJc=" }
```

`plaintext` fait 32 octets — une clé AES-256. `key` fait 203 signes et c'est un **JWE compact**, dont l'en-tête décodé dit :

```json
{ "alg": "dir", "enc": "A256GCM", "kid": "23b5f0cd-…", "x-key-ver": 0 }
```

**Ce `x-key-ver` décide de la rotation.** OVH versionne la clé maîtresse et fait voyager le numéro de version _dans_ l'enveloppe. Une rotation côté OVH produira des enveloppes en version 1 pendant que les anciennes resteront en version 0, et le coffre saura déchiffrer les deux. La rotation de la clé maîtresse ne demande donc aucun code de notre part — le format la porte. Ce que nous devons écrire, c'est la rotation de la **clé de données**, qui est un autre problème (§ 7).

**Le coût du déchiffrement**, dix mesures après deux tours de chauffe :

|         |           |
| ------- | --------- |
| médiane | **30 ms** |
| minimum | 27 ms     |
| maximum | 33 ms     |

**Le coffre injoignable** rend une `HttpRequestException` en 91 ms. L'échec est net et rapide ; il n'y a pas de pendaison à craindre sur ce cas-là.

**Il n'existe aucun SDK .NET.** OVH publie une interface en ligne de commande et un SDK Go. L'API REST s'écrit donc à la main — ce qui, vu les trois appels dont nous avons besoin, coûte moins qu'une dépendance de plus (la doctrine des dépendances de `CLAUDE.md` § 4).

---

## 3. La politique IAM, telle qu'elle est posée

Le domaine `d0624881-…` est à Roubaix. La clé de service `4b73224e-…` est une AES-256 active. Un compte de service `palier-api` porte la politique `palier-api-kms` : **une identité, une ressource, sept actions** sur les cinquante-neuf que le produit expose.

Les identifiants sont tronqués ici volontairement. Ils ne sont pas des secrets — sans `OKMS_CLIENT_SECRET` ils n'ouvrent rien — mais ils désignent une infrastructure réelle, et un dépôt n'est pas l'endroit où offrir de la reconnaissance. Les valeurs entières vivent dans `back/.env`, que `.gitignore` refuse.

```
okms:apikms:secret/create               créer un secret
okms:apikms:secret/version/create       y écrire une valeur
okms:apikms:secret/get                  lire ses métadonnées
okms:apikms:secret/version/getData      lire sa valeur
okms:apikms:serviceKey/dataKey/create   demander une clé de données
okms:apikms:serviceKey/dataKey/decrypt  déballer une enveloppe
okms:apikms:serviceKey/get              lire les métadonnées de la clé
```

**Ce qu'elle refuse, et c'est le point.** Aucune suppression — ni `secret/delete`, ni la moindre action de la catégorie `DELETE`. Aucune modification. Aucune signature. Un compte de service volé permet de lire et d'écrire ; il ne permet pas de **rendre les données illisibles**, qui est le dommage irréversible.

Trois familles d'actions ont été écartées en bloc : `okms:apiovh:*` (la même chose par l'autre API — une seule voie suffit), `okms:kmip:*` (protocole non utilisé), et les quinze _groupes de permissions managées_ du type `globalReadWrite`, qui portent sur le compte OVHcloud entier et non sur le KMS.

**Une limite à connaître : la politique ne peut pas restreindre par chemin de secret.** La ressource est le domaine OKMS entier. Un compte de service qui lit `palier/dev` peut lire `palier/prod`. C'est ce qui rend le point ouvert n° 1 (§ 10) réel.

---

## 4. Ce qui va où

La règle de partage tient en une phrase : **le coffre ne reçoit que ce qui est secret**. Y mettre `TRUSTED_PROXIES` ou `LLM_DEFAULT_PROVIDER` ajouterait un appel réseau et une dépendance pour protéger une valeur qui n'a rien à cacher.

### Ce qui reste dans l'environnement du serveur

```
OKMS_ENDPOINT · OKMS_ID · OKMS_KEY_ID · OKMS_CLIENT_ID · OKMS_CLIENT_SECRET
ASPNETCORE_ENVIRONMENT      décide du chemin lu : dev ou prod
```

Plus ce qui n'est pas secret : `TRUSTED_PROXIES`, `GOOGLE_OAUTH_CLIENT_ID` (public par construction dans OAuth — il apparaît dans l'URL de redirection), `LLM_DEFAULT_PROVIDER`, `LLM_MONTHLY_BUDGET_EUR`, `OPENFOODFACTS_USER_AGENT`.

**`OKMS_CLIENT_SECRET` ne peut pas aller au coffre : c'est la clé du coffre.** Un secret restera donc toujours dans l'environnement. Le gain n'est pas « zéro secret local » mais « un seul secret local au lieu de huit, et celui-là ne donne accès qu'à sept actions ».

### Ce qui va au Secret Manager

L'action s'appelle _« Read KV engine configuration »_ : OKMS est un moteur clé-valeur, un chemin porte **plusieurs paires**. On l'exploite — deux chemins, six paires chacun :

```
palier/dev                              palier/prod
├── ConnectionStrings__Palier           └── les six mêmes clés,
├── ConnectionStrings__PalierAuth           valeurs de production
├── ConnectionStrings__PalierMigrations
├── ConnectionStrings__PalierSauvegarde
├── JWT_SIGNING_KEY
└── GOOGLE_OAUTH_CLIENT_SECRET
```

Un seul appel `secret/version/getData` rapporte les six. Le changement de configuration devient **atomique** : une nouvelle version porte l'ensemble cohérent, jamais un état intermédiaire où deux chaînes se contredisent.

Les clés Stripe et celles des modèles n'y sont pas : elles n'existent pas encore. Elles y entreront le jour où elles serviront.

### Ce qui va en base

L'enveloppe de la clé de données — le JWE — dans une table dédiée `cles_de_donnees` : identifiant, enveloppe, date de création.

**Et chaque secret TOTP chiffré porte l'identifiant de la clé qui l'a chiffré — non pas dans une colonne, mais dans la valeur elle-même** (§ 6). Le secret vit dans `AspNetUserTokens`, une table d'Identity : y ajouter une colonne obligerait à dériver le modèle d'Identity pour un besoin qui tient dans une chaîne. Le format `v1:{identifiant de clé}:…` porte l'information là où elle se lit.

Elle ne sert à rien aujourd'hui, puisqu'il n'y a qu'une clé. Elle est écrite quand même, parce que c'est le seul moment où elle est gratuite : sans elle, changer de clé plus tard obligerait à déchiffrer et réécrire tous les secrets dans une migration unique qui ne peut pas échouer à moitié. C'est l'exception que `CLAUDE.md` autorise à YAGNI — pas un besoin supposé, une porte qu'on ne peut plus percer après coup.

---

## 5. La séquence de démarrage

```
1. Lire les variables d'amorçage de l'environnement
2. Obtenir un jeton OAuth2 auprès de www.ovh.com          ~200 ms
3. Lire palier/{env} → les six paires                      ~30 ms
4. Ouvrir PostgreSQL avec la chaîne qui vient d'arriver
5. Contrôles RLS existants (rôle, propriété, FORCE)
6. Lire les enveloppes en base, ou en créer une si la table est vide
7. datakey/decrypt sur chacune → le trousseau, en mémoire   ~30 ms/clé
```

Environ un quart de seconde ajouté à la mise en service. Rien de tout cela ne tombe dans une requête.

**Le point d'intégration compte plus que la séquence.** Les étapes 1 à 3 s'écrivent comme un **fournisseur de configuration** ASP.NET Core branché sur `builder.Configuration`. Conséquence : `GetConnectionString("Palier")` continue de fonctionner exactement comme aujourd'hui, et **aucune autre ligne du backend ne change**. Le coffre devient une source de configuration parmi d'autres, pas une dépendance qui se propage — ce qui rend l'ensemble réversible : retirer le coffre, c'est retirer une source.

Les étapes 6 et 7 sont d'une autre nature : elles ne produisent pas de la configuration mais un service. Elles vivent dans un service hébergé qui s'exécute avant l'ouverture du port ; une exception y empêche le démarrage sans code particulier.

### Cinq refus nouveaux

Ils s'ajoutent aux cinq déjà en place (D37 pour RLS, longueur de `JWT_SIGNING_KEY`, propriété des tables, `palier_auth`).

| Quand                         | Ce que le journal dit                                                    |
| ----------------------------- | ------------------------------------------------------------------------ |
| Une variable `OKMS_*` absente | son nom, et rien d'autre                                                 |
| Le jeton OAuth2 refusé        | que l'authentification a échoué — jamais l'identifiant, jamais le secret |
| `palier/{env}` introuvable    | le chemin demandé et l'environnement                                     |
| Une des six clés manque       | **le nom de la clé manquante**, jamais la valeur des autres              |
| `datakey/decrypt` échoue      | l'identifiant de l'enveloppe, pas son contenu                            |

Aucune valeur de secret ne part au journal — ni entière, ni tronquée, ni hachée. Un secret tronqué reste un secret amputé, et sur une chaîne de connexion les huit premiers signes disent déjà l'hôte.

### Trois décisions à l'intérieur

**Le jeton dure 59 minutes, et on ne le renouvellera jamais.** Après l'étape 7, l'API n'a plus rien à demander au coffre : la configuration est chargée, le trousseau est en mémoire. Le client OKMS meurt avec le démarrage. Rien à rafraîchir, aucune expiration à gérer, aucun cache à invalider — un changement de secret passe par un redémarrage, ce qui est déjà le cas aujourd'hui.

**Aucune reprise sur échec.** La tentation serait de réessayer trois fois. Non : l'API sort en erreur et l'orchestrateur de conteneurs la relance — c'est son travail, il le fait déjà, et mieux qu'une boucle écrite à la main. Écrire la reprise dupliquerait une logique existante et ajouterait une branche à éprouver pour rien.

**Un délai maximal explicite de dix secondes sur toute la séquence coffre.** La sonde a montré qu'un coffre injoignable échoue en 91 ms. Mais un coffre _lent_ est un cas différent : sans plafond, l'API pendrait au démarrage — un état pire qu'un refus, parce qu'il ne déclenche aucune alerte.

---

## 6. Le chiffrement du secret TOTP

### Le point d'interception, et pourquoi ce n'est pas l'évident

La voie apparente serait de surcharger `ResetAuthenticatorKeyAsync`, d'appeler `base`, puis de relire et de chiffrer. Elle est fausse : `base` génère la clé **et l'écrit en clair** avant de rendre la main. La clé en clair toucherait la table, donc le journal d'écriture anticipée de PostgreSQL, donc la réplication et les sauvegardes physiques — et elle y resterait bien après l'écrasement de la ligne, jusqu'au passage du `VACUUM`.

Vérifié à la source (`Microsoft.Extensions.Identity.Core` 10.0.0) : `GetAuthenticatorKeyAsync` et `ResetAuthenticatorKeyAsync` sont `public virtual` sur `UserManager<TUser>`, mais **`SetAuthenticatorKeyAsync` n'existe pas sur `UserManager`** — seul le magasin le porte. Il n'y a donc aucun point d'interception à l'écriture dans la classe déjà dérivée.

La bonne voie n'écrit jamais de clair, et n'ajoute aucun type :

```csharp
public override async Task<IdentityResult> ResetAuthenticatorKeyAsync(Utilisateur utilisateur)
{
    var cle = GenerateNewAuthenticatorKey();
    await SetAuthenticationTokenAsync(
        utilisateur, "[AspNetUserStore]", "AuthenticatorKey", Chiffrer(cle, utilisateur.Id));
    return await UpdateSecurityStampAsync(utilisateur);
}
```

C'est **exactement le motif déjà en place** dans `GestionnaireDUtilisateurs` pour les codes de récupération — mêmes constantes, même classe, même mécanisme. Aucune exemption d'architecture à ajouter : dériver le magasin aurait exigé une troisième entrée dans `_exemptionsNommees` d'`ArchitectureTests`. Et `AuthenticatorTokenProvider` continue de fonctionner sans rien savoir : il appelle `GetAuthenticatorKeyAsync`, que nous surchargeons pour déchiffrer.

### Le format en base

```
v1:{identifiant de clé}:{nonce}:{chiffré et son étiquette}      en Base64Url
```

AES-GCM-256, nonce de 12 octets tiré au hasard à chaque chiffrement, étiquette de 16 octets.

Le préfixe `v1:` n'est pas décoratif : il rend la détection du clair **explicite** au lieu d'heuristique. Une clé TOTP en clair est du Base32, et « ça ressemble à du Base32 donc c'est du clair » est le genre de test qui se trompe un jour sur un cas limite. Ce qui ne commence pas par `v1:` est en clair, sans interprétation.

### Les données associées : l'identifiant de l'utilisateur

AES-GCM permet de lier un chiffré à un contexte sans le chiffrer. On y met l'identifiant du propriétaire.

Conséquence concrète : quelqu'un qui obtient un accès en écriture à la base **ne peut pas copier le secret TOTP d'un compte vers un autre** pour s'y connecter — le déchiffrement échoue parce que le contexte ne correspond plus. Sans cette liaison, un chiffré serait une valeur transportable, et le chiffrement n'aurait protégé que contre la lecture.

### La migration de l'existant

Au premier `GetAuthenticatorKeyAsync`, une valeur sans préfixe `v1:` est du clair hérité : on la chiffre, on la réécrit, on rend la clé. La connexion suivante est normale, la migration est invisible, et elle ne se produit qu'une fois par utilisateur. C'est le mécanisme que l'audit a déjà employé pour les codes de récupération — il est éprouvé.

**Ce que cette migration ne répare pas.** Les secrets déjà en base ont laissé leur trace en clair dans le journal d'écriture anticipée. Rien dans l'application ne peut l'effacer : ces octets y sont déjà. La réparation appartient à l'exploitation — faire tourner le WAL et reprendre une sauvegarde neuve après la migration. C'est consigné au § 10.

---

## 7. Le trousseau : amorçage, concurrence, rotation

### Amorçage

Au démarrage, l'API lit **toutes** les lignes de `cles_de_donnees` et déballe chacune par `datakey/decrypt` — 30 ms par clé. Elle en garde un dictionnaire `identifiant → 32 octets` en mémoire. Si la table est vide, elle demande une clé par `datakey/create` et range l'enveloppe.

Les nouveaux secrets sont chiffrés avec la **plus récente** ; n'importe lequel se déchiffre, puisque chacun porte l'identifiant de sa clé.

### La concurrence, et pourquoi on ne fait rien

Deux instances démarrant simultanément sur une base vierge créeront deux clés. **Ce n'est pas un défaut.** Chaque secret porte l'identifiant de la clé qui l'a chiffré, les deux clés sont chargées par les deux instances, tout se déchiffre. Le pire cas est une clé de données inutilisée en base.

Aucun verrou consultatif, aucun index unique partiel, aucun `ON CONFLICT`. Le mécanisme qui rend la rotation possible rend la concurrence inoffensive — c'est la même propriété, et il serait absurde de payer un verrou pour un problème que le format a déjà résolu.

### Rotation

Ajouter une ligne par `datakey/create`, redémarrer. Les nouveaux secrets utilisent la nouvelle clé, les anciens restent lisibles.

**Le rechiffrement se fait en passant.** Au `GetAuthenticatorKeyAsync`, si le secret n'est pas chiffré avec la clé courante, on le rechiffre après l'avoir déchiffré. Le même code migre le clair hérité _et_ les anciennes clés — c'est trois lignes, et c'est ce qui rend la rotation réellement utilisable plutôt que théorique.

**Ce qu'on ne fait pas :** retirer une ancienne clé quand plus aucun secret n'en dépend. Il faudrait compter les secrets par clé, et cette requête n'a pas de consommateur aujourd'hui. Noté au § 9.

---

## 8. Où le code vit, et comment il est éprouvé

```
back/Palier.Infrastructure/Coffre/
├── ClientOkms.cs                       jeton OAuth2 + les trois appels REST
├── SourceDeConfigurationOkms.cs        IConfigurationSource
├── FournisseurDeConfigurationOkms.cs   IConfigurationProvider
├── TrousseauDeChiffrement.cs           les clés en mémoire, chiffrer et déchiffrer
└── AmorcageDuTrousseau.cs              IHostedService, étapes 6 et 7

back/Palier.Infrastructure/Identite/
└── GestionnaireDUtilisateurs.cs        + 2 surcharges

back/Palier.Infrastructure/Migrations/
└── table cles_de_donnees — seul ajout au schéma
```

Le schéma d'Identity n'est pas touché : l'identifiant de clé voyage dans la valeur du jeton (§ 4).

**`Palier.Domain` ne bouge pas, `Palier.Application` non plus.** La cryptographie n'est pas une connaissance du domaine, et il n'existe aucun cas d'usage qui chiffre — le chiffrement est un détail de la façon dont Identity range ses jetons. Y poser un port serait une abstraction sans second implémenteur, ce que KISS refuse.

### Les épreuves

- l'aller-retour chiffrer/déchiffrer, et le format `v1:` ;
- **la liaison au propriétaire** : un chiffré déplacé d'un utilisateur à un autre doit échouer ;
- la migration d'une valeur en clair, constatée sur la valeur réécrite ;
- le rechiffrement d'un secret porté par une clé plus ancienne ;
- **les cinq refus de démarrage, chacun provoqué et constaté** — deux assertions par épreuve, le refus _et_ un motif propre à sa cause, comme l'impose la règle des garde-fous du lot 1 ;
- une épreuve d'intégration contre le coffre réel.

**Le seuil de couverture.** D57 a figé `Palier.Infrastructure` à 96/79/71 au niveau atteint. Le code nouveau s'y ajoute : le seuil doit être vérifié et, s'il monte, relevé dans le même geste — un seuil qui reste sous le niveau atteint cesse de détecter le code mort public, ce qui est précisément sa raison d'être.

---

## 9. Ce que ce lot ne contient pas

- **Le chiffrement des données de santé au repos.** Il reste au chiffrement de volume. Le faire au niveau applicatif rendrait toute agrégation impossible.
- **La rotation automatique.** Manuelle, documentée, éprouvée — suffisant tant qu'aucune obligation n'impose une périodicité.
- **Le retrait d'une clé de données devenue inutile.** Demande un comptage sans consommateur aujourd'hui.
- **KMIP.** REST suffit et se teste plus simplement.
- **Le renouvellement du jeton OAuth2.** Sans objet : le client meurt avec le démarrage.

---

## 10. Points ouverts

**1. Les identifiants du coffre en intégration continue.** L'épreuve d'intégration a besoin d'un accès réel. Les mettre en secrets GitHub Actions confierait à un fournisseur américain l'accès au coffre européen — ce qui affaiblit D15 par la porte de derrière, et la politique IAM ne sait pas restreindre par chemin (§ 3), donc un compte de service de CI pourrait lire la production. **Recommandation :** l'épreuve d'intégration s'exécute au `pre-push` local, où les identifiants existent déjà, et pas en CI. La CI valide tout le reste. À trancher avec le porteur du projet.

**2. Le journal d'écriture anticipée porte les secrets déjà écrits en clair.** Réparation en exploitation après la migration : rotation du WAL, sauvegarde neuve, destruction des anciennes. À inscrire dans la procédure d'exploitation, pas dans le code.

**3. La contrainte de nommage des chemins OKMS.** `palier/dev` est une proposition. La forme exacte acceptée par le moteur KV se vérifie au premier `secret/create`, pas avant.

**4. La durée de vie du trousseau en mémoire.** Une clé de données en mémoire d'un processus long est exposée à un vidage mémoire. C'est le prix assumé de ne pas dépendre du coffre à chaque vérification TOTP (§ 5). Si le modèle de menace change, l'alternative est un appel `datakey/decrypt` par vérification, à 30 ms mesurés.

---

## 11. Sources

- Mesures : sonde jetable exécutée le 22/08/2026 contre `https://eu-west-rbx.okms.ovh.net`, domaine `d0624881-…`. La sonde n'est pas versionnée — son produit est ce document.
- `Microsoft.Extensions.Identity.Core` 10.0.0, `UserManager<TUser>` : signatures de `GetAuthenticatorKeyAsync`, `ResetAuthenticatorKeyAsync`, `SetAuthenticationTokenAsync` vérifiées sur Microsoft Learn le 22/08/2026.
- `docs/decisions.md` : D12 (Mediator), D15 (OVHcloud), D36 (identité depuis le jeton), D37 (trois rôles), D38 (tables d'identité en refus par défaut), D57 (seuils des adaptateurs), D58 (les six défauts de l'audit).
- `back/Palier.Database.Tests/ArchitectureTests.cs` : la règle qui interdit de prendre le contexte hors des gestionnaires, et ses deux exemptions nommées.
