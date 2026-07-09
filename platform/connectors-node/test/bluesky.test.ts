import { test } from "node:test";
import assert from "node:assert/strict";
import { BlueskyConnector, type BlueskyAgentLike } from "../src/connectors/bluesky.js";
import type { ConnectorContext, Post } from "../src/types.js";

const ctx: ConnectorContext = {
  connectorId: "c",
  userId: "u",
  configJson: JSON.stringify({ Handle: "alice.bsky.social" }),
  secretJson: JSON.stringify({ AppPassword: "app-pass" }),
  targetId: null,
};

const post: Post = { title: null, body: "hello world", tags: [], media: [] };

function fakeAgent(over: Partial<BlueskyAgentLike> = {}): BlueskyAgentLike {
  return {
    async login() {
      return {};
    },
    async post() {
      return { uri: "at://did:plc:abc/app.bsky.feed.post/xyz123", cid: "cid1" };
    },
    ...over,
  };
}

test("bluesky list-targets returns single handle target", async () => {
  const connector = new BlueskyConnector(() => fakeAgent());
  const result = await connector.listTargets(ctx);
  assert.deepEqual(result, {
    targets: [{ id: "alice.bsky.social", name: "Bluesky: alice.bsky.social" }],
  });
});

test("bluesky is-authenticated true on successful login", async () => {
  const connector = new BlueskyConnector(() => fakeAgent());
  const result = await connector.isAuthenticated(ctx);
  assert.equal(result.isAuthenticated, true);
});

test("bluesky deliver success returns uri + url", async () => {
  const connector = new BlueskyConnector(() => fakeAgent());
  const result = await connector.deliver(ctx, post);
  assert.equal(result.success, true);
  assert.equal(result.externalId, "at://did:plc:abc/app.bsky.feed.post/xyz123");
  assert.equal(
    result.externalUrl,
    "https://bsky.app/profile/alice.bsky.social/post/xyz123",
  );
});

test("bluesky deliver failure when login throws", async () => {
  const connector = new BlueskyConnector(() =>
    fakeAgent({
      async login() {
        throw new Error("invalid app password");
      },
    }),
  );
  const result = await connector.deliver(ctx, post);
  assert.equal(result.success, false);
  assert.equal(result.error, "invalid app password");
  assert.equal(result.externalId, undefined);
});
