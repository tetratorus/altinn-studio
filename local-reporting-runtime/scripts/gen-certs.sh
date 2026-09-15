#!/usr/bin/env bash
# Generates development-only certificates for the local reporting runtime.
#
#   certs/ca.crt / ca.key               local root CA (trust this in the browser / OS)
#   certs/local.altinn.cloud.{crt,key}  TLS server cert for the gateway (SAN: local.altinn.cloud, *.local.altinn.cloud)
#   certs/jwt-signing.pfx               JWT signing cert used by Authentication (issuer of Altinn tokens)
#   certs/mockporten-signing.pfx        JWT signing cert used by MockPorten (upstream synthetic IdP)
#   certs/platform-access-token.pfx     passwordless cert used by Altinn.Common.AccessTokenClient to sign
#                                       service-to-service PlatformAccessToken headers (Key Vault substitute)
#   certs/maskinporten-client.{key,jwk.json,public.jwk.json}
#                                       RSA key pair for the app's synthetic Maskinporten client: the private JWK
#                                       (base64 in certs/maskinporten-client.jwk.b64) goes to the app, the public JWK
#                                       is registered in MockPorten's JWT-bearer grant
#   certs/ca-bundle.crt                 system CA bundle + local CA, mounted into containers as SSL_CERT_FILE
#
# Nothing here is a secret: everything is generated locally and only trusted on this machine.
set -euo pipefail

DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CERTS="$DIR/certs"
mkdir -p "$CERTS"
cd "$CERTS"

# Password for the dev-only PFX files; read by services via LRR_PFX_PASSWORD in .env
PFX_PASSWORD="${LRR_PFX_PASSWORD:-lrr-dev-only}"

if [[ -f ca.key && -f ca.crt ]]; then
  echo "CA already exists, reusing"
else
  openssl req -x509 -newkey rsa:3072 -sha256 -nodes -days 3650 \
    -keyout ca.key -out ca.crt \
    -subj "/CN=Altinn Local Reporting Runtime Dev CA/O=local-dev-only" \
    -addext "basicConstraints=critical,CA:TRUE" -addext "keyUsage=critical,keyCertSign,cRLSign"
fi

if [[ ! -f local.altinn.cloud.crt ]]; then
  openssl req -newkey rsa:2048 -nodes -keyout local.altinn.cloud.key -out server.csr \
    -subj "/CN=local.altinn.cloud"
  cat > server.ext <<EOF
basicConstraints=CA:FALSE
keyUsage=digitalSignature,keyEncipherment
extendedKeyUsage=serverAuth
subjectAltName=DNS:local.altinn.cloud,DNS:*.local.altinn.cloud,DNS:localhost,IP:127.0.0.1
EOF
  openssl x509 -req -in server.csr -CA ca.crt -CAkey ca.key -CAcreateserial -days 825 -sha256 \
    -out local.altinn.cloud.crt -extfile server.ext
  rm -f server.csr server.ext
fi

gen_signing_pfx() {
  local name="$1" cn="$2" pass="${3-$PFX_PASSWORD}"
  [[ -f "$name.pfx" ]] && return
  openssl req -x509 -newkey rsa:2048 -sha256 -nodes -days 825 \
    -keyout "$name.key" -out "$name.crt" -subj "/CN=$cn/O=local-dev-only"
  openssl pkcs12 -export -inkey "$name.key" -in "$name.crt" -out "$name.pfx" \
    -passout "pass:$pass" -keypbe AES-256-CBC -certpbe AES-256-CBC -macalg sha256
  rm -f "$name.key"
}
gen_signing_pfx jwt-signing "altinn-authentication-local"
gen_signing_pfx mockporten-signing "mockporten-local"
gen_signing_pfx platform-access-token "altinn-platform-access-token-local" ""

if [[ ! -f maskinporten-client.jwk.b64 ]]; then
  openssl genrsa -out maskinporten-client.key 2048 2>/dev/null
  python3 "$DIR/scripts/rsa-to-jwk.py" maskinporten-client.key lrr-app-maskinporten-client \
    maskinporten-client.jwk.json maskinporten-client.public.jwk.json
  base64 -w0 maskinporten-client.jwk.json > maskinporten-client.jwk.b64
fi

# Bundle for containers: distro CAs (so NuGet/Azure SDK etc still work) + our CA.
SYSTEM_BUNDLE=/etc/ssl/certs/ca-certificates.crt
{ [[ -f $SYSTEM_BUNDLE ]] && cat "$SYSTEM_BUNDLE"; cat ca.crt; } > ca-bundle.crt

chmod 644 ./*.crt ./*.pfx ./*.key 2>/dev/null || true
echo "certificates written to $CERTS"
