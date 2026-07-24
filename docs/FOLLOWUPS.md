# Follow-ups & deferred work

Residual media follow-ups (smaller):
- **Media normalization** — ✅ done. Images **and** video are resized/transcoded/format-converted to
  each platform's limits before upload, as a core shared component in both stacks (C#
  `IMediaProcessor`/`IMediaResolver` with ImageSharp + FFMpegCore; connectors-node `src/media/` with
  `sharp` + `fluent-ffmpeg`). Per-platform type/size/count limits are enforced from each connector's
  `MediaSpec` (Fediverse merges the instance's live limits); media that can't be brought within limits
  fails only its own target. Residuals: **documents** still pass through untouched; tune the
  Telegram document-vs-photo branch for non-image types; optionally **cache** normalized variants in
  the object store keyed by `(mediaKey, spec-hash)` to avoid recompute on retries / across the
  multi-target fan-out.
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
- **Secret backend for production** — ✅ done. Secrets go through the `adapters-secrets`
  `ISecretsProvider`, selected via `Secrets:Provider`: in-memory for dev, and BitWarden/VaultWarden
  (deployed default), Azure Key Vault, or Infisical for production. Residuals: the BitWarden adapter
  can't delete (its `DeleteSecretAsync` throws, so cleanup deletes are best-effort via
  `TryDeleteSecretAsync`), and secrets still need seeding (see platform-secret management above).
- **Internal transport hardening** — worker/core ↔ connectors-node uses a shared `X-Internal-Token`;
  consider network policy.
- **Auth option B** — currently the APIs trust the oauth2-proxy identity header; optionally add
  in-app JWT (JWKS) validation as defence-in-depth.
- **Autoscaling** — wire KEDA (K8s) / ACA scale rules on RabbitMQ queue depth for the worker.
