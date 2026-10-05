import { test } from "node:test";
import assert from "node:assert/strict";
import { createHash } from "node:crypto";
import { SofurryConnector } from "../src/connectors/sofurry.js";
import type { MediaStore } from "../src/media-store.js";
import type { ConnectorContext, Post } from "../src/types.js";

interface Call {
  url: string;
  method: string;
  headers: Record<string, string>;
  body: RequestInit["body"];
}

/** Answers each request with the next queued response, recording what was sent. */
function fakeFetch(responses: [number, unknown][]) {
  const calls: Call[] = [];
  const impl = (async (input: string | URL | Request, init?: RequestInit) => {
    calls.push({
      url: String(input),
      method: init?.method ?? "GET",
      headers: (init?.headers ?? {}) as Record<string, string>,
      body: init?.body,
    });
    const next = responses.shift();
    if (!next) throw new Error(`unexpected request to ${String(input)}`);
    const [status, body] = next;
    return new Response(body === undefined ? null : JSON.stringify(body), { status });
  }) as typeof fetch;
  return { calls, impl };
}

const operational = JSON.stringify({ clientId: "client-1", clientSecret: "secret-1" });

const context: ConnectorContext = {
  connectorId: "connector-1",
  userId: "user-1",
  configJson: "{}",
  secretJson: JSON.stringify({ AccessToken: "access-1", RefreshToken: "refresh-1", ExpiresAt: "2027-01-01T00:00:00Z" }),
  operationalSecretJson: operational,
  targetId: null,
};

const me = { handle: "terra", username: "Terra", isAdult: true };

const post: Post = {
  title: "Sunset fox",
  body: "A fox at sunset",
  tags: ["fox", "digital art", "fox", " "],
  media: [
    { container: "media", key: "u/one.png", contentType: "image/png", alt: null },
    { container: "media", key: "u/two.webp", contentType: "image/webp", alt: null },
  ],
  rating: "extreme",
};

const mediaStore: MediaStore = {
  fetch: async () => Buffer.from("image-bytes"),
  async put() {},
  async presignedGetUrl() {
    return "https://example.test/staged";
  },
  async delete() {},
};

function connectorWith(responses: [number, unknown][]) {
  const fetch = fakeFetch(responses);
  return { connector: new SofurryConnector({ mediaStore, fetch: fetch.impl }), calls: fetch.calls };
}

function withConfig(config: object): ConnectorContext {
  return { ...context, configJson: JSON.stringify(config) };
}

test("sofurry authenticates and identifies the account", async () => {
  const { connector, calls } = connectorWith([[200, me], [200, me]]);
  assert.deepEqual(await connector.isAuthenticated(context), { isAuthenticated: true, detail: "Terra" });
  assert.deepEqual((await connector.listTargets(context)).targets, [{ id: "terra", name: "SoFurry: Terra" }]);
  assert.equal(calls[0].url, "https://api.sofurry.com/v1/user/me");
  assert.equal(calls[0].headers.authorization, "Bearer access-1");
});

test("sofurry treats the login redirect as a rejected token", async () => {
  const { connector } = connectorWith([[302, undefined]]);
  const result = await connector.isAuthenticated(context);
  assert.equal(result.isAuthenticated, false);
  assert.match(result.detail ?? "", /reconnect the account/);
});

test("sofurry creates a draft, attaches each image, then publishes it", async () => {
  const { connector, calls } = connectorWith([
    [200, me],
    [201, { id: "sub1" }],
    [201, { contentId: "c1" }],
    [201, { contentId: "c2" }],
    [200, { id: "sub1", privacy: 3 }],
  ]);
  const result = await connector.deliver(context, post);

  assert.deepEqual(result, { success: true, externalId: "sub1", externalUrl: "https://sofurry.com/s/sub1" });
  assert.deepEqual(
    calls.map((c) => `${c.method} ${c.url}`),
    [
      "GET https://api.sofurry.com/v1/user/me",
      "PUT https://api.sofurry.com/v1/submission",
      "POST https://api.sofurry.com/v1/submission/sub1/content",
      "POST https://api.sofurry.com/v1/submission/sub1/content",
      "POST https://api.sofurry.com/v1/submission/sub1",
    ],
  );
  assert.equal(((calls[2].body as FormData).get("file") as File).name, "one.png");
  assert.deepEqual(JSON.parse(String(calls[4].body)), {
    title: "Sunset fox",
    description: "A fox at sunset",
    category: 10,
    type: 11,
    rating: 20,
    privacy: 3,
    allowComments: true,
    allowDownloads: true,
    isWip: false,
    optimize: true,
    pixelPerfect: false,
    isAdvert: false,
    canPurchase: false,
    artistTags: ["fox", "digital art"],
    contentOrder: ["c1", "c2"],
  });
});

test("sofurry applies the per-submission options and files into folders", async () => {
  const { connector, calls } = connectorWith([
    [200, me],
    [200, [{ id: "abc", name: "Sketches" }, { id: "def", name: "WIPs" }]],
    [201, { id: "sub1" }],
    [201, { contentId: "c1" }],
    [200, { id: "sub1" }],
    [204, undefined],
    [403, { error: "Permission denied" }],
  ]);
  const result = await connector.deliver(
    withConfig({
      Type: "31",
      Privacy: "2",
      FolderIds: "abc, def",
      AllowComments: "false",
      AllowDownloads: "false",
      WorkInProgress: "true",
      PixelPerfect: "true",
    }),
    { ...post, media: [post.media[0]], rating: "general" },
  );

  // A folder failing after publication doesn't fail the delivery: a retry would post a duplicate.
  assert.equal(result.success, true);
  const body = JSON.parse(String(calls[4].body));
  assert.equal(body.category, 30);
  assert.equal(body.type, 31);
  assert.equal(body.privacy, 2);
  assert.equal(body.rating, 0);
  assert.equal(body.allowComments, false);
  assert.equal(body.allowDownloads, false);
  assert.equal(body.isWip, true);
  assert.equal(body.pixelPerfect, true);
  assert.equal(calls[5].url, "https://api.sofurry.com/v1/folder/abc/sub1");
  assert.equal(calls[6].url, "https://api.sofurry.com/v1/folder/def/sub1");
});

test("sofurry rejects an unknown folder before posting anything", async () => {
  const { connector, calls } = connectorWith([[200, me], [200, [{ id: "abc", name: "Sketches" }]]]);
  const result = await connector.deliver(withConfig({ FolderIds: "abc,zzz" }), post);
  assert.equal(result.success, false);
  assert.match(result.error ?? "", /folder not found: zzz/);
  assert.equal(calls.length, 2);
});

test("sofurry refuses non-clean ratings on accounts without adult content", async () => {
  const { connector, calls } = connectorWith([[200, { ...me, isAdult: false }]]);
  const result = await connector.deliver(context, { ...post, rating: "mature" });
  assert.equal(result.success, false);
  assert.match(result.error ?? "", /only allows Clean/);
  assert.equal(calls.length, 1);
});

test("sofurry rejects posts it can't submit before making any request", async () => {
  const cases: [Post, RegExp][] = [
    [{ ...post, title: " " }, /require a title/],
    [{ ...post, media: [] }, /at least one image/],
    [{ ...post, rating: null }, /content rating/],
    [{ ...post, media: [{ ...post.media[0], contentType: "video/mp4" }] }, /JPEG, PNG, GIF, and WebP/],
    [{ ...post, tags: Array.from({ length: 101 }, (_, i) => `tag${i}`) }, /at most 100 tags/],
    [{ ...post, body: "x".repeat(2001) }, /2000 characters/],
  ];
  for (const [bad, error] of cases) {
    const { connector, calls } = connectorWith([]);
    const result = await connector.deliver(context, bad);
    assert.equal(result.success, false);
    assert.match(result.error ?? "", error);
    assert.equal(calls.length, 0);
  }
});

test("sofurry surfaces the API's validation errors", async () => {
  const { connector } = connectorWith([
    [200, me],
    [201, { id: "sub1" }],
    [201, { contentId: "c1" }],
    [201, { contentId: "c2" }],
    [422, { message: "The given data was invalid.", errors: { title: ["The title field is required."] } }],
  ]);
  const result = await connector.deliver(context, post);
  assert.equal(result.success, false);
  assert.match(result.error ?? "", /HTTP 422: title: The title field is required\./);
});

test("sofurry oauth sends PKCE and exchanges the code with the stored verifier", async () => {
  const { connector, calls } = connectorWith([[200, { access_token: "a", refresh_token: "r", expires_in: 3600 }]]);
  const start = await connector.oauth.startAuthorization({
    callbackUrl: "https://app.example/api/connectors/oauth/callback",
    operationalSecretJson: operational,
  });
  const url = new URL(start.authorizeUrl);
  assert.equal(url.origin + url.pathname, "https://api.sofurry.com/oauth/authorize");
  assert.equal(url.searchParams.get("client_id"), "client-1");
  assert.equal(url.searchParams.get("state"), start.requestToken);
  assert.equal(url.searchParams.get("code_challenge_method"), "S256");

  const pending = JSON.parse(start.requestTokenSecret) as { callbackUrl: string; codeVerifier: string };
  assert.equal(
    url.searchParams.get("code_challenge"),
    createHash("sha256").update(pending.codeVerifier).digest("base64url"),
  );

  const before = Date.now();
  const done = await connector.oauth.completeAuthorization({
    requestToken: start.requestToken,
    requestTokenSecret: start.requestTokenSecret,
    verifier: "the-code",
    operationalSecretJson: operational,
  });
  const form = new URLSearchParams(String(calls[0].body));
  assert.equal(calls[0].url, "https://api.sofurry.com/oauth/token");
  assert.equal(form.get("grant_type"), "authorization_code");
  assert.equal(form.get("code"), "the-code");
  assert.equal(form.get("code_verifier"), pending.codeVerifier);
  assert.equal(form.get("redirect_uri"), "https://app.example/api/connectors/oauth/callback");
  assert.equal(form.get("client_secret"), "secret-1");

  const secret = JSON.parse(done.secretJson);
  assert.equal(secret.AccessToken, "a");
  assert.equal(secret.RefreshToken, "r");
  assert.ok(Date.parse(secret.ExpiresAt) >= before + 3600 * 1000);
});

test("sofurry oauth fails closed without app credentials", async () => {
  const { connector } = connectorWith([]);
  await assert.rejects(
    connector.oauth.startAuthorization({ callbackUrl: "https://app.example/cb", operationalSecretJson: null }),
    /not configured/,
  );
});

test("sofurry refresh rotates tokens and keeps the old refresh token if none is issued", async () => {
  const { connector, calls } = connectorWith([[200, { access_token: "a2", expires_in: 60 }]]);
  const result = await connector.refresh(context);
  const form = new URLSearchParams(String(calls[0].body));
  assert.equal(form.get("grant_type"), "refresh_token");
  assert.equal(form.get("refresh_token"), "refresh-1");
  const secret = JSON.parse(result!.secretJson);
  assert.equal(secret.AccessToken, "a2");
  assert.equal(secret.RefreshToken, "refresh-1");
});

test("sofurry refresh reports a revoked refresh token as declined", async () => {
  const { connector } = connectorWith([[400, { error: "invalid_grant" }]]);
  assert.equal(await connector.refresh(context), null);
});
