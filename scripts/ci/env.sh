#!/usr/bin/env bash
# Source this file; defaults match the disposable service in ci.yml.
export SIGIC_DB_HOST="${SIGIC_DB_HOST:-127.0.0.1}"
export SIGIC_DB_PORT="${SIGIC_DB_PORT:-54329}"
export SIGIC_DB_NAME="${SIGIC_DB_NAME:-postgres}"
export SIGIC_DB_ADMIN_USER="${SIGIC_DB_ADMIN_USER:-postgres}"
export SIGIC_DB_ADMIN_PASSWORD="${SIGIC_DB_ADMIN_PASSWORD:-sigic_ci_ephemeral}"
export SIGIC_DB_USER="${SIGIC_DB_USER:-sigic_app}"
export SIGIC_DB_PASSWORD="${SIGIC_DB_PASSWORD:-sigic_fixture_ephemeral}"
export SIGIC_POS_CONNECTION_STRING="${SIGIC_POS_CONNECTION_STRING:-Host=$SIGIC_DB_HOST;Port=6432;Database=sigic_pos_pool;Username=$SIGIC_DB_USER;Password=$SIGIC_DB_PASSWORD;Pooling=false}"
export SIGIC_WEB_CONNECTION_STRING="${SIGIC_WEB_CONNECTION_STRING:-Host=$SIGIC_DB_HOST;Port=6432;Database=sigic_web_pool;Username=$SIGIC_DB_USER;Password=$SIGIC_DB_PASSWORD;Pooling=false}"
