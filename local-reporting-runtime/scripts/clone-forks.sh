#!/usr/bin/env bash
# Clone any missing sibling fork (../../<repo>) and check it out at the revision pinned in forks.lock.
# Existing checkouts are left untouched; scripts/build-images.sh reports any drift.
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"
R="$LRR_DIR/../.."
BASE="${LRR_FORK_BASE:-https://github.com/tetratorus}"
while read -r repo commit; do
  [[ -z "$repo" || "$repo" == \#* ]] && continue
  if [[ -d "$R/$repo/.git" ]]; then log "$repo: present"; continue; fi
  log "$repo: cloning $BASE/$repo"
  git clone -q "$BASE/$repo.git" "$R/$repo"
  git -C "$R/$repo" checkout -q --detach "$commit"
done < forks.lock
