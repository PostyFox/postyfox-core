import sharp, { type Metadata, type Sharp } from "sharp";
import type { ImageSpec, MediaFraming, NormalizedMedia } from "./types.js";

const MIN_QUALITY = 40;
const MIN_EDGE = 320;
const START_QUALITY = 90;
const MAX_INPUT_PIXELS = 100_000_000;
// Framing aims this far inside a platform's aspect-ratio bounds so the later downscale's rounding
// can't push the result back out.
const RATIO_MARGIN = 0.002;

/**
 * Normalises a still raster image (JPEG/PNG/WebP) to an {@link ImageSpec}: EXIF auto-orient,
 * metadata strip, aspect-ratio framing (see {@link frameToSpec}), downscale-only resize, format
 * conversion to an accepted type, and iterative quality/dimension reduction to fit the byte budget.
 * Undecodable input and animated images (multi-frame) are returned unchanged (animated media belongs
 * to the video path). Throws when the image cannot be brought within the byte limit, or is outside
 * the spec's aspect-ratio range with no framing chosen.
 */
export async function normalizeImage(
  source: Buffer,
  contentType: string,
  spec: ImageSpec,
  framing?: MediaFraming | null,
): Promise<NormalizedMedia> {
  let bytes = source;
  let meta: Metadata;
  try {
    meta = await sharp(bytes).metadata();
  } catch {
    return { bytes, contentType }; // not a decodable raster image, leave it untouched
  }
  if ((meta.pages ?? 1) > 1) return { bytes, contentType }; // animated, handled by the video path
  if (!meta.width || !meta.height) return { bytes, contentType };

  const framed = await frameToSpec(bytes, meta, spec, framing);
  if (framed) {
    bytes = framed;
    meta = await sharp(framed).metadata();
  }

  const transparentPad = framed !== null && framing?.mode === "pad" && !framing.background;
  const mayHaveAlpha = transparentPad || /(png|webp|gif)/i.test(contentType);
  // Transparent padding prefers a format that keeps it, whatever the source was.
  const outMime = chooseMime(transparentPad ? "image/png" : contentType, spec.allowedMimeTypes, mayHaveAlpha);

  const oversized =
    (spec.maxWidth !== undefined && meta.width > spec.maxWidth) ||
    (spec.maxHeight !== undefined && meta.height > spec.maxHeight);

  const pipeline = (edge?: number) => {
    let p = sharp(bytes, { failOn: "none", limitInputPixels: MAX_INPUT_PIXELS }).rotate();
    if (edge !== undefined) {
      p = p.resize({ width: edge, height: edge, fit: "inside", withoutEnlargement: true });
    } else if (oversized) {
      p = p.resize({
        width: spec.maxWidth,
        height: spec.maxHeight,
        fit: "inside",
        withoutEnlargement: true,
      });
    }
    if (outMime === "image/jpeg" && mayHaveAlpha) p = p.flatten({ background: "#ffffff" });
    return p;
  };

  const done = async (out: Buffer): Promise<NormalizedMedia> => {
    const { width, height } = await sharp(out).metadata();
    return { bytes: out, contentType: outMime, width, height };
  };

  let out = await encode(pipeline(), outMime, START_QUALITY);
  const budget = spec.maxBytes;
  if (budget === undefined || out.length <= budget) return done(out);

  // 1) Reduce encoder quality (lossy formats only).
  if (supportsQuality(outMime)) {
    for (let q = START_QUALITY - 10; q >= MIN_QUALITY; q -= 10) {
      out = await encode(pipeline(), outMime, q);
      if (out.length <= budget) return done(out);
    }
  }

  // 2) Step dimensions down (~85% per pass) at the quality floor until it fits or bottoms out.
  const quality = supportsQuality(outMime) ? MIN_QUALITY : START_QUALITY;
  const capped = Math.min(
    Math.max(meta.width, meta.height),
    spec.maxWidth ?? Number.MAX_SAFE_INTEGER,
    spec.maxHeight ?? Number.MAX_SAFE_INTEGER,
  );
  let edge = Number.isFinite(capped) ? capped : Math.max(meta.width, meta.height);
  while (edge > MIN_EDGE) {
    edge = Math.max(MIN_EDGE, Math.floor(edge * 0.85));
    out = await encode(pipeline(edge), outMime, quality);
    if (out.length <= budget) return done(out);
    if (edge === MIN_EDGE) break;
  }

  throw new Error(`image cannot be reduced below this platform's ${budget}-byte limit`);
}

/**
 * Brings an image inside the spec's aspect-ratio range (issue #39) using the author's framing:
 * "pad" extends the short sides with the background colour (transparent when none), "crop" cuts
 * to the chosen region, trimmed centrally if rounding leaves it fractionally out of range. Returns a
 * lossless, auto-oriented PNG of the result, or null when no framing is needed (no range, or
 * already inside it). Throws when the image is out of range and the author chose nothing.
 */
async function frameToSpec(
  bytes: Buffer,
  meta: Metadata,
  spec: ImageSpec,
  framing: MediaFraming | null | undefined,
): Promise<Buffer | null> {
  if (spec.minAspectRatio === undefined && spec.maxAspectRatio === undefined) return null;
  const min = spec.minAspectRatio ?? 0;
  const max = spec.maxAspectRatio ?? Number.POSITIVE_INFINITY;
  // EXIF orientations 5-8 rotate by 90°, swapping the stored dimensions.
  const swap = (meta.orientation ?? 1) >= 5;
  const width = swap ? meta.height! : meta.width!;
  const height = swap ? meta.width! : meta.height!;
  const ratio = width / height;
  if (ratio >= min && ratio <= max) return null;
  if (!framing) {
    throw new Error(
      `image aspect ratio ${ratio.toFixed(2)} is outside this platform's accepted ${min}–${max} range; choose pad or crop for it when composing`,
    );
  }

  const lo = min * (1 + RATIO_MARGIN);
  const hi = max * (1 - RATIO_MARGIN);
  // Orientation is baked in first so padding and crop coordinates refer to the upright image.
  const upright = await sharp(bytes, { failOn: "none", limitInputPixels: MAX_INPUT_PIXELS }).rotate().png().toBuffer();

  if (framing.mode === "pad") {
    const background = framing.background || { r: 255, g: 255, b: 255, alpha: 0 };
    const extend = { top: 0, bottom: 0, left: 0, right: 0, background };
    if (ratio < min) {
      const extra = Math.ceil(height * lo) - width;
      extend.left = Math.floor(extra / 2);
      extend.right = extra - extend.left;
    } else {
      const extra = Math.ceil(width / hi) - height;
      extend.top = Math.floor(extra / 2);
      extend.bottom = extra - extend.top;
    }
    return sharp(upright).extend(extend).png().toBuffer();
  }

  const c = framing.crop;
  if (!c) throw new Error("crop framing has no crop region");
  let left = Math.min(width - 1, Math.max(0, Math.round(c.x * width)));
  let top = Math.min(height - 1, Math.max(0, Math.round(c.y * height)));
  let w = Math.max(1, Math.min(width - left, Math.round(c.width * width)));
  let h = Math.max(1, Math.min(height - top, Math.round(c.height * height)));
  if (w / h < min) {
    const trimmed = Math.max(1, Math.floor(w / lo));
    top += Math.floor((h - trimmed) / 2);
    h = trimmed;
  } else if (w / h > max) {
    const trimmed = Math.max(1, Math.floor(h * hi));
    left += Math.floor((w - trimmed) / 2);
    w = trimmed;
  }
  return sharp(upright).extract({ left, top, width: w, height: h }).png().toBuffer();
}

function encode(pipeline: Sharp, mime: string, quality: number): Promise<Buffer> {
  switch (mime) {
    case "image/jpeg":
      return pipeline.jpeg({ quality }).toBuffer();
    case "image/webp":
      return pipeline.webp({ quality }).toBuffer();
    case "image/png":
      return pipeline.png().toBuffer();
    default:
      return pipeline.jpeg({ quality }).toBuffer();
  }
}

function supportsQuality(mime: string): boolean {
  return mime === "image/jpeg" || mime === "image/webp";
}

function normMime(mime: string | undefined): string {
  const m = (mime ?? "").trim().toLowerCase();
  return m === "image/jpg" ? "image/jpeg" : m;
}

/** Keeps the source format when the platform accepts it, else picks a preferred allowed one. */
export function chooseMime(sourceMime: string, allowed: string[], mayHaveAlpha: boolean): string {
  const src = normMime(sourceMime);
  if (!allowed || allowed.length === 0) return src || "image/jpeg";
  const has = (m: string) => allowed.some((a) => normMime(a) === m);
  if (src && has(src)) return src;
  if (mayHaveAlpha) {
    if (has("image/webp")) return "image/webp";
    if (has("image/png")) return "image/png";
  }
  if (has("image/jpeg")) return "image/jpeg";
  if (has("image/webp")) return "image/webp";
  if (has("image/png")) return "image/png";
  return normMime(allowed[0]) || "image/jpeg";
}
