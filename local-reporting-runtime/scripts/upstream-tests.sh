#!/usr/bin/env bash
# Acceptance 8: run each platform fork's own test suites the way its CI does, and write a pass/fail/skip
# summary to evidence/upstream-tests-<run>.md (raw logs + TRX under logs/upstream-tests/<run>/).
#
#   scripts/upstream-tests.sh                  all suites
#   scripts/upstream-tests.sh storage profile  selected suites
#
# Storage and Profile tests expect PostgreSQL (and Storage also Azurite) on localhost:5432/10000, which
# would collide with this stack's own databases. They run inside an SDK container that shares a network
# namespace with a throwaway database, so the stack's ./data is never touched. The other suites start
# their own dependencies with Testcontainers and run on the host.
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"
export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"; export PATH="$DOTNET_ROOT:$PATH"
R="$(cd "$LRR_DIR/../.." && pwd)"
RUN_ID="$(date -u +%Y%m%dT%H%M%SZ)"
OUTDIR="$LRR_DIR/logs/upstream-tests/$RUN_ID"; mkdir -p "$OUTDIR"
SUMMARY="$LRR_DIR/evidence/upstream-tests-$RUN_ID.md"
SDK_IMAGE="mcr.microsoft.com/dotnet/sdk:10.0"
SUITES=("$@"); [[ ${#SUITES[@]} -gt 0 ]] || SUITES=(storage events authentication authorization accessmanagement register profile)

# The default inotify instance limit (128) is exhausted by the WebApplicationFactory-heavy suites.
if [[ "$(cat /proc/sys/fs/inotify/max_user_instances)" -lt 1024 ]]; then
  log "fs.inotify.max_user_instances is below 1024; the Authentication/Auth suites will fail with inotify errors"
fi

{ echo "# Upstream test suites $RUN_ID"; echo
  echo "| Suite | Repository @ commit | Command | Total | Passed | Failed | Skipped | Exit |"
  echo "|---|---|---|---|---|---|---|---|"; } > "$SUMMARY"

# Sum TRX counters (VSTest and Microsoft.Testing.Platform both write them) under a directory.
trx_counts() {
  python3 - "$1" <<'PY'
import glob, sys, xml.etree.ElementTree as ET
t = p = f = s = 0
for path in glob.glob(sys.argv[1] + "/**/*.trx", recursive=True):
    c = ET.parse(path).getroot().find(".//{*}ResultSummary/{*}Counters")
    if c is None: continue
    g = lambda k: int(c.get(k, 0))
    t += g("total"); p += g("passed"); f += g("failed") + g("error") + g("timeout") + g("aborted")
    # VSTest leaves skipped tests out of every counter except total.
    s += max(g("notExecuted") + g("inconclusive"), g("total") - g("passed") - g("failed") - g("error") - g("timeout") - g("aborted"))
print(t, p, f, s)
PY
}

record() { # record <suite> <repo> <command> <exit>
  local suite="$1" repo="$2" cmd="${3//$LRR_DIR\//}" code="$4" counts
  counts=$(trx_counts "$OUTDIR/$suite")
  read -r total passed failed skipped <<<"$counts"
  echo "| $suite | \`$repo\` @ \`$(git -C "$R/$repo" rev-parse --short HEAD)\` | \`$cmd\` | $total | $passed | $failed | $skipped | $code |" >> "$SUMMARY"
  log "$suite: total=$total passed=$passed failed=$failed skipped=$skipped exit=$code"
}

host_suite() { # host_suite <suite> <repo> <workdir-relative> <dotnet test args...>
  local suite="$1" repo="$2" wd="$3"; shift 3
  log "running $suite (host, Testcontainers)"
  local code=0
  (cd "$R/$repo/$wd" && dotnet test "$@") > "$OUTDIR/$suite.log" 2>&1 || code=$?
  record "$suite" "$repo" "dotnet test $*" "$code"
}

# isolated_suite <suite> <repo> <pg-image> <admin-user> <db> <app-role> <with-azurite> <dotnet test args...>
isolated_suite() {
  local suite="$1" repo="$2" image="$3" admin="$4" db="$5" role="$6" azurite="$7"; shift 7
  local pod="lrr-ut-$suite" code=0
  log "running $suite (isolated SDK container + throwaway $image)"
  docker rm -f "$pod" "$pod-azurite" >/dev/null 2>&1 || true
  docker run -d --name "$pod" -e POSTGRES_USER="$admin" -e POSTGRES_PASSWORD=Password -e POSTGRES_DB="$db" \
    "$image" -c max_connections=200 >/dev/null
  wait_for "$suite postgres" 60 docker exec "$pod" pg_isready -U "$admin" -d "$db"
  sleep 2  # pg_isready reports ready during the init-script restart; give it a moment
  wait_for "$suite postgres" 60 docker exec "$pod" pg_isready -U "$admin" -d "$db"
  # Same role the fork's dbsetup.sh creates for CI.
  docker exec -e PGPASSWORD=Password "$pod" psql -v ON_ERROR_STOP=1 -U "$admin" -d "$db" \
    -c "DO \$\$ BEGIN CREATE ROLE $role WITH LOGIN PASSWORD 'Password'; EXCEPTION WHEN duplicate_object THEN NULL; END \$\$;" >/dev/null
  if [[ "$azurite" == "1" ]]; then
    docker run -d --name "$pod-azurite" --network "container:$pod" mcr.microsoft.com/azure-storage/azurite:3.35.0 \
      azurite --blobHost 0.0.0.0 --queueHost 0.0.0.0 --tableHost 0.0.0.0 --skipApiVersionCheck >/dev/null
  fi
  # Pre-create the container HOME and mount points as the host user, or Docker creates them root-owned.
  local chome="$LRR_DIR/logs/upstream-tests/.home"
  mkdir -p "$HOME/.nuget/packages" "$chome/.nuget/packages"
  docker run --rm --network "container:$pod" --user "$(id -u):$(id -g)" \
    -e HOME=/tmp/home -e DOTNET_CLI_TELEMETRY_OPTOUT=1 -e DOTNET_NOLOGO=1 \
    -v "$chome:/tmp/home" -v "$HOME/.nuget/packages:/tmp/home/.nuget/packages" \
    -v "$R/$repo:/src" -v "$OUTDIR/$suite:/results" \
    -w /src "$SDK_IMAGE" dotnet test "$@" --results-directory /results --logger "trx" \
    > "$OUTDIR/$suite.log" 2>&1 || code=$?
  docker rm -f "$pod" "$pod-azurite" >/dev/null 2>&1 || true
  record "$suite" "$repo" "dotnet test $*" "$code"
}

for suite in "${SUITES[@]}"; do
  mkdir -p "$OUTDIR/$suite"
  case "$suite" in
    storage) isolated_suite storage altinn-storage postgres:16 platform_storage_admin storagedb platform_storage 1 \
               Altinn.Platform.Storage.slnx -c Release ;;
    profile) isolated_suite profile altinn-profile postgres:17 platform_profile_admin profiledb platform_profile 0 \
               Altinn.Profile.sln -c Release ;;
    events) host_suite events altinn-events . Altinn.Platform.Events.sln -c Release \
              --logger trx --results-directory "$OUTDIR/events" ;;
    authentication) host_suite authentication altinn-authentication . \
              test/Altinn.Platform.Authentication.Tests/Altinn.Platform.Authentication.Tests.csproj \
              --logger trx --results-directory "$OUTDIR/authentication" ;;
    register) host_suite register altinn-register . --solution Altinn.Register.sln -c Release \
              --results-directory "$OUTDIR/register" --report-xunit-trx ;;
    # altinn-auth CI runs each vertical's unit lane and integration lane separately.
    authorization|accessmanagement)
      app=Altinn.Authorization; [[ "$suite" == accessmanagement ]] && app=Altinn.AccessManagement
      host_suite "$suite" altinn-auth "src/apps/$app" -c Release -- \
        --results-directory "$OUTDIR/$suite/unit" --report-xunit-trx --filter-trait "Category=Unit" --ignore-exit-code 8
      mv "$OUTDIR/$suite.log" "$OUTDIR/$suite-unit.log"
      sed -i '$ s/^| '"$suite"' /| '"$suite"' (unit) /' "$SUMMARY"
      host_suite "$suite" altinn-auth "src/apps/$app" -c Release -p:TestLane=Integration -- \
        --results-directory "$OUTDIR/$suite/integration" --report-xunit-trx --filter-trait "Category=Integration" --ignore-exit-code 8
      sed -i '$ s/^| '"$suite"' /| '"$suite"' (integration) /' "$SUMMARY"
      ;;
    *) fail "unknown suite $suite" ;;
  esac
done
log "wrote $SUMMARY"
