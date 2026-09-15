#!/usr/bin/env bash
# Sign a synthetic user in through the real Authentication service (OIDC authorization-code + PKCE
# against the local MockPorten IdP) and leave the resulting session cookies in a curl cookie jar.
#
#   scripts/login.sh <pid> [cookie-jar]        e.g. scripts/login.sh "$AUTHORIZED_PID" /tmp/auth.cj
#
# Prints the final URL reached after the login round-trip; exits non-zero if no AltinnStudioRuntime
# cookie was issued.
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"
PID_="${1:?pid}"; JAR="${2:-/tmp/lrr-$PID_.cj}"
rm -f "$JAR"
C=(curl -s --cacert "$CA_CERT" -c "$JAR" -b "$JAR")
U="$APP_URL/"
# follow redirects manually until we land on the MockPorten login form
for _ in 1 2 3 4 5; do
  R=$("${C[@]}" -o /tmp/lrr-login-body -w "%{http_code} %{redirect_url}" "$U")
  N=${R#* }; [[ -z "$N" ]] && break; U=$N
done
grep -q 'action="/Authorize"' /tmp/lrr-login-body || fail "did not reach MockPorten login form (last url $U)"
form() { grep -oE "<input[^>]*name=\"$1\"[^>]*>" /tmp/lrr-login-body | grep -oE 'value="[^"]*"' | head -1 | sed 's/^value="//;s/"$//'; }
data=(--data-urlencode "Pid=$PID_" --data-urlencode "Password=${LRR_MOCKPORTEN_PASSWORD:-lrr-synthetic-only}")
for f in Response_type Client_id Redirect_uri Scope State Nonce Acr_values Response_mode Ui_locales Prompt Code_challenge Code_challenge_method Login_hint Claims Request_uri __RequestVerificationToken; do
  data+=(--data-urlencode "$f=$(form "$f")")
done
MP="${U%%/Authorize*}"
U=$("${C[@]}" -o /dev/null -w "%{redirect_url}" -X POST "${data[@]}" "$MP/Authorize")
[[ -n "$U" ]] || fail "MockPorten did not redirect after login POST"
for _ in 1 2 3 4 5 6; do
  R=$("${C[@]}" -o /dev/null -w "%{http_code} %{redirect_url}" "$U")
  N=${R#* }; [[ -z "$N" ]] && break; U=$N
done
grep -q AltinnStudioRuntime "$JAR" || fail "no AltinnStudioRuntime cookie after login (ended at $U, $R)"
echo "$U"
