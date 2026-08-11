import * as tumblr from "tumblr.js";
import { createReadStream } from "node:fs";
import { mkdtemp, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { basename, join } from "node:path";
import { describeError } from "./errors.js";
import { mediaStoreFromEnv, type MediaStore } from "../media-store.js";
import { normalizeMedia } from "../media/normalize.js";
import { TUMBLR_SPEC } from "../media/specs.js";
import type {
  Connector,
  ConnectorContext,
  DeliverResult,
  IsAuthenticatedResult,
  ListTargetsResult,
  OAuthProvider,
  Post,
} from "../types.js";
import {
  TumblrOAuth1Provider,
  type TumblrConsumerCredentials,
} from "./tumblr-oauth.js";

interface TumblrBlog {
  name: string;
  title?: string;
}

/** A decoded image to attach to a photo post. */
export interface TumblrMedia {
  fileName: string;
  contentType: string;
  /** Alternative text for the image. */
  alt: string;
  bytes: Buffer;
}

type TumblrPostResult = {
  id?: string | number;
  id_string?: string;
  post_url?: string;
  [key: string]: unknown;
};

/** Minimal surface of the tumblr.js client that this connector relies on. */
export interface TumblrClientLike {
  userInfo(): Promise<{ user?: { blogs?: TumblrBlog[] } }>;
  createTextPost(
    blogIdentifier: string,
    params: { title?: string; body: string; tags?: string[] },
  ): Promise<TumblrPostResult>;
  createPhotoPost(
    blogIdentifier: string,
    params: { title?: string; body: string; tags?: string[]; media: TumblrMedia[] },
  ): Promise<TumblrPostResult>;
}

/** OAuth1 credentials that tumblr.js expects. */
export interface TumblrOAuth1Credentials {
  consumer_key: string;
  consumer_secret: string;
  token: string;
  token_secret: string;
}

export type TumblrClientFactory = (creds: TumblrOAuth1Credentials) => TumblrClientLike;

interface TumblrConfig {
  Username: string;
}

interface TumblrSecret {
  OAuthToken?: string;
  OAuthTokenSecret?: string;
}

/** Rewrites a filename's extension to match the (possibly converted) MIME type. */
function renameExtension(fileName: string, mime: string): string {
  const ext: Record<string, string> = {
    "image/jpeg": ".jpg",
    "image/png": ".png",
    "image/webp": ".webp",
    "image/gif": ".gif",
    "video/mp4": ".mp4",
    "video/webm": ".webm",
  };
  const target = ext[mime.toLowerCase()];
  if (!target) return fileName;
  const dot = fileName.lastIndexOf(".");
  const stem = dot >= 0 ? fileName.slice(0, dot) : fileName;
  return stem + target;
}

const defaultClientFactory: TumblrClientFactory = (creds) => {
  const client = tumblr.createClient({
    consumer_key: creds.consumer_key,
    consumer_secret: creds.consumer_secret,
    token: creds.token,
    token_secret: creds.token_secret,
  }) as unknown as {
    userInfo(): Promise<{ user?: { blogs?: TumblrBlog[] } }>;
    createLegacyPost(blog: string, params: Record<string, unknown>): Promise<Record<string, unknown>>;
    createPost(blog: string, params: Record<string, unknown>): Promise<Record<string, unknown>>;
  };

  return {
    userInfo: () => client.userInfo(),
    createTextPost: (blogIdentifier, params) =>
      client.createLegacyPost(blogIdentifier, {
        type: "text",
        title: params.title,
        body: params.body,
        tags: params.tags?.join(","),
      }) as Promise<TumblrPostResult>,
    createPhotoPost: async (blogIdentifier, params) => {
      // tumblr.js NPF media upload requires fs.ReadStream sources, so decoded
      // bytes are staged to a short-lived temp directory that is always cleaned
      // up afterwards.
      const dir = await mkdtemp(join(tmpdir(), "tumblr-media-"));
      try {
        const content: Record<string, unknown>[] = [];
        if (params.title) {
          content.push({ type: "text", text: params.title, subtype: "heading1" });
        }
        if (params.body) {
          content.push({ type: "text", text: params.body });
        }
        for (let i = 0; i < params.media.length; i++) {
          const item = params.media[i];
          const filePath = join(dir, item.fileName || `image-${i}`);
          await writeFile(filePath, item.bytes);
          content.push({
            type: "image",
            media: createReadStream(filePath),
            alt_text: item.alt,
          });
        }
        return (await client.createPost(blogIdentifier, {
          content,
          tags: params.tags,
        })) as TumblrPostResult;
      } finally {
        await rm(dir, { recursive: true, force: true });
      }
    },
  };
};

export class TumblrConnector implements Connector {
  private readonly consumer?: TumblrConsumerCredentials;
  readonly oauth: OAuthProvider;

  constructor(
    private readonly clientFactory: TumblrClientFactory = defaultClientFactory,
    private readonly mediaStore: MediaStore = mediaStoreFromEnv(),
    consumer?: TumblrConsumerCredentials,
  ) {
    this.consumer = consumer;
    this.oauth = {
      startAuthorization: (input) =>
        new TumblrOAuth1Provider(this.resolveConsumer(input.operationalSecretJson))
          .startAuthorization(input),
      completeAuthorization: (input) =>
        new TumblrOAuth1Provider(this.resolveConsumer(input.operationalSecretJson))
          .completeAuthorization(input),
    };
  }

  private resolveConsumer(raw?: string | null): TumblrConsumerCredentials {
    if (raw) {
      const parsed = JSON.parse(raw) as Partial<TumblrConsumerCredentials>;
      if (parsed.consumerKey && parsed.consumerSecret) {
        return { consumerKey: parsed.consumerKey, consumerSecret: parsed.consumerSecret };
      }
    }
    if (this.consumer) return this.consumer;
    throw new Error("Tumblr consumer credentials not configured in the operational secret store");
  }

  private parseCredentials(ctx: ConnectorContext): {
    username: string;
    creds: TumblrOAuth1Credentials;
  } {
    const config = JSON.parse(ctx.configJson) as TumblrConfig;
    const username = config?.Username;
    if (!username) throw new Error("missing Tumblr Username in config");
    const consumer = this.resolveConsumer(ctx.operationalSecretJson);
    if (ctx.secretJson === null) throw new Error("missing Tumblr credentials");
    const secret = JSON.parse(ctx.secretJson) as TumblrSecret;
    if (!secret?.OAuthToken || !secret?.OAuthTokenSecret) {
      throw new Error("missing Tumblr OAuth token — reconnect the account");
    }
    return {
      username,
      creds: {
        consumer_key: consumer.consumerKey,
        consumer_secret: consumer.consumerSecret,
        token: secret.OAuthToken,
        token_secret: secret.OAuthTokenSecret,
      },
    };
  }

  async isAuthenticated(ctx: ConnectorContext): Promise<IsAuthenticatedResult> {
    try {
      const { creds } = this.parseCredentials(ctx);
      const client = this.clientFactory(creds);
      await client.userInfo();
      return { isAuthenticated: true };
    } catch (err) {
      return { isAuthenticated: false, detail: describeError(err) };
    }
  }

  async listTargets(ctx: ConnectorContext): Promise<ListTargetsResult> {
    try {
      const { creds } = this.parseCredentials(ctx);
      const client = this.clientFactory(creds);
      const info = await client.userInfo();
      const blogs = info?.user?.blogs ?? [];
      return {
        targets: blogs.map((blog) => ({
          id: blog.name,
          name: blog.title || blog.name,
        })),
      };
    } catch {
      return { targets: [] };
    }
  }

  async deliver(ctx: ConnectorContext, post: Post): Promise<DeliverResult> {
    try {
      const { username, creds } = this.parseCredentials(ctx);
      const client = this.clientFactory(creds);

      const cap = TUMBLR_SPEC.maxAttachments ?? Number.MAX_SAFE_INTEGER;
      const media = (post.media ?? []).slice(0, cap);
      let result;
      if (media.length > 0) {
        const resolved: TumblrMedia[] = [];
        for (const item of media) {
          const raw = await this.mediaStore.fetch(item.container, item.key);
          const normalized = await normalizeMedia(raw, item.contentType, TUMBLR_SPEC);
          resolved.push({
            fileName: renameExtension(basename(item.key) || item.key, normalized.contentType),
            contentType: normalized.contentType,
            alt: item.alt ?? "",
            bytes: normalized.bytes,
          });
        }
        result = await client.createPhotoPost(username, {
          title: post.title ?? undefined,
          body: post.body,
          tags: post.tags,
          media: resolved,
        });
      } else {
        result = await client.createTextPost(username, {
          title: post.title ?? undefined,
          body: post.body,
          tags: post.tags,
        });
      }

      const rawId = result.id_string ?? result.id;
      const externalId = rawId !== undefined ? String(rawId) : undefined;
      const externalUrl =
        typeof result.post_url === "string" ? result.post_url : undefined;
      return { success: true, externalId, externalUrl };
    } catch (err) {
      return { success: false, error: describeError(err) };
    }
  }
}
