import { test } from "node:test";
import assert from "node:assert/strict";
import {
  MegalodonConnector,
  type MegalodonClientLike,
  type MegalodonSns,
} from "../src/connectors/megalodon.js";
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
  configJson: JSON.stringify({ InstanceUrl: "https://shrimp.example" }),
  secretJson: JSON.stringify({ AccessToken: "tok", Sns: "firefish" }),
  targetId: null,
};

const post: Post = { title: "CW", body: "hello world", tags: ["cats", "#dogs"], media: [] };

function fakeClient(over: Partial<MegalodonClientLike> = {}): MegalodonClientLike {
  return {
    async registerApp() {
      return {
        client_id: "cid",
        client_secret: "csecret",
        url: "https://shrimp.example/auth/sess",
        session_token: "sess-tok",
      };
    },
    async fetchAccessToken() {
      return { access_token: "new-access-token" };
    },
    async verifyAccountCredentials() {
      return { data: { acct: "alice", username: "alice", url: "https://shrimp.example/@alice" } };
    },
    async uploadMedia() {
      return { data: { id: "media-1" } };
    },
    async postStatus() {
      return { data: { id: "42", url: "https://shrimp.example/notes/42" } };
    },
    async getInstance() {
      return {
        data: {
          configuration: {
            statuses: { max_characters: 5000, max_media_attachments: 4 },
            media_attachments: {
              supported_mime_types: ["image/jpeg", "image/png", "video/mp4"],
              image_size_limit: 10_485_760,
              video_size_limit: 41_943_040,
            },
          },
        },
      };
    },
    ...over,
  };
}

/** Builds a connector whose client factory + detector are stubbed for tests. */
function build(
  client: MegalodonClientLike,
  store?: MediaStore,
  detect: (url: string) => Promise<MegalodonSns> = async () => "firefish",
): MegalodonConnector {
  return new MegalodonConnector("firefish", store ?? fakeMediaStore(Buffer.from("")), () => client, detect);
}

test("megalodon is-authenticated true when credentials verify", async () => {
  const result = await build(fakeClient()).isAuthenticated(ctx);
  assert.equal(result.isAuthenticated, true);
});

test("megalodon is-authenticated false on verify failure", async () => {
  const result = await build(
    fakeClient({
      async verifyAccountCredentials() {
        throw new Error("401 unauthorized");
      },
    }),
  ).isAuthenticated(ctx);
  assert.equal(result.isAuthenticated, false);
  assert.ok(result.detail?.includes("401"));
});

test("megalodon list-targets returns the account handle", async () => {
  const result = await build(fakeClient()).listTargets(ctx);
  assert.deepEqual(result.targets, [
    { id: "https://shrimp.example/@alice", name: "@alice@shrimp.example" },
  ]);
});

test("megalodon deliver appends tags as hashtags and returns id + url", async () => {
  let receivedStatus: string | undefined;
  let receivedOptions: { spoiler_text?: string; media_ids?: string[] } | undefined;
  const result = await build(
    fakeClient({
      async postStatus(status, options) {
        receivedStatus = status;
        receivedOptions = options;
        return { data: { id: "42", url: "https://shrimp.example/notes/42" } };
      },
    }),
  ).deliver(ctx, post);

  assert.equal(result.success, true);
  assert.equal(result.externalId, "42");
  assert.equal(result.externalUrl, "https://shrimp.example/notes/42");
  assert.ok(receivedStatus?.includes("hello world"));
  assert.ok(receivedStatus?.includes("#cats"));
  assert.ok(receivedStatus?.includes("#dogs"));
  assert.equal(receivedOptions?.spoiler_text, "CW");
  assert.equal(receivedOptions?.media_ids, undefined);
});

test("megalodon deliver uploads media and attaches ids", async () => {
  const store = fakeMediaStore(Buffer.from("img-bytes"));
  let uploadCount = 0;
  let receivedMediaIds: string[] | undefined;
  const connector = build(
    fakeClient({
      async uploadMedia() {
        uploadCount++;
        return { data: { id: `media-${uploadCount}` } };
      },
      async postStatus(_status, options) {
        receivedMediaIds = options?.media_ids;
        return { data: { id: "43", url: "https://shrimp.example/notes/43" } };
      },
    }),
    store,
  );

  const mediaPost: Post = {
    title: null,
    body: "with pics",
    tags: [],
    media: [{ container: "media", key: "u1/a/pic.jpg", contentType: "image/jpeg", alt: "a cat" }],
  };

  const result = await connector.deliver(ctx, mediaPost);
  assert.equal(result.success, true);
  assert.equal(uploadCount, 1);
  assert.deepEqual(receivedMediaIds, ["media-1"]);
  assert.deepEqual(store.calls, [{ container: "media", key: "u1/a/pic.jpg" }]);
});

test("megalodon deliver sets alt text via setMediaComment where the driver drops it (firefish)", async () => {
  const store = fakeMediaStore(Buffer.from("img-bytes"));
  const commentCalls: { fileId: string; comment: string }[] = [];
  const connector = build(
    fakeClient({
      async uploadMedia() {
        return { data: { id: "media-1" } };
      },
      async setMediaComment(fileId, comment) {
        commentCalls.push({ fileId, comment });
      },
    }),
    store,
  );

  const mediaPost: Post = {
    title: null,
    body: "with pics",
    tags: [],
    media: [{ container: "media", key: "u1/a/pic.jpg", contentType: "image/jpeg", alt: "a cat" }],
  };

  const result = await connector.deliver(ctx, mediaPost);
  assert.equal(result.success, true);
  assert.deepEqual(commentCalls, [{ fileId: "media-1", comment: "a cat" }]);
});

test("megalodon deliver skips setMediaComment when there is no alt text", async () => {
  const store = fakeMediaStore(Buffer.from("img-bytes"));
  let commentCalled = false;
  const connector = build(
    fakeClient({
      async uploadMedia() {
        return { data: { id: "media-1" } };
      },
      async setMediaComment() {
        commentCalled = true;
      },
    }),
    store,
  );

  const mediaPost: Post = {
    title: null,
    body: "with pics",
    tags: [],
    media: [{ container: "media", key: "u1/a/pic.jpg", contentType: "image/jpeg", alt: "" }],
  };

  const result = await connector.deliver(ctx, mediaPost);
  assert.equal(result.success, true);
  assert.equal(commentCalled, false);
});

test("megalodon deliver surfaces a failure to set alt text", async () => {
  const store = fakeMediaStore(Buffer.from("img-bytes"));
  const connector = build(
    fakeClient({
      async uploadMedia() {
        return { data: { id: "media-1" } };
      },
      async setMediaComment() {
        throw new Error("drive/files/update failed: HTTP 403");
      },
    }),
    store,
  );

  const mediaPost: Post = {
    title: null,
    body: "with pics",
    tags: [],
    media: [{ container: "media", key: "u1/a/pic.jpg", contentType: "image/jpeg", alt: "a cat" }],
  };

  const result = await connector.deliver(ctx, mediaPost);
  assert.equal(result.success, false);
  assert.ok(result.error?.includes("403"));
});

test("megalodon getLimits reports character, attachment, MIME and size caps", async () => {
  const limits = await build(fakeClient()).getLimits(ctx);
  assert.deepEqual(limits, {
    maxContentLength: 5000,
    maxMediaAttachments: 4,
    supportedMimeTypes: ["image/jpeg", "image/png", "video/mp4"],
    imageSizeLimit: 10_485_760,
    videoSizeLimit: 41_943_040,
  });
});

test("megalodon getLimits returns nulls when the instance omits limits", async () => {
  const limits = await build(
    fakeClient({
      async getInstance() {
        return { data: {} };
      },
    }),
  ).getLimits(ctx);
  assert.deepEqual(limits, {
    maxContentLength: null,
    maxMediaAttachments: null,
    supportedMimeTypes: null,
    imageSizeLimit: null,
    videoSizeLimit: null,
  });
});

test("megalodon deliver fails clearly when a media type is unsupported", async () => {
  let uploaded = false;
  const connector = build(
    fakeClient({
      async uploadMedia() {
        uploaded = true;
        return { data: { id: "m" } };
      },
    }),
    fakeMediaStore(Buffer.from("gif-bytes")),
  );
  const gifPost: Post = {
    title: null,
    body: "pic",
    tags: [],
    media: [{ container: "media", key: "a.gif", contentType: "image/gif", alt: null }],
  };
  const result = await connector.deliver(ctx, gifPost);
  assert.equal(result.success, false);
  assert.ok(result.error?.includes("image/gif"));
  assert.equal(uploaded, false);
});

test("megalodon deliver fails clearly when a media item exceeds the size limit", async () => {
  let uploaded = false;
  const bigImage = Buffer.alloc(20); // 20 bytes, over the 10-byte limit below
  const connector = build(
    fakeClient({
      async getInstance() {
        return {
          data: {
            configuration: {
              statuses: { max_characters: 5000, max_media_attachments: 4 },
              media_attachments: { supported_mime_types: ["image/png"], image_size_limit: 10, video_size_limit: 10 },
            },
          },
        };
      },
      async uploadMedia() {
        uploaded = true;
        return { data: { id: "m" } };
      },
    }),
    fakeMediaStore(bigImage),
  );
  const pngPost: Post = {
    title: null,
    body: "pic",
    tags: [],
    media: [{ container: "media", key: "big.png", contentType: "image/png", alt: null }],
  };
  const result = await connector.deliver(ctx, pngPost);
  assert.equal(result.success, false);
  assert.ok(result.error?.includes("at most 10"));
  assert.equal(uploaded, false);
});

test("megalodon deliver fails clearly when the post exceeds the character limit", async () => {
  let posted = false;
  const connector = build(
    fakeClient({
      async getInstance() {
        return { data: { configuration: { statuses: { max_characters: 10, max_media_attachments: 4 } } } };
      },
      async postStatus() {
        posted = true;
        return { data: { id: "x" } };
      },
    }),
  );
  const result = await connector.deliver(ctx, { title: null, body: "way too long for ten", tags: [], media: [] });
  assert.equal(result.success, false);
  assert.ok(result.error?.includes("at most 10"));
  assert.equal(posted, false); // must not post when over the limit
});

test("megalodon deliver fails clearly when there are too many attachments", async () => {
  let uploaded = false;
  const store = fakeMediaStore(Buffer.from("img"));
  const connector = build(
    fakeClient({
      async getInstance() {
        return { data: { configuration: { statuses: { max_characters: 5000, max_media_attachments: 1 } } } };
      },
      async uploadMedia() {
        uploaded = true;
        return { data: { id: "m" } };
      },
    }),
    store,
  );
  const twoImages: Post = {
    title: null,
    body: "pics",
    tags: [],
    media: [
      { container: "media", key: "a.jpg", contentType: "image/jpeg", alt: null },
      { container: "media", key: "b.jpg", contentType: "image/jpeg", alt: null },
    ],
  };
  const result = await connector.deliver(ctx, twoImages);
  assert.equal(result.success, false);
  assert.ok(result.error?.includes("at most 1"));
  assert.equal(uploaded, false); // must not upload when over the limit
});

test("megalodon deliver fails when the access token is missing", async () => {
  const badCtx: ConnectorContext = { ...ctx, secretJson: null };
  const result = await build(fakeClient()).deliver(badCtx, post);
  assert.equal(result.success, false);
  assert.ok(result.error?.includes("access token"));
});

test("megalodon oauth start registers an app and carries the MiAuth session token", async () => {
  const start = await build(fakeClient()).oauth.startAuthorization({
    callbackUrl: "https://app/cb",
    configJson: JSON.stringify({ InstanceUrl: "shrimp.example" }),
  });
  // For MiAuth the correlation key is the session token, and the authorize URL is used verbatim.
  assert.equal(start.requestToken, "sess-tok");
  assert.equal(start.authorizeUrl, "https://shrimp.example/auth/sess");
  const pending = JSON.parse(start.requestTokenSecret);
  assert.equal(pending.instanceUrl, "https://shrimp.example");
  assert.equal(pending.sessionToken, "sess-tok");
  assert.equal(pending.clientSecret, "csecret");
});

test("megalodon oauth start requests granular write:notes permission for firefish (Iceshrimp)", async () => {
  // Regression: registering with coarse Mastodon "read"/"write" scopes yields an app that cannot
  // create notes on Iceshrimp/Firefish — /api/notes/create returns PERMISSION_DENIED.
  let requested: string[] | undefined;
  const client = fakeClient({
    async registerApp(_name, options) {
      requested = options?.scopes;
      return { client_id: "cid", client_secret: "csecret", url: "https://shrimp.example/auth/sess", session_token: "sess-tok" };
    },
  });
  await build(client).oauth.startAuthorization({
    callbackUrl: "https://app/cb",
    configJson: JSON.stringify({ InstanceUrl: "shrimp.example" }),
  });
  assert.ok(requested, "registerApp should receive scopes");
  assert.ok(requested.includes("write:notes"), `expected granular Misskey permissions, got ${JSON.stringify(requested)}`);
  assert.ok(!requested.includes("write"), "should not send coarse Mastodon scopes to a firefish instance");
});

test("megalodon oauth start requests coarse read/write scopes for Mastodon-style providers", async () => {
  let requested: string[] | undefined;
  const client = fakeClient({
    async registerApp(_name, options) {
      requested = options?.scopes;
      return { client_id: "cid", client_secret: "csecret", url: "https://mastodon.example/oauth/authorize?client_id=cid", session_token: null };
    },
  });
  const connector = new MegalodonConnector("mastodon", fakeMediaStore(Buffer.from("")), () => client, async () => "mastodon");
  await connector.oauth.startAuthorization({
    callbackUrl: "https://app/cb",
    configJson: JSON.stringify({ InstanceUrl: "https://mastodon.example" }),
  });
  assert.deepEqual(requested, ["read", "write"]);
});

test("megalodon oauth start (OAuth2, no session token) appends state to the authorize URL", async () => {
  // Mastodon-style: registerApp returns an authorize URL and no session token.
  const client = fakeClient({
    async registerApp() {
      return {
        client_id: "cid",
        client_secret: "csecret",
        url: "https://mastodon.example/oauth/authorize?client_id=cid&response_type=code",
        session_token: null,
      };
    },
  });
  const connector = new MegalodonConnector("mastodon", fakeMediaStore(Buffer.from("")), () => client, async () => "mastodon");
  const start = await connector.oauth.startAuthorization({
    callbackUrl: "https://app/cb",
    configJson: JSON.stringify({ InstanceUrl: "https://mastodon.example" }),
  });
  // Correlation is a generated state, echoed on the authorize URL for the provider to return.
  assert.ok(start.authorizeUrl.includes(`state=${encodeURIComponent(start.requestToken)}`));
  const pending = JSON.parse(start.requestTokenSecret);
  assert.equal(pending.sessionToken, null);
  assert.equal(pending.sns, "mastodon");
});

test("megalodon oauth complete (OAuth2) exchanges the callback code for an access token", async () => {
  let exchanged: string | undefined;
  const client = fakeClient({
    async fetchAccessToken(_clientId, _clientSecret, codeOrSession) {
      exchanged = codeOrSession;
      return { access_token: "at2" };
    },
  });
  const requestTokenSecret = JSON.stringify({
    instanceUrl: "https://mastodon.example",
    sns: "mastodon",
    clientId: "cid",
    clientSecret: "csecret",
    sessionToken: null,
    callbackUrl: "https://app/cb",
  });
  const done = await new MegalodonConnector(
    "mastodon",
    fakeMediaStore(Buffer.from("")),
    () => client,
    async () => "mastodon",
  ).oauth.completeAuthorization({ requestToken: "state-123", requestTokenSecret, verifier: "auth-code-xyz" });
  // No session token → the callback `code` (verifier) is exchanged.
  assert.equal(exchanged, "auth-code-xyz");
  assert.deepEqual(JSON.parse(done.secretJson), { AccessToken: "at2", Sns: "mastodon" });
});

test("megalodon oauth complete exchanges the session token for an access token", async () => {
  let exchanged: { code?: string } | undefined;
  const client = fakeClient({
    async fetchAccessToken(_clientId, _clientSecret, codeOrSession) {
      exchanged = { code: codeOrSession };
      return { access_token: "new-access-token" };
    },
  });
  const requestTokenSecret = JSON.stringify({
    instanceUrl: "https://shrimp.example",
    sns: "firefish",
    clientId: "cid",
    clientSecret: "csecret",
    sessionToken: "sess-tok",
    callbackUrl: "https://app/cb",
  });
  const done = await build(client).oauth.completeAuthorization({
    requestToken: "sess-tok",
    requestTokenSecret,
    verifier: "",
  });
  // MiAuth ignores the (empty) verifier and exchanges the stored session token.
  assert.equal(exchanged?.code, "sess-tok");
  assert.deepEqual(JSON.parse(done.secretJson), { AccessToken: "new-access-token", Sns: "firefish" });
});
