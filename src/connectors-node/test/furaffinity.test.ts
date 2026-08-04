import { test } from "node:test";
import assert from "node:assert/strict";
import sharp from "sharp";
import {
  FurAffinityConnector,
  type FurAffinityConnectorOptions,
} from "../src/connectors/furaffinity.js";
import type { MediaStore } from "../src/media-store.js";
import type {
  ScraperRequest,
  ScraperResponse,
  ScraperSession,
} from "../src/scraping/http.js";
import type { ConnectorContext, Post } from "../src/types.js";

const loggedInPage = `
  <html><a id="logout-link">Logout</a>
  <img class="loggedin_user_avatar" alt="FoxArtist"></html>`;

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
    Category: "1",
    Theme: "2",
    Species: "3",
    Gender: "4",
    FolderIds: "10, 20",
  }),
  secretJson: JSON.stringify({ CookieHeader: "a=one; b=two" }),
  targetId: null,
};

const post: Post = {
  title: "A fox",
  body: "Description",
  tags: ["red fox", "digital/art", "portrait"],
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

function connectorWith(
  session: FakeSession,
  mediaStore?: MediaStore,
  extra: Partial<FurAffinityConnectorOptions> = {},
): FurAffinityConnector {
  return new FurAffinityConnector({
    sessionFactory: async () => session,
    mediaStore: mediaStore ?? { fetch: async () => imageBytes() },
    minimumPostIntervalMs: 0,
    ...extra,
  });
}

test("furaffinity authenticates and identifies the account", async () => {
  const authSession = new FakeSession([
    response(loggedInPage, "https://www.furaffinity.net/controls/submissions"),
  ]);
  const auth = await connectorWith(authSession).isAuthenticated(context);
  assert.deepEqual(auth, { isAuthenticated: true, detail: "FoxArtist" });

  const targetSession = new FakeSession([
    response(loggedInPage, "https://www.furaffinity.net/controls/submissions"),
  ]);
  const targets = await connectorWith(targetSession).listTargets(context);
  assert.deepEqual(targets.targets, [
    { id: "FoxArtist", name: "FurAffinity: FoxArtist" },
  ]);
});

test("furaffinity reports an expired browser session", async () => {
  const session = new FakeSession([
    response("<html>Login</html>", "https://www.furaffinity.net/login"),
  ]);
  const result = await connectorWith(session).isAuthenticated(context);
  assert.equal(result.isAuthenticated, false);
  assert.match(result.detail ?? "", /not logged in/);
});

test("furaffinity delivers through upload and finalize forms", async () => {
  const session = new FakeSession([
    response(loggedInPage, "https://www.furaffinity.net/controls/submissions"),
    response(
      '<form id="upload_form"><input name="key" value="upload-key"></form>',
      "https://www.furaffinity.net/submit/",
    ),
    response(
      '<form id="myform"><input name="key" value="finalize-key"></form>',
      "https://www.furaffinity.net/submit/upload",
    ),
    response(
      "<html>done</html>",
      "https://www.furaffinity.net/submit/finalize",
      302,
      { location: "/view/12345/?upload-successful" },
    ),
  ]);
  const fetched: { container: string; key: string }[] = [];
  const connector = connectorWith(session, {
    async fetch(container, key) {
      fetched.push({ container, key });
      return imageBytes();
    },
  });

  const result = await connector.deliver(context, post);

  assert.deepEqual(result, {
    success: true,
    externalId: "12345",
    externalUrl: "https://www.furaffinity.net/view/12345/",
  });
  assert.deepEqual(fetched, [{ container: "media", key: "user/file.png" }]);
  assert.deepEqual(session.requests.map((r) => r.path), [
    "/controls/submissions",
    "/submit/",
    "/submit/upload",
    "/submit/finalize",
  ]);

  const upload = session.requests[2].request?.body;
  assert.ok(upload instanceof FormData);
  assert.equal(upload.get("key"), "upload-key");
  assert.equal(upload.get("submission_type"), "submission");
  assert.ok(upload.get("submission") instanceof Blob);
  assert.ok(upload.get("thumbnail") instanceof Blob);

  const finalize = session.requests[3].request?.body;
  assert.ok(finalize instanceof URLSearchParams);
  assert.equal(finalize.get("key"), "finalize-key");
  assert.equal(finalize.get("rating"), "2");
  assert.equal(finalize.get("keywords"), "red_fox digital_art portrait");
  assert.equal(finalize.get("atype"), "2");
  assert.deepEqual(finalize.getAll("folder_ids[]"), ["10", "20"]);
});

test("furaffinity rejects a missing rating before making requests", async () => {
  const session = new FakeSession([]);
  const result = await connectorWith(session).deliver(context, {
    ...post,
    rating: null,
  });

  assert.equal(result.success, false);
  assert.match(result.error ?? "", /explicit content rating/);
  assert.equal(session.requests.length, 0);
});

test("furaffinity surfaces the account CAPTCHA restriction", async () => {
  const session = new FakeSession([
    response(loggedInPage, "https://www.furaffinity.net/controls/submissions"),
    response(
      '<form id="upload_form"><input name="key" value="upload-key"></form>',
      "https://www.furaffinity.net/submit/",
    ),
    response(
      '<div class="redirect-message">CAPTCHA required</div>',
      "https://www.furaffinity.net/submit/upload",
    ),
  ]);

  const result = await connectorWith(session).deliver(context, post);
  assert.equal(result.success, false);
  assert.match(result.error ?? "", /11 existing submissions/);
});
