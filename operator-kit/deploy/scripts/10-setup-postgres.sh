#!/usr/bin/env bash
set -euo pipefail

if [ "$(id -u)" -ne 0 ]; then
  echo "Run as root"
  exit 1
fi

DB_USER="${DB_USER:-miningcore}"
DB_NAME="${DB_NAME:-miningcore}"
PASS_FILE="${PASS_FILE:-/etc/miningcore/db.pass}"

mkdir -p /etc/miningcore

if [ -f "$PASS_FILE" ]; then
  DB_PASS="$(cat "$PASS_FILE")"
else
  read -rsp "Postgres password for $DB_USER: " DB_PASS
  echo
  if [ -z "$DB_PASS" ]; then
    echo "Empty password not allowed"
    exit 1
  fi
  echo "$DB_PASS" > "$PASS_FILE"
  chmod 600 "$PASS_FILE"
fi

if ! sudo -u postgres psql -tAc "SELECT 1 FROM pg_roles WHERE rolname='$DB_USER'" | grep -q 1; then
  sudo -u postgres psql -v pw="$DB_PASS" -c "CREATE USER $DB_USER WITH LOGIN PASSWORD :'pw';"
else
  sudo -u postgres psql -v pw="$DB_PASS" -c "ALTER USER $DB_USER WITH PASSWORD :'pw';"
fi

if ! sudo -u postgres psql -tAc "SELECT 1 FROM pg_database WHERE datname='$DB_NAME'" | grep -q 1; then
  sudo -u postgres psql -c "CREATE DATABASE $DB_NAME OWNER $DB_USER;"
fi

schema_path="/opt/miningcore/src/Miningcore/Persistence/Postgres/Scripts/createdb.sql"
if [ -f "$schema_path" ]; then
  sudo -u postgres psql -d "$DB_NAME" -f "$schema_path"
fi
