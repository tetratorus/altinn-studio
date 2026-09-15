#!/usr/bin/env bash
# Register the local webhook subscriber with real Events for all frontend-test app events.
# The ttd service-owner token comes from the MockPorten JWT-bearer grant + real Authentication exchange;
# Events asks real Authorization (XACML "read" on the app resource) before accepting the subscription,
# then its validation worker POSTs platform.events.validatesubscription to the endpoint.
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"
T=$(scripts/maskinporten-token.sh "altinn:serviceowner/instances.read altinn:events.subscribe")
existing=$(curl -sf --cacert "$CA_CERT" -H "Authorization: Bearer $T" "$GATEWAY_URL/events/api/v1/subscriptions" \
  | python3 -c 'import json,sys; print(len(json.load(sys.stdin)["subscriptions"]))')
if [[ "$existing" != "0" ]]; then echo "subscription already registered"; exit 0; fi
curl -sf --cacert "$CA_CERT" -X POST "$GATEWAY_URL/events/api/v1/subscriptions" -H "Authorization: Bearer $T" \
  -H "Content-Type: application/json" --data '{
    "endPoint":"https://subscriber.local.altinn.cloud/webhook",
    "sourceFilter":"https://app.local.altinn.cloud/ttd/frontend-test",
    "resourceFilter":"urn:altinn:resource:app_ttd_frontend-test"}'
echo
