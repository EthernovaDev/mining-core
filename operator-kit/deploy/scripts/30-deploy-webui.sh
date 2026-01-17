#!/usr/bin/env bash
set -euo pipefail

if [ "$(id -u)" -ne 0 ]; then
  echo "Run as root"
  exit 1
fi

WEBUI_REPO_URL="${WEBUI_REPO_URL:-https://github.com/minernl/Miningcore.WebUI.git}"
WEBUI_DIR="${WEBUI_DIR:-/var/www/pool}"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

if [ -d "$WEBUI_DIR/.git" ]; then
  git -C "$WEBUI_DIR" fetch --all
  git -C "$WEBUI_DIR" pull
else
  git clone "$WEBUI_REPO_URL" "$WEBUI_DIR"
fi

git -C "$WEBUI_DIR" config --global --add safe.directory "$WEBUI_DIR" >/dev/null 2>&1 || true

js_file=""
if [ -f "$WEBUI_DIR/miningcore.js" ]; then
  js_file="$WEBUI_DIR/miningcore.js"
elif [ -f "$WEBUI_DIR/js/miningcore.js" ]; then
  js_file="$WEBUI_DIR/js/miningcore.js"
fi

if [ -n "$js_file" ]; then
  if grep -q '^var API = "/api/";' "$js_file"; then
    :
  else
    sed -i 's|^var API = .*|var API = "/api/";|g' "$js_file"
  fi
fi

"$SCRIPT_DIR/apply-webui-patches.sh" "$WEBUI_DIR"

chown -R www-data:www-data "$WEBUI_DIR"
