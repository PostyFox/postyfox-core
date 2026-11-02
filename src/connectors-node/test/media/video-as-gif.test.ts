import { test } from "node:test";
import assert from "node:assert/strict";
import { fetchMedia, withVideoAsGif } from "../../src/media/video-as-gif.js";
import { gifRefusal, MAX_GIF_SECONDS } from "../../src/media/video.js";
import type { MediaStore } from "../../src/media-store.js";
import type { Post } from "../../src/types.js";

const post: Post = {
  title: "t",
  body: "b",
  tags: [],
  media: [
    { container: "media", key: "u/1/clip.mp4", contentType: "video/mp4", alt: null, isDefault: true },
    { container: "media", key: "u/2/still.png", contentType: "image/png", alt: null },
  ],
};

function store(): MediaStore & { keys: string[] } {
  const keys: string[] = [];
  return {
    keys,
    async fetch(_container, key) {
      keys.push(key);
      return Buffer.from(key);
    },
    async put() {},
    async delete() {},
  };
}

test("leaves the post alone without the option", () => {
  assert.equal(withVideoAsGif(post, {}), post);
  assert.equal(withVideoAsGif(post, { VideoAsGif: "false" }), post);
});

test("presents each video as the GIF it will become", () => {
  const rewritten = withVideoAsGif(post, { VideoAsGif: "true" });
  assert.deepEqual(rewritten.media[0], { ...post.media[0], contentType: "image/gif", key: "u/1/clip.gif" });
  assert.equal(rewritten.media[1], post.media[1]);
});

test("fetches untouched items as stored", async () => {
  const s = store();
  const bytes = await fetchMedia(s, post.media[1], { allowedMimeTypes: [] });
  assert.equal(bytes.toString(), "u/2/still.png");
});

test("converts a rewritten item from the stored video", async () => {
  const s = store();
  const gif = withVideoAsGif(post, { VideoAsGif: "true" }).media[0];
  // The fake bytes aren't a real video, so the conversion itself fails, after fetching the source.
  await assert.rejects(fetchMedia(s, gif, { allowedMimeTypes: [] }));
  assert.deepEqual(s.keys, ["u/1/clip.mp4"]);
});

test("refuses videos too long for a GIF", () => {
  assert.equal(gifRefusal(MAX_GIF_SECONDS), undefined);
  assert.match(gifRefusal(45) ?? "", /45s but conversion to GIF allows at most 30s/);
});
