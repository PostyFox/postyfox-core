# Friendica test stack

Brings up a local Friendica instance (`friendica:apache`, official image) for manually testing the
PostyFox `friendica` connector. See [../README.md](../README.md) for the shared conventions.

## Setup

```
docker compose up -d
docker compose logs -f admin-init   # wait for it to print "Password changed." and exit
```

The Friendica image runs its whole install wizard automatically from the environment variables in
`docker-compose.yml` (`FRIENDICA_URL`, `FRIENDICA_ADMIN_MAIL`, `MYSQL_*`) — first boot takes a
minute or so while it waits for MariaDB and runs the installer. `docker compose ps` should settle
with `db`/`friendica` `running` and `admin-init` `exited (0)`.

Instance URL: **http://localhost:8084** (`WEB_PORT` in `.env` — change it if that's taken too, it
feeds both the port mapping and `FRIENDICA_URL` automatically).

Test account (created by `admin-init`; registered under `FRIENDICA_ADMIN_MAIL`, so Friendica grants
it admin automatically):

| | |
|---|---|
| Nickname | `postyfox` |
| Email | `admin@postyfox.test` |
| Password | `postyfox-test-1234` |

Change these in `docker-compose.yml` (`FRIENDICA_ADMIN_MAIL` and `admin-init`'s command) before
first `up` if you want different values.

## What's disabled and why — and what isn't

Friendica is built around federating; unlike the other platforms here it has **no single switch to
turn federation off**. `admin-init` runs `bin/console config set system block_public true`, which
stops unauthenticated visitors (local or remote) from reading profiles/posts/the directory and
requesting new connections — the closest thing Friendica has to the "standalone" mode its own docs
describe. It does not stop this instance from *initiating* outbound federation traffic if you were
to actually add a remote contact.

The real mitigation here is that the whole stack sits on a Docker `internal: true` network with no
route to the real internet, so there is nothing out there for it to reach regardless. Don't remove
that unless you also accept that federation is, at that point, genuinely on.

**E-mail**: no `SMTP_*` variables are set on the `friendica` service (see `docker-compose.yml`), so
there's no mail transport configured at all — any attempt just fails quietly, and nothing could
leave the host anyway given the point above. The test account is created directly by `admin-init`,
so the registration flow (and its confirmation e-mail) is never exercised.

## Manual test checklist

1. In PostyFox, add a new **Friendica** connector with Instance URL `http://localhost:8084` — or,
   if PostyFox itself is running in the dockerized dev stack, `http://host.containers.internal:8084`
   (Podman) / `http://host.docker.internal:8084` (Docker), see
   [../README.md](../README.md#connecting-from-a-dockerized-postyfox-dev-stack).
2. Click **Connect**, log in with the test account above, and approve the app when prompted.
3. Compose a short test post in PostyFox and send it to the Friendica target.
4. Log in at http://localhost:8084 with the test account and confirm the post landed on the
   profile/timeline with the right text.
5. If you're testing repost/delete support, trigger it from PostyFox and confirm the result shows
   up (or disappears) on the same profile.

## Resetting

```
docker compose down -v   # wipes the instance back to empty
docker compose up -d     # runs the installer and admin-init again
```
