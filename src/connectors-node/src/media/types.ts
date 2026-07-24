// Per-platform media constraints, mirrored from the C# `MediaSpec` contract. Undefined numeric
// fields mean "no constraint on that axis"; an empty `allowedMimeTypes` means "no format restriction".

export interface ImageSpec {
  maxWidth?: number;
  maxHeight?: number;
  /** Max encoded size in bytes. */
  maxBytes?: number;
  /** Output formats the platform accepts, e.g. ["image/jpeg", "image/png", "image/webp"]. */
  allowedMimeTypes: string[];
}

export interface VideoSpec {
  maxWidth?: number;
  maxHeight?: number;
  maxBytes?: number;
  maxDurationSeconds?: number;
  allowedMimeTypes: string[];
}

export interface MediaSpec {
  image: ImageSpec;
  video: VideoSpec;
  /** Max number of attachments the platform accepts on one post. */
  maxAttachments?: number;
}

/** Result of normalizing one media item: the upload-ready bytes and their (possibly new) MIME type. */
export interface NormalizedMedia {
  bytes: Buffer;
  contentType: string;
}
