import { Rettiwt } from "rettiwt-api";
import { describeError } from "./errors.js";
import { mediaStoreFromEnv, type MediaStore } from "../media-store.js";
import { normalizeMedia } from "../media/normalize.js";
import { limitsFromSpec, X_SPEC } from "../media/specs.js";
import type {
  Connector,
  ConnectorContext,
  ConnectorLimits,
  DeliverResult,
  IsAuthenticatedResult,
  ListTargetsResult,
  Post,
} from "../types.js";

const MAX_POST_LENGTH = 280;
// The cookies rettiwt-api builds its "API key" from; everything else in the header is dropped.
const REQUIRED_COOKIES = ["auth_token", "ct0", "twid"] as const;

interface XSecret {
  CookieHeader?: string;
}

/** The slice of rettiwt-api's `Rettiwt` this connector uses, so tests can substitute a fake. */
export interface XClient {
  user: { details(): Promise<{ userName: string } | undefined> };
  tweet: {
    upload(media: ArrayBuffer): Promise<string>;
    post(options: { text?: string; media?: { id: string }[] }): Promise<string | undefined>;
  };
}

export type XClientFactory = (apiKey: string) => XClient;

export interface XConnectorOptions {
  mediaStore?: MediaStore;
  clientFactory?: XClientFactory;
}

export class XConnector implements Connector {
  private readonly mediaStore: MediaStore;
  private readonly clientFactory: XClientFactory;

  constructor(options: XConnectorOptions = {}) {
    this.mediaStore = options.mediaStore ?? mediaStoreFromEnv();
    this.clientFactory = options.clientFactory ?? ((apiKey) => new Rettiwt({ apiKey }) as XClient);
  }

  async isAuthenticated(ctx: ConnectorContext): Promise<IsAuthenticatedResult> {
    try {
      const user = await this.createClient(ctx).user.details();
      return user
        ? { isAuthenticated: true, detail: `@${user.userName}` }
        : { isAuthenticated: false, detail: "X session is not logged in" };
    } catch (error) {
      return { isAuthenticated: false, detail: describeError(error) };
    }
  }

  async listTargets(ctx: ConnectorContext): Promise<ListTargetsResult> {
    try {
      const user = await this.createClient(ctx).user.details();
      if (!user) return { targets: [] };
      return { targets: [{ id: user.userName, name: `X: @${user.userName}` }] };
    } catch {
      return { targets: [] };
    }
  }

  // Post length is reported via the static ServiceDefinition descriptor instead (see
  // ServiceCollectionExtensions), the same convention every other fixed-spec connector follows here.
  async getLimits(): Promise<ConnectorLimits> {
    return limitsFromSpec(X_SPEC);
  }

  async deliver(ctx: ConnectorContext, post: Post): Promise<DeliverResult> {
    try {
      // rettiwt-api switches to X's long-post endpoint above 280 characters, which only Premium
      // accounts may use, so reject up front rather than fail obscurely at X.
      if (post.body.length > MAX_POST_LENGTH)
        throw new Error(`X posts may not exceed ${MAX_POST_LENGTH} characters`);
      if (!post.body.trim() && post.media.length === 0)
        throw new Error("X posts require text or an image");
      if (post.media.length > (X_SPEC.maxAttachments ?? 4))
        throw new Error(`X posts accept at most ${X_SPEC.maxAttachments} images`);

      const client = this.createClient(ctx);
      const media: { id: string }[] = [];
      for (const item of post.media) {
        if (!item.contentType.toLowerCase().startsWith("image/"))
          throw new Error("X integration currently supports image attachments only");
        const raw = await this.mediaStore.fetch(item.container, item.key);
        const normalized = await normalizeMedia(raw, item.contentType, X_SPEC);
        const bytes = normalized.bytes;
        // rettiwt-api takes an ArrayBuffer, not a Buffer view, which may sit inside a larger pool.
        const buffer = bytes.buffer.slice(bytes.byteOffset, bytes.byteOffset + bytes.byteLength) as ArrayBuffer;
        media.push({ id: await client.tweet.upload(buffer) });
      }

      const id = await client.tweet.post({
        text: post.body,
        ...(media.length > 0 ? { media } : {}),
      });
      if (!id) throw new Error("X did not return a post id");
      return { success: true, externalId: id, externalUrl: `https://x.com/i/status/${id}` };
    } catch (error) {
      return { success: false, error: describeError(error) };
    }
  }

  private createClient(ctx: ConnectorContext): XClient {
    if (!ctx.secretJson) throw new Error("missing X session cookies");
    let secret: XSecret;
    try {
      secret = JSON.parse(ctx.secretJson) as XSecret;
    } catch {
      throw new Error("invalid X session cookies");
    }
    if (!secret.CookieHeader) throw new Error("missing X session cookies");
    return this.clientFactory(apiKeyFromCookies(secret.CookieHeader));
  }
}

/**
 * rettiwt-api authenticates with a base64 "API key" of `name=value;` pairs (the trailing `;` is
 * required, it parses the user id out of `twid` with it).
 */
export function apiKeyFromCookies(cookieHeader: string): string {
  const cookies = new Map<string, string>();
  for (const pair of cookieHeader.split(";")) {
    const at = pair.indexOf("=");
    if (at > 0) cookies.set(pair.slice(0, at).trim(), pair.slice(at + 1).trim());
  }
  const parts = REQUIRED_COOKIES.map((name) => {
    const value = cookies.get(name);
    if (!value) throw new Error(`missing X session cookie '${name}'`);
    return `${name}=${value};`;
  });
  return Buffer.from(parts.join("")).toString("base64");
}
