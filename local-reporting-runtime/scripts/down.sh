#!/usr/bin/env bash
# Stop the stack. Persistent state lives in bind mounts under ./data (Postgres, Azurite blobs/queues/tables,
# RabbitMQ, SQL Server, Lowkey Vault, subscriber log, app data-protection keys) and is kept.
# Pass --purge to also delete ./data (full reset; next up.sh re-runs migrations and seeds).
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"
docker compose down --remove-orphans
if [[ "${1:-}" == "--purge" ]]; then
  log "purging ./data"
  docker run --rm -v "$LRR_DIR/data:/d" alpine sh -c 'rm -rf /d/*'
fi
