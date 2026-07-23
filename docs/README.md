# PostyFox Platform — Documentation

Documentation for the containerised PostyFox platform (repo root: `src/`, `tests/`, `deploy/`).

| Doc | What it covers |
|-----|----------------|
| [ARCHITECTURE.md](./ARCHITECTURE.md) | As-built architecture: containers, code layers, data model, auth, connector + trigger frameworks, pipeline, messaging, deployment. Includes diagrams. |
| [DECISIONS.md](./DECISIONS.md) | Architecture Decision Records — the significant choices and their trade-offs. |
| [FOLLOWUPS.md](./FOLLOWUPS.md) | Residual follow-up work. |
| [../deploy/observability/README.md](../deploy/observability/README.md) | Observability wiring — OTLP → central OpenSearch. |
| [../README.md](../README.md) | Operational guide: run locally, build & test, configuration, phase status. |
| [../src/connectors-node/README.md](../src/connectors-node/README.md) | The Node connectors service and its HTTP contract. |

The **interactive API contract** is served by each API at `/swagger` (UI) and `/openapi/v1.json`
(document) — the source of truth for request/response schemas and status codes.

---

## HTTP endpoint reference

### core-api (`api.postyfox.com`)

| Method | Path | Purpose |
|--------|------|---------|
| GET | `/healthz`, `/readyz` | Liveness / readiness |
| GET | `/swagger`, `/openapi/v1.json` | API docs |
| POST/GET/DELETE | `/api/profile/keys[/{id}]` | Create / list / revoke API keys |
| GET | `/api/services[/{id}]` | Platform catalogue |
| GET/PUT/DELETE | `/api/connectors[/{id}]` | Configured connector CRUD |
| GET | `/api/connectors/{id}/authenticated` | Connector auth check |
| GET | `/api/connectors/{id}/targets` | List connector delivery targets |
| POST | `/api/connectors/{id}/telegram/login` | Advance Telegram MTProto login |
| GET/PUT/DELETE | `/api/templates[/{id}]` | Posting template CRUD |
| POST/GET/DELETE | `/api/triggers[/{id}]` | External-trigger registration |

### post-api (`post.postyfox.com`)

| Method | Path | Purpose |
|--------|------|---------|
| GET | `/healthz`, `/readyz`, `/swagger`, `/openapi/v1.json` | Ops / docs |
| POST | `/api/posts` | Create a post (202 + id) |
| GET | `/api/posts` | List the user's posts (newest-first summaries), bounded by the retention window; `?filter=active` for in-flight only, `?limit=` (1..200, default 50) |
| GET | `/api/posts/{id}` | Aggregated post + per-target status |
| POST | `/api/webhooks/{sourceType}` | Inbound signed trigger webhook (anonymous; signature-verified) |

### connectors-node (internal, `X-Internal-Token`)

| Method | Path | Purpose |
|--------|------|---------|
| GET | `/health` | Liveness |
| POST | `/connectors/{platform}/is-authenticated` | Auth check (Bluesky/Tumblr) |
| POST | `/connectors/{platform}/list-targets` | List targets |
| POST | `/connectors/{platform}/deliver` | Deliver a rendered post |

Auth: browser/user calls go through the oauth2-proxy edge (identity header); machine callers use
`X-API-Key`. Webhooks are authenticated by per-source signature, not the auth layer.
