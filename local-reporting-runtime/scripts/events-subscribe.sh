#!/usr/bin/env bash
# Register the local webhook subscriber with real Events for all frontend-test app events.
# The ttd service-owner token comes from the MockPorten JWT-bearer grant + real Authentication exchange;
# Events asks real Authorization (XACML "read" on the app resource) before accepting the subscription,
# then its validation worker POSTs platform.events.validatesubscription to the endpoint.
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"
ENDPOINT="https://subscriber.local.altinn.cloud/webhook"
SOURCE_FILTER="https://app.local.altinn.cloud/ttd/frontend-test"
RESOURCE_FILTER="urn:altinn:resource:app_ttd_frontend-test"
T=$(scripts/maskinporten-token.sh "altinn:serviceowner/instances.read altinn:events.subscribe")
# Skip only when this exact subscription exists; any other subscription for ttd must not suppress it.
match=$(curl -sf --cacert "$CA_CERT" -H "Authorization: Bearer $T" "$GATEWAY_URL/events/api/v1/subscriptions" \
  | ENDPOINT="$ENDPOINT" SOURCE_FILTER="$SOURCE_FILTER" RESOURCE_FILTER="$RESOURCE_FILTER" python3 -c '
import json, os, sys
want = (os.environ["ENDPOINT"], os.environ["SOURCE_FILTER"], os.environ["RESOURCE_FILTER"])
subs = json.load(sys.stdin).get("subscriptions") or []
ids = [s["id"] for s in subs if (s.get("endPoint"), s.get("sourceFilter"), s.get("resourceFilter")) == want]
print(ids[0] if ids else "")')
if [[ -n "$match" ]]; then echo "subscription already registered (id=$match)"; exit 0; fi
curl -sf --cacert "$CA_CERT" -X POST "$GATEWAY_URL/events/api/v1/subscriptions" -H "Authorization: Bearer $T" \
  -H "Content-Type: application/json" --data "{
    \"endPoint\":\"$ENDPOINT\",
    \"sourceFilter\":\"$SOURCE_FILTER\",
    \"resourceFilter\":\"$RESOURCE_FILTER\"}"
echo
