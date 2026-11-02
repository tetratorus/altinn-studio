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
for h in local.altinn.cloud app mockporten keyvault subscriber pdf workflow-engine app-frontend otel pgadmin; do
  [[ $h == local.altinn.cloud ]] && n=$h || n=$h.local.altinn.cloud
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
./scripts/up.sh
./scripts/acceptance.sh
docker image prune -f >/dev/null
