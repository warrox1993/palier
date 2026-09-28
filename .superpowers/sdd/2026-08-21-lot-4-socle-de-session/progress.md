# Lot 4 — socle de session · journal de bord

**Branche :** `feat/lot-4-socle-session` · **17 commits** · 21/08/2026
**Spec :** `docs/superpowers/specs/2026-08-21-lot-4-socle-de-session-design.md`
**Plan :** `docs/superpowers/plans/2026-08-21-lot-4-socle-de-session.md` — 20 tâches

---

## Ce qui a changé

| Avant le lot                                               | Après                                                                                     |
| ---------------------------------------------------------- | ----------------------------------------------------------------------------------------- |
| `DemandeurSansIdentite` — tout cas d'usage échouait        | `IdentiteDepuisJeton` lit `HttpContext.User`, peuplé après vérification de la signature   |
| Trois rôles PostgreSQL                                     | **Quatre** — `palier_auth` ouvre le seul chemin vers les tables que D38 avait fermées     |
| Aucun magasin de sessions                                  | `sessions_refresh` : empreinte SHA-256, famille, rotation, grâce, purge, cascade éprouvée |
| Aucune route d'authentification                            | **Neuf** — inscription, connexion, rafraîchir, déconnexion ×2, sessions, 2FA ×3           |
| PBKDF2 à 100 000 itérations                                | **210 000**, mesuré : 56,7 ms → 111,2 ms sur seize cœurs                                  |
| Aucun contrôle de mot de passe compromis                   | Deux étages — liste embarquée obligatoire, HIBP opportuniste                              |
| Aucune limitation                                          | Par adresse **réelle**, et verrouillage par compte 5 → 15 → 60 min                        |
| Aucune 2FA                                                 | TOTP complet, codes de récupération, porte à la connexion                                 |
| Seuils de couverture : `Domain` et `Application` seulement | **`Infrastructure` et `Api` aussi**, à 96 / 79 / 71 %                                     |
| 176 + 16 = **192 épreuves**                                | **388 épreuves**                                                                          |

**Trois migrations** : `SessionsEtRoleAuth`, `VerrouillageProgressif`, `ConsentementSante`.
**Une dépendance ajoutée** : `Microsoft.AspNetCore.Authentication.JwtBearer` 10.0.11, licence vérifiée.
**Quatre décisions** : D54 à D57.

---

## Ce qui a cassé — y compris ce que j'ai cassé moi-même

### Douze défauts réels, tous trouvés en franchissant, aucun en relisant

1. **`AddJwtBearer` diffère son délégué** jusqu'à la première requête. Le contrôle de longueur de `JWT_SIGNING_KEY` ne tombait donc **pas** au démarrage — alors que le commentaire juste au-dessus l'affirmait. Mesuré : « No exception was thrown ».

2. **`RequireUniqueEmail` vaut `false` par défaut.** Deux comptes pouvaient partager une adresse, et `FindByEmailAsync` — dont dépend toute la connexion — en aurait choisi un au hasard. Découvert parce que l'épreuve d'énumération **restait verte** quand on retirait `DuplicateEmail` de la liste avalée : Identity ne produisait jamais ce code.

3. **⚠ Vider `KnownProxies` n'interdit pas la confiance : elle la rend TOTALE.** `ForwardedHeadersMiddleware` calcule `checkKnownIps = KnownIPNetworks.Count > 0 || KnownProxies.Count > 0` et ne contrôle l'origine **que si** ce drapeau est vrai. Listes vides, il croit n'importe quel `X-Forwarded-For`, venu de n'importe où. C'est l'inverse exact de l'intuition, et c'était le comportement du code avant correction : « Expected 192.0.2.44, Actual 203.0.113.9 » — l'adresse comptée était **choisie par le client**.

4. **`AuthenticatorTokenProvider.GenerateAsync` rend toujours la chaîne vide**, par conception. Sept épreuves rouges avant de le comprendre. Le harnais calcule désormais le TOTP lui-même, tenant le rôle du téléphone.

5. **`ResetAuthenticatorKeyAsync` rend un `IdentityResult` que rien n'oblige à lire.** L'ignorer faisait rendre un QR parfaitement valide pour une clé **non enregistrée** : l'utilisateur aurait scanné, activé, et se serait retrouvé dehors.

6. **Les quatre règles de composition d'Identity** refusaient « brouette-hivernale-38-oscille » — vingt-neuf signes, absent de toute fuite — faute de majuscule. NIST SP 800-63B rév. 4 les interdit en toutes lettres.

7. **`KnownNetworks` est déprécié** en .NET 10 (ASPDEPR005) au profit de `KnownIPNetworks`. La documentation en ligne donne encore l'ancien nom.

8. **Le change tracker d'EF servait le cache** : une session révoquée par `ExecuteUpdate` ressortait « Acceptee ». `AsNoTracking` + écritures par `ExecuteUpdate`.

9. **`grant select on all tables` ne couvre pas les tables futures** — trois épreuves de sauvegarde ont rougi, `pg_dump` échouait sur `sessions_refresh`.

10. **Un cookie de chemin `/rafraichir` n'atteint jamais `/deconnexion`.** La déconnexion aurait répondu « c'est fait » sans rien couper.

11. **`Results.Json` résout ses services dans `RequestServices`** — un `DefaultHttpContext` nu le fait échouer sur « Value cannot be null. (Parameter 'provider') ».

12. **`const decimal` publique fait chuter la couverture** (lot 3, rappelé ici) : elle engendre un constructeur statique mort, qu'aucun test ne peut atteindre.

### Trois défauts d'ÉPREUVE — les plus instructifs

- **Trois épreuves du jeton émettaient à une date figée et validaient contre l'horloge réelle.** Vertes le matin, rouges l'après-midi.

- **L'épreuve de `ClockSkew` ne prouvait rien.** Un jeton vieux de seize minutes est refusé même avec la tolérance de cinq minutes par défaut. Elle porte maintenant sur trente secondes d'expiration, avec la **contre-épreuve** qui nomme la cause : le même jeton passe dès qu'on rend la tolérance.

- **`Des_echecs_ESPACES` dérivait son espacement de `Fenetre` elle-même.** Porter la fenêtre à dix ans espaçait aussi les tentatives de dix ans, et l'épreuve restait **verte**. Une épreuve dont l'entrée dérive du réglage qu'elle éprouve ne mord jamais. L'espacement est désormais littéral, avec une assertion qui rougit si la fenêtre le dépasse.

### Deux franchissements invalides, et ce qu'ils ont appris

- **Retirer trois fichiers d'épreuves pour éprouver le seuil de couverture** a fait échouer la **compilation** sur CA1812 — deux doubles du harnais devenaient orphelins — **avant** que le seuil ne soit évalué. Un franchissement qui casse en amont de sa cible ne prouve rien. Refait avec un seul fichier.

- **`if (false)` pour désactiver la porte 2FA** : code inaccessible, refusé à la compilation. Refait avec une propriété fausse mais non constante.

### Une duplication rattrapée

`La_suppression_d_un_utilisateur_emporte_TOUTES_ses_sessions` recopiait une règle déjà éprouvée dans `IsolationTests`. Retirée ; seul le complément manquant — **la cascade ne déborde pas** — a été gardé, et placé à côté de l'originale. Une règle, un endroit.

---

## Ce que je signale sans y avoir touché

- **`docs/09-comptes.md` § 1 laisse ouverte la réinitialisation de mot de passe.** `DataProtectionTokenProviderOptions.TokenLifespan` est passé à une heure comme le document l'exige, mais **aucune route de réinitialisation n'existe** — et la réserve du document tient : cette option est partagée par tous les jetons du fournisseur, confirmation d'adresse comprise. Le jour où deux durées devront différer, il faudra un fournisseur dédié.

- **`AspNetRoleClaims` n'est pas dans la boucle d'ouverture RLS** de la migration `SessionsEtRoleAuth`, contrairement aux six autres tables `AspNet*`. Aucun rôle n'est utilisé aujourd'hui, donc rien ne casse — mais le jour où un rôle sera créé, l'écriture échouera sur une table fermée. **Hors périmètre de ce lot ; à traiter au lot qui introduira les rôles.**

- **La limitation compte en mémoire de processus.** Sur plusieurs répliques, la limite effective est multipliée par leur nombre, et un redémarrage la remet à zéro. C'est le contrat de `AddRateLimiter`, écrit dans le code et dans l'AIPD.

- **`verify` prend 333 s**, au-delà de l'avertissement de 90 s. Le porteur du projet a explicitement accepté ce coût ; l'avertissement reste, et c'est bien.

---

## Le franchissement

**Chaque garde-fou de ce lot a été vu rouge sur la violation qu'il refuse.** Vingt-huit franchissements, isolés les uns des autres.

| Garde-fou                                   | Violation provoquée           | Rouges | Motif constaté                                    |
| ------------------------------------------- | ----------------------------- | ------ | ------------------------------------------------- |
| `ClockSkew = TimeSpan.Zero`                 | retiré                        | 2      | `Assert.Null() Failure: Value is not null`        |
| `IsAuthenticated`                           | contrôle retiré               | 1      | « un `sub` non authentifié a été accepté »        |
| `Secure` du cookie                          | passé à `false`               | 1      | sous-chaîne `secure` absente                      |
| Règles de composition                       | majuscule réimposée           | 1      | 400 au lieu de 202                                |
| Hachage sur adresse inconnue                | retiré                        | 1      | « aucun PBKDF2 n'a eu lieu »                      |
| `DuplicateEmail` avalé                      | retiré de la liste            | 1      | corps de réponse divergents                       |
| `RequireUniqueEmail`                        | remis à `false`               | 1      | « deux comptes portent la même adresse »          |
| Révocation de famille au réemploi           | `RevoquerLaFamille: false`    | 1      | le successeur survit                              |
| Fenêtre de grâce                            | mise à zéro                   | 2      | 401 au lieu de 200                                |
| Cause d'échec de rotation                   | révélée dans la réponse       | 1      | corps divergents                                  |
| Contrôle d'identité de la liste             | retiré                        | 1      | 200 au lieu de 401                                |
| En-tête transféré sans proxy déclaré        | cru inconditionnellement      | 2      | 6 seaux pour 6 requêtes d'une même machine        |
| Boucle locale de confiance                  | listes par défaut conservées  | 1      | `Collection was not empty`                        |
| Politique de limitation sur `/connexion`    | retirée                       | 1      | politique nulle                                   |
| Partition du limiteur                       | clé constante                 | 1      | le voisin refusé                                  |
| `ForwardLimit = 1`                          | passé à `null`                | 1      | remontée de deux crans                            |
| Verrou avant vérification du mot de passe   | ordre inversé + code distinct | 1      | corps divergents                                  |
| Escalade du verrouillage                    | palier unique                 | 1      | seconde récidive à 5 min                          |
| Enregistrement de l'échec                   | retiré                        | 1      | `LockoutEnd` nul                                  |
| `DernierEchecLe`                            | non écrite                    | 1      | plus rien ne verrouille                           |
| Fenêtre du compteur                         | portée à dix ans              | 2      | assertion de garde + valeurs du document          |
| Preuve de possession pour désactiver la 2FA | retirée                       | 1      | 204 au lieu de 400                                |
| `algorithm` dans l'URI TOTP                 | retiré                        | 1      | sous-chaîne absente                               |
| Porte 2FA à la connexion                    | ouverte                       | 2      | 200 au lieu de 401                                |
| Codes de récupération                       | non essayés                   | 1      | 401 au premier usage                              |
| Espaces du code TOTP                        | non retirés                   | 1      | 400 au lieu de 200                                |
| `NutritionOuverte`                          | `&&` → `\|\|`                 | 2      | deux combinaisons ouvertes à tort                 |
| `EntrainementOuvert`                        | fermé comme la nutrition      | 3      | « le produit refuse de noter sa séance »          |
| Cascade des sessions                        | `Cascade` → `Restrict`        | 3      | `23001: violates RESTRICT setting`                |
| Seuil de couverture                         | un fichier d'épreuves retiré  | build  | « total line coverage is below the specified 96 » |

### Les quatre questions, sur les mécanismes nouveaux

- **Tourne-t-il ?** 388 épreuves vertes, `npm run verify` en 16 étapes.
- **Mord-il ?** Le tableau ci-dessus. **Et dans ses branches** : la porte nutrition est éprouvée sur ses **quatre** combinaisons, pas seulement les deux extrêmes.
- **Refuse-t-il d'enregistrer à moitié ?** La rotation tient dans une transaction avec `for update` ; `GardienDeVerrouillage` **lève** si `UpdateAsync` échoue.
- **Crie-t-il quand il n'a plus de cible ?** `JWT_SIGNING_KEY` absente ou trop courte → refus au démarrage en nommant la variable ; liste de mots de passe absente → `InvalidOperationException` ; clé d'authentificateur non engendrée → idem ; politique d'autorisation absente → épreuve dédiée ; `RevoquerFamilleAsync` rend `false` sur un jeton inconnu.

---

## La mesure avec son instrument

| Chiffre                         | Ce qui l'a produit                                                                   |
| ------------------------------- | ------------------------------------------------------------------------------------ |
| 56,7 ms → 111,2 ms              | PBKDF2 chronométré sur seize cœurs, 100 000 puis 210 000 itérations                  |
| HMAC-SHA512, 100 000 itérations | décodage du format IdentityV3 **octet par octet** — la documentation dit autre chose |
| 96,63 / 79,60 / 71,55 %         | `dotnet test` avec coverlet MSBuild, `Include` sur les deux assemblages              |
| 388 épreuves                    | 176 `Domain` + 37 `Application` + 175 `Database`                                     |
| 333,5 s                         | `npm run verify`, ses seize étapes chronométrées individuellement                    |
| 6 seaux pour 6 requêtes         | `HashSet` de clés de partition, avant correction du traitement des en-têtes          |

---

## Ce qui n'a pas pu être vérifié

- **Le comportement derrière le proxy OVHcloud réel.** `TRUSTED_PROXIES` est éprouvée sur des adresses fabriquées ; ce que le répartiteur écrit réellement dans `X-Forwarded-For` n'a pas été observé.
- **Le coût du hachage sur un vCPU mutualisé.** La mesure est faite sur seize cœurs. « Compter le double » est une estimation, pas une mesure.
- **L'interopérabilité TOTP avec des authentificateurs réels.** Le code est calculé et vérifié par deux implémentations indépendantes, mais aucun téléphone n'a scanné le QR.
- **Le comportement sous charge.** Aucune épreuve de concurrence réelle sur la rotation : le verrou de ligne est posé et éprouvé fonctionnellement, pas sous contention.
- **La suppression de compte de bout en bout.** Seule la cascade est éprouvée.

---

## Les skills Superpowers invoqués

| Skill                            | Usage                                                                                             |
| -------------------------------- | ------------------------------------------------------------------------------------------------- |
| `brainstorming`                  | avant la spec — les sept trous architecturaux, dont le chemin d'accès de D38                      |
| `writing-plans`                  | le plan en 20 tâches                                                                              |
| `executing-plans`                | l'exécution, tâche par tâche                                                                      |
| `test-driven-development`        | chaque tâche : épreuve rouge, puis code                                                           |
| `systematic-debugging`           | la clé TOTP vide — le premier diagnostic (RLS) était **faux**, la lecture de la donnée l'a réfuté |
| `verification-before-completion` | `verify` complet avant la clôture                                                                 |

**Non invoqués, et pourquoi :** `frontend-design` et `avoid-ai-design` — aucun écran dans ce lot. `requesting-code-review` — le porteur du projet lance `/code-review ultra` lui-même ; c'est signalé ci-dessous.

---

## La définition de terminé — douze points

| #   | Point                                        | État                                                                         |
| --- | -------------------------------------------- | ---------------------------------------------------------------------------- |
| 1   | Test avant le code, rouge puis vert          | ✅ chaque tâche                                                              |
| 2   | Lint et types sans erreur                    | ✅ `verify`, étapes `front:lint`, `front:lint:types`, `scripts:lint`         |
| 3   | Build réussi                                 | ✅ `back:build`, `front:build`                                               |
| 4   | Parcours vérifié de bout en bout             | ✅ inscription → connexion → rafraîchir → 2FA → déconnexion, sur moteur réel |
| 5   | Revue passée                                 | ⏳ **`/code-review ultra` à lancer par le porteur du projet**                |
| 6   | Aucune régression                            | ✅ 388 vertes, seuils tenus                                                  |
| 7   | Accessible au clavier, axe-core              | ⛔ **sans objet** — aucun écran dans ce lot                                  |
| 8   | États de chargement, erreur, vide, partiel   | ⛔ **sans objet** — aucun écran                                              |
| 9   | Textes français et anglais                   | ⛔ **sans objet** — l'API ne rend que des **codes**, jamais de phrase        |
| 10  | Aucune donnée de santé dans les logs         | ✅ `JournalisationTests` ; aucun mot de passe, aucun jeton, aucun code TOTP  |
| 11  | Tests de conformité si nutrition/progression | ✅ la porte nutrition, ses quatre combinaisons                               |
| 12  | Commit explicite                             | ✅ 17 commits, chacun nommant ce qu'il ferme                                 |

**Les trois points sans objet sont déclarés, pas sautés.** Ils reviennent au lot qui livrera les écrans — et le point 9 mérite d'être noté à l'envers : c'est parce que l'API ne rend que des codes (`IdentifiantsInvalides`, `SessionInvalide`, `MotDePasseCompromis`) que la traduction reste entièrement possible plus tard.

---

## Ce que ce lot ne referme PAS

1. **Google OAuth et la fusion des comptes** — exigences 1 et 7, entièrement reportées. L'adresse et le mot de passe sont aujourd'hui le **seul** moyen de créer un compte.
2. **L'envoi de courriels.** Sans lui, **aucun compte ne peut atteindre la nutrition** : la règle est en place, le moyen de la satisfaire ne l'est pas.
3. **La suppression de compte complète** — écran, export, effacement sous 30 jours, **purge des sauvegardes**. Ce lot livre la cascade.
4. **La portabilité de la limitation.** Un magasin partagé sera nécessaire à la deuxième réplique.
5. **La liste des tables hors modèle RLS** — elle appartient au porteur du projet, et n'a pas été établie.
6. **La réinitialisation de mot de passe** : la durée du jeton est réglée, la route n'existe pas.

---

## À faire par le porteur du projet

**`/code-review ultra`** sur cette branche. C'est le moment : le lot touche l'authentification, les rôles PostgreSQL et le RGPD — trois des quatre moments où une revue multi-agents a été jugée nécessaire.

Et **`TRUSTED_PROXIES`** au déploiement : vide, l'API ignore tout en-tête transféré, ce qui est sûr mais fait compter l'adresse du répartiteur pour tout le monde.
