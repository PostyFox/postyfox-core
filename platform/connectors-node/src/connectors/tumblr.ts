import * as tumblr from "tumblr.js";
import type {
  Connector,
  ConnectorContext,
  DeliverResult,
  IsAuthenticatedResult,
  ListTargetsResult,
  Post,
} from "../types.js";

interface TumblrBlog {
  name: string;
  title?: string;
}

/** Minimal surface of the tumblr.js client that this connector relies on. */
export interface TumblrClientLike {
  userInfo(): Promise<{ user?: { blogs?: TumblrBlog[] } }>;
  createTextPost(
    blogIdentifier: string,
    params: { title?: string; body: string; tags?: string[] },
  ): Promise<{
    id?: string | number;
    id_string?: string;
    post_url?: string;
    [key: string]: unknown;
  }>;
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
  ConsumerKey?: string;
  ConsumerSecret?: string;
  OAuthToken?: string;
  OAuthTokenSecret?: string;
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
  };

  return {
    userInfo: () => client.userInfo(),
    createTextPost: (blogIdentifier, params) =>
      client.createLegacyPost(blogIdentifier, {
        type: "text",
        title: params.title,
        body: params.body,
        tags: params.tags?.join(","),
      }) as Promise<{ id?: string | number; id_string?: string; post_url?: string }>,
  };
};

export class TumblrConnector implements Connector {
  constructor(private readonly clientFactory: TumblrClientFactory = defaultClientFactory) {}

  private parseCredentials(ctx: ConnectorContext): {
    username: string;
    creds: TumblrOAuth1Credentials;
  } {
    const config = JSON.parse(ctx.configJson) as TumblrConfig;
    const username = config?.Username;
    if (!username) throw new Error("missing Tumblr Username in config");
    if (ctx.secretJson === null) throw new Error("missing Tumblr credentials");
    const secret = JSON.parse(ctx.secretJson) as TumblrSecret;
    if (
      !secret?.ConsumerKey ||
      !secret?.ConsumerSecret ||
      !secret?.OAuthToken ||
      !secret?.OAuthTokenSecret
    ) {
      throw new Error("missing Tumblr OAuth1 credentials");
    }
    return {
      username,
      creds: {
        consumer_key: secret.ConsumerKey,
        consumer_secret: secret.ConsumerSecret,
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
      return { isAuthenticated: false, detail: errorMessage(err) };
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

      // TODO: media — photo/video posts are not yet delivered (text-only).
      const result = await client.createTextPost(username, {
        title: post.title ?? undefined,
        body: post.body,
        tags: post.tags,
      });

      const rawId = result.id_string ?? result.id;
      const externalId = rawId !== undefined ? String(rawId) : undefined;
      const externalUrl =
        typeof result.post_url === "string" ? result.post_url : undefined;
      return { success: true, externalId, externalUrl };
    } catch (err) {
      return { success: false, error: errorMessage(err) };
    }
  }
}

function errorMessage(err: unknown): string {
  return err instanceof Error ? err.message : String(err);
}
