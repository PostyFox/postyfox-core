# GoToSocial test stack

Brings up a local GoToSocial instance (`docker.io/superseriousbusiness/gotosocial:0.22.1`, sqlite
backend, no separate database container needed) for manually testing the PostyFox `gotosocial`
connector. See [../README.md](../README.md) for the shared conventions (what's disabled and why).

## Setup

```
docker compose up -d
docker compose logs -f admin-init   # wait for it to exit 0
```

`docker compose ps` should settle with `gotosocial` `running` and `admin-init` `exited (0)`.

Instance URL: **http://localhost:4080**

Test account (created by `admin-init`, already confirmed — not an admin account, since PostyFox
only needs OAuth login + posting):

| | |
|---|---|
| Username | `postyfox` |
| Email | `admin@postyfox.test` |
| Password | `postyfox-test-1234` |

Change these in `docker-compose.yml`'s `admin-init` command before first `up` if you want different
values.

## What's disabled and why

- **Federation**: `GTS_INSTANCE_FEDERATION_MODE=allowlist` in `.env` — closed federation
  (allow-list only, nothing allow-listed), plus the whole stack sits on a Docker `internal: true`
  network with no route to the real internet, so there's nothing for it to federate with even if
  that were off.
- **Registration / e-mail**: `GTS_ACCOUNTS_REGISTRATION_OPEN=false`, and no `GTS_SMTP_*` variables
  are set at all, so there's no mail transport configured. The test account is created directly by
  `admin-init`, already confirmed, so self-registration is never exercised.

## Manual test checklist

1. In PostyFox, add a new **GoToSocial** connector with Instance URL `http://localhost:4080` — or,
   if PostyFox itself is running in the dockerized dev stack, `http://host.containers.internal:4080`
   (Podman) / `http://host.docker.internal:4080` (Docker), see
   [../README.md](../README.md#connecting-from-a-dockerized-postyfox-dev-stack).
2. Click **Connect**, log in with the test account above, and approve the app when prompted.
3. Compose a short test post in PostyFox and send it to the GoToSocial target.
4. Open http://localhost:4080/@postyfox in a browser and confirm the post landed with the right
   text.
5. If you're testing repost/delete support, trigger it from PostyFox and confirm the boost/removal
   shows up (or disappears) on the same profile page.

## Resetting

```
docker compose down -v   # wipes the instance back to empty
docker compose up -d     # admin-init recreates the test account
```
