import sharp, { type Metadata, type Sharp } from "sharp";
import type { ImageSpec, NormalizedMedia } from "./types.js";

const MIN_QUALITY = 40;
const MIN_EDGE = 320;
const START_QUALITY = 90;

/**
 * Normalizes a still raster image (JPEG/PNG/WebP) to an {@link ImageSpec}: EXIF auto-orient,
 * metadata strip, downscale-only resize, format conversion to an accepted type, and iterative
 * quality/dimension reduction to fit the byte budget. Undecodable input and animated images
 * (multi-frame) are returned unchanged (animated media belongs to the video path). Throws when the
 * image cannot be brought within the byte limit.
 */
export async function normalizeImage(
  bytes: Buffer,
  contentType: string,
  spec: ImageSpec,
): Promise<NormalizedMedia> {
  let meta: Metadata;
  try {
    meta = await sharp(bytes).metadata();
  } catch {
    return { bytes, contentType }; // not a decodable raster image — leave it untouched
  }
  if ((meta.pages ?? 1) > 1) return { bytes, contentType }; // animated — handled by the video path
  if (!meta.width || !meta.height) return { bytes, contentType };

  const mayHaveAlpha = /(png|webp|gif)/i.test(contentType);
  const outMime = chooseMime(contentType, spec.allowedMimeTypes, mayHaveAlpha);

  const oversized =
    (spec.maxWidth !== undefined && meta.width > spec.maxWidth) ||
    (spec.maxHeight !== undefined && meta.height > spec.maxHeight);

  const pipeline = (edge?: number) => {
    let p = sharp(bytes, { failOn: "none", limitInputPixels: 100_000_000 }).rotate();
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

  let out = await encode(pipeline(), outMime, START_QUALITY);
  const budget = spec.maxBytes;
  if (budget === undefined || out.length <= budget) return { bytes: out, contentType: outMime };

  // 1) Reduce encoder quality (lossy formats only).
  if (supportsQuality(outMime)) {
    for (let q = START_QUALITY - 10; q >= MIN_QUALITY; q -= 10) {
      out = await encode(pipeline(), outMime, q);
      if (out.length <= budget) return { bytes: out, contentType: outMime };
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
    if (out.length <= budget) return { bytes: out, contentType: outMime };
    if (edge === MIN_EDGE) break;
  }

  throw new Error(`image cannot be reduced below this platform's ${budget}-byte limit`);
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
