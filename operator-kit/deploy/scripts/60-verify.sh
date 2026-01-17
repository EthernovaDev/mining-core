#!/usr/bin/env bash
set -euo pipefail

DOMAIN="${DOMAIN:-}"

systemctl status miningcore nginx postgresql --no-pager

ss -ltnp | egrep ':80|:443|:4000|:4073|:4074' || true

curl -s http://127.0.0.1:4000/api/pools | head -n 5 || true

if [ -n "$DOMAIN" ]; then
  curl -I "https://$DOMAIN/" || true
  curl -s "https://$DOMAIN/api/pools" | head -n 5 || true
fi
