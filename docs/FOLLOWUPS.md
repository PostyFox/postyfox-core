# Follow-ups & deferred work

## Media

- Media normalisation: documents still pass through untouched; tune the Telegram document-vs-photo
  branch for non-image types; optionally cache normalised variants in the object store keyed by
  `(mediaKey, spec-hash)` to avoid recompute on retries and across the multi-target fan-out.
- Video gaps: X takes no video or alt text (`rettiwt-api` upload is unchunked and has no alt-text
  field). Video and video-as-GIF delivery is not yet verified against the live platforms.
- Pre-signed upload: direct-to-object-store uploads (pre-signed URLs) instead of proxying bytes
  through `POST /api/media`, for large files.

## Other follow-ups

- Telegram MTProto statefulness: login is interactive and session-based; route a user's
  Telegram ops to a single instance (consistent hashing / dedicated telegram-worker). The live
  MTProto gateway is not covered by automated tests (needs real credentials); the logic around the
  `ITelegramGateway` seam is.
- Platform-secret management: connector operational secrets are managed at `/admin`; trigger
  signing keys (`trigger-{sourceType}-signing`) still need seeding into the secret store directly.
- Internal transport hardening: worker/core ↔ connectors-node uses a shared `X-Internal-Token`;
  consider network policy.
- Autoscaling: wire KEDA (K8s) / ACA scale rules on RabbitMQ queue depth for the worker.
- Account delegation (issue #409): expired invites are computed at read time (`IsExpired`), not
  purged. The table is low-volume, so this is acceptable for now, but a retention sweep would keep it
  tidy. Access is all-or-nothing (a member can do everything the owner can); a permission subset
  (e.g. posting only, no API keys/billing) was considered and deferred, see the ticket discussion.
- Unverified connectors: Ko-fi, Artconomy, X and the FurAffinity journal flow follow PostyBirb or the
  site's source and have not been exercised against the live sites.
