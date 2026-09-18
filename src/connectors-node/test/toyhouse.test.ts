import { test } from "node:test";
import assert from "node:assert/strict";
import sharp from "sharp";
import {
  ToyhouseConnector,
  type ToyhouseConnectorOptions,
} from "../src/connectors/toyhouse.js";
import type { MediaStore } from "../src/media-store.js";
import type {
  ScraperRequest,
  ScraperResponse,
  ScraperSession,
} from "../src/scraping/http.js";
import type { ConnectorContext, Post } from "../src/types.js";

const charactersPage = `
  <html><nav class="navbar"><div class="display-user-tiny">
  <span class="display-user-username">FoxArtist</span>
  </div></nav></html>`;

const uploadForm = '<html><head><meta name="csrf-token" content="tok-abc"></head></html>';

function response(
  body: string,
  url: string,
  status = 200,
  headers?: HeadersInit,
): ScraperResponse {
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
  configJson: JSON.stringify({
    CharacterIds: "111, 222",
    ArtistName: "FoxArtist",
  }),
  secretJson: JSON.stringify({
    CookieHeader: "remember_web_59ba36addc2b2f9401580f014c7f58ea4e30989d=one",
  }),
  targetId: null,
};

const post: Post = {
  title: null,
  body: "A quick sketch",
  tags: [],
  media: [
    {
      container: "media",
      key: "user/file.png",
      contentType: "image/png",
      alt: "A red fox",
    },
  ],
  rating: "mature",
};

async function imageBytes(): Promise<Buffer> {
  return sharp({
    create: { width: 20, height: 20, channels: 3, background: "#cc6633" },
  }).png().toBuffer();
}

/** The non-`fetch` MediaStore members, unused by Toyhouse but required by the interface. */
const noopStoreExtras = {
  async put() {},
  async presignedGetUrl() {
    return "https://example.test/staged";
  },
  async delete() {},
};

function connectorWith(
  session: FakeSession,
  mediaStore?: MediaStore,
  extra: Partial<ToyhouseConnectorOptions> = {},
): ToyhouseConnector {
  return new ToyhouseConnector({
    sessionFactory: async () => session,
    mediaStore: mediaStore ?? { fetch: async () => imageBytes(), ...noopStoreExtras },
    ...extra,
  });
}

test("toyhouse getLimits reports the platform's fixed media caps", async () => {
  const connector = connectorWith(new FakeSession([]));
  const limits = await connector.getLimits();
  assert.deepEqual(limits, {
    // Caption length (255) is reported via the static ServiceDefinition descriptor instead, the
    // same convention every other fixed-spec connector follows (see furaffinity.test.ts).
    maxContentLength: null,
    maxMediaAttachments: 1,
    supportedMimeTypes: ["image/jpeg", "image/png", "image/gif"],
    imageSizeLimit: 4_194_304,
    videoSizeLimit: 4_194_304,
    imageMaxWidth: null,
    imageMaxHeight: null,
    videoMaxWidth: null,
    videoMaxHeight: null,
  });
});

test("toyhouse authenticates and identifies the account", async () => {
  const authSession = new FakeSession([
    response(charactersPage, "https://toyhou.se/~characters/manage/folder:all"),
  ]);
  const auth = await connectorWith(authSession).isAuthenticated(context);
  assert.deepEqual(auth, { isAuthenticated: true, detail: "FoxArtist" });

  const targetSession = new FakeSession([
    response(charactersPage, "https://toyhou.se/~characters/manage/folder:all"),
  ]);
  const targets = await connectorWith(targetSession).listTargets(context);
  assert.deepEqual(targets.targets, [{ id: "FoxArtist", name: "Toyhouse: FoxArtist" }]);
});

test("toyhouse reports an expired browser session", async () => {
  const session = new FakeSession([
    response("<html>Login</html>", "https://toyhou.se/~account/login"),
  ]);
  const result = await connectorWith(session).isAuthenticated(context);
  assert.equal(result.isAuthenticated, false);
  assert.match(result.detail ?? "", /not logged in/);
});

test("toyhouse delivers through the upload form", async () => {
  const session = new FakeSession([
    response(uploadForm, "https://toyhou.se/~images/upload"),
    response("<html>done</html>", "https://toyhou.se/~images/98765.my-art-piece"),
  ]);
  const fetched: { container: string; key: string }[] = [];
  const connector = connectorWith(session, {
    async fetch(container, key) {
      fetched.push({ container, key });
      return imageBytes();
    },
    ...noopStoreExtras,
  });

  const result = await connector.deliver(context, post);

  assert.deepEqual(result, {
    success: true,
    externalId: "98765",
    externalUrl: "https://toyhou.se/~images/98765.my-art-piece",
  });
  assert.deepEqual(fetched, [{ container: "media", key: "user/file.png" }]);
  assert.deepEqual(session.requests.map((r) => r.path), [
    "/~images/upload",
    "/~images/upload",
  ]);

  const upload = session.requests[1].request?.body;
  assert.ok(upload instanceof FormData);
  assert.equal(upload.get("_token"), "tok-abc");
  assert.equal(upload.get("caption"), "A quick sketch");
  assert.equal(upload.get("is_sexual"), "1");
  assert.equal(upload.get("authorized_privacy"), "0");
  assert.equal(upload.get("watermark_id"), "1");
  assert.deepEqual(upload.getAll("character_ids[]"), ["111", "222", ""]);
  assert.deepEqual(upload.getAll("artist[]"), ["onsite", "onsite"]);
  assert.deepEqual(upload.getAll("artist_username[]"), ["FoxArtist", ""]);
  assert.ok(upload.get("image") instanceof Blob);
});

test("toyhouse credits an off-site artist when a URL is given", async () => {
  const session = new FakeSession([
    response(uploadForm, "https://toyhou.se/~images/upload"),
    response("<html>done</html>", "https://toyhou.se/~images/98765.my-art-piece"),
  ]);
  const connector = connectorWith(session);
  const offSiteContext: ConnectorContext = {
    ...context,
    configJson: JSON.stringify({
      CharacterIds: "111",
      ArtistName: "FoxArtist",
      OffSiteArtistUrl: "https://example.test/artist",
    }),
  };

  await connector.deliver(offSiteContext, post);

  const upload = session.requests[1].request?.body as FormData;
  assert.deepEqual(upload.getAll("artist[]"), ["offsite", "onsite"]);
  assert.deepEqual(upload.getAll("artist_url[]"), ["https://example.test/artist", ""]);
  assert.deepEqual(upload.getAll("artist_name[]"), ["FoxArtist", ""]);
});

test("toyhouse rejects a missing rating before making requests", async () => {
  const session = new FakeSession([]);
  const result = await connectorWith(session).deliver(context, { ...post, rating: null });

  assert.equal(result.success, false);
  assert.match(result.error ?? "", /explicit content rating/);
  assert.equal(session.requests.length, 0);
});

test("toyhouse requires at least one image", async () => {
  const session = new FakeSession([]);
  const result = await connectorWith(session).deliver(context, { ...post, media: [] });

  assert.equal(result.success, false);
  assert.match(result.error ?? "", /require an image/);
  assert.equal(session.requests.length, 0);
});

test("toyhouse requires at least one character id", async () => {
  const session = new FakeSession([]);
  const noCharacters: ConnectorContext = { ...context, configJson: JSON.stringify({ ArtistName: "FoxArtist" }) };
  const result = await connectorWith(session).deliver(noCharacters, post);

  assert.equal(result.success, false);
  assert.match(result.error ?? "", /at least one character/);
  assert.equal(session.requests.length, 0);
});

test("toyhouse requires an artist name", async () => {
  const session = new FakeSession([]);
  const noArtist: ConnectorContext = { ...context, configJson: JSON.stringify({ CharacterIds: "111" }) };
  const result = await connectorWith(session).deliver(noArtist, post);

  assert.equal(result.success, false);
  assert.match(result.error ?? "", /artist name/);
  assert.equal(session.requests.length, 0);
});

test("toyhouse submits the author's chosen default when several images are attached", async () => {
  const session = new FakeSession([
    response(uploadForm, "https://toyhou.se/~images/upload"),
    response("<html>done</html>", "https://toyhou.se/~images/98765.my-art-piece"),
  ]);
  const fetched: { container: string; key: string }[] = [];
  const connector = connectorWith(session, {
    async fetch(container, key) {
      fetched.push({ container, key });
      return imageBytes();
    },
    ...noopStoreExtras,
  });

  const multiImagePost: Post = {
    ...post,
    media: [
      { container: "media", key: "user/first.png", contentType: "image/png", alt: null },
      {
        container: "media",
        key: "user/second.png",
        contentType: "image/png",
        alt: "chosen default",
        isDefault: true,
      },
    ],
  };

  const result = await connector.deliver(context, multiImagePost);

  assert.equal(result.success, true);
  assert.deepEqual(fetched, [{ container: "media", key: "user/second.png" }]);
});

test("toyhouse surfaces a rejected submission", async () => {
  const session = new FakeSession([
    response(uploadForm, "https://toyhou.se/~images/upload"),
    response(
      '<div class="alert-danger">You must own the selected characters.</div>',
      "https://toyhou.se/~images/upload",
    ),
  ]);

  const result = await connectorWith(session).deliver(context, post);
  assert.equal(result.success, false);
  assert.match(result.error ?? "", /You must own the selected characters/);
});

test("toyhouse reports an expired session found while delivering", async () => {
  const session = new FakeSession([
    response("<html>Login</html>", "https://toyhou.se/~account/login"),
  ]);

  const result = await connectorWith(session).deliver(context, post);
  assert.equal(result.success, false);
  assert.match(result.error ?? "", /not logged in/);
});
