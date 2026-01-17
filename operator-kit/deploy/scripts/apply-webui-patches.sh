#!/usr/bin/env bash
set -euo pipefail

WEBUI_DIR="${1:-/var/www/pool}"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PATCH_DIR="${2:-$SCRIPT_DIR/../patches}"

if [ ! -d "$WEBUI_DIR/.git" ]; then
  echo "WebUI repo not found at $WEBUI_DIR"
  exit 1
fi

if [ ! -d "$PATCH_DIR" ]; then
  echo "Patch directory not found at $PATCH_DIR"
  exit 1
fi

git -C "$WEBUI_DIR" config --global --add safe.directory "$WEBUI_DIR" >/dev/null 2>&1 || true

shopt -s nullglob
patches=("$PATCH_DIR"/*.patch)
if [ ${#patches[@]} -eq 0 ]; then
  echo "No patches found in $PATCH_DIR"
  exit 0
fi

for patch in "${patches[@]}"; do
  if git -C "$WEBUI_DIR" apply --check "$patch" >/dev/null 2>&1; then
    git -C "$WEBUI_DIR" apply "$patch"
    echo "Applied $(basename "$patch")"
  elif git -C "$WEBUI_DIR" apply -R --check "$patch" >/dev/null 2>&1; then
    echo "Already applied $(basename "$patch")"
  else
    echo "Patch did not apply cleanly: $patch"
  fi
done
