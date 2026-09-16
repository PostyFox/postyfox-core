# Platform testing stacks

Local, disposable Docker Compose stacks for manually verifying PostyFox connectors against a real
copy of each platform we support.

| Folder | Platform | Issue |
|---|---|---|
| [`mastodon/`](mastodon/) | Mastodon | [#350](https://github.com/PostyFox/postyfox-core/issues/350) |
| [`hometown/`](hometown/) | Hometown | [#351](https://github.com/PostyFox/postyfox-core/issues/351) |
| [`pleroma/`](pleroma/) | Pleroma | [#349](https://github.com/PostyFox/postyfox-core/issues/349) |
| [`akkoma/`](akkoma/) | Akkoma | [#356](https://github.com/PostyFox/postyfox-core/issues/356) |
| [`gotosocial/`](gotosocial/) | GoToSocial | [#352](https://github.com/PostyFox/postyfox-core/issues/352) |
| [`friendica/`](friendica/) | Friendica | [#353](https://github.com/PostyFox/postyfox-core/issues/353) |
| [`discord-webhook/`](discord-webhook/) | Discord Webhooks | [#355](https://github.com/PostyFox/postyfox-core/issues/355) |

## What's automated vs. manual

Each stack is fully self-contained: `docker compose up -d` in the platform's folder brings up the
platform, its database, and a one-shot init container that creates a confirmed test account (admin,
where the platform has that concept) with a fixed password, so there's no web install wizard or
email loop to get through by hand.

The **test itself is not automated**. Each platform folder has a README with a short numbered
checklist: point a PostyFox connector at the instance, connect, compose a post, send it, and confirm
it landed on the platform. This is deliberate — the point of these stacks is a human sanity-check
against a real target before/after connector changes, not a CI suite.

## Connecting from a dockerized PostyFox dev stack

If you're pointing a PostyFox connector at one of these stacks (rather than just eyeballing the
platform's own UI), and PostyFox itself is running via `postyfox-core/deploy`'s Compose stack, use
a **host-gateway hostname**, not `http://localhost:PORT`, as the connector's Instance/Webhook URL.

Why: the connector's outbound calls (OAuth app registration, posting, webhook delivery) come from
`connectors-node`, which runs in its own container. `localhost` there means that container, not
your machine, so a `localhost:PORT` URL fails with `ECONNREFUSED`/502 even though the platform
works fine in your browser. Which hostname to use depends on which tool you're running the stack
with:

- **Podman** (`podman compose …`): use `http://host.containers.internal:PORT`. Podman injects this
  automatically into every container, pointing at the host — no compose changes needed.
- **Docker** (`docker compose …`): use `http://host.docker.internal:PORT`. `connectors-node`'s
  service definition in `deploy/docker-compose.yml` carries `extra_hosts:
  host.docker.internal:host-gateway` for this (Docker Desktop resolves it out of the box anyway;
  that line is what makes it work on Docker Engine on Linux too). Podman also understands
  `host-gateway`, so `host.docker.internal` happens to work there as well if you'd rather use one
  hostname everywhere — but `host.containers.internal` is the one that needs zero setup on Podman.

For platforms with an OAuth connect flow, the provider's redirect also sends your **browser**
straight to that same Instance URL (to show its authorize page) — and your browser, running on the
host, has no idea what `host.containers.internal`/`host.docker.internal` mean either, by default.
Fix that once, for every stack, by adding the line(s) for whichever hostname(s) you use to your
machine's `/etc/hosts` (not something to script/automate — it's a one-time manual edit):

```
127.0.0.1 host.containers.internal
127.0.0.1 host.docker.internal
```

After that, the hostname resolves correctly from both your browser and every container, and you
can use it as the connector's Instance/Webhook URL for any stack in this folder. Plain
`http://localhost:PORT` still works fine for just browsing a platform's own UI directly.

## Conventions shared by every stack

- **Isolated per platform.** Each folder is its own Compose project (own network, own volumes) so
  stacks don't collide and can run side by side. Host ports are chosen to not overlap across folders
  — see each README for its port.
- **Federation and outbound e-mail are disabled** at the application config level wherever the
  platform exposes a switch for it (closed/allowlist federation mode, `federating: false`,
  registration-confirmation e-mails skipped by creating the test account directly). These are local,
  throwaway instances never intended to be reachable from the internet; the config change is there so
  a background job doesn't sit there retrying delivery to the real fediverse, and so nothing forwards
  mail to a real inbox. Where a platform genuinely has no such switch (Friendica), the README says so.
- **Secrets are baked in, not generated per-checkout.** Things like Rails `SECRET_KEY_BASE` or a
  Postgres password are committed with fixed, clearly-fake values in each stack's `.env`. These
  instances are local-only test doubles with no real user data — treat the values as public, and feel
  free to regenerate them (each README says how) if that bothers you.
- **State lives in Docker volumes**, not bind mounts into the repo, so `git status` stays clean while
  a stack is running. `docker compose down -v` wipes a stack back to nothing.

## Adding a platform

Copy the shape of an existing folder that's architecturally closest to the new platform (the
Mastodon-family ones — Mastodon, Hometown — share almost everything; Pleroma and Akkoma share the
`federating`/`pleroma_ctl`-style config). Keep the same three things every folder has: a
`docker-compose.yml` that needs zero manual steps beyond `up -d`, a README with the fixed test
credentials and the manual test checklist, and federation/e-mail turned off wherever the platform
allows it.
