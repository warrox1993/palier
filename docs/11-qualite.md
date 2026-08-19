# 11 — Résilience, accessibilité, mesure

Ces trois sujets sont ce qui sépare un produit fini d'un prototype. Ils ne se rajoutent pas à la fin.

---

## 1. Résilience réseau et hors ligne

### Le motif
Écriture locale immédiate (IndexedDB via Dexie), mutation réseau en arrière-plan avec file de retry. La saisie n'attend jamais une requête.

### Que se passe-t-il si le réseau tombe pendant une séance
1. La saisie continue normalement, tout est écrit en local
2. Un indicateur discret dans l'en-tête passe en « en attente », avec le nombre d'opérations en file
3. À la reconnexion (`navigator.onLine` + écouteur `online`), la file se rejoue dans l'ordre
4. Si le réseau ne revient pas de la séance : les données restent en local, la synchronisation se fait au prochain lancement

### Que se passe-t-il si l'application est fermée avec une file non vide
La file est persistée dans IndexedDB, pas en mémoire. Elle est rejouée au démarrage suivant, avant tout affichage de données. Un badge signale une file en attente depuis plus de 24 h.

### Conflits
Horodatage par enregistrement, dernier écrivain gagne **au niveau de la série**, jamais de la séance entière. Un changement d'appareil ne doit pas écraser un travail plus récent.

### Échecs définitifs
Une opération qui échoue cinq fois passe en file d'erreur, consultable par l'utilisateur, avec possibilité de réessayer ou d'exporter. **On ne jette jamais silencieusement une donnée saisie.**

### Cache applicatif
Service worker : coquille applicative en cache-first, données en network-first avec repli local. Les 500 aliments les plus consultés et le programme actif sont pré-cachés.

---

## 2. Accessibilité

Cible : **WCAG 2.2 niveau AA**, vérifié, pas déclaré.

### Structure
- HTML sémantique, un seul `h1` par écran, hiérarchie de titres continue
- Points de repère : `main`, `nav`, `header`
- Lien d'évitement vers le contenu principal

### Clavier
- Tout parcours réalisable au clavier seul
- Focus visible, contraste 3:1 minimum, jamais supprimé
- Ordre de tabulation logique
- Échappement fermant tout panneau ouvert
- Piège de focus dans les panneaux modaux, restitution à la fermeture

### Lecteurs d'écran
- Chaque champ de saisie porte un `label` associé, pas seulement un placeholder
- Le minuteur de repos annonce sa fin via une région `aria-live="polite"`
- Le radar de progression a une alternative textuelle sous forme de liste de valeurs
- La règle graduée d'écart annonce valeur, fourchette et position
- Les états de chargement sont annoncés
- Testé sur VoiceOver iOS et TalkBack Android

### Vision
- Contraste 4,5:1 sur le texte, 3:1 sur les éléments d'interface, **vérifié à luminosité réduite**
- **Aucune information portée par la couleur seule** : chaque état a un libellé et une position
- Zoom à 200 % sans perte de fonction ni défilement horizontal
- Respect des tailles de police système, unités relatives partout
- Test avec simulation de deutéranopie et de protanopie

### Motricité
- Cibles tactiles 48 × 48 px minimum, 8 px d'espacement
- Aucun geste complexe requis ; tout balayage a une alternative par bouton
- Aucune action déclenchée par un délai

### Mouvement
`prefers-reduced-motion` respecté partout : le radar s'affiche directement, la règle ne glisse pas, le minuteur reste numérique.

### Vérification
axe-core en CI, échec du build si une violation critique apparaît. Audit manuel au lecteur d'écran sur les parcours de séance, de saisie alimentaire et d'onboarding.

---

## 3. Internationalisation

Français et anglais dès la V1. Aucune chaîne en dur dans les composants, y compris les messages d'erreur et les états vides.

- `i18next`, fichiers de traduction versionnés
- Formats de date, de nombre et d'unité localisés
- Unités impériales en option (livres, pouces) — indispensable pour un marché anglophone
- Les libellés nutritionnels validés par le diététicien sont traduits **puis revalidés**, jamais traduits automatiquement
- Détection de langue par le navigateur, modifiable dans les réglages

---

## 4. Mesure

### Ce qu'on mesure
Le critère de réussite du produit : combien d'utilisateurs ont enregistré 20 séances ou 30 journées alimentaires après 8 semaines.

| Indicateur | Définition |
|---|---|
| Activation | Onboarding terminé + première séance enregistrée |
| Rétention J7 / J30 / J60 | Au moins une saisie sur la période |
| Profondeur de saisie | Séances et journées alimentaires cumulées |
| Complétion de séance | Séances terminées / séances commencées |
| Adoption nutrition | Utilisateurs avec ≥ 7 journées saisies |
| Adoption compléments | Utilisateurs avec ≥ 1 complément enregistré — le différenciateur |
| Conversion | Essai → payant |
| Résiliation mensuelle | Avec motif quand il est donné |

### Indicateurs de conformité — à surveiller autant que les autres
- Nombre de rejets par le filtre de sortie, par type
- Escalades déclenchées
- Alertes TCA déclenchées, et ce qui s'est passé ensuite
- Tentatives de contournement des planchers de sécurité

### Comment
**Outil respectueux de la vie privée, hébergé dans l'UE** : Plausible ou Umami, auto-hébergé. Pas de Google Analytics — incompatible avec le positionnement et source de complications RGPD sur des données de santé.

**Aucune donnée de santé dans l'analytique.** On mesure « une séance a été enregistrée », jamais son contenu. Les événements sont des compteurs, pas des contenus.

Journalisation applicative : Sentry en auto-hébergé ou région UE, avec masquage strict — aucun corps de requête, aucun contenu de saisie.

### Ce qu'on ne mesure pas
Aucun suivi comportemental à des fins publicitaires, aucun pixel tiers, aucune revente, aucune carte de chaleur enregistrant des interactions sur des écrans de santé.
