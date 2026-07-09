import { test } from "node:test";
import assert from "node:assert/strict";
import { TumblrConnector, type TumblrClientLike } from "../src/connectors/tumblr.js";
import type { ConnectorContext, Post } from "../src/types.js";

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
