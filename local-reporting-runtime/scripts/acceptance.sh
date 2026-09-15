#!/usr/bin/env bash
# End-to-end acceptance run against the real platform stack. Records evidence in evidence/.
# Steps: authorized sign-in -> profile/party -> instance create -> invalid data rejected -> valid data
# accepted -> attachment uploaded -> process next -> Storage round-trip -> unauthorized user denied.
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"
EV="evidence"; mkdir -p "$EV"
RUN_ID="$(date -u +%Y%m%dT%H%M%SZ)"
OUT="$EV/acceptance-$RUN_ID.md"
pass=0; failc=0
check() { local name="$1"; shift; if eval "$*"; then echo "- PASS: $name" | tee -a "$OUT"; pass=$((pass+1)); else echo "- FAIL: $name" | tee -a "$OUT"; failc=$((failc+1)); fi; }
req() { # req <jar> <method> <url> [curl args...] ; sets BODY, CODE, REQID
  local jar="$1" method="$2" url="$3"; shift 3
  local xsrf; xsrf=$(grep -P "\tXSRF-TOKEN\t" "$jar" | cut -f7)
  BODY=$(curl -s --cacert "$CA_CERT" -b "$jar" -c "$jar" -X "$method" -H "X-XSRF-TOKEN: $xsrf" \
    -D /tmp/lrr-headers -w '\n%{http_code}' "$url" "$@")
  CODE=${BODY##*$'\n'}; BODY=${BODY%$'\n'*}
  REQID=$(grep -i '^x-request-id:' /tmp/lrr-headers | tr -d '\r' | awk '{print $2}')
}
json() { python3 -c "import json,sys; d=json.load(sys.stdin); print($1)" 2>/dev/null; }

echo "# Acceptance run $RUN_ID" > "$OUT"
echo >> "$OUT"

# ── 1+2. authorized sign-in, profile & party via real Authentication/Profile/Register ─────
JAR=/tmp/lrr-acc-auth.cj
scripts/login.sh "$AUTHORIZED_PID" "$JAR" >/dev/null
check "1 sign-in (MockPorten -> Authentication) issued AltinnStudioRuntime cookie" grep -q AltinnStudioRuntime "$JAR"
req "$JAR" GET "$APP_URL/api/v1/profile/user"
USERID=$(echo "$BODY" | json 'd["userId"]'); PARTYID=$(echo "$BODY" | json 'd["partyId"]')
check "2 profile resolved (userId=$USERID partyId=$PARTYID) [req $REQID]" '[[ "$USERID" == "1001" && "$PARTYID" == "50100001" ]]'
req "$JAR" GET "$APP_URL/api/v1/parties?allowedToInstantiateFilter=true"
check "2 parties from Register (authorized user sees own party)" '[[ "$BODY" == *50100001* ]]'

# ── 3a + 5. instance creation through Authorization -> Storage -> workflow engine ──────────
req "$JAR" POST "$APP_URL/instances?instanceOwnerPartyId=50100001" -H "Content-Type: application/json"
BODY0="$BODY"; INSTANCE=$(echo "$BODY" | json 'd["id"]'); DATAID=$(echo "$BODY" | json '[x["id"] for x in d["data"] if x["dataType"]=="message"][0]')
check "3a authorized user may instantiate: $CODE instance=$INSTANCE [req $REQID]" '[[ "$CODE" == "201" && -n "$INSTANCE" ]]'
echo "instanceId=$INSTANCE" >> "$OUT"; echo "$BODY" > "$EV/instance-$RUN_ID.json"
IURL="$APP_URL/instances/$INSTANCE"

# ── 4. Task_1 (message): fill and submit the first form step ─────────────────────────────
req "$JAR" PUT "$IURL/data/$DATAID?language=nb" -H "Content-Type: application/json" \
  --data '{"ProcessTask":"Task_1","Title":"Quarterly report Q3","Body":"Synthetic reporting submission","Reference":"REF-1","Sender":"TESTPERSON AUTORISERT","GwTargetTask":"Task_2"}'
check "4 Task_1 form data accepted ($CODE)" '[[ "$CODE" == "201" || "$CODE" == "200" ]]'
PDFSET=$(echo "$BODY0" | json '[x["id"] for x in d["data"] if x["dataType"]=="PdfSettings"][0]')
req "$JAR" PUT "$IURL/data/$PDFSET?language=nb" -H "Content-Type: application/json" --data '{"CreatePdf":true}'
check "4 PdfSettings.CreatePdf=true stored -> PdfTask will call pdf3 ($CODE)" '[[ "$CODE" == "201" || "$CODE" == "200" ]]'

# ── 5. attachment upload to real Storage ────────────────────────────────────────────────
printf 'synthetic attachment %s\n' "$RUN_ID" > /tmp/lrr-attach.txt
req "$JAR" POST "$IURL/data?dataType=fileUpload-message" -H "Content-Type: text/plain" \
  -H 'Content-Disposition: attachment; filename="report-attachment.txt"' --data-binary @/tmp/lrr-attach.txt
ATTID=$(echo "$BODY" | json 'd["id"]')
check "5 attachment uploaded ($CODE id=$ATTID) [req $REQID]" '[[ "$CODE" == "201" && -n "$ATTID" ]]'
echo "attachmentId=$ATTID dataId=$DATAID" >> "$OUT"
curl -s --cacert "$CA_CERT" -b "$JAR" -o /tmp/lrr-attach.back "$IURL/data/$ATTID"
check "5 attachment retrievable with identical content" 'cmp -s /tmp/lrr-attach.txt /tmp/lrr-attach.back'

req "$JAR" PUT "$IURL/process/next?language=nb" -H "Content-Type: application/json"
check "4 Task_1 process/next accepted ($CODE)" '[[ "$CODE" == "200" ]]'
req "$JAR" GET "$IURL"
TASK=$(echo "$BODY" | json 'd["process"]["currentTask"]["elementId"]')
FORMID=$(echo "$BODY" | json '[x["id"] for x in d["data"] if x["dataType"]=="ServiceModel-test"][0]')
check "5 instance advanced to $TASK (ServiceModel-test data element $FORMID created)" '[[ "$TASK" == "Task_2" && -n "$FORMID" ]]'

# ── 4. Task_2 (changename form): invalid input rejected, valid input accepted ─────────────
# ChangeNameValidator (app backend): first name containing "test" is an error, GridData percentages must sum to 100.
req "$JAR" PATCH "$IURL/data/$FORMID?language=nb" -H "Content-Type: application/json" --data '{"ignoredValidators":[],"patch":[
  {"op":"add","path":"/NyttNavn-grp-9313","value":{"NyttNavn-grp-9314":{"PersonFornavnNytt-datadef-34758":{"value":"test"},"PersonEtternavnNytt-datadef-34757":{"value":"Rapportør"}}}},
  {"op":"add","path":"/GridData","value":{"TotalGjeld":1000,"Bolig":{"Prosent":50},"Studie":{"Prosent":10},"Kredittkort":{"Prosent":10}}}]}'
check "4 invalid Task_2 data stored as draft ($CODE)" '[[ "$CODE" == "200" ]]'
req "$JAR" GET "$IURL/validate?language=nb"
NERR=$(echo "$BODY" | json 'len([v for v in d if v["severity"]==1])')
check "4 validate reports $NERR error(s) for invalid data" '[[ "${NERR:-0}" -ge 2 ]]'
echo "$BODY" > "$EV/validate-invalid-$RUN_ID.json"
req "$JAR" PUT "$IURL/process/next?language=nb" -H "Content-Type: application/json"
check "4 process/next REJECTED on invalid data ($CODE)" '[[ "$CODE" == "409" ]]'
echo "$BODY" > "$EV/invalid-next-$RUN_ID.json"

req "$JAR" PATCH "$IURL/data/$FORMID?language=nb" -H "Content-Type: application/json" --data '{"ignoredValidators":[],"patch":[
  {"op":"replace","path":"/NyttNavn-grp-9313/NyttNavn-grp-9314/PersonFornavnNytt-datadef-34758/value","value":"Synthia"},
  {"op":"replace","path":"/GridData/Kredittkort/Prosent","value":40},
  {"op":"add","path":"/Innledning-grp-9309","value":{"Kontaktinformasjon-grp-9311":{"MelderFultnavn":{"value":"Synthia Rapportør"}},"NavneendringenGjelderFor-grp-9310":{"SubjektFornavnFolkeregistrert-datadef-34730":{"value":"Synthia"}},"Signerer-grp-9320":{"SignererEkstraArkivDato-datadef-34752":{"value":"2026-09-01"}}}}]}'
check "4 valid Task_2 data accepted ($CODE)" '[[ "$CODE" == "200" ]]'
req "$JAR" GET "$IURL/validate?language=nb"
NERR=$(echo "$BODY" | json 'len([v for v in d if v["severity"]==1])')
check "4 validate clean for valid data ($NERR errors)" '[[ "$NERR" == "0" ]]'
req "$JAR" PUT "$IURL/process/next?language=nb" -H "Content-Type: application/json"
check "4 process/next accepted on valid data ($CODE)" '[[ "$CODE" == "200" ]]'
echo "$BODY" > "$EV/process-next-$RUN_ID.json"
req "$JAR" GET "$IURL"
TASK=$(echo "$BODY" | json 'd["process"]["currentTask"]["elementId"]')
check "5 instance advanced past Task_2 (now $TASK)" '[[ "$TASK" != "Task_2" ]]'

# ── 5. data is in real Storage (direct DB/blob evidence) ─────────────────────────────────
GUID=${INSTANCE#*/}
NDATA=$(docker compose exec -T postgres psql -U platform_storage_admin -d storagedb -tAc \
  "select count(*) from storage.dataelements d join storage.instances i on i.id=d.instanceinternalid where i.alternateid='$GUID'")
check "5 Storage DB holds $NDATA data elements for instance" '[[ "${NDATA:-0}" -ge 3 ]]'
NPDF=$(docker compose exec -T postgres psql -U platform_storage_admin -d storagedb -tAc \
  "select count(*) from storage.dataelements d join storage.instances i on i.id=d.instanceinternalid where i.alternateid='$GUID' and d.element->>'ContentType'='application/pdf'")
check "5 pdf3 rendered $NPDF receipt PDF(s) into real Storage" '[[ "${NPDF:-0}" -ge 1 ]]'
docker compose exec -T postgres psql -U platform_storage_admin -d storagedb -tAc \
  "select event->>'EventType', event->>'Created', event->>'DataId' from storage.instanceevents where instance='$GUID' order by id" \
  > "$EV/instance-events-$RUN_ID.txt"

# ── 6. submission events reach real Events and the local subscriber (via Service Bus workers) ─
sleep 10
NEV=$(docker compose exec -T postgres psql -U platform_events_admin -d eventsdb -tAc \
  "select count(*) from events.events where cloudevent->>'resourceinstance'='$INSTANCE'")
check "6 real Events stored $NEV cloud events for the instance" '[[ "${NEV:-0}" -ge 5 ]]'
docker compose exec -T postgres psql -U platform_events_admin -d eventsdb -tAc \
  "select cloudevent->>'id', cloudevent->>'type', cloudevent->>'time' from events.events where cloudevent->>'resourceinstance'='$INSTANCE' order by sequenceno" \
  > "$EV/events-$RUN_ID.txt"
NDLV=$(python3 -c "
import json,sys
n=0
for l in open('data/subscriber/received.jsonl'):
    d=json.loads(l)
    if d['event'].get('resourceinstance')=='$INSTANCE': n+=1; print(d['receivedAt'], d['event']['type'], d['event']['id'], d.get('traceparent'), file=sys.stderr)
print(n)" 2>"$EV/subscriber-$RUN_ID.txt")
check "6 local subscriber received $NDLV webhook deliveries for the instance" '[[ "${NDLV:-0}" -ge 5 ]]'
docker compose exec -T postgres psql -U platform_events_admin -d eventsdb -tAc \
  "select t.activity, t.responsecode, t.eventtype, t.cloudeventid from events.trace_log t join events.events e on e.cloudevent->>'id'=t.cloudeventid::text where e.cloudevent->>'resourceinstance'='$INSTANCE' order by t.sequenceno" \
  > "$EV/events-trace-$RUN_ID.txt"
NWH=$(grep -c "WebhookPostResponse|200" "$EV/events-trace-$RUN_ID.txt" || true)
check "6 Events workers: Registered -> OutboundQueue -> WebhookPostResponse 200 ($NWH deliveries traced)" '[[ "${NWH:-0}" -ge 5 ]]'

# ── 3b. unauthorized user denied by real Authorization / Access Management ───────────────
JAR2=/tmp/lrr-acc-unauth.cj
scripts/login.sh "$UNAUTHORIZED_PID" "$JAR2" >/dev/null
check "3b unauthorized user signs in (identity is real, rights are not)" grep -q AltinnStudioRuntime "$JAR2"
req "$JAR2" GET "$IURL"
check "3b unauthorized user reading authorized user's instance -> $CODE [req $REQID]" '[[ "$CODE" == "403" ]]'
req "$JAR2" POST "$APP_URL/instances?instanceOwnerPartyId=50100001" -H "Content-Type: application/json"
check "3b unauthorized user instantiating for party 50100001 -> $CODE [req $REQID]" '[[ "$CODE" == "403" ]]'
req "$JAR2" POST "$APP_URL/instances?instanceOwnerPartyId=50100003" -H "Content-Type: application/json"
check "3b unauthorized user instantiating for org $REPORTING_ORGNO -> $CODE [req $REQID]" '[[ "$CODE" == "403" ]]'

# handle for scripts/restart-check.sh
printf 'INSTANCE=%s\nATTID=%s\nATTACHMENT_SHA=%s\n' "$INSTANCE" "$ATTID" "$(sha256sum /tmp/lrr-attach.txt | cut -c1-64)" > "$EV/last-instance.env"

echo >> "$OUT"; echo "passed=$pass failed=$failc" | tee -a "$OUT"
[[ $failc -eq 0 ]]
