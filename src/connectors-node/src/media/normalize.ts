import { normalizeImage } from "./image.js";
import { normalizeVideo } from "./video.js";
import type { MediaSpec, NormalizedMedia } from "./types.js";

/**
 * The single media-normalization entry point every connector routes fetched bytes through before
 * upload. Dispatches by content type: still raster images to the image path, video and animated GIF
 * to the ffmpeg path, and anything else (documents, unknown) passes through unchanged.
 */
export async function normalizeMedia(
  bytes: Buffer,
  contentType: string,
  spec: MediaSpec,
): Promise<NormalizedMedia> {
  const type = (contentType ?? "").toLowerCase();

  if (type === "image/jpeg" || type === "image/jpg" || type === "image/png" || type === "image/webp") {
    return normalizeImage(bytes, contentType, spec.image);
  }
  if (type === "image/gif" || type.startsWith("video/")) {
    return normalizeVideo(bytes, contentType, spec.video);
  }
  return { bytes, contentType };
}
