import { basename } from "node:path";
import { AtpAgent } from "@atproto/api";
import { buildRichText } from "./bluesky-richtext.js";
import { describeError } from "./errors.js";
import { mediaStoreFromEnv, type MediaStore } from "../media-store.js";
import { fetchWithUserAgent } from "../user-agent.js";
import { normalizeMedia } from "../media/normalize.js";
import { BLUESKY_SPEC, limitsFromSpec } from "../media/specs.js";
import type {
  Connector,
  ConnectorContext,
  ConnectorLimits,
  DeleteResult,
  DeliverResult,
  IsAuthenticatedResult,
  ListTargetsResult,
  Post,
} from "../types.js";

/** Reference to an uploaded blob as returned by the atproto agent. */
export interface BlueskyBlobRef {
  [key: string]: unknown;
}

/** Minimal surface of the atproto agent that this connector relies on. */
export interface BlueskyAgentLike {
  login(opts: { identifier: string; password: string }): Promise<unknown>;
  uploadBlob(
    bytes: Uint8Array,
    opts: { encoding: string },
  ): Promise<{ data: { blob: BlueskyBlobRef } }>;
  post(record: {
    text: string;
    facets?: unknown;
    embed?: unknown;
    labels?: {
      $type: "com.atproto.label.defs#selfLabels";
      values: { val: string }[];
    };
    createdAt?: string;
  }): Promise<{ uri: string; cid: string }>;
  repost(uri: string, cid: string): Promise<{ uri: string; cid: string }>;
  deletePost(postUri: string): Promise<void>;
  /** Set once logged in: the account and its PDS, which a video upload is authorised against. */
  readonly session?: { did: string };
  readonly pdsUrl?: URL;
  com: {
    atproto: {
      server: {
        getServiceAuth(params: { aud: string; lxm: string; exp: number }): Promise<{ data: { token: string } }>;
      };
    };
  };
}

/** The video service's processing job, as `uploadVideo` and `getJobStatus` report it. */
interface VideoJobStatus {
  jobId?: string;
  state?: string;
  blob?: BlueskyBlobRef;
  error?: string;
  message?: string;
}

/** Bluesky permits at most 4 images per post. */
const MAX_IMAGES = 4;

/** Factory that produces a fresh agent per operation (stateless service). */
export type BlueskyAgentFactory = () => BlueskyAgentLike;

interface BlueskyConfig {
  Handle: string;
}

interface BlueskySecret {
  AppPassword: string;
}

const DEFAULT_SERVICE = "https://bsky.social";
// Bluesky's recommended video flow (docs.bsky.app, "Uploading video"): the video service processes
// the upload and writes the result to the account's PDS before the post exists.
const VIDEO_SERVICE = "https://video.bsky.app";
const VIDEO_POLL_INTERVAL_MS = 1000;
const VIDEO_POLL_TIMEOUT_MS = 5 * 60 * 1000;

const defaultAgentFactory: BlueskyAgentFactory = () =>
  new AtpAgent({ service: DEFAULT_SERVICE, fetch: fetchWithUserAgent }) as unknown as BlueskyAgentLike;

export class BlueskyConnector implements Connector {
  constructor(
    private readonly agentFactory: BlueskyAgentFactory = defaultAgentFactory,
    private readonly mediaStore: MediaStore = mediaStoreFromEnv(),
    private readonly fetchImpl: typeof fetch = fetchWithUserAgent,
    /** Injectable so tests don't wait out real video-processing polls. */
    private readonly videoPollIntervalMs: number = VIDEO_POLL_INTERVAL_MS,
  ) {}

  private parseCredentials(ctx: ConnectorContext): { handle: string; appPassword: string } {
    const config = JSON.parse(ctx.configJson) as BlueskyConfig;
    if (ctx.secretJson === null) {
      throw new Error("missing Bluesky secret (AppPassword)");
    }
    const secret = JSON.parse(ctx.secretJson) as BlueskySecret;
    const handle = config?.Handle;
    const appPassword = secret?.AppPassword;
    if (!handle) throw new Error("missing Bluesky Handle in config");
    if (!appPassword) throw new Error("missing Bluesky AppPassword in secret");
    return { handle, appPassword };
  }

  async isAuthenticated(ctx: ConnectorContext): Promise<IsAuthenticatedResult> {
    try {
      const { handle, appPassword } = this.parseCredentials(ctx);
      const agent = this.agentFactory();
      await agent.login({ identifier: handle, password: appPassword });
      return { isAuthenticated: true };
    } catch (err) {
      return { isAuthenticated: false, detail: describeError(err) };
    }
  }

  async listTargets(ctx: ConnectorContext): Promise<ListTargetsResult> {
    try {
      const config = JSON.parse(ctx.configJson) as BlueskyConfig;
      const handle = config?.Handle;
      if (!handle) return { targets: [] };
      return { targets: [{ id: handle, name: `Bluesky: ${handle}` }] };
    } catch {
      return { targets: [] };
    }
  }

  // Bluesky's caps are fixed (not per-instance), so this needs no network call — it just exposes
  // BLUESKY_SPEC in the shape the frontend's pre-flight media check expects.
  async getLimits(): Promise<ConnectorLimits> {
    return limitsFromSpec(BLUESKY_SPEC);
  }

  async deliver(ctx: ConnectorContext, post: Post): Promise<DeliverResult> {
    try {
      const { handle, appPassword } = this.parseCredentials(ctx);
      const agent = this.agentFactory();
      await agent.login({ identifier: handle, password: appPassword });

      // Bluesky has no separate tags field; core weaves any tags into `post.body` as `#hashtag`s
      // before it reaches here (see TemplateEngine, this connector declares `SupportsTags: false`),
      // so `post.tags` normally arrives empty. buildRichText detects those hashtags as "tag" facets,
      // and turns the body's markdown links into link facets over their display text.
      const { text, facets } = await buildRichText(post.body, agent);

      // Each item is resized/transcoded to Bluesky's limits (images ≤2000px, ≤~976 KB blob; video
      // and animated GIF become MP4) before upload. A post carries either up to 4 images (extras are
      // ignored) as an app.bsky.embed.images embed, or a single video as app.bsky.embed.video.
      let embed: unknown | undefined;
      const cap = BLUESKY_SPEC.maxAttachments ?? MAX_IMAGES;
      const media = (post.media ?? []).slice(0, cap);
      const normalized = [];
      for (const item of media) {
        const raw = await this.mediaStore.fetch(item.container, item.key);
        normalized.push({ item, ...(await normalizeMedia(raw, item.contentType, BLUESKY_SPEC, item.framing)) });
      }
      // Without aspectRatio the Bluesky app lays the media out as a square (issue #39).
      const aspectRatio = ({ width, height }: { width?: number; height?: number }) =>
        width && height ? { aspectRatio: { width, height } } : {};
      const video = normalized.find((n) => n.contentType.startsWith("video/"));
      if (video) {
        if (normalized.length > 1) throw new Error("Bluesky posts take one video or up to four images, not both");
        embed = {
          $type: "app.bsky.embed.video",
          video: await this.uploadVideo(agent, video.bytes, basename(video.item.key) || "video.mp4"),
          ...(video.item.alt ? { alt: video.item.alt } : {}),
          ...aspectRatio(video),
          // An animated GIF plays like one: looped and muted.
          ...(video.item.contentType.toLowerCase() === "image/gif" ? { presentation: "gif" } : {}),
        };
      } else if (normalized.length > 0) {
        const images: { image: BlueskyBlobRef; alt: string; aspectRatio?: { width: number; height: number } }[] = [];
        for (const n of normalized) {
          const uploaded = await agent.uploadBlob(n.bytes, { encoding: n.contentType });
          images.push({ image: uploaded.data.blob, alt: n.item.alt ?? "", ...aspectRatio(n) });
        }
        embed = { $type: "app.bsky.embed.images", images };
      }

      const result = await agent.post({
        text,
        facets,
        embed,
        labels: this.labelsForRating(post.rating),
        createdAt: new Date().toISOString(),
      });

      const rkey = result.uri.split("/").pop() ?? "";
      const externalUrl = `https://bsky.app/profile/${handle}/post/${rkey}`;
      // A repost needs both the record's at:// uri *and* its content-hash cid (atproto's
      // `repost(uri, cid)`), which `post()` only ever returns here at creation time — so both are
      // carried in externalId rather than the bare uri. Opaque to everything else that reads it
      // (core stores/passes it back verbatim; externalUrl above is what's ever shown to a user).
      return { success: true, externalId: encodeRef(result.uri, result.cid), externalUrl };
    } catch (err) {
      return { success: false, error: describeError(err) };
    }
  }

  /** Reposts a record this connector previously delivered (issue #323). */
  async repost(ctx: ConnectorContext, externalId: string): Promise<DeliverResult> {
    try {
      const { handle, appPassword } = this.parseCredentials(ctx);
      const agent = this.agentFactory();
      await agent.login({ identifier: handle, password: appPassword });
      const { uri, cid } = decodeRef(externalId);
      const result = await agent.repost(uri, cid);
      return { success: true, externalId: encodeRef(result.uri, result.cid) };
    } catch (err) {
      return { success: false, error: describeError(err) };
    }
  }

  /** Deletes a record this connector previously delivered (issue #323). */
  async deleteRemote(ctx: ConnectorContext, externalId: string): Promise<DeleteResult> {
    try {
      const { handle, appPassword } = this.parseCredentials(ctx);
      const agent = this.agentFactory();
      await agent.login({ identifier: handle, password: appPassword });
      const { uri } = decodeRef(externalId);
      await agent.deletePost(uri);
      return { success: true };
    } catch (err) {
      return { success: false, error: describeError(err) };
    }
  }

  /**
   * Uploads a video through Bluesky's video service and waits for it to be processed, returning the
   * blob it stored on the account's PDS. A video the service has already processed comes back as
   * a failed job that still carries the earlier blob, so any blob ends the wait.
   */
  private async uploadVideo(agent: BlueskyAgentLike, bytes: Buffer, name: string): Promise<BlueskyBlobRef> {
    const did = agent.session?.did;
    const pds = agent.pdsUrl;
    if (!did || !pds) throw new Error("Bluesky did not report the account's PDS; cannot upload video");
    const { data } = await agent.com.atproto.server.getServiceAuth({
      aud: `did:web:${pds.hostname}`,
      lxm: "com.atproto.repo.uploadBlob",
      exp: Math.floor(Date.now() / 1000) + 30 * 60,
    });

    const upload = new URL("/xrpc/app.bsky.video.uploadVideo", VIDEO_SERVICE);
    upload.searchParams.set("did", did);
    upload.searchParams.set("name", name);
    const res = await this.fetchImpl(upload, {
      method: "POST",
      headers: { authorization: `Bearer ${data.token}`, "content-type": "video/mp4" },
      body: bytes,
    });
    let job = await readJob(res);
    if (!job.jobId && !job.blob)
      throw new Error(`Bluesky video upload failed with HTTP ${res.status}${job.message ? `: ${job.message}` : ""}`);

    const deadline = Date.now() + VIDEO_POLL_TIMEOUT_MS;
    while (!job.blob) {
      if (job.state === "JOB_STATE_FAILED")
        throw new Error(`Bluesky video processing failed: ${job.message ?? job.error ?? "unknown error"}`);
      if (Date.now() > deadline) throw new Error("Bluesky video processing timed out");
      await new Promise((resolve) => setTimeout(resolve, this.videoPollIntervalMs));
      const status = new URL("/xrpc/app.bsky.video.getJobStatus", VIDEO_SERVICE);
      status.searchParams.set("jobId", job.jobId!);
      job = await readJob(await this.fetchImpl(status));
    }
    return job.blob;
  }

  private labelsForRating(
    rating: Post["rating"],
  ):
    | {
        $type: "com.atproto.label.defs#selfLabels";
        values: { val: string }[];
      }
    | undefined {
    let value: string | undefined;
    switch (rating) {
      case "mature":
        value = "sexual";
        break;
      case "adult":
        value = "porn";
        break;
      case "extreme":
        value = "graphic-media";
        break;
    }
    return value
      ? {
          $type: "com.atproto.label.defs#selfLabels",
          values: [{ val: value }],
        }
      : undefined;
  }
}

/** Reads a job status, which the video service returns either bare or wrapped in `jobStatus`. */
async function readJob(res: Response): Promise<VideoJobStatus> {
  const text = await res.text();
  try {
    const body = JSON.parse(text) as VideoJobStatus & { jobStatus?: VideoJobStatus };
    return body.jobStatus ?? body;
  } catch {
    return { message: text.slice(0, 200) };
  }
}

/** Packs a record's uri+cid into the opaque string this connector stores as `externalId`. */
function encodeRef(uri: string, cid: string): string {
  return JSON.stringify({ uri, cid });
}

/** Unpacks a ref encoded by {@link encodeRef}. Throws on anything else (a stale/foreign externalId). */
function decodeRef(externalId: string): { uri: string; cid: string } {
  const parsed = JSON.parse(externalId) as { uri?: string; cid?: string };
  if (!parsed.uri || !parsed.cid) throw new Error("malformed Bluesky post reference");
  return { uri: parsed.uri, cid: parsed.cid };
}
