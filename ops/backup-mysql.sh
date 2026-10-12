#!/usr/bin/env bash
set -euo pipefail

: "${MYSQL_DATABASE:?Set MYSQL_DATABASE}"
: "${MYSQL_CNF:?Set MYSQL_CNF to a protected MySQL client defaults file}"
: "${BACKUP_DIR:?Set BACKUP_DIR outside the web root}"

if [[ ! -r "$MYSQL_CNF" ]]; then
  echo "The MySQL client defaults file is not readable." >&2
  exit 1
fi

umask 077
mkdir -p -- "$BACKUP_DIR"
stamp="$(date -u +%Y%m%dT%H%M%S%N)"
temporary_file="$(mktemp "$BACKUP_DIR/.${MYSQL_DATABASE}-${stamp}.XXXXXX.tmp")"
backup_file="$BACKUP_DIR/${MYSQL_DATABASE}-${stamp}.sql.gz"
trap 'rm -f -- "$temporary_file"' EXIT

mysqldump \
  --defaults-extra-file="$MYSQL_CNF" \
  --protocol=tcp \
  --host="${MYSQL_HOST:-127.0.0.1}" \
  --port="${MYSQL_PORT:-3307}" \
  --single-transaction \
  --routines \
  --triggers \
  --events \
  --set-gtid-purged=OFF \
  "$MYSQL_DATABASE" | gzip -9 > "$temporary_file"

gzip --test "$temporary_file"
mv -- "$temporary_file" "$backup_file"
trap - EXIT
printf 'Backup created: %s\n' "$backup_file"
