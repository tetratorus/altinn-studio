#!/usr/bin/env bash
# Build the platform service images from the sibling forks (~/repos/altinn-*) plus MockPorten and the app.
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"
R="$LRR_DIR/../.."

# Refuse to build from checkouts that differ from forks.lock (wrong commit or uncommitted changes),
# so images always match the documented manifest. LRR_ALLOW_UNPINNED=1 builds them anyway.
drift=0
while read -r repo commit; do
  [[ -z "$repo" || "$repo" == \#* ]] && continue
  dir="$R/$repo"
  if [[ ! -d "$dir/.git" ]]; then log "$repo: missing (run scripts/clone-forks.sh)"; drift=1; continue; fi
  head=$(git -C "$dir" rev-parse HEAD)
  [[ "$head" == "$commit" ]] || { log "$repo: HEAD $head, forks.lock pins $commit"; drift=1; }
  [[ -z "$(git -C "$dir" status --porcelain)" ]] || { log "$repo: uncommitted changes"; drift=1; }
done < forks.lock
if [[ "$drift" == "1" ]]; then
  [[ "${LRR_ALLOW_UNPINNED:-0}" == "1" ]] || fail "fork checkouts differ from forks.lock (set LRR_ALLOW_UNPINNED=1 to build anyway)"
  log "LRR_ALLOW_UNPINNED=1: building from unpinned checkouts"
fi

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
