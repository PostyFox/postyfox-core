# PostyFox Platform — Architecture (as built)

This describes the **containerised reimplementation** under `platform/` as it actually exists in
code (Phases 0–4). For the original requirements/plan see
[`../../docs/REIMPLEMENTATION_PLAN.md`](../../docs/REIMPLEMENTATION_PLAN.md); for the key decisions
and their rationale see [`DECISIONS.md`](./DECISIONS.md); for operational how-to see
[`../README.md`](../README.md).

> Diagrams are Mermaid (render on GitHub).

---

## 1. Overview

PostyFox is a multi-platform social **broadcasting / cross-posting backend**. A user connects social
accounts ("connectors"), then either publishes a post that is rendered per-platform and delivered to
every selected target, or registers an **external trigger** that auto-posts when a signed inbound
event arrives. This repo is the backend + infrastructure only (the control-panel UI is separate).

### Design principles

- **Cloud-agnostic** — no hard dependency on any single cloud; every external concern (DB, object
  store, message bus, secrets, auth edge, telemetry) sits behind an abstraction with a swappable
  implementation.
- **Stateless services, async pipeline** — APIs are stateless and horizontally scalable; delivery
  is decoupled through a message bus so it scales on queue depth and survives restarts.
- **Uniform extensibility** — adding a platform means implementing one connector contract; adding an
  event source means implementing one trigger-source contract.
- **Two language stacks by fit** — C# for the bulk; Node/TypeScript only where its libraries are
  materially better (Bluesky, Tumblr), behind the same connector contract.

---

## 2. Container view

```mermaid
flowchart TB
    client["Client / control panel"]
    edge["Ingress: oauth2-proxy<br/>(OIDC → Keycloak)<br/>[auth profile]"]

    subgraph apps["PostyFox services (containers)"]
        core["core-api (C#)<br/>profile/keys, services,<br/>connectors, templates, triggers"]
        post["post-api (C#)<br/>post intake + status,<br/>webhook callbacks"]
        worker["posting-worker (C#)<br/>generate → deliver pipeline"]
        node["connectors-node (TS)<br/>Bluesky, Tumblr"]
    end

    subgraph backing["Backing services"]
        pg[("PostgreSQL")]
        s3[("S3 / MinIO<br/>object store")]
        mq[["RabbitMQ<br/>(delayed exchange)"]]
        sec{{"Secret store<br/>(encrypted in PG / Vault)"}}
        otel["OTel Collector"]
    end

    ext["External platforms<br/>Discord · Telegram · Bluesky · Tumblr"]
    src["External event sources<br/>(signed webhooks)"]

    client --> edge --> core & post
    client -->|X-API-Key| core & post

    core --> pg & sec
    core -->|Bluesky/Tumblr auth,targets| node
    post --> pg & s3
    post -->|publish generate| mq
    src -->|POST /api/webhooks/:source| post

    mq -->|consume generate/deliver| worker
    worker --> pg & s3 & sec
    worker -->|Discord, Telegram MTProto| ext
    worker -->|deliver Bluesky/Tumblr| node --> ext

    core & post & worker & node -.OTLP.-> otel
```

### Components

| Component | Runtime | Responsibility |
|-----------|---------|----------------|
| **core-api** | ASP.NET Core (.NET 10) | Identity/API keys, service catalogue, connector CRUD + auth/target ops, templates, trigger registration. Applies EF migrations + seeds catalogue on boot. |
| **post-api** | ASP.NET Core (.NET 10) | Post intake + status; inbound external-trigger webhook callbacks. Publishes pipeline commands. |
| **posting-worker** | .NET Worker | Consumes `generate`/`deliver` queues; renders + delivers each target; owns retries/backoff/DLQ + status rollup. |
| **connectors-node** | Node 24 / Fastify | Bluesky (`@atproto/api`) + Tumblr (`tumblr.js`) behind an `IConnector`-shaped HTTP contract; internal-token auth; stateless. |
| PostgreSQL | — | System of record. |
| S3 / MinIO | — | Media, post payloads, Telegram MTProto sessions. |
| RabbitMQ | — | Pipeline queues; delayed-message exchange for scheduling + retry backoff. |
| Secret store | — | Per-user connector secrets, platform secrets, trigger signing secrets. |
| oauth2-proxy + Keycloak | — | OIDC edge (opt-in `auth` compose profile); injects the trusted identity header. |
| OTel Collector | — | Traces + metrics sink (OTLP). |

---

## 3. Code structure (.NET solution)

Clean-architecture layering; dependencies point inward.

```mermaid
flowchart LR
    Domain --> Application
    Application --> Infrastructure
    Domain --> Infrastructure
    Application --> Web
    Application --> ApiCore[Api.Core]
    Application --> ApiPost[Api.Post]
    Application --> Worker[Worker.Posting]
    Infrastructure --> ApiCore
    Infrastructure --> ApiPost
    Infrastructure --> Worker
    Web --> ApiCore
    Web --> ApiPost
```

| Project | Contents |
|---------|----------|
| `PostyFox.Domain` | Entities + enums. No dependencies. |
| `PostyFox.Application` | Abstractions (`IAppDbContext`, `IObjectStore`, `IMessageBus`, `ISecretStore`, `IConnector`, `ITelegramGateway`, `ITriggerSource`, …), services/use-cases, template engine, pipeline handlers, connector + trigger contracts, DTOs. |
| `PostyFox.Infrastructure` | EF Core `AppDbContext` + migrations, S3 object store, RabbitMQ bus + topology, encrypted secret store, connectors (Discord, Telegram/WTelegram, `HttpConnector`), catalogue seeder. |
| `PostyFox.Web` | Shared auth handlers (header + API key) and OpenTelemetry wiring. |
| `PostyFox.Api.Core` / `PostyFox.Api.Post` | Minimal-API hosts + endpoint groups. |
| `PostyFox.Worker.Posting` | Hosts the queue consumers. |
| `tests/*` | 5 xUnit projects (91 tests). `connectors-node` has its own 15 tests. |

The `Application` layer deliberately depends on EF Core Core (`IAppDbContext` exposes `DbSet<>`),
trading a little purity for far less repository boilerplate.

---

## 4. Data model

```mermaid
erDiagram
    users ||--o{ api_keys : owns
    users ||--o{ user_connectors : configures
    users ||--o{ templates : owns
    users ||--o{ posts : creates
    users ||--o{ external_triggers : registers
    service_definitions ||--o{ user_connectors : "instance of"
    posts ||--o{ post_targets : "fans out to"
    user_connectors ||--o{ post_targets : "delivered via"

    users {
        string Id PK "OIDC subject"
    }
    api_keys {
        guid Id PK
        string UserId
        string Prefix
        string KeyHash
        datetime RevokedAt
    }
    service_definitions {
        string Id PK
        string Platform
        string ConfigSchema
        string SecureConfigSchema
        bool Enabled
    }
    user_connectors {
        guid Id PK
        string UserId
        string ServiceDefinitionId FK
        string ConfigJson
        bool Enabled
    }
    templates {
        guid Id PK
        string UserId
        string Title
        string MarkdownBody
    }
    posts {
        guid Id PK
        string UserId
        string TagsJson
        string MediaManifestJson
        string VariablesJson
        guid TemplateId
        datetime PostAt
        string RootStatus
    }
    post_targets {
        guid Id PK
        guid PostId FK
        guid ConnectorId
        string Platform
        string RenderedContentJson
        string Status
        string ExternalId
        int Attempts
    }
    external_triggers {
        guid Id PK
        string UserId
        string SourceType
        string ExternalAccount
        guid TemplateId
        guid TargetConnectorId
        int NotifyFrequencyHrs
        datetime LastFiredAt
    }
    external_interests {
        string SourceType PK
        string ExternalAccount PK
        string UserId PK
    }
    webhook_dedupe {
        string Source PK
        string MessageId PK
        datetime SeenAt
    }
    secrets {
        string Name PK
        string CipherText
    }
```

Notes:
- **Secrets are never in domain tables.** Per-user connector secrets live in the secret store under
  `conn-{connectorId:N}-{userId}`; trigger signing secrets under `trigger-{sourceType}-signing`;
  platform secrets (e.g. `TelegramApiID`/`TelegramApiHash`) under their own names. The `secrets`
  table is the local encrypted-at-rest (AES-256-GCM) backing for `ISecretStore`; production can
  swap it for Vault/KMS.
- **Enums stored as strings** (`RootStatus`, `Status`) for readability.
- **Object store**: `post/{postId}/{title|description|description-html}`, media manifest entries, and
  `telegram/{userId}` MTProto session blobs.

---

## 5. Authentication & authorization

```mermaid
flowchart LR
    req["Incoming request"] --> policy{"X-API-Key present?"}
    policy -- yes --> apik["ApiKey scheme<br/>validate hash → userId"]
    policy -- no --> hdr["Header scheme<br/>X-Auth-Request-User → userId<br/>(DevMode → dev-user)"]
    apik & hdr --> claims["ClaimsPrincipal<br/>(NameIdentifier = userId)"]
```

- A **policy scheme** (`PostyFox`) forwards to one of two handlers based on the presence of the
  `X-API-Key` header.
- **Header scheme** trusts the identity header injected by the oauth2-proxy edge (which performs the
  OIDC exchange against Keycloak). `Auth:DevMode=true` authenticates every request as `Auth:DevUserId`
  for local iteration.
- **API-key scheme** validates the presented key against a PBKDF2 hash (constant-time), for
  external/machine callers — the retained requirement. Keys are prefix-indexed; the secret is never
  stored.
- Webhook callbacks are anonymous at the auth layer and instead authenticated per-source by
  **signature verification** (see §8).

---

## 6. Connector framework

Every platform implements `IConnector`: `Describe`, `IsAuthenticatedAsync`, `ListTargetsAsync`,
`DeliverAsync`. A `ConnectorRegistry` resolves connectors by platform key. The delivery handler and
the connector-ops endpoints never hard-code a platform.

| Platform | Where it runs | Library / mechanism |
|----------|---------------|---------------------|
| Discord | C# in-process | webhook HTTP |
| Telegram | C# in-process | **MTProto user account** via `WTelegramClient`, behind `ITelegramGateway` |
| Bluesky | connectors-node | `@atproto/api` |
| Tumblr | connectors-node | `tumblr.js` |

The C# **`HttpConnector`** adapter fulfils `IConnector` for Bluesky/Tumblr by forwarding to
connectors-node over HTTP (`POST /connectors/{platform}/{is-authenticated|list-targets|deliver}`),
passing the resolved config + secret in the request body so the Node service stays **stateless**. All
internal calls carry a shared `X-Internal-Token`.

```mermaid
sequenceDiagram
    participant W as posting-worker
    participant R as ConnectorRegistry
    participant H as HttpConnector (BlueSky)
    participant N as connectors-node
    participant B as Bluesky
    W->>R: resolve "BlueSky"
    R-->>W: HttpConnector
    W->>H: DeliverAsync(context, renderedPost)
    H->>N: POST /connectors/BlueSky/deliver (X-Internal-Token)
    N->>B: @atproto/api createRecord
    B-->>N: uri / cid
    N-->>H: { success, externalId, externalUrl }
    H-->>W: DeliveryResult
```

**Telegram statefulness**: MTProto login is interactive and session-based. Sessions persist to the
object store; in-progress login clients are held per-instance. Route a user's Telegram operations to
a single instance (consistent hashing / dedicated telegram-worker) — see
[`../../docs/REIMPLEMENTATION_PLAN.md`](../../docs/REIMPLEMENTATION_PLAN.md) §4.5. The MTProto work
sits behind `ITelegramGateway` so the connector and login flow are unit-tested with a fake.

---

## 7. Posting pipeline

```mermaid
sequenceDiagram
    actor U as User
    participant P as post-api
    participant Q as RabbitMQ
    participant W as posting-worker
    participant C as Connector
    U->>P: POST /api/posts (targets, content, PostAt?)
    P->>P: persist Post + PostTarget[] (Queued), store payload
    P->>Q: publish generate(target) [delay if scheduled]
    P-->>U: 202 { postId }
    Q->>W: generate(target)
    W->>W: render via template engine → Ready
    W->>Q: publish deliver(target)
    Q->>W: deliver(target)
    W->>C: DeliverAsync
    alt success
        C-->>W: externalId/url → Delivered
    else transient failure (attempts < max)
        W->>Q: re-publish deliver(target) with exponential backoff
    else exhausted
        W->>W: Failed
    end
    W->>W: recompute root status
    U->>P: GET /api/posts/{id} → aggregated status
```

Target states: `Queued → Generating → Ready → Delivering → Delivered | Failed`.
Root rollup: all delivered → `Delivered`; mixed terminal → `PartiallyFailed`; none delivered →
`Failed`; otherwise `Delivering`/`Generating`/`Queued`. Retry count and base backoff are configured
via `Pipeline:MaxDeliveryAttempts` / `Pipeline:RetryBaseSeconds`.

---

## 8. External-trigger framework

Source-agnostic. An `ITriggerSource` encapsulates a source's signature scheme and payload shape; a
generic HMAC-signed webhook source ships built-in (`X-Signature` = hex HMAC-SHA256 of the body,
`X-Message-Id` for dedupe).

```mermaid
sequenceDiagram
    participant S as External source
    participant P as post-api (/api/webhooks/:source)
    participant T as ExternalTriggerService
    participant Q as pipeline
    S->>P: POST signed webhook
    P->>T: HandleWebhook(source, headers, body)
    T->>T: parse → challenge? echo & return
    T->>T: verify signature (else 401)
    T->>T: dedupe by (source, messageId) (else AlreadyProcessed)
    loop each matching trigger for ExternalAccount
        T->>T: within NotifyFrequencyHrs? skip
        T->>Q: create templated post (target connector, variables)
        T->>T: set LastFiredAt
    end
    T-->>P: Processed(firedCount) → 200
```

Registration (`POST /api/triggers`) records an `external_triggers` row (source, external account,
template, target connector, frequency). Fan-out reuses the normal posting pipeline, so triggered
posts get the same rendering, delivery, retry and status behaviour.

---

## 9. Messaging topology

- One durable **`x-delayed-message`** exchange (`postyfox`, delegate type `direct`) — the delay
  header powers both scheduled posts and retry backoff.
- Queues `generate` and `deliver`, each bound by routing key = queue name, each dead-lettering to
  `postyfox.dlx` → `{queue}.dlq`.
- Publisher declares the queue before publishing so messages aren't lost if the worker is down.
- Consumers ack on success; an unhandled exception nacks → DLQ (per-target *delivery* retries are
  handled in the pipeline handler via delayed re-publish, not transport requeue).

---

## 10. Observability, deployment, testing

- **Observability**: OpenTelemetry traces + metrics exported via OTLP (`OTEL_EXPORTER_OTLP_ENDPOINT`)
  to any collector/backend. `/healthz` (liveness) and `/readyz` (DB connectivity) on both APIs.
- **Security hardening**: a global fixed-window **rate limiter** (config-driven, partitioned by
  user/IP, HTTP 429) and conservative **security response headers** on both APIs.
- **Deployment** — three modes, all consuming the same published images
  (`{registry}/{repository}-{service}:{tag}`):
  1. **docker-compose** (`platform/deploy/docker-compose.yml`) — full local stack; `auth` profile
     adds Keycloak + oauth2-proxy.
  2. **Helm chart** (`platform/deploy/helm/postyfox`) — the 4 services + config/secret/ingress for
     any Kubernetes; backing services provided externally.
  3. **Terraform → Azure Container Apps** (`platform/deploy/terraform-aca`) — deploys the published
     images to ACA (external ingress for the APIs, internal for connectors-node, no ingress for the
     worker).

  One templated `Dockerfile` builds each .NET service (build args select project + assembly);
  connectors-node has its own multi-stage Dockerfile. CI (`.github/workflows/platform-ci.yml`)
  builds + tests both stacks, lints the IaC, and builds/pushes images.
- **Config**: 12-factor env vars, nested with `__` (see [`../README.md`](../README.md#configuration-env-vars)).
- **Testing**: unit + integration tests per layer using in-memory SQLite / EF-InMemory and fakes for
  I/O — no Docker required. The pipeline is covered end-to-end via an in-process bus that drives the
  real handlers. Not covered: the live MTProto gateway and the live external-platform calls (need
  real credentials) — the logic around them is tested via seams.

---

## 11. Known constraints & follow-ups

See [FOLLOWUPS.md](./FOLLOWUPS.md) for the full list. Headlines:

- **🔴 Media delivery is deferred** — the pipeline carries a media manifest end-to-end but no
  connector uploads media yet (text-only delivery). This is a **key product requirement** and the
  next work item.
- Telegram MTProto is stateful (single-writer routing) and not integration-tested (needs live creds).
- No admin endpoint yet for platform-level secrets (Telegram api id/hash, trigger signing secrets).
- Scheduling relies on the RabbitMQ delayed-message plugin; a durable scheduler is a follow-up.
- Twitch was **descoped** and is intentionally absent.
