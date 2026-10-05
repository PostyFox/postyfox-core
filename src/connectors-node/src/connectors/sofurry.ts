import { basename } from "node:path";
import { describeError } from "./errors.js";
import {
  SofurryGrantRejectedError,
  SofurryOAuth2Provider,
  type SofurryAppCredentials,
  type SofurrySecret,
} from "./sofurry-oauth.js";
import { mediaStoreFromEnv, type MediaStore } from "../media-store.js";
import { SOFURRY_SPEC, limitsFromSpec } from "../media/specs.js";
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

const API_URL = "https://api.sofurry.com";
const SITE_URL = "https://sofurry.com";
const MAX_TITLE_LENGTH = 255;
const MAX_DESCRIPTION_LENGTH = 2000;
const MAX_TAGS = 100;
const DEFAULT_TYPE = 11; // Artwork: Drawing
const IMAGE_TYPES = [11, 12, 13, 19, 31, 32, 39];
const PUBLIC = 3;
const RATINGS: Record<NonNullable<Post["rating"]>, number> = { general: 0, mature: 10, adult: 20, extreme: 20 };

/** Per-submission options (see sofurry-post-options.schema.json); values arrive as strings. */
interface SofurryConfig {
  Type?: string;
  Privacy?: string;
  FolderIds?: string;
  AllowComments?: string;
  AllowDownloads?: string;
  WorkInProgress?: string;
  PixelPerfect?: string;
}

interface SofurryUser {
  handle?: string;
  username?: string;
  isAdult?: boolean;
}

interface SofurryError {
  message?: string;
  description?: string;
  errors?: Record<string, string[]>;
}

export interface SofurryConnectorOptions {
  /** Fallback app credentials; normally they come from the operational secret store per request. */
  app?: SofurryAppCredentials;
  mediaStore?: MediaStore;
  fetch?: typeof fetch;
}

/**
 * SoFurry's documented public API (developer.sofurry.com/dev-docs), authorised through an OAuth2
 * connect flow. A post becomes one submission: create an empty draft, attach each image as content,
 * then set its metadata, which publishes it, and finally file it into any chosen folders.
 */
export class SofurryConnector implements Connector {
  readonly oauth: OAuthProvider;
  private readonly mediaStore: MediaStore;
  private readonly fetchImpl: typeof fetch;

  constructor(private readonly options: SofurryConnectorOptions = {}) {
    this.mediaStore = options.mediaStore ?? mediaStoreFromEnv();
    this.fetchImpl = options.fetch ?? fetch;
    this.oauth = {
      startAuthorization: async (input) => this.provider(input.operationalSecretJson).startAuthorization(input),
      completeAuthorization: async (input) => this.provider(input.operationalSecretJson).completeAuthorization(input),
    };
  }

  async isAuthenticated(ctx: ConnectorContext): Promise<IsAuthenticatedResult> {
    try {
      const user = await this.currentUser(parseSecret(ctx));
      return { isAuthenticated: true, detail: user.username ?? user.handle };
    } catch (error) {
      return { isAuthenticated: false, detail: describeError(error) };
    }
  }

  async listTargets(ctx: ConnectorContext): Promise<ListTargetsResult> {
    try {
      const user = await this.currentUser(parseSecret(ctx));
      const handle = user.handle ?? user.username;
      if (!handle) return { targets: [] };
      return { targets: [{ id: handle, name: `SoFurry: ${user.username ?? handle}` }] };
    } catch {
      return { targets: [] };
    }
  }

  async getLimits(): Promise<ConnectorLimits> {
    return limitsFromSpec(SOFURRY_SPEC);
  }

  async deliver(ctx: ConnectorContext, post: Post): Promise<DeliverResult> {
    try {
      const secret = parseSecret(ctx);
      const config = parseConfig(ctx);
      const input = this.validate(post, config);

      const user = await this.currentUser(secret);
      if (input.rating > 0 && user.isAdult === false)
        throw new Error("SoFurry only allows Clean submissions until adult content is enabled on the account");
      if (input.folderIds.length > 0) await this.checkFolders(secret, input.folderIds);

      // Steps 1-3 of the documented creation workflow. The draft stays private until step 3.
      const draft = await this.api<{ id?: string }>(secret, "/v1/submission", { method: "PUT", json: {} });
      if (!draft.id) throw new Error("SoFurry created no submission id");

      const contentOrder: string[] = [];
      for (const media of post.media) {
        const bytes = await this.mediaStore.fetch(media.container, media.key);
        const form = new FormData();
        form.set("file", new Blob([bytes], { type: media.contentType }), basename(media.key) || "image");
        const content = await this.api<{ contentId?: string }>(secret, `/v1/submission/${draft.id}/content`, {
          method: "POST",
          body: form,
        });
        if (!content.contentId) throw new Error("SoFurry image upload returned no content id");
        contentOrder.push(content.contentId);
      }

      await this.api(secret, `/v1/submission/${draft.id}`, {
        method: "POST",
        json: {
          title: input.title,
          description: post.body,
          category: Math.floor(input.type / 10) * 10,
          type: input.type,
          rating: input.rating,
          privacy: input.privacy,
          allowComments: config.AllowComments !== "false",
          allowDownloads: config.AllowDownloads !== "false",
          isWip: config.WorkInProgress === "true",
          optimize: true,
          pixelPerfect: config.PixelPerfect === "true",
          isAdvert: false,
          canPurchase: false,
          artistTags: input.tags,
          contentOrder,
        },
      });

      // The submission is already published, so a folder that fails now must not fail the delivery
      // (a retry would post a duplicate). The ids were checked against the account's folders above.
      for (const folderId of input.folderIds) {
        await this.api(secret, `/v1/folder/${encodeURIComponent(folderId)}/${draft.id}`, { method: "POST" }).catch(
          () => undefined,
        );
      }

      return { success: true, externalId: draft.id, externalUrl: `${SITE_URL}/s/${draft.id}` };
    } catch (error) {
      return { success: false, error: describeError(error) };
    }
  }

  /** Renews the access token ahead of expiry (see the core token-refresh sweeper). Null means reconnect. */
  async refresh(ctx: ConnectorContext): Promise<{ secretJson: string } | null> {
    if (ctx.secretJson === null) return null;
    const secret = JSON.parse(ctx.secretJson) as Partial<SofurrySecret>;
    if (!secret?.AccessToken || !secret.RefreshToken) return null;
    try {
      const next = await this.provider(ctx.operationalSecretJson).refresh(secret as SofurrySecret);
      return { secretJson: JSON.stringify(next) };
    } catch (error) {
      if (error instanceof SofurryGrantRejectedError) return null;
      throw error;
    }
  }

  /** Checks everything the API would reject, so a bad post fails before a draft is created. */
  private validate(post: Post, config: SofurryConfig) {
    const title = post.title?.trim() ?? "";
    if (!title) throw new Error("SoFurry submissions require a title");
    if (title.length > MAX_TITLE_LENGTH)
      throw new Error(`SoFurry titles must be under ${MAX_TITLE_LENGTH} characters`);
    if (post.body.length > MAX_DESCRIPTION_LENGTH)
      throw new Error(`SoFurry descriptions are limited to ${MAX_DESCRIPTION_LENGTH} characters`);
    if (post.media.length === 0) throw new Error("SoFurry submissions require at least one image");
    if (post.media.length > (SOFURRY_SPEC.maxAttachments ?? 0))
      throw new Error(`SoFurry submissions take at most ${SOFURRY_SPEC.maxAttachments} images`);
    for (const media of post.media)
      if (!SOFURRY_SPEC.image.allowedMimeTypes.includes(media.contentType.toLowerCase()))
        throw new Error("SoFurry integration currently supports JPEG, PNG, GIF, and WebP image uploads");
    if (!post.rating) throw new Error("SoFurry requires a content rating");

    const tags = [...new Set(post.tags.map((tag) => tag.trim()).filter(Boolean))];
    if (tags.length > MAX_TAGS) throw new Error(`SoFurry allows at most ${MAX_TAGS} tags`);

    const type = config.Type ? Number(config.Type) : DEFAULT_TYPE;
    if (!IMAGE_TYPES.includes(type)) throw new Error(`invalid SoFurry type '${config.Type}'`);
    const privacy = config.Privacy ? Number(config.Privacy) : PUBLIC;
    if (![1, 2, 3].includes(privacy)) throw new Error(`invalid SoFurry visibility '${config.Privacy}'`);
    const folderIds = (config.FolderIds ?? "")
      .split(",")
      .map((id) => id.trim())
      .filter(Boolean);

    return { title, tags, type, privacy, folderIds, rating: RATINGS[post.rating] };
  }

  private async checkFolders(secret: SofurrySecret, folderIds: string[]): Promise<void> {
    const folders = await this.api<{ id: string }[]>(secret, "/v1/folders");
    const known = new Set(folders.map((folder) => folder.id));
    const missing = folderIds.filter((id) => !known.has(id));
    if (missing.length > 0) throw new Error(`SoFurry folder not found: ${missing.join(", ")}`);
  }

  private currentUser(secret: SofurrySecret): Promise<SofurryUser> {
    return this.api<SofurryUser>(secret, "/v1/user/me");
  }

  private async api<T = unknown>(
    secret: SofurrySecret,
    path: string,
    request: { method?: string; json?: unknown; body?: FormData } = {},
  ): Promise<T> {
    const headers: Record<string, string> = {
      authorization: `Bearer ${secret.AccessToken}`,
      accept: "application/json",
    };
    if (request.json !== undefined) headers["content-type"] = "application/json";
    const res = await this.fetchImpl(`${API_URL}${path}`, {
      method: request.method ?? "GET",
      headers,
      body: request.json !== undefined ? JSON.stringify(request.json) : request.body,
      // An unauthenticated request is redirected to the login page rather than answered with a 401.
      redirect: "manual",
    });
    const text = await res.text();
    if (res.status === 401 || (res.status >= 300 && res.status < 400))
      throw new Error("SoFurry rejected the access token; reconnect the account");
    if (!res.ok) throw new Error(`SoFurry ${path} failed with HTTP ${res.status}${describeApiError(text)}`);
    return (text ? JSON.parse(text) : {}) as T;
  }

  private provider(operationalSecretJson?: string | null): SofurryOAuth2Provider {
    return new SofurryOAuth2Provider(this.resolveApp(operationalSecretJson), this.fetchImpl);
  }

  private resolveApp(raw?: string | null): SofurryAppCredentials {
    if (raw) {
      const parsed = JSON.parse(raw) as Partial<SofurryAppCredentials>;
      if (parsed.clientId && parsed.clientSecret) return { clientId: parsed.clientId, clientSecret: parsed.clientSecret };
    }
    if (this.options.app) return this.options.app;
    throw new Error("SoFurry app credentials not configured in the operational secret store");
  }
}

function parseSecret(ctx: ConnectorContext): SofurrySecret {
  if (ctx.secretJson === null) throw new Error("missing SoFurry credentials — reconnect the account");
  const secret = JSON.parse(ctx.secretJson) as Partial<SofurrySecret>;
  if (!secret?.AccessToken) throw new Error("missing SoFurry access token — reconnect the account");
  return secret as SofurrySecret;
}

function parseConfig(ctx: ConnectorContext): SofurryConfig {
  try {
    return JSON.parse(ctx.configJson || "{}") as SofurryConfig;
  } catch {
    throw new Error("invalid SoFurry config JSON");
  }
}

/** SoFurry errors carry a `description`; validation failures a field → messages map. */
function describeApiError(body: string): string {
  let parsed: SofurryError;
  try {
    parsed = JSON.parse(body) as SofurryError;
  } catch {
    return "";
  }
  const fields = Object.entries(parsed.errors ?? {}).flatMap(([field, messages]) =>
    messages.map((message) => `${field}: ${message}`),
  );
  const detail = fields.length > 0 ? fields.join("; ") : parsed.description ?? parsed.message;
  return detail ? `: ${detail}` : "";
}
