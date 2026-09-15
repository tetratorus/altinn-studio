#!/usr/bin/env bash
# One-time (idempotent) bootstrap of dev-only secrets, blob containers and synthetic fixtures.
# Runs after the infrastructure containers are healthy; up.sh calls it.
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

APP_DIR="$LRR_DIR/../src/test/apps/frontend-test/App"

# ── 1. certificates ────────────────────────────────────────────────────────────
./scripts/gen-certs.sh
if [[ ! -f certs/lowkey-vault.p12 ]]; then
  openssl pkcs12 -export -inkey certs/local.altinn.cloud.key -in certs/local.altinn.cloud.crt \
    -out certs/lowkey-vault.p12 -passout "pass:$LRR_PFX_PASSWORD" -name lowkey
fi
chmod 644 certs/*.p12 certs/*.pfx certs/*.key 2>/dev/null || true

# ── 2. key vault secrets (public cert used to VERIFY platform access tokens) ──
# Issuer names follow Altinn.Common.AccessTokenClient: "<issuer>-access-token-public-cert".
# lowkey-vault keys vaults by request host, so the URL must use the same host the services use.
wait_for "lowkey-vault" 60 curl -sk -o /dev/null https://localhost:8443/ping
PUBCERT_B64=$(openssl x509 -in certs/platform-access-token.crt -outform DER | base64 -w0)
for issuer in platform ttd; do
  curl -sSk -o /dev/null -X PUT "https://keyvault.local.altinn.cloud:8443/secrets/${issuer}-access-token-public-cert?api-version=7.4" \
    -H "Authorization: Bearer lowkey" -H "Content-Type: application/json" \
    -d "{\"value\":\"$PUBCERT_B64\",\"contentType\":\"application/x-x509-ca-cert\"}"
done
log "key vault: access-token public certs provisioned"

# ── 3. blob containers + XACML app policy for Authorization ───────────────────
wait_for "azurite" 60 curl -s -o /dev/null http://127.0.0.1:10000/devstoreaccount1?comp=list
docker run --rm --network lrr -v "$APP_DIR/config/authorization/policy.xml:/policy.xml:ro" \
  -e AZURITE_CONN="${AZURITE_CONN//127.0.0.1/azurite}" python:3.12-alpine sh -c '
    pip install -q azure-storage-blob==12.25.1 >/dev/null 2>&1
    python - <<EOF
import os
from azure.storage.blob import BlobServiceClient
svc = BlobServiceClient.from_connection_string(os.environ["AZURITE_CONN"])
for c in ["metadata", "delegationpolicies", "servicedata", "resourceregistry", "authentication-dataprotection",
          "ttd-appsdata"]:  # last one is Storage OrgStorageContainer for org ttd
    try: svc.create_container(c)
    except Exception: pass
svc.get_blob_client("metadata", "ttd/frontend-test/policy.xml").upload_blob(open("/policy.xml","rb"), overwrite=True)
print("policy uploaded")
EOF'

# ── 4. Register synthetic parties/users/roles ─────────────────────────────────
wait_for "register migrations" 300 bash -c "docker compose exec -T postgres psql -U register -d register -tAc \"select 1 from pg_tables where schemaname='register_quartz' and tablename='job_details'\" | grep -q 1"
psql_lrr -d register -f - < infra/register/seed.sql >/dev/null
log "register: synthetic fixtures seeded"

# ── 4b. Access Management: EF migrations + Register import, then schema grants ──
# The EF migrations grant table privileges to platform_authorization but leave schema USAGE to
# environment provisioning; the init job runs as the admin role, the API as platform_authorization.
docker compose up -d accessmanagement-init >/dev/null
wait_for "accessmanagement-init" 600 bash -c "docker compose ps -a accessmanagement-init --format '{{.Status}}' | grep -q 'Exited (0)'"
docker compose exec -T postgres psql -v ON_ERROR_STOP=1 -U postgres -d authorizationdb -tAc "
DO \$\$ DECLARE s text; BEGIN
  FOR s IN SELECT nspname FROM pg_namespace WHERE nspname IN ('dbo','dbo_history','ingest','consent','delegation','accessmanagement') LOOP
    EXECUTE format('GRANT USAGE ON SCHEMA %I TO platform_authorization', s);
    EXECUTE format('GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA %I TO platform_authorization', s);
    EXECUTE format('GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA %I TO platform_authorization', s);
    EXECUTE format('GRANT EXECUTE ON ALL FUNCTIONS IN SCHEMA %I TO platform_authorization', s);
  END LOOP; END \$\$;" >/dev/null
log "accessmanagement: migrations, register import and grants done"

# ── 5. Storage application metadata + texts for ttd/frontend-test ─────────────
wait_for "storage" 300 bash -c "docker compose ps storage --format '{{.Health}}' | grep -q healthy"
docker compose exec -T postgres psql -v ON_ERROR_STOP=1 -U postgres -d storagedb <<EOF
INSERT INTO storage.applications(app, org, application)
VALUES ('frontend-test', 'ttd', \$json\$$(python3 scripts/storage-json.py app "$APP_DIR/config/applicationmetadata.json")\$json\$::jsonb)
ON CONFLICT (org, app) DO UPDATE SET application = EXCLUDED.application;
EOF
for lang in nb en; do
  docker compose exec -T postgres psql -v ON_ERROR_STOP=1 -U postgres -d storagedb <<EOF
INSERT INTO storage.texts(org, app, language, applicationinternalid, textresource)
SELECT 'ttd','frontend-test','$lang', a.id, \$json\$$(python3 scripts/storage-json.py text "$APP_DIR/config/texts/resource.$lang.json" ttd frontend-test)\$json\$::jsonb
FROM storage.applications a WHERE a.org='ttd' AND a.app='frontend-test'
ON CONFLICT (org, app, language) DO UPDATE SET textresource = EXCLUDED.textresource;
EOF
done
log "storage: application metadata + texts registered"
