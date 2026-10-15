import { test } from "node:test";
import assert from "node:assert/strict";
import sharp from "sharp";
import { normalizeImage } from "../../src/media/image.js";
import type { ImageSpec } from "../../src/media/types.js";

/** A noisy raw-RGB JPEG so it can't be trivially compressed to a handful of bytes. */
async function noisyJpeg(w: number, h: number): Promise<Buffer> {
  const raw = Buffer.alloc(w * h * 3);
  for (let i = 0; i < raw.length; i++) raw[i] = (i * 37 + ((i / 7) | 0)) & 0xff;
  return sharp(raw, { raw: { width: w, height: h, channels: 3 } }).jpeg({ quality: 95 }).toBuffer();
}

function pngWithAlpha(w: number, h: number): Promise<Buffer> {
  return sharp({ create: { width: w, height: h, channels: 4, background: { r: 10, g: 20, b: 30, alpha: 0.5 } } })
    .png()
    .toBuffer();
}

function spec(over: Partial<ImageSpec> = {}): ImageSpec {
  return { allowedMimeTypes: ["image/jpeg", "image/png", "image/webp"], ...over };
}

test("downscales an oversized image within dimension limits, preserving aspect", async () => {
  const src = await noisyJpeg(4000, 3000);
  const out = await normalizeImage(src, "image/jpeg", spec({ maxWidth: 2000, maxHeight: 2000 }));
  const meta = await sharp(out.bytes).metadata();
  assert.equal(meta.width, 2000);
  assert.equal(meta.height, 1500);
});

test("does not enlarge a small image", async () => {
  const src = await sharp({ create: { width: 100, height: 80, channels: 3, background: "#123456" } }).png().toBuffer();
  const out = await normalizeImage(src, "image/png", spec({ maxWidth: 2000, maxHeight: 2000, allowedMimeTypes: ["image/png"] }));
  const meta = await sharp(out.bytes).metadata();
  assert.equal(meta.width, 100);
  assert.equal(meta.height, 80);
  assert.equal(out.contentType, "image/png");
});

test("undecodable input passes through unchanged", async () => {
  const garbage = Buffer.from("this is not an image");
  const out = await normalizeImage(garbage, "image/png", spec({ maxBytes: 5 }));
  assert.equal(out.bytes, garbage);
  assert.equal(out.contentType, "image/png");
});

test("converts to an allowed format when the source format is not accepted", async () => {
  const src = await sharp({ create: { width: 200, height: 200, channels: 3, background: "#abcdef" } }).png().toBuffer();
  const out = await normalizeImage(src, "image/png", spec({ allowedMimeTypes: ["image/jpeg"] }));
  assert.equal(out.contentType, "image/jpeg");
  const meta = await sharp(out.bytes).metadata();
  assert.equal(meta.format, "jpeg");
});

test("prefers a transparency-preserving format when converting an image with alpha", async () => {
  const src = await pngWithAlpha(200, 200);
  const out = await normalizeImage(src, "image/png", spec({ allowedMimeTypes: ["image/jpeg", "image/webp"] }));
  assert.equal(out.contentType, "image/webp");
});

test("reduces bytes under the budget", async () => {
  const src = await noisyJpeg(2000, 2000);
  const out = await normalizeImage(src, "image/jpeg", spec({ maxWidth: 2000, maxHeight: 2000, maxBytes: 150_000, allowedMimeTypes: ["image/jpeg"] }));
  assert.ok(out.bytes.length <= 150_000, `got ${out.bytes.length} bytes`);
});

test("throws when the image cannot fit the byte budget", async () => {
  const src = await noisyJpeg(1500, 1500);
  await assert.rejects(() =>
    normalizeImage(src, "image/jpeg", spec({ maxWidth: 1500, maxHeight: 1500, maxBytes: 500, allowedMimeTypes: ["image/jpeg"] })),
  );
});

// ----- aspect-ratio framing (issue #39) ---------------------------------------------------------

const igLike = (over: Partial<ImageSpec> = {}) =>
  spec({ allowedMimeTypes: ["image/jpeg"], minAspectRatio: 0.8, maxAspectRatio: 1.91, ...over });

/** Left half red, right half blue, so a crop's position shows in the output colours. */
async function splitPng(w: number, h: number): Promise<Buffer> {
  const half = await sharp({ create: { width: w / 2, height: h, channels: 3, background: "#0000ff" } }).png().toBuffer();
  return sharp({ create: { width: w, height: h, channels: 3, background: "#ff0000" } })
    .composite([{ input: half, left: w / 2, top: 0 }])
    .png()
    .toBuffer();
}

async function pixel(bytes: Buffer, x: number, y: number): Promise<number[]> {
  const { data, info } = await sharp(bytes).raw().toBuffer({ resolveWithObject: true });
  const i = (y * info.width + x) * info.channels;
  return [...data.subarray(i, i + info.channels)];
}

test("an image already inside the aspect range is not framed", async () => {
  const src = await noisyJpeg(1000, 1000);
  const out = await normalizeImage(src, "image/jpeg", igLike());
  assert.equal(out.width, 1000);
  assert.equal(out.height, 1000);
});

test("an out-of-range image with no framing is refused with a clear error", async () => {
  const src = await noisyJpeg(500, 1000);
  await assert.rejects(normalizeImage(src, "image/jpeg", igLike()), /aspect ratio 0\.50 is outside .*0\.8–1\.91/);
});

test("framing is ignored on a platform with no aspect range", async () => {
  const src = await noisyJpeg(500, 1000);
  const out = await normalizeImage(src, "image/jpeg", spec(), { mode: "pad", background: null });
  assert.equal(out.width, 500);
  assert.equal(out.height, 1000);
  assert.equal(out.contentType, "image/jpeg");
});

test("pad widens a tall image into range with the chosen colour", async () => {
  const src = await sharp({ create: { width: 500, height: 1000, channels: 3, background: "#000000" } }).png().toBuffer();
  const out = await normalizeImage(src, "image/png", igLike(), { mode: "pad", background: "#00ff00" });
  assert.equal(out.contentType, "image/jpeg");
  const ratio = out.width! / out.height!;
  assert.ok(ratio >= 0.8 && ratio <= 1.91, `ratio ${ratio}`);
  assert.equal(out.height, 1000);
  const [r, g, b] = await pixel(out.bytes, 2, 500);
  assert.ok(r < 30 && g > 220 && b < 30, `edge pixel ${r},${g},${b}`);
  const [cr, cg, cb] = await pixel(out.bytes, Math.floor(out.width! / 2), 500);
  assert.ok(cr < 30 && cg < 30 && cb < 30, `centre pixel ${cr},${cg},${cb}`);
});

test("pad heightens a wide image into range", async () => {
  const src = await noisyJpeg(2000, 500);
  const out = await normalizeImage(src, "image/jpeg", igLike({ maxWidth: undefined, maxHeight: undefined }), {
    mode: "pad",
    background: "#ffffff",
  });
  assert.equal(out.width, 2000);
  assert.ok(out.width! / out.height! <= 1.91);
});

test("transparent pad keeps alpha where the platform accepts PNG", async () => {
  const src = await noisyJpeg(500, 1000);
  const out = await normalizeImage(src, "image/jpeg", igLike({ allowedMimeTypes: ["image/jpeg", "image/png"] }), {
    mode: "pad",
    background: null,
  });
  assert.equal(out.contentType, "image/png");
  const [, , , alpha] = await pixel(out.bytes, 0, 500);
  assert.equal(alpha, 0);
});

test("transparent pad falls back to white on a JPEG-only platform", async () => {
  const src = await sharp({ create: { width: 500, height: 1000, channels: 3, background: "#000000" } }).jpeg().toBuffer();
  const out = await normalizeImage(src, "image/jpeg", igLike(), { mode: "pad", background: null });
  assert.equal(out.contentType, "image/jpeg");
  const [r, g, b] = await pixel(out.bytes, 1, 500);
  assert.ok(r > 240 && g > 240 && b > 240, `edge pixel ${r},${g},${b}`);
});

test("crop cuts to the chosen region", async () => {
  // 2000x500 (4:1); keep the right (blue) half's middle as a 1000x500 (2:1 → trimmed to ≤1.91) region.
  const src = await splitPng(2000, 500);
  const out = await normalizeImage(src, "image/png", igLike({ maxWidth: undefined, maxHeight: undefined }), {
    mode: "crop",
    crop: { x: 0.5, y: 0, width: 0.5, height: 1 },
  });
  const ratio = out.width! / out.height!;
  assert.ok(ratio >= 0.8 && ratio <= 1.91, `ratio ${ratio}`);
  assert.equal(out.height, 500);
  const [r, , b] = await pixel(out.bytes, 5, 250);
  assert.ok(b > 200 && r < 50, `left edge should be blue, got r=${r} b=${b}`);
});

test("crop applies to the upright image when EXIF rotates it", async () => {
  // Stored 1000x400 with orientation 6 (90° CW) → displayed 400x1000 (tall). Crop the top 400x500.
  const src = await sharp({ create: { width: 1000, height: 400, channels: 3, background: "#808080" } })
    .withMetadata({ orientation: 6 })
    .jpeg()
    .toBuffer();
  const out = await normalizeImage(src, "image/jpeg", igLike({ maxWidth: undefined, maxHeight: undefined }), {
    mode: "crop",
    crop: { x: 0, y: 0, width: 1, height: 0.5 },
  });
  assert.equal(out.width, 400);
  assert.equal(out.height, 500);
});

test("crop without a region is refused", async () => {
  const src = await noisyJpeg(500, 1000);
  await assert.rejects(normalizeImage(src, "image/jpeg", igLike(), { mode: "crop" }), /no crop region/);
});
