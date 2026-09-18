import { basename } from "node:path";
import { parse } from "node-html-parser";
import { describeError } from "./errors.js";
import { mediaStoreFromEnv, type MediaStore } from "../media-store.js";
import { normalizeMedia } from "../media/normalize.js";
import { limitsFromSpec, TOYHOUSE_SPEC } from "../media/specs.js";
import { throwIfCloudflareChallenge } from "../scraping/cloudflare.js";
import {
  CookieScraperSession,
  type ScraperResponse,
  type ScraperSession,
} from "../scraping/http.js";
import type {
  Connector,
  ConnectorContext,
  ConnectorLimits,
  DeliverResult,
  IsAuthenticatedResult,
  ListTargetsResult,
  Post,
} from "../types.js";

const BASE_URL = "https://toyhou.se";
const MAX_FILE_BYTES = 4 * 1024 * 1024;
const MAX_CAPTION_LENGTH = 255;

interface ToyhouseConfig {
  CharacterIds?: string;
  ArtistName?: string;
  OffSiteArtistUrl?: string;
  AuthorizedViewers?: string;
  PublicViewers?: string;
  Watermark?: string;
  ContentWarning?: string;
}

interface ToyhouseSecret {
  CookieHeader?: string;
  UserAgent?: string;
}

interface LoginInfo {
  authenticated: boolean;
  username?: string;
}

export type ToyhouseSessionFactory = (
  cookieHeader: string,
  userAgent?: string,
) => Promise<ScraperSession>;

export interface ToyhouseConnectorOptions {
  mediaStore?: MediaStore;
  sessionFactory?: ToyhouseSessionFactory;
}

export class ToyhouseConnector implements Connector {
  private readonly mediaStore: MediaStore;
  private readonly sessionFactory: ToyhouseSessionFactory;
  private readonly accountQueues = new Map<string, Promise<void>>();

  constructor(options: ToyhouseConnectorOptions = {}) {
    this.mediaStore = options.mediaStore ?? mediaStoreFromEnv();
    this.sessionFactory =
      options.sessionFactory ??
      ((cookieHeader, userAgent) => CookieScraperSession.create(BASE_URL, cookieHeader, { userAgent }));
  }

  async isAuthenticated(ctx: ConnectorContext): Promise<IsAuthenticatedResult> {
    try {
      const session = await this.createSession(ctx);
      const login = await this.checkLogin(session);
      return login.authenticated
        ? { isAuthenticated: true, detail: login.username }
        : { isAuthenticated: false, detail: "Toyhouse session is not logged in" };
    } catch (error) {
      return { isAuthenticated: false, detail: describeError(error) };
    }
  }

  async listTargets(ctx: ConnectorContext): Promise<ListTargetsResult> {
    try {
      const session = await this.createSession(ctx);
      const login = await this.checkLogin(session);
      if (!login.authenticated || !login.username) return { targets: [] };
      return {
        targets: [{ id: login.username, name: `Toyhouse: ${login.username}` }],
      };
    } catch {
      return { targets: [] };
    }
  }

  // Caption length is reported via the static ServiceDefinition descriptor instead (see
  // ServiceCollectionExtensions), the same convention every other fixed-spec connector follows here.
  async getLimits(): Promise<ConnectorLimits> {
    return limitsFromSpec(TOYHOUSE_SPEC);
  }

  async deliver(ctx: ConnectorContext, post: Post): Promise<DeliverResult> {
    try {
      const input = this.validatePost(post);
      const config = this.parseConfig(ctx);
      const characterIds = this.characterIds(config.CharacterIds);
      const artistName = this.requireArtistName(config);
      const session = await this.createSession(ctx);

      return await this.enqueue(ctx.connectorId, async () => {
        const page = await session.request("/~images/upload", {
          headers: { referer: `${BASE_URL}/~images/upload` },
        });
        this.requireSuccess(page, "load upload form");
        if (this.isLoginRedirect(page.url)) throw new Error("Toyhouse session is not logged in");
        const token = this.metaContent(page.body, "csrf-token");
        if (!token) throw new Error("Toyhouse form token was not found; the site form may have changed");

        const source = await this.mediaStore.fetch(input.media.container, input.media.key);
        const normalized = await this.normalizeImage(source, input.media.contentType);

        const formData = new FormData();
        formData.set("_token", token);
        formData.set("referer_url", `${BASE_URL}/~images/upload`);
        formData.set(
          "image",
          new Blob([normalized.bytes], { type: normalized.contentType }),
          basename(input.media.key) || "image",
        );
        formData.set("image_zoom", "");
        formData.set("image_x", "");
        formData.set("image_y", "");
        // Custom thumbnails are not supported: an empty placeholder plus "onsite" tells Toyhouse to
        // derive one from the uploaded image itself.
        formData.set("thumbnail", new Blob([], { type: "application/octet-stream" }), "");
        formData.set("thumbnail_options", "onsite");
        formData.set("thumbnail_custom", "offsite");
        formData.set("caption", post.body);
        formData.set("authorized_privacy", this.privacyOption(config.AuthorizedViewers));
        formData.set("public_privacy", this.privacyOption(config.PublicViewers));
        formData.set("watermark_id", this.watermarkOption(config.Watermark));
        formData.set("is_sexual", this.mapSexual(post.rating!));
        formData.set("warning", config.ContentWarning?.trim() ?? "");
        for (const [key, value] of this.artistFields(artistName, config.OffSiteArtistUrl))
          formData.append(key, value);
        for (const id of characterIds) formData.append("character_ids[]", id);
        formData.append("character_ids[]", "");

        const result = await session.request("/~images/upload", {
          method: "POST",
          headers: { referer: `${BASE_URL}/~images/upload` },
          body: formData,
        });
        this.requireSuccess(result, "upload submission");
        if (result.body.includes("alert-danger")) {
          const message = parse(result.body).querySelector(".alert-danger")?.text?.trim();
          throw new Error(message ? `Toyhouse rejected the submission: ${message}` : "Toyhouse rejected the submission");
        }

        const externalId = /\/~images\/(\d+)\./.exec(result.url)?.[1];
        return { success: true, externalId, externalUrl: result.url };
      });
    } catch (error) {
      return { success: false, error: describeError(error) };
    }
  }

  private validatePost(post: Post): { media: Post["media"][number] } {
    if (!post.rating) throw new Error("Toyhouse requires an explicit content rating");
    if (!["general", "mature", "adult", "extreme"].includes(post.rating))
      throw new Error(`unsupported Toyhouse content rating '${post.rating}'`);
    if (post.body.length > MAX_CAPTION_LENGTH)
      throw new Error(`Toyhouse captions may not exceed ${MAX_CAPTION_LENGTH} characters`);
    if (post.media.length === 0) throw new Error("Toyhouse image posts require an image");

    // Toyhouse's upload form takes exactly one file, same accommodation as FurAffinity for a post
    // authored for several multi-image platforms at once.
    const media = post.media.find((item) => item.isDefault) ?? post.media[0];
    const contentType = media.contentType.toLowerCase();
    if (!["image/jpeg", "image/jpg", "image/png", "image/gif"].includes(contentType))
      throw new Error("Toyhouse integration currently supports JPEG, PNG, and GIF image uploads");

    return { media };
  }

  private async normalizeImage(bytes: Buffer, contentType: string): Promise<{ bytes: Buffer; contentType: string }> {
    if (contentType.toLowerCase() === "image/gif") {
      if (bytes.length > MAX_FILE_BYTES)
        throw new Error(`Toyhouse GIF exceeds the ${MAX_FILE_BYTES}-byte limit`);
      return { bytes, contentType: "image/gif" };
    }
    return normalizeMedia(bytes, contentType, {
      ...TOYHOUSE_SPEC,
      image: { ...TOYHOUSE_SPEC.image, allowedMimeTypes: ["image/jpeg", "image/png"] },
    });
  }

  private async createSession(ctx: ConnectorContext): Promise<ScraperSession> {
    if (!ctx.secretJson) throw new Error("missing Toyhouse session cookie");
    let secret: ToyhouseSecret;
    try {
      secret = JSON.parse(ctx.secretJson) as ToyhouseSecret;
    } catch {
      throw new Error("invalid Toyhouse secret JSON");
    }
    if (!secret.CookieHeader?.trim()) throw new Error("missing Toyhouse CookieHeader in secret");
    return this.sessionFactory(secret.CookieHeader, secret.UserAgent);
  }

  private parseConfig(ctx: ConnectorContext): ToyhouseConfig {
    try {
      return JSON.parse(ctx.configJson || "{}") as ToyhouseConfig;
    } catch {
      throw new Error("invalid Toyhouse config JSON");
    }
  }

  private async checkLogin(session: ScraperSession): Promise<LoginInfo> {
    const response = await session.request("/~characters/manage/folder:all");
    this.requireSuccess(response, "check login");
    if (this.isLoginRedirect(response.url)) return { authenticated: false };
    const username = parse(response.body)
      .querySelector(".navbar .display-user-tiny > span.display-user-username")
      ?.text?.trim();
    return { authenticated: true, username: username || undefined };
  }

  private isLoginRedirect(url: string): boolean {
    try {
      return new URL(url).pathname === "/~account/login";
    } catch {
      return false;
    }
  }

  private requireSuccess(response: ScraperResponse, operation: string): void {
    throwIfCloudflareChallenge(response, "Toyhouse");
    if (response.status < 200 || response.status >= 400)
      throw new Error(`Toyhouse ${operation} failed with HTTP ${response.status}`);
  }

  private metaContent(body: string, name: string): string | undefined {
    return parse(body).querySelector(`head > meta[name="${name}"]`)?.getAttribute("content") || undefined;
  }

  private characterIds(value: string | undefined): string[] {
    const ids = (value ?? "").split(",").map((id) => id.trim()).filter(Boolean);
    if (ids.length === 0)
      throw new Error("Toyhouse requires at least one character (by ID) to attach the image to");
    if (ids.some((id) => !/^\d+$/.test(id)))
      throw new Error("Toyhouse character IDs must be comma-separated numbers");
    return ids;
  }

  private requireArtistName(config: ToyhouseConfig): string {
    const name = config.ArtistName?.trim();
    if (!name) throw new Error("Toyhouse requires an artist name/username to credit");
    return name;
  }

  private privacyOption(value: string | undefined): string {
    return this.numericOption(value, "0");
  }

  private watermarkOption(value: string | undefined): string {
    return this.numericOption(value, "1");
  }

  private numericOption(value: string | undefined, fallback: string): string {
    const result = value?.trim() || fallback;
    if (!/^\d+$/.test(result)) throw new Error(`invalid Toyhouse option '${result}'`);
    return result;
  }

  private mapSexual(rating: NonNullable<Post["rating"]>): string {
    switch (rating) {
      case "general":
        return "0";
      case "mature":
        return "1";
      case "adult":
      case "extreme":
        return "2";
    }
  }

  /**
   * Toyhouse expects n+1 artist entries per array (an onsite/offsite pair); we only support crediting
   * one artist, so the second slot is always the "onsite" filler PostyBirb's implementation uses.
   */
  private artistFields(artistName: string, offSiteArtistUrl: string | undefined): [string, string][] {
    const url = offSiteArtistUrl?.trim();
    if (url) {
      return [
        ["artist[]", "offsite"],
        ["artist[]", "onsite"],
        ["artist_username[]", ""],
        ["artist_username[]", ""],
        ["artist_url[]", url],
        ["artist_url[]", ""],
        ["artist_name[]", artistName],
        ["artist_name[]", ""],
        ["artist_credit[]", ""],
        ["artist_credit[]", ""],
      ];
    }
    return [
      ["artist[]", "onsite"],
      ["artist[]", "onsite"],
      ["artist_username[]", artistName],
      ["artist_username[]", ""],
      ["artist_url[]", ""],
      ["artist_url[]", ""],
      ["artist_name[]", ""],
      ["artist_name[]", ""],
      ["artist_credit[]", ""],
      ["artist_credit[]", ""],
    ];
  }

  private async enqueue<T>(accountId: string, operation: () => Promise<T>): Promise<T> {
    const previous = this.accountQueues.get(accountId) ?? Promise.resolve();
    let release!: () => void;
    const current = new Promise<void>((resolve) => {
      release = resolve;
    });
    const queued = previous.then(() => current);
    this.accountQueues.set(accountId, queued);
    await previous;
    try {
      return await operation();
    } finally {
      release();
      if (this.accountQueues.get(accountId) === queued) this.accountQueues.delete(accountId);
    }
  }
}
