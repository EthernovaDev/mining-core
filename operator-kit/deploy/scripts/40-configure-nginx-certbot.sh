#!/usr/bin/env bash
set -euo pipefail

if [ "$(id -u)" -ne 0 ]; then
  echo "Run as root"
  exit 1
fi

DOMAIN="${DOMAIN:-}"
EMAIL="${EMAIL:-}"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
TEMPLATE="$SCRIPT_DIR/../configs/nginx-pool.ethnova.net.conf"

if [ -z "$DOMAIN" ]; then
  echo "Set DOMAIN env var, ex: export DOMAIN=pool.example.com"
  exit 1
fi

if [ ! -f "$TEMPLATE" ]; then
  echo "Template not found: $TEMPLATE"
  exit 1
fi

CONF_PATH="/etc/nginx/sites-available/$DOMAIN"
mkdir -p /etc/nginx/sites-available /etc/nginx/sites-enabled

sed "s/{{DOMAIN}}/$DOMAIN/g" "$TEMPLATE" > "$CONF_PATH"
ln -sf "$CONF_PATH" "/etc/nginx/sites-enabled/$DOMAIN"

nginx -t
systemctl reload nginx

if [ "${NO_CERTBOT:-0}" = "1" ]; then
  echo "Skipping certbot (NO_CERTBOT=1)"
  exit 0
fi

if [ -z "$EMAIL" ]; then
  echo "Set EMAIL env var for certbot, ex: export EMAIL=admin@example.com"
  exit 1
fi

certbot --nginx -d "$DOMAIN" --agree-tos -m "$EMAIL" --redirect --non-interactive
