#!/usr/bin/env bash
# Build the platform service images from the sibling forks (~/repos/altinn-*) plus MockPorten and the app.
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"
R="$LRR_DIR/../.."
build() { log "building $1"; docker build -q -t "lrr/$1:local" -f "$2" "$3" >/dev/null; }
build storage          "$R/altinn-storage/Dockerfile"          "$R/altinn-storage"
build events           "$R/altinn-events/Dockerfile"           "$R/altinn-events"
build authentication   "$R/altinn-authentication/Dockerfile"   "$R/altinn-authentication"
build register         "$R/altinn-register/Dockerfile"         "$R/altinn-register"
build profile          "$R/altinn-profile/Dockerfile"          "$R/altinn-profile"
build authorization    "$R/altinn-auth/src/apps/Altinn.Authorization/Dockerfile"    "$R/altinn-auth"
build accessmanagement "$R/altinn-auth/src/apps/Altinn.AccessManagement/Dockerfile" "$R/altinn-auth"
./scripts/build-app.sh
docker compose build mockporten app
