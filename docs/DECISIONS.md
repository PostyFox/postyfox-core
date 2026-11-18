# Architecture Decision Records

Short records of the significant choices behind the PostyFox platform reimplementation. Each notes the
context, the decision, and the trade-off. See [`ARCHITECTURE.md`](./ARCHITECTURE.md) for how they
fit together.

---

## ADR-001: Cloud-agnostic containers over Azure Functions

**Context.** The legacy system was Azure-Functions-native (Table/Blob/Queue Storage, KeyVault,
EasyAuth, App Insights). The reimplementation must run anywhere (Container Apps / Kubernetes /
self-hosted).

**Decision.** Long-running containerised services (ASP.NET Core + a worker) with every cloud concern
behind an abstraction: `IAppDbContext`, `IObjectStore`, `IMessageBus`, `ISecretsProvider`, an auth edge,
and OpenTelemetry. Default implementations are portable (PostgreSQL, S3/MinIO/RustFS, RabbitMQ, oauth2-proxy,
OTLP); secrets use the external `adapters-secrets` library (in-memory for dev; HashiCorp Vault,
BitWarden/VaultWarden, Azure Key Vault, or Infisical for deployments) rather than a bespoke store.

**Trade-off.** We manage more moving parts than a serverless model, but gain portability and local
dev parity (`docker compose up`).

---

## ADR-002: PostgreSQL as the system of record

**Context.** Legacy used Azure Table Storage (denormalised key-value). We need a portable store with
relational integrity for the post → target → status graph.

**Decision.** PostgreSQL via EF Core (Npgsql), with a normalised schema and migrations.

**Trade-off.** A schema to migrate vs. schemaless flexibility; worth it for query power and
integrity. `Application` depends on EF Core Core (`IAppDbContext` exposes `DbSet<>`) to avoid a
repository explosion, a deliberate, minor purity compromise.

---

## ADR-003: RabbitMQ as the message bus (not Kafka)

**Status.** Accepted; the scheduling and retry mechanism is superseded by ADR-008.

**Context.** The posting pipeline is task-queue-shaped: per-message ack, per-target retries with
backoff, dead-lettering, and delayed/scheduled delivery.

**Decision.** RabbitMQ, using the `x-delayed-message` exchange for both scheduling and retry backoff;
per-queue dead-letter queues.

**Trade-off.** Kafka's partitioned log excels at high-throughput streaming/replay but makes
per-message retry/DLQ/delay awkward. RabbitMQ fits the workload directly. The delayed-message plugin
is an in-memory scheduler; a durable due-scan is a future item for very long horizons.

---

## ADR-004: oauth2-proxy edge + retained hashed API keys

**Context.** The legacy trusted platform-injected identity headers (EasyAuth). We need equivalent
OIDC auth that is portable, plus machine-to-machine access.

**Decision.** An oauth2-proxy edge performs the OIDC exchange (Keycloak) and forwards the validated
token to the APIs as `Authorization: Bearer`; the APIs **re-validate it in-app** against the realm's
JWKS rather than trusting an injected identity header. An internal nginx gateway path-routes the edge
to core-api/post-api. **API keys are retained** for external connectivity, stored as PBKDF2 hashes
(never in clear) and verified in constant time. There is no DevMode bypass in any deployment. Local
dev runs the same Keycloak edge.

**Trade-off.** The APIs depend on OIDC config (issuer/JWKS) being correct, and local dev now requires
Keycloak to be up (no header/DevMode shortcut), but the trust boundary no longer relies on a
spoofable header, and local mirrors production. Fixing the legacy's non-comparing API-key check was a
requirement.

---

## ADR-005: Uniform connector contract; Node only where its libraries win

**Context.** Platform SDK quality varies by language. Bluesky's first-party client (`@atproto/api`),
Tumblr's official client (`tumblr.js`), `megalodon` for the Fediverse and `rettiwt-api` for X are
Node; .NET equivalents are weaker or missing. The web-form connectors (FurAffinity, Toyhouse, Ko-fi)
follow PostyBirb's TypeScript drivers, and media processing has mature Node tooling (`sharp`,
`fluent-ffmpeg`).

**Decision.** One `IConnector` contract for all platforms. Discord and Telegram run in-process in
C#; every other platform runs in a small **connectors-node** service behind the same contract over
internal HTTP, called via a C# `HttpConnector` adapter. The Node service is stateless: the C# side
passes resolved config + secret in each request. New platforms default to Node.

**Trade-off.** A second runtime and an internal hop, in exchange for using the best library per
platform and keeping a single extension seam. Media normalisation exists once per stack (ImageSharp
and FFMpegCore in C#, `sharp` and `fluent-ffmpeg` in Node). Internal calls are secured with a shared
token; mTLS is a possible follow-up but unnecessary complexity for a private internal service.

---

## ADR-006: Telegram via MTProto user account (not the Bot API)

**Context.** The legacy posted to Telegram **as the user** via MTProto (WTelegramClient), listing
the user's own chats/channels. The Bot API is simpler and stateless but requires a bot to be added
to each channel and changes the product behaviour.

**Decision.** Keep MTProto user-account posting via WTelegramClient, matching the completed legacy
behaviour. Sessions persist to the object store; MTProto work sits behind `ITelegramGateway` so the
connector and login flow are testable with a fake.

**Trade-off.** MTProto login is interactive and session-stateful, so Telegram operations for a user
must be routed to a single instance (consistent hashing / dedicated worker). The live gateway needs
real credentials and is therefore not covered by automated tests. Only the logic around the seam is.

---

## ADR-007: Source-agnostic external triggers

**Context.** The only concrete legacy trigger source (Twitch) was descoped. External triggers were
still wanted.

**Decision.** A generic trigger framework: `ITriggerSource` encapsulates a source's signature scheme
and payload shape; a generic HMAC-signed webhook source ships built-in. Inbound events are
signature-verified, deduped by message id, and fanned out to matching triggers (each throttled by
`NotifyFrequencyHrs`), reusing the normal posting pipeline. New sources plug in by implementing the
contract.

**Trade-off.** No real third-party source is wired yet, but the engine is complete and tested, and
adding one (e.g. a future streaming platform) is a single implementation.

---

## ADR-008: Database-polled scheduling and TTL retry queues (no delayed-message plugin)

**Context.** ADR-003 used the `rabbitmq_delayed_message_exchange` plugin for both scheduled posts and
retry backoff. The plugin is unmaintained, keeps pending messages in Mnesia (so schedules are not
durable in the usual sense), and does not run on RabbitMQ 4.3+. A plain per-message-TTL queue is no
substitute for scheduling: RabbitMQ only expires the head of a queue, so a long delay published
before a short one blocks it.

**Decision.** Scheduling moves off the bus. Intake publishes `generate` only for immediate posts;
`PostSchedulerSweeper` in the worker runs `PostSchedulerService` every `Pipeline:SchedulerPollSeconds`
(default 15), claims targets whose persisted `PostAt` is due in batches of
`Pipeline:SchedulerBatchSize`, and publishes them. Post automations (`PostAutomationSweeper`) use the same
pattern against `DueAt`. Retry backoff uses a `{queue}.retry` holding queue per queue:
the message carries an AMQP `expiration` and dead-letters back into the main queue when it expires.
The exchange is a plain `direct` exchange and the broker needs no plugins beyond the stock image.

**Trade-off.** Schedules are as durable as the database and the broker can track upstream releases
freely, at the cost of up to one poll interval of latency and a polling query. Head-of-line blocking
in the retry queues is acceptable because only delivery retries use them, with a narrow, bounded
backoff.

---

## ADR-009: Bundled HashiCorp Vault with automatic unsealing

**Context.** ADR-001 put secrets behind `ISecretsProvider`. Self-hosted deployments (the dev compose
stack and the production Helm chart) need a persistent store that comes up without an operator
typing unseal keys after every restart.

**Decision.** Both bundle HashiCorp Vault (file backend, Shamir seal) with a `vault-init` sidecar. On
first boot it initialises Vault and writes the unseal keys and root token to the `vaultkeys` volume;
afterwards it unseals Vault whenever it finds it sealed. Once unsealed it provisions a KV v2 mount, a
policy scoped to `VAULT_BASE_PATH` and an AppRole whose RoleId/SecretId are pinned to values the app
already holds, so no token is handed over at runtime. `HashiCorpVault` is the default provider for
these stacks; Azure Key Vault remains the default for Terraform/ACA.

**Trade-off.** Storing the unseal keys beside the server gives up Shamir key-splitting for unattended
restarts: anyone with the `vaultkeys` volume can unseal Vault. That volume must be backed up and
tightly restricted. A Transit or cloud-KMS auto-unseal seal is the stronger option if the deployment
can depend on one.

---

## ADR-010: Paired browser sessions for platforms with no posting API

**Context.** FurAffinity, Toyhouse, Ko-fi, Artconomy and X offer no usable public posting API.
Automating their login means handling passwords, 2FA and Cloudflare challenges, and is fragile.

**Decision.** The user logs in to the site in their own browser and the PostyFox Connect extension
hands the session cookies to core (`/api/connectors/cookie-pairing/pair`, authorised by the user's
PostyFox session; a five-minute one-use token is the fallback). Site knowledge lives on the server in
each connector's `CookiePairingSpec`: which cookies, which login page. Only the declared cookie names
are read or stored, in the secret store. An admin setting decides per platform whether delivery
replays the pairing browser's User-Agent (needed where Cloudflare binds clearance to it). Connectors
never attempt automated login or challenge solving; an expired session means re-pairing.

**Trade-off.** No credentials are stored and a new site needs no extension release, but sessions
expire and must be re-paired, and the connectors depend on undocumented site forms and internal APIs
that can change without notice. Automated use breaks X's terms and can get the account suspended,
and Ko-fi restricts automation; the connect UI warns users before they connect.

---

## ADR-011: Media normalised per target at delivery time

**Context.** One uploaded asset fans out to platforms with different limits on dimensions, bytes,
duration, formats and attachment count. Some only take images; some need a particular aspect ratio.

**Decision.** The object store keeps the original. Each connector normalises it against its
platform's `MediaSpec` at delivery time through one shared building block per stack
(`IMediaResolver`/`IMediaProcessor` with ImageSharp and FFMpegCore in C#, `src/media/` with `sharp` and
`fluent-ffmpeg` in Node), merging live per-instance limits where the platform reports them. Media that
cannot be brought within limits fails only its own target. Converting video to a silent GIF for
image-only platforms is opt-in per target (`VideoAsGif`), and out-of-range aspect ratios need the
author's pad or crop choice rather than a silent crop.

**Trade-off.** Normalisation is recomputed for every target and every retry (a variant cache is a
follow-up), and both runtime images carry `ffmpeg`. In exchange each platform gets the best version
the original allows, and one platform's limits never degrade another's copy.

---

## ADR-012: Account delegation by swapping the request's user identity

**Context.** Artists want an assistant or manager to post on their behalf without sharing a login
(issue #409).

**Decision.** An owner invites someone by email; accepting creates an `AccountMember` row,
cross-checked against the invitee's own OIDC email. The client sends `X-Act-As: <owner id>` and
`ActAsClaimsTransformation` swaps `NameIdentifier` to the owner's id when a membership exists. Every
endpoint already reads the current user from that claim, so nothing downstream knows delegation
exists. An invalid header is dropped, not rejected.

**Trade-off.** One small seam instead of permission checks in every service, but access is
all-or-nothing: a member can do anything the owner can, including API keys. A permission subset was
considered and deferred.

---

## ADR-013: Production on Kubernetes via Helm, dev on docker-compose

**Context.** Dev runs on a single host reached by a self-hosted runner. Production needs replicas,
rolling upgrades and rollback on an existing cluster.

**Decision.** Dev stays on docker-compose (`docker-compose.server.yml` + `docker-compose.dev.yml`),
deployed automatically after each main build. Production is the `postyfox` Helm release in the
`postyfox` namespace, deployed manually with approval through `helm upgrade --install --atomic`. Every
backing service in the chart is individually toggleable. The frontend is its own Helm release in the
same namespace, and core's gateway proxies `/` to it, falling back to the maintenance page if it is
absent. Both shapes consume the same published images, and third-party image tags are pinned
identically across compose and Helm.

**Trade-off.** Two deployment definitions to keep in step (Vault bootstrap, gateway config and image
pins are duplicated), in exchange for a cheap dev host and a production target with real rollback.

---

## ADR-014: Instagram media served from a public core-api route

**Context.** Instagram's Content Publishing API fetches media by public URL; it does not accept
uploads. The object store is private, and presigned URLs would require the store itself to be
reachable from the internet.

**Decision.** connectors-node writes normalised bytes to the `instagram-staging` container under a
random `{uuid}.{ext}` key. core-api serves them anonymously at `/api/media/staged/{key}`, oauth2-proxy
skips auth for exactly that pattern, and the object is deleted once the attempt finishes.

**Trade-off.** Media is briefly public behind an unguessable key, and Instagram delivery needs
`PUBLIC_BASE_URL` to be internet-reachable (a local stack needs a tunnel). The object store stays
private and no second public endpoint is needed.

---

## ADR-015: Expiring tokens refreshed by a sweeper and before use

**Context.** Instagram's tokens last about 60 days, SoFurry's lifetime is undocumented, and
DeviantArt's access tokens last one hour with a three-month refresh token.

**Decision.** A connector with expiring tokens implements `IRefreshableConnector` and stores
`ExpiresAt` in its secret. `ConnectorTokenRefreshSweeper` in the worker refreshes any token within
`ConnectorRefresh:RefreshWithinDays`, and `RefreshIfExpiredAsync` refreshes a token that is expired or
about to expire before core uses the connector. Any rotated refresh token is persisted. A refused
refresh leaves the secret in place, and the user must reconnect.

**Trade-off.** Two refresh paths to reason about, but a new expiring-token platform needs only its
connector-level `refresh`, and short-lived tokens work without user action.

---

## ADR-016: Valkey for server-side oauth2-proxy sessions

**Context.** With Keycloak role claims present, oauth2-proxy's cookie session exceeds the browser's
4 KB cookie limit. Redis moved to a non-open-source licence after 7.x.

**Decision.** oauth2-proxy keeps sessions server-side in a bundled Valkey instance (wire-compatible
with Redis), with persistence disabled. The browser holds only an opaque session cookie.

**Trade-off.** One more service to run, and restarting it signs everyone out. No application data
lives there, and tokens stay out of the browser.

---

## ADR-017: Terms of service enforced by API middleware

**Context.** The operator must be able to require acceptance of terms before the service is used,
and require it again when the terms change (issue #417).

**Decision.** Admins publish versioned terms (`PUT /api/admin/terms`); blank content turns them off.
`TermsOfServiceMiddleware` on both APIs returns 403 with `terms_not_accepted` for any authenticated
request, OIDC or API key, until the caller has accepted the current version, and with
`owner_terms_not_accepted` when the account being acted as has not. Health, version, webhooks, the
terms endpoints and the admin terms route are exempt, so an admin can correct a bad publish. Posts
and triggers already scheduled keep delivering.

**Trade-off.** One server-side gate that clients cannot bypass, at the cost of every client needing to
handle the 403 code. The frontend routes it to its acceptance page.

---

## ADR-018: Central Data Prepper owns OpenSearch access

**Context.** Several environments ship telemetry to one shared OpenSearch cluster. Giving every stack
OpenSearch credentials and certificates spreads secrets and schema knowledge.

**Decision.** Each environment runs a local OpenTelemetry collector that batches, compresses, filters
health checks and optionally tail-samples, then forwards OTLP to one central Data Prepper. Only Data
Prepper holds OpenSearch credentials and owns the index templates and service-map pipelines.

**Trade-off.** One more central component that must be up for telemetry to land, but app stacks hold
no OpenSearch secrets, and volume is cut before it crosses the network.

