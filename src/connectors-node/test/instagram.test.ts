import { test } from "node:test";
import assert from "node:assert/strict";
import { InstagramConnector } from "../src/connectors/instagram.js";
import type { MediaStore } from "../src/media-store.js";
import type { ConnectorContext, Post } from "../src/types.js";

const app = { appId: "app-id", appSecret: "app-secret" };

/** Fake media store returning known bytes, recording fetch/put/delete calls. */
function fakeMediaStore(bytes: Buffer): MediaStore & {
  fetches: { container: string; key: string }[];
  puts: { container: string; key: string; contentType: string }[];
  deletes: { container: string; key: string }[];
} {
  const fetches: { container: string; key: string }[] = [];
  const puts: { container: string; key: string; contentType: string }[] = [];
  const deletes: { container: string; key: string }[] = [];
  return {
    fetches,
    puts,
    deletes,
    async fetch(container, key) {
      fetches.push({ container, key });
      return bytes;
    },
    async put(container, key, _bytes, contentType) {
      puts.push({ container, key, contentType });
    },
    async presignedGetUrl(container, key) {
      return `https://staged.example/${container}/${key}`;
    },
    async delete(container, key) {
      deletes.push({ container, key });
    },
  };
}

const ctx: ConnectorContext = {
  connectorId: "c",
  userId: "u",
  configJson: "{}",
  secretJson: JSON.stringify({ AccessToken: "tok", IgUserId: "ig1", ExpiresAt: "2099-01-01T00:00:00Z" }),
  targetId: null,
};

function buildConnector(store?: MediaStore): InstagramConnector {
  // videoPollIntervalMs: 0 — tests don't wait out real video-processing polls.
  return new InstagramConnector(app, store ?? fakeMediaStore(Buffer.from("fake-image-bytes")), 0);
}

/** Installs a fake global fetch for the duration of `fn`, then always restores the original. */
async function withFetch<T>(
  handler: (url: URL, init: RequestInit | undefined) => Response | Promise<Response>,
  fn: () => Promise<T>,
): Promise<T> {
  const orig = globalThis.fetch;
  globalThis.fetch = (async (input: string | URL | Request, init?: RequestInit) =>
    handler(new URL(input.toString()), init)) as typeof fetch;
  try {
    return await fn();
  } finally {
    globalThis.fetch = orig;
  }
}

function json(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), { status });
}

const post: Post = { title: null, body: "hello", tags: [], media: [] };
const mediaPost: Post = {
  ...post,
  media: [{ container: "media", key: "u1/abc/pic.jpg", contentType: "image/jpeg", alt: "a cat" }],
};

test("instagram is-authenticated true when the graph API resolves the account", async () => {
  const result = await withFetch(
    (url) => {
      assert.equal(url.pathname, "/ig1");
      return json({ id: "ig1", username: "alice" });
    },
    () => buildConnector().isAuthenticated(ctx),
  );
  assert.equal(result.isAuthenticated, true);
});

test("instagram is-authenticated false with a missing secret", async () => {
  const noSecret: ConnectorContext = { ...ctx, secretJson: null };
  const result = await buildConnector().isAuthenticated(noSecret);
  assert.equal(result.isAuthenticated, false);
});

test("instagram list-targets returns the linked business account", async () => {
  const result = await withFetch(
    () => json({ id: "ig1", username: "alice" }),
    () => buildConnector().listTargets(ctx),
  );
  assert.deepEqual(result.targets, [{ id: "ig1", name: "@alice" }]);
});

test("instagram getLimits reports the platform's fixed media caps", async () => {
  const limits = await buildConnector().getLimits();
  assert.deepEqual(limits, {
    maxContentLength: null,
    maxMediaAttachments: 10,
    supportedMimeTypes: ["image/jpeg", "video/mp4"],
    imageSizeLimit: 8_388_608,
    videoSizeLimit: 1_073_741_824,
    imageMaxWidth: 1440,
    imageMaxHeight: 1800,
    videoMaxWidth: 1920,
    videoMaxHeight: 1080,
  });
});

test("instagram deliver rejects a post with no media", async () => {
  const result = await buildConnector().deliver(ctx, post);
  assert.equal(result.success, false);
  assert.match(result.error ?? "", /requires at least one image or video/);
});

test("instagram deliver publishes a single image and stages + cleans up the object", async () => {
  const store = fakeMediaStore(Buffer.from("fake-image-bytes"));
  const calls: string[] = [];
  const result = await withFetch(
    (url, init) => {
      calls.push(`${init?.method ?? "GET"} ${url.pathname}`);
      if (url.pathname === "/ig1/media") {
        assert.ok(url.search === "" && typeof init?.body === "object"); // params travel in the POST body
        return json({ id: "container-1" });
      }
      if (url.pathname === "/ig1/media_publish") return json({ id: "published-1" });
      if (url.pathname === "/published-1") return json({ permalink: "https://instagram.com/p/xyz" });
      throw new Error(`unexpected request: ${url}`);
    },
    () => buildConnector(store).deliver(ctx, mediaPost),
  );

  assert.equal(result.success, true);
  assert.equal(result.externalId, "published-1");
  assert.equal(result.externalUrl, "https://instagram.com/p/xyz");
  assert.deepEqual(store.fetches, [{ container: "media", key: "u1/abc/pic.jpg" }]);
  assert.equal(store.puts.length, 1);
  assert.equal(store.puts[0].contentType, "image/jpeg");
  // The staged object is always cleaned up, success or failure.
  assert.deepEqual(store.deletes, [{ container: "instagram-staging", key: store.puts[0].key }]);
  assert.deepEqual(calls, ["POST /ig1/media", "POST /ig1/media_publish", "GET /published-1"]);
});

test("instagram deliver publishes a carousel for several images", async () => {
  const store = fakeMediaStore(Buffer.from("fake-image-bytes"));
  const carouselPost: Post = {
    ...post,
    media: [
      { container: "media", key: "u1/a.jpg", contentType: "image/jpeg", alt: null },
      { container: "media", key: "u1/b.jpg", contentType: "image/jpeg", alt: null },
    ],
  };
  let childCount = 0;
  const result = await withFetch(
    (url, init) => {
      if (url.pathname === "/ig1/media" && init?.method === "POST") {
        const body = new URLSearchParams(init.body as string);
        if (body.get("media_type") === "CAROUSEL") {
          assert.equal(body.get("children"), "child-0,child-1");
          return json({ id: "carousel-1" });
        }
        const id = `child-${childCount++}`;
        return json({ id });
      }
      if (url.pathname === "/ig1/media_publish") return json({ id: "published-1" });
      if (url.pathname === "/published-1") return json({});
      throw new Error(`unexpected request: ${url}`);
    },
    () => buildConnector(store).deliver(ctx, carouselPost),
  );

  assert.equal(result.success, true);
  assert.equal(result.externalId, "published-1");
  assert.equal(store.puts.length, 2);
  assert.equal(store.deletes.length, 2);
});

test("instagram deliver polls a video container until FINISHED before publishing", async () => {
  const store = fakeMediaStore(Buffer.from("fake-video-bytes"));
  const videoPost: Post = {
    ...post,
    media: [{ container: "media", key: "u1/clip.mp4", contentType: "video/mp4", alt: null }],
  };
  let polls = 0;
  const result = await withFetch(
    (url, init) => {
      if (url.pathname === "/ig1/media" && init?.method === "POST") {
        const body = new URLSearchParams(init.body as string);
        assert.equal(body.get("media_type"), "REELS");
        assert.ok(body.get("video_url")?.startsWith("https://staged.example/"));
        return json({ id: "container-1" });
      }
      if (url.pathname === "/container-1") {
        polls++;
        return json({ status_code: polls < 2 ? "IN_PROGRESS" : "FINISHED" });
      }
      if (url.pathname === "/ig1/media_publish") return json({ id: "published-1" });
      if (url.pathname === "/published-1") return json({});
      throw new Error(`unexpected request: ${url}`);
    },
    () => buildConnector(store).deliver(ctx, videoPost),
  );

  assert.equal(result.success, true);
  assert.ok(polls >= 2);
});

test("instagram deliver surfaces a platform error and still cleans up staged media", async () => {
  const store = fakeMediaStore(Buffer.from("fake-image-bytes"));
  const result = await withFetch(
    (url) => {
      if (url.pathname === "/ig1/media") return json({ error: { message: "Invalid image" } }, 400);
      throw new Error(`unexpected request: ${url}`);
    },
    () => buildConnector(store).deliver(ctx, mediaPost),
  );

  assert.equal(result.success, false);
  assert.match(result.error ?? "", /Invalid image/);
  assert.equal(store.deletes.length, 1);
});

test("instagram oauth start builds the authorize URL with app id + scope", async () => {
  const connector = buildConnector();
  const start = await connector.oauth!.startAuthorization({ callbackUrl: "https://app/cb" });
  assert.ok(start.authorizeUrl.startsWith("https://www.instagram.com/oauth/authorize?"));
  assert.ok(start.authorizeUrl.includes("client_id=app-id"));
  assert.ok(start.authorizeUrl.includes("scope=instagram_business_basic%2Cinstagram_business_content_publish"));
  assert.equal(start.requestTokenSecret, "https://app/cb");
});

test("instagram oauth complete exchanges the code for a long-lived token", async () => {
  const connector = buildConnector();
  const result = await withFetch(
    (url, init) => {
      if (url.hostname === "api.instagram.com") {
        assert.equal(init?.method, "POST");
        return json({ access_token: "short-lived", user_id: "ig1" });
      }
      if (url.pathname === "/access_token") {
        assert.equal(url.searchParams.get("grant_type"), "ig_exchange_token");
        return json({ access_token: "long-lived", expires_in: 5_184_000 });
      }
      throw new Error(`unexpected request: ${url}`);
    },
    () =>
      connector.oauth!.completeAuthorization({
        requestToken: "state",
        requestTokenSecret: "https://app/cb",
        verifier: "auth-code",
      }),
  );

  const secret = JSON.parse(result.secretJson);
  assert.equal(secret.AccessToken, "long-lived");
  assert.equal(secret.IgUserId, "ig1");
  assert.ok(secret.ExpiresAt);
});

test("instagram refresh renews the token and preserves the IG user id", async () => {
  const connector = buildConnector();
  const result = await withFetch(
    (url) => {
      assert.equal(url.pathname, "/refresh_access_token");
      assert.equal(url.searchParams.get("grant_type"), "ig_refresh_token");
      return json({ access_token: "refreshed", expires_in: 5_184_000 });
    },
    () => connector.refresh(ctx),
  );

  const secret = JSON.parse(result!.secretJson);
  assert.equal(secret.AccessToken, "refreshed");
  assert.equal(secret.IgUserId, "ig1");
});

test("instagram refresh returns null when there is no stored secret", async () => {
  const connector = buildConnector();
  const result = await connector.refresh({ ...ctx, secretJson: null });
  assert.equal(result, null);
});
