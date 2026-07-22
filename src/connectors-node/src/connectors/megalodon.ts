import { randomUUID } from "node:crypto";
import { createReadStream } from "node:fs";
import { mkdtemp, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { basename, join } from "node:path";
import generator, { detector } from "megalodon";
import { describeError } from "./errors.js";
import { mediaStoreFromEnv, type MediaStore } from "../media-store.js";
import type {
  Connector,
  ConnectorContext,
  ConnectorLimits,
  DeliverResult,
  IsAuthenticatedResult,
  ListTargetsResult,
  OAuthCompleteResult,
  OAuthProvider,
  OAuthStartResult,
  Post,
  PostMedia,
} from "../types.js";

/** SNS types megalodon's `generator` accepts. Iceshrimp is served by the `firefish` driver. */
export type MegalodonSns =
  | "mastodon"
  | "pleroma"
  | "friendica"
  | "firefish"
  | "gotosocial"
  | "pixelfed";

/** Minimal surface of the megalodon client this connector relies on. */
export interface MegalodonClientLike {
  registerApp(
    clientName: string,
    options: { scopes?: string[]; redirect_uris?: string; website?: string },
  ): Promise<{
    client_id: string;
    client_secret: string;
    url: string | null;
    session_token: string | null;
  }>;
  fetchAccessToken(
    clientId: string | null,
    clientSecret: string,
    codeOrSessionToken: string,
    redirectUri?: string,
  ): Promise<{ access_token: string }>;
  verifyAccountCredentials(): Promise<{ data: { acct?: string; username?: string; url?: string } }>;
  uploadMedia(
    file: unknown,
    options?: { description?: string },
  ): Promise<{ data: { id: string } }>;
  /**
   * Sets a drive file's alt text (`comment`). Present only on the firefish (Iceshrimp/Firefish)
   * client: megalodon's firefish uploadMedia/updateMedia both drop the description, so alt text has
   * to be applied via /api/drive/files/update directly. Mastodon-style clients set it at upload
   * time and leave this undefined.
   */
  setMediaComment?(fileId: string, comment: string): Promise<void>;
  postStatus(
    status: string,
    options?: {
      media_ids?: string[];
      spoiler_text?: string;
      sensitive?: boolean;
      visibility?: string;
    },
  ): Promise<{ data: { id?: string; url?: string | null; uri?: string | null } }>;
  getInstance(): Promise<{
    data: {
      configuration?: {
        statuses?: { max_characters?: number; max_media_attachments?: number };
        media_attachments?: {
          supported_mime_types?: string[];
          image_size_limit?: number;
          video_size_limit?: number;
        };
      };
    };
  }>;
}

/** Produces a megalodon client for an SNS + instance, optionally authenticated. */
export type MegalodonClientFactory = (
  sns: MegalodonSns,
  baseUrl: string,
  accessToken?: string | null,
) => MegalodonClientLike;

/** Resolves an instance URL to its SNS type (megalodon's nodeinfo probe). */
export type SnsDetector = (url: string) => Promise<MegalodonSns>;

const APP_NAME = "PostyFox";
const APP_WEBSITE = "https://postyfox.com";
/** Coarse Mastodon-style OAuth2 scopes (Mastodon, Pleroma, Friendica, GoToSocial, Pixelfed). */
const MASTODON_SCOPES = ["read", "write"];

/**
 * Misskey-family (Iceshrimp/Firefish) authorize *granular* permissions via MiAuth. The coarse
 * Mastodon "read"/"write" strings are not recognised as Misskey permissions, so an app registered
 * with them cannot create notes — /api/notes/create returns PERMISSION_DENIED. This mirrors
 * megalodon's firefish DEFAULT_SCOPE (the permission set its endpoints expect); "write:notes" and
 * "read/write:drive" are what posting + media upload actually need.
 */
const FIREFISH_SCOPES = [
  "read:account",
  "write:account",
  "read:blocks",
  "write:blocks",
  "read:drive",
  "write:drive",
  "read:favorites",
  "write:favorites",
  "read:following",
  "write:following",
  "read:mutes",
  "write:mutes",
  "write:notes",
  "read:notifications",
  "write:notifications",
  "read:reactions",
  "write:reactions",
  "write:votes",
];

/** MiAuth (firefish) needs granular permissions; Mastodon-style OAuth2 uses coarse scopes. */
function scopesForSns(sns: MegalodonSns): string[] {
  return sns === "firefish" ? FIREFISH_SCOPES : MASTODON_SCOPES;
}

interface MegalodonConfig {
  InstanceUrl: string;
}

interface MegalodonSecret {
  AccessToken: string;
  /** SNS resolved during the connect flow, so delivery need not re-probe the instance. */
  Sns?: MegalodonSns;
}

/** Transient blob round-tripped through the OAuth "connect" flow (carried in requestTokenSecret). */
interface PendingConnect {
  instanceUrl: string;
  sns: MegalodonSns;
  clientId: string;
  clientSecret: string;
  /** Firefish/Misskey (MiAuth): the session token is the credential exchanged for the access token. */
  sessionToken: string | null;
  callbackUrl: string;
}

const defaultClientFactory: MegalodonClientFactory = (sns, baseUrl, accessToken) => {
  const client = generator(sns, baseUrl, accessToken ?? null) as unknown as MegalodonClientLike;
  if (sns === "firefish") {
    // megalodon's firefish driver silently drops the alt text on both uploadMedia and updateMedia,
    // so set the drive file's `comment` via the Misskey-native endpoint (uses the same write:drive
    // permission the upload needed). Only wired up for firefish; other drivers honour the upload
    // description and leave setMediaComment undefined.
    const apiBase = baseUrl.replace(/\/+$/, "");
    client.setMediaComment = async (fileId, comment) => {
      const resp = await fetch(`${apiBase}/api/drive/files/update`, {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify({ i: accessToken, fileId, comment }),
      });
      if (!resp.ok) {
        const body = await resp.text().catch(() => "");
        throw new Error(`drive/files/update failed: HTTP ${resp.status}${body ? `: ${body}` : ""}`);
      }
    };
  }
  return client;
};

const defaultDetector: SnsDetector = (url) => detector(url) as Promise<MegalodonSns>;

/**
 * Generic Fediverse connector backed by megalodon. One instance serves a single Fediverse platform
 * (e.g. Iceshrimp → the `firefish` driver); the SNS is auto-detected at connect time and cached in
 * the secret. Authorization spans two provider families that megalodon abstracts behind the same
 * calls but drive differently:
 *   - Mastodon-style OAuth2: the callback returns a `code` which is exchanged for a token.
 *   - Firefish/Misskey MiAuth: `registerApp` yields a session token up front; after the user
 *     authorizes, that same session token (not a callback code) is exchanged for a token.
 * The `oauth` flow captures both by carrying the session token (when present) through
 * `requestTokenSecret` and using it in place of a callback code.
 */
export class MegalodonConnector implements Connector {
  readonly oauth: OAuthProvider;

  constructor(
    /** Fallback SNS when nodeinfo detection fails (e.g. "firefish" for Iceshrimp). */
    private readonly defaultSns: MegalodonSns,
    private readonly mediaStore: MediaStore = mediaStoreFromEnv(),
    private readonly clientFactory: MegalodonClientFactory = defaultClientFactory,
    private readonly detect: SnsDetector = defaultDetector,
  ) {
    this.oauth = {
      startAuthorization: (input) => this.startAuthorization(input),
      completeAuthorization: (input) => this.completeAuthorization(input),
    };
  }

  private async resolveSns(instanceUrl: string): Promise<MegalodonSns> {
    try {
      return await this.detect(instanceUrl);
    } catch {
      return this.defaultSns;
    }
  }

  private async startAuthorization({
    callbackUrl,
    configJson,
  }: {
    callbackUrl: string;
    configJson?: string;
  }): Promise<OAuthStartResult> {
    if (!configJson) throw new Error("missing connector config (InstanceUrl) for OAuth start");
    const config = JSON.parse(configJson) as MegalodonConfig;
    const instanceUrl = normalizeInstanceUrl(config?.InstanceUrl);
    if (!instanceUrl) throw new Error("missing InstanceUrl in config");

    const sns = await this.resolveSns(instanceUrl);
    const client = this.clientFactory(sns, instanceUrl);
    const app = await client.registerApp(APP_NAME, {
      scopes: scopesForSns(sns),
      redirect_uris: callbackUrl,
      website: APP_WEBSITE,
    });
    if (!app.url) throw new Error("provider did not return an authorization URL");

    const sessionToken = app.session_token ?? null;
    // Correlation key surfaced to core: for MiAuth we key on the session token; otherwise a random
    // state we append to the authorize URL so the provider echoes it back on the callback.
    const requestToken = sessionToken ?? randomUUID();
    const authorizeUrl = sessionToken ? app.url : appendQueryParam(app.url, "state", requestToken);

    const pending: PendingConnect = {
      instanceUrl,
      sns,
      clientId: app.client_id,
      clientSecret: app.client_secret,
      sessionToken,
      callbackUrl,
    };
    return { authorizeUrl, requestToken, requestTokenSecret: JSON.stringify(pending) };
  }

  private async completeAuthorization({
    requestTokenSecret,
    verifier,
  }: {
    requestToken: string;
    requestTokenSecret: string;
    verifier: string;
  }): Promise<OAuthCompleteResult> {
    const pending = JSON.parse(requestTokenSecret) as PendingConnect;
    const client = this.clientFactory(pending.sns, pending.instanceUrl);
    // MiAuth exchanges the stored session token; OAuth2 exchanges the callback `code` (verifier).
    const credential = pending.sessionToken ?? verifier;
    if (!credential) throw new Error("no authorization code or session token to exchange");
    const token = await client.fetchAccessToken(
      pending.clientId,
      pending.clientSecret,
      credential,
      pending.callbackUrl,
    );
    if (!token?.access_token) throw new Error("provider did not return an access token");
    const secret: MegalodonSecret = { AccessToken: token.access_token, Sns: pending.sns };
    return { secretJson: JSON.stringify(secret) };
  }

  private parse(ctx: ConnectorContext): { instanceUrl: string; token: string; sns: MegalodonSns } {
    const config = JSON.parse(ctx.configJson) as MegalodonConfig;
    const instanceUrl = normalizeInstanceUrl(config?.InstanceUrl);
    if (!instanceUrl) throw new Error("missing InstanceUrl in config");
    if (ctx.secretJson === null) throw new Error("missing access token — reconnect the account");
    const secret = JSON.parse(ctx.secretJson) as MegalodonSecret;
    if (!secret?.AccessToken) throw new Error("missing access token — reconnect the account");
    return { instanceUrl, token: secret.AccessToken, sns: secret.Sns ?? this.defaultSns };
  }

  async isAuthenticated(ctx: ConnectorContext): Promise<IsAuthenticatedResult> {
    try {
      const { instanceUrl, token, sns } = this.parse(ctx);
      const client = this.clientFactory(sns, instanceUrl, token);
      await client.verifyAccountCredentials();
      return { isAuthenticated: true };
    } catch (err) {
      return { isAuthenticated: false, detail: describeError(err) };
    }
  }

  async listTargets(ctx: ConnectorContext): Promise<ListTargetsResult> {
    try {
      const { instanceUrl, token, sns } = this.parse(ctx);
      const client = this.clientFactory(sns, instanceUrl, token);
      const me = await client.verifyAccountCredentials();
      const host = hostOf(instanceUrl);
      const acct = me.data.acct ?? me.data.username ?? host;
      const id = me.data.url ?? `${instanceUrl}/@${acct}`;
      return { targets: [{ id, name: `@${acct}@${host}` }] };
    } catch {
      return { targets: [] };
    }
  }

  /**
   * Reports the instance's live limits (character + attachment caps). Only needs the instance URL —
   * `/api/v1/instance` is public — so it works before the account is connected. The SNS driver is
   * taken from the stored secret when present, else the fallback.
   */
  async getLimits(ctx: ConnectorContext): Promise<ConnectorLimits> {
    const config = JSON.parse(ctx.configJson) as MegalodonConfig;
    const instanceUrl = normalizeInstanceUrl(config?.InstanceUrl);
    if (!instanceUrl) throw new Error("missing InstanceUrl in config");
    let sns = this.defaultSns;
    if (ctx.secretJson) {
      try {
        const secret = JSON.parse(ctx.secretJson) as MegalodonSecret;
        if (secret?.Sns) sns = secret.Sns;
      } catch {
        // Ignore an unparseable secret; the fallback SNS is fine for a public instance probe.
      }
    }
    return this.fetchLimits(this.clientFactory(sns, instanceUrl));
  }

  private async fetchLimits(client: MegalodonClientLike): Promise<ConnectorLimits> {
    const res = await client.getInstance();
    const config = res.data?.configuration;
    const statuses = config?.statuses;
    const media = config?.media_attachments;
    return {
      maxContentLength: statuses?.max_characters ?? null,
      maxMediaAttachments: statuses?.max_media_attachments ?? null,
      supportedMimeTypes: media?.supported_mime_types ?? null,
      imageSizeLimit: media?.image_size_limit ?? null,
      videoSizeLimit: media?.video_size_limit ?? null,
    };
  }

  async deliver(ctx: ConnectorContext, post: Post): Promise<DeliverResult> {
    try {
      const { instanceUrl, token, sns } = this.parse(ctx);
      const client = this.clientFactory(sns, instanceUrl, token);

      // Enforce the instance's real limits up front and fail clearly — before uploading media or
      // posting — so nothing is silently truncated or dropped.
      const media = post.media ?? [];
      const status = composeStatus(post);
      const limits = await this.fetchLimits(client);
      const length = [...status].length;
      if (limits.maxContentLength !== null && length > limits.maxContentLength) {
        throw new Error(
          `post is ${length} characters but this instance allows at most ${limits.maxContentLength}`,
        );
      }
      if (limits.maxMediaAttachments !== null && media.length > limits.maxMediaAttachments) {
        throw new Error(
          `post has ${media.length} attachments but this instance allows at most ${limits.maxMediaAttachments}`,
        );
      }

      // Fetch + validate every item (MIME type, file size) before uploading any, so a rejected item
      // fails the whole delivery cleanly rather than leaving orphaned uploads on the instance.
      const resolved = await this.resolveMedia(media, limits);
      const mediaIds = await this.uploadResolved(client, resolved);
      const result = await client.postStatus(status, {
        media_ids: mediaIds.length > 0 ? mediaIds : undefined,
        spoiler_text: post.title ?? undefined,
        visibility: "public",
      });

      const externalId = result.data.id !== undefined ? String(result.data.id) : undefined;
      const externalUrl = result.data.url ?? result.data.uri ?? undefined;
      return { success: true, externalId, externalUrl };
    } catch (err) {
      return { success: false, error: describeError(err) };
    }
  }

  /** Fetches each item's bytes and checks it against the instance's media limits (fails clearly). */
  private async resolveMedia(
    media: PostMedia[],
    limits: ConnectorLimits,
  ): Promise<{ item: PostMedia; bytes: Buffer }[]> {
    const resolved: { item: PostMedia; bytes: Buffer }[] = [];
    for (const item of media) {
      const bytes = await this.mediaStore.fetch(item.container, item.key);
      assertMediaAllowed(item, bytes, limits);
      resolved.push({ item, bytes });
    }
    return resolved;
  }

  /**
   * Stages each resolved item to a short-lived temp file and uploads it as a ReadStream — megalodon's
   * multipart upload needs a filename, which a bare Buffer would not carry. Always cleaned up.
   */
  private async uploadResolved(
    client: MegalodonClientLike,
    resolved: { item: PostMedia; bytes: Buffer }[],
  ): Promise<string[]> {
    if (resolved.length === 0) return [];
    const dir = await mkdtemp(join(tmpdir(), "megalodon-media-"));
    try {
      const ids: string[] = [];
      for (let i = 0; i < resolved.length; i++) {
        const { item, bytes } = resolved[i];
        const filePath = join(dir, basename(item.key) || `image-${i}`);
        await writeFile(filePath, bytes);
        const uploaded = await client.uploadMedia(createReadStream(filePath), {
          description: item.alt ?? undefined,
        });
        // Where the driver drops the upload description (firefish), apply alt text explicitly.
        if (item.alt && client.setMediaComment) {
          await client.setMediaComment(uploaded.data.id, item.alt);
        }
        ids.push(uploaded.data.id);
      }
      return ids;
    } finally {
      await rm(dir, { recursive: true, force: true });
    }
  }
}

/** Fails clearly when a media item violates the instance's MIME-type or file-size limits. */
function assertMediaAllowed(item: PostMedia, bytes: Buffer, limits: ConnectorLimits): void {
  const type = item.contentType;
  if (limits.supportedMimeTypes && limits.supportedMimeTypes.length > 0
    && !limits.supportedMimeTypes.includes(type)) {
    throw new Error(`media type ${type} is not supported by this instance`);
  }
  // Mastodon groups audio under the video size limit; images use the image limit.
  const isVideoOrAudio = type.startsWith("video/") || type.startsWith("audio/");
  const sizeLimit = isVideoOrAudio ? limits.videoSizeLimit : limits.imageSizeLimit;
  if (sizeLimit !== null && bytes.length > sizeLimit) {
    const name = basename(item.key) || item.key;
    throw new Error(`media ${name} is ${bytes.length} bytes but this instance allows at most ${sizeLimit}`);
  }
}

/** Fediverse has no separate tags field; append them as hashtags, as clients conventionally expect. */
function composeStatus(post: Post): string {
  const tagLine = (post.tags ?? [])
    .map((t) => t.trim())
    .filter(Boolean)
    .map((t) => (t.startsWith("#") ? t : `#${t}`))
    .join(" ");
  return tagLine ? `${post.body}\n\n${tagLine}` : post.body;
}

function normalizeInstanceUrl(raw: string | undefined): string | undefined {
  if (!raw) return undefined;
  const trimmed = raw.trim().replace(/\/+$/, "");
  if (!trimmed) return undefined;
  return /^https?:\/\//i.test(trimmed) ? trimmed : `https://${trimmed}`;
}

function hostOf(instanceUrl: string): string {
  try {
    return new URL(instanceUrl).host;
  } catch {
    return instanceUrl.replace(/^https?:\/\//i, "");
  }
}

function appendQueryParam(url: string, key: string, value: string): string {
  const sep = url.includes("?") ? "&" : "?";
  return `${url}${sep}${encodeURIComponent(key)}=${encodeURIComponent(value)}`;
}

