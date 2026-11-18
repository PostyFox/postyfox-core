# PostyFox Platform: Documentation

Documentation for the containerised PostyFox platform (repo root: `src/`, `tests/`, `deploy/`).

| Doc | What it covers |
|-----|----------------|
| [ARCHITECTURE.md](./ARCHITECTURE.md) | As-built architecture: containers, code layers, data model, auth, connector + trigger frameworks, pipeline, messaging, deployment. Includes diagrams. |
| [DECISIONS.md](./DECISIONS.md) | Architecture Decision Records (ADR-001 to ADR-018): the significant choices and their trade-offs. Frontend decisions are in postyfox-frontend's `docs/DECISIONS.md`. |
| [FOLLOWUPS.md](./FOLLOWUPS.md) | Residual follow-up work. |
| [../deploy/observability/README.md](../deploy/observability/README.md) | Observability wiring: OTLP → central OpenSearch. |
| [oauth-connectors.md](./oauth-connectors.md) | OAuth connect flow and operator setup for Tumblr, Instagram, SoFurry, DeviantArt and the Fediverse. |
| [DEVELOPMENT.md](./DEVELOPMENT.md) | Running locally, build & test, configuration, ImageSharp licence, localisation. |
| [../README.md](../README.md) | Project overview and quick start. |
| [../deploy/README.md](../deploy/README.md) | Deployment: CI/CD flow, dev compose stack, production Helm chart, secret store, Vault. |
| [../src/connectors-node/README.md](../src/connectors-node/README.md) | The Node connectors service and its HTTP contract. |
| [../clients/postyfox-connect/README.md](../clients/postyfox-connect/README.md) | The PostyFox Connect browser extension (cookie pairing). |
| [../testing-platforms/README.md](../testing-platforms/README.md) | Disposable local platform stacks for manual connector checks. |

The **interactive API contract** is served by each API at `/swagger` (UI) and `/openapi/v1.json`
(document): the source of truth for request/response schemas and status codes.

---

## HTTP endpoint reference

Both APIs sit behind one public edge. The gateway sends `/api/posts`, `/api/webhooks`, `/swagger-post`
and `/openapi-post` to post-api and every other `/api/` path to core-api.

Auth: browser calls go through the oauth2-proxy edge, which forwards an OIDC bearer token the APIs
validate in-app; machine callers use `X-API-Key`. Routes marked *anonymous* skip the auth layer.
Webhooks are authenticated by per-source signature instead.

### core-api

| Method | Path | Purpose |
|--------|------|---------|
| GET | `/healthz`, `/readyz` | Liveness / readiness (anonymous) |
| GET | `/api/version` | Build version (anonymous) |
| GET | `/swagger`, `/openapi/v1.json` | API docs |
| POST/GET/DELETE | `/api/profile/keys[/{id}]` | Create / list / revoke API keys |
| GET/PUT | `/api/profile/settings` | User settings |
| GET | `/api/profile/avatar` | Profile image (`?size=`) |
| POST/GET/DELETE | `/api/profile/invites[/{id}]` | Invite someone by email to manage your account (issue #409); revoke a pending invite |
| GET | `/api/profile/invites/pending` | Invites addressed to your own email, awaiting acceptance |
| POST | `/api/profile/invites/accept` | Accept an invite by its emailed token |
| POST | `/api/profile/invites/{id}/accept` | Accept an invite from the "pending" list, by id |
| GET | `/api/profile/accounts` | Accounts you can act as (self + accepted memberships); switch with the `X-Act-As` header |
| GET/DELETE | `/api/profile/members[/{memberUserId}]` | Who has delegated access to your account; revoke it |
| GET | `/api/terms` | Current terms of service and whether you (and the account acted as) accepted them (issue #417) |
| GET | `/api/terms/current` | Current terms of service only |
| POST | `/api/terms/accept` | Accept the current terms version; 409 if the version is stale |
| GET | `/api/admin/access` | Admin: confirms the caller has the `postyfox-admin` role |
| GET/PUT/DELETE | `/api/admin/operational-secrets[/{key}]` | Admin: connector operational secrets (status only, values never returned) |
| GET/PUT | `/api/admin/paired-user-agents[/{platform}]` | Admin: whether a cookie-paired platform replays the pairing browser's User-Agent |
| PUT | `/api/admin/terms` | Admin: publish a new terms version (forces re-acceptance); blank content turns terms off |
| GET | `/api/services[/{id}]` | Platform catalogue |
| GET/PUT/DELETE | `/api/connectors[/{id}]` | Configured connector CRUD |
| GET | `/api/connectors/{id}/authenticated` | Connector auth check |
| GET | `/api/connectors/{id}/targets` | List connector delivery targets |
| GET/PUT | `/api/connectors/{id}/destinations` | Destinations exposed for posting under one connector (e.g. Telegram chats) |
| GET | `/api/connectors/destinations` | Every exposed destination across the user's connectors |
| GET | `/api/connectors/{id}/limits` | Connector limits (live per instance for Fediverse) |
| POST | `/api/connectors/media-check` | Check media compatibility across connectors (including `NeedsFraming`) |
| POST | `/api/connectors/{id}/telegram/login` | Advance Telegram MTProto login |
| POST | `/api/connectors/{id}/oauth/start` | Begin the OAuth connect flow |
| GET | `/api/connectors/oauth/callback` | OAuth provider callback |
| GET | `/api/connectors/cookie-pairing/targets` | Sites a browser client can hand a session to |
| POST | `/api/connectors/cookie-pairing/pair` | Connect a site session collected by PostyFox Connect |
| POST | `/api/connectors/{id}/cookie-pairing/start` | Mint a one-use pairing token (fallback flow) |
| GET | `/api/connectors/cookie-pairing/sites` | Cookie-authenticated sites this deployment supports (anonymous) |
| POST | `/api/connectors/cookie-pairing/complete` | Redeem a pairing token (anonymous) |
| POST | `/api/media` | Upload a media file |
| GET | `/api/media/limits` | Configured max upload size |
| GET/HEAD | `/api/media/staged/{key}` | Staged media for Instagram to fetch (anonymous) |
| GET/PUT/DELETE | `/api/templates[/{id}]` | Posting template CRUD |
| GET/PUT/DELETE | `/api/text-templates[/{id}]` | Text template CRUD |
| GET/PUT/DELETE | `/api/tag-presets[/{id}]` | Tag preset CRUD |
| POST/GET/DELETE | `/api/triggers[/{id}]` | External-trigger registration |

### post-api

| Method | Path | Purpose |
|--------|------|---------|
| GET | `/healthz`, `/readyz`, `/swagger`, `/openapi/v1.json` | Ops / docs |
| POST | `/api/posts` | Create a post (202 + id), or save it as a draft |
| GET | `/api/posts` | List the user's posts (newest-first summaries), bounded by the retention window; `?filter=active` for in-flight only, `?limit=` (1..200, default 50) |
| GET | `/api/posts/{id}` | Aggregated post + per-target status |
| GET | `/api/posts/{id}/content` | Authored content as-is (loads a draft back into the compose form) |
| PUT | `/api/posts/{id}` | Update a draft; 409 once published |
| POST | `/api/posts/{id}/publish` | Publish a draft |
| POST | `/api/posts/{id}/duplicate` | Content of a post for "post again" (media copied to fresh blobs); does not create a post |
| POST | `/api/posts/{id}/cancel` | Cancel not-yet-sent targets (204; 409 if nothing left to cancel) |
| DELETE | `/api/posts/{id}` | Permanently delete a post + its stored payload/media (204) |
| DELETE | `/api/posts/history` | Delete every terminal post; drafts and in-flight posts are kept |
| DELETE | `/api/posts/{postId}/automations/{automationId}` | Cancel a pending repost/delete automation (409 if already run) |
| POST | `/api/webhooks/{sourceType}` | Inbound signed trigger webhook (anonymous; signature-verified) |

### connectors-node (internal, `X-Internal-Token`)

Rate limited per client IP (default 600 req/min, `RATE_LIMIT_MAX` overrides; `/health` exempt). Over the limit returns `429`.

| Method | Path | Purpose |
|--------|------|---------|
| GET | `/health` | Liveness |
| POST | `/connectors/{platform}/is-authenticated` | Platform auth check |
| POST | `/connectors/{platform}/list-targets` | List targets |
| POST | `/connectors/{platform}/limits` | Report platform/instance limits |
| POST | `/connectors/{platform}/deliver` | Deliver a rendered post |
| POST | `/connectors/{platform}/repost` | Repost/boost a delivered post (Bluesky, Fediverse) |
| POST | `/connectors/{platform}/delete` | Delete a delivered post (Bluesky, Tumblr, Fediverse) |
| POST | `/connectors/{platform}/refresh-token` | Renew an expiring token (Instagram, SoFurry, DeviantArt) |
| POST | `/connectors/{platform}/oauth/request-token` | Begin an OAuth connect |
| POST | `/connectors/{platform}/oauth/access-token` | Complete an OAuth connect |

Request and response shapes are in [../src/connectors-node/README.md](../src/connectors-node/README.md).
