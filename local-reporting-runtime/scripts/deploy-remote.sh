#!/usr/bin/env bash
# Runs ON the deploy server (invoked over SSH by .github/workflows/local-reporting-runtime.yml).
# Installs prerequisites, pulls the CI-built images from GHCR, (re)starts the stack and runs acceptance.
# Env: GHCR_USER GHCR_TOKEN IMAGE_PREFIX IMAGE_TAG SERVICES
set -euo pipefail
LRR_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SUDO=""; [[ $EUID -ne 0 ]] && SUDO="sudo"

if ! command -v docker >/dev/null; then
  curl -fsSL https://get.docker.com | $SUDO sh
  [[ $EUID -ne 0 ]] && $SUDO usermod -aG docker "$USER"
fi
if ! command -v python3 >/dev/null || ! command -v openssl >/dev/null; then
  $SUDO apt-get update -q && $SUDO apt-get install -yq python3 openssl curl
fi
# Public name: <ip-with-dashes>.sslip.io resolves to this server with no DNS signup. Override with LRR_DOMAIN in .env.
LRR_PUBLIC_IP="$(curl -fsS4 --max-time 10 https://api.ipify.org || hostname -I | awk '{print $1}')"
ENV_FILE="$LRR_DIR/.env"
touch "$ENV_FILE"
env_set() { grep -q "^$1=" "$ENV_FILE" || printf '%s=%s\n' "$1" "$2" >> "$ENV_FILE"; }
env_set LRR_DOMAIN "${LRR_PUBLIC_IP//./-}.sslip.io"
env_set LRR_GATE_USER altinn
env_set LRR_GATE_PASSWORD "$(openssl rand -hex 12)"
env_set LRR_GATE_COOKIE "$(openssl rand -hex 24)"
sed -i "/^LRR_PUBLIC_IP=/d" "$ENV_FILE" && echo "LRR_PUBLIC_IP=$LRR_PUBLIC_IP" >> "$ENV_FILE"
LRR_DOMAIN="$(sed -n 's/^LRR_DOMAIN=//p' "$ENV_FILE")"

# On the server itself every stack name points at the internal gateway, not the public front door.
for h in "" app. mockporten. keyvault. subscriber. pdf. workflow-engine. app-frontend. otel. pgadmin.; do
  n="$h$LRR_DOMAIN"
  grep -qE "^127\.0\.0\.1\s+$n\s*$" /etc/hosts || echo "127.0.0.1 $n" | $SUDO tee -a /etc/hosts >/dev/null
done
$SUDO sysctl -qw fs.inotify.max_user_instances=1024

# Re-exec with the docker group active if the user was just added to it.
if ! docker info >/dev/null 2>&1 && [[ -z "${LRR_SG:-}" ]]; then
  exec sg docker -c "LRR_SG=1 GHCR_USER='${GHCR_USER:-}' GHCR_TOKEN='${GHCR_TOKEN:-}' IMAGE_PREFIX='$IMAGE_PREFIX' IMAGE_TAG='$IMAGE_TAG' SERVICES='$SERVICES' bash '$0'"
fi

[[ -n "${GHCR_TOKEN:-}" ]] && echo "$GHCR_TOKEN" | docker login ghcr.io -u "$GHCR_USER" --password-stdin >/dev/null
for s in $SERVICES; do
  docker pull -q "$IMAGE_PREFIX-$s:$IMAGE_TAG"
  docker tag "$IMAGE_PREFIX-$s:$IMAGE_TAG" "lrr/$s:local"
done
[[ -n "${GHCR_TOKEN:-}" ]] && docker logout ghcr.io >/dev/null

cd "$LRR_DIR"
export COMPOSE_FILE=docker-compose.yml:docker-compose.deploy.yml
if ! grep -q "^LRR_GATE_HASH=" .env; then
  hash="$(docker run --rm caddy:2.11.7-alpine caddy hash-password --plaintext "$(sed -n 's/^LRR_GATE_PASSWORD=//p' .env)")"
  echo "LRR_GATE_HASH='$hash'" >> .env
fi
./scripts/up.sh
./scripts/acceptance.sh
bash -c 'source scripts/lib.sh && docker compose up -d caddy'
echo "Public URL: https://app.$LRR_DOMAIN/ttd/frontend-test/ (user and password: LRR_GATE_USER / LRR_GATE_PASSWORD in $LRR_DIR/.env)"
docker image prune -f >/dev/null
