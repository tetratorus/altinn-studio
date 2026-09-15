"""Minimal local CloudEvents webhook subscriber for Altinn Events.

Accepts POSTed CloudEvents (structured JSON), logs them to stdout and appends them to
/data/received.jsonl so acceptance tests can prove event delivery. Answers 200 to
the platform.events.validatesubscription event so subscription validation succeeds.
"""

import json
import os
import sys
from datetime import datetime, timezone
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

DATA_FILE = os.environ.get("SUBSCRIBER_DATA_FILE", "/data/received.jsonl")


class Handler(BaseHTTPRequestHandler):
    def do_GET(self):
        if self.path.startswith("/received"):
            body = b""
            if os.path.exists(DATA_FILE):
                with open(DATA_FILE, "rb") as f:
                    body = f.read()
            self.send_response(200)
            self.send_header("Content-Type", "application/x-ndjson")
            self.end_headers()
            self.wfile.write(body)
            return
        self.send_response(200)
        self.send_header("Content-Type", "text/plain")
        self.end_headers()
        self.wfile.write(b"lrr-subscriber ok\n")

    def do_POST(self):
        length = int(self.headers.get("Content-Length") or 0)
        raw = self.rfile.read(length) if length else b""
        try:
            event = json.loads(raw.decode("utf-8")) if raw else {}
        except json.JSONDecodeError:
            event = {"raw": raw.decode("utf-8", "replace")}
        record = {
            "receivedAt": datetime.now(timezone.utc).isoformat(),
            "path": self.path,
            "traceparent": self.headers.get("traceparent"),
            "event": event,
        }
        os.makedirs(os.path.dirname(DATA_FILE), exist_ok=True)
        with open(DATA_FILE, "a", encoding="utf-8") as f:
            f.write(json.dumps(record) + "\n")
        print(
            f"received type={event.get('type')} id={event.get('id')} source={event.get('source')} subject={event.get('subject')}",
            flush=True,
        )
        self.send_response(200)
        self.end_headers()

    def log_message(self, fmt, *args):
        sys.stderr.write("%s - %s\n" % (self.address_string(), fmt % args))


if __name__ == "__main__":
    port = int(os.environ.get("PORT", "8085"))
    print(f"lrr-subscriber listening on {port}", flush=True)
    ThreadingHTTPServer(("0.0.0.0", port), Handler).serve_forever()
