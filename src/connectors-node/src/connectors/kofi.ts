import { basename } from "node:path";
import { parse } from "node-html-parser";
import { describeError } from "./errors.js";
import { mediaStoreFromEnv, type MediaStore } from "../media-store.js";
import { KOFI_SPEC, limitsFromSpec } from "../media/specs.js";
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
  PostMedia,
} from "../types.js";

const BASE_URL = "https://ko-fi.com";
const AUDIENCES = ["public", "supporter", "recurringSupporter"];

interface KofiConfig {
  Audience?: string;
}

interface KofiSecret {
  CookieHeader?: string;
  UserAgent?: string;
}

interface KofiLogin {
  authenticated: boolean;
  token?: string;
  handle?: string;
  displayName?: string;
}

export type KofiSessionFactory = (cookieHeader: string, userAgent?: string) => Promise<ScraperSession>;

export interface KofiConnectorOptions {
  mediaStore?: MediaStore;
  sessionFactory?: KofiSessionFactory;
}

/**
 * Ko-fi has no posting API. Requests replay the site's own form endpoints with a browser session
 * paired by PostyFox Connect (same handoff as FurAffinity/Toyhouse). Text-only posts become articles;
 * posts with images become gallery items. Endpoints and fields follow PostyBirb's open-source Ko-fi
 * module and have not been exercised against the live site.
 */
export class KofiConnector implements Connector {
  private readonly mediaStore: MediaStore;
  private readonly sessionFactory: KofiSessionFactory;

  constructor(options: KofiConnectorOptions = {}) {
    this.mediaStore = options.mediaStore ?? mediaStoreFromEnv();
    this.sessionFactory =
      options.sessionFactory ??
      ((cookieHeader, userAgent) => CookieScraperSession.create(BASE_URL, cookieHeader, { userAgent }));
  }

  async isAuthenticated(ctx: ConnectorContext): Promise<IsAuthenticatedResult> {
    try {
      const login = await this.checkLogin(await this.createSession(ctx));
      return login.authenticated
        ? { isAuthenticated: true, detail: login.displayName ?? login.handle }
        : { isAuthenticated: false, detail: "Ko-fi session is not logged in" };
    } catch (error) {
      return { isAuthenticated: false, detail: describeError(error) };
    }
  }

  async listTargets(ctx: ConnectorContext): Promise<ListTargetsResult> {
    try {
      const login = await this.checkLogin(await this.createSession(ctx));
      if (!login.authenticated || !login.handle) return { targets: [] };
      return { targets: [{ id: login.handle, name: `Ko-fi: ${login.displayName ?? login.handle}` }] };
    } catch {
      return { targets: [] };
    }
  }

  async getLimits(): Promise<ConnectorLimits> {
    return limitsFromSpec(KOFI_SPEC);
  }

  async deliver(ctx: ConnectorContext, post: Post): Promise<DeliverResult> {
    try {
      const title = post.title?.trim();
      if (!title) throw new Error("Ko-fi posts require a title");
      const audience = this.audience(ctx);
      this.validateMedia(post.media);

      const session = await this.createSession(ctx);
      const login = await this.checkLogin(session);
      if (!login.authenticated) throw new Error("Ko-fi session is not logged in");
      if (!login.token) throw new Error("Ko-fi form token was not found; the site form may have changed");

      return post.media.length > 0
        ? await this.postGalleryItem(session, login.token, post, title, audience)
        : await this.postArticle(session, login.token, post, title, audience);
    } catch (error) {
      return { success: false, error: describeError(error) };
    }
  }

  private async postArticle(
    session: ScraperSession,
    token: string,
    post: Post,
    title: string,
    audience: string,
  ): Promise<DeliverResult> {
    const form = new FormData();
    form.set("__RequestVerificationToken", token);
    form.set("type", "Article");
    form.set("blogPostId", "0");
    form.set("blogPostTitle", title);
    form.set("postBody", post.body);
    form.set("noFeaturedImage", "false");
    form.set("tags", post.tags.join(","));
    form.set("postAudience", audience);
    form.set("submit", "publish");

    const result = await session.request("/Blog/AddBlogPost", {
      method: "POST",
      headers: this.formHeaders(token),
      body: form,
    });
    this.requireSuccess(result, "article submission");
    this.throwIfRejected(result.body);
    return { success: true, externalUrl: result.url };
  }

  private async postGalleryItem(
    session: ScraperSession,
    token: string,
    post: Post,
    title: string,
    audience: string,
  ): Promise<DeliverResult> {
    const uploadIds: string[] = [];
    for (const media of post.media) {
      const bytes = await this.mediaStore.fetch(media.container, media.key);
      const filename = basename(media.key) || "image";
      const form = new FormData();
      form.set("__RequestVerificationToken", token);
      form.set("file[0]", new Blob([bytes], { type: media.contentType }), filename);
      form.set("filenames", filename);

      const upload = await session.request("/api/media/gallery-item/upload?throwOnError=true", {
        method: "POST",
        headers: this.formHeaders(token),
        body: form,
      });
      this.requireSuccess(upload, "image upload");
      const externalId = this.parseJson<{ ExternalId?: string }[]>(upload.body)?.[0]?.ExternalId;
      if (!externalId) throw new Error("Ko-fi image upload returned no upload id");
      uploadIds.push(externalId);
    }

    const result = await session.request("/Gallery/AddGalleryItem", {
      method: "POST",
      headers: { ...this.formHeaders(token), "content-type": "application/json" },
      body: JSON.stringify({
        Album: "",
        Audience: audience,
        Description: post.body,
        DisableNewComments: false,
        EnableHiRes: false,
        GalleryItemId: "",
        ImageUploadIds: uploadIds,
        PostToTwitter: false,
        ScheduleEnabled: false,
        ScheduledDate: "",
        ScheduledTime: "",
        Title: title,
        UploadAsIndividualImages: false,
      }),
    });
    this.requireSuccess(result, "gallery submission");
    if (this.parseJson<{ success?: boolean }>(result.body)?.success !== true)
      throw new Error("Ko-fi rejected the gallery post");
    return { success: true };
  }

  private validateMedia(media: PostMedia[]): void {
    if (media.length > (KOFI_SPEC.maxAttachments ?? 0))
      throw new Error(`Ko-fi allows at most ${KOFI_SPEC.maxAttachments} images per post`);
    for (const item of media)
      if (!KOFI_SPEC.image.allowedMimeTypes.includes(item.contentType.toLowerCase()))
        throw new Error("Ko-fi integration currently supports JPEG, PNG, and GIF image uploads");
  }

  private audience(ctx: ConnectorContext): string {
    let config: KofiConfig;
    try {
      config = JSON.parse(ctx.configJson || "{}") as KofiConfig;
    } catch {
      throw new Error("invalid Ko-fi config JSON");
    }
    const audience = config.Audience?.trim() || "public";
    if (!AUDIENCES.includes(audience)) throw new Error(`invalid Ko-fi audience '${audience}'`);
    return audience;
  }

  private async createSession(ctx: ConnectorContext): Promise<ScraperSession> {
    if (!ctx.secretJson) throw new Error("missing Ko-fi session cookie");
    let secret: KofiSecret;
    try {
      secret = JSON.parse(ctx.secretJson) as KofiSecret;
    } catch {
      throw new Error("invalid Ko-fi secret JSON");
    }
    if (!secret.CookieHeader?.trim()) throw new Error("missing Ko-fi CookieHeader in secret");
    return this.sessionFactory(secret.CookieHeader, secret.UserAgent);
  }

  /** The settings page is only rendered for a logged-in session, and carries the antiforgery token. */
  private async checkLogin(session: ScraperSession): Promise<KofiLogin> {
    const response = await session.request("/settings");
    this.requireSuccess(response, "check login");
    if (!response.body.includes("profile-tab")) return { authenticated: false };
    const html = parse(response.body);
    return {
      authenticated: true,
      token: html.querySelector('input[name="__RequestVerificationToken"]')?.getAttribute("value") || undefined,
      handle: html.querySelector("input#handle")?.getAttribute("value") || undefined,
      displayName: html.querySelector('input[name="DisplayName"]')?.getAttribute("value") || undefined,
    };
  }

  private formHeaders(token: string): Record<string, string> {
    return { referer: `${BASE_URL}/`, requestverificationtoken: token };
  }

  private requireSuccess(response: ScraperResponse, operation: string): void {
    throwIfCloudflareChallenge(response, "Ko-fi");
    if (response.status < 200 || response.status >= 400)
      throw new Error(`Ko-fi ${operation} failed with HTTP ${response.status}`);
  }

  private throwIfRejected(body: string): void {
    const parsed = this.parseJson<{ success?: boolean; error?: string; friendly_error_message?: string }>(body);
    if (parsed?.success === false)
      throw new Error(`Ko-fi rejected the submission: ${parsed.friendly_error_message || parsed.error || "unknown error"}`);
  }

  private parseJson<T>(body: string): T | undefined {
    try {
      return JSON.parse(body) as T;
    } catch {
      return undefined;
    }
  }
}
