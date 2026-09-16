# Akkoma test stack

Brings up a local Akkoma instance for manually testing the PostyFox `akkoma` connector. PostyFox
talks to Akkoma with the `pleroma` SNS driver (see
`src/connectors-node/src/connectors/index.ts` — Akkoma is a Pleroma fork and keeps the same client
API). See [../README.md](../README.md) for the shared conventions (what's disabled and why).

**This is the least battle-tested stack in this folder.** Akkoma publishes no prebuilt Docker image
(unlike Mastodon or Pleroma), so `Dockerfile` here builds it from source and generates its instance
config non-interactively with Akkoma's own `mix pleroma.instance gen` at build time — a legitimate,
documented path, but one that couldn't be dry-run against a live daemon while writing this. If
`docker compose up -d` doesn't converge cleanly, check `docker compose logs` on the failing service
first; the [Akkoma docs](https://docs.akkoma.dev/stable/installation/docker_en/) are the fallback
reference for anything that's drifted since.

## Setup

```
docker compose up -d
docker compose logs -f admin-init   # wait for it to print success and exit
```

**First run builds Akkoma from source** (pinned to the `stable` branch, includes compiling the
Elixir/Erlang app) — expect this to take several minutes before Postgres extensions/migrations even
start. Subsequent `up`s reuse the built `akkoma-test:local` image and are fast.

`docker compose ps` should settle with `db`/`akkoma` `running` and `db-init`/`migrate`/`admin-init`
`exited (0)`.

Instance URL: **http://localhost:4020**

Test admin account (created by `admin-init`):

| | |
|---|---|
| Nickname | `postyfox` |
| Email | `admin@postyfox.test` |
| Password | `postyfox-test-1234` |

These are fixed in `Dockerfile` (instance generation) and `docker-compose.yml` (`admin-init`'s
command) rather than an env file, since they're baked into the image at build time — edit both and
rebuild (`docker compose build`) if you want different values.

## What's disabled and why

- **Federation**: `Dockerfile` appends `federating: false` to the generated instance config, plus
  the whole stack sits on a Docker `internal: true` network with no route to the real internet, so
  there's nothing for it to federate with even if that were off.
- **Registration / e-mail**: the same appended block sets `registrations_open: false` and
  `account_activation_required: false`. The test account is created directly by `admin-init`, so
  self-registration and its confirmation e-mail are never exercised.

## Manual test checklist

1. In PostyFox, add a new **Akkoma** connector with Instance URL `http://localhost:4020` — or, if
   PostyFox itself is running in the dockerized dev stack, `http://host.containers.internal:4020`
   (Podman) / `http://host.docker.internal:4020` (Docker), see
   [../README.md](../README.md#connecting-from-a-dockerized-postyfox-dev-stack).
2. Click **Connect**, log in with the test account above, and approve the app when prompted.
3. Compose a short test post in PostyFox and send it to the Akkoma target.
4. Open http://localhost:4020/api/v1/accounts/verify_credentials (or a frontend if you've installed
   one) and confirm the post landed with the right text.
5. If you're testing repost/delete support, trigger it from PostyFox and confirm the boost/removal
   shows up (or disappears).

## Resetting

```
docker compose down -v   # wipes the instance back to empty (keeps the built image)
docker compose up -d     # db-init/migrate/admin-init redo their setup
```
