#!/usr/bin/env bash
set -euo pipefail

if [ "$(id -u)" -ne 0 ]; then
  echo "Run as root"
  exit 1
fi

MC_REPO_URL="${MC_REPO_URL:-https://github.com/EthernovaDev/mining-core}"
MC_BRANCH="${MC_BRANCH:-main}"
MC_SRC_DIR="${MC_SRC_DIR:-/opt/miningcore}"
MC_BUILD_DIR="/opt/miningcore/build"

if ! id -u miningcore >/dev/null 2>&1; then
  useradd --system --home /var/lib/miningcore --shell /usr/sbin/nologin miningcore
fi
if ! id -u ethernova >/dev/null 2>&1; then
  useradd --system --home /var/lib/ethernova --shell /usr/sbin/nologin ethernova
fi

mkdir -p /etc/miningcore /var/lib/miningcore /var/lib/ethernova
chown -R miningcore:miningcore /var/lib/miningcore
chown -R ethernova:ethernova /var/lib/ethernova

if [ -d "$MC_SRC_DIR/.git" ]; then
  git -C "$MC_SRC_DIR" fetch --all
  git -C "$MC_SRC_DIR" checkout "$MC_BRANCH"
  git -C "$MC_SRC_DIR" pull
else
  git clone "$MC_REPO_URL" "$MC_SRC_DIR"
  git -C "$MC_SRC_DIR" checkout "$MC_BRANCH"
fi

if [ -f "$MC_SRC_DIR/src/Miningcore/Miningcore.csproj" ]; then
  dotnet publish "$MC_SRC_DIR/src/Miningcore/Miningcore.csproj" -c Release -o "$MC_BUILD_DIR"
fi

if [ -f "$MC_SRC_DIR/src/Miningcore/coins.json" ] && [ ! -f /etc/miningcore/coins.json ]; then
  cp "$MC_SRC_DIR/src/Miningcore/coins.json" /etc/miningcore/coins.json
fi
