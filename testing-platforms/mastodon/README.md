# Mastodon test stack

Brings up a local Mastodon instance (`ghcr.io/mastodon/mastodon:v4.7.1`) for manually testing the
PostyFox `mastodon` connector. Fully automated — see [../README.md](../README.md) for what that
means and what's disabled (federation, outbound mail) and why.

## Setup

```
docker compose up -d
docker compose logs -f admin-init   # wait for it to print "OK" and exit
```

First boot is slow (Postgres init, `db:migrate`, Sidekiq warming up) — give it a minute or two.
`docker compose ps` should settle with `db`, `redis`, `web`, `streaming`, `sidekiq` all `running`
and `migrate`/`admin-init` `exited (0)`.

Instance URL: **http://localhost:3000**

Test admin account (created by `admin-init`, already e-mail-confirmed):

| | |
|---|---|
| Username | `postyfox` |
| Email | `admin@postyfox.test` |
| Password | `postyfox-test-1234` |

Change the password or any of this in `.env` before first `up` if you want different values —
`admin-init` reads `ADMIN_USERNAME`/`ADMIN_EMAIL`/`ADMIN_PASSWORD` from there.

## What's disabled and why

- **Federation**: `LIMITED_FEDERATION_MODE=true` in `.env` — closed federation (allow-list only,
  nothing allow-listed), plus the whole stack sits on a Docker `internal: true` network with no
  route to the real internet, so there's nothing for it to federate with even if that were off.
- **Outbound mail**: no `SMTP_SERVER` is configured. The test account is created already-confirmed
  by `admin-init` (`tootctl accounts create --confirmed`), so the signup-confirmation email is never
  triggered in the first place; any other transactional mail just fails quietly in Sidekiq.

## Manual test checklist

1. In PostyFox, add a new **Mastodon** connector with Instance URL `http://localhost:3000` — or, if
   PostyFox itself is running in the dockerized dev stack, `http://host.containers.internal:3000`
   (Podman) / `http://host.docker.internal:3000` (Docker), see
   [../README.md](../README.md#connecting-from-a-dockerized-postyfox-dev-stack).
2. Click **Connect**, log in with the test account above, and approve the app when Mastodon
   prompts you.
3. Compose a short test post in PostyFox and send it to the Mastodon target.
4. Open http://localhost:3000/@postyfox in a browser and confirm the post landed with the right
   text, and any attached media/CW/thread behaviour you're testing looks right.
5. If you're testing repost/delete support, trigger it from PostyFox and confirm the boost/removal
   shows up (or disappears) on the same profile page.

## Resetting

```
docker compose down -v   # wipes the instance back to empty
docker compose up -d     # admin-init recreates the test account
```
