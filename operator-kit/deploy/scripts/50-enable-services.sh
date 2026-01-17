#!/usr/bin/env bash
set -euo pipefail

if [ "$(id -u)" -ne 0 ]; then
  echo "Run as root"
  exit 1
fi

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SYSTEMD_DIR="$SCRIPT_DIR/../configs/systemd"
OVERRIDE="${OVERRIDE_SYSTEMD:-0}"

install_unit() {
  local src="$1"
  local dest="/etc/systemd/system/$(basename "$src")"

  if [ ! -f "$src" ]; then
    return
  fi

  if [ -f "$dest" ] && [ "$OVERRIDE" != "1" ]; then
    echo "Skipping existing $dest (set OVERRIDE_SYSTEMD=1 to replace)"
    return
  fi

  if [ -f "$dest" ]; then
    cp "$dest" "$dest.bak.$(date +%Y%m%d-%H%M%S)"
  fi

  cp "$src" "$dest"
}

install_unit "$SYSTEMD_DIR/miningcore.service"
install_unit "$SYSTEMD_DIR/ethernova.service"

systemctl daemon-reload
systemctl enable --now postgresql
systemctl enable --now miningcore

if systemctl list-unit-files | grep -q '^ethernova.service'; then
  systemctl enable --now ethernova
fi

systemctl restart nginx
