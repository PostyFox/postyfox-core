# Discord Webhooks test stack

There's no self-hostable Discord server, so this isn't a copy of the real platform like the other
folders — it's a small mock (`app/server.py`, stdlib-only Python, no dependencies) that implements
exactly the HTTP contract PostyFox's `DiscordWH` connector uses against a real Discord webhook (see
`src/PostyFox.Infrastructure/Connectors/DiscordWebhookConnector.cs`):

- `POST <path>?wait=true` — JSON `{"content": "..."}`, or multipart with a `payload_json` field
  plus `files[n]` attachment parts — exactly what the connector sends.
- `DELETE <path>/messages/<id>` — what the connector's delete/repost-cleanup path calls.

Any path is accepted as its own independent "channel" (the mock doesn't validate a real
webhook id/token, since there isn't one) — see [../README.md](../README.md) for the shared
conventions.

## Setup

```
docker compose up -d
```

Nothing to provision — it's ready as soon as the container is healthy.

## Test URL

Point PostyFox's Discord connector at any path under **http://localhost:8090**, e.g.
`http://localhost:8090/api/webhooks/1/postyfox-test`. Pick your own path if you're running more
than one test session and want to keep them visually separate on the viewer page below.

## Manual test checklist

1. In PostyFox, add a new **Discord Web Hook** connector with Webhook URL
   `http://localhost:8090/api/webhooks/1/postyfox-test` — or, if PostyFox itself is running in the
   dockerized dev stack, `http://host.containers.internal:8090/api/webhooks/1/postyfox-test`
   (Podman) / `http://host.docker.internal:8090/api/webhooks/1/postyfox-test` (Docker), see
   [../README.md](../README.md#connecting-from-a-dockerized-postyfox-dev-stack). (This connector
   has no browser-side OAuth redirect, so the `/etc/hosts` entry that section mentions isn't
   strictly required for this one stack, but it's harmless to have and keeps things consistent.)
2. Compose a short test post in PostyFox (try one with an image attached too, to exercise the
   multipart path) and send it to the Discord target.
3. Open http://localhost:8090/ in a browser — it lists every message received, grouped by path,
   auto-refreshing every 5s. Confirm the content (and attachment, if any) landed correctly.
4. If you're testing delete support, trigger it from PostyFox and confirm the message disappears
   from the viewer on the next refresh.

## What's disabled and why

Nothing to disable — there's no federation or e-mail in a webhook receiver. The stack still sits on
a Docker `internal: true` network for consistency with the rest of this folder, though the mock
never talks to anything else regardless.

## Resetting

Messages are kept in memory only (see `app/server.py`), so:

```
docker compose restart   # clears everything
```
