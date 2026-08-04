import { basename } from "node:path";
import { parse } from "node-html-parser";
import sharp from "sharp";
import { describeError } from "./errors.js";
import { mediaStoreFromEnv, type MediaStore } from "../media-store.js";
import { normalizeMedia } from "../media/normalize.js";
import { FURAFFINITY_SPEC } from "../media/specs.js";
import {
  CookieScraperSession,
  type ScraperResponse,
  type ScraperSession,
} from "../scraping/http.js";
import type {
  Connector,
  ConnectorContext,
  DeliverResult,
  IsAuthenticatedResult,
  ListTargetsResult,
  Post,
} from "../types.js";

const BASE_URL = "https://www.furaffinity.net";
const MAX_FILE_BYTES = 10 * 1024 * 1024;
const MAX_KEYWORDS_LENGTH = 500;
const MINIMUM_POST_INTERVAL_MS = 70_000;

interface FurAffinityConfig {
  Category?: string;
  Theme?: string;
  Species?: string;
  Gender?: string;
  Scraps?: boolean;
  DisableComments?: boolean;
  FolderIds?: string;
}

interface FurAffinitySecret {
  CookieHeader?: string;
}

interface LoginInfo {
  authenticated: boolean;
  username?: string;
}

export type FurAffinitySessionFactory = (cookieHeader: string) => Promise<ScraperSession>;

export interface FurAffinityConnectorOptions {
  mediaStore?: MediaStore;
  sessionFactory?: FurAffinitySessionFactory;
  minimumPostIntervalMs?: number;
  sleep?: (milliseconds: number) => Promise<void>;
  now?: () => number;
}

/** FurAffinity's form-driven gallery connector, based on the current PostyBirb v4 workflow. */
export class FurAffinityConnector implements Connector {
  private readonly mediaStore: MediaStore;
  private readonly sessionFactory: FurAffinitySessionFactory;
  private readonly minimumPostIntervalMs: number;
  private readonly sleep: (milliseconds: number) => Promise<void>;
  private readonly now: () => number;
  private readonly lastPostAt = new Map<string, number>();
  private readonly accountQueues = new Map<string, Promise<void>>();

  constructor(options: FurAffinityConnectorOptions = {}) {
    this.mediaStore = options.mediaStore ?? mediaStoreFromEnv();
    this.sessionFactory =
      options.sessionFactory ??
      ((cookieHeader) => CookieScraperSession.create(BASE_URL, cookieHeader));
    this.minimumPostIntervalMs = options.minimumPostIntervalMs ?? MINIMUM_POST_INTERVAL_MS;
    this.sleep = options.sleep ?? ((milliseconds) => new Promise((resolve) => setTimeout(resolve, milliseconds)));
    this.now = options.now ?? Date.now;
  }

  async isAuthenticated(ctx: ConnectorContext): Promise<IsAuthenticatedResult> {
    try {
      const session = await this.createSession(ctx);
      const login = await this.checkLogin(session);
      return login.authenticated
        ? { isAuthenticated: true, detail: login.username }
        : { isAuthenticated: false, detail: "FurAffinity session is not logged in" };
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
        targets: [{ id: login.username, name: `FurAffinity: ${login.username}` }],
      };
    } catch {
      return { targets: [] };
    }
  }

  async deliver(ctx: ConnectorContext, post: Post): Promise<DeliverResult> {
    try {
      const input = this.validatePost(post);
      const session = await this.createSession(ctx);
      const login = await this.checkLogin(session);
      if (!login.authenticated || !login.username)
        throw new Error("FurAffinity session is not logged in");

      return await this.enqueue(login.username.toLowerCase(), async () => {
        await this.waitForRateLimit(login.username!.toLowerCase());
        this.lastPostAt.set(login.username!.toLowerCase(), this.now());

        const source = await this.mediaStore.fetch(input.media.container, input.media.key);
        const normalized = await this.normalizeImage(source, input.media.contentType);
        const thumbnail = await sharp(normalized.bytes, { animated: false })
          .resize({ width: 300, height: 300, fit: "inside", withoutEnlargement: true })
          .jpeg({ quality: 85 })
          .toBuffer();

        const uploadPage = await session.request("/submit/", {
          headers: { referer: `${BASE_URL}/submit/` },
        });
        this.requireSuccess(uploadPage, "load upload form");
        const uploadKey = this.inputValue(uploadPage.body, [
          '#upload_form input[name="key"]',
          '#myform input[name="key"]',
        ]);

        const upload = new FormData();
        upload.set("key", uploadKey);
        upload.set("submission_type", "submission");
        upload.set(
          "submission",
          new Blob([normalized.bytes], { type: normalized.contentType }),
          basename(input.media.key) || "submission",
        );
        upload.set("thumbnail", new Blob([thumbnail], { type: "image/jpeg" }), "thumbnail.jpg");

        const uploaded = await session.request("/submit/upload", {
          method: "POST",
          headers: { referer: `${BASE_URL}/submit/` },
          body: upload,
        });
        this.requireSuccess(uploaded, "upload file");
        this.throwPageError(uploaded.body);
        const finalizeKey = this.inputValue(uploaded.body, ['#myform input[name="key"]']);

        const config = this.parseConfig(ctx);
        const finalize = new URLSearchParams();
        finalize.set("key", finalizeKey);
        finalize.set("title", input.title);
        finalize.set("message", post.body);
        finalize.set("keywords", input.keywords);
        finalize.set("rating", this.mapRating(post.rating!));
        finalize.set("cat", this.numericOption(config.Category, "1"));
        finalize.set("atype", this.numericOption(config.Theme, "1"));
        finalize.set("species", this.numericOption(config.Species, "1"));
        finalize.set("gender", this.numericOption(config.Gender, "0"));
        finalize.set("create_folder_name", "");
        if (config.DisableComments) finalize.set("lock_comments", "on");
        if (config.Scraps) finalize.set("scrap", "1");
        for (const folderId of this.folderIds(config.FolderIds))
          finalize.append("folder_ids[]", folderId);

        const completed = await session.request("/submit/finalize", {
          method: "POST",
          redirect: "manual",
          headers: {
            "content-type": "application/x-www-form-urlencoded",
            referer: `${BASE_URL}/submit/upload`,
          },
          body: finalize,
        });
        this.requireSuccess(completed, "finalize submission");
        this.throwPageError(completed.body);
        const location = completed.headers.get("location");
        const successUrl = location ? new URL(location, BASE_URL).toString() : completed.url;
        if (!successUrl.includes("?upload-successful"))
          throw new Error("FurAffinity did not confirm the submission");

        const externalUrl = successUrl.replace(/\?upload-successful.*$/, "");
        const externalId = /\/view\/(\d+)/.exec(externalUrl)?.[1];
        return { success: true, externalId, externalUrl };
      });
    } catch (error) {
      return { success: false, error: describeError(error) };
    }
  }

  private validatePost(post: Post): {
    title: string;
    keywords: string;
    media: Post["media"][number];
  } {
    const title = post.title?.trim();
    if (!title) throw new Error("FurAffinity requires a title");
    if (title.length > 60) throw new Error("FurAffinity titles may not exceed 60 characters");
    if (!post.rating) throw new Error("FurAffinity requires an explicit content rating");
    if (!["general", "mature", "adult", "extreme"].includes(post.rating))
      throw new Error(`unsupported FurAffinity content rating '${post.rating}'`);
    if (post.media.length !== 1) throw new Error("FurAffinity gallery submissions require exactly one file");

    const contentType = post.media[0].contentType.toLowerCase();
    if (!["image/jpeg", "image/jpg", "image/png", "image/gif"].includes(contentType))
      throw new Error("FurAffinity integration currently supports JPEG, PNG, and GIF gallery submissions");

    const tags = post.tags
      .map((tag) => tag.trim().replace(/[\\/]/g, "_").replace(/\s+/g, "_"))
      .filter((tag) => tag.length >= 3);
    if (tags.length < 3) throw new Error("FurAffinity requires at least three tags of three or more characters");

    const accepted: string[] = [];
    for (const tag of tags) {
      const candidate = [...accepted, tag].join(" ");
      if (candidate.length > MAX_KEYWORDS_LENGTH) break;
      accepted.push(tag);
    }
    return { title, keywords: accepted.join(" "), media: post.media[0] };
  }

  private async normalizeImage(bytes: Buffer, contentType: string): Promise<{ bytes: Buffer; contentType: string }> {
    if (contentType.toLowerCase() === "image/gif") {
      if (bytes.length > MAX_FILE_BYTES)
        throw new Error(`FurAffinity GIF exceeds the ${MAX_FILE_BYTES}-byte limit`);
      return { bytes, contentType: "image/gif" };
    }
    return normalizeMedia(bytes, contentType, {
      ...FURAFFINITY_SPEC,
      image: { ...FURAFFINITY_SPEC.image, allowedMimeTypes: ["image/jpeg", "image/png"] },
    });
  }

  private async createSession(ctx: ConnectorContext): Promise<ScraperSession> {
    if (!ctx.secretJson) throw new Error("missing FurAffinity session cookie");
    let secret: FurAffinitySecret;
    try {
      secret = JSON.parse(ctx.secretJson) as FurAffinitySecret;
    } catch {
      throw new Error("invalid FurAffinity secret JSON");
    }
    if (!secret.CookieHeader?.trim()) throw new Error("missing FurAffinity CookieHeader in secret");
    return this.sessionFactory(secret.CookieHeader);
  }

  private parseConfig(ctx: ConnectorContext): FurAffinityConfig {
    try {
      return JSON.parse(ctx.configJson || "{}") as FurAffinityConfig;
    } catch {
      throw new Error("invalid FurAffinity config JSON");
    }
  }

  private async checkLogin(session: ScraperSession): Promise<LoginInfo> {
    const response = await session.request("/controls/submissions");
    this.requireSuccess(response, "check login");
    this.throwCloudflareError(response);
    if (!response.body.includes("logout-link")) return { authenticated: false };
    const username = parse(response.body)
      .querySelector(".loggedin_user_avatar")
      ?.getAttribute("alt")
      ?.trim();
    return { authenticated: true, username: username || undefined };
  }

  private requireSuccess(response: ScraperResponse, operation: string): void {
    this.throwCloudflareError(response);
    if (response.status < 200 || response.status >= 400)
      throw new Error(`FurAffinity ${operation} failed with HTTP ${response.status}`);
  }

  private throwCloudflareError(response: ScraperResponse): void {
    const mitigated = response.headers.get("cf-mitigated")?.toLowerCase() === "challenge";
    const challengePage =
      /<title[^>]*>\s*just a moment(?:\.\.\.)?\s*<\/title>/i.test(response.body) ||
      /\/cdn-cgi\/challenge-platform\//i.test(response.body) ||
      /window\._cf_chl_opt/i.test(response.body);
    if (mitigated || challengePage)
      throw new Error("FurAffinity requires a Cloudflare challenge; refresh the imported session cookies in a browser");
  }

  private throwPageError(body: string): void {
    const message = parse(body).querySelector(".redirect-message")?.text.trim();
    if (!message) return;
    if (message.toUpperCase().includes("CAPTCHA"))
      throw new Error("FurAffinity requires at least 11 existing submissions before automated posting is allowed");
    throw new Error(message);
  }

  private inputValue(body: string, selectors: string[]): string {
    const document = parse(body);
    for (const selector of selectors) {
      const value = document.querySelector(selector)?.getAttribute("value");
      if (value) return value;
    }
    throw new Error("FurAffinity form token was not found; the site form may have changed");
  }

  private mapRating(rating: NonNullable<Post["rating"]>): string {
    switch (rating) {
      case "general":
        return "0";
      case "mature":
        return "2";
      case "adult":
      case "extreme":
        return "1";
    }
  }

  private numericOption(value: string | undefined, fallback: string): string {
    const result = value?.trim() || fallback;
    if (!/^\d+$/.test(result)) throw new Error(`invalid FurAffinity option '${result}'`);
    return result;
  }

  private folderIds(value: string | undefined): string[] {
    if (!value?.trim()) return [];
    const ids = value.split(",").map((id) => id.trim()).filter(Boolean);
    if (ids.some((id) => !/^\d+$/.test(id))) throw new Error("FurAffinity folder IDs must be comma-separated numbers");
    return ids;
  }

  private async waitForRateLimit(accountId: string): Promise<void> {
    const last = this.lastPostAt.get(accountId);
    if (last === undefined) return;
    const remaining = this.minimumPostIntervalMs - (this.now() - last);
    if (remaining > 0) await this.sleep(remaining);
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
