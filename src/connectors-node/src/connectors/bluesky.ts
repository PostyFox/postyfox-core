import { AtpAgent, RichText } from "@atproto/api";
import { describeError } from "./errors.js";
import { mediaStoreFromEnv, type MediaStore } from "../media-store.js";
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
      // Bluesky permits at most 4 images; extras are ignored.
      let embed: unknown | undefined;
      const media = (post.media ?? []).slice(0, MAX_IMAGES);
      if (media.length > 0) {
        const images: { image: BlueskyBlobRef; alt: string }[] = [];
        for (const item of media) {
          const bytes = await this.mediaStore.fetch(item.container, item.key);
          const uploaded = await agent.uploadBlob(bytes, {
            encoding: item.contentType,
          });
          images.push({ image: uploaded.data.blob, alt: item.alt ?? "" });
        }
        embed = { $type: "app.bsky.embed.images", images };
      }

      const result = await agent.post({
        text,
        facets,
        embed,
        createdAt: new Date().toISOString(),
      });

      const rkey = result.uri.split("/").pop() ?? "";
      const externalUrl = `https://bsky.app/profile/${handle}/post/${rkey}`;
      return { success: true, externalId: result.uri, externalUrl };
    } catch (err) {
      return { success: false, error: describeError(err) };
    }
  }
}

