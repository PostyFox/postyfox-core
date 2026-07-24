import { test } from "node:test";
import assert from "node:assert/strict";
import { decideVideo, fitWithin } from "../../src/media/video.js";
import type { VideoSpec } from "../../src/media/types.js";

// The ffmpeg transcode itself needs the ffmpeg/ffprobe binaries (exercised at runtime); the pure
// fit/decision logic below is what determines whether/how a video is transformed.

function spec(over: Partial<VideoSpec> = {}): VideoSpec {
  return { allowedMimeTypes: ["video/mp4"], ...over };
}

test("fitWithin scales down preserving aspect with even dimensions", () => {
  assert.deepEqual(fitWithin(3840, 2160, 1920, 1080), [1920, 1080]);
  assert.deepEqual(fitWithin(1000, 1000, 1920, 1080), [1000, 1000]); // no enlargement
  const [w, h] = fitWithin(1921, 1081, 1920, 1080);
  assert.equal(w % 2, 0);
  assert.equal(h % 2, 0);
});

test("passthrough when within all limits and format allowed", () => {
  const d = decideVideo({ width: 1280, height: 720, durationSeconds: 30, bytes: 5_000_000 }, spec({ maxWidth: 1920, maxHeight: 1080, maxBytes: 50_000_000, maxDurationSeconds: 60 }), "video/mp4");
  assert.equal(d.action, "passthrough");
});

test("oversized dimensions trigger a transcode", () => {
  const d = decideVideo({ width: 3840, height: 2160, durationSeconds: 30, bytes: 5_000_000 }, spec({ maxWidth: 1920, maxHeight: 1080 }), "video/mp4");
  assert.equal(d.action, "transcode");
  assert.equal(d.targetWidth, 1920);
  assert.equal(d.targetHeight, 1080);
});

test("over-duration is a hard fail", () => {
  const d = decideVideo({ width: 1280, height: 720, durationSeconds: 120, bytes: 5_000_000 }, spec({ maxDurationSeconds: 60 }), "video/mp4");
  assert.equal(d.action, "fail");
  assert.ok(d.reason);
});

test("disallowed format converts to mp4", () => {
  const d = decideVideo({ width: 640, height: 480, durationSeconds: 10, bytes: 1_000_000 }, spec({ maxWidth: 1920, maxHeight: 1080 }), "video/x-matroska");
  assert.equal(d.action, "transcode");
  assert.equal(d.targetMime, "video/mp4");
});

test("oversized bytes trigger a transcode", () => {
  const d = decideVideo({ width: 1280, height: 720, durationSeconds: 30, bytes: 80_000_000 }, spec({ maxWidth: 1920, maxHeight: 1080, maxBytes: 50_000_000 }), "video/mp4");
  assert.equal(d.action, "transcode");
});

test("animated gif is converted to mp4", () => {
  const d = decideVideo({ width: 500, height: 500, durationSeconds: 3, bytes: 400_000 }, spec({ maxWidth: 1920, maxHeight: 1080 }), "image/gif");
  assert.equal(d.action, "transcode");
  assert.equal(d.targetMime, "video/mp4");
});
