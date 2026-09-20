import { test } from "node:test";
import assert from "node:assert/strict";
import { KofiConnector } from "../src/connectors/kofi.js";
import type { MediaStore } from "../src/media-store.js";
import type { ScraperRequest, ScraperResponse, ScraperSession } from "../src/scraping/http.js";
import type { ConnectorContext, Post } from "../src/types.js";

const settingsPage = `
  <html><li id="profile-tab"></li>
  <form><input name="__RequestVerificationToken" value="tok-abc">
  <input name="DisplayName" value="Fox Artist"><input id="handle" value="foxartist"></form></html>`;

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
  secretJson: JSON.stringify({ CookieHeader: "kofi_identity_cookie=one" }),
  targetId: null,
};

const textPost: Post = { title: "Update", body: "Hello there", tags: ["news", "art"], media: [] };

const imagePost: Post = {
  ...textPost,
  media: [{ container: "media", key: "user/file.png", contentType: "image/png", alt: null }],
};

const mediaStore: MediaStore = {
  fetch: async () => Buffer.from("png-bytes"),
  async put() {},
  async presignedGetUrl() {
    return "https://example.test/staged";
  },
  async delete() {},
};

function connectorWith(session: FakeSession): KofiConnector {
  return new KofiConnector({ sessionFactory: async () => session, mediaStore });
}

function withConfig(config: object): ConnectorContext {
  return { ...context, configJson: JSON.stringify(config) };
}

test("kofi getLimits reports the fixed media caps", async () => {
  const limits = await connectorWith(new FakeSession([])).getLimits();
  assert.equal(limits.maxMediaAttachments, 10);
  assert.deepEqual(limits.supportedMimeTypes, ["image/jpeg", "image/png", "image/gif"]);
  assert.equal(limits.imageSizeLimit, null);
});

test("kofi authenticates and identifies the account", async () => {
  const auth = await connectorWith(
    new FakeSession([response(settingsPage, "https://ko-fi.com/settings")]),
  ).isAuthenticated(context);
  assert.deepEqual(auth, { isAuthenticated: true, detail: "Fox Artist" });

  const targets = await connectorWith(
    new FakeSession([response(settingsPage, "https://ko-fi.com/settings")]),
  ).listTargets(context);
  assert.deepEqual(targets.targets, [{ id: "foxartist", name: "Ko-fi: Fox Artist" }]);
});

test("kofi reports an expired browser session", async () => {
  const session = new FakeSession([response("<html>Login</html>", "https://ko-fi.com/account/login")]);
  const result = await connectorWith(session).isAuthenticated(context);
  assert.equal(result.isAuthenticated, false);
  assert.match(result.detail ?? "", /not logged in/);
});

test("kofi reports a missing secret", async () => {
  const result = await connectorWith(new FakeSession([])).isAuthenticated({ ...context, secretJson: null });
  assert.equal(result.isAuthenticated, false);
  assert.match(result.detail ?? "", /missing Ko-fi session cookie/);
});

test("kofi surfaces a Cloudflare challenge", async () => {
  const session = new FakeSession([
    response("<title>Just a moment...</title>", "https://ko-fi.com/settings", 403, { "cf-mitigated": "challenge" }),
  ]);
  const result = await connectorWith(session).isAuthenticated(context);
  assert.match(result.detail ?? "", /Cloudflare challenge/);
});

test("kofi posts a text article", async () => {
  const session = new FakeSession([
    response(settingsPage, "https://ko-fi.com/settings"),
    response("<html>ok</html>", "https://ko-fi.com/foxartist/posts"),
  ]);
  const result = await connectorWith(session).deliver(withConfig({ Audience: "supporter" }), textPost);
  assert.deepEqual(result, { success: true, externalUrl: "https://ko-fi.com/foxartist/posts" });

  const submit = session.requests[1];
  assert.equal(submit.path, "/Blog/AddBlogPost");
  assert.equal(submit.request?.method, "POST");
  assert.deepEqual(submit.request?.headers, { referer: "https://ko-fi.com/", requestverificationtoken: "tok-abc" });
  const form = submit.request?.body as FormData;
  assert.equal(form.get("__RequestVerificationToken"), "tok-abc");
  assert.equal(form.get("blogPostTitle"), "Update");
  assert.equal(form.get("postBody"), "Hello there");
  assert.equal(form.get("tags"), "news,art");
  assert.equal(form.get("postAudience"), "supporter");
  assert.equal(form.get("submit"), "publish");
});

test("kofi surfaces an article rejection", async () => {
  const session = new FakeSession([
    response(settingsPage, "https://ko-fi.com/settings"),
    response(JSON.stringify({ success: false, error: "e", friendly_error_message: "Too short" }), "https://ko-fi.com/Blog/AddBlogPost"),
  ]);
  const result = await connectorWith(session).deliver(context, textPost);
  assert.equal(result.success, false);
  assert.match(result.error ?? "", /Too short/);
});

test("kofi posts images as a gallery item", async () => {
  const session = new FakeSession([
    response(settingsPage, "https://ko-fi.com/settings"),
    response(JSON.stringify([{ ExternalId: "up-1" }]), "https://ko-fi.com/api/media/gallery-item/upload"),
    response(JSON.stringify({ success: true }), "https://ko-fi.com/Gallery/AddGalleryItem"),
  ]);
  const result = await connectorWith(session).deliver(context, imagePost);
  assert.deepEqual(result, { success: true });

  const upload = session.requests[1];
  assert.equal(upload.path, "/api/media/gallery-item/upload?throwOnError=true");
  const uploadForm = upload.request?.body as FormData;
  assert.equal(uploadForm.get("filenames"), "file.png");
  assert.equal((uploadForm.get("file[0]") as File).name, "file.png");

  const gallery = session.requests[2];
  assert.equal(gallery.path, "/Gallery/AddGalleryItem");
  const body = JSON.parse(gallery.request?.body as string);
  assert.equal(body.Title, "Update");
  assert.equal(body.Description, "Hello there");
  assert.equal(body.Audience, "public");
  assert.deepEqual(body.ImageUploadIds, ["up-1"]);
  assert.equal((gallery.request?.headers as Record<string, string>)["content-type"], "application/json");
});

test("kofi fails when the gallery post is not accepted", async () => {
  const session = new FakeSession([
    response(settingsPage, "https://ko-fi.com/settings"),
    response(JSON.stringify([{ ExternalId: "up-1" }]), "https://ko-fi.com/api/media/gallery-item/upload"),
    response(JSON.stringify({ success: false }), "https://ko-fi.com/Gallery/AddGalleryItem"),
  ]);
  const result = await connectorWith(session).deliver(context, imagePost);
  assert.equal(result.success, false);
  assert.match(result.error ?? "", /rejected the gallery post/);
});

test("kofi fails when an upload returns no id", async () => {
  const session = new FakeSession([
    response(settingsPage, "https://ko-fi.com/settings"),
    response("<html>nope</html>", "https://ko-fi.com/api/media/gallery-item/upload"),
  ]);
  const result = await connectorWith(session).deliver(context, imagePost);
  assert.match(result.error ?? "", /no upload id/);
});

test("kofi validates the post before any request", async () => {
  const connector = connectorWith(new FakeSession([]));
  assert.match((await connector.deliver(context, { ...textPost, title: null })).error ?? "", /require a title/);
  assert.match((await connector.deliver(withConfig({ Audience: "bogus" }), textPost)).error ?? "", /invalid Ko-fi audience/);
  const webp = { ...imagePost, media: [{ ...imagePost.media[0], contentType: "image/webp" }] };
  assert.match((await connector.deliver(context, webp)).error ?? "", /JPEG, PNG, and GIF/);
  const many = { ...imagePost, media: Array.from({ length: 11 }, () => imagePost.media[0]) };
  assert.match((await connector.deliver(context, many)).error ?? "", /at most 10/);
});

test("kofi requires a logged-in session and a form token to post", async () => {
  const loggedOut = new FakeSession([response("<html>Login</html>", "https://ko-fi.com/account/login")]);
  assert.match((await connectorWith(loggedOut).deliver(context, textPost)).error ?? "", /not logged in/);

  const noToken = new FakeSession([response('<html><li id="profile-tab"></li></html>', "https://ko-fi.com/settings")]);
  assert.match((await connectorWith(noToken).deliver(context, textPost)).error ?? "", /form token was not found/);
});
