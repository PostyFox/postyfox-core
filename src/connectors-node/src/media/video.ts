import { mkdtemp, readFile, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { basename, join } from "node:path";
import ffmpeg from "fluent-ffmpeg";
import type { ImageSpec, NormalizedMedia, VideoSpec } from "./types.js";

export type VideoAction = "passthrough" | "transcode" | "fail";

export interface VideoProbe {
  width: number;
  height: number;
  durationSeconds: number;
  bytes: number;
}

export interface VideoDecision {
  action: VideoAction;
  targetWidth: number;
  targetHeight: number;
  targetMime: string;
  reason?: string;
}

/**
 * Pure fit logic for video, factored out so it is unit-testable without ffmpeg. Downscale-only
 * (aspect preserved, even dimensions for H.264); over-duration is a hard fail (no silent
 * truncation); format is converted to an accepted container when the source isn't allowed.
 */
export function decideVideo(probe: VideoProbe, spec: VideoSpec, sourceMime: string): VideoDecision {
  if (spec.maxDurationSeconds !== undefined && probe.durationSeconds > spec.maxDurationSeconds + 0.5) {
    return {
      action: "fail",
      targetWidth: probe.width,
      targetHeight: probe.height,
      targetMime: normMime(sourceMime) || "video/mp4",
      reason: `video is ${Math.round(probe.durationSeconds)}s but this platform allows at most ${spec.maxDurationSeconds}s`,
    };
  }

  const [tw, th] = fitWithin(probe.width, probe.height, spec.maxWidth, spec.maxHeight);
  const needsResize = tw !== probe.width || th !== probe.height;
  const targetMime = chooseMime(sourceMime, spec.allowedMimeTypes);
  const needsConvert = targetMime !== normMime(sourceMime);
  const overBytes = spec.maxBytes !== undefined && probe.bytes > spec.maxBytes;

  if (!needsResize && !needsConvert && !overBytes) {
    return { action: "passthrough", targetWidth: probe.width, targetHeight: probe.height, targetMime };
  }
  return { action: "transcode", targetWidth: tw, targetHeight: th, targetMime };
}

/** Scales (w,h) down to fit within (maxW,maxH) preserving aspect; never enlarges. Even dims (H.264). */
export function fitWithin(w: number, h: number, maxW?: number, maxH?: number): [number, number] {
  if (w <= 0 || h <= 0) return [w, h];
  let scale = 1;
  if (maxW !== undefined && w > maxW) scale = Math.min(scale, maxW / w);
  if (maxH !== undefined && h > maxH) scale = Math.min(scale, maxH / h);
  if (scale >= 1) return [w, h];
  const nw = Math.max(2, Math.round(w * scale));
  const nh = Math.max(2, Math.round(h * scale));
  return [nw - (nw % 2), nh - (nh % 2)];
}

function chooseMime(sourceMime: string, allowed: string[]): string {
  const src = normMime(sourceMime);
  if (!allowed || allowed.length === 0) return src || "video/mp4";
  if (src && allowed.some((a) => normMime(a) === src)) return src;
  if (allowed.some((a) => normMime(a) === "video/mp4")) return "video/mp4";
  return normMime(allowed[0]) || "video/mp4";
}

function normMime(mime: string | undefined): string {
  return (mime ?? "").trim().toLowerCase();
}

/**
 * Normalises video (and animated GIF) to a {@link VideoSpec} using ffmpeg: probes, then passes
 * through when already within limits or downscales / bitrate-caps / transcodes to an accepted
 * container. Static GIFs pass through. Throws when the media can't be brought within the limits.
 */
export async function normalizeVideo(
  bytes: Buffer,
  contentType: string,
  spec: VideoSpec,
): Promise<NormalizedMedia> {
  const dir = await mkdtemp(join(tmpdir(), "postyfox-video-"));
  const input = join(dir, basename(contentType.includes("gif") ? "in.gif" : "in.bin"));
  try {
    await writeFile(input, bytes);

    let data: ffmpeg.FfprobeData;
    try {
      data = await probe(input);
    } catch {
      return { bytes, contentType }; // unprobeable, leave untouched
    }

    const stream = data.streams.find((s) => s.codec_type === "video");
    if (!stream || !stream.width || !stream.height) return { bytes, contentType };

    const durationSeconds = Number(data.format.duration ?? stream.duration ?? 0) || 0;

    // A single-frame GIF is effectively a still image, leave it untouched.
    if (contentType.includes("gif") && durationSeconds <= 0.1) return { bytes, contentType };

    const decision = decideVideo(
      { width: stream.width, height: stream.height, durationSeconds, bytes: bytes.length },
      spec,
      contentType,
    );
    if (decision.action === "fail") throw new Error(decision.reason ?? "video exceeds this platform's limits");
    if (decision.action === "passthrough") return { bytes, contentType, width: stream.width, height: stream.height };

    const ext = decision.targetMime.includes("webm") ? ".webm" : ".mp4";
    const output = join(dir, `out${ext}`);

    let bitrateKbps = 0;
    if (spec.maxBytes !== undefined && durationSeconds > 0.5) {
      bitrateKbps = Math.floor(((spec.maxBytes * 8) / durationSeconds / 1000) * 0.85);
    }

    await transcode(input, output, decision, bitrateKbps);

    const outBytes = await readFile(output);
    if (spec.maxBytes !== undefined && outBytes.length > spec.maxBytes) {
      throw new Error(
        `video is ${outBytes.length} bytes after transcoding, above this platform's ${spec.maxBytes}-byte limit`,
      );
    }
    return {
      bytes: outBytes,
      contentType: decision.targetMime,
      width: decision.targetWidth,
      height: decision.targetHeight,
    };
  } finally {
    await rm(dir, { recursive: true, force: true });
  }
}

/** Longest video converted to GIF; anything longer is far too large for an image-only platform. */
export const MAX_GIF_SECONDS = 30;

/** Frame rate and max width for each GIF attempt, tried in order until one fits the byte cap. */
export const GIF_ATTEMPTS = [
  { fps: 12, maxWidth: 480 },
  { fps: 8, maxWidth: 320 },
];

/** Why a video can't become a GIF, or undefined when it can be attempted. */
export function gifRefusal(durationSeconds: number): string | undefined {
  return durationSeconds > MAX_GIF_SECONDS + 0.5
    ? `video is ${Math.round(durationSeconds)}s but conversion to GIF allows at most ${MAX_GIF_SECONDS}s`
    : undefined;
}

/**
 * Converts a video to an animated GIF for platforms that take images only, when the author opted in
 * (the audio is lost). Downscales and drops the frame rate per {@link GIF_ATTEMPTS} until the GIF
 * fits the image spec's byte cap, and throws when even the last attempt doesn't.
 */
export async function videoToGif(bytes: Buffer, spec: ImageSpec): Promise<NormalizedMedia> {
  const dir = await mkdtemp(join(tmpdir(), "postyfox-gif-"));
  const input = join(dir, "in.bin");
  try {
    await writeFile(input, bytes);
    const data = await probe(input);
    const stream = data.streams.find((s) => s.codec_type === "video");
    if (!stream?.width || !stream.height) throw new Error("video has no readable picture to convert to GIF");
    const refusal = gifRefusal(Number(data.format.duration ?? stream.duration ?? 0) || 0);
    if (refusal) throw new Error(refusal);

    let size = 0;
    for (const [i, attempt] of GIF_ATTEMPTS.entries()) {
      const maxWidth = Math.min(attempt.maxWidth, spec.maxWidth ?? attempt.maxWidth);
      const [width, height] = fitWithin(stream.width, stream.height, maxWidth, spec.maxHeight);
      const output = join(dir, `out${i}.gif`);
      await toGif(input, output, attempt.fps, width, height);
      const gif = await readFile(output);
      if (spec.maxBytes === undefined || gif.length <= spec.maxBytes)
        return { bytes: gif, contentType: "image/gif", width, height };
      size = gif.length;
    }
    throw new Error(`video is ${size} bytes as a GIF, above this platform's ${spec.maxBytes}-byte limit`);
  } finally {
    await rm(dir, { recursive: true, force: true });
  }
}

function toGif(input: string, output: string, fps: number, width: number, height: number): Promise<void> {
  return new Promise((resolve, reject) => {
    ffmpeg(input)
      .noAudio()
      .outputOptions([
        "-vf",
        // A palette built from the clip itself keeps colours far closer than ffmpeg's default one.
        `fps=${fps},scale=${width}:${height}:flags=lanczos,split[a][b];[a]palettegen[p];[b][p]paletteuse`,
        "-loop",
        "0",
      ])
      .on("end", () => resolve())
      .on("error", (err) => reject(err))
      .save(output);
  });
}

function probe(input: string): Promise<ffmpeg.FfprobeData> {
  return new Promise((resolve, reject) => {
    ffmpeg.ffprobe(input, (err, data) => (err ? reject(err) : resolve(data)));
  });
}

function transcode(
  input: string,
  output: string,
  decision: VideoDecision,
  bitrateKbps: number,
): Promise<void> {
  return new Promise((resolve, reject) => {
    let command = ffmpeg(input);
    if (decision.targetMime.includes("webm")) {
      command = command.videoCodec("libvpx-vp9").audioCodec("libopus");
    } else {
      command = command.videoCodec("libx264").audioCodec("aac").outputOptions("-movflags", "+faststart");
    }
    command = command.size(`${decision.targetWidth}x${decision.targetHeight}`);
    if (bitrateKbps > 0) command = command.videoBitrate(bitrateKbps);
    command
      .on("end", () => resolve())
      .on("error", (err) => reject(err))
      .save(output);
  });
}
