import { normalizeImage } from "./image.js";
import { normalizeVideo } from "./video.js";
import type { MediaFraming, MediaSpec, NormalizedMedia } from "./types.js";

/**
 * The single media-normalization entry point every connector routes fetched bytes through before
 * upload. Dispatches by content type: still raster images to the image path, video and animated GIF
 * to the ffmpeg path, and anything else (documents, unknown) passes through unchanged. `framing` is
 * the author's aspect-ratio choice for a still image (issue #39); video ignores it.
 */
export async function normalizeMedia(
  bytes: Buffer,
  contentType: string,
  spec: MediaSpec,
  framing?: MediaFraming | null,
): Promise<NormalizedMedia> {
  const type = (contentType ?? "").toLowerCase();

  if (type === "image/jpeg" || type === "image/jpg" || type === "image/png" || type === "image/webp") {
    return normalizeImage(bytes, contentType, spec.image, framing);
  }
  if (type === "image/gif" || type.startsWith("video/")) {
    return normalizeVideo(bytes, contentType, spec.video);
  }
  return { bytes, contentType };
}
