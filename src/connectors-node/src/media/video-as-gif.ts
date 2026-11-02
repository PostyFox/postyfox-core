import type { MediaStore } from "../media-store.js";
import type { Post, PostMedia } from "../types.js";
import type { ImageSpec } from "./types.js";
import { videoToGif } from "./video.js";

/** The per-target "Convert to GIF" post option for image-only platforms (issue #40). */
export interface VideoAsGifConfig {
  VideoAsGif?: string;
}

// Rewritten item → the stored video it stands for.
const sources = new WeakMap<PostMedia, PostMedia>();

/**
 * When the author picked "Convert to GIF" for this target, returns the post with each video
 * presented as the GIF it will become (`image/gif`, key ending `.gif`), so the connector's usual
 * GIF validation, naming and caps apply. Fetch the items with {@link fetchMedia} to get GIF bytes.
 */
export function withVideoAsGif(post: Post, config: VideoAsGifConfig): Post {
  if (config.VideoAsGif !== "true") return post;
  return {
    ...post,
    media: post.media.map((item) => {
      if (!item.contentType.toLowerCase().startsWith("video/")) return item;
      const gif: PostMedia = { ...item, contentType: "image/gif", key: item.key.replace(/(\.[^./]*)?$/, ".gif") };
      sources.set(gif, item);
      return gif;
    }),
  };
}

/** Fetches a media item's bytes, converting it to GIF when {@link withVideoAsGif} rewrote it. */
export async function fetchMedia(store: MediaStore, item: PostMedia, image: ImageSpec): Promise<Buffer> {
  const source = sources.get(item);
  if (!source) return store.fetch(item.container, item.key);
  return (await videoToGif(await store.fetch(source.container, source.key), image)).bytes;
}
