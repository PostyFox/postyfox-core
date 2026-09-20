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

/** The non-`fetch` MediaStore members, unused by FurAffinity but required by the interface. */
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
  extra: Partial<FurAffinityConnectorOptions> = {},
): FurAffinityConnector {
  return new FurAffinityConnector({
    sessionFactory: async () => session,
    mediaStore: mediaStore ?? { fetch: async () => imageBytes(), ...noopStoreExtras },
    minimumPostIntervalMs: 0,
    ...extra,
  });
}

test("furaffinity getLimits reports the platform's fixed media caps", async () => {
  const connector = connectorWith(new FakeSession([]));
  const limits = await connector.getLimits();
  assert.deepEqual(limits, {
    maxContentLength: null,
    maxMediaAttachments: 1,
    supportedMimeTypes: ["image/jpeg", "image/png", "image/gif"],
    imageSizeLimit: 10_485_760,
    videoSizeLimit: 10_485_760,
    imageMaxWidth: null,
    imageMaxHeight: null,
    videoMaxWidth: null,
    videoMaxHeight: null,
  });
});

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
    ...noopStoreExtras,
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

const journalPost: Post = { title: "News", body: "Hello journal", tags: [], media: [], rating: null };
const journalForm = '<form id="journal-form"><input name="key" value="journal-key"></form>';

test("furaffinity posts a text-only post as a journal", async () => {
  const session = new FakeSession([
    response(loggedInPage, "https://www.furaffinity.net/controls/submissions"),
    response(journalForm, "https://www.furaffinity.net/controls/journal"),
    response("<html>ok</html>", "https://www.furaffinity.net/journal/777/"),
  ]);

  const result = await connectorWith(session).deliver(context, journalPost);

  assert.deepEqual(result, {
    success: true,
    externalId: "777",
    externalUrl: "https://www.furaffinity.net/journal/777/",
  });
  assert.deepEqual(session.requests.map((r) => r.path), [
    "/controls/submissions",
    "/controls/journal",
    "/controls/journal/",
  ]);
  const form = session.requests[2].request?.body;
  assert.ok(form instanceof URLSearchParams);
  assert.equal(form.get("key"), "journal-key");
  assert.equal(form.get("subject"), "News");
  assert.equal(form.get("message"), "Hello journal");
  assert.equal(form.get("id"), "0");
  assert.equal(form.get("do"), "update");
  assert.equal(form.has("make_featured"), false);
});

test("furaffinity features a journal when the Feature option is chosen", async () => {
  const session = new FakeSession([
    response(loggedInPage, "https://www.furaffinity.net/controls/submissions"),
    response(journalForm, "https://www.furaffinity.net/controls/journal"),
    response("<html>ok</html>", "https://www.furaffinity.net/journal/777/"),
  ]);

  await connectorWith(session).deliver({ ...context, configJson: '{"Feature":"true"}' }, journalPost);

  const form = session.requests[2].request?.body;
  assert.ok(form instanceof URLSearchParams);
  assert.equal(form.get("make_featured"), "on");
});

test("furaffinity validates a journal title before making requests", async () => {
  const session = new FakeSession([]);
  const connector = connectorWith(session);

  assert.match((await connector.deliver(context, { ...journalPost, title: " " })).error ?? "", /requires a title/);
  const long = await connector.deliver(context, { ...journalPost, title: "x".repeat(61) });
  assert.match(long.error ?? "", /60 characters/);
  assert.equal(session.requests.length, 0);
});

test("furaffinity reports a journal that is not confirmed", async () => {
  const session = new FakeSession([
    response(loggedInPage, "https://www.furaffinity.net/controls/submissions"),
    response(journalForm, "https://www.furaffinity.net/controls/journal"),
    response("<html>nope</html>", "https://www.furaffinity.net/controls/journal/"),
  ]);

  const result = await connectorWith(session).deliver(context, journalPost);
  assert.equal(result.success, false);
  assert.match(result.error ?? "", /did not confirm the journal/);
});

test("furaffinity surfaces a journal form error and a missing form token", async () => {
  const rejected = new FakeSession([
    response(loggedInPage, "https://www.furaffinity.net/controls/submissions"),
    response(journalForm, "https://www.furaffinity.net/controls/journal"),
    response('<div class="redirect-message">Journal too long</div>', "https://www.furaffinity.net/controls/journal/"),
  ]);
  assert.match((await connectorWith(rejected).deliver(context, journalPost)).error ?? "", /Journal too long/);

  const noToken = new FakeSession([
    response(loggedInPage, "https://www.furaffinity.net/controls/submissions"),
    response("<html></html>", "https://www.furaffinity.net/controls/journal"),
  ]);
  assert.match((await connectorWith(noToken).deliver(context, journalPost)).error ?? "", /form token was not found/);
});

test("furaffinity journal requires a logged-in session", async () => {
  const session = new FakeSession([response("<html>Login</html>", "https://www.furaffinity.net/login")]);
  const result = await connectorWith(session).deliver(context, journalPost);
  assert.match(result.error ?? "", /not logged in/);
});

test("furaffinity submits the author's chosen default when several images are attached", async () => {
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
      { container: "media", key: "user/third.png", contentType: "image/png", alt: null },
    ],
  };

  const result = await connector.deliver(context, multiImagePost);

  assert.equal(result.success, true);
  // The rest of the post's attachments (destined for platforms that support multiple images) are
  // silently dropped rather than blocking the FurAffinity submission.
  assert.deepEqual(fetched, [{ container: "media", key: "user/second.png" }]);
});

test("furaffinity falls back to the first image when several are attached with no default chosen", async () => {
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
    ...noopStoreExtras,
  });

  const multiImagePost: Post = {
    ...post,
    media: [
      { container: "media", key: "user/first.png", contentType: "image/png", alt: null },
      { container: "media", key: "user/second.png", contentType: "image/png", alt: null },
    ],
  };

  const result = await connector.deliver(context, multiImagePost);

  assert.equal(result.success, true);
  assert.deepEqual(fetched, [{ container: "media", key: "user/first.png" }]);
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
