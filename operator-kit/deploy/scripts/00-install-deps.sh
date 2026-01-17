#!/usr/bin/env bash
set -euo pipefail

if [ "$(id -u)" -ne 0 ]; then
  echo "Run as root"
  exit 1
fi

apt update
apt install -y \
  ca-certificates \
  curl \
  git \
  jq \
  gnupg \
  lsb-release \
  nginx \
  ufw \
  postgresql \
  postgresql-contrib \
  build-essential \
  pkg-config \
  cmake \
  libssl-dev \
  libboost-all-dev \
  libsodium-dev \
  libzmq5-dev \
  certbot \
  python3-certbot-nginx

if ! command -v dotnet >/dev/null 2>&1; then
  . /etc/os-release
  version_id="${VERSION_ID:-22.04}"
  case "$version_id" in
    24.04)
      pkg_url="https://packages.microsoft.com/config/ubuntu/24.04/packages-microsoft-prod.deb"
      ;;
    22.04)
      pkg_url="https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb"
      ;;
    *)
      pkg_url="https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb"
      ;;
  esac

  curl -fsSL "$pkg_url" -o /tmp/packages-microsoft-prod.deb
  dpkg -i /tmp/packages-microsoft-prod.deb
  apt update
  apt install -y dotnet-sdk-6.0
fi
