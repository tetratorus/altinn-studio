#!/usr/bin/env bash
# Build images (if needed), start the whole local reporting runtime and bootstrap fixtures.
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

mkdir -p data/postgres data/azurite data/rabbitmq data/mssql data/lowkey-vault data/subscriber data/app-keys logs/otel logs/app
chmod 777 data/mssql data/lowkey-vault data/subscriber data/app-keys logs/otel logs/app   # written by non-root containers
./scripts/gen-certs.sh
[[ -f certs/lowkey-vault.p12 ]] || openssl pkcs12 -export -inkey certs/local.altinn.cloud.key \
  -in certs/local.altinn.cloud.crt -out certs/lowkey-vault.p12 -passout "pass:$LRR_PFX_PASSWORD" -name lowkey
chmod 644 certs/*.p12 certs/*.pfx certs/*.key 2>/dev/null || true

if [[ "${LRR_BUILD:-0}" == "1" ]]; then
  ./scripts/build-images.sh
fi

log "starting infrastructure"
docker compose up -d postgres azurite rabbitmq sb-sqledge servicebus lowkey-vault otel-collector mockporten subscriber gateway
docker compose up -d register-init profile-init

log "starting platform services"
docker compose up -d register profile authentication authorization accessmanagement storage events workflow-engine pdf3
./scripts/bootstrap.sh

log "starting the reporting app"
docker compose up -d app
wait_for "app" 180 curl -sf --cacert certs/ca.crt -o /dev/null "$APP_URL/api/v1/applicationmetadata"

log "registering the local Events subscriber"
./scripts/events-subscribe.sh

log "up. Browser entry point: $APP_URL/"
docker compose ps
