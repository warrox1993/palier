# 13 — Juridique et opérations

Complète `01-conformite.md`, qui traite la ligne informer/prescrire. Ce document couvre la structure, les risques et les contrats.

---

## 1. Mineurs

**Décision : âge minimum 16 ans, partie nutrition verrouillée en dessous de 18 ans.**

Une application de comptage calorique accessible à un adolescent est le pire scénario possible, éthiquement et juridiquement. En Belgique, l'âge du consentement numérique est de 13 ans, mais un service traitant des données de santé avec suivi du poids relève d'un autre régime : consentement parental, obligation de vigilance renforcée, et exposition réputationnelle majeure au moindre incident.

| Âge déclaré | Accès                                                                          |
| ----------- | ------------------------------------------------------------------------------ |
| < 16 ans    | Refus d'inscription                                                            |
| 16-17 ans   | Entraînement uniquement. Nutrition, poids et progression corporelle désactivés |
| ≥ 18 ans    | Complet                                                                        |

Vérification par date de naissance à l'inscription, non modifiable ensuite sans intervention du support. Ce n'est pas une preuve d'âge, mais c'est la diligence attendue d'un éditeur.

Les conditions générales interdisent explicitement l'usage par un mineur de moins de 16 ans, et prévoient la fermeture du compte en cas de fausse déclaration constatée.

---

## 2. Transferts de données vers les fournisseurs de modèles

> **Nom de l'hébergeur repris le 20/08/2026.** La version précédente écrivait « héberger
> Supabase à Francfort » ; D15 a porté l'hébergement chez OVHcloud. Le raisonnement de cette
> section ne bouge pas d'un mot, et c'est précisément parce qu'il ne dépendait pas du nom de
> l'hébergeur qu'il tient encore.

**Le point le plus souvent manqué, et il l'a été dans les premières versions de ce dossier.**

Héberger la base et le backend chez un fournisseur européen ne suffit pas : chaque appel à un modèle envoie du contexte utilisateur — profil, contraintes déclarées, apports — vers les serveurs du fournisseur, souvent aux États-Unis. Ce sont des données de santé au sens de l'article 9.

**Le choix d'OVHcloud protège la base, jamais les transferts vers les modèles.** Ce sont deux traitements distincts : le premier est désormais entièrement européen, le second reste entier et appelle les mesures ci-dessous. Un hébergeur européen ne s'y substitue pas.

### Mesures obligatoires

**Minimisation du contexte.** Le modèle reçoit le strict nécessaire, sous forme dénominalisée : jamais de nom, jamais d'email, jamais d'identifiant de compte. Un identifiant technique éphémère, des valeurs chiffrées, des libellés de contrainte. Le prompt système ne contient aucune donnée directement identifiante.

**Vérification des régions.** Contrôler pour chaque fournisseur les régions de traitement disponibles, la politique de rétention, et l'exclusion d'usage des données pour l'entraînement. Documenter le résultat.

**Accords de sous-traitance signés** avec chaque fournisseur, avec clauses contractuelles types pour les transferts hors UE.

**Transparence.** La politique de confidentialité nomme chaque sous-traitant, sa localisation et la finalité du traitement. Une page dédiée explique quelles données partent au modèle et lesquelles ne partent jamais.

**Consentement séparé** pour l'usage de l'assistant, distinct du consentement général sur les données de santé. Refus possible sans perte d'accès au reste du produit.

**Option sans IA.** Le produit reste pleinement utilisable avec l'assistant désactivé. C'est à la fois un argument commercial et une garantie de conformité.

### Registre

Chaque appel journalise : horodatage, fournisseur, modèle, type de tâche, volume de tokens. **Jamais le contenu du prompt ni de la réponse.**

---

## 3. Structure juridique et assurance

### Forme

| Option                                        | Coût de départ                       | Responsabilité                             |
| --------------------------------------------- | ------------------------------------ | ------------------------------------------ |
| Indépendant complémentaire, personne physique | 200-300 €                            | **Illimitée, patrimoine personnel exposé** |
| SRL                                           | 1 500-2 500 € (acte, plan financier) | Limitée aux apports                        |

**Recommandation : SRL avant l'ouverture au public.** Le développement peut démarrer en personne physique, mais dès le premier abonné payant sur un produit touchant à la santé, la séparation des patrimoines n'est plus optionnelle. À arbitrer avec un comptable.

### Assurance responsabilité civile professionnelle

**Indispensable, jamais mentionnée jusqu'ici.** Couvre les dommages causés à un tiers dans le cadre de l'activité. Rechercher une police couvrant explicitement l'édition de logiciel et le conseil, avec extension cyber (violation de données, rançongiciel).

Ordre de grandeur : 400 à 900 €/an pour une petite structure. À souscrire avant l'ouverture, pas après.

### Comptabilité

Comptable dès la constitution. TVA trimestrielle, déclarations, cotisations sociales. Compter 800 à 1 500 €/an.

---

## 4. Sous-traitants et contrats

> **Registre repris le 20/08/2026.** La version précédente portait deux lignes — « Supabase |
> Base de données, auth » et « Vercel | Hébergement » — qui ne décrivent plus rien. D9, D15,
> D16 et D17 les suppriment toutes les deux : la base et le backend sont hébergés chez
> OVHcloud, le front est servi sous le même domaine que l'API, et l'authentification n'est
> plus déléguée à personne. Un registre des traitements est un document réglementaire
> (art. 30) : il nomme les sous-traitants réels, pas ceux d'une pile abandonnée.

| Sous-traitant   | Rôle                                                                          | À obtenir                                                                                                                                         |
| --------------- | ----------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------- |
| OVHcloud        | Hébergement du backend et du front — même domaine, D16 — et PostgreSQL managé | DPA, localisation exacte des centres de données, documentation de sécurité, de sauvegarde et de notification, liste des sous-traitants ultérieurs |
| Anthropic       | Modèle                                                                        | DPA, politique de rétention, exclusion entraînement                                                                                               |
| Google          | Modèle                                                                        | DPA, région, exclusion entraînement                                                                                                               |
| Stripe          | Paiement                                                                      | DPA, conformité PCI                                                                                                                               |
| Sentry          | Erreurs                                                                       | DPA, région UE, masquage configuré                                                                                                                |
| Plausible/Umami | Mesure                                                                        | Auto-hébergé de préférence                                                                                                                        |
| Diététicien     | Validation                                                                    | Contrat de prestation, périmètre, responsabilité                                                                                                  |

**Pourquoi cette substitution renforce la conformité, et ne fait pas que la simplifier.** « Région UE » chez un fournisseur américain signifie : données stockées en Europe, société mère soumise au CLOUD Act américain. OVHcloud est une société européenne, donc hors de portée de ce texte — c'est le motif retenu par D15. Pour des données de santé au sens de l'article 9 et une clientèle belge, l'argument est plus solide qu'une région de stockage, parce qu'il ne repose plus sur des garanties contractuelles censées compenser une juridiction extraterritoriale.

**Ce qui n'est plus délégué, et devient un traitement que vous opérez.** Deux lignes ont quitté le tableau ; les responsabilités qu'elles portaient, elles, n'ont pas disparu — elles ont changé de côté.

| Ce qui était délégué                                              | À qui              | Qui en répond désormais                                                         |
| ----------------------------------------------------------------- | ------------------ | ------------------------------------------------------------------------------- |
| Authentification, mots de passe, sessions, jetons                 | Supabase Auth      | Vous — D17                                                                      |
| Système d'exploitation, correctifs, certificats TLS, durcissement | Plateforme managée | Vous — D15, le backend est conteneurisé sur un VPS ou une instance Public Cloud |
| Supervision de l'infrastructure, sauvegardes, restauration        | Plateforme managée | Vous — D15                                                                      |
| Diffusion du front                                                | Vercel             | Vous — D16, même domaine que l'API                                              |

OVHcloud reste sous-traitant de l'infrastructure — matériel, centre de données, PostgreSQL managé. Il n'est sous-traitant d'aucun des quatre traitements ci-dessus.

Registre des traitements (RGPD art. 30) tenu à jour, listant finalité, base légale, catégories de données, destinataires, durées et transferts.

**Analyse d'impact (AIPD) obligatoire** : traitement à grande échelle de données de santé avec profilage. À réaliser avant l'ouverture, avec l'avocat.

**Ce que le changement d'architecture ajoute à l'AIPD.** L'analyse ne décrit plus une application qui délègue sa sécurité, mais une application qui l'opère. Trois points en découlent, et aucun n'est cosmétique.

1. **L'authentification devient une mesure technique de l'AIPD, et non plus une garantie de sous-traitant.** Les sept exigences de `09-comptes.md` § 1 — Google OAuth, vérification d'email obligatoire avant l'accès nutrition, contrôle du mot de passe contre HaveIBeenPwned, limitation des tentatives avec verrouillage progressif, 2FA TOTP, rotation des jetons de rafraîchissement, fusion des comptes email et Google — sont à décrire comme telles, avec leur état d'implémentation. D17 note qu'ASP.NET Identity en couvre une partie, pas tout : l'écart se documente, il ne se suppose pas comblé.
2. **L'exploitation du serveur devient une mesure de sécurité au sens de l'article 32.** Correctifs, certificats, durcissement, journalisation, sauvegardes et restauration éprouvée relèvent de vous. Un correctif non appliqué n'est plus l'oubli d'un fournisseur.
3. **La détection d'un incident repose sur votre supervision.** Une plateforme managée signalait d'elle-même une anomalie de son socle ; sur un serveur administré, ce qui n'est pas supervisé n'est vu par personne. `14-contenu.md` § 6 en tire la liste.

**Ce qui reste à trancher, et que ce document ne tranche pas :**

- **Qui signe le DPA OVHcloud, et sous quelle entité.** La réponse dépend de la forme juridique retenue au § 3 : signer en personne physique puis constituer une SRL oblige à reprendre le contrat.
- **Quelles pièces OVHcloud annexer au registre et à l'AIPD** — localisation des centres de données, mesures de sécurité, sous-traitants ultérieurs, procédure de notification. À demander au fournisseur ; ce dossier ne présume pas de ce qu'il publie ni de la forme sous laquelle il le publie.
- **Quel produit OVHcloud héberge le backend** — VPS ou instance Public Cloud. D15 laisse les deux ouverts et le partage des responsabilités n'y est pas identique ; l'AIPD a besoin de savoir lequel.
- **Si une certification d'hébergement de données de santé est pertinente ici.** Le régime HDS relève du droit français ; rien n'établit à ce jour qu'il s'impose à un éditeur belge. Question pour l'avocat, pas obligation constatée.

---

## 5. Partenariats salles et professionnels

Le canal d'acquisition principal. Trois modèles possibles :

| Modèle         | Mécanique                                      | Remarque                                     |
| -------------- | ---------------------------------------------- | -------------------------------------------- |
| Recommandation | Code de parrainage, commission ou mois offerts | Le plus simple, à privilégier au lancement   |
| Licence salle  | Tarif par membre, dégressif                    | Revenu régulier, mais engagement de service  |
| Marque blanche | Personnalisation visuelle                      | Complexité technique élevée, à écarter en V1 |

**Point de vigilance :** un kinésithérapeute qui recommande l'application engage une part de sa crédibilité. Le contrat doit préciser que l'application ne délivre pas de conseil médical et que le professionnel reste seul responsable de ses propres recommandations.

Prévoir une fiche d'une page destinée aux professionnels : ce que fait l'application, ce qu'elle ne fait pas, comment elle gère les contraintes physiques.

---

## 6. Modération et signaux préoccupants

Un utilisateur peut écrire à l'assistant qu'il va mal. Trois niveaux de réponse, définis à l'avance :

| Niveau | Signal                                                 | Réponse                                                                                        |
| ------ | ------------------------------------------------------ | ---------------------------------------------------------------------------------------------- |
| 1      | Découragement, fatigue, démotivation                   | Réponse normale, bienveillante                                                                 |
| 2      | Signaux de TCA, restriction sévère, obsession du poids | Interruption du fil, orientation vers un professionnel, ressources locales                     |
| 3      | Détresse aiguë, mention d'automutilation ou de suicide | Message court, coordonnées d'urgence belges, aucune tentative d'accompagnement par l'assistant |

Les ressources sont localisées : Centre de prévention du suicide, ligne 108, et pour les troubles alimentaires les structures belges de référence. **À faire valider par un professionnel de santé, pas rédigé seul.**

Aucune de ces situations n'est traitée par le modèle sans garde-fou : la détection est déterministe, la réponse est un texte fixe et validé.
