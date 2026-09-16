# Hometown test stack

Brings up a local Hometown instance for manually testing the PostyFox `hometown` connector.
Hometown (https://github.com/hometown-fork/hometown) is a light fork of Mastodon — PostyFox talks
to it with the same `mastodon` SNS driver (see `src/connectors-node/src/connectors/index.ts`) — so
this stack is the Mastodon one with a source build instead of a prebuilt image, since Hometown
doesn't publish one. See [../README.md](../README.md) for the shared conventions (what's disabled
and why).

## Setup

```
./fetch-source.sh
docker compose up -d
docker compose logs -f admin-init   # wait for it to print "OK" and exit
```

`./fetch-source.sh` clones the `hometown-dev` branch into `./upstream/` (gitignored) and patches its
Dockerfile to work around a podman/buildah parser bug (see the comment that script writes into the
patched file) — `migrate`'s build context points at that directory rather than upstream's repo
directly. Re-run it any time you want to pick up changes on that branch.

**First run also builds Hometown from source** (pinned to the commit `fetch-source.sh` cloned) —
expect this step alone to take several minutes (Ruby gems + JS asset compilation, plus compiling
ffmpeg/libvips) before Postgres/migrations even start. Subsequent `up`s reuse the built
`hometown-test:local` image and are fast.

`docker compose ps` should settle with `db`, `redis`, `web`, `streaming`, `sidekiq` all `running`
and `migrate`/`admin-init` `exited (0)`.

Instance URL: **http://localhost:3001**

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

1. In PostyFox, add a new **Hometown** connector with Instance URL `http://localhost:3001` — or, if
   PostyFox itself is running in the dockerized dev stack, `http://host.containers.internal:3001`
   (Podman) / `http://host.docker.internal:3001` (Docker), see
   [../README.md](../README.md#connecting-from-a-dockerized-postyfox-dev-stack).
2. Click **Connect**, log in with the test account above, and approve the app when prompted.
3. Compose a short test post in PostyFox and send it to the Hometown target. If you're testing
   Hometown's local-only-posting feature specifically, use whatever PostyFox exposes for visibility/
   scope on this connector to mark it local-only.
4. Open http://localhost:3001/@postyfox in a browser and confirm the post landed with the right
   text and visibility.
5. If you're testing repost/delete support, trigger it from PostyFox and confirm the boost/removal
   shows up (or disappears) on the same profile page.

## Resetting

```
docker compose down -v   # wipes the instance back to empty (keeps the built image)
docker compose up -d     # admin-init recreates the test account
```
