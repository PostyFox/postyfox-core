# Follow-ups & deferred work

Tracked gaps after Phases 0–5. Ordered by importance.

## 🔴 KEY REQUIREMENT — Media delivery (deferred)

**Media attachments are a core product requirement and are NOT yet implemented end-to-end.**
Today the pipeline carries a **media manifest** (references to objects in the object store) all the
way through — `Post.MediaManifestJson` → `RenderedPost.Media` (`MediaRef[]`) → the connector
`deliver` payload — but **no connector uploads media**; all connectors deliver **text only**.

To complete it:
- **Intake/upload**: an endpoint (or pre-signed upload flow) to put media into the object store and
  return `MediaRef`s the client includes on the post. (`POST /api/posts` already accepts a `media`
  array of `MediaRef`.)
- **connectors-node** (Bluesky, Tumblr): fetch each `MediaRef` from the object store (needs
  object-store credentials wired into the Node service) and upload via `@atproto/api` blob upload /
  `tumblr.js` photo posts. The `deliver` contract already receives `media`; the handlers currently
  mark it `// TODO: media`.
- **Discord**: attach files (multipart) or embed image URLs on the webhook.
- **Telegram**: send media via `SendMediaAsync` / album (`WTelegramClient`) instead of `SendMessageAsync`.
- **Validation/limits**: per-platform media type/size/count limits in each connector descriptor.

This is the single largest remaining feature and should be the next work item.

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
  consider mTLS / network policy.
- **Auth option B** — currently the APIs trust the oauth2-proxy identity header; optionally add
  in-app JWT (JWKS) validation as defence-in-depth.
- **Autoscaling** — wire KEDA (K8s) / ACA scale rules on RabbitMQ queue depth for the worker.
