# PostyFox Platform (container reimplementation)

Cloud-agnostic, containerised reimplementation of PostyFox.

**Documentation:** [docs/ARCHITECTURE.md](./docs/ARCHITECTURE.md) (as-built architecture + diagrams) ·
[docs/DECISIONS.md](./docs/DECISIONS.md) (ADRs) · [docs/README.md](./docs/README.md) (index + endpoint
reference) · [docs/FOLLOWUPS.md](./docs/FOLLOWUPS.md) (deferred work).

## Status

| Phase | Scope | State |
|-------|-------|-------|
| **0 — Foundations** | Solution layout, EF Core/Postgres, S3 object store, RabbitMQ bus, secret store, OpenTelemetry, health checks, docker-compose stack | ✅ done |
| **1 — Identity, connectors, templates** | oauth2-proxy header auth + hashed API keys, service catalogue + connector CRUD, template CRUD | ✅ done |
| **2 — Posting pipeline** | Post intake, template engine, generate→deliver worker with retries/backoff/DLQ, scheduling, status API, Discord connector | ✅ done |
| **3 — Connectors** | Telegram (MTProto/WTelegramClient), Bluesky + Tumblr (Node service via HTTP adapter), connector auth/target endpoints. **Twitch descoped.** | ✅ done |
| **4 — External triggers** | Generic source-agnostic trigger framework: signed webhooks → dedupe → frequency-throttled fan-out into the pipeline (generic HMAC source). | ✅ done |
| **5 — Hardening & deploy** | Dependency audit suppressions, rate limiting + security headers, CI, Helm chart + Terraform/ACA deploy, OTLP→OpenSearch observability. | ✅ done |

> **Media/image delivery is implemented** across all connectors (upload → object store → per-platform
> upload). Residuals (alt text, video, limits) are in [docs/FOLLOWUPS.md](./docs/FOLLOWUPS.md).

## Architecture

| Concern | Choice | Abstraction |
|---------|--------|-------------|
| Datastore | PostgreSQL (EF Core / Npgsql) | `IAppDbContext` |
| Object storage | S3-compatible (MinIO local) | `IObjectStore` |
| Message bus | RabbitMQ (delayed-message exchange for scheduling + backoff) | `IMessageBus` |
| Secrets | Encrypted-in-Postgres (AES-GCM) locally; swap for Vault/KMS | `ISecretStore` |
| AuthN | oauth2-proxy → Keycloak, header identity; **or** `X-API-Key` (hashed) | `PostyFox.Web.Auth` |
| Observability | OpenTelemetry → OTLP collector | — |

### Projects

```
src/
  PostyFox.Domain          entities + enums (no deps)
  PostyFox.Application      abstractions, services, template engine, pipeline handlers, connector contract
  PostyFox.Infrastructure   EF Core + migrations, S3, RabbitMQ, secret store, Discord connector
  PostyFox.Web              shared auth (oauth2-proxy header + API key) + OpenTelemetry wiring
  PostyFox.Api.Core         profile/keys, services catalogue, connector CRUD, template CRUD
  PostyFox.Api.Post         post intake + status, external-trigger webhook callback
  PostyFox.Worker.Posting   consumes generate/deliver queues; runs the pipeline
  connectors-node/          Node/TS service: Bluesky (@atproto/api) + Tumblr (tumblr.js)
tests/                      one project per layer (xUnit) — 99 C# tests (+18 in connectors-node)
```

### The connector contract

Every platform implements `IConnector` (`Describe`/`IsAuthenticated`/`ListTargets`/`Deliver`).
Adding a platform = implement `IConnector` + a `ServiceDefinition` row.

| Platform | Stack | Library / mechanism |
|----------|-------|---------------------|
| Discord | .NET in-process | webhook HTTP |
| Telegram | .NET in-process | **MTProto user account** via WTelegramClient (blob-backed session; see §4.5 statefulness note) |
| Bluesky | **Node** service | `@atproto/api` — via the `HttpConnector` adapter over internal HTTP (`X-Internal-Token`) |
| Tumblr | **Node** service | `tumblr.js` — same adapter |
| ~~Twitch~~ | — | descoped |

`connectors-node` exposes an `IConnector`-shaped HTTP contract (`/connectors/:platform/{is-authenticated,list-targets,deliver}`); the C# `HttpConnector` forwards to it, passing the resolved config + secret in the request so the Node side stays stateless. Connector auth/target operations are exposed at `GET /api/connectors/{id}/authenticated`, `GET /api/connectors/{id}/targets`, and the Telegram login flow at `POST /api/connectors/{id}/telegram/login`.

### External triggers (Phase 4)

Source-agnostic: an `ITriggerSource` encapsulates each source's signature scheme + payload shape (a
generic HMAC-signed webhook source ships built-in). Register interest via `POST /api/triggers`;
inbound events hit `POST /api/webhooks/{sourceType}` (anonymous) → signature-verified → deduped by
message id → fanned out to matching triggers, each throttled by `NotifyFrequencyHrs`, creating a
templated post through the normal pipeline.

### Posting pipeline

`POST /api/posts` persists a root post + one `PostTarget` per connector, stores payload/media, and
publishes a `generate` command per target. The worker renders each target (template engine), then
publishes a `deliver` command; delivery routes to the connector, captures the external id/url, and
rolls the per-target status up into the root status (`Delivered` / `PartiallyFailed` / `Failed`).
Transient delivery failures retry with exponential backoff (delayed re-publish) up to
`Pipeline:MaxDeliveryAttempts`, then fail. `GET /api/posts/{id}` returns aggregated status.

## Run locally

```bash
cd deploy
docker compose up --build            # infra + APIs + worker (APIs in DevMode, no login)
# Core API:  http://localhost:8080    Post API: http://localhost:8081
# Swagger UI: http://localhost:8080/swagger  and  http://localhost:8081/swagger
# OpenAPI:    http://localhost:8080/openapi/v1.json  (and :8081)
# RabbitMQ:  http://localhost:15672   MinIO console: http://localhost:9001
# connectors-node (Bluesky/Tumblr): http://localhost:8090/health

docker compose --profile auth up --build   # adds Keycloak + oauth2-proxy edge
```

In DevMode every request is authenticated as `dev-user`. With the `auth` profile, oauth2-proxy
performs the OIDC exchange against Keycloak and injects the `X-Auth-Request-User` header the APIs
trust. External/machine callers authenticate with `X-API-Key: <key>` (create one via
`POST /api/profile/keys`).

> The RabbitMQ image build downloads the delayed-message-exchange plugin (needs network at build).

## Build & test

```bash
# from the repo root
dotnet build                 # whole solution (PostyFox.Platform.slnx)
dotnet test                  # all 99 unit/integration tests
# Node connectors:  cd src/connectors-node && npm ci && npm test   # 18 tests

# EF migrations
dotnet dotnet-ef migrations add <Name> --project src/PostyFox.Infrastructure
```

Tests use in-memory SQLite / EF-InMemory and fakes for I/O — no Docker required to run them.

## Deploy

All modes consume the same published images (`{registry}/{repository}-{service}:{tag}`, built by
CI in [`.github/workflows/platform-ci.yml`](./.github/workflows/platform-ci.yml)):

- **docker-compose** — `deploy/docker-compose.yml` (local / single-host).
- **Helm** — `deploy/helm/postyfox` for any Kubernetes (`helm install postyfox deploy/helm/postyfox`);
  backing services (Postgres/RabbitMQ/object store) provided externally via values.
- **Terraform → Azure Container Apps** — `deploy/terraform-aca` deploys the images to ACA
  (`terraform apply`), with external ingress for the APIs and internal for connectors-node.

Telemetry (OTLP) is exported to a collector and forwarded to central **OpenSearch** — see
[deploy/observability/README.md](./deploy/observability/README.md).

## Configuration (env vars)

Nested keys use `__`. Key settings: `ConnectionStrings__Postgres`, `ObjectStore__*`, `RabbitMq__*`,
`Secrets__EncryptionKey` (base64 32-byte AES key), `Auth__DevMode`, `Auth__UserHeader`,
`NodeConnectors__BaseUrl` + `NodeConnectors__InternalToken` (→ connectors-node; also set as
`INTERNAL_TOKEN` on that service), `ApplyMigrations`, `SeedServiceDefinitions`,
`OTEL_EXPORTER_OTLP_ENDPOINT`.

Trigger signing secrets live in the secret store under `trigger-{sourceType}-signing`. Telegram
requires `TelegramApiID` / `TelegramApiHash` in the secret store (real MTProto credentials).

## Known follow-ups

- Security advisories are resolved by explicit version pins (no audit suppressions):
  `Microsoft.OpenApi` → **2.7.5** in the API projects (the framework's `AspNetCore.OpenApi` 10.0.9
  otherwise pulls the vulnerable 2.0.0 transitively), and `SQLitePCLRaw.bundle_e_sqlite3` → **3.0.3**
  in the test projects (EF Core Sqlite otherwise pulls the flagged 2.1.11 native lib).
- Scheduling uses the RabbitMQ delayed-message plugin; a durable scheduler / due-scan is a Phase 5
  hardening item for very long-horizon schedules.
- **Telegram MTProto is stateful**: route a user's Telegram ops to a single instance (consistent
  hashing / dedicated telegram-worker) — see reimplementation plan §4.5. The MTProto gateway is not
  covered by automated tests (needs live credentials); the connector logic around it is (via the
  `ITelegramGateway` seam).
- Node connectors deliver text posts; media upload (fetching from object storage) is a follow-up.
- There is no endpoint yet to set platform-level secrets (e.g. Telegram api id/hash, trigger signing
  secrets) — seed them into the secret store directly for now.
