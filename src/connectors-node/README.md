# connectors-node

The **connectors-node** service is part of the PostyFox platform. It exposes
social-platform "connectors" behind a small, stateless HTTP contract that the
C# posting worker calls to delegate delivery for platforms whose best libraries
are Node-only:

- **Bluesky** via [`@atproto/api`](https://www.npmjs.com/package/@atproto/api)
- **Tumblr** via [`tumblr.js`](https://www.npmjs.com/package/tumblr.js)
- **FurAffinity** via its authenticated HTML forms
- **Instagram** via the Instagram Content Publishing API (Business Login for Instagram)

The service is intentionally small and holds no state: every request carries
the credentials/config it needs.

## Stack

- TypeScript (NodeNext / ES2022, strict)
- [Fastify](https://fastify.dev/) HTTP server
- Node's built-in test runner (`node --test`) driven through `tsx`

## Environment variables

| Variable         | Default | Description                                                                 |
| ---------------- | ------- | --------------------------------------------------------------------------- |
| `PORT`           | `8090`  | TCP port the server binds to (`0.0.0.0`).                                    |
| `INTERNAL_TOKEN` | _unset_ | Shared secret. When set, all routes except `/health` require a match. When unset, auth is disabled (dev mode). |
| `OBJECT_STORE_SERVICE_URL` | _unset_ | S3-compatible endpoint used to fetch media bytes, e.g. `http://minio:9000`. Leave unset for real AWS S3. |
| `OBJECT_STORE_ACCESS_KEY`  | _unset_ | Object-store access key id. |
| `OBJECT_STORE_SECRET_KEY`  | _unset_ | Object-store secret access key. |
| `OBJECT_STORE_BUCKET`      | `postyfox` | Single bucket that holds every media container. |
| `OBJECT_STORE_FORCE_PATH_STYLE` | `true` | Path-style addressing (required by MinIO and most self-hosted stores). Set `false` for virtual-host style. |
| `OBJECT_STORE_REGION`      | `us-east-1` | S3 region. |

### Media object store

`deliver` no longer receives inline base64 bytes. Each `post.media` item is a
**reference** into an S3-compatible object store. The service fetches the bytes
itself from the single bucket named by `OBJECT_STORE_BUCKET`, using the object
key `` `${container}/${key}` `` (e.g. container `media`, key `u1/abc/pic.png` →
S3 key `media/u1/abc/pic.png`). A fetch failure is caught and returned as
`{ "success": false, "error": "..." }`, it never crashes the request.

## Authentication

Every request except `GET /health` must send header
`X-Internal-Token: <INTERNAL_TOKEN>`. A missing or wrong token returns `401`.
If `INTERNAL_TOKEN` is not configured, all requests are allowed (dev only).

## HTTP contract

`:platform` includes `BlueSky`, `Tumblr`, `FurAffinity`, `Instagram`, and the supported Fediverse
platforms (case-insensitive). Unknown platforms
return `404 { "error": "unknown platform" }`.

The **context object** used by every endpoint:

```json
{
  "connectorId": "string",
  "userId": "string",
  "configJson": "string (JSON)",
  "secretJson": "string (JSON) | null",
  "targetId": "string | null"
}
```

`configJson` and `secretJson` are JSON **strings** that the service parses.

### `GET /health`

```json
200 { "status": "ok" }
```

### `POST /connectors/:platform/is-authenticated`

Body: the context object.

```json
200 { "isAuthenticated": true, "detail": "optional string" }
```

### `POST /connectors/:platform/list-targets`

Body: the context object.

```json
200 { "targets": [ { "id": "string", "name": "string" } ] }
```

### `POST /connectors/:platform/deliver`

Body:

```json
{
  "context": { ...context object... },
  "post": {
    "title": "string | null",
    "body": "string",
    "tags": ["string"],
    "media": [ { "container": "string", "key": "string", "contentType": "string", "alt": "string | null" } ]
  }
}
```

```json
200 { "success": true, "externalId": "string", "externalUrl": "string", "error": "string" }
```

`deliver` never throws on platform errors: it returns
`{ "success": false, "error": "..." }`.

> **Media:** each `post.media` item is an object-store reference. The service
> fetches the bytes from `OBJECT_STORE_BUCKET` at key `` `${container}/${key}` ``,
> **normalizes them to the platform's limits** (see below), uploads them, and
> applies `alt` as the image's alt text. Bluesky attaches up to 4 images as an
> `app.bsky.embed.images` embed; Tumblr creates an NPF photo post. Text-only posts
> are unaffected — except Instagram, which has no text-only post type and rejects
> a `deliver` call with empty `media` outright (see below).

### `POST /connectors/:platform/refresh-token`

Body: the context object. Only implemented by connectors whose token expires (currently just
Instagram); returns `400 { "error": "refresh not supported" }` for any other platform.

```json
200 { "secretJson": "string (JSON) | null" }
```

`secretJson: null` means the platform declined the refresh (the caller keeps the existing secret
and the user must reconnect); a thrown error still returns `502` like every other route. Driven by
a background sweeper on the core side (`ConnectorTokenRefreshSweeper`), never a user action.

### Media normalization (mandatory core step)

Every connector **must** route fetched bytes through `normalizeMedia()` from
`src/media/` before uploading: this is a core building block, not per-connector
glue. It resizes/re-encodes still images (`sharp`) and transcodes video and
animated GIFs (`fluent-ffmpeg`) to the platform's `MediaSpec` (`src/media/specs.ts`):
max dimensions / bytes / duration, accepted formats and attachment count. Sources
in an unaccepted format are converted; media that can't be brought within the limits
fails that delivery cleanly. Fediverse connectors merge the instance's live limits
(`mergeLiveLimits`) over the static spec. Adding a new connector means calling
`normalizeMedia(bytes, contentType, spec)` right after `mediaStore.fetch`, never
upload raw bytes. The runtime image installs the `ffmpeg` binary for the video path.

## Connector credential shapes

### Bluesky

- `configJson` → `{ "Handle": "alice.bsky.social" }`
- `secretJson` → `{ "AppPassword": "xxxx-xxxx-xxxx-xxxx" }`
- `list-targets` returns a single target `{ id: handle, name: "Bluesky: <handle>" }`.
- `deliver` posts `post.body` (with best-effort RichText link/mention facet
  detection) and returns `externalId` = post `uri`,
  `externalUrl` = `https://bsky.app/profile/<handle>/post/<rkey>`.

### Tumblr

- `configJson` → `{ "Username": "myblog" }` (blog identifier)
- `secretJson` → per-user OAuth1 credentials:
  `{ "OAuthToken": "", "OAuthTokenSecret": "" }`
- `operationalSecretJson` → Vault-sourced app credentials:
  `{ "consumerKey": "", "consumerSecret": "" }`
- `list-targets` returns the user's blogs as `{ id: blog.name, name: blog.title || blog.name }`.
- `deliver` creates a text post and returns the post `id` and `post_url`.

### Instagram

- No `configJson` fields: Business Login for Instagram carries no per-account config, the connect
  flow itself determines which IG business/creator account is linked.
- `secretJson` → `{ "AccessToken": "", "IgUserId": "", "ExpiresAt": "2026-09-01T00:00:00Z" }`. The
  token is long-lived (~60 days); `ExpiresAt` is what the core token-refresh sweeper reads to decide
  when a refresh is due.
- `operationalSecretJson` → Vault-sourced app credentials: `{ "appId": "", "appSecret": "" }`.
- `list-targets` returns the single linked account as `{ id: IgUserId, name: "@username" }`
  (Business Login tokens are scoped to one IG user).
- `deliver` **requires at least one media item** — Instagram has no text-only post type — and
  publishes via the create-container-then-publish flow: a single image/video container, or one
  child container per item plus a parent `CAROUSEL` container for several. Video containers are
  polled until Instagram finishes processing them before publish is attempted. Unlike every other
  connector here, Instagram fetches media **by URL** rather than accepting a direct upload, so
  normalized bytes are staged in the object store under a short-lived presigned URL
  (`MediaStore.put` + `presignedGetUrl`) and deleted again once the container is created — this only
  works when the object store is reachable from the public internet (not true for a local MinIO dev
  stack).
- `refresh` exchanges the still-valid token for a new one, resetting the 60-day expiry.

### FurAffinity

- `secretJson` → `{ "CookieHeader": "a=…; b=…" }`. The cookie header must come from a currently
  logged-in `furaffinity.net` browser session and is stored by core's configured secret provider.
- Optional `configJson` fields: `Category`, `Theme`, `Species`, `Gender`, and comma-separated
  `FolderIds`. Missing values use FurAffinity's general-purpose defaults.
- Gallery delivery currently accepts exactly one JPEG, PNG, or GIF, requires an explicit post
  `rating` plus at least three tags, and follows FurAffinity's upload/finalize forms.
- Run a single connectors-node replica while FurAffinity is enabled; its required per-account
  posting interval is coordinated in-process.
- Automated login and Cloudflare challenge solving are intentionally not attempted. When a
  challenge or expired login is detected, refresh the imported browser cookies.

## Running

```bash
npm install
npm run build      # tsc -> dist/
npm start          # runs dist/index.js
npm run dev        # tsx watch (hot reload)
npm test           # node --test via tsx
```

## Docker

```bash
docker build -t connectors-node .
docker run -p 8090:8090 -e INTERNAL_TOKEN=changeme connectors-node
```

Multi-stage build: `node:24` compiles TypeScript, `node:24-slim` runs the
compiled `dist/`. Exposes port `8090`.
