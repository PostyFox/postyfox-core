# Architecture Decision Records

Short records of the significant choices behind the PostyFox platform reimplementation. Each notes the
context, the decision, and the trade-off. See [`ARCHITECTURE.md`](./ARCHITECTURE.md) for how they
fit together.

---

## ADR-001 — Cloud-agnostic containers over Azure Functions

**Context.** The legacy system was Azure-Functions-native (Table/Blob/Queue Storage, KeyVault,
EasyAuth, App Insights). The reimplementation must run anywhere (Container Apps / Kubernetes /
self-hosted).

**Decision.** Long-running containerised services (ASP.NET Core + a worker) with every cloud concern
behind an abstraction: `IAppDbContext`, `IObjectStore`, `IMessageBus`, `ISecretStore`, an auth edge,
and OpenTelemetry. Default implementations are portable (PostgreSQL, S3/MinIO/RustFS, RabbitMQ, encrypted-
in-DB secrets, oauth2-proxy, OTLP).

**Trade-off.** We manage more moving parts than a serverless model, but gain portability and local
dev parity (`docker compose up`).

---

## ADR-002 — PostgreSQL as the system of record

**Context.** Legacy used Azure Table Storage (denormalised key-value). We need a portable store with
relational integrity for the post → target → status graph.

**Decision.** PostgreSQL via EF Core (Npgsql), with a normalised schema and migrations.

**Trade-off.** A schema to migrate vs. schemaless flexibility; worth it for query power and
integrity. `Application` depends on EF Core Core (`IAppDbContext` exposes `DbSet<>`) to avoid a
repository explosion — a deliberate, minor purity compromise.

---

## ADR-003 — RabbitMQ as the message bus (not Kafka)

**Context.** The posting pipeline is task-queue-shaped: per-message ack, per-target retries with
backoff, dead-lettering, and delayed/scheduled delivery.

**Decision.** RabbitMQ, using the `x-delayed-message` exchange for both scheduling and retry backoff;
per-queue dead-letter queues.

**Trade-off.** Kafka's partitioned log excels at high-throughput streaming/replay but makes
per-message retry/DLQ/delay awkward. RabbitMQ fits the workload directly. The delayed-message plugin
is an in-memory scheduler; a durable due-scan is a future item for very long horizons.

---

## ADR-004 — oauth2-proxy edge + retained hashed API keys

**Context.** The legacy trusted platform-injected identity headers (EasyAuth). We need equivalent
OIDC auth that is portable, plus machine-to-machine access.

**Decision.** An oauth2-proxy ingress performs the OIDC exchange (Keycloak) and injects a trusted
identity header the APIs consume — preserving the header-trust model while moving the trust boundary
to a component we deploy everywhere. **API keys are retained** for external connectivity, stored as
PBKDF2 hashes (never in clear) and verified in constant time.

**Trade-off.** APIs trust an upstream header, so the edge must be correctly fronting them (or use the
`auth` profile locally / DevMode for dev). In-app JWT validation is a viable alternative hardening
step. Fixing the legacy's non-comparing API-key check was a requirement.

---

## ADR-005 — Uniform connector contract; Node only where its libraries win

**Context.** Platform SDK quality varies by language. Bluesky's first-party client (`@atproto/api`)
and Tumblr's official client (`tumblr.js`) are Node; .NET equivalents are weaker/unmaintained.

**Decision.** One `IConnector` contract for all platforms. Discord and Telegram run in-process in
C#; Bluesky and Tumblr run in a small **connectors-node** service behind the same contract over
internal HTTP, called via a C# `HttpConnector` adapter. The Node service is stateless — the C# side
passes resolved config + secret in each request.

**Trade-off.** A second runtime and an internal hop for two platforms, in exchange for using the best
library per platform and keeping a single extension seam. Internal calls are secured with a shared
token; a follow-up could move to mTLS - however mTLS brings its own operational complexity and is not strictly 
necessary for a private internal service.

---

## ADR-006 — Telegram via MTProto user account (not the Bot API)

**Context.** The legacy posted to Telegram **as the user** via MTProto (WTelegramClient), listing
the user's own chats/channels. The Bot API is simpler and stateless but requires a bot to be added
to each channel and changes the product behaviour.

**Decision.** Keep MTProto user-account posting via WTelegramClient, matching the completed legacy
behaviour. Sessions persist to the object store; MTProto work sits behind `ITelegramGateway` so the
connector and login flow are testable with a fake.

**Trade-off.** MTProto login is interactive and session-stateful, so Telegram operations for a user
must be routed to a single instance (consistent hashing / dedicated worker). The live gateway needs
real credentials and is therefore not covered by automated tests — only the logic around the seam is.

---

## ADR-007 — Source-agnostic external triggers

**Context.** The only concrete legacy trigger source (Twitch) was descoped. External triggers were
still wanted.

**Decision.** A generic trigger framework: `ITriggerSource` encapsulates a source's signature scheme
and payload shape; a generic HMAC-signed webhook source ships built-in. Inbound events are
signature-verified, deduped by message id, and fanned out to matching triggers — each throttled by
`NotifyFrequencyHrs` — reusing the normal posting pipeline. New sources plug in by implementing the
contract.

**Trade-off.** No real third-party source is wired yet, but the engine is complete and tested, and
adding one (e.g. a future streaming platform) is a single implementation.
