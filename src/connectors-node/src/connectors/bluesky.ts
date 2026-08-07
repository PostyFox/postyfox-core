import { AtpAgent, RichText } from "@atproto/api";
import { describeError } from "./errors.js";
import { mediaStoreFromEnv, type MediaStore } from "../media-store.js";
import { normalizeMedia } from "../media/normalize.js";
import { BLUESKY_SPEC } from "../media/specs.js";
import type {
  Connector,
  ConnectorContext,
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

const defaultAgentFactory: BlueskyAgentFactory = () =>
  new AtpAgent({ service: DEFAULT_SERVICE }) as unknown as BlueskyAgentLike;

export class BlueskyConnector implements Connector {
  constructor(
    private readonly agentFactory: BlueskyAgentFactory = defaultAgentFactory,
    private readonly mediaStore: MediaStore = mediaStoreFromEnv(),
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

  async deliver(ctx: ConnectorContext, post: Post): Promise<DeliverResult> {
    try {
      const { handle, appPassword } = this.parseCredentials(ctx);
      const agent = this.agentFactory();
      await agent.login({ identifier: handle, password: appPassword });

      // Detect facets (links/mentions) where possible; fall back to plain text.
      let text = post.body;
      let facets: unknown | undefined;
      try {
        const rt = new RichText({ text: post.body });
        await rt.detectFacets(agent as unknown as AtpAgent);
        text = rt.text;
        facets = rt.facets;
      } catch {
        // RichText detection is best-effort; plain text is acceptable.
        text = post.body;
        facets = undefined;
      }

      // Upload any image media and attach it as an app.bsky.embed.images embed.
      // Bluesky permits at most 4 images; extras are ignored. Each item is resized/transcoded to
      // Bluesky's limits (≤2000px, ≤~976 KB blob) before upload.
      let embed: unknown | undefined;
      const cap = BLUESKY_SPEC.maxAttachments ?? MAX_IMAGES;
      const media = (post.media ?? []).slice(0, cap);
      if (media.length > 0) {
        const images: { image: BlueskyBlobRef; alt: string }[] = [];
        for (const item of media) {
          const raw = await this.mediaStore.fetch(item.container, item.key);
          const normalized = await normalizeMedia(raw, item.contentType, BLUESKY_SPEC);
          const uploaded = await agent.uploadBlob(normalized.bytes, {
            encoding: normalized.contentType,
          });
          images.push({ image: uploaded.data.blob, alt: item.alt ?? "" });
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
      return { success: true, externalId: result.uri, externalUrl };
    } catch (err) {
      return { success: false, error: describeError(err) };
    }
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
