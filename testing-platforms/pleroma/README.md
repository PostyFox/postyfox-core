# Pleroma test stack

Brings up a local Pleroma instance for manually testing the PostyFox `pleroma` connector. Builds
Pleroma from source (Pleroma's own Dockerfile, pinned to the `stable` branch) — the documented setup
([pleroma-docker-compose](https://git.pleroma.social/pleroma/pleroma-docker-compose)) points at a
prebuilt image, `git.pleroma.social:5050/pleroma/pleroma:latest`, but that registry no longer exists
(TCP connection refused on port 5050, while the GitLab web UI on the same host is up — looks
discontinued, not transiently down). See [../README.md](../README.md) for the shared conventions
(what's disabled and why).

## Setup

```
./fetch-source.sh
docker compose up -d
docker compose logs -f admin-init   # wait for it to print "User postyfox created" (or exit quietly
                                     # if it already existed) and then exit
```

`./fetch-source.sh` clones the `stable` branch into `./upstream/` (gitignored) and applies a few
patches — see that script for details (hardcoded HTTPS URL scheme, short image names needing
qualification for this podman setup, and a hex.pm TLS bug in the pinned Erlang version). Re-run it
any time you want to pick up changes on that branch.

**First run builds Pleroma from source** — expect this step to take a few minutes. Subsequent `up`s
reuse the built `pleroma-test:local` image and are fast.

`docker compose ps` should settle with `pleroma-db`/`pleroma` `running` and `admin-init`
`exited (0)`.

Instance URL: **http://localhost:4010**

Test admin account (created by `admin-init`):

| | |
|---|---|
| Nickname | `postyfox` |
| Email | `admin@postyfox.test` |
| Password | `postyfox-test-1234` |

Change any of this in `environments/pleroma/pleroma.env` before first `up` if you want different
values.

### If `admin-init` doesn't converge

`admin-init` sets `PLEROMA_CTL_RPC_DISABLED=true` so `pleroma_ctl user new` runs standalone instead
of RPC-ing into the separate `pleroma` container's node (which can't work — no shared Erlang
distribution between containers; confirmed this fails with `RPC failed with reason :noconnection`
otherwise). If it still exits non-zero or the account isn't there, create it by hand once the stack
is up:

```
docker compose exec pleroma /opt/pleroma/bin/pleroma_ctl user new postyfox admin@postyfox.test \
  --password postyfox-test-1234 --admin -y
```

## What's disabled and why

- **Federation**: `federating: false` in `volumes/pleroma/config.exs`, plus the whole stack sits on
  a Docker `internal: true` network with no route to the real internet, so there's nothing for it to
  federate with even if that were off.
- **Registration / e-mail**: `registrations_open: false` and `account_activation_required: false` in
  the same file. The test account is created directly by `admin-init`, already confirmed, so
  self-registration and its confirmation e-mail are never exercised.

## Manual test checklist

1. In PostyFox, add a new **Pleroma** connector with Instance URL `http://localhost:4010` — or, if
   PostyFox itself is running in the dockerized dev stack, `http://host.containers.internal:4010`
   (Podman) / `http://host.docker.internal:4010` (Docker), see
   [../README.md](../README.md#connecting-from-a-dockerized-postyfox-dev-stack).
2. Click **Connect**, log in with the test account above, and approve the app when prompted.
3. Compose a short test post in PostyFox and send it to the Pleroma target.
4. Open http://localhost:4010/postyfox in a browser (or the Pleroma-FE/admin-FE if you've installed
   a frontend — the bare API works fine for this check without one) and confirm the post landed
   with the right text.
5. If you're testing repost/delete support, trigger it from PostyFox and confirm the boost/removal
   shows up (or disappears).

## Resetting

```
docker compose down -v   # wipes the instance back to empty
docker compose up -d     # admin-init recreates the test account
```
