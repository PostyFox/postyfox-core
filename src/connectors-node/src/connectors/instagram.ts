import { randomUUID } from "node:crypto";
import { describeError } from "./errors.js";
import { InstagramOAuth2Provider, refreshLongLivedToken, type InstagramAppCredentials, type InstagramSecret } from "./instagram-oauth.js";
import { mediaStoreFromEnv, type MediaStore } from "../media-store.js";
import { normalizeMedia } from "../media/normalize.js";
import { INSTAGRAM_SPEC, limitsFromSpec } from "../media/specs.js";
import type {
  Connector,
  ConnectorContext,
  ConnectorLimits,
  DeliverResult,
  IsAuthenticatedResult,
  ListTargetsResult,
  OAuthProvider,
  Post,
  PostMedia,
} from "../types.js";

const GRAPH_URL = "https://graph.instagram.com";
// The presigned URL only needs to live long enough for Meta's servers to fetch it once, plus
// however long a video container takes to process; generous but still short-lived.
const PRESIGN_EXPIRY_SECONDS = 15 * 60;
const VIDEO_POLL_INTERVAL_MS = 3000;
const VIDEO_POLL_TIMEOUT_MS = 5 * 60 * 1000;
/** Container the connector stages normalized bytes in so Instagram can fetch them by URL (see MediaStore.put/presignedGetUrl). */
const STAGING_CONTAINER = "instagram-staging";

// Business Login for Instagram carries no per-account config: the connect flow itself determines
// which IG business/creator account is linked (see InstagramSecret.IgUserId), so there is no
// InstagramConfig type to parse here (unlike Tumblr's Username or the Fediverse's InstanceUrl).

/** Rewrites a normalized MIME type to the file extension the staged object key carries. */
function extensionFor(mime: string): string {
  const ext: Record<string, string> = {
    "image/jpeg": ".jpg",
    "image/png": ".png",
    "video/mp4": ".mp4",
  };
  return ext[mime.toLowerCase()] ?? "";
}

function parseSecret(ctx: ConnectorContext): InstagramSecret {
  if (ctx.secretJson === null) throw new Error("missing Instagram credentials — reconnect the account");
  const secret = JSON.parse(ctx.secretJson) as Partial<InstagramSecret>;
  if (!secret?.AccessToken || !secret?.IgUserId) {
    throw new Error("missing Instagram access token — reconnect the account");
  }
  return { AccessToken: secret.AccessToken, IgUserId: secret.IgUserId, ExpiresAt: secret.ExpiresAt ?? "" };
}

interface MediaContainer {
  id: string;
}

async function graphFetch(path: string, params: Record<string, string>, method: "GET" | "POST" = "GET"): Promise<unknown> {
  const url = new URL(`${GRAPH_URL}${path}`);
  let init: RequestInit;
  if (method === "GET") {
    for (const [k, v] of Object.entries(params)) url.searchParams.set(k, v);
    init = { method: "GET" };
  } else {
    init = { method: "POST", headers: { "Content-Type": "application/x-www-form-urlencoded" }, body: new URLSearchParams(params) };
  }
  const res = await fetch(url.toString(), init);
  const text = await res.text();
  if (!res.ok) throw new Error(`Instagram API error (${res.status}): ${text.slice(0, 1000)}`);
  return text ? JSON.parse(text) : {};
}

export class InstagramConnector implements Connector {
  readonly oauth: OAuthProvider;

  constructor(
    private readonly app?: InstagramAppCredentials,
    private readonly mediaStore: MediaStore = mediaStoreFromEnv(),
    /** Injectable so tests don't wait out real video-processing polls. */
    private readonly videoPollIntervalMs: number = VIDEO_POLL_INTERVAL_MS,
  ) {
    this.oauth = {
      startAuthorization: (input) =>
        new InstagramOAuth2Provider(this.resolveApp(input.operationalSecretJson)).startAuthorization(input),
      completeAuthorization: (input) =>
        new InstagramOAuth2Provider(this.resolveApp(input.operationalSecretJson)).completeAuthorization(input),
    };
  }

  private resolveApp(raw?: string | null): InstagramAppCredentials {
    if (raw) {
      const parsed = JSON.parse(raw) as Partial<InstagramAppCredentials>;
      if (parsed.appId && parsed.appSecret) return { appId: parsed.appId, appSecret: parsed.appSecret };
    }
    if (this.app) return this.app;
    throw new Error("Instagram app credentials not configured in the operational secret store");
  }

  async isAuthenticated(ctx: ConnectorContext): Promise<IsAuthenticatedResult> {
    try {
      const secret = parseSecret(ctx);
      await graphFetch(`/${secret.IgUserId}`, { fields: "id,username", access_token: secret.AccessToken });
      return { isAuthenticated: true };
    } catch (err) {
      return { isAuthenticated: false, detail: describeError(err) };
    }
  }

  async listTargets(ctx: ConnectorContext): Promise<ListTargetsResult> {
    try {
      const secret = parseSecret(ctx);
      const me = (await graphFetch(`/${secret.IgUserId}`, { fields: "id,username", access_token: secret.AccessToken })) as {
        username?: string;
      };
      return { targets: [{ id: secret.IgUserId, name: me.username ? `@${me.username}` : secret.IgUserId }] };
    } catch {
      return { targets: [] };
    }
  }

  // Instagram's caps are fixed (not per-instance), so this needs no network call.
  async getLimits(): Promise<ConnectorLimits> {
    return limitsFromSpec(INSTAGRAM_SPEC);
  }

  async deliver(ctx: ConnectorContext, post: Post): Promise<DeliverResult> {
    const staged: { container: string; key: string }[] = [];
    try {
      const secret = parseSecret(ctx);
      const cap = INSTAGRAM_SPEC.maxAttachments ?? 10;
      const media = (post.media ?? []).slice(0, cap);
      if (media.length === 0) {
        // Belt-and-braces: core's RequiresMedia already rejects this at intake, but the connector
        // must not assume every caller goes through core (e.g. direct connectors-node testing).
        return { success: false, error: "Instagram requires at least one image or video" };
      }

      const caption = post.body;
      let creationId: string;
      if (media.length === 1) {
        creationId = await this.createItemContainer(secret, media[0], staged, caption);
      } else {
        const childIds: string[] = [];
        for (const item of media) {
          childIds.push(await this.createItemContainer(secret, item, staged, undefined, /* carousel item */ true));
        }
        const carousel = (await graphFetch(
          `/${secret.IgUserId}/media`,
          { media_type: "CAROUSEL", children: childIds.join(","), caption, access_token: secret.AccessToken },
          "POST",
        )) as MediaContainer;
        creationId = carousel.id;
      }

      const publishResult = (await graphFetch(
        `/${secret.IgUserId}/media_publish`,
        { creation_id: creationId, access_token: secret.AccessToken },
        "POST",
      )) as { id: string };

      const externalId = publishResult.id;
      const externalUrl = await this.permalinkFor(secret, externalId);
      return { success: true, externalId, externalUrl };
    } catch (err) {
      return { success: false, error: describeError(err) };
    } finally {
      for (const item of staged) {
        try {
          await this.mediaStore.delete(item.container, item.key);
        } catch {
          // Best-effort cleanup: an orphaned staging object is harmless (it just sits until
          // manually swept), never worth failing an otherwise-successful delivery over.
        }
      }
    }
  }

  /**
   * Normalizes one media item, stages it in the object store under a public presigned URL (see
   * {@link STAGING_CONTAINER}), and creates its container. `isCarouselItem` omits the caption (only
   * the parent carousel container carries it).
   */
  private async createItemContainer(
    secret: InstagramSecret,
    item: PostMedia,
    staged: { container: string; key: string }[],
    caption?: string,
    isCarouselItem = false,
  ): Promise<string> {
    const raw = await this.mediaStore.fetch(item.container, item.key);
    const normalized = await normalizeMedia(raw, item.contentType, INSTAGRAM_SPEC);
    const isVideo = normalized.contentType.startsWith("video/");
    const key = `${randomUUID()}${extensionFor(normalized.contentType)}`;
    await this.mediaStore.put(STAGING_CONTAINER, key, normalized.bytes, normalized.contentType);
    staged.push({ container: STAGING_CONTAINER, key });
    const url = await this.mediaStore.presignedGetUrl(STAGING_CONTAINER, key, PRESIGN_EXPIRY_SECONDS);

    const params: Record<string, string> = { access_token: secret.AccessToken };
    if (isVideo) {
      params.video_url = url;
      params.media_type = "REELS";
    } else {
      params.image_url = url;
    }
    if (isCarouselItem) params.is_carousel_item = "true";
    if (caption !== undefined) params.caption = caption;

    const container = (await graphFetch(`/${secret.IgUserId}/media`, params, "POST")) as MediaContainer;
    if (isVideo) await this.waitForVideoProcessing(secret, container.id);
    return container.id;
  }

  /** Video containers process asynchronously; publish fails until status_code is FINISHED. */
  private async waitForVideoProcessing(secret: InstagramSecret, containerId: string): Promise<void> {
    const deadline = Date.now() + VIDEO_POLL_TIMEOUT_MS;
    while (Date.now() < deadline) {
      const status = (await graphFetch(`/${containerId}`, {
        fields: "status_code",
        access_token: secret.AccessToken,
      })) as { status_code?: string };
      if (status.status_code === "FINISHED") return;
      if (status.status_code === "ERROR" || status.status_code === "EXPIRED") {
        throw new Error(`Instagram video processing failed (${status.status_code})`);
      }
      await new Promise((resolve) => setTimeout(resolve, this.videoPollIntervalMs));
    }
    throw new Error("Instagram video processing timed out");
  }

  /** Best-effort permalink lookup; delivery has already succeeded regardless of this outcome. */
  private async permalinkFor(secret: InstagramSecret, mediaId: string): Promise<string | undefined> {
    try {
      const media = (await graphFetch(`/${mediaId}`, {
        fields: "permalink",
        access_token: secret.AccessToken,
      })) as { permalink?: string };
      return media.permalink;
    } catch {
      return undefined;
    }
  }

  /** Refreshes the long-lived token ahead of its ~60-day expiry (see the core token-refresh sweeper). */
  async refresh(ctx: ConnectorContext): Promise<{ secretJson: string } | null> {
    if (ctx.secretJson === null) return null;
    const secret = JSON.parse(ctx.secretJson) as Partial<InstagramSecret>;
    if (!secret?.AccessToken || !secret?.IgUserId) return null;
    const refreshed = await refreshLongLivedToken(secret.AccessToken);
    const next: InstagramSecret = {
      AccessToken: refreshed.access_token,
      IgUserId: secret.IgUserId,
      ExpiresAt: new Date(Date.now() + refreshed.expires_in * 1000).toISOString(),
    };
    return { secretJson: JSON.stringify(next) };
  }
}
