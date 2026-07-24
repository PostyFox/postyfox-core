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
