#!/usr/bin/env bash
# Disposable PostgreSQL only. CI supplies the postgres:16 service on port 54329.
set -euo pipefail
umask 077
sigic_repo_root=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/../.." && pwd)
source "$sigic_repo_root/scripts/ci/env.sh"
if [[ $(id -u) = 0 ]]; then
  echo 'PgBouncer must run as a non-root user.' >&2
  exit 1
fi
[[ "$SIGIC_DB_USER" = sigic_app ]] || { echo 'The DDL/fixture requires sigic_app.' >&2; exit 1; }
export PGHOST="$SIGIC_DB_HOST" PGPORT="$SIGIC_DB_PORT" PGDATABASE="$SIGIC_DB_NAME"
export PGUSER="$SIGIC_DB_ADMIN_USER" PGPASSWORD="$SIGIC_DB_ADMIN_PASSWORD" PGCONNECT_TIMEOUT=2
ready=0
for attempt in $(seq 1 30); do
  if psql -XAt -v ON_ERROR_STOP=1 -c 'SELECT 1' >/dev/null 2>&1; then ready=1; break; fi
  sleep 2
done
[[ "$ready" = 1 ]] || { echo 'PostgreSQL readiness timed out (60s).' >&2; exit 1; }
if psql -XAt -c "SELECT to_regclass('public.productos') IS NOT NULL" | grep -qx t; then
  echo 'Bootstrap requires a fresh disposable database; productos already exists.' >&2
  exit 1
fi
# mktemp path is also exported to the runner before any later step can fail.
export SIGIC_POOL_DIR
SIGIC_POOL_DIR=$(mktemp -d /tmp/sigic-pool.XXXXXXXX)
if [[ -n ${GITHUB_ENV:-} ]]; then printf 'SIGIC_POOL_DIR=%s\n' "$SIGIC_POOL_DIR" >> "$GITHUB_ENV"; fi
cleanup_failed_bootstrap() {
  sigic_status=$?
  if [[ "$sigic_status" != 0 ]]; then
    bash "$sigic_repo_root/scripts/ci/stop-pgbouncer.sh"
    echo "Bootstrap failed; logs (without credentials): $SIGIC_POOL_DIR/pgbouncer.log" >&2
  fi
  exit "$sigic_status"
}
trap cleanup_failed_bootstrap EXIT
ddl="$sigic_repo_root/src/SIGIC.Infrastructure/Persistence/Scripts"
psql -X -v ON_ERROR_STOP=1 -f "$ddl/00_init_schema.sql"
psql -X -v ON_ERROR_STOP=1 -f "$ddl/01_append_only_permissions.sql"
psql -X -v ON_ERROR_STOP=1 -f "$ddl/02_cierres_analiticos.sql"
psql -X -v ON_ERROR_STOP=1 -f "$ddl/03_producto_imagen.sql"
psql -X -v ON_ERROR_STOP=1 -v fixture_password="$SIGIC_DB_PASSWORD" >/dev/null <<'SQL'
SET password_encryption = 'scram-sha-256';
ALTER ROLE sigic_app WITH LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE PASSWORD :'fixture_password';
SQL
psql -XAt -v ON_ERROR_STOP=1 -c "SELECT format('%s%s%s %s%s%s',chr(34),rolname,chr(34),chr(34),rolpassword,chr(34)) FROM pg_authid WHERE rolname='sigic_app' AND rolpassword LIKE 'SCRAM-SHA-256%'" > "$SIGIC_POOL_DIR/userlist.txt"
test -s "$SIGIC_POOL_DIR/userlist.txt"
if command -v pgbouncer >/dev/null && pgbouncer --version | grep -Eq '^PgBouncer 1\.22\.1$'; then
  sigic_pool_bin=$(command -v pgbouncer)
else
  (
    cd "$SIGIC_POOL_DIR"
    sigic_archive=pgbouncer-1.22.1.tar.gz
    sigic_release=https://www.pgbouncer.org/downloads/files/1.22.1
    curl --connect-timeout 10 --max-time 120 --retry 3 -fsSLO "$sigic_release/$sigic_archive"
    sigic_checksum=$(curl --connect-timeout 10 --max-time 30 -fsSL "$sigic_release/$sigic_archive.sha256" | awk 'NR==1 {print $1}')
    [[ "$sigic_checksum" =~ ^[[:xdigit:]]{64}$ ]]
    printf '%s  %s\n' "$sigic_checksum" "$sigic_archive" | sha256sum -c -
    tar xzf "$sigic_archive"
    cd pgbouncer-1.22.1
    ./configure --prefix="$SIGIC_POOL_DIR/dist"
    make -j2 pgbouncer
  )
  sigic_pool_bin="$SIGIC_POOL_DIR/pgbouncer-1.22.1/pgbouncer"
fi
"$sigic_pool_bin" --version | grep -Eq '^PgBouncer 1\.22\.1$'
python3 - "$sigic_repo_root/infrastructure/pgbouncer/pgbouncer.ini.in" <<'PY'
import os, re, sys
from pathlib import Path
template = Path(sys.argv[1]).read_text()
for name in ('SIGIC_DB_HOST', 'SIGIC_DB_PORT', 'SIGIC_DB_NAME', 'SIGIC_POOL_DIR'):
    value = os.environ[name]
    if not re.fullmatch(r'[A-Za-z0-9_./:-]+', value):
        raise SystemExit(f'Invalid ini value: {name}')
    template = template.replace('${' + name + '}', value)
Path(os.environ['SIGIC_POOL_DIR'], 'pgbouncer.ini').write_text(template)
PY
"$sigic_pool_bin" -d "$SIGIC_POOL_DIR/pgbouncer.ini"
export PGUSER="$SIGIC_DB_USER" PGPASSWORD="$SIGIC_DB_PASSWORD"
for alias in sigic_pos_pool sigic_web_pool; do
  ready=0
  for attempt in $(seq 1 30); do
    if psql -XAt -v ON_ERROR_STOP=1 -p 6432 -d "$alias" -c 'SELECT current_user' 2>/dev/null | grep -qx sigic_app; then
      ready=1; break
    fi
    sleep 1
  done
  [[ "$ready" = 1 ]] || { echo "Pool $alias failed readiness." >&2; exit 1; }
done
psql -X -v ON_ERROR_STOP=1 -p 6432 -d pgbouncer -c 'SHOW POOLS'
psql -XAt -v ON_ERROR_STOP=1 -c "SELECT has_table_privilege(current_user,'caja_arqueo','UPDATE') OR has_table_privilege(current_user,'caja_arqueo','DELETE')" | grep -qx f
printf 'Ready: PostgreSQL %s:%s; PgBouncer 1.22.1 on 6432 (50 POS / 30 Web).\n' "$SIGIC_DB_HOST" "$SIGIC_DB_PORT"
printf 'export SIGIC_POOL_DIR=%q\n' "$SIGIC_POOL_DIR"
