# Gabarit — rapport de fin de lot

## Ce qui a changé

Fichiers, compteurs, avant/après.

## Ce qui a cassé

Y compris ce qui a été cassé puis rattrapé.

## Ce que je signale sans y avoir touché

Trouvé en chemin, hors périmètre.

## Le franchissement

La preuve que le garde-fou refuse, pas sa relecture. Pour chaque épreuve :
ce qui a été provoqué, ce qui était attendu, ce qui s'est produit.

## Les vérifications que l'automatisation ne peut pas faire

**À lancer à la main, à chaque fin de lot, et à recopier ici avec leur sortie.**
Ce ne sont pas des oublis : ce sont des limites mesurées. Une vérification qu'aucun
outil ne peut porter et qu'aucun gabarit ne réclame est une vérification qui
disparaît.

### L'état du service Dependabot — D30

`.github/dependabot.yml` peut être parfaitement juste pendant que la
fonctionnalité qu'il paramètre est éteinte côté GitHub. C'est arrivé, et une
épreuve verte a lu un fichier correct pendant vingt-quatre heures.

La CI porte une sonde (job `securite`), mais elle ne peut pas se suffire :
**mesuré le 20/08/2026 à la source**, les deux points d'entrée exigent la
permission fine « Administration: read », qui ne figure pas parmi les clés que
`permissions:` accepte pour `GITHUB_TOKEN` — l'appel rend 403 « Resource not
accessible by integration » même sous `write-all`. La sonde n'aboutit donc que
si un jeton à granularité fine est déposé en secret `JETON_ETAT_DEPENDABOT`.

```bash
gh api -i repos/warrox1993/palier/vulnerability-alerts     # attendu : HTTP 204 No Content
gh api repos/warrox1993/palier/automated-security-fixes    # attendu : {"enabled":true,"paused":false}
```

| Date       | `vulnerability-alerts` | `automated-security-fixes`        |
| ---------- | ---------------------- | --------------------------------- |
| 20/08/2026 | `HTTP 204`             | `{"enabled":true,"paused":false}` |

Un `404` sur le premier est **ambigu** : il signifie « éteint » _ou_ « jeton sans
droit ». Le distinguer demande de relancer avec un jeton dont on sait qu'il porte
« Administration: read ».

## Ce qui n'a pas pu être vérifié

Nommément. Un rapport sans incertitude est un rapport incomplet.

## Mesures

Durée de `npm run verify`. Nombre de tests. Couverture du domaine.
Faux positifs écartés, avec leur motif.

## Skills et agents invoqués

Et pour ceux qui ne l'ont pas été, pourquoi.
