import { basename } from "node:path";
import { describeError } from "./errors.js";
import {
  DeviantartGrantRejectedError,
  DeviantartOAuth2Provider,
  type DeviantartAppCredentials,
  type DeviantartSecret,
} from "./deviantart-oauth.js";
import { mediaStoreFromEnv, type MediaStore } from "../media-store.js";
import { fetchWithUserAgent } from "../user-agent.js";
import { DEVIANTART_SPEC, limitsFromSpec } from "../media/specs.js";
import type {
  Connector,
  ConnectorContext,
  ConnectorLimits,
  DeliverResult,
  IsAuthenticatedResult,
  ListTargetsResult,
  OAuthProvider,
  Post,
} from "../types.js";

const API_URL = "https://www.deviantart.com/api/v1/oauth2";
const MATURE_LEVELS: Record<NonNullable<Post["rating"]>, string | null> = {
  general: null,
  mature: "moderate",
  adult: "strict",
  extreme: "strict",
};

/** Per-submission options (see deviantart-post-options.schema.json); values arrive as strings. */
interface DeviantartConfig {
  Mode?: string;
  GalleryIds?: string;
  AllowComments?: string;
  NoAi?: string;
  AiGenerated?: string;
}

interface DeviantartError {
  error?: string;
  error_description?: string;
  error_details?: Record<string, string>;
  error_code?: number;
}

export interface DeviantartConnectorOptions {
  /** Fallback app credentials; normally they come from the operational secret store per request. */
  app?: DeviantartAppCredentials;
  mediaStore?: MediaStore;
  fetch?: typeof fetch;
}

/**
 * DeviantArt's documented API (deviantart.readme.io), authorised through an OAuth2 connect flow. A
 * post uploads one image to Sta.sh (`/stash/submit`), then publishes it as a deviation
 * (`/stash/publish`) unless the post asks to stay in Sta.sh. Array fields use the PHP-style `name[]`
 * form encoding established DeviantArt clients send.
 */
export class DeviantartConnector implements Connector {
  readonly oauth: OAuthProvider;
  private readonly mediaStore: MediaStore;
  private readonly fetchImpl: typeof fetch;

  constructor(private readonly options: DeviantartConnectorOptions = {}) {
    this.mediaStore = options.mediaStore ?? mediaStoreFromEnv();
    this.fetchImpl = options.fetch ?? fetchWithUserAgent;
    this.oauth = {
      startAuthorization: async (input) => this.provider(input.operationalSecretJson).startAuthorization(input),
      completeAuthorization: async (input) => this.provider(input.operationalSecretJson).completeAuthorization(input),
    };
  }

  async isAuthenticated(ctx: ConnectorContext): Promise<IsAuthenticatedResult> {
    try {
      const user = await this.whoami(parseSecret(ctx));
      return { isAuthenticated: true, detail: user.username };
    } catch (error) {
      return { isAuthenticated: false, detail: describeError(error) };
    }
  }

  async listTargets(ctx: ConnectorContext): Promise<ListTargetsResult> {
    try {
      const user = await this.whoami(parseSecret(ctx));
      if (!user.username) return { targets: [] };
      return { targets: [{ id: user.username, name: `DeviantArt: ${user.username}` }] };
    } catch {
      return { targets: [] };
    }
  }

  async getLimits(): Promise<ConnectorLimits> {
    return limitsFromSpec(DEVIANTART_SPEC);
  }

  async deliver(ctx: ConnectorContext, post: Post): Promise<DeliverResult> {
    try {
      const secret = parseSecret(ctx);
      const config = parseConfig(ctx);
      const input = validate(post, config);

      const bytes = await this.mediaStore.fetch(input.media.container, input.media.key);
      const form = new FormData();
      if (input.title) form.set("title", input.title);
      if (post.body) form.set("artist_comments", post.body);
      input.tags.forEach((tag, i) => form.set(`tags[${i}]`, tag));
      for (const [name, value] of input.aiFlags) form.set(name, value);
      form.set("file", new Blob([bytes], { type: input.media.contentType }), basename(input.media.key) || "image");
      const item = await this.api<{ itemid?: number }>(secret, "/stash/submit", form);
      if (item.itemid === undefined) throw new Error("DeviantArt Sta.sh upload returned no item id");

      if (config.Mode === "stash") return { success: true, externalId: String(item.itemid) };

      const publish = new URLSearchParams({ itemid: String(item.itemid), is_mature: input.matureLevel ? "1" : "0" });
      if (input.matureLevel) publish.set("mature_level", input.matureLevel);
      publish.set("allow_comments", config.AllowComments === "false" ? "0" : "1");
      for (const id of input.galleryIds) publish.append("galleryids[]", id);
      for (const tag of input.tags) publish.append("tags[]", tag);
      for (const [name, value] of input.aiFlags) publish.set(name, value);
      // The item is already in Sta.sh, so a retry after a failure here uploads it again.
      const deviation = await this.api<{ deviationid?: string; url?: string }>(secret, "/stash/publish", publish);
      if (!deviation.deviationid) throw new Error("DeviantArt publish returned no deviation id");
      return { success: true, externalId: deviation.deviationid, externalUrl: deviation.url };
    } catch (error) {
      return { success: false, error: describeError(error) };
    }
  }

  /** Renews the access token (see core's refresh-before-use and token-refresh sweeper). Null means reconnect. */
  async refresh(ctx: ConnectorContext): Promise<{ secretJson: string } | null> {
    if (ctx.secretJson === null) return null;
    const secret = JSON.parse(ctx.secretJson) as Partial<DeviantartSecret>;
    if (!secret?.AccessToken || !secret.RefreshToken) return null;
    try {
      const next = await this.provider(ctx.operationalSecretJson).refresh(secret as DeviantartSecret);
      return { secretJson: JSON.stringify(next) };
    } catch (error) {
      if (error instanceof DeviantartGrantRejectedError) return null;
      throw error;
    }
  }

  private whoami(secret: DeviantartSecret): Promise<{ username?: string }> {
    return this.api(secret, "/user/whoami");
  }

  private async api<T = unknown>(secret: DeviantartSecret, path: string, body?: FormData | URLSearchParams): Promise<T> {
    const res = await this.fetchImpl(`${API_URL}${path}`, {
      method: body ? "POST" : "GET",
      headers: { authorization: `Bearer ${secret.AccessToken}`, accept: "application/json" },
      body,
    });
    const text = await res.text();
    if (res.status === 401) throw new Error("DeviantArt rejected the access token; reconnect the account");
    if (!res.ok) throw new Error(`DeviantArt ${path} failed with HTTP ${res.status}${describeApiError(text)}`);
    return (text ? JSON.parse(text) : {}) as T;
  }

  private provider(operationalSecretJson?: string | null): DeviantartOAuth2Provider {
    return new DeviantartOAuth2Provider(this.resolveApp(operationalSecretJson), this.fetchImpl);
  }

  private resolveApp(raw?: string | null): DeviantartAppCredentials {
    if (raw) {
      const parsed = JSON.parse(raw) as Partial<DeviantartAppCredentials>;
      if (parsed.clientId && parsed.clientSecret) return { clientId: parsed.clientId, clientSecret: parsed.clientSecret };
    }
    if (this.options.app) return this.options.app;
    throw new Error("DeviantArt app credentials not configured in the operational secret store");
  }
}

/** Checks everything the API would reject, so a bad post fails before anything is uploaded. */
function validate(post: Post, config: DeviantartConfig) {
  // A Sta.sh item holds one file, so a multi-image post uses its primary image.
  const media = post.media.find((item) => item.isDefault) ?? post.media[0];
  if (!media) throw new Error("DeviantArt submissions require an image");
  if (!DEVIANTART_SPEC.image.allowedMimeTypes.includes(media.contentType.toLowerCase()))
    throw new Error("DeviantArt integration currently supports JPEG, PNG, and GIF image uploads");
  if (!post.rating) throw new Error("DeviantArt requires a content rating");
  if (config.Mode && config.Mode !== "stash") throw new Error(`invalid DeviantArt mode '${config.Mode}'`);

  // Tags take letters, numbers and underscores only.
  const tags = [
    ...new Set(post.tags.map((tag) => tag.trim().replace(/\s+/g, "_").replace(/[^\p{L}\p{N}_]/gu, "")).filter(Boolean)),
  ];
  const galleryIds = (config.GalleryIds ?? "")
    .split(",")
    .map((id) => id.trim())
    .filter(Boolean);
  const aiFlags: [string, string][] = [];
  if (config.NoAi) aiFlags.push(["noai", config.NoAi === "true" ? "1" : "0"]);
  if (config.AiGenerated) aiFlags.push(["is_ai_generated", config.AiGenerated === "true" ? "1" : "0"]);

  return { media, title: post.title?.trim() ?? "", tags, galleryIds, aiFlags, matureLevel: MATURE_LEVELS[post.rating] };
}

function parseSecret(ctx: ConnectorContext): DeviantartSecret {
  if (ctx.secretJson === null) throw new Error("missing DeviantArt credentials — reconnect the account");
  const secret = JSON.parse(ctx.secretJson) as Partial<DeviantartSecret>;
  if (!secret?.AccessToken) throw new Error("missing DeviantArt access token — reconnect the account");
  return secret as DeviantartSecret;
}

function parseConfig(ctx: ConnectorContext): DeviantartConfig {
  try {
    return JSON.parse(ctx.configJson || "{}") as DeviantartConfig;
  } catch {
    throw new Error("invalid DeviantArt config JSON");
  }
}

/** DeviantArt errors carry an `error_description`; validation failures a field → message map. */
function describeApiError(body: string): string {
  let parsed: DeviantartError;
  try {
    parsed = JSON.parse(body) as DeviantartError;
  } catch {
    return "";
  }
  const fields = Object.entries(parsed.error_details ?? {}).map(([field, message]) => `${field}: ${message}`);
  const detail = fields.length > 0 ? fields.join("; ") : parsed.error_description;
  return detail ? `: ${detail}` : "";
}
