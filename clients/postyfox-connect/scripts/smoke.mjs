/**
 * Drives popup.js against a stubbed browser: a minimal DOM, a stubbed extension API, and a routed
 * fetch. It exercises the states the popup can land in, so the one-click flow can be checked without
 * loading the extension into Chrome.
 *
 *   node clients/postyfox-connect/scripts/smoke.mjs
 */
import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import vm from "node:vm";

const source = await readFile(new URL("../browser-extension/popup.js", import.meta.url), "utf8");

const FUR_AFFINITY = {
  connectorId: null,
  serviceDefinitionId: "FurAffinity",
  platform: "FurAffinity",
  displayName: "FurAffinity",
  siteUrl: "https://www.furaffinity.net/",
  loginUrl: "https://www.furaffinity.net/login",
  cookieNames: ["a", "b"],
};

const SELECTORS = [
  "#dev-mode",
  "#status",
  "#target-row",
  "#target",
  "#action",
  "#pairing-token",
  "#manual-connect",
  "#cookie-summary",
  "#origin-summary",
];

function element(tag = "div") {
  return {
    tag,
    children: [],
    listeners: {},
    hidden: false,
    disabled: false,
    checked: false,
    value: "",
    textContent: "",
    className: "",
    addEventListener(type, handler) {
      (this.listeners[type] ??= []).push(handler);
    },
    replaceChildren(...nodes) {
      this.children = nodes;
    },
  };
}

/** Lets queued promise chains inside popup.js run to completion before asserting. */
async function settle() {
  for (let i = 0; i < 30; i += 1) await new Promise((resolve) => setTimeout(resolve, 0));
}

/**
 * @param {object} options
 * @param {boolean} options.signedIn        PostyFox recognises the session
 * @param {object|null} options.cookies     what the site has set, or null for "no access"
 * @param {string|null} options.connectorId the user's existing connector, if any
 * @param {boolean} [options.devMode]       Dev switch already persisted on
 */
function harness({ signedIn, cookies, connectorId, devMode = false }) {
  const nodes = Object.fromEntries(SELECTORS.map((selector) => [selector, element()]));
  const requests = [];
  const openedTabs = [];
  const storage = devMode ? { devMode: true } : {};

  const respond = (status, body) => ({
    status,
    ok: status >= 200 && status < 300,
    type: "basic",
    json: async () => body,
  });

  const sandbox = {
    URL,
    console,
    window: { close() {} },
    document: {
      listeners: {},
      querySelector: (selector) => nodes[selector] ?? null,
      createElement: (tag) => element(tag),
      addEventListener(type, handler) {
        (this.listeners[type] ??= []).push(handler);
      },
    },
    chrome: {
      storage: {
        local: {
          get: async (key) => (key in storage ? { [key]: storage[key] } : {}),
          set: async (values) => Object.assign(storage, values),
        },
      },
      permissions: {
        contains: async () => cookies !== null,
        request: async () => true,
      },
      cookies: {
        getAll: async () =>
          Object.entries(cookies ?? {}).map(([name, value]) => ({ name, value })),
      },
      tabs: {
        query: async () => [{ url: "https://www.furaffinity.net/msg/others/" }],
        create: async ({ url }) => openedTabs.push(url),
      },
    },
    fetch: async (url, init) => {
      requests.push({ url, method: init.method, body: init.body ? JSON.parse(init.body) : null });
      const { pathname } = new URL(url);
      if (pathname.endsWith("/cookie-pairing/targets"))
        return signedIn
          ? respond(200, [{ ...FUR_AFFINITY, connectorId }])
          : respond(401, { error: "unauthorized" });
      if (pathname.endsWith("/cookie-pairing/sites")) return respond(200, [FUR_AFFINITY]);
      if (pathname.endsWith("/cookie-pairing/pair"))
        return respond(200, { connectorId: connectorId ?? "created-id", displayName: "FurAffinity" });
      if (pathname.endsWith("/cookie-pairing/complete")) return respond(204, null);
      return respond(404, { error: `unrouted ${pathname}` });
    },
  };

  new vm.Script(source).runInNewContext(sandbox);

  return {
    nodes,
    requests,
    openedTabs,
    storage,
    status: () => nodes["#status"].textContent,
    button: () => (nodes["#action"].hidden ? null : nodes["#action"].textContent),
    async load() {
      for (const handler of sandbox.document.listeners.DOMContentLoaded ?? []) handler();
      await settle();
    },
    async click(selector = "#action") {
      for (const handler of nodes[selector].listeners.click ?? []) handler();
      await settle();
    },
    async toggleDev() {
      nodes["#dev-mode"].checked = !nodes["#dev-mode"].checked;
      for (const handler of nodes["#dev-mode"].listeners.change ?? []) handler();
      await settle();
    },
  };
}

const scenarios = {
  async "one click connects an existing connector"() {
    const app = harness({
      signedIn: true,
      cookies: { a: "session-a", b: "session-b", tracking: "ignored" },
      connectorId: "conn-1",
    });
    await app.load();

    assert.equal(app.button(), "Connect FurAffinity");
    assert.match(app.status(), /Ready to connect FurAffinity/);
    assert.equal(app.nodes["#target-row"].hidden, true, "a single site needs no picker");

    await app.click();

    const pair = app.requests.find((r) => r.url.endsWith("/cookie-pairing/pair"));
    assert.ok(pair, "the click should pair");
    assert.equal(pair.url, "https://cp.postyfox.com/api/connectors/cookie-pairing/pair");
    assert.equal(pair.body.connectorId, "conn-1");
    assert.deepEqual(pair.body.cookies, { a: "session-a", b: "session-b" });
    assert.match(app.status(), /is connected to PostyFox/);
    assert.equal(app.button(), null, "nothing left to do");
  },

  async "one click creates the connector when the user has none"() {
    const app = harness({
      signedIn: true,
      cookies: { a: "session-a", b: "session-b" },
      connectorId: null,
    });
    await app.load();

    assert.match(app.status(), /Ready to add FurAffinity to PostyFox/);
    await app.click();

    const pair = app.requests.find((r) => r.url.endsWith("/cookie-pairing/pair"));
    assert.equal(pair.body.connectorId, null);
    assert.equal(pair.body.platform, "FurAffinity");
  },

  async "a missing website session asks for that login"() {
    const app = harness({ signedIn: true, cookies: {}, connectorId: "conn-1" });
    await app.load();

    assert.equal(app.button(), "Log in to www.furaffinity.net");
    assert.match(app.status(), /Log in to www\.furaffinity\.net first/);

    await app.click();

    assert.deepEqual(app.openedTabs, ["https://www.furaffinity.net/login"]);
    assert.ok(
      !app.requests.some((r) => r.url.endsWith("/cookie-pairing/pair")),
      "nothing should be sent without a session",
    );
  },

  async "a missing PostyFox session asks for sign-in and still reads the site"() {
    const app = harness({
      signedIn: false,
      cookies: { a: "session-a", b: "session-b" },
      connectorId: null,
    });
    await app.load();

    assert.ok(
      app.requests.some((r) => r.url.endsWith("/cookie-pairing/sites")),
      "should fall back to the public site list",
    );
    assert.equal(app.button(), "Sign in to PostyFox");

    await app.click();
    assert.deepEqual(app.openedTabs, ["https://cp.postyfox.com/"]);
  },

  async "both logins missing are reported together"() {
    const app = harness({ signedIn: false, cookies: {}, connectorId: null });
    await app.load();

    assert.match(app.status(), /Log in to PostyFox and www\.furaffinity\.net first/);
    assert.equal(app.button(), "Sign in to PostyFox");
  },

  async "the dev switch retargets everything and is remembered"() {
    const app = harness({
      signedIn: true,
      cookies: { a: "session-a", b: "session-b" },
      connectorId: "conn-1",
    });
    await app.load();
    await app.toggleDev();

    assert.equal(app.storage.devMode, true, "the choice should persist");
    assert.equal(app.nodes["#origin-summary"].textContent, "dev.postyfox.com");

    await app.click();

    const pair = app.requests.findLast((r) => r.url.endsWith("/cookie-pairing/pair"));
    assert.equal(pair.url, "https://dev.postyfox.com/api/connectors/cookie-pairing/pair");
  },

  async "a persisted dev choice is applied on open"() {
    const app = harness({
      signedIn: true,
      cookies: { a: "session-a", b: "session-b" },
      connectorId: "conn-1",
      devMode: true,
    });
    await app.load();

    assert.ok(app.requests.every((r) => r.url.startsWith("https://dev.postyfox.com/")));
  },

  async "the token fallback works while signed out of PostyFox"() {
    const app = harness({
      signedIn: false,
      cookies: { a: "session-a", b: "session-b" },
      connectorId: null,
    });
    await app.load();
    app.nodes["#pairing-token"].value = "  token-123  ";

    await app.click("#manual-connect");

    const complete = app.requests.find((r) => r.url.endsWith("/cookie-pairing/complete"));
    assert.equal(complete.body.pairingToken, "token-123");
    assert.deepEqual(complete.body.cookies, { a: "session-a", b: "session-b" });
    assert.match(app.status(), /is connected to PostyFox/);
    assert.equal(app.nodes["#pairing-token"].value, "", "the token should not linger");
  },

  async "the token fallback refuses an empty token"() {
    const app = harness({
      signedIn: false,
      cookies: { a: "session-a", b: "session-b" },
      connectorId: null,
    });
    await app.load();

    await app.click("#manual-connect");

    assert.match(app.status(), /Paste the one-time pairing token/);
    assert.equal(app.nodes["#status"].className, "error");
  },

  async "a site the extension cannot read asks for permission first"() {
    const app = harness({ signedIn: true, cookies: null, connectorId: "conn-1" });
    await app.load();

    assert.equal(app.button(), "Allow access to www.furaffinity.net");
  },
};

let failures = 0;
for (const [name, scenario] of Object.entries(scenarios)) {
  try {
    await scenario();
    console.log(`  ok  ${name}`);
  } catch (error) {
    failures += 1;
    console.error(`fail  ${name}\n      ${error.message}`);
  }
}

if (failures > 0) {
  console.error(`\n${failures} PostyFox Connect scenario(s) failed`);
  process.exit(1);
}
console.log(`\nAll ${Object.keys(scenarios).length} PostyFox Connect scenarios passed`);
