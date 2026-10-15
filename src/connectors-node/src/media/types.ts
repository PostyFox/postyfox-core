// Per-platform media constraints, mirrored from the C# `MediaSpec` contract. Undefined numeric
// fields mean "no constraint on that axis"; an empty `allowedMimeTypes` means "no format restriction".

export interface ImageSpec {
  maxWidth?: number;
  maxHeight?: number;
  /** Max encoded size in bytes. */
  maxBytes?: number;
  /** Output formats the platform accepts, e.g. ["image/jpeg", "image/png", "image/webp"]. */
  allowedMimeTypes: string[];
  /**
   * Width ÷ height bounds the platform rejects images outside of (issue #39). An image outside them
   * is padded or cropped per the author's {@link MediaFraming}, or refused when there is none.
   */
  minAspectRatio?: number;
  maxAspectRatio?: number;
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

/** A crop rectangle as fractions (0..1) of the auto-oriented image, origin top-left. */
export interface CropRegion {
  x: number;
  y: number;
  width: number;
  height: number;
}

/**
 * The author's choice for fitting an image into a platform's aspect-ratio range (issue #39),
 * mirrored from the C# `MediaFraming`. `background` is "#rrggbb", or null for transparent (white
 * where the output format has no alpha). `crop` is set for mode "crop".
 */
export interface MediaFraming {
  mode: "pad" | "crop";
  background?: string | null;
  crop?: CropRegion | null;
}

/** Result of normalising one media item: the upload-ready bytes and their (possibly new) MIME type. */
export interface NormalizedMedia {
  bytes: Buffer;
  contentType: string;
  /** Pixel dimensions of a still image's output; undefined for video or untouched input. */
  width?: number;
  height?: number;
}
