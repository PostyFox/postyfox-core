# connectors-node

The **connectors-node** service is part of the PostyFox platform. It exposes
social-platform "connectors" behind a small, stateless HTTP contract that the
C# posting worker calls to delegate delivery for platforms whose best libraries
are Node-only:

- **Bluesky** via [`@atproto/api`](https://www.npmjs.com/package/@atproto/api)
- **Tumblr** via [`tumblr.js`](https://www.npmjs.com/package/tumblr.js)

The service is intentionally small and holds no state — every request carries
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

## Authentication

Every request except `GET /health` must send header
`X-Internal-Token: <INTERNAL_TOKEN>`. A missing or wrong token returns `401`.
If `INTERNAL_TOKEN` is not configured, all requests are allowed (dev only).

## HTTP contract

`:platform` is `BlueSky` or `Tumblr` (case-insensitive). Unknown platforms
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
    "media": [ { "container": "string", "key": "string", "contentType": "string" } ]
  }
}
```

```json
200 { "success": true, "externalId": "string", "externalUrl": "string", "error": "string" }
```

`deliver` never throws on platform errors — it returns
`{ "success": false, "error": "..." }`.

> **Media:** the `post.media` array is accepted but currently ignored
> (text-only delivery). See the `// TODO: media` markers in the connectors.

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
- `secretJson` → OAuth1 credentials:
  `{ "ConsumerKey": "", "ConsumerSecret": "", "OAuthToken": "", "OAuthTokenSecret": "" }`
- `list-targets` returns the user's blogs as `{ id: blog.name, name: blog.title || blog.name }`.
- `deliver` creates a text post and returns the post `id` and `post_url`.

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
