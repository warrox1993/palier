#!/usr/bin/env bash
# Lance la démonstration de l'API Palier en une commande — D83.
#
#   ./demarrer-demo.sh           construit, lance, attend que l'API réponde
#   ./demarrer-demo.sh arreter   arrête les conteneurs (les données restent)
#   ./demarrer-demo.sh effacer   arrête et DÉTRUIT les données de la démonstration
#
# Moteur : Docker Compose s'il répond, sinon `podman compose`, sinon
# `podman-compose`. `PALIER_COMPOSE="podman-compose"` impose un choix.
#
# Au premier lancement, le script engendre `.env` (exclu du dépôt) : une clé de
# signature des jetons et une clé de données tirées au hasard, et les chaînes de
# connexion dont les mots de passe sont LUS dans `db/amorcage/01-roles.sql`.
# Rien de tout cela n'est un vrai secret : ce sont des valeurs locales, qui
# n'ouvrent que des conteneurs écoutant sur 127.0.0.1.
set -euo pipefail

cd "$(dirname "$0")"

ROLES=db/amorcage/01-roles.sql
ENV=.env

# --- Le moteur -------------------------------------------------------------
if [ -n "${PALIER_COMPOSE:-}" ]; then
  read -r -a compose <<< "$PALIER_COMPOSE"
elif command -v docker > /dev/null 2>&1 && docker info > /dev/null 2>&1; then
  compose=(docker compose)
elif command -v podman > /dev/null 2>&1 && podman compose version > /dev/null 2>&1; then
  compose=(podman compose)
elif command -v podman-compose > /dev/null 2>&1; then
  compose=(podman-compose)
else
  echo "Aucun moteur de conteneurs ne répond : installer Docker (avec Compose) ou Podman." >&2
  exit 3
fi

case "${1:-demarrer}" in
  demarrer) ;;
  arreter)
    "${compose[@]}" down
    exit 0
    ;;
  effacer)
    "${compose[@]}" down -v
    exit 0
    ;;
  *)
    echo "Action inconnue « $1 ». Actions : demarrer (défaut), arreter, effacer." >&2
    exit 2
    ;;
esac

# --- Le fichier .env, engendré une seule fois ------------------------------
mot_de_passe() {
  # Le mot de passe du rôle, tel que l'amorçage le crée. Lu, jamais recopié.
  sed -n "s/^[[:space:]]*login password '\([^']*\)'.*/\1/p" \
    < <(grep -A1 -E "^create role $1\b" "$ROLES") | head -n 1
}

aleatoire() {
  head -c "$1" /dev/urandom | base64 | tr -d '\n'
}

# `Gss Encryption Mode=Disable` : la base de la démonstration n'offre pas
# Kerberos, et sans ce réglage Npgsql cherche `libgssapi_krb5`, absente de
# l'image, puis écrit une erreur trompeuse avant de continuer sans elle.
base_de_la_chaine="Host=base;Port=5432;Database=palier;Gss Encryption Mode=Disable"

chaine() {
  local mdp
  mdp="$(mot_de_passe "$1")"
  if [ -z "$mdp" ]; then
    echo "Mot de passe du rôle $1 introuvable dans $ROLES." >&2
    exit 4
  fi
  printf '%s;Username=%s;Password=%s' "$base_de_la_chaine" "$1" "$mdp"
}

if [ ! -f "$ENV" ]; then
  umask 077
  {
    echo "# Engendré par demarrer-demo.sh le $(date -u +%Y-%m-%dT%H:%MZ). Valeurs locales seulement."
    echo "JWT_SIGNING_KEY=$(aleatoire 48)"
    echo "PALIER_CLE_LOCALE=$(aleatoire 32)"
    echo "PALIER_CHAINE_API=$(chaine palier_app)"
    echo "PALIER_CHAINE_AUTH=$(chaine palier_auth)"
    echo "PALIER_CHAINE_MIGRATIONS=$(chaine palier_migrations)"
    echo "PALIER_PORT_API=${PALIER_PORT_API:-5025}"
    echo "PALIER_PORT_COURRIER=${PALIER_PORT_COURRIER:-8025}"
    echo "PALIER_PORT_BASE=${PALIER_PORT_BASE:-55432}"
  } > "$ENV"
  echo "$ENV engendré."
fi

# Les ports réellement retenus, relus dans le fichier : c'est lui que Compose lit.
port() { sed -n "s/^$1=//p" "$ENV" | tail -n 1; }
PORT_API="$(port PALIER_PORT_API)"
PORT_COURRIER="$(port PALIER_PORT_COURRIER)"

# --- Construire et lancer ---------------------------------------------------
# `db/amorcage/` est monté dans le conteneur PostgreSQL, qui le lit sous son
# propre utilisateur, étranger au poste. Sur un poste dont le masque de création
# est 007, le dossier n'est pas lisible par les « autres » et l'amorçage échoue
# sur « Permission denied » (mesuré sous Podman sans privilèges le 28/09/2026).
# Ces fichiers sont publics : les rendre lisibles ne découvre rien.
chmod -R a+rX db/amorcage

echo "Moteur : ${compose[*]}"
# L'image est construite UNE fois, par le service `preparation` ; `api` la
# réutilise. Laisser `up --build` construire les deux services lançait deux
# compilations .NET en parallèle sous podman-compose.
"${compose[@]}" build preparation
"${compose[@]}" up -d

echo -n "Attente de l'API sur le port $PORT_API "
for _ in $(seq 1 90); do
  if curl -fs -o /dev/null "http://127.0.0.1:$PORT_API/openapi/v1.json"; then
    echo
    echo
    echo "Palier API est prête."
    echo "  Scalar (essayer l'API) : http://localhost:$PORT_API/scalar"
    echo "  Document OpenAPI       : http://localhost:$PORT_API/openapi/v1.json"
    echo "  Courriels (Mailpit)    : http://localhost:$PORT_COURRIER"
    echo
    echo "Arrêter : ./demarrer-demo.sh arreter   Tout effacer : ./demarrer-demo.sh effacer"
    exit 0
  fi
  echo -n "."
  sleep 2
done

echo
echo "L'API n'a pas répondu en trois minutes. Journal :" >&2
"${compose[@]}" logs --tail 60 api preparation >&2 || true
exit 1
