import { test } from "node:test";
import assert from "node:assert/strict";
import { BlueskyConnector, type BlueskyAgentLike } from "../src/connectors/bluesky.js";
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
    async uploadBlob() {
      return { data: { blob: { $type: "blob", ref: "blob-ref" } } };
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

test("bluesky deliver with media fetches, uploads blob and attaches images embed", async () => {
  const rawBytes = Buffer.from("fake-png-bytes");
  let uploadedBytes: Uint8Array | undefined;
  let uploadedEncoding: string | undefined;
  let postedRecord: { text: string; embed?: unknown } | undefined;

  const store = fakeMediaStore(rawBytes);
  const connector = new BlueskyConnector(
    () =>
      fakeAgent({
        async uploadBlob(bytes, opts) {
          uploadedBytes = bytes;
          uploadedEncoding = opts.encoding;
          return { data: { blob: { $type: "blob", ref: "blob-ref" } } };
        },
        async post(record) {
          postedRecord = record;
          return { uri: "at://did:plc:abc/app.bsky.feed.post/xyz123", cid: "cid1" };
        },
      }),
    store,
  );

  const mediaPost: Post = {
    title: null,
    body: "with image",
    tags: [],
    media: [
      {
        container: "media",
        key: "u1/abc/pic.png",
        contentType: "image/png",
        alt: "a red square",
      },
    ],
  };

  const result = await connector.deliver(ctx, mediaPost);
  assert.equal(result.success, true);

  // Media store fetched with the ref's container + key.
  assert.deepEqual(store.calls, [{ container: "media", key: "u1/abc/pic.png" }]);

  // Uploaded with the fetched bytes + correct encoding.
  assert.ok(uploadedBytes);
  assert.deepEqual(Buffer.from(uploadedBytes!), rawBytes);
  assert.equal(uploadedEncoding, "image/png");

  // Record carries an app.bsky.embed.images embed with the uploaded blob + alt.
  const embed = postedRecord?.embed as {
    $type: string;
    images: { image: unknown; alt: string }[];
  };
  assert.equal(embed.$type, "app.bsky.embed.images");
  assert.equal(embed.images.length, 1);
  assert.deepEqual(embed.images[0].image, { $type: "blob", ref: "blob-ref" });
  assert.equal(embed.images[0].alt, "a red square");
});

test("bluesky deliver caps media at 4 images", async () => {
  let uploadCount = 0;
  let postedRecord: { embed?: unknown } | undefined;
  const connector = new BlueskyConnector(
    () =>
      fakeAgent({
        async uploadBlob() {
          uploadCount++;
          return { data: { blob: { $type: "blob", ref: `ref-${uploadCount}` } } };
        },
        async post(record) {
          postedRecord = record;
          return { uri: "at://did:plc:abc/app.bsky.feed.post/xyz123", cid: "cid1" };
        },
      }),
    fakeMediaStore(Buffer.from("x")),
  );

  const item = {
    container: "media",
    key: "a.png",
    contentType: "image/png",
    alt: null,
  };
  const mediaPost: Post = {
    title: null,
    body: "many",
    tags: [],
    media: [item, item, item, item, item, item],
  };

  const result = await connector.deliver(ctx, mediaPost);
  assert.equal(result.success, true);
  assert.equal(uploadCount, 4);
  const embed = postedRecord?.embed as { images: unknown[] };
  assert.equal(embed.images.length, 4);
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
