# PostyFox — Requirements & Reimplementation Plan

> **⚠️ This is the original pre-implementation plan.** Phases 0–4 are now built under `platform/`.
> For the **as-built architecture** (which supersedes this document where they differ) see
> [`../platform/docs/ARCHITECTURE.md`](../platform/docs/ARCHITECTURE.md) and
> [`../platform/docs/DECISIONS.md`](../platform/docs/DECISIONS.md).
> Two subsequent scope changes are **not** reflected below: **Twitch was descoped entirely**
> (so external triggers are a generic, source-agnostic framework, not Twitch EventSub), and
> **Telegram uses the MTProto user account** (WTelegramClient), not the Bot API.

> **Status:** Draft for review. This document is derived from a ground-truth read of the
> current source (not the aspirational `docs/` folder, which describes behaviour that does
> not exist — JWT validation in-app, API-key comparison, a two-stage queue pipeline, status
> tracking, Bluesky support, etc.). Where this document and the older `docs/` disagree,
> **this document wins**.
>
> **Target for reimplementation (agreed):**
> - Platform: **cloud-agnostic containers** (Docker → Azure Container Apps / Kubernetes / anywhere).
> - Scope: the **full intended vision**, including the parts currently stubbed.
> - Stacks: **C# for the bulk**, **Node/TypeScript where C# libraries are weak** (Bluesky, Tumblr) —
>   two live API stacks behind one connector contract.

---

## 1. What PostyFox is

PostyFox is a **multi-platform social broadcasting / cross-posting backend**. A user connects
their accounts on several social/messaging platforms, then either:

1. **Publishes a post** (title, description, HTML body, tags, media, optional schedule time) that
   PostyFox renders per-platform and delivers to every selected target; or
2. **Registers an external trigger** (e.g. a Twitch channel going live) that automatically fires a
   templated post to their chosen targets, subject to a frequency limit.

This repository is the **backend + infrastructure** only. The control-panel UI (`cp.postyfox.com`)
is a separate project and out of scope here.

### 1.1 Supported / intended platforms

| Platform | Direction | Library | Current state |
|---|---|---|---|
| Telegram | Deliver + list chats | `WTelegramClient` (MTProto) | Auth + send working in Core; posting-side incomplete |
| Twitch | External trigger source + announcements | `Twitch.Net.*` (vendored) | EventSub register + callback handshake work; auto-post is TODO |
| Discord | Deliver (webhook) | plain HTTP | Placeholder only |
| Bluesky | Deliver | `@atproto/api` (Node) | Not implemented (commented out) |
| Tumblr | Deliver | `tumblr.js` (Node) | OAuth flow only |

The architecture must make **adding a platform** a well-defined, uniform operation (implement one
connector contract + register a service template).

---

## 2. Current system — accurate summary

### 2.1 Components (as they exist today)

- **PostyFox-NetCore** (.NET 10 isolated Functions, `api.postyfox.com`): Profile/API-token CRUD,
  Service (connector) CRUD, Posting-template list/upsert, Twitch subscription registration,
  Telegram auth + messaging.
- **PostyFox-Posting** (.NET 10 isolated Functions, `post.postyfox.com`): `Post` intake endpoint
  (writes blobs + enqueues), Twitch EventSub callback, a partial Telegram chat lister. **The queue
  consumers `GeneratePost` and `QueuePost` only log — no post is ever generated or delivered.**
- **PostyFox-TypeScript** (Node 20 Functions, `api2.postyfox.com`): Tumblr OAuth URL + callback;
  Bluesky commented out; `Tumblr_isAuthenticated` body commented out.
- **PostyFox-DataLayer** (.NET lib): DTOs + Azure Table entities + `TelegramStore` (blob-backed
  MTProto session `MemoryStream`).
- **PostyFox-Common** (.NET lib): `Templating` — empty stub.
- **Vendor/Twitch.Net.***: vendored Twitch API + EventSub client libraries.

### 2.2 Authentication (important — the old docs are wrong)

- Auth is enforced by the **Azure Functions EasyAuth** layer (`auth_settings_v2`, OIDC against
  **Keycloak** — `auth.postyfox.com/realms/PostyFox`). The platform validates the token and injects
  `X-MS-CLIENT-PRINCIPAL` / `X-MS-CLIENT-PRINCIPAL-ID` headers.
- **Application code merely trusts those headers** (`AuthHelper`, TS `Helpers/index.ts`). No
  signature validation happens in code.
- `PostyFoxDevMode=true` bypasses auth entirely and uses `PostyFoxUserID`.
- The **API-key** feature stores 40-char keys but the only consumer (`Post.cs`) validates by
  `PartitionKey=userId + RowKey=keyId` and **never compares the key value** → not a real secret.

### 2.3 Data (as-is)

- **Tables:** `UserProfilesAPIKeys`, `AvailableServices` (PK=`Service`), `ConfigTable`
  (per-user service configs, PK=userId), `PostingTemplates`, `ExternalTriggers`,
  `ExternalInterests`, `TwitchRequests`.
- **Queue:** `postingqueue` (single queue; the "generatequeue" in the docs is not wired up).
- **Blobs:** `post/{postId}/{title|description|description-html|tags|media|lock-*}`;
  `telegram/{userId}` (session).
- **Secrets:** OIDC client secret, Twitch client/signature secrets, Telegram API id/hash, and
  per-user service secrets stored as `{serviceId}-{userId}` via the `aneillans.adapters-secrets`
  abstraction (KeyVault / Infisical / BitWarden).

### 2.4 Known weaknesses to fix in the reimplementation

1. **No delivery pipeline** — generation, per-platform delivery, retries, and status are all absent.
2. **No status tracking** — no `Post_GetStatus`, no status store.
3. **API-key validation doesn't check the secret** — trivially forgeable.
4. **`StaticState` Telegram client cache** — an in-process `Dictionary` (author-flagged as "a
   complete and utter hack"); breaks across stateless replicas.
5. **Empty templating** — no variable substitution or per-platform formatting.
6. **Azure-coupled everything** — Tables/Blobs/Queues/KeyVault/EasyAuth/App Insights.

---

## 3. Target requirements (full vision, platform-agnostic)

### 3.1 Functional requirements

**Identity & access**
- FR-1 Authenticate users via OIDC (Keycloak or any compliant IdP).
- FR-2 Resolve a stable `userId` for every authenticated request.
- FR-3 Machine-to-machine access via **API keys that are hashed at rest and compared as secrets**
  (or OIDC client-credentials — see §4.4).
- FR-4 Issue / list (truncated) / revoke API keys.

**Connector (service) management**
- FR-5 List platform definitions available to configure (`AvailableServices` equivalent), including
  which config fields are required and which are secret.
- FR-6 CRUD a user's configured connectors; non-secret config in the DB, secret config in the
  secret store keyed per user+connector.
- FR-7 Per-connector auth sub-flows (Telegram MTProto login, Tumblr OAuth, Bluesky app-password,
  Twitch channel resolution) exposed as connector-specific operations.
- FR-8 Query a connector's available delivery targets (Telegram chats, Tumblr blogs, Discord
  webhooks, etc.).

**Templating**
- FR-9 CRUD posting templates (title + markdown body).
- FR-10 A real templating engine: variable substitution (`{game}`, `{date}`, trigger-supplied
  variables…), conditionals, and **per-platform rendering** (markdown → Telegram HTML, Bluesky
  facets, Discord embed, character-limit handling, Tumblr NPF/HTML).

**Posting pipeline (the core new work)**
- FR-11 Accept a post (title, description, HTML description, tags, media refs, target list,
  optional `PostAt`) and persist it as a **root post + one child per target**.
- FR-12 Upload media once to object storage; reference from each target.
- FR-13 **Generate** stage: render platform-specific content per target (template + variables).
- FR-14 **Deliver** stage: route each target to its connector, call the external API, capture the
  external post id + URL.
- FR-15 **Status tracking**: per-target status (`Queued → Generating → Ready → Delivering →
  Delivered | Failed`) and an aggregated root status (`Delivered | PartiallyFailed | Failed`),
  queryable via `GET /posts/{id}`.
- FR-16 **Retries** with exponential backoff and a dead-letter path for permanent failures.
- FR-17 **Scheduling**: honour `PostAt` (deliver at/after the scheduled time).
- FR-18 **Idempotency**: safe re-processing of duplicate messages (dedupe key per target attempt).

**External triggers**
- FR-19 Register interest in an external event (currently Twitch `stream.online`) mapping
  event → user(s) → template + target(s) + frequency.
- FR-20 Receive external webhooks (signature-validated), dedupe, and fan out to all interested users.
- FR-21 Enforce per-trigger frequency (`NotifyFrequencyHrs`) via last-fired tracking.
- FR-22 Framework must generalize to future trigger sources.

**Connectors (uniform contract)** — each platform implements:
- `describe()` capabilities (title? media? threads? char limit? auth type),
- `authenticate()` / `isAuthenticated()`,
- `listTargets()`,
- `deliver(renderedPost, media) → { externalId, url }`.

### 3.2 Non-functional requirements

- NFR-1 **Portability**: no hard dependency on any single cloud; all infra behind abstractions
  (`ISecretsProvider` already exists; add `IObjectStore`, `IMessageBus`, DB via EF Core / repos).
- NFR-2 **Stateless API replicas**; horizontal scale. Workers scale on queue depth (KEDA / Container
  Apps scale rules).
- NFR-3 **Security**: real token validation, hashed API keys, secrets never logged, least-privilege
  service identities, TLS everywhere, per-user data isolation.
- NFR-4 **Observability**: OpenTelemetry traces/metrics/logs to any backend; `/healthz` + `/readyz`.
- NFR-5 **Local dev parity**: one `docker compose up` brings up the full stack + backing services.
- NFR-6 **Resilience**: at-least-once delivery + idempotency ⇒ effectively-once user experience.

---

## 4. Target architecture

### 4.1 Backing-service mapping (Azure → cloud-agnostic)

| Azure today | Purpose | Cloud-agnostic target | App-side abstraction |
|---|---|---|---|
| Table Storage | users, keys, connectors, templates, triggers, status | **PostgreSQL** (normalized schema) | EF Core + repository interfaces |
| Blob Storage | media, Telegram session, post payloads | **S3-compatible** (MinIO local; S3/GCS/Azure Blob prod) | `IObjectStore` |
| Queue Storage | `postingqueue` | **RabbitMQ** (or NATS JetStream / Redis Streams) | `IMessageBus` |
| Key Vault | secrets | **Infisical / HashiCorp Vault / K8s secrets / env** | `ISecretsProvider` (reuse existing) |
| EasyAuth (OIDC) | authn + header injection | **oauth2-proxy at ingress** (preferred) *or* in-app OIDC middleware | auth middleware (see §4.4) |
| App Insights | telemetry | **OpenTelemetry Collector → any backend** | OTel SDK |

PostgreSQL is the recommended default store: relational integrity for the post/target/status graph,
portable, and well-supported in both C# (EF Core / Npgsql) and Node.

### 4.2 Service decomposition (containers)

```
                ┌────────────────────────── ingress / oauth2-proxy (OIDC → Keycloak) ──────────────────────────┐
                │  validates token, injects X-Auth-User headers (preserves current trust-the-header model)      │
                └───────────────┬───────────────────────────┬───────────────────────────┬─────────────────────┘
                                │                            │                           │
                     ┌──────────▼─────────┐      ┌───────────▼──────────┐     ┌──────────▼───────────┐
                     │  core-api (C#)     │      │ post-api (C#)        │     │ connectors-node (TS) │
                     │  ASP.NET Core      │      │ ASP.NET Core         │     │ NestJS/Express       │
                     │  profile/keys      │      │ POST /posts (intake) │     │ Bluesky, Tumblr      │
                     │  connectors CRUD   │      │ GET  /posts/{id}     │     │ connector contract   │
                     │  templates         │      │ external-trigger     │     │ (HTTP internal)      │
                     │  Telegram/Twitch   │      │   webhooks (Twitch)  │     └──────────┬───────────┘
                     │  auth sub-flows    │      └───────────┬──────────┘                │
                     └──────────┬─────────┘                  │ publish PostRequested     │
                                │                            ▼                           │
                                │                   ┌──────────────────┐                 │
                                │                   │  message bus      │                 │
                                │                   │  (RabbitMQ/NATS)  │                 │
                                │                   └───────┬──────────┘                 │
                                │                           │ consume                    │
                                │                 ┌─────────▼───────────┐                 │
                                │                 │ posting-worker (C#) │─────────────────┘  delegate delivery
                                │                 │ generate → deliver  │  (C# connectors in-proc;
                                │                 │ status, retries,    │   Node connectors via internal HTTP)
                                │                 │ scheduling, DLQ     │
                                │                 └─────────┬───────────┘
                                ▼                           ▼
                     ┌──────────────────┐        ┌────────────────────────────┐
                     │ PostgreSQL       │        │ Object store (S3/MinIO)     │
                     │ Secret store     │        │ media + Telegram sessions   │
                     └──────────────────┘        └────────────────────────────┘

  telegram-worker (C#, stateful/single-writer per user session — see §4.5)
```

Rationale for the split (keeps the current logical boundaries, containerized):
- **core-api** — user-facing config & connector auth flows (was PostyFox-NetCore).
- **post-api** — post intake, status, external webhooks (was PostyFox-Posting HTTP surface).
- **posting-worker** — the missing pipeline; owns generation, delivery orchestration, status, retries.
- **connectors-node** — platforms with no good C# library (Bluesky, Tumblr), behind the same
  connector contract, invoked by the worker over internal HTTP/gRPC.
- **telegram-worker** — isolates MTProto's stateful session requirement from the stateless APIs.

Services can be merged/split later; the message bus + connector contract are the real seams.

### 4.3 The connector contract (the extensibility seam)

A single interface, implemented in-process by C# connectors (Telegram, Twitch, Discord) and over
internal HTTP by Node connectors (Bluesky, Tumblr). The worker never hard-codes a platform:

```
interface IConnector {
  ConnectorDescriptor Describe();                        // capabilities + required config schema
  Task<AuthState> IsAuthenticated(UserConnector cfg);
  Task<IReadOnlyList<Target>> ListTargets(UserConnector cfg);
  Task<DeliveryResult> Deliver(RenderedPost post, IReadOnlyList<MediaRef> media, UserConnector cfg);
}
```

Adding a platform = implement `IConnector` (in C# or Node) + register a service template row. This
is the uniform "add a platform" path the old docs only gestured at.

### 4.4 Authentication design (decision point)

Two viable approaches; **recommend A** for the smallest faithful port, with B as a hardening step:

- **A — oauth2-proxy at ingress (recommended first).** A reverse proxy performs the OIDC exchange
  against Keycloak and injects trusted identity headers (`X-Auth-Request-User`, etc.). App keeps the
  current "trust the header" model with almost no code change — but the trust boundary moves to a
  component we control and deploy everywhere. Works identically on Container Apps / K8s / compose.
- **B — in-app OIDC middleware.** Each API validates the bearer JWT (issuer, audience, signature via
  JWKS) itself. No proxy dependency; stricter. More code, but removes reliance on ingress config.

Either way: **fix API keys** to store a hash and compare the presented secret (FR-3), and drop the
current forgeable lookup.

### 4.5 Stateful-connector handling (Telegram)

MTProto login is interactive and stateful (code, then optional 2FA). The current in-memory
`StaticState` cache cannot survive multiple replicas. Target design:
- Persist session blobs to the object store (already done via `TelegramStore`).
- Route all operations for a given user session to a **single-writer telegram-worker** (consistent
  hashing on `userId`, or a per-user actor/lock) so concurrent replicas don't corrupt the session.
- Expose Telegram auth sub-flow through core-api → telegram-worker (request/response over the bus or
  internal HTTP), returning the "need code / need password / done" state machine the current
  `Telegram_DoLogin` already models.

### 4.6 Posting pipeline (state machine)

```
POST /posts ──► persist Root + Target(s) [Queued] ──► upload media ──► publish PostRequested(target)
                                                                              │
                              ┌───────────────────────────────────────────────┘
                              ▼
                    generate: render template → RenderedPost   [Generating → Ready]
                              │
                              ▼
                    deliver: IConnector.Deliver(...)           [Delivering → Delivered | Failed]
                              │  success → store externalId/url
                              │  transient failure → retry (backoff), then DLQ  [Failed]
                              ▼
                    update Target status; recompute Root status
                    (all Delivered → Delivered; mixed → PartiallyFailed; none → Failed)

GET /posts/{id} ──► aggregate Root + Target statuses
Scheduling: PostAt in future → delayed message / due-scan; else deliver immediately.
```

### 4.7 Data model (normalized, replaces the table soup)

Core tables (PostgreSQL):
- `users` (id, external_subject, created_at)
- `api_keys` (id, user_id, key_hash, prefix, created_at, revoked_at)
- `service_definitions` (id, name, config_schema, secure_config_schema, enabled) — seed data
- `user_connectors` (id, user_id, service_definition_id, display_name, config_json, enabled) —
  secrets live in the secret store, not here
- `templates` (id, user_id, title, markdown_body, created_at, updated_at)
- `external_triggers` (id, user_id, source_type, external_account, template_id, target_connector_id,
  notify_frequency_hrs, last_fired_at)
- `external_interests` (source_type, external_account, user_id) — inbound-webhook fan-out index
- `posts` (id, user_id, title, description, html_description, tags, media_manifest, post_at,
  root_status, created_at)
- `post_targets` (id, post_id, connector_id, platform, rendered_content, status, external_id,
  external_url, error, attempts, updated_at)
- `webhook_dedupe` (source, message_id, seen_at) — replaces the no-op `RequestLogger`

Object store: `media/{postId}/...`, `telegram/{userId}` session.
Secret store: `oidc-client-secret`, `twitch-client-secret`, `twitch-signature-secret`,
`telegram-api-id`, `telegram-api-hash`, and per-user `{connectorId}:{userId}` secrets.

---

## 5. Implementation plan (phased)

Each phase is independently shippable and demoable. Phases 0–2 deliver the first true end-to-end
post (something the system has never done).

### Phase 0 — Foundations & local stack
- New solution/repo layout: `core-api`, `post-api`, `posting-worker`, `telegram-worker`,
  `connectors-node`, shared `Domain`/`Contracts` libs, reused `adapters-secrets`.
- `docker-compose` local stack: PostgreSQL, MinIO, RabbitMQ, Keycloak, oauth2-proxy, OTel collector.
- Cross-cutting: config via env (12-factor), `ISecretsProvider`/`IObjectStore`/`IMessageBus`
  abstractions + one impl each, EF Core + migrations, health/readiness, OTel wiring, CI (build +
  test + container image publish).
- **Exit:** all services boot in compose, healthchecks green, one migration applied.

### Phase 1 — Identity, connectors, templates (port existing working behaviour)
- Auth middleware (oauth2-proxy path) + real API-key issue/list/revoke with hashing.
- Service definitions seed + `user_connectors` CRUD (secure fields → secret store).
- Templates CRUD (add the missing delete).
- **Exit:** feature-parity with today's *working* Profile/Services/Template endpoints, on the new stack.

### Phase 2 — Posting pipeline core (the missing heart)
- Post intake (persist root+targets, media upload), status API, message bus events.
- Templating engine (variables, conditionals, markdown → per-platform rendering).
- posting-worker: generate → deliver state machine, retries/backoff, DLQ, scheduling, idempotency.
- First connector end-to-end: **Discord webhook** (simplest) to prove the whole path.
- **Exit:** a scheduled post is rendered and delivered to Discord with queryable status.

### Phase 3 — Connectors
- **Telegram** via telegram-worker (single-writer session model) — auth sub-flow + deliver.
- **Twitch announcements** (Twitch.Net) — deliver.
- **Bluesky** (`@atproto/api`) and **Tumblr** (`tumblr.js`) in connectors-node behind the contract;
  finish Tumblr OAuth + implement Bluesky app-password auth.
- **Exit:** all five platforms deliver through the uniform contract.

### Phase 4 — External triggers
- Twitch EventSub: registration (channel resolve, subscribe), signature-validated callback,
  real webhook dedupe, fan-out via `external_interests`, `NotifyFrequencyHrs` throttling → enqueue
  a templated post.
- Generalize the trigger framework for future sources.
- **Exit:** a Twitch "stream online" event auto-posts to a user's chosen targets, rate-limited.

### Phase 5 — Hardening & deploy
- Security pass (auth option B if desired, secret hygiene, authz tests).
- Observability dashboards + alerting; load/scale test the worker (KEDA/scale rules).
- Deployment artifacts: Helm chart(s) and/or Container Apps + Terraform; managed-identity/OIDC to
  cloud backends where used.
- Rewrite `docs/` to match reality; retire the aspirational docs.
- **Exit:** production-deployable, observable, documented.

### Data migration (if carrying over existing users)
- Export `AvailableServices` seed → `service_definitions`.
- Migrate per-user `ConfigTable` rows → `user_connectors`; re-key secrets from `{serviceId}-{userId}`.
- Migrate `PostingTemplates`, `ExternalTriggers`, `ExternalInterests`.
- (No post/status history exists to migrate.)

---

## 6. Key risks & decisions to confirm

1. **Auth**: oauth2-proxy (A) vs in-app OIDC (B) — recommend A first, B as hardening. *Confirm.*
2. **Datastore**: PostgreSQL recommended; a document DB is possible if you prefer schemaless. *Confirm.*
3. **Message bus**: RabbitMQ default; NATS JetStream or Redis Streams are alternatives. *Confirm.*
4. **Telegram at scale**: the single-writer session model is the main architectural constraint;
   validate it meets throughput needs.
5. **Node ↔ C# connector transport**: internal HTTP (simple) vs the worker subscribing Node to
   platform sub-queues (more decoupled). Recommend internal HTTP first.
6. **API keys vs OIDC-only for M2M**: decide whether hashed API keys stay or machine access moves to
   OIDC client-credentials.
