#!/usr/bin/env python3
"""
A minimal stand-in for a Discord incoming webhook, for manually testing the PostyFox
`DiscordWH` connector (src/PostyFox.Infrastructure/Connectors/DiscordWebhookConnector.cs)
without a real Discord server/channel.

Implements exactly the three requests that connector makes, against any path (so it behaves like a
real Discord webhook URL of the form /api/webhooks/<id>/<token>, but doesn't actually validate the
id/token — any path is accepted as its own independent "channel"):

  POST   <path>?wait=true            body: JSON {"content": "..."} or multipart with a
                                      `payload_json` field + `files[n]` parts (matching Discord's
                                      own webhook-with-attachments contract)
                                      -> 200 + JSON {"id": "...", "content": "...", ...}
  DELETE <path>/messages/<id>        -> 204 if found, 404 otherwise

Everything received is kept in memory and shown on a small HTML viewer at `/` (this stack's
equivalent of "log into the platform's web UI and look at the post") — nothing is persisted to
disk, and there's nowhere for it to federate or email even if it wanted to. Standard library only,
no dependencies, so the Dockerfile just needs a bare Python image.
"""

import html
import itertools
import json
import re
import threading
import time
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from urllib.parse import urlparse, parse_qs

LOCK = threading.Lock()
# path -> list of message dicts (most recent last)
MESSAGES: dict[str, list[dict]] = {}
ID_COUNTER = itertools.count(1)


def next_id() -> str:
    return f"{int(time.time())}{next(ID_COUNTER):06d}"


def parse_multipart(raw: bytes, content_type: str) -> tuple[str, list[dict]]:
    """
    Bare-bones multipart/form-data parser for exactly the shape
    DiscordWebhookConnector.DeliverAsync sends: a `payload_json` text field plus zero or more
    `files[n]` file parts. Deliberately not using the stdlib `cgi` module — it was removed in
    Python 3.13, and this format is simple enough not to need a library.
    Returns (content, [{"filename":..., "size":...}, ...]).
    """
    m = re.search(r'boundary="?([^";]+)"?', content_type)
    if not m:
        return "", []
    boundary = ("--" + m.group(1)).encode()

    content = ""
    attachments = []
    for part in raw.split(boundary)[1:-1]:
        part = part.strip(b"\r\n")
        if not part:
            continue
        header_blob, _, body = part.partition(b"\r\n\r\n")
        headers = header_blob.decode("latin-1")
        disposition = next((h for h in headers.split("\r\n") if h.lower().startswith("content-disposition")), "")
        name_m = re.search(r'name="([^"]*)"', disposition)
        filename_m = re.search(r'filename="([^"]*)"', disposition)
        name = name_m.group(1) if name_m else ""
        if filename_m:
            attachments.append({"filename": filename_m.group(1), "size": len(body)})
        elif name == "payload_json":
            try:
                content = json.loads(body.decode("utf-8")).get("content", "")
            except json.JSONDecodeError:
                pass
    return content, attachments


class Handler(BaseHTTPRequestHandler):
    server_version = "PostyFoxDiscordMock/1.0"

    def log_message(self, fmt, *args):  # quieter default logging
        print(f"[{self.log_date_time_string()}] {fmt % args}")

    def _send_json(self, status: int, payload: dict | None):
        body = json.dumps(payload).encode() if payload is not None else b""
        self.send_response(status)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        if body:
            self.wfile.write(body)

    def do_GET(self):
        parsed = urlparse(self.path)
        if parsed.path == "/":
            self._send_viewer()
            return
        if parsed.path == "/_health":
            self._send_json(200, {"ok": True})
            return
        self._send_json(404, {"message": "Unknown route", "code": 0})

    def do_POST(self):
        parsed = urlparse(self.path)
        webhook_path = parsed.path.rstrip("/")
        if not webhook_path:
            self._send_json(400, {"message": "POST to a webhook path, e.g. /api/webhooks/1/test"})
            return

        content_type = self.headers.get("Content-Type", "")
        length = int(self.headers.get("Content-Length") or 0)
        raw = self.rfile.read(length) if length else b""

        if content_type.startswith("multipart/form-data"):
            content, attachments = parse_multipart(raw, content_type)
        else:
            try:
                payload = json.loads(raw or b"{}")
            except json.JSONDecodeError:
                payload = {}
            content = payload.get("content", "")
            attachments = []

        msg_id = next_id()
        message = {
            "id": msg_id,
            "type": 0,
            "content": content,
            "channel_id": webhook_path,
            "attachments": attachments,
            "timestamp": time.strftime("%Y-%m-%dT%H:%M:%S+00:00", time.gmtime()),
        }
        with LOCK:
            MESSAGES.setdefault(webhook_path, []).append(message)

        query = parse_qs(parsed.query)
        wait = query.get("wait", ["false"])[0].lower() == "true"
        self._send_json(200 if wait else 204, message if wait else None)

    def do_DELETE(self):
        parsed = urlparse(self.path)
        m = re.match(r"^(.*)/messages/([^/]+)$", parsed.path.rstrip("/"))
        if not m:
            self._send_json(400, {"message": "DELETE <webhook path>/messages/<id>"})
            return
        webhook_path, msg_id = m.group(1), m.group(2)
        with LOCK:
            msgs = MESSAGES.get(webhook_path, [])
            before = len(msgs)
            MESSAGES[webhook_path] = [m for m in msgs if m["id"] != msg_id]
            deleted = len(MESSAGES[webhook_path]) < before
        self.send_response(204 if deleted else 404)
        self.end_headers()

    def _send_viewer(self):
        with LOCK:
            snapshot = {k: list(v) for k, v in MESSAGES.items()}
        blocks = []
        if not snapshot:
            blocks.append("<p>No messages received yet. Send a test post from PostyFox.</p>")
        for path, msgs in sorted(snapshot.items()):
            blocks.append(f"<h2><code>{html.escape(path)}</code></h2>")
            for m in reversed(msgs):
                atts = "".join(
                    f"<li>{html.escape(a['filename'] or 'file')} ({a['size']} bytes)</li>"
                    for a in m["attachments"]
                )
                blocks.append(
                    "<div class='msg'>"
                    f"<div class='meta'>id={html.escape(m['id'])} · {html.escape(m['timestamp'])}</div>"
                    f"<div class='content'>{html.escape(m['content'])}</div>"
                    + (f"<ul>{atts}</ul>" if atts else "")
                    + "</div>"
                )
        page = f"""<!doctype html>
<html><head><meta charset="utf-8"><title>PostyFox Discord webhook mock</title>
<meta http-equiv="refresh" content="5">
<style>
body {{ font-family: system-ui, sans-serif; margin: 2rem; background: #2b2d31; color: #dbdee1; }}
h1 {{ color: #fff; }}
code {{ background: #1e1f22; padding: 0.1rem 0.4rem; border-radius: 4px; }}
.msg {{ background: #313338; border-radius: 8px; padding: 0.75rem 1rem; margin: 0.5rem 0; }}
.meta {{ color: #949ba4; font-size: 0.8rem; margin-bottom: 0.25rem; }}
.content {{ white-space: pre-wrap; }}
</style></head>
<body>
<h1>PostyFox Discord webhook mock</h1>
<p>Stands in for a real Discord channel — see testing-platform/discord-webhook/README.md.
This page auto-refreshes every 5s.</p>
{''.join(blocks)}
</body></html>"""
        body = page.encode()
        self.send_response(200)
        self.send_header("Content-Type", "text/html; charset=utf-8")
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)


if __name__ == "__main__":
    port = 8090
    server = ThreadingHTTPServer(("0.0.0.0", port), Handler)
    print(f"Discord webhook mock listening on :{port}")
    server.serve_forever()
