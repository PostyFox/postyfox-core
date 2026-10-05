import { basename, extname } from "node:path";
import { describeError } from "./errors.js";
import { mediaStoreFromEnv, type MediaStore } from "../media-store.js";
import { ARTCONOMY_SPEC, limitsFromSpec } from "../media/specs.js";
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

const BASE_URL = "https://artconomy.com";
const MIN_TAGS = 5;
const MAX_TAG_LENGTH = 50;
const MAX_TITLE_LENGTH = 100;
const MAX_CAPTION_LENGTH = 2000;
const MAX_JOURNAL_SUBJECT_LENGTH = 150;
const MAX_JOURNAL_BODY_LENGTH = 5000;
const RATINGS: Record<NonNullable<Post["rating"]>, number> = { general: 0, mature: 1, adult: 2, extreme: 3 };
const EXTENSIONS: Record<string, string> = {
  "image/jpeg": "jpg",
  "image/png": "png",
  "image/gif": "gif",
  "image/webp": "webp",
};

/** Per-submission options (see artconomy-post-options.schema.json); values arrive as strings. */
interface ArtconomyConfig {
  CreditAsArtist?: string;
  Private?: string;
  DisableComments?: string;
}

interface ArtconomySecret {
  CookieHeader?: string;
  UserAgent?: string;
}

interface ArtconomyUser {
  id?: number;
  username?: string;
  guest?: boolean;
}

interface Account {
  id: number;
  username: string;
}

export type ArtconomySessionFactory = (cookieHeader: string, userAgent?: string) => Promise<ScraperSession>;

export interface ArtconomyConnectorOptions {
  mediaStore?: MediaStore;
  sessionFactory?: ArtconomySessionFactory;
}

/**
 * Artconomy's frontend is a client of the site's own Django REST API, so the connector calls the
 * same JSON endpoints with a browser session paired by PostyFox Connect. Session authentication
 * makes every write carry the CSRF cookie back as an X-CSRFToken header. A post with an image
 * becomes a gallery submission (the image is uploaded as an asset first, which only the uploading
 * session may then reference); a post without one becomes a journal. Endpoints and fields follow
 * Artconomy's open-source code (gitlab.com/artconomy/artconomy) and have not been exercised against
 * the live site.
 */
export class ArtconomyConnector implements Connector {
  private readonly mediaStore: MediaStore;
  private readonly sessionFactory: ArtconomySessionFactory;

  constructor(options: ArtconomyConnectorOptions = {}) {
    this.mediaStore = options.mediaStore ?? mediaStoreFromEnv();
    this.sessionFactory =
      options.sessionFactory ??
      ((cookieHeader, userAgent) => CookieScraperSession.create(BASE_URL, cookieHeader, { userAgent }));
  }

  async isAuthenticated(ctx: ConnectorContext): Promise<IsAuthenticatedResult> {
    try {
      const account = await this.currentAccount((await this.createSession(ctx)).session);
      return account
        ? { isAuthenticated: true, detail: account.username }
        : { isAuthenticated: false, detail: "Artconomy session is not logged in" };
    } catch (error) {
      return { isAuthenticated: false, detail: describeError(error) };
    }
  }

  async listTargets(ctx: ConnectorContext): Promise<ListTargetsResult> {
    try {
      const account = await this.currentAccount((await this.createSession(ctx)).session);
      if (!account) return { targets: [] };
      return { targets: [{ id: account.username, name: `Artconomy: ${account.username}` }] };
    } catch {
      return { targets: [] };
    }
  }

  async getLimits(): Promise<ConnectorLimits> {
    return limitsFromSpec(ARTCONOMY_SPEC);
  }

  async deliver(ctx: ConnectorContext, post: Post): Promise<DeliverResult> {
    try {
      const config = this.config(ctx);
      const title = post.title?.trim() ?? "";
      if (post.media.length === 0) {
        this.validateJournal(title, post.body);
        const { session, csrfToken } = await this.createSession(ctx);
        const account = await this.requireAccount(session);
        return await this.postJournal(session, csrfToken, account, title, post.body, config);
      }

      const tags = cleanTags(post.tags);
      const rating = this.validateSubmission(post, title, tags);
      const { session, csrfToken } = await this.createSession(ctx);
      const account = await this.requireAccount(session);
      return await this.postSubmission(session, csrfToken, account, post, title, tags, rating, config);
    } catch (error) {
      return { success: false, error: describeError(error) };
    }
  }

  private async postSubmission(
    session: ScraperSession,
    csrfToken: string,
    account: Account,
    post: Post,
    title: string,
    tags: string[],
    rating: number,
    config: ArtconomyConfig,
  ): Promise<DeliverResult> {
    const media = post.media[0];
    const bytes = await this.mediaStore.fetch(media.container, media.key);
    const form = new FormData();
    form.set("files[]", new Blob([bytes], { type: media.contentType }), this.filename(media.key, media.contentType));

    const upload = await session.request("/api/lib/asset/", {
      method: "POST",
      headers: this.writeHeaders(csrfToken),
      body: form,
    });
    this.requireSuccess(upload, "image upload");
    const assetId = this.parseJson<{ id?: string }>(upload.body)?.id;
    if (!assetId) throw new Error("Artconomy image upload returned no asset id");

    const result = await session.request(`/api/profiles/account/${encodeURIComponent(account.username)}/submissions/`, {
      method: "POST",
      headers: { ...this.writeHeaders(csrfToken), "content-type": "application/json" },
      body: JSON.stringify({
        file: assetId,
        preview: null,
        title,
        caption: post.body,
        rating,
        tags,
        artists: config.CreditAsArtist === "false" ? [] : [account.id],
        characters: [],
        private: config.Private === "true",
        comments_disabled: config.DisableComments === "true",
      }),
    });
    this.requireSuccess(result, "submission");
    const id = this.parseJson<{ id?: number }>(result.body)?.id;
    if (id == null) throw new Error("Artconomy accepted the submission but returned no id");
    return { success: true, externalId: String(id), externalUrl: `${BASE_URL}/submissions/${id}/` };
  }

  private async postJournal(
    session: ScraperSession,
    csrfToken: string,
    account: Account,
    subject: string,
    body: string,
    config: ArtconomyConfig,
  ): Promise<DeliverResult> {
    const username = encodeURIComponent(account.username);
    const result = await session.request(`/api/profiles/account/${username}/journals/`, {
      method: "POST",
      headers: { ...this.writeHeaders(csrfToken), "content-type": "application/json" },
      body: JSON.stringify({ subject, body, comments_disabled: config.DisableComments === "true" }),
    });
    this.requireSuccess(result, "journal");
    const id = this.parseJson<{ id?: number }>(result.body)?.id;
    if (id == null) throw new Error("Artconomy accepted the journal but returned no id");
    return { success: true, externalId: String(id), externalUrl: `${BASE_URL}/profile/${username}/journals/${id}/` };
  }

  private validateJournal(subject: string, body: string): void {
    if (!subject) throw new Error("Artconomy journals require a title");
    if (subject.length > MAX_JOURNAL_SUBJECT_LENGTH)
      throw new Error(`Artconomy journal titles are limited to ${MAX_JOURNAL_SUBJECT_LENGTH} characters`);
    if (!body.trim()) throw new Error("Artconomy journals require a body");
    if (body.length > MAX_JOURNAL_BODY_LENGTH)
      throw new Error(`Artconomy journals are limited to ${MAX_JOURNAL_BODY_LENGTH} characters`);
  }

  /** Checks everything the API would reject, so a bad post fails before anything is uploaded. */
  private validateSubmission(post: Post, title: string, tags: string[]): number {
    if (post.media.length > 1)
      throw new Error("Artconomy submissions take a single image; attach only one");
    const contentType = post.media[0].contentType.toLowerCase();
    if (!ARTCONOMY_SPEC.image.allowedMimeTypes.includes(contentType))
      throw new Error("Artconomy integration currently supports JPEG, PNG, GIF, and WebP image uploads");
    if (title.length > MAX_TITLE_LENGTH)
      throw new Error(`Artconomy submission titles are limited to ${MAX_TITLE_LENGTH} characters`);
    if (post.body.length > MAX_CAPTION_LENGTH)
      throw new Error(`Artconomy captions are limited to ${MAX_CAPTION_LENGTH} characters`);
    if (tags.length < MIN_TAGS)
      throw new Error(`Artconomy requires at least ${MIN_TAGS} distinct tags (got ${tags.length})`);
    if (!post.rating) throw new Error("Artconomy requires a content rating");
    return RATINGS[post.rating];
  }

  private config(ctx: ConnectorContext): ArtconomyConfig {
    try {
      return JSON.parse(ctx.configJson || "{}") as ArtconomyConfig;
    } catch {
      throw new Error("invalid Artconomy config JSON");
    }
  }

  private async createSession(ctx: ConnectorContext): Promise<{ session: ScraperSession; csrfToken: string }> {
    if (!ctx.secretJson) throw new Error("missing Artconomy session cookie");
    let secret: ArtconomySecret;
    try {
      secret = JSON.parse(ctx.secretJson) as ArtconomySecret;
    } catch {
      throw new Error("invalid Artconomy secret JSON");
    }
    const cookieHeader = secret.CookieHeader?.trim();
    if (!cookieHeader) throw new Error("missing Artconomy CookieHeader in secret");
    const csrfToken = cookieValue(cookieHeader, "csrftoken");
    if (!csrfToken) throw new Error("missing Artconomy csrftoken cookie; pair the session again");
    return { session: await this.sessionFactory(cookieHeader, secret.UserAgent), csrfToken };
  }

  private async requireAccount(session: ScraperSession): Promise<Account> {
    const account = await this.currentAccount(session);
    if (!account) throw new Error("Artconomy session is not logged in");
    return account;
  }

  /**
   * The requester endpoint answers every visitor: anonymous ones get the placeholder username "_",
   * and checkout guests a "__<id>" one with `guest` set. Neither can post.
   */
  private async currentAccount(session: ScraperSession): Promise<Account | undefined> {
    const response = await session.request("/api/profiles/data/requester/", {
      headers: { accept: "application/json" },
    });
    this.requireSuccess(response, "check login");
    const user = this.parseJson<ArtconomyUser>(response.body);
    if (!user?.username || user.username.startsWith("_") || user.guest || typeof user.id !== "number")
      return undefined;
    return { id: user.id, username: user.username };
  }

  /** Django checks the CSRF header, and on HTTPS that the referer is the site itself. */
  private writeHeaders(csrfToken: string): Record<string, string> {
    return {
      accept: "application/json",
      "x-csrftoken": csrfToken,
      referer: `${BASE_URL}/`,
      origin: BASE_URL,
    };
  }

  /** The asset endpoint rejects a name without an extension, or with one longer than four characters. */
  private filename(key: string, contentType: string): string {
    const name = basename(key) || "image";
    const ext = extname(name).slice(1);
    if (ext && ext.length <= 4) return name;
    return `${name}.${EXTENSIONS[contentType.toLowerCase()] ?? "png"}`;
  }

  private requireSuccess(response: ScraperResponse, operation: string): void {
    throwIfCloudflareChallenge(response, "Artconomy");
    if (response.status >= 200 && response.status < 300) return;
    const detail = describeApiErrors(response.body);
    throw new Error(
      `Artconomy ${operation} failed with HTTP ${response.status}${detail ? `: ${detail}` : ""}`,
    );
  }

  private parseJson<T>(body: string): T | undefined {
    try {
      return JSON.parse(body) as T;
    } catch {
      return undefined;
    }
  }
}

/**
 * Mirrors Artconomy's own tag_list_cleaner (lower-case, spaces to underscores, Django's slugify,
 * hyphens to underscores, 50 characters, deduplicated). The API validates each tag as a slug before
 * cleaning it, so raw tags with spaces would be rejected outright.
 */
export function cleanTags(tags: string[]): string[] {
  const cleaned = tags.map((tag) =>
    slugify(tag.toLowerCase().replaceAll(" ", "_")).replaceAll("-", "_").slice(0, MAX_TAG_LENGTH),
  );
  return [...new Set(cleaned.filter(Boolean))];
}

/** Django's django.utils.text.slugify with allow_unicode=False. */
function slugify(value: string): string {
  return value
    .normalize("NFKD")
    .replace(/[^\x00-\x7f]/g, "")
    .toLowerCase()
    .replace(/[^\w\s-]/g, "")
    .replace(/[-\s]+/g, "-")
    .replace(/^[-_]+|[-_]+$/g, "");
}

function cookieValue(cookieHeader: string, name: string): string | undefined {
  for (const pair of cookieHeader.split(";")) {
    const separator = pair.indexOf("=");
    if (separator > 0 && pair.slice(0, separator).trim() === name) return pair.slice(separator + 1).trim() || undefined;
  }
  return undefined;
}

/** DRF validation errors are a field → messages map; flatten them into one line. */
function describeApiErrors(body: string): string | undefined {
  let parsed: unknown;
  try {
    parsed = JSON.parse(body);
  } catch {
    return undefined;
  }
  if (!parsed || typeof parsed !== "object") return undefined;
  const messages = Object.entries(parsed as Record<string, unknown>).flatMap(([field, value]) => {
    const list = Array.isArray(value) ? value : [value];
    return list.filter((m) => typeof m === "string").map((m) => (field === "detail" ? m : `${field}: ${m}`));
  });
  return messages.length > 0 ? messages.join("; ") : undefined;
}
