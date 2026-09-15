#!/usr/bin/env bash
# Acceptance 9: stop the whole stack (containers removed, ./data kept), start it again, and prove the
# instance from the last acceptance.sh run — form data, attachment bytes, PDFs, instance events — is
# still there in real Storage and readable through the app with a fresh real sign-in.
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"
EV="evidence"; source "$EV/last-instance.env"
RUN_ID="$(date -u +%Y%m%dT%H%M%SZ)"; OUT="$EV/restart-$RUN_ID.md"
pass=0; failc=0
check() { local name="$1"; shift; if eval "$*"; then echo "- PASS: $name" | tee -a "$OUT"; pass=$((pass+1)); else echo "- FAIL: $name" | tee -a "$OUT"; failc=$((failc+1)); fi; }
json() { python3 -c "import json,sys; d=json.load(sys.stdin); print($1)" 2>/dev/null; }
echo "# Restart persistence check $RUN_ID (instance $INSTANCE)" > "$OUT"; echo >> "$OUT"

BEFORE=$(docker compose ps -q | wc -l)
log "stopping the stack ($BEFORE containers)"; ./scripts/down.sh >/dev/null 2>&1
check "9 all containers removed after down.sh (kept ./data)" '[[ "$(docker compose ps -q | wc -l)" == "0" ]]'
log "starting the stack again"; ./scripts/up.sh > "$EV/restart-up-$RUN_ID.log" 2>&1 || { echo "- FAIL: up.sh exited non-zero (see restart-up-$RUN_ID.log)" | tee -a "$OUT"; exit 1; }
echo "- up.sh completed at $(date -u +%FT%TZ); $(docker compose ps -q | wc -l) containers running" | tee -a "$OUT"

check "9 Register seed survived (no re-seed needed: parties present)" \
  '[[ "$(psql_lrr -d register -tAc "select count(*) from register.party where id in (50100001,50100002,50100003,50100004)")" == "4" ]]'
check "9 Storage DB still holds the instance" \
  '[[ "$(psql_lrr -d storagedb -tAc "select count(*) from storage.instances where alternateid='"'"'${INSTANCE#*/}'"'"'")" == "1" ]]'
NDE=$(psql_lrr -d storagedb -tAc "select count(*) from storage.dataelements where instanceinternalid=(select id from storage.instances where alternateid='${INSTANCE#*/}')")
check "9 Storage DB still holds $NDE data elements for the instance" '[[ "$NDE" -ge 7 ]]'

JAR=/tmp/lrr-restart.cj; rm -f "$JAR"
scripts/login.sh "$AUTHORIZED_PID" "$JAR" >/dev/null
check "9 fresh sign-in after restart (MockPorten -> Authentication)" grep -q AltinnStudioRuntime "$JAR"
IURL="$APP_URL/instances/$INSTANCE"
XSRF=$(grep -P "\tXSRF-TOKEN\t" "$JAR" | cut -f7)
CODE=$(curl -s --cacert "$CA_CERT" -b "$JAR" -H "X-XSRF-TOKEN: $XSRF" -o "$EV/instance-after-restart-$RUN_ID.json" -w '%{http_code}' "$IURL")
check "9 instance readable through the app after restart ($CODE)" '[[ "$CODE" == "200" ]]'
TASK=$(json 'd["process"]["currentTask"]["elementId"]' < "$EV/instance-after-restart-$RUN_ID.json")
check "9 process state persisted (currentTask=$TASK)" '[[ "$TASK" == "Task_3" ]]'
NPDF=$(json 'len([x for x in d["data"] if x["contentType"]=="application/pdf"])' < "$EV/instance-after-restart-$RUN_ID.json")
check "9 $NPDF PDF data elements still listed" '[[ "$NPDF" -ge 2 ]]'
CODE=$(curl -s --cacert "$CA_CERT" -b "$JAR" -H "X-XSRF-TOKEN: $XSRF" -o /tmp/lrr-attach.after -w '%{http_code}' "$IURL/data/$ATTID")
check "9 attachment blob retrievable from Azurite via Storage after restart ($CODE)" '[[ "$CODE" == "200" ]]'
check "9 attachment bytes identical (sha256 $ATTACHMENT_SHA)" '[[ "$(sha256sum /tmp/lrr-attach.after | cut -c1-64)" == "$ATTACHMENT_SHA" ]]'
DATAID=$(json '[x["id"] for x in d["data"] if x["dataType"]=="ServiceModel-test"][0]' < "$EV/instance-after-restart-$RUN_ID.json")
CODE=$(curl -s --cacert "$CA_CERT" -b "$JAR" -H "X-XSRF-TOKEN: $XSRF" -o /tmp/lrr-form.after -w '%{http_code}' "$IURL/data/$DATAID")
check "9 Task_2 form data retrievable and holds the accepted value ($CODE)" '[[ "$CODE" == "200" ]] && grep -q Synthia /tmp/lrr-form.after'
NEV=$(psql_lrr -d eventsdb -tAc "select count(*) from events.events where cloudevent->>'resourceinstance'='$INSTANCE'")
check "9 Events DB still holds $NEV cloud events for the instance" '[[ "$NEV" -ge 6 ]]'
NSUB=$(psql_lrr -d eventsdb -tAc "select count(*) from events.subscription")
check "9 Events subscription persisted (subscribe.sh was a no-op)" '[[ "$NSUB" -ge 1 ]]'

echo >> "$OUT"; echo "passed=$pass failed=$failc" | tee -a "$OUT"
[[ $failc -eq 0 ]]
