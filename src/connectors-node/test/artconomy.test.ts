import { test } from "node:test";
import assert from "node:assert/strict";
import { ArtconomyConnector, cleanTags } from "../src/connectors/artconomy.js";
import type { MediaStore } from "../src/media-store.js";
import type { ScraperRequest, ScraperResponse, ScraperSession } from "../src/scraping/http.js";
import type { ConnectorContext, Post } from "../src/types.js";

const requester = JSON.stringify({ id: 42, username: "foxartist", guest: false });

function response(body: string, url: string, status = 200, headers?: HeadersInit): ScraperResponse {
  return { body, url, status, headers: new Headers(headers) };
}

class FakeSession implements ScraperSession {
  readonly requests: { path: string; request?: ScraperRequest }[] = [];

  constructor(private readonly responses: ScraperResponse[]) {}

  async request(path: string, request?: ScraperRequest): Promise<ScraperResponse> {
    this.requests.push({ path, request });
    const next = this.responses.shift();
    if (!next) throw new Error(`unexpected request to ${path}`);
    return next;
  }
}

const context: ConnectorContext = {
  connectorId: "connector-1",
  userId: "user-1",
  configJson: "{}",
  secretJson: JSON.stringify({ CookieHeader: "sessionid=sess; csrftoken=csrf-abc" }),
  targetId: null,
};

const tags = ["fox", "digital art", "Red Panda", "sketch", "commission"];

const imagePost: Post = {
  title: "Sunset fox",
  body: "A fox at sunset",
  tags,
  media: [{ container: "media", key: "user/file.png", contentType: "image/png", alt: null }],
  rating: "mature",
};

const journalPost: Post = { title: "News", body: "Commissions are open", tags: [], media: [] };

const mediaStore: MediaStore = {
  fetch: async () => Buffer.from("png-bytes"),
  async put() {},
  async presignedGetUrl() {
    return "https://example.test/staged";
  },
  async delete() {},
};

function connectorWith(session: FakeSession): ArtconomyConnector {
  return new ArtconomyConnector({ sessionFactory: async () => session, mediaStore });
}

function withConfig(config: object): ConnectorContext {
  return { ...context, configJson: JSON.stringify(config) };
}

function submissionSession(): FakeSession {
  return new FakeSession([
    response(requester, "https://artconomy.com/api/profiles/data/requester/"),
    response(JSON.stringify({ id: "asset-uuid", url: "u", file: "f" }), "https://artconomy.com/api/lib/asset/", 201),
    response(JSON.stringify({ id: 1234 }), "https://artconomy.com/api/profiles/account/foxartist/submissions/", 201),
  ]);
}

test("artconomy getLimits reports a single image", async () => {
  const limits = await connectorWith(new FakeSession([])).getLimits();
  assert.equal(limits.maxMediaAttachments, 1);
  assert.deepEqual(limits.supportedMimeTypes, ["image/jpeg", "image/png", "image/gif", "image/webp"]);
});

test("artconomy authenticates and identifies the account", async () => {
  const auth = await connectorWith(
    new FakeSession([response(requester, "https://artconomy.com/api/profiles/data/requester/")]),
  ).isAuthenticated(context);
  assert.deepEqual(auth, { isAuthenticated: true, detail: "foxartist" });

  const targets = await connectorWith(
    new FakeSession([response(requester, "https://artconomy.com/api/profiles/data/requester/")]),
  ).listTargets(context);
  assert.deepEqual(targets.targets, [{ id: "foxartist", name: "Artconomy: foxartist" }]);
});

test("artconomy treats anonymous and guest sessions as logged out", async () => {
  for (const body of [
    JSON.stringify({ username: "_", rating: 0 }),
    JSON.stringify({ id: 9, username: "__9", guest: true }),
  ]) {
    const result = await connectorWith(
      new FakeSession([response(body, "https://artconomy.com/api/profiles/data/requester/")]),
    ).isAuthenticated(context);
    assert.equal(result.isAuthenticated, false);
    assert.match(result.detail ?? "", /not logged in/);
  }
});

test("artconomy needs the CSRF cookie", async () => {
  const result = await connectorWith(new FakeSession([])).isAuthenticated({
    ...context,
    secretJson: JSON.stringify({ CookieHeader: "sessionid=sess" }),
  });
  assert.equal(result.isAuthenticated, false);
  assert.match(result.detail ?? "", /csrftoken/);
});

test("artconomy uploads the asset then creates the submission", async () => {
  const session = submissionSession();
  const result = await connectorWith(session).deliver(context, imagePost);

  assert.deepEqual(result, {
    success: true,
    externalId: "1234",
    externalUrl: "https://artconomy.com/submissions/1234/",
  });
  const [, upload, create] = session.requests;
  assert.equal(upload.path, "/api/lib/asset/");
  assert.equal((upload.request?.headers as Record<string, string>)["x-csrftoken"], "csrf-abc");
  const file = (upload.request?.body as FormData).get("files[]") as File;
  assert.equal(file.name, "file.png");

  assert.equal(create.path, "/api/profiles/account/foxartist/submissions/");
  assert.equal((create.request?.headers as Record<string, string>)["x-csrftoken"], "csrf-abc");
  assert.deepEqual(JSON.parse(String(create.request?.body)), {
    file: "asset-uuid",
    preview: null,
    title: "Sunset fox",
    caption: "A fox at sunset",
    rating: 1,
    tags: ["fox", "digital_art", "red_panda", "sketch", "commission"],
    artists: [42],
    characters: [],
    private: false,
    comments_disabled: false,
  });
});

test("artconomy applies the per-submission options", async () => {
  const session = submissionSession();
  await connectorWith(session).deliver(
    withConfig({ CreditAsArtist: "false", Private: "true", DisableComments: "true" }),
    imagePost,
  );
  const body = JSON.parse(String(session.requests[2].request?.body));
  assert.deepEqual(body.artists, []);
  assert.equal(body.private, true);
  assert.equal(body.comments_disabled, true);
});

test("artconomy rejects posts it can't submit before making any request", async () => {
  const cases: [Post, RegExp][] = [
    [{ ...imagePost, media: [imagePost.media[0], imagePost.media[0]] }, /single image/],
    [{ ...imagePost, tags: ["fox", "Fox", "fox!", "art", "sketch"] }, /at least 5 distinct tags \(got 3\)/],
    [{ ...imagePost, rating: null }, /content rating/],
    [{ ...imagePost, title: "x".repeat(101) }, /100 characters/],
    [{ ...imagePost, media: [{ ...imagePost.media[0], contentType: "video/mp4" }] }, /JPEG, PNG, GIF, and WebP/],
  ];
  for (const [post, error] of cases) {
    const session = new FakeSession([]);
    const result = await connectorWith(session).deliver(context, post);
    assert.equal(result.success, false);
    assert.match(result.error ?? "", error);
    assert.equal(session.requests.length, 0);
  }
});

test("artconomy surfaces the API's validation errors", async () => {
  const session = new FakeSession([
    response(requester, "https://artconomy.com/api/profiles/data/requester/"),
    response(JSON.stringify({ id: "asset-uuid" }), "https://artconomy.com/api/lib/asset/", 201),
    response(
      JSON.stringify({ rating: ["You must indicate your birthday to view adult content."] }),
      "https://artconomy.com/api/profiles/account/foxartist/submissions/",
      400,
    ),
  ]);
  const result = await connectorWith(session).deliver(context, imagePost);
  assert.equal(result.success, false);
  assert.match(result.error ?? "", /HTTP 400: rating: You must indicate your birthday/);
});

test("artconomy posts a text-only post as a journal", async () => {
  const session = new FakeSession([
    response(requester, "https://artconomy.com/api/profiles/data/requester/"),
    response(JSON.stringify({ id: 77 }), "https://artconomy.com/api/profiles/account/foxartist/journals/", 201),
  ]);
  const result = await connectorWith(session).deliver(withConfig({ DisableComments: "true" }), journalPost);

  assert.deepEqual(result, {
    success: true,
    externalId: "77",
    externalUrl: "https://artconomy.com/profile/foxartist/journals/77/",
  });
  assert.equal(session.requests[1].path, "/api/profiles/account/foxartist/journals/");
  assert.deepEqual(JSON.parse(String(session.requests[1].request?.body)), {
    subject: "News",
    body: "Commissions are open",
    comments_disabled: true,
  });
});

test("artconomy journals need a title", async () => {
  const result = await connectorWith(new FakeSession([])).deliver(context, { ...journalPost, title: null });
  assert.equal(result.success, false);
  assert.match(result.error ?? "", /require a title/);
});

test("artconomy cleans tags the way the site does", () => {
  assert.deepEqual(cleanTags(["Digital Art", "red-panda", "café", "  ", "fox!", "FOX", "x".repeat(60)]), [
    "digital_art",
    "red_panda",
    "cafe",
    "fox",
    "x".repeat(50),
  ]);
});
