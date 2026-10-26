import { test } from "node:test";
import assert from "node:assert/strict";
import { createHash } from "node:crypto";
import { DeviantartConnector } from "../src/connectors/deviantart.js";
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

const post: Post = {
  title: "Sunset fox",
  body: "A fox at sunset",
  tags: ["fox", "digital art", "fox", "sci-fi", " "],
  media: [
    { container: "media", key: "u/one.png", contentType: "image/png", alt: null },
    { container: "media", key: "u/two.gif", contentType: "image/gif", alt: null, isDefault: true },
  ],
  rating: "adult",
};

const mediaStore: MediaStore = {
  fetch: async () => Buffer.from("image-bytes"),
  async put() {},
  async delete() {},
};

function connectorWith(responses: [number, unknown][]) {
  const fetch = fakeFetch(responses);
  return { connector: new DeviantartConnector({ mediaStore, fetch: fetch.impl }), calls: fetch.calls };
}

function withConfig(config: object): ConnectorContext {
  return { ...context, configJson: JSON.stringify(config) };
}

test("deviantart authenticates and identifies the account", async () => {
  const { connector, calls } = connectorWith([[200, { username: "Terra" }], [200, { username: "Terra" }]]);
  assert.deepEqual(await connector.isAuthenticated(context), { isAuthenticated: true, detail: "Terra" });
  assert.deepEqual((await connector.listTargets(context)).targets, [{ id: "Terra", name: "DeviantArt: Terra" }]);
  assert.equal(calls[0].url, "https://www.deviantart.com/api/v1/oauth2/user/whoami");
  assert.equal(calls[0].headers.authorization, "Bearer access-1");
});

test("deviantart reports a rejected token", async () => {
  const { connector } = connectorWith([[401, { error: "invalid_token" }]]);
  const result = await connector.isAuthenticated(context);
  assert.equal(result.isAuthenticated, false);
  assert.match(result.detail ?? "", /reconnect the account/);
});

test("deviantart uploads the primary image to Sta.sh, then publishes it", async () => {
  const { connector, calls } = connectorWith([
    [200, { status: "success", itemid: 42 }],
    [200, { status: "success", deviationid: "dev-1", url: "https://www.deviantart.com/terra/art/Sunset-fox-1" }],
  ]);
  const result = await connector.deliver(context, post);

  assert.deepEqual(result, {
    success: true,
    externalId: "dev-1",
    externalUrl: "https://www.deviantart.com/terra/art/Sunset-fox-1",
  });
  assert.deepEqual(
    calls.map((c) => `${c.method} ${c.url}`),
    [
      "POST https://www.deviantart.com/api/v1/oauth2/stash/submit",
      "POST https://www.deviantart.com/api/v1/oauth2/stash/publish",
    ],
  );

  const submit = calls[0].body as FormData;
  assert.equal(submit.get("title"), "Sunset fox");
  assert.equal(submit.get("artist_comments"), "A fox at sunset");
  assert.deepEqual([submit.get("tags[0]"), submit.get("tags[1]"), submit.get("tags[2]")], ["fox", "digital_art", "scifi"]);
  assert.equal(submit.get("tags[3]"), null);
  assert.equal((submit.get("file") as File).name, "two.gif");

  const publish = calls[1].body as URLSearchParams;
  assert.equal(publish.get("itemid"), "42");
  assert.equal(publish.get("is_mature"), "1");
  assert.equal(publish.get("mature_level"), "strict");
  assert.equal(publish.get("allow_comments"), "1");
  assert.deepEqual(publish.getAll("tags[]"), ["fox", "digital_art", "scifi"]);
  assert.equal(publish.get("noai"), null);
});

test("deviantart applies the per-submission options", async () => {
  const { connector, calls } = connectorWith([
    [200, { itemid: 42 }],
    [200, { deviationid: "dev-1", url: "https://www.deviantart.com/terra/art/x-1" }],
  ]);
  const result = await connector.deliver(
    withConfig({ GalleryIds: "aaa-1, bbb-2", AllowComments: "false", NoAi: "true", AiGenerated: "false" }),
    { ...post, rating: "general" },
  );

  assert.equal(result.success, true);
  const submit = calls[0].body as FormData;
  assert.equal(submit.get("noai"), "1");
  assert.equal(submit.get("is_ai_generated"), "0");
  const publish = calls[1].body as URLSearchParams;
  assert.equal(publish.get("is_mature"), "0");
  assert.equal(publish.get("mature_level"), null);
  assert.equal(publish.get("allow_comments"), "0");
  assert.deepEqual(publish.getAll("galleryids[]"), ["aaa-1", "bbb-2"]);
  assert.equal(publish.get("noai"), "1");
  assert.equal(publish.get("is_ai_generated"), "0");
});

test("deviantart maps mature to moderate", async () => {
  const { connector, calls } = connectorWith([[200, { itemid: 1 }], [200, { deviationid: "d" }]]);
  await connector.deliver(context, { ...post, rating: "mature" });
  assert.equal((calls[1].body as URLSearchParams).get("mature_level"), "moderate");
});

test("deviantart stops at Sta.sh in stash mode", async () => {
  const { connector, calls } = connectorWith([[200, { itemid: 42 }]]);
  const result = await connector.deliver(withConfig({ Mode: "stash" }), post);
  assert.deepEqual(result, { success: true, externalId: "42" });
  assert.equal(calls.length, 1);
});

test("deviantart rejects posts it can't submit before making any request", async () => {
  const cases: [Post, RegExp][] = [
    [{ ...post, media: [] }, /require an image/],
    [{ ...post, rating: null }, /content rating/],
    [{ ...post, media: [{ ...post.media[0], contentType: "image/webp" }] }, /JPEG, PNG, and GIF/],
  ];
  for (const [bad, error] of cases) {
    const { connector, calls } = connectorWith([]);
    const result = await connector.deliver(context, bad);
    assert.equal(result.success, false);
    assert.match(result.error ?? "", error);
    assert.equal(calls.length, 0);
  }
  const { connector, calls } = connectorWith([]);
  const result = await connector.deliver(withConfig({ Mode: "draft" }), post);
  assert.match(result.error ?? "", /invalid DeviantArt mode/);
  assert.equal(calls.length, 0);
});

test("deviantart surfaces the API's errors", async () => {
  const { connector } = connectorWith([
    [200, { itemid: 42 }],
    [400, { error: "invalid_request", error_description: "Must accept DA terms of service", error_code: 1 }],
  ]);
  const result = await connector.deliver(context, post);
  assert.equal(result.success, false);
  assert.match(result.error ?? "", /stash\/publish failed with HTTP 400: Must accept DA terms of service/);

  const validation = connectorWith([
    [400, { error: "invalid_request", error_description: "Request field validation failed.", error_details: { title: "title is too long" } }],
  ]);
  const failed = await validation.connector.deliver(context, post);
  assert.match(failed.error ?? "", /HTTP 400: title: title is too long/);
});

test("deviantart oauth sends PKCE and scopes and exchanges the code with the stored verifier", async () => {
  const { connector, calls } = connectorWith([[200, { access_token: "a", refresh_token: "r", expires_in: 3600 }]]);
  const start = await connector.oauth!.startAuthorization({
    callbackUrl: "https://app.example/api/connectors/oauth/callback",
    operationalSecretJson: operational,
  });
  const url = new URL(start.authorizeUrl);
  assert.equal(url.origin + url.pathname, "https://www.deviantart.com/oauth2/authorize");
  assert.equal(url.searchParams.get("response_type"), "code");
  assert.equal(url.searchParams.get("client_id"), "client-1");
  assert.equal(url.searchParams.get("scope"), "basic user stash publish");
  assert.equal(url.searchParams.get("state"), start.requestToken);
  assert.equal(url.searchParams.get("code_challenge_method"), "S256");

  const pending = JSON.parse(start.requestTokenSecret) as { callbackUrl: string; codeVerifier: string };
  assert.equal(
    url.searchParams.get("code_challenge"),
    createHash("sha256").update(pending.codeVerifier).digest("base64url"),
  );

  const before = Date.now();
  const done = await connector.oauth!.completeAuthorization({
    requestToken: start.requestToken,
    requestTokenSecret: start.requestTokenSecret,
    verifier: "the-code",
    operationalSecretJson: operational,
  });
  const form = new URLSearchParams(String(calls[0].body));
  assert.equal(calls[0].url, "https://www.deviantart.com/oauth2/token");
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

test("deviantart oauth fails closed without app credentials", async () => {
  const { connector } = connectorWith([]);
  await assert.rejects(
    connector.oauth!.startAuthorization({ callbackUrl: "https://app.example/cb", operationalSecretJson: null }),
    /not configured/,
  );
});

test("deviantart refresh stores the rotated refresh token", async () => {
  const { connector, calls } = connectorWith([[200, { access_token: "a2", refresh_token: "refresh-2", expires_in: 3600 }]]);
  const result = await connector.refresh(context);
  const form = new URLSearchParams(String(calls[0].body));
  assert.equal(form.get("grant_type"), "refresh_token");
  assert.equal(form.get("refresh_token"), "refresh-1");
  const secret = JSON.parse(result!.secretJson);
  assert.equal(secret.AccessToken, "a2");
  assert.equal(secret.RefreshToken, "refresh-2");
});

test("deviantart refresh reports a revoked refresh token as declined", async () => {
  const { connector } = connectorWith([[400, { error: "invalid_request" }]]);
  assert.equal(await connector.refresh(context), null);
});
