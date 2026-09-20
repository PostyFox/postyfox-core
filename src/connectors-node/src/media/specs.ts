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

// Meta's documented Content Publishing API limits: JPEG only for images (PNG/WebP are converted),
// 8MB cap; video (Reels) up to 1GB/limited duration, MP4/MOV container. maxAttachments is the
// carousel cap (2–10 items).
export const INSTAGRAM_SPEC: MediaSpec = {
  image: { maxWidth: 1440, maxHeight: 1800, maxBytes: 8_388_608, allowedMimeTypes: ["image/jpeg"] },
  video: { maxWidth: 1920, maxHeight: 1080, maxBytes: 1_073_741_824, maxDurationSeconds: 900, allowedMimeTypes: ["video/mp4"] },
  maxAttachments: 10,
};

export const FURAFFINITY_SPEC: MediaSpec = {
  image: { maxBytes: 10_485_760, allowedMimeTypes: ["image/jpeg", "image/png", "image/gif"] },
  video: { maxBytes: 10_485_760, allowedMimeTypes: [] },
  maxAttachments: 1,
};

// Toyhou.se's upload form takes one image at a time, uniformly capped at 4MB (no larger allowance
// for GIFs, unlike FurAffinity).
export const TOYHOUSE_SPEC: MediaSpec = {
  image: { maxBytes: 4_194_304, allowedMimeTypes: ["image/jpeg", "image/png", "image/gif"] },
  video: { maxBytes: 4_194_304, allowedMimeTypes: [] },
  maxAttachments: 1,
};

// X's web composer takes up to four images per post. The sizes are conservative defaults: rettiwt-api
// sends each upload as a single unchunked request, so video is deliberately not offered.
export const X_SPEC: MediaSpec = {
  image: { maxWidth: 4096, maxHeight: 4096, maxBytes: 5_242_880, allowedMimeTypes: ["image/jpeg", "image/png", "image/webp"] },
  video: { maxBytes: 5_242_880, allowedMimeTypes: [] },
  maxAttachments: 4,
};

// Ko-fi's gallery upload takes up to 10 images per post (PostyBirb's batch size). No byte cap is
// documented, so none is asserted here.
export const KOFI_SPEC: MediaSpec = {
  image: { allowedMimeTypes: ["image/jpeg", "image/png", "image/gif"] },
  video: { allowedMimeTypes: [] },
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
  furaffinity: FURAFFINITY_SPEC,
  toyhouse: TOYHOUSE_SPEC,
  instagram: INSTAGRAM_SPEC,
  x: X_SPEC,
};

/**
 * Converts a platform's static `MediaSpec` into the `ConnectorLimits` shape the `limits` endpoint
 * reports. Used by connectors whose byte caps are fixed (Bluesky, Tumblr, FurAffinity) rather than
 * fetched live from an instance (contrast the Fediverse driver, which overlays `mergeLiveLimits` on
 * top of this). `maxContentLength` is left null — those platforms' character caps are already
 * surfaced from the static `ServiceDefinition` descriptor, not this endpoint.
 */
export function limitsFromSpec(spec: MediaSpec): ConnectorLimits {
  const mimes = [...spec.image.allowedMimeTypes, ...spec.video.allowedMimeTypes];
  return {
    maxContentLength: null,
    maxMediaAttachments: spec.maxAttachments ?? null,
    supportedMimeTypes: mimes.length > 0 ? mimes : null,
    imageSizeLimit: spec.image.maxBytes ?? null,
    videoSizeLimit: spec.video.maxBytes ?? null,
    imageMaxWidth: spec.image.maxWidth ?? null,
    imageMaxHeight: spec.image.maxHeight ?? null,
    videoMaxWidth: spec.video.maxWidth ?? null,
    videoMaxHeight: spec.video.maxHeight ?? null,
  };
}

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
      maxWidth: limits.imageMaxWidth ?? base.image.maxWidth,
      maxHeight: limits.imageMaxHeight ?? base.image.maxHeight,
      allowedMimeTypes: imageMimes && imageMimes.length > 0 ? imageMimes : base.image.allowedMimeTypes,
    },
    video: {
      ...base.video,
      maxBytes: limits.videoSizeLimit ?? base.video.maxBytes,
      maxWidth: limits.videoMaxWidth ?? base.video.maxWidth,
      maxHeight: limits.videoMaxHeight ?? base.video.maxHeight,
      allowedMimeTypes: videoMimes && videoMimes.length > 0 ? videoMimes : base.video.allowedMimeTypes,
    },
    maxAttachments: limits.maxMediaAttachments ?? base.maxAttachments,
  };
}
