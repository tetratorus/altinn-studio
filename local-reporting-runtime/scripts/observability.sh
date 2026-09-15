#!/usr/bin/env bash
# Acceptance 7: collect cross-service correlation evidence for the instance from the last acceptance.sh run.
#  - gateway access log: every hop (app -> storage/register/authorization/events/...) with req_id + W3C traceparent
#  - OTel collector file export: app + workflow-engine spans (trace ids) mentioning the instance
#  - Events: cloud event ids and the trace_log rows written by the Events workers for the instance
#  - service logs (docker) that mention the instance id
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"
EV="evidence"; source "$EV/last-instance.env"
IID="${INSTANCE#*/}"; RUN_ID="$(date -u +%Y%m%dT%H%M%SZ)"; OUT="$EV/observability-$RUN_ID.md"
{
echo "# Cross-service correlation evidence $RUN_ID"
echo; echo "instance: \`$INSTANCE\` attachment data element: \`$ATTID\`"
echo; echo "## Gateway access log (req_id + traceparent per hop, instance id in path)"
echo '```'
docker compose logs --no-log-prefix gateway 2>/dev/null | grep "$IID" | grep -v "GET /ttd/frontend-test/instances/$INSTANCE HTTP" | sed -E 's/ [0-9.]+ "/ "/' | cut -c1-260
echo '```'
echo; echo "## Distinct trace ids touching the instance at the gateway, with the hosts/upstreams they span"
echo '```'
docker compose logs --no-log-prefix gateway 2>/dev/null | grep "$IID" | grep -o 'traceparent="00-[0-9a-f]*' | cut -d- -f2 | sort | uniq -c | sort -rn | head -15
echo '```'
echo; echo "## OTel collector export (app + workflow-engine OTLP spans/logs containing the instance id)"
echo '```'
if [[ -s logs/otel/traces.jsonl ]]; then
python3 - "$IID" <<'PY'
import json,sys
iid=sys.argv[1]; seen=set()
for line in open("logs/otel/traces.jsonl"):
    d=json.loads(line)
    for rs in d.get("resourceSpans",[]):
        svc=next((a["value"].get("stringValue") for a in rs["resource"]["attributes"] if a["key"]=="service.name"),"?")
        for ss in rs.get("scopeSpans",[]):
            for sp in ss.get("spans",[]):
                if iid in json.dumps(sp):
                    k=(sp["traceId"],sp["spanId"])
                    if k in seen: continue
                    seen.add(k)
                    print(f"{svc:20} trace={sp['traceId']} span={sp['spanId']} {sp['name'][:90]}")
print(f"{len(seen)} spans")
PY
else echo "(no OTLP export yet)"; fi
echo '```'
echo; echo "## Events: cloud event ids + worker trace_log for the instance"
echo '```'
psql_lrr -d eventsdb -tAc "select cloudevent->>'id', cloudevent->>'type', cloudevent->>'time' from events.events where cloudevent->>'resourceinstance'='$INSTANCE' order by sequenceno"
echo '---'
psql_lrr -d eventsdb -tAc "select t.cloudeventid, t.activity, t.responsecode, t.subscriptionid, t.time from events.trace_log t join events.events e on e.cloudevent->>'id'=t.cloudeventid::text where e.cloudevent->>'resourceinstance'='$INSTANCE' order by t.sequenceno"
echo '```'
echo; echo "## Service logs mentioning the instance (docker compose logs, per service)"
echo '```'
for s in app storage events authorization workflow-engine pdf3 subscriber; do
  n=$(docker compose logs --no-log-prefix "$s" 2>/dev/null | grep -c "$IID" || true)
  echo "$s: $n log lines"
done
echo '```'
echo; echo "### Sample: app log lines with the instance id"
echo '```'; docker compose logs --no-log-prefix app 2>/dev/null | { grep "$IID" || true; } | head -8 | cut -c1-300; echo '```'
echo; echo "### Sample: workflow-engine log lines with the instance id"
echo '```'; docker compose logs --no-log-prefix workflow-engine 2>/dev/null | { grep "$IID" || true; } | head -6 | cut -c1-300; echo '```'
echo; echo "### Sample: pdf3 render log"
echo '```'; docker compose logs --no-log-prefix pdf3 2>/dev/null | { grep -i "$IID\|render" || true; } | head -6 | cut -c1-300; echo '```'
echo; echo "### Subscriber deliveries (CloudEvent id + traceparent as received)"
echo '```'; grep "$INSTANCE" data/subscriber/received.jsonl | python3 -c 'import json,sys
for l in sys.stdin:
    d=json.loads(l); e=d.get("event",d); print(e.get("id"), e.get("type"), d.get("traceparent") or d.get("headers",{}).get("traceparent"))' ; echo '```'
} > "$OUT"
log "wrote $OUT"; grep -c . "$OUT"
