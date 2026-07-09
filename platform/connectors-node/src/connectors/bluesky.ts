import { AtpAgent, RichText } from "@atproto/api";
import type {
  Connector,
  ConnectorContext,
  DeliverResult,
  IsAuthenticatedResult,
  ListTargetsResult,
  Post,
} from "../types.js";

/** Minimal surface of the atproto agent that this connector relies on. */
export interface BlueskyAgentLike {
  login(opts: { identifier: string; password: string }): Promise<unknown>;
  post(record: {
    text: string;
    facets?: unknown;
    createdAt?: string;
  }): Promise<{ uri: string; cid: string }>;
}

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
  constructor(private readonly agentFactory: BlueskyAgentFactory = defaultAgentFactory) {}

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
      return { isAuthenticated: false, detail: errorMessage(err) };
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

      // TODO: media — image/video embeds are not yet delivered (text-only).
      const result = await agent.post({
        text,
        facets,
        createdAt: new Date().toISOString(),
      });

      const rkey = result.uri.split("/").pop() ?? "";
      const externalUrl = `https://bsky.app/profile/${handle}/post/${rkey}`;
      return { success: true, externalId: result.uri, externalUrl };
    } catch (err) {
      return { success: false, error: errorMessage(err) };
    }
  }
}

function errorMessage(err: unknown): string {
  return err instanceof Error ? err.message : String(err);
}
