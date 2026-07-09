import { test } from "node:test";
import assert from "node:assert/strict";
import { TumblrConnector, type TumblrClientLike } from "../src/connectors/tumblr.js";
import type { MediaStore } from "../src/media-store.js";
import type { ConnectorContext, Post } from "../src/types.js";

/** Fake media store returning known bytes, recording the ref it was asked for. */
function fakeMediaStore(
  bytes: Buffer,
): MediaStore & { calls: { container: string; key: string }[] } {
  const calls: { container: string; key: string }[] = [];
  return {
    calls,
    async fetch(container, key) {
      calls.push({ container, key });
      return bytes;
    },
  };
}

const ctx: ConnectorContext = {
  connectorId: "c",
  userId: "u",
  configJson: JSON.stringify({ Username: "myblog" }),
  secretJson: JSON.stringify({
    ConsumerKey: "ck",
    ConsumerSecret: "cs",
    OAuthToken: "ot",
    OAuthTokenSecret: "ots",
  }),
  targetId: null,
};

const post: Post = { title: "Title", body: "hello", tags: ["a", "b"], media: [] };

function fakeClient(over: Partial<TumblrClientLike> = {}): TumblrClientLike {
  return {
    async userInfo() {
      return {
        user: {
          blogs: [
            { name: "myblog", title: "My Blog" },
            { name: "second", title: "" },
          ],
        },
      };
    },
    async createTextPost() {
      return { id_string: "789", post_url: "https://myblog.tumblr.com/post/789" };
    },
    async createPhotoPost() {
      return { id_string: "790", post_url: "https://myblog.tumblr.com/post/790" };
    },
    ...over,
  };
}

test("tumblr list-targets maps blogs", async () => {
  const connector = new TumblrConnector(() => fakeClient());
  const result = await connector.listTargets(ctx);
  assert.deepEqual(result.targets, [
    { id: "myblog", name: "My Blog" },
    { id: "second", name: "second" },
  ]);
});

test("tumblr is-authenticated true on userInfo success", async () => {
  const connector = new TumblrConnector(() => fakeClient());
  const result = await connector.isAuthenticated(ctx);
  assert.equal(result.isAuthenticated, true);
});

test("tumblr deliver success returns id + url", async () => {
  const connector = new TumblrConnector(() => fakeClient());
  const result = await connector.deliver(ctx, post);
  assert.equal(result.success, true);
  assert.equal(result.externalId, "789");
  assert.equal(result.externalUrl, "https://myblog.tumblr.com/post/789");
});

test("tumblr deliver with media fetches bytes and creates a photo post with alt", async () => {
  const rawBytes = Buffer.from("fake-image-bytes");
  let receivedBlog: string | undefined;
  let receivedParams:
    | {
        title?: string;
        body: string;
        tags?: string[];
        media: { bytes: Buffer; alt: string }[];
      }
    | undefined;

  const store = fakeMediaStore(rawBytes);
  const connector = new TumblrConnector(
    () =>
      fakeClient({
        async createPhotoPost(blog, params) {
          receivedBlog = blog;
          receivedParams = params;
          return { id_string: "790", post_url: "https://myblog.tumblr.com/post/790" };
        },
        async createTextPost() {
          throw new Error("should not create a text post when media is present");
        },
      }),
    store,
  );

  const mediaPost: Post = {
    title: "Title",
    body: "hello",
    tags: ["a", "b"],
    media: [
      {
        container: "media",
        key: "u1/abc/pic.jpg",
        contentType: "image/jpeg",
        alt: "a cat",
      },
    ],
  };

  const result = await connector.deliver(ctx, mediaPost);
  assert.equal(result.success, true);
  assert.equal(result.externalId, "790");
  assert.equal(result.externalUrl, "https://myblog.tumblr.com/post/790");

  // Media store fetched with the ref's container + key.
  assert.deepEqual(store.calls, [{ container: "media", key: "u1/abc/pic.jpg" }]);

  assert.equal(receivedBlog, "myblog");
  assert.equal(receivedParams?.media.length, 1);
  assert.deepEqual(receivedParams?.media[0].bytes, rawBytes);
  assert.equal(receivedParams?.media[0].alt, "a cat");
});

test("tumblr deliver fails with missing credentials", async () => {
  const badCtx: ConnectorContext = {
    ...ctx,
    secretJson: JSON.stringify({ ConsumerKey: "ck" }), // incomplete
  };
  const connector = new TumblrConnector(() => fakeClient());
  const result = await connector.deliver(badCtx, post);
  assert.equal(result.success, false);
  assert.ok(result.error && result.error.includes("credentials"));
});

test("tumblr deliver fails when secret is null", async () => {
  const badCtx: ConnectorContext = { ...ctx, secretJson: null };
  const connector = new TumblrConnector(() => fakeClient());
  const result = await connector.deliver(badCtx, post);
  assert.equal(result.success, false);
});
