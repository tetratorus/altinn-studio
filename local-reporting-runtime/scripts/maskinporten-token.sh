#!/usr/bin/env bash
# Smoke test of the machine-token path: sign an RFC 7523 JWT grant with the app's synthetic client key,
# obtain a Maskinporten-style token from the local MockPorten, then exchange it at real Authentication.
# Prints the Altinn service-owner token on stdout.
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

SCOPES="${1:-altinn:serviceowner/instances.read altinn:serviceowner/instances.write}"
AUTHORITY="https://mockporten.local.altinn.cloud/"
KEY="certs/maskinporten-client.key"

b64url() { openssl base64 -A | tr '+/' '-_' | tr -d '='; }
now=$(date +%s)
header=$(printf '{"alg":"RS256","typ":"JWT","kid":"lrr-app-maskinporten-client"}' | b64url)
payload=$(printf '{"iss":"lrr-app-maskinporten-client","aud":"%s","scope":"%s","iat":%d,"exp":%d,"jti":"%s"}' \
  "$AUTHORITY" "$SCOPES" "$now" "$((now + 60))" "$(cat /proc/sys/kernel/random/uuid)" | b64url)
sig=$(printf '%s.%s' "$header" "$payload" | openssl dgst -sha256 -sign "$KEY" -binary | b64url)
assertion="$header.$payload.$sig"

mp_token=$(curl -sf --cacert "$CA_CERT" -X POST "${AUTHORITY}token" \
  --data-urlencode "grant_type=urn:ietf:params:oauth:grant-type:jwt-bearer" \
  --data-urlencode "assertion=$assertion" | python3 -c 'import json,sys; print(json.load(sys.stdin)["access_token"])')

curl -sf --cacert "$CA_CERT" "$GATEWAY_URL/authentication/api/v1/exchange/maskinporten" \
  -H "Authorization: Bearer $mp_token"
