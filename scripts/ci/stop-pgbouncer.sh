#!/usr/bin/env bash
set -euo pipefail
if [[ -z ${SIGIC_POOL_DIR:-} || ! -f "$SIGIC_POOL_DIR/pgbouncer.pid" ]]; then exit 0; fi
sigic_pool_pid=$(cat "$SIGIC_POOL_DIR/pgbouncer.pid")
[[ "$sigic_pool_pid" =~ ^[0-9]+$ ]] || exit 1
# Refuse unrelated/stale PIDs. Only stop the process started with our exact ini.
if [[ -r /proc/$sigic_pool_pid/cmdline ]] &&
   tr '\0' '\n' < "/proc/$sigic_pool_pid/cmdline" | grep -Fxq -- "$SIGIC_POOL_DIR/pgbouncer.ini"; then
  kill "$sigic_pool_pid"
fi
