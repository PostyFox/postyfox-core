import type { ConnectorLimits } from "../types.js";
import type { MediaSpec } from "./types.js";

// Conservative per-platform defaults, chosen to sit safely inside each platform's documented caps.
// The Fediverse spec is always overlaid with the instance's live limits (see `mergeLiveLimits`).

export const BLUESKY_SPEC: MediaSpec = {
  image: { maxWidth: 2000, maxHeight: 2000, maxBytes: 976_560, allowedMimeTypes: ["image/jpeg", "image/png", "image/webp"] },
  video: { maxWidth: 1920, maxHeight: 1080, maxBytes: 52_428_800, maxDurationSeconds: 60, allowedMimeTypes: ["video/mp4"] },
  maxAttachments: 4,
};

export const TUMBLR_SPEC: MediaSpec = {
  image: { maxWidth: 2560, maxHeight: 2560, maxBytes: 20_971_520, allowedMimeTypes: ["image/jpeg", "image/png", "image/webp", "image/gif"] },
  video: { maxWidth: 1920, maxHeight: 1080, maxBytes: 524_288_000, maxDurationSeconds: 300, allowedMimeTypes: ["video/mp4"] },
  maxAttachments: 10,
};

/** Fallback used before an instance's live limits are known (or when it reports none). */
export const FEDIVERSE_SPEC: MediaSpec = {
  image: { maxWidth: 2048, maxHeight: 2048, maxBytes: 8_388_608, allowedMimeTypes: ["image/jpeg", "image/png", "image/webp", "image/gif"] },
  video: { maxWidth: 1920, maxHeight: 1080, maxBytes: 41_943_040, allowedMimeTypes: ["video/mp4"] },
  maxAttachments: 4,
};

export const MEDIA_SPECS: Record<string, MediaSpec> = {
  bluesky: BLUESKY_SPEC,
  tumblr: TUMBLR_SPEC,
};

/**
 * Overlays a connector's live, per-instance limits (e.g. a Fediverse instance's reported caps) onto
 * a static base spec. A live value wins only when reported; otherwise the base value is kept.
 */
export function mergeLiveLimits(base: MediaSpec, limits: ConnectorLimits): MediaSpec {
  const mimes = limits.supportedMimeTypes ?? undefined;
  const imageMimes = mimes?.filter((m) => m.toLowerCase().startsWith("image/"));
  const videoMimes = mimes?.filter((m) => m.toLowerCase().startsWith("video/"));
  return {
    image: {
      ...base.image,
      maxBytes: limits.imageSizeLimit ?? base.image.maxBytes,
      allowedMimeTypes: imageMimes && imageMimes.length > 0 ? imageMimes : base.image.allowedMimeTypes,
    },
    video: {
      ...base.video,
      maxBytes: limits.videoSizeLimit ?? base.video.maxBytes,
      allowedMimeTypes: videoMimes && videoMimes.length > 0 ? videoMimes : base.video.allowedMimeTypes,
    },
    maxAttachments: limits.maxMediaAttachments ?? base.maxAttachments,
  };
}
