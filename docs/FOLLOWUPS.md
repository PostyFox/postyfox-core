# Follow-ups & deferred work

Residual media follow-ups (smaller):
- **Video / documents** — only images are exercised end-to-end; verify/tune non-image media per
  platform (content-type handling, Telegram document vs photo).
- **Per-platform limits** — enforce type/size/count limits from each connector's descriptor.
- **Pre-signed upload** — direct-to-object-store uploads (pre-signed URLs) instead of proxying
  bytes through `POST /api/media`, for large files.

## Other follow-ups

- **Telegram MTProto statefulness** — login is interactive and session-based; route a user's
  Telegram ops to a single instance (consistent hashing / dedicated telegram-worker). The live
  MTProto gateway is not covered by automated tests (needs real credentials); the logic around the
  `ITelegramGateway` seam is.
- **Platform-secret management** — no endpoint yet to set platform-level secrets (Telegram
  `TelegramApiID`/`TelegramApiHash`, trigger `trigger-{source}-signing`). Seed the secret store
  directly for now; add an admin surface.
- **Durable scheduling** — scheduled posts use the RabbitMQ delayed-message plugin (in-memory).
  Add a durable scheduler / due-scan for very long horizons.
- **Secret backend for production** — the default `ISecretStore` is AES-GCM-in-Postgres. Wire a
  Vault / cloud KMS implementation for production; Helm/ACA reference secrets but don't provision a
  manager.
- **Internal transport hardening** — worker/core ↔ connectors-node uses a shared `X-Internal-Token`;
  consider network policy.
- **Auth option B** — currently the APIs trust the oauth2-proxy identity header; optionally add
  in-app JWT (JWKS) validation as defence-in-depth.
- **Autoscaling** — wire KEDA (K8s) / ACA scale rules on RabbitMQ queue depth for the worker.
