import { test } from "node:test";
import assert from "node:assert/strict";
import sharp from "sharp";
import { XConnector, apiKeyFromCookies, type XClient } from "../src/connectors/x.js";
import type { MediaStore } from "../src/media-store.js";
import type { ConnectorContext, Post } from "../src/types.js";

const cookieHeader = "auth_token=tok; ct0=csrf; twid=u%3D12345; cf_clearance=ignored";

const context: ConnectorContext = {
  connectorId: "connector-1",
  userId: "user-1",
  configJson: "{}",
  secretJson: JSON.stringify({ CookieHeader: cookieHeader }),
  targetId: null,
};

const textPost: Post = { title: null, body: "Hello from PostyFox", tags: [], media: [] };
const imagePost: Post = {
  ...textPost,
  media: [{ container: "media", key: "u/pic.png", contentType: "image/png", alt: null }],
};

const noopStoreExtras = {
  async put() {},
  async presignedGetUrl() {
    return "https://example.test/staged";
  },
  async delete() {},
};

async function imageBytes(): Promise<Buffer> {
  return sharp({ create: { width: 20, height: 20, channels: 3, background: "#cc6633" } })
    .png()
    .toBuffer();
}

class FakeClient implements XClient {
  readonly uploads: number[] = [];
  readonly posts: { text?: string; media?: { id: string }[] }[] = [];

  constructor(
    private readonly options: { user?: { userName: string } | undefined; postId?: string | undefined; error?: Error } = {
      user: { userName: "foxartist" },
      postId: "999",
    },
  ) {}

  user = {
    details: async () => {
      if (this.options.error) throw this.options.error;
      return this.options.user;
    },
  };

  tweet = {
    upload: async (media: ArrayBuffer) => {
      this.uploads.push(media.byteLength);
      return `media-${this.uploads.length}`;
    },
    post: async (options: { text?: string; media?: { id: string }[] }) => {
      if (this.options.error) throw this.options.error;
      this.posts.push(options);
      return this.options.postId;
    },
  };
}

function connectorWith(client: FakeClient, keys: string[] = []): XConnector {
  const mediaStore: MediaStore = { fetch: async () => imageBytes(), ...noopStoreExtras };
  return new XConnector({
    mediaStore,
    clientFactory: (apiKey) => {
      keys.push(apiKey);
      return client;
    },
  });
}

test("x api key is built from only the three required cookies, with trailing semicolons", () => {
  const key = apiKeyFromCookies(cookieHeader);
  assert.equal(Buffer.from(key, "base64").toString(), "auth_token=tok;ct0=csrf;twid=u%3D12345;");
});

test("x api key rejects a cookie header missing a required cookie", () => {
  assert.throws(() => apiKeyFromCookies("auth_token=tok; ct0=csrf"), /missing X session cookie 'twid'/);
});

test("x getLimits reports the fixed image caps", async () => {
  const limits = await connectorWith(new FakeClient()).getLimits();
  assert.equal(limits.maxMediaAttachments, 4);
  assert.deepEqual(limits.supportedMimeTypes, ["image/jpeg", "image/png", "image/webp"]);
  assert.equal(limits.imageSizeLimit, 5_242_880);
});

test("x authenticates and identifies the account", async () => {
  const keys: string[] = [];
  const connector = connectorWith(new FakeClient(), keys);

  assert.deepEqual(await connector.isAuthenticated(context), { isAuthenticated: true, detail: "@foxartist" });
  assert.deepEqual(await connector.listTargets(context), {
    targets: [{ id: "foxartist", name: "X: @foxartist" }],
  });
  assert.equal(Buffer.from(keys[0], "base64").toString(), "auth_token=tok;ct0=csrf;twid=u%3D12345;");
});

test("x reports an unauthenticated session", async () => {
  const connector = connectorWith(new FakeClient({ user: undefined }));
  const result = await connector.isAuthenticated(context);
  assert.equal(result.isAuthenticated, false);
  assert.deepEqual(await connector.listTargets(context), { targets: [] });
});

test("x reports a rejected session without throwing", async () => {
  const connector = connectorWith(new FakeClient({ error: new Error("BAD_AUTHENTICATION") }));
  const result = await connector.isAuthenticated(context);
  assert.equal(result.isAuthenticated, false);
  assert.match(result.detail!, /BAD_AUTHENTICATION/);
  assert.deepEqual(await connector.listTargets(context), { targets: [] });
});

test("x reports missing or malformed secrets", async () => {
  const connector = connectorWith(new FakeClient());
  const missing = await connector.isAuthenticated({ ...context, secretJson: null });
  assert.match(missing.detail!, /missing X session cookies/);
  const broken = await connector.isAuthenticated({ ...context, secretJson: "{" });
  assert.match(broken.detail!, /invalid X session cookies/);
  const incomplete = await connector.isAuthenticated({
    ...context,
    secretJson: JSON.stringify({ CookieHeader: "auth_token=tok" }),
  });
  assert.match(incomplete.detail!, /missing X session cookie 'ct0'/);
});

test("x delivers a text-only post", async () => {
  const client = new FakeClient();
  const result = await connectorWith(client).deliver(context, textPost);

  assert.deepEqual(result, { success: true, externalId: "999", externalUrl: "https://x.com/i/status/999" });
  assert.deepEqual(client.posts, [{ text: "Hello from PostyFox" }]);
  assert.equal(client.uploads.length, 0);
});

test("x uploads images and attaches their media ids", async () => {
  const client = new FakeClient();
  const post: Post = { ...imagePost, media: [...imagePost.media, ...imagePost.media] };
  const result = await connectorWith(client).deliver(context, post);

  assert.equal(result.success, true);
  assert.equal(client.uploads.length, 2);
  assert.ok(client.uploads.every((size) => size > 0));
  assert.deepEqual(client.posts[0].media, [{ id: "media-1" }, { id: "media-2" }]);
});

test("x allows an image post with no text", async () => {
  const client = new FakeClient();
  const result = await connectorWith(client).deliver(context, { ...imagePost, body: "" });
  assert.equal(result.success, true);
  assert.equal(client.posts.length, 1);
});

test("x rejects a post over 280 characters before contacting X", async () => {
  const client = new FakeClient();
  const result = await connectorWith(client).deliver(context, { ...textPost, body: "a".repeat(281) });

  assert.equal(result.success, false);
  assert.match(result.error!, /280 characters/);
  assert.equal(client.posts.length, 0);
});

test("x rejects an empty post", async () => {
  const result = await connectorWith(new FakeClient()).deliver(context, { ...textPost, body: "  " });
  assert.equal(result.success, false);
  assert.match(result.error!, /text or an image/);
});

test("x rejects more than four images and non-image media", async () => {
  const client = new FakeClient();
  const item = imagePost.media[0];
  const tooMany = await connectorWith(client).deliver(context, { ...imagePost, media: Array(5).fill(item) });
  assert.match(tooMany.error!, /at most 4 images/);

  const video = await connectorWith(client).deliver(context, {
    ...imagePost,
    media: [{ ...item, contentType: "video/mp4" }],
  });
  assert.match(video.error!, /image attachments only/);
  assert.equal(client.uploads.length, 0);
});

test("x returns a failure when X gives no post id", async () => {
  const result = await connectorWith(new FakeClient({ user: { userName: "a" }, postId: undefined })).deliver(
    context,
    textPost,
  );
  assert.equal(result.success, false);
  assert.match(result.error!, /did not return a post id/);
});

test("x returns platform errors instead of throwing", async () => {
  const result = await connectorWith(new FakeClient({ error: new Error("rate limited") })).deliver(context, textPost);
  assert.deepEqual(result, { success: false, error: "rate limited" });
});
